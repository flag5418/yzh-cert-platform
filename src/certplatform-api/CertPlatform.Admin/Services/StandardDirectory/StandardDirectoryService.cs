extern alias SharedEntities;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.Models;
using YZH.Core.DataBase.Services;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Queue;
using SharedEntities::YZH.Entity.Admin.Platform.Dir;
using SharedEntities::YZH.Entity.Admin.Platform.Cert;
using SharedEntities::YZH.Entity.Admin.Platform.Sys;

namespace CertPlatform.Admin.Services.StandardDirectory;

/// <summary>
/// 标准目录管理核心服务
/// 职责：组织树、文件夹/文件 CRUD、上传 4 步、下载
/// ORM：IDbOrm（SqlSugar）
/// 存储：IObjectStorage（MinIO/阿里云 OSS）
/// </summary>
public class StandardDirectoryService
{
    private readonly IDbOrm _db;
    private readonly IObjectStorage _storage;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StandardDirectoryService> _logger;
    private readonly CodeGeneratorService _codeGenerator;
    private readonly QueueManager _queueManager;

    private readonly JsonSerializerOptions _payloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    // 服务器端文件类型白名单（跳过 .DS_Store 等）
    private static readonly HashSet<string> IgnoredFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".DS_Store", ".Thumbs.db", "desktop.ini"
    };

    public StandardDirectoryService(
        IDbOrm db,
        IObjectStorage storage,
        IConfiguration configuration,
        ILogger<StandardDirectoryService> logger,
        CodeGeneratorService codeGenerator,
        QueueManager queueManager)
    {
        _db = db;
        _storage = storage;
        _configuration = configuration;
        _logger = logger;
        _codeGenerator = codeGenerator;
        _queueManager = queueManager;
    }

    #region 组织树（org → standard → phase）

    /// <summary>
    /// 获取三级组织树：认证机构 → 标准 → 阶段
    /// </summary>
    public async Task<List<object>> GetOrganizationTreeAsync()
    {
        var orgs = (await _db.GetListAsync<CertificationBody>(x => x.IsValid == 1)).Data ?? new();
        var standards = (await _db.GetListAsync<ISOStandard>(x => x.IsValid == 1)).Data ?? new();
        var stages = (await _db.GetListAsync<CertStage>(x => x.IsValid == 1)).Data ?? new();
        var orgStandards = (await _db.GetListAsync<CertOrgStandard>()).Data ?? new();
        var orgStages = (await _db.GetListAsync<CertOrgStage>()).Data ?? new();

        var tree = new List<object>();

        foreach (var org in orgs)
        {
            var orgNode = new Dictionary<string, object>
            {
                ["id"] = org.Code,
                ["label"] = org.Name,
                ["type"] = "organization",
                ["cbCode"] = org.Code,
                ["children"] = new List<object>()
            };
            var orgChildren = (List<object>)orgNode["children"];

            // 该机构关联的标准
            var orgStdCodes = orgStandards
                .Where(x => x.OrgCode == org.Code)
                .Select(x => x.StandardCode).ToList();
            var linkedStandards = standards
                .Where(x => orgStdCodes.Contains(x.Code)).ToList();

            foreach (var std in linkedStandards)
            {
                var stdNode = new Dictionary<string, object>
                {
                    ["id"] = $"{org.Code}|{std.StandardCode}",
                    ["label"] = $"{std.StandardCode} - {std.StandardName}",
                    ["type"] = "standard",
                    ["cbCode"] = org.Code,
                    ["stdCode"] = std.Code,
                    ["standardCode"] = std.StandardCode,
                    ["standardName"] = std.StandardName,
                    ["children"] = new List<object>()
                };
                var stdChildren = (List<object>)stdNode["children"];

                // 该机构+标准关联的阶段（StandardCode==null 表示适用于所有标准）
                var orgStageCodes = orgStages
                    .Where(x => x.OrgCode == org.Code
                        && (x.StandardCode == null || x.StandardCode == std.Code))
                    .Select(x => x.StageCode).ToList();
                var linkedStages = stages
                    .Where(x => orgStageCodes.Contains(x.StageCode)).ToList();

                foreach (var stage in linkedStages)
                {
                    var phaseNode = new Dictionary<string, object>
                    {
                        ["id"] = $"{org.Code}|{std.StandardCode}|{stage.StageCode}",
                        ["label"] = $"{stage.StageCode} - {stage.StageName}",
                        ["type"] = "phase",
                        ["cbCode"] = org.Code,
                        ["stdCode"] = std.Code,
                        ["standardCode"] = std.StandardCode,
                        ["phaseCode"] = stage.StageCode,
                        ["phaseName"] = stage.StageName
                    };
                    stdChildren.Add(phaseNode);
                }
                orgChildren.Add(stdNode);
            }
            tree.Add(orgNode);
        }
        return tree;
    }

    #endregion

    #region 目录配置 CRUD

    public async Task<List<StandardDirectoryConfig>> GetConfigsAsync()
    {
        return (await _db.GetListAsync<StandardDirectoryConfig>(x => x.Enable == true)).Data ?? new();
    }

    public async Task<StandardDirectoryConfig?> GetConfigAsync(string directoryCode)
    {
        return (await _db.GetOneAsync<StandardDirectoryConfig>(x => x.DirectoryCode == directoryCode)).Data;
    }

    public async Task<StandardDirectoryConfig> CreateConfigAsync(StandardDirectoryConfig config)
    {
        config.Code = Guid.NewGuid().ToString("N");
        config.DirectoryCode ??= _codeGenerator.GenerateDirectoryCode(config.StandardCode, config.PhaseCode);
        config.Enable = true;
        config.Status = "draft";
        config.CreateDate = DateTime.Now;
        return (await _db.InsertAsync(config)).Data;
    }

    public async Task<bool> UpdateConfigAsync(StandardDirectoryConfig config)
    {
        config.ModifyDate = DateTime.Now;
        var result = await _db.UpdateAsync(config);
        return result.Code == 200;
    }

    public async Task<bool> DeleteConfigAsync(string directoryCode)
    {
        var config = (await _db.GetOneAsync<StandardDirectoryConfig>(x => x.DirectoryCode == directoryCode)).Data;
        if (config == null) return false;
        config.Enable = false;
        config.DeleteTime = DateTime.Now;
        var result = await _db.UpdateAsync(config);
        return result.Code == 200;
    }

    #endregion

    #region 文件夹 CRUD

    /// <summary>
    /// 获取文件夹树（仅有效文件夹）
    /// </summary>
    public async Task<List<StandardDirectoryFolder>> GetFolderTreeAsync(string directoryCode)
    {
        var folders = (await _db.GetListAsync<StandardDirectoryFolder>(
            x => x.DirectoryCode == directoryCode && x.Enable == true && x.IsValid == 1)).Data ?? new();

        var rootFolders = folders.Where(x => string.IsNullOrEmpty(x.ParentCode))
            .OrderBy(x => x.SortOrder).ToList();

        foreach (var root in rootFolders)
            root.Children = GetChildFolders(folders, root.FolderCode);

        return rootFolders;
    }

    private List<StandardDirectoryFolder> GetChildFolders(
        List<StandardDirectoryFolder> allFolders, string parentCode)
    {
        var children = allFolders.Where(x => x.ParentCode == parentCode)
            .OrderBy(x => x.SortOrder).ToList();
        foreach (var child in children)
            child.Children = GetChildFolders(allFolders, child.FolderCode);
        return children;
    }

    /// <summary>
    /// 从 DirectoryCode 解析 StandardCode 和 PhaseCode
    /// 目录编码格式: SDC-{standardCode}|{phaseCode}
    /// </summary>
    private static (string? standardCode, string? phaseCode) ParseDirectoryCode(string? directoryCode)
    {
        if (string.IsNullOrEmpty(directoryCode)) return (null, null);
        // SDC-ISO134852016|AP → standardCode=ISO134852016, phaseCode=AP
        var prefix = "SDC-";
        if (!directoryCode.StartsWith(prefix)) return (null, null);
        var body = directoryCode.Substring(prefix.Length);
        var parts = body.Split('|', 2);
        if (parts.Length == 2) return (parts[0], parts[1]);
        return (body, null);
    }

    /// <summary>
    /// 创建文件夹
    /// </summary>
    public async Task<(bool ok, string? error, StandardDirectoryFolder? folder)> CreateFolderAsync(
        StandardDirectoryFolder folder)
    {
        // 队列锁检查
        var lockErr = await GetQueueLockErrorAsync(folder.DirectoryCode);
        if (lockErr != null) return (false, lockErr, null);

        folder.Code = Guid.NewGuid().ToString("N");
        int maxSeq = await GetMaxSequenceAsync(folder.DirectoryCode, folder.Depth);
        folder.FolderCode = _codeGenerator.GenerateFolderCode(folder.DirectoryCode, folder.Depth, maxSeq + 1);
        folder.FullPath = await BuildFolderFullPathAsync(folder);
        folder.IsValid = 1;
        folder.Enable = true;
        folder.Status = "draft";
        folder.CreateDate = DateTime.Now;

        // 重试机制：处理唯一编码冲突
        for (int attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                var result = await _db.InsertAsync(folder);
                if (result.Data != null) return (true, null, result.Data);
                return (false, "创建失败", null);
            }
            catch (Exception ex) when (IsDuplicateKeyError(ex))
            {
                folder.FolderCode = _codeGenerator.GenerateFolderCode(
                    folder.DirectoryCode, folder.Depth, maxSeq + 1 + attempt + 1);
                continue;
            }
        }
        return (false, "创建失败：无法生成唯一编码，请重试", null);
    }

    /// <summary>
    /// 更新文件夹（重命名）
    /// </summary>
    public async Task<(bool ok, string? error)> UpdateFolderAsync(StandardDirectoryFolder folder)
    {
        var lockErr = await GetQueueLockErrorAsync(folder.DirectoryCode);
        if (lockErr != null) return (false, lockErr);

        var existing = (await _db.GetOneAsync<StandardDirectoryFolder>(
            x => x.FolderCode == folder.FolderCode && x.Enable == true)).Data;
        if (existing == null) return (false, "文件夹不存在");

        existing.FolderName = folder.FolderName;
        existing.ModifyDate = DateTime.Now;
        await _db.UpdateAsync(existing);
        return (true, null);
    }

    /// <summary>
    /// 删除文件夹（递归软删除 + MinIO 清理）
    /// </summary>
    public async Task<(bool ok, string? error, int foldersDeleted, int filesDeleted)> DeleteFolderAsync(
        string folderCode)
    {
        var folder = (await _db.GetOneAsync<StandardDirectoryFolder>(
            x => x.FolderCode == folderCode && x.Enable == true)).Data;
        if (folder == null) return (false, "文件夹不存在", 0, 0);

        var lockErr = await GetQueueLockErrorAsync(folder.DirectoryCode);
        if (lockErr != null) return (false, lockErr, 0, 0);

        var (foldersDeleted, filesDeleted) = await DeleteFolderRecursiveAsync(folderCode);
        return (true, null, foldersDeleted, filesDeleted);
    }

    private async Task<(int foldersDeleted, int filesDeleted)> DeleteFolderRecursiveAsync(string folderCode)
    {
        int foldersDeleted = 0, filesDeleted = 0;

        // 递归删除子文件夹
        var children = (await _db.GetListAsync<StandardDirectoryFolder>(
            x => x.ParentCode == folderCode && x.Enable == true)).Data ?? new();
        foreach (var child in children)
        {
            var (fd, fild) = await DeleteFolderRecursiveAsync(child.FolderCode);
            foldersDeleted += fd;
            filesDeleted += fild;
        }

        // 删除文件夹下的文件（MinIO + DB）
        var files = (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.FolderCode == folderCode && x.Enable == true)).Data ?? new();
        foreach (var file in files)
        {
            await DeleteFileFromStorageAsync(file);
            file.Enable = false;
            file.Status = "archived";
            file.DeleteTime = DateTime.Now;
            await _db.UpdateAsync(file);
            filesDeleted++;
        }

        // 软删除文件夹
        var folderEntity = (await _db.GetOneAsync<StandardDirectoryFolder>(
            x => x.FolderCode == folderCode)).Data;
        if (folderEntity != null)
        {
            folderEntity.Enable = false;
            folderEntity.Status = "archived";
            folderEntity.DeleteTime = DateTime.Now;
            await _db.UpdateAsync(folderEntity);
            foldersDeleted++;
        }

        return (foldersDeleted, filesDeleted);
    }

    #endregion

    #region 文件 CRUD

    /// <summary>
    /// 获取文件列表
    /// </summary>
    public async Task<List<StandardDirectoryFile>> GetFilesAsync(string? folderCode = null)
    {
        if (string.IsNullOrEmpty(folderCode))
        {
            return (await _db.GetListAsync<StandardDirectoryFile>(
                x => x.Enable == true && x.IsValid == 1)).Data ?? new();
        }
        return (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.FolderCode == folderCode && x.Enable == true && x.IsValid == 1)).Data ?? new();
    }

    /// <summary>
    /// 获取目录根级别的文件（FolderCode 为空或不存在的文件）
    /// </summary>
    public async Task<List<StandardDirectoryFile>> GetRootFilesAsync(string directoryCode)
    {
        // 使用原生 SQL 绕过全局过滤（根级文件可能 IsValid=0 处于上传中）
        var files = (await _db.SqlQueryAsync<StandardDirectoryFile>(
            "SELECT * FROM cert_standard_directory_file WHERE DirectoryCode=@dir AND (FolderCode IS NULL OR FolderCode='') AND IsDeleted=0 AND UploadStatus IN ('uploaded','active') ORDER BY CreateDate DESC",
            new { dir = directoryCode })).Data ?? new();
        return files;
    }

    /// <summary>
    /// 更新文件信息
    /// </summary>
    public async Task<(bool ok, string? error)> UpdateFileAsync(StandardDirectoryFile file)
    {
        var existing = (await _db.QueryFirstOrDefaultAsync<StandardDirectoryFile>(
            "SELECT * FROM cert_standard_directory_file WHERE FileCode=@fileCode AND Enable=1 AND IsDeleted=0",
            new { fileCode = file.FileCode })).Data;
        if (existing == null) return (false, "文件不存在");

        existing.FileName = file.FileName;
        existing.Description = file.Description;
        existing.ModifyDate = DateTime.Now;
        await _db.UpdateAsync(existing);
        return (true, null);
    }

    /// <summary>
    /// 删除文件（MinIO + DB 软删除）
    /// </summary>
    public async Task<(bool ok, string? error)> DeleteFileAsync(string fileCode)
    {
        var lockErr = await GetFileLockErrorAsync(fileCode);
        if (lockErr != null) return (false, lockErr);

        var file = (await _db.QueryFirstOrDefaultAsync<StandardDirectoryFile>(
            "SELECT * FROM cert_standard_directory_file WHERE FileCode=@fileCode AND Enable=1 AND IsDeleted=0",
            new { fileCode })).Data;
        if (file == null) return (false, "文件不存在");

        await DeleteFileFromStorageAsync(file);
        file.Enable = false;
        file.Status = "archived";
        file.DeleteTime = DateTime.Now;
        await _db.UpdateAsync(file);
        return (true, null);
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    public async Task<(Stream stream, string contentType, string fileName)?> DownloadFileAsync(string storagePath)
    {
        try
        {
            var objectName = storagePath.TrimStart('/');
            var (stream, contentType) = await _storage.DownloadAsync(objectName);
            var fileName = Path.GetFileName(objectName.Replace('\\', '/'));
            return (stream, contentType ?? "application/octet-stream", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载文件失败: {StoragePath}", storagePath);
            return null;
        }
    }

    private async Task DeleteFileFromStorageAsync(StandardDirectoryFile file)
    {
        if (!string.IsNullOrEmpty(file.StoragePath))
        {
            try { await _storage.DeleteAsync(file.StoragePath.TrimStart('/')); }
            catch (Exception ex) { _logger.LogWarning(ex, "MinIO 删除原始文件失败: {Path}", file.StoragePath); }
        }
        if (!string.IsNullOrEmpty(file.ConvertedStoragePath))
        {
            try { await _storage.DeleteAsync(file.ConvertedStoragePath.TrimStart('/')); }
            catch (Exception ex) { _logger.LogWarning(ex, "MinIO 删除转换文件失败: {Path}", file.ConvertedStoragePath); }
        }
    }

    #endregion

    #region 上传 4 步

    /// <summary>
    /// Step 1: 上传预初始化 — 生成编码、创建预记录、返回增强清单
    /// </summary>
    public async Task<(bool ok, string? error, UploadManifestResponse? response)> UploadInitAsync(
        UploadManifestRequest manifest)
    {
        // 0. 从 DirectoryCode 解析 StandardCode / PhaseCode（前端可能不传）
        if (string.IsNullOrEmpty(manifest.StandardCode) || string.IsNullOrEmpty(manifest.PhaseCode))
        {
            var parsed = ParseDirectoryCode(manifest.DirectoryCode);
            if (parsed.standardCode != null) manifest.StandardCode = parsed.standardCode;
            if (parsed.phaseCode != null) manifest.PhaseCode = parsed.phaseCode;
        }

        // 1. 验证或自动创建 StandardDirectoryConfig
        var config = (await _db.GetOneAsync<StandardDirectoryConfig>(
            x => x.DirectoryCode == manifest.DirectoryCode)).Data;
        if (config == null)
        {
            config = new StandardDirectoryConfig
            {
                Code = Guid.NewGuid().ToString("N"),
                DirectoryCode = manifest.DirectoryCode,
                StandardCode = manifest.StandardCode,
                PhaseCode = manifest.PhaseCode,
                Enable = true,
                Status = "draft",
                CreateDate = DateTime.Now
            };
            await _db.InsertAsync(config);
        }

        // 2. 队列互斥检查
        var queueLockErr = await GetQueueLockErrorAsync(manifest.DirectoryCode);
        if (queueLockErr != null) return (false, queueLockErr, null);

        // 3. 生成 TaskId
        var taskId = Guid.NewGuid().ToString("N");

        // 4. 清理孤儿数据（上次失败上传的预创建记录）
        await CleanupOrphanDataAsync(manifest.DirectoryCode, taskId);

        // 5. 处理文件夹（按深度排序，复用或创建）
        var sortedFolders = manifest.Folders
            .OrderBy(f => f.Path.Count(c => c == '/'))
            .ThenBy(f => f.Path).ToList();
        var enhancedFolders = new List<EnhancedFolderItem>();
        var folderMap = new Dictionary<string, string>(); // FullPath → FolderCode

        int depthCounter = 1;
        int seqCounter = 1;
        foreach (var folder in sortedFolders)
        {
            var existing = (await _db.GetOneAsync<StandardDirectoryFolder>(
                x => x.DirectoryCode == manifest.DirectoryCode
                    && x.FullPath == folder.Path && x.IsValid == 1)).Data;

            if (existing != null)
            {
                enhancedFolders.Add(new EnhancedFolderItem
                {
                    FolderCode = existing.FolderCode,
                    FolderName = existing.FolderName,
                    ParentCode = existing.ParentCode,
                    Depth = existing.Depth,
                    FullPath = existing.FullPath,
                    Mode = "reuse"
                });
                folderMap[folder.Path] = existing.FolderCode;
            }
            else
            {
                var parts = folder.Path.Split('/');
                var folderName = parts.Last();
                var parentPath = parts.Length > 1 ? string.Join("/", parts.Take(parts.Length - 1)) : null;
                var depth = parts.Length;
                var parentCode = parentPath != null && folderMap.ContainsKey(parentPath)
                    ? folderMap[parentPath] : null;

                var folderCode = _codeGenerator.GenerateFolderCode(
                    manifest.DirectoryCode, depth, seqCounter++);

                var newFolder = new StandardDirectoryFolder
                {
                    Code = Guid.NewGuid().ToString("N"),
                    FolderCode = folderCode,
                    DirectoryCode = manifest.DirectoryCode,
                    ParentCode = parentCode,
                    FolderName = folderName,
                    Depth = depth,
                    SortOrder = seqCounter,
                    IsValid = 0,
                    TaskId = taskId,
                    FullPath = folder.Path,
                    Enable = true,
                    Status = "draft",
                    CreateDate = DateTime.Now
                };
                await _db.InsertAsync(newFolder);

                enhancedFolders.Add(new EnhancedFolderItem
                {
                    FolderCode = folderCode,
                    FolderName = folderName,
                    ParentCode = parentCode,
                    Depth = depth,
                    FullPath = folder.Path,
                    Mode = "create"
                });
                folderMap[folder.Path] = folderCode;
            }
        }

        // 6. 处理文件（创建或替换）
        var enhancedFiles = new List<EnhancedFileItem>();
        long totalSize = 0;

        for (int i = 0; i < manifest.Files.Count; i++)
        {
            var fileItem = manifest.Files[i];
            var fullPath = fileItem.RelativePath;
            var fileName = fileItem.FileName;

            // 服务器端白名单过滤
            var ext = Path.GetExtension(fileName);
            if (IgnoredFileExtensions.Contains(ext)) continue;

            // 解析父文件夹
            var parentDir = Path.GetDirectoryName(fullPath)?.Replace('\\', '/');
            var folderCode = parentDir != null && folderMap.ContainsKey(parentDir)
                ? folderMap[parentDir] : "";

            // 查找已有文件
            var existingFile = (await _db.GetOneAsync<StandardDirectoryFile>(
                x => x.DirectoryCode == manifest.DirectoryCode
                    && x.FullPath == fullPath && x.IsValid == 1)).Data;

            var storagePath = _codeGenerator.GenerateStandardDirectoryPath(
                manifest.OrgCode, manifest.StandardCode, manifest.PhaseCode,
                parentDir, fileName);

            if (existingFile != null)
            {
                // 替换模式
                var oldStoragePath = existingFile.StoragePath;
                existingFile.UploadStatus = "replacing";
                existingFile.TaskId = taskId;
                existingFile.StoragePath = storagePath;
                existingFile.Remark = $"[upload-replace:{taskId}]";
                await _db.UpdateAsync(existingFile);

                enhancedFiles.Add(new EnhancedFileItem
                {
                    Index = i,
                    FileCode = existingFile.FileCode,
                    FileName = fileName,
                    RelativePath = fullPath,
                    FullPath = fullPath,
                    FileSize = fileItem.FileSize,
                    MimeType = fileItem.MimeType,
                    StoragePath = storagePath,
                    ParentFolderCode = folderCode,
                    Mode = "replace",
                    ExistingFileCode = existingFile.FileCode,
                    ExistingFileId = existingFile.Id,
                    OldStoragePath = oldStoragePath,
                    Status = "pending"
                });
            }
            else
            {
                // 创建模式
                var fileCode = _codeGenerator.GenerateFileCode(folderCode ?? "", fileName);
                var newFile = new StandardDirectoryFile
                {
                    Code = Guid.NewGuid().ToString("N"),
                    FileCode = fileCode,
                    FolderCode = folderCode,
                    DirectoryCode = manifest.DirectoryCode,
                    FileName = fileName,
                    FileType = ext?.TrimStart('.'),
                    StoragePath = storagePath,
                    FullPath = fullPath,
                    IsValid = 0,
                    UploadStatus = "pending",
                    TaskId = taskId,
                    Enable = true,
                    Status = "draft",
                    CreateDate = DateTime.Now
                };
                await _db.InsertAsync(newFile);

                enhancedFiles.Add(new EnhancedFileItem
                {
                    Index = i,
                    FileCode = fileCode,
                    FileName = fileName,
                    RelativePath = fullPath,
                    FullPath = fullPath,
                    FileSize = fileItem.FileSize,
                    MimeType = fileItem.MimeType,
                    StoragePath = storagePath,
                    ParentFolderCode = folderCode,
                    Mode = "create",
                    Status = "pending"
                });
            }
            totalSize += fileItem.FileSize;
        }

        if (enhancedFiles.Count == 0)
            return (false, "没有有效的文件需要上传", null);

        // 7. 创建上传任务记录
        var uploadTask = new UploadTask
        {
            Code = taskId,
            TaskId = taskId,
            DirectoryCode = manifest.DirectoryCode,
            TotalFiles = enhancedFiles.Count,
            TotalSize = totalSize,
            Status = "initialized",
            CreateDate = DateTime.Now,
            ExpireTime = DateTime.Now.AddMinutes(30)
        };
        await _db.InsertAsync(uploadTask);

        return (true, null, new UploadManifestResponse
        {
            Status = "initialized",
            TaskId = taskId,
            DirectoryCode = manifest.DirectoryCode,
            TotalFiles = enhancedFiles.Count,
            TotalSize = totalSize,
            Folders = enhancedFolders,
            Files = enhancedFiles
        });
    }

    /// <summary>
    /// Step 2: 逐文件上传到 MinIO
    /// </summary>
    public async Task<(bool ok, string? error)> UploadFileAsync(
        Stream fileStream, long fileSize, string fileCode, string taskId)
    {
        // 验证任务（绕过全局过滤：IsValid/IsDeleted 在 SqlSugarDbOrm.GetOneAsync 中自动加，此处用原生 SQL）
        var task = (await _db.QueryFirstOrDefaultAsync<UploadTask>(
            "SELECT * FROM cert_upload_task WHERE TaskId=@taskId AND status='initialized' AND IsDeleted=0",
            new { taskId })).Data;
        if (task == null) return (false, "上传任务不存在或已过期");

        // 验证文件记录（绕过全局过滤：新上传文件 IsValid=0）
        var file = (await _db.QueryFirstOrDefaultAsync<StandardDirectoryFile>(
            "SELECT * FROM cert_standard_directory_file WHERE FileCode=@fileCode AND TaskId=@taskId AND IsDeleted=0",
            new { fileCode, taskId })).Data;
        if (file == null) return (false, "文件编码与任务不匹配");

        var isReplaceMode = file.UploadStatus == "replacing";
        if (!isReplaceMode && (file.IsValid == 1 || file.UploadStatus != "pending"))
            return (false, "文件状态异常");
        if (isReplaceMode && file.UploadStatus != "replacing")
            return (false, "文件状态异常");

        // 上传到 MinIO（使用 DB 中的 StoragePath）
        var objectName = (file.StoragePath ?? "").TrimStart('/');
        if (string.IsNullOrEmpty(objectName))
            return (false, "存储路径未生成");

        try
        {
            var contentType = "application/octet-stream";
            await _storage.UploadAsync(objectName, fileStream, fileSize, contentType);

            // 替换模式：如果路径变了，删除旧对象
            if (isReplaceMode && !string.IsNullOrEmpty(file.Remark))
            {
                var marker = $"[upload-replace:{taskId}]";
                if ((file.Remark ?? "").Contains(marker))
                {
                    // 从 remark 中提取旧路径（简化处理：不做旧路径删除，由 confirm 统一处理）
                }
            }

            // 更新状态
            file.UploadStatus = "uploaded";
            file.FileSize = fileSize; // 以服务端实际大小为准
            await _db.UpdateAsync(file);

            // 更新任务计数
            task.SuccessCount++;
            await _db.UpdateAsync(task);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传文件到 MinIO 失败: {FileCode}", fileCode);
            return (false, $"上传失败：{ex.Message}");
        }
    }

    /// <summary>
    /// Step 3: 确认上传 — 激活文件、创建转换队列
    /// </summary>
    public async Task<(bool ok, string? error, string? convertQueueCode)> UploadConfirmAsync(string taskId)
    {
        var task = (await _db.QueryFirstOrDefaultAsync<UploadTask>(
            "SELECT * FROM cert_upload_task WHERE TaskId=@taskId AND IsDeleted=0",
            new { taskId })).Data;
        if (task == null) return (false, "上传任务不存在", null);

        // 队列锁检查
        var queueLockErr = await GetQueueLockErrorAsync(task.DirectoryCode);
        if (queueLockErr != null) return (false, queueLockErr, null);

        // 检查所有文件是否已上传（绕过全局过滤：文件 IsValid=0 处于上传中）
        var allFiles = (await _db.SqlQueryAsync<StandardDirectoryFile>(
            "SELECT * FROM cert_standard_directory_file WHERE TaskId=@taskId AND IsDeleted=0",
            new { taskId })).Data ?? new();
        var pendingCount = allFiles.Count(x => x.UploadStatus == "pending");
        if (pendingCount > 0)
            return (false, $"还有 {pendingCount} 个文件未上传完成", null);

        // 激活文件夹（绕过全局过滤）
        var folders = (await _db.SqlQueryAsync<StandardDirectoryFolder>(
            "SELECT * FROM cert_standard_directory_folder WHERE TaskId=@taskId AND IsValid=0 AND IsDeleted=0",
            new { taskId })).Data ?? new();
        foreach (var f in folders)
        {
            f.IsValid = 1;
            await _db.UpdateAsync(f);
        }

        // 先找出需要转换的 doc/xls 文件（在激活前，避免内存引用污染）
        var convertibleFiles = allFiles.Where(x =>
            x.UploadStatus == "uploaded" &&
            (x.FileType == "doc" || x.FileType == "xls")).ToList();
        var convertibleCodes = new HashSet<string>(convertibleFiles.Select(f => f.FileCode));

        // 激活文件（非转换文件：IsValid=1, UploadStatus=active；转换文件：保持 IsValid=0）
        foreach (var f in allFiles.Where(x => x.UploadStatus == "uploaded"))
        {
            if (convertibleCodes.Contains(f.FileCode))
            {
                // 转换文件：保持 IsValid=0，稍后设置 ConvertStatus
                f.UploadStatus = "uploaded"; // 保持不变
            }
            else
            {
                // 普通文件：直接激活
                f.IsValid = 1;
                f.UploadStatus = "active";
            }
            f.TaskId = null;
            await _db.UpdateAsync(f);
        }

        string? convertQueueCode = null;
        if (convertibleFiles.Count > 0)
        {
            var specs = convertibleFiles.Select(f => new FileConvertPayload
            {
                FileCode = f.FileCode,
                FileName = f.FileName,
                SourcePath = f.StoragePath,
                TargetPath = _codeGenerator.GenerateConvertedStoragePath(
                    "", "", "", "", f.FileName),
                ConvertType = f.FileType == "doc" ? "doc2docx" : "xls2xlsx"
            }).ToList();

            var req = new QueueManager.CreateQueueRequest
            {
                QueueType = "file_convert",
                QueueName = $"文档转换 - {specs.Count}个文件",
                ScopeKey = task.DirectoryCode,
                SourceType = "upload_task",
                SourceId = taskId,
                ResourceLocks = new List<QueueManager.ResourceLockItem>
                {
                    new() { ResourceTable = QueueManager.RESOURCE_DIR, ResourceCode = task.DirectoryCode }
                },
                Tasks = specs.Select(s => new QueueManager.TaskItem
                {
                    TaskType = "file_convert",
                    Payload = JsonSerializer.Serialize(s, _payloadJsonOptions),
                    TaskId = taskId
                }).ToList()
            };

            var (ok, error, queueCode, _) = await _queueManager.CreateQueueAsync(req);
            if (ok && queueCode != null)
            {
                convertQueueCode = queueCode;
                // 转换中的文件重新隐藏
                foreach (var f in convertibleFiles)
                {
                    f.IsValid = 0;
                    f.ConvertStatus = "pending";
                    f.ConvertMessage = null;
                    f.TaskId = null;
                    await _db.UpdateAsync(f);
                }
            }
        }

        // 更新任务状态
        task.Status = "completed";
        task.ModifyDate = DateTime.Now;
        await _db.UpdateAsync(task);

        return (true, null, convertQueueCode);
    }

    /// <summary>
    /// Step 4: 回滚上传 — 删除预创建记录和 MinIO 对象
    /// </summary>
    public async Task<(bool ok, string? error, int deleted, int restored)> UploadCancelAsync(string taskId)
    {
        // 取消关联的转换队列
        var activeQueues = (await _db.GetListAsync<YzhQueue>(
            x => x.SourceType == "upload_task" && x.SourceId == taskId
                && x.Status != "completed" && x.Status != "failed" && x.Status != "cancelled")).Data ?? new();
        foreach (var q in activeQueues)
        {
            await _queueManager.CancelQueueAsync(q.QueueCode);
        }

        var files = (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.TaskId == taskId)).Data ?? new();
        int deletedCount = 0, restoredCount = 0;
        var replaceMarker = $"[upload-replace:{taskId}]";

        foreach (var file in files)
        {
            // 删除 MinIO 对象
            if (!string.IsNullOrEmpty(file.StoragePath))
            {
                try { await _storage.DeleteAsync(file.StoragePath.TrimStart('/')); }
                catch { /* 非阻塞 */ }
            }
            if (!string.IsNullOrEmpty(file.ConvertedStoragePath))
            {
                try { await _storage.DeleteAsync(file.ConvertedStoragePath.TrimStart('/')); }
                catch { /* 非阻塞 */ }
            }

            if ((file.Remark ?? "").Contains(replaceMarker))
            {
                // 替换模式：恢复为有效状态（内容已覆盖，无法回退）
                file.UploadStatus = "active";
                file.TaskId = null;
                file.ConvertStatus = null;
                file.ConvertedStoragePath = null;
                file.ConvertMessage = null;
                file.Remark = (file.Remark ?? "").Replace(replaceMarker, "").Trim();
                await _db.UpdateAsync(file);
                restoredCount++;
            }
            else
            {
                // 创建模式：物理删除
                await _db.DeleteByCodeAsync<StandardDirectoryFile>(file.Code);
                deletedCount++;
            }
        }

        // 删除此任务创建的空文件夹
        var taskFolders = (await _db.GetListAsync<StandardDirectoryFolder>(
            x => x.TaskId == taskId)).Data ?? new();
        foreach (var folder in taskFolders)
        {
            var hasFiles = (await _db.CountAsync<StandardDirectoryFile>(
                x => x.FolderCode == folder.FolderCode && x.Enable == true)).Data > 0;
            if (!hasFiles)
                await _db.DeleteByCodeAsync<StandardDirectoryFolder>(folder.Code);
        }

        // 删除上传任务记录
        var task = (await _db.GetOneAsync<UploadTask>(x => x.TaskId == taskId)).Data;
        if (task != null)
            await _db.DeleteByCodeAsync<UploadTask>(task.Code);

        return (true, null, deletedCount, restoredCount);
    }

    /// <summary>
    /// 查询上传状态
    /// </summary>
    public async Task<UploadStatusResponse?> GetUploadStatusAsync(string taskId)
    {
        var task = (await _db.GetOneAsync<UploadTask>(x => x.TaskId == taskId)).Data;
        if (task == null) return null;

        var files = (await _db.GetListAsync<StandardDirectoryFile>(
            x => x.TaskId == taskId)).Data ?? new();

        return new UploadStatusResponse
        {
            TaskId = taskId,
            Status = task.Status,
            TotalFiles = task.TotalFiles,
            SuccessCount = task.SuccessCount,
            FailCount = files.Count(x => x.UploadStatus == "failed"),
            Files = files.Select(f => new FileStatusItem
            {
                FileCode = f.FileCode,
                FileName = f.FileName,
                Status = f.UploadStatus
            }).ToList()
        };
    }

    #endregion

    #region 队列相关

    /// <summary>
    /// 获取活跃队列
    /// </summary>
    public async Task<YzhQueue?> GetActiveQueueAsync(string directoryCode)
    {
        return (await _queueManager.FindRunningQueueByScopeKeyAsync(directoryCode));
    }

    /// <summary>
    /// 获取转换进度
    /// </summary>
    public async Task<object> GetConvertProgressAsync(string taskId)
    {
        return await _queueManager.GetBatchProgressAsync(taskId);
    }

    /// <summary>
    /// 取消转换
    /// </summary>
    public async Task<(bool ok, string? error)> CancelConvertAsync(string queueCode)
    {
        return await _queueManager.CancelQueueAsync(queueCode);
    }

    #endregion

    #region 辅助方法

    private async Task<int> GetMaxSequenceAsync(string directoryCode, int depth)
    {
        var folders = (await _db.GetListAsync<StandardDirectoryFolder>(
            x => x.DirectoryCode == directoryCode && x.Depth == depth)).Data ?? new();
        int max = 0;
        foreach (var f in folders)
        {
            if (f.FolderCode != null && f.FolderCode.Contains("|S"))
            {
                var parts = f.FolderCode.Split('|');
                var seqPart = parts.LastOrDefault();
                if (seqPart != null && seqPart.StartsWith("S") && int.TryParse(seqPart[1..], out int seq))
                    max = Math.Max(max, seq);
            }
        }
        return max;
    }

    private async Task<string> BuildFolderFullPathAsync(StandardDirectoryFolder folder)
    {
        if (string.IsNullOrEmpty(folder.ParentCode))
            return folder.FolderName;

        var parent = (await _db.GetOneAsync<StandardDirectoryFolder>(
            x => x.FolderCode == folder.ParentCode)).Data;
        if (parent == null || string.IsNullOrEmpty(parent.FullPath))
            return folder.FolderName;

        return $"{parent.FullPath}/{folder.FolderName}";
    }

    private async Task CleanupOrphanDataAsync(string directoryCode, string taskId)
    {
        await _db.SqlExecuteAsync(
            "DELETE FROM cert_standard_directory_file WHERE DirectoryCode = @dc AND IsValid = 0 AND TaskId != @tid",
            new { dc = directoryCode, tid = taskId });
        await _db.SqlExecuteAsync(
            "DELETE FROM cert_standard_directory_folder WHERE DirectoryCode = @dc AND IsValid = 0 AND TaskId != @tid",
            new { dc = directoryCode, tid = taskId });
        await _db.SqlExecuteAsync(
            "DELETE FROM cert_upload_task WHERE DirectoryCode = @dc AND Status != 'completed' AND TaskId != @tid",
            new { dc = directoryCode, tid = taskId });
    }

    private async Task<string?> GetQueueLockErrorAsync(string directoryCode)
    {
        var activeQueue = await _queueManager.FindRunningQueueByScopeKeyAsync(directoryCode);
        if (activeQueue != null)
            return $"有正在执行的任务队列（{activeQueue.QueueCode}），请等待完成后再操作";
        return null;
    }

    private async Task<string?> GetFileLockErrorAsync(string fileCode)
    {
        var file = (await _db.GetOneAsync<StandardDirectoryFile>(
            x => x.FileCode == fileCode)).Data;
        if (file == null) return null;

        var dirLockErr = await GetQueueLockErrorAsync(file.DirectoryCode);
        if (dirLockErr != null) return dirLockErr;

        var lockResult = await _queueManager.FindResourceLockAsync(
            QueueManager.RESOURCE_FILE, new List<string> { fileCode });
        if (lockResult != null)
            return $"文件正在被队列 {lockResult.QueueCode} 使用，无法操作";

        return null;
    }

    private static bool IsDuplicateKeyError(Exception ex)
    {
        return ex.Message.Contains("Duplicate entry") ||
               ex.Message.Contains("duplicate key") ||
               ex.Message.Contains("1062");
    }

    #endregion
}

#region 辅助类

/// <summary>
/// 文件转换载荷（队列任务 payload）
/// </summary>
public class FileConvertPayload
{
    public string FileCode { get; set; } = "";
    public string FileName { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public string ConvertType { get; set; } = ""; // doc2docx / xls2xlsx
}

#endregion
