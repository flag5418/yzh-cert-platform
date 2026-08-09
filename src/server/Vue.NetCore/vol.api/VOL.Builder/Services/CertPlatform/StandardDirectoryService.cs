/*
 * 标准目录管理 Service 实现
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;
using VOL.Core.BaseProvider;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.Utilities;
using VOL.Entity.CertPlatform.Dir;
using VOL.Entity.CertPlatform.Cert;
using VOL.Entity.CertPlatform.Sys;
using VOL.Builder.IServices.CertPlatform;
using VOL.Entity.DomainModels;
using VOL.Core.ManageUser;

namespace VOL.Builder.Services.CertPlatform
{
    public class StandardDirectoryService : IStandardDirectoryService
    {
        private readonly VOLContext _db;
        private readonly ICodeGeneratorService _codeGenerator;
        private readonly IConfiguration _configuration;
        private readonly IMinioClient _minioClient;

        public StandardDirectoryService(VOLContext db, ICodeGeneratorService codeGenerator, IConfiguration configuration)
        {
            _db = db;
            _codeGenerator = codeGenerator;
            _configuration = configuration;
            
            // 初始化 MinIO 客户端
            _minioClient = new MinioClient()
                .WithEndpoint(configuration["MinIO:Endpoint"] ?? "127.0.0.1:9000")
                .WithCredentials(
                    configuration["MinIO:AccessKey"] ?? "admin",
                    configuration["MinIO:SecretKey"] ?? "Yzh123456.")
                .WithSSL(false)
                .Build();
        }

        #region 组织树

        /// <summary>
        /// 获取组织树数据
        /// 格式：机构 -> 标准 -> 阶段
        /// </summary>
        public WebResponseContent GetOrganizationTree()
        {
            try
            {
                // 查询所有启用的机构
                var organizations = _db.Set<CertificationBody>()
                    .Where(x => x.Enable == true)
                    .OrderBy(x => x.Sort)
                    .ToList();

                // 查询所有启用的标准
                var standards = _db.Set<ISOStandard>()
                    .Where(x => x.Enable == true)
                    .ToList();

                // 查询所有阶段（CertStage 对应表 cert_cert_stage）
                var stages = _db.Set<CertStage>()
                    .Where(x => x.Enable == true)
                    .OrderBy(x => x.SortOrder)
                    .ToList();

                // 查询机构-标准关联
                var orgStandards = _db.Set<CertOrgStandard>()
                    .ToList();

                // 查询机构-阶段关联
                var orgStages = _db.Set<CertOrgStage>()
                    .ToList();

                // 构建树形结构
                var tree = new List<object>();

                foreach (var org in organizations)
                {
                    var orgNode = new
                    {
                        id = org.CbCode,
                        label = org.Name,
                        type = "organization",
                        cbCode = org.CbCode,
                        children = new List<object>()
                    };

                    // 获取该机构关联的标准（关联表CbCode存储的是CertificationBody.Code/GUID）
                    var orgStdIds = orgStandards
                        .Where(x => x.CbCode == org.Code)
                        .Select(x => x.StdId)
                        .ToList();

                    var orgStandardsList = standards
                        .Where(x => orgStdIds.Contains(x.Id))
                        .ToList();

                    foreach (var std in orgStandardsList)
                    {
                        var stdNode = new
                        {
                            id = $"{org.CbCode}|{std.StandardCode}",
                            label = $"{std.StandardCode} - {std.StandardName}",
                            type = "standard",
                            cbCode = org.CbCode,
                            standardCode = std.StandardCode,
                            standardName = std.StandardName,
                            children = new List<object>()
                        };

                        // 获取该机构关联的阶段
                        var orgStageIds = orgStages
                            .Where(x => x.CbCode == org.Code)
                            .Select(x => x.StageId)
                            .ToList();

                        var orgStagesList = stages
                            .Where(x => orgStageIds.Contains(x.Id))
                            .ToList();

                        foreach (var stage in orgStagesList)
                        {
                            var phaseNode = new
                            {
                                id = $"{org.CbCode}|{std.StandardCode}|{stage.StageCode}",
                                label = $"{stage.StageCode} - {stage.StageName}",
                                type = "phase",
                                cbCode = org.CbCode,
                                standardCode = std.StandardCode,
                                phaseCode = stage.StageCode,
                                phaseName = stage.StageName,
                                children = new List<object>()
                            };

                            ((List<object>)stdNode.children).Add(phaseNode);
                        }

                        ((List<object>)orgNode.children).Add(stdNode);
                    }

                    tree.Add(orgNode);
                }

                return new WebResponseContent().OK(null, tree);
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"获取组织树失败：{ex.Message}");
            }
        }

        #endregion

        #region 标准目录配置

        /// <summary>
        /// 获取标准目录配置列表
        /// </summary>
        public PageGridData<StandardDirectoryConfig> GetConfigs(PageDataOptions options)
        {
            var query = _db.Set<StandardDirectoryConfig>()
                .Where(x => x.Enable == true)
                .OrderByDescending(x => x.CreateDate);

            int total = query.Count();
            int page = options.Page > 0 ? options.Page : 1;
            int rows = options.Rows > 0 ? options.Rows : 20;
            var list = query.Skip((page - 1) * rows).Take(rows).ToList();

            var result = new PageGridData<StandardDirectoryConfig>();
            result.rows = list;
            result.total = total;
            return result;
        }

        /// <summary>
        /// 获取单个标准目录配置
        /// </summary>
        public StandardDirectoryConfig GetConfig(string directoryCode)
        {
            return _db.Set<StandardDirectoryConfig>()
                .FirstOrDefault(x => x.DirectoryCode == directoryCode && x.Enable == true);
        }

        /// <summary>
        /// 创建标准目录配置
        /// </summary>
        public WebResponseContent CreateConfig(StandardDirectoryConfig config)
        {
            try
            {
                // 生成编码
                config.DirectoryCode = _codeGenerator.GenerateDirectoryCode(config.StandardCode, config.PhaseCode);
                config.Code = Guid.NewGuid().ToString("N");
                config.CreateDate = DateTime.Now;
                config.Status = "draft";
                config.Enable = true;

                _db.Set<StandardDirectoryConfig>().Add(config);
                _db.SaveChanges();

                return new WebResponseContent().OK($"创建成功，目录编码：{config.DirectoryCode}");
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"创建失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 更新标准目录配置
        /// </summary>
        public WebResponseContent UpdateConfig(StandardDirectoryConfig config)
        {
            try
            {
                var existing = _db.Set<StandardDirectoryConfig>()
                    .FirstOrDefault(x => x.DirectoryCode == config.DirectoryCode);

                if (existing == null)
                    return new WebResponseContent().Error("配置不存在");

                existing.RootFolderName = config.RootFolderName;
                existing.Status = config.Status;
                existing.Sort = config.Sort;
                existing.Remark = config.Remark;
                existing.ModifyDate = DateTime.Now;

                _db.SaveChanges();

                return new WebResponseContent().OK("更新成功");
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"更新失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 删除标准目录配置
        /// </summary>
        public WebResponseContent DeleteConfig(string directoryCode)
        {
            try
            {
                var config = _db.Set<StandardDirectoryConfig>()
                    .FirstOrDefault(x => x.DirectoryCode == directoryCode);

                if (config == null)
                    return new WebResponseContent().Error("配置不存在");

                // 软删除
                config.Enable = false;
                config.DeleteID = UserContext.Current?.UserId;
                config.Deleter = UserContext.Current?.UserName;
                config.DeleteTime = DateTime.Now;
                config.Status = "archived";

                _db.SaveChanges();

                return new WebResponseContent().OK("删除成功");
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"删除失败：{ex.Message}");
            }
        }

        #endregion

        #region 标准目录文件夹

        /// <summary>
        /// 获取标准目录文件夹树
        /// </summary>
        public WebResponseContent GetFolderTree(string directoryCode)
        {
            try
            {
                var folders = _db.Set<StandardDirectoryFolder>()
                    .Where(x => x.DirectoryCode == directoryCode && x.Enable == true && x.IsValid == true)
                    .OrderBy(x => x.SortOrder)
                    .ToList();

                // 构建树形结构
                var rootFolders = folders.Where(x => string.IsNullOrEmpty(x.ParentCode)).ToList();
                
                foreach (var root in rootFolders)
                {
                    root.Children = GetChildFolders(folders, root.FolderCode);
                }

                return new WebResponseContent().OK(null, rootFolders);
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"获取失败：{ex.Message}");
            }
        }

        private List<StandardDirectoryFolder> GetChildFolders(List<StandardDirectoryFolder> allFolders, string parentCode)
        {
            var children = allFolders.Where(x => x.ParentCode == parentCode).OrderBy(x => x.SortOrder).ToList();
            
            foreach (var child in children)
            {
                child.Children = GetChildFolders(allFolders, child.FolderCode);
            }

            return children;
        }

        /// <summary>
        /// 创建标准目录文件夹
        /// </summary>
        public WebResponseContent CreateFolder(StandardDirectoryFolder folder)
        {
            try
            {
                // 生成编码
                folder.Code = Guid.NewGuid().ToString("N");
                folder.FolderCode = _codeGenerator.GenerateFolderCode(
                    folder.DirectoryCode, 
                    folder.Depth, 
                    GetMaxSequence(folder.DirectoryCode, folder.ParentCode) + 1
                );
                folder.CreateDate = DateTime.Now;
                folder.Status = "draft";
                folder.Enable = true;

                _db.Set<StandardDirectoryFolder>().Add(folder);
                _db.SaveChanges();

                return new WebResponseContent().OK($"创建成功，文件夹编码：{folder.FolderCode}");
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"创建失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 更新标准目录文件夹
        /// </summary>
        public WebResponseContent UpdateFolder(StandardDirectoryFolder folder)
        {
            try
            {
                var existing = _db.Set<StandardDirectoryFolder>()
                    .FirstOrDefault(x => x.FolderCode == folder.FolderCode);

                if (existing == null)
                    return new WebResponseContent().Error("文件夹不存在");

                existing.FolderName = folder.FolderName;
                existing.SortOrder = folder.SortOrder;
                existing.Remark = folder.Remark;
                existing.ModifyDate = DateTime.Now;

                _db.SaveChanges();

                return new WebResponseContent().OK("更新成功");
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"更新失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 删除标准目录文件夹
        /// </summary>
        public WebResponseContent DeleteFolder(string folderCode)
        {
            try
            {
                var folder = _db.Set<StandardDirectoryFolder>()
                    .FirstOrDefault(x => x.FolderCode == folderCode);

                if (folder == null)
                    return new WebResponseContent().Error("文件夹不存在");

                // 软删除
                folder.Enable = false;
                folder.DeleteID = UserContext.Current?.UserId;
                folder.Deleter = UserContext.Current?.UserName;
                folder.DeleteTime = DateTime.Now;
                folder.Status = "archived";

                _db.SaveChanges();

                return new WebResponseContent().OK("删除成功");
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"删除失败：{ex.Message}");
            }
        }

        #endregion

        #region 标准目录文件

        /// <summary>
        /// 获取标准目录文件列表
        /// </summary>
        public WebResponseContent GetFiles(string folderCode)
        {
            try
            {
                var files = _db.Set<StandardDirectoryFile>()
                    .Where(x => x.FolderCode == folderCode && x.Enable == true && x.IsValid == true)
                    .OrderBy(x => x.SortOrder)
                    .ToList();

                var result = new WebResponseContent().OK(null, files);
                return result;
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"获取失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 创建标准目录文件
        /// </summary>
        public WebResponseContent CreateFile(StandardDirectoryFile file)
        {
            try
            {
                // 生成编码（简化版）
                file.Code = Guid.NewGuid().ToString("N");
                file.FileCode = _codeGenerator.GenerateFileCode(
                    file.FolderCode,
                    file.FileName
                );
                file.CreateDate = DateTime.Now;
                file.Status = "draft";
                file.Enable = true;

                _db.Set<StandardDirectoryFile>().Add(file);
                _db.SaveChanges();

                return new WebResponseContent().OK($"创建成功，文件编码：{file.FileCode}");
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"创建失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 更新标准目录文件
        /// </summary>
        public WebResponseContent UpdateFile(StandardDirectoryFile file)
        {
            try
            {
                var existing = _db.Set<StandardDirectoryFile>()
                    .FirstOrDefault(x => x.FileCode == file.FileCode);

                if (existing == null)
                    return new WebResponseContent().Error("文件不存在");

                existing.FileName = file.FileName;
                existing.FileType = file.FileType;
                existing.FilePattern = file.FilePattern;
                existing.IsRequired = file.IsRequired;
                existing.MaxFileSizeMB = file.MaxFileSizeMB;
                existing.Description = file.Description;
                existing.SortOrder = file.SortOrder;
                existing.ExtractionEnabled = file.ExtractionEnabled;
                existing.ExtractionRules = file.ExtractionRules;
                existing.PreCheckRequired = file.PreCheckRequired;
                existing.ComplianceRequired = file.ComplianceRequired;
                existing.Remark = file.Remark;
                existing.ModifyDate = DateTime.Now;

                _db.SaveChanges();

                return new WebResponseContent().OK("更新成功");
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"更新失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 删除标准目录文件
        /// </summary>
        public WebResponseContent DeleteFile(string fileCode)
        {
            try
            {
                var file = _db.Set<StandardDirectoryFile>()
                    .FirstOrDefault(x => x.FileCode == fileCode);

                if (file == null)
                    return new WebResponseContent().Error("文件不存在");

                // 软删除
                file.Enable = false;
                file.DeleteID = UserContext.Current?.UserId;
                file.Deleter = UserContext.Current?.UserName;
                file.DeleteTime = DateTime.Now;
                file.Status = "archived";

                _db.SaveChanges();

                return new WebResponseContent().OK("删除成功");
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"删除失败：{ex.Message}");
            }
        }

        #endregion

        #region 导出打包

        /// <summary>
        /// 将标准目录配置及其子文件夹、文件打包成ZIP
        /// </summary>
        /// <param name="directoryCode">目录编码</param>
        /// <returns>ZIP文件流</returns>
        public async Task<Stream> ExportAsZip(string directoryCode)
        {
            var config = _db.Set<StandardDirectoryConfig>()
                .FirstOrDefault(x => x.DirectoryCode == directoryCode && x.Enable == true);

            if (config == null)
                throw new ArgumentException("目录配置不存在");

            var folders = _db.Set<StandardDirectoryFolder>()
                .Where(x => x.DirectoryCode == directoryCode && x.Enable == true)
                .OrderBy(x => x.SortOrder)
                .ToList();

            var folderCodes = folders.Select(x => x.FolderCode).ToList();

            var files = _db.Set<StandardDirectoryFile>()
                .Where(x => folderCodes.Contains(x.FolderCode) && x.Enable == true)
                .OrderBy(x => x.SortOrder)
                .ToList();

            var rootFolders = folders.Where(x => string.IsNullOrEmpty(x.ParentCode)).ToList();

            var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                foreach (var root in rootFolders)
                {
                    AddFolderToZip(archive, folders, files, root, "");
                }
            }

            ms.Position = 0;
            return await Task.FromResult(ms);
        }

        private void AddFolderToZip(
            ZipArchive archive,
            List<StandardDirectoryFolder> allFolders,
            List<StandardDirectoryFile> allFiles,
            StandardDirectoryFolder folder,
            string parentPath)
        {
            var folderPath = string.IsNullOrEmpty(parentPath)
                ? folder.FolderName
                : $"{parentPath}/{folder.FolderName}";

            var folderEntry = archive.CreateEntry($"{folderPath}/");
            using (var entryStream = folderEntry.Open())
            {
                entryStream.Close();
            }

            var folderFiles = allFiles.Where(x => x.FolderCode == folder.FolderCode).ToList();
            foreach (var file in folderFiles)
            {
                var fileEntry = archive.CreateEntry($"{folderPath}/{file.FileName}");
                using (var entryStream = fileEntry.Open())
                {
                    var contentBytes = System.Text.Encoding.UTF8.GetBytes(
                        $"File: {file.FileName}\nCode: {file.FileCode}\nType: {file.FileType}\nDescription: {file.Description}");
                    entryStream.Write(contentBytes, 0, contentBytes.Length);
                }
            }

            var children = allFolders
                .Where(x => x.ParentCode == folder.FolderCode)
                .OrderBy(x => x.SortOrder)
                .ToList();

            foreach (var child in children)
            {
                AddFolderToZip(archive, allFolders, allFiles, child, folderPath);
            }
        }

        #endregion

        #region 文件上传

        /// <summary>
        /// 上传文件到标准目录（存储到MinIO）
        /// </summary>
        public async Task<WebResponseContent> UploadFile(IFormFile file, string directoryCode, string relativePath)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return new WebResponseContent().Error("请选择要上传的文件");
                }

                // 获取目录配置
                var config = _db.Set<StandardDirectoryConfig>()
                    .FirstOrDefault(x => x.DirectoryCode == directoryCode && x.Enable == true);
                if (config == null)
                {
                    return new WebResponseContent().Error("目录配置不存在");
                }

                // 解析相对路径，获取文件夹结构
                var pathParts = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                string parentCode = "";
                string folderCode = directoryCode;

                // 如果有子文件夹，逐级创建或查找
                if (pathParts.Length > 1)
                {
                    for (int i = 0; i < pathParts.Length - 1; i++)
                    {
                        var folderName = pathParts[i];
                        var existingFolder = _db.Set<StandardDirectoryFolder>()
                            .FirstOrDefault(x => x.DirectoryCode == directoryCode 
                                               && x.ParentCode == parentCode 
                                               && x.FolderName == folderName 
                                               && x.Enable == true);
                        
                        if (existingFolder == null)
                        {
                            // 创建新文件夹
                            var depth = i + 1;
                            var sortOrder = GetMaxSequence(directoryCode, parentCode) + 1;
                            var newFolderCode = _codeGenerator.GenerateFolderCode(directoryCode, depth, sortOrder);
                            
                            var newFolder = new StandardDirectoryFolder
                            {
                                FolderCode = newFolderCode,
                                DirectoryCode = directoryCode,
                                ParentCode = parentCode,
                                FolderName = folderName,
                                Depth = depth,
                                SortOrder = sortOrder,
                                Code = Guid.NewGuid().ToString("N"),
                                CreateDate = DateTime.Now,
                                Enable = true
                            };
                            
                            _db.Set<StandardDirectoryFolder>().Add(newFolder);
                            await _db.SaveChangesAsync();
                            
                            folderCode = newFolderCode;
                        }
                        else
                        {
                            folderCode = existingFolder.FolderCode;
                        }
                        
                        parentCode = folderCode;
                    }
                }

                // 获取文件名
                var fileName = pathParts.Last();
                
                // 生成文件编码和存储路径
                var fileCode = _codeGenerator.GenerateFileCode(folderCode, fileName);
                var storagePath = _codeGenerator.GenerateStoragePath(
                    config.StandardCode, config.PhaseCode, folderCode, fileName);

                // 确保 Bucket 存在
                var bucketName = _configuration["MinIO:BucketName"] ?? "cert-platform";
                var bucketExists = await _minioClient.BucketExistsAsync(
                    new BucketExistsArgs().WithBucket(bucketName));
                if (!bucketExists)
                {
                    await _minioClient.MakeBucketAsync(
                        new MakeBucketArgs().WithBucket(bucketName));
                }

                // 上传文件到 MinIO
                var objectName = storagePath.TrimStart('/');
                using (var stream = file.OpenReadStream())
                {
                    var putArgs = new PutObjectArgs()
                        .WithBucket(bucketName)
                        .WithObject(objectName)
                        .WithStreamData(stream)
                        .WithObjectSize(file.Length)
                        .WithContentType(file.ContentType ?? "application/octet-stream");
                    
                    await _minioClient.PutObjectAsync(putArgs);
                }

                // 创建文件记录
                var fileRecord = new StandardDirectoryFile
                {
                    FileCode = fileCode,
                    DirectoryCode = directoryCode,
                    FolderCode = folderCode,
                    FileName = fileName,
                    FileType = Path.GetExtension(fileName).TrimStart('.'),
                    Code = Guid.NewGuid().ToString("N"),
                    CreateDate = DateTime.Now,
                    Enable = true
                };

                _db.Set<StandardDirectoryFile>().Add(fileRecord);
                await _db.SaveChangesAsync();

                return new WebResponseContent().OK($"文件上传成功：{fileName}");
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"上传失败：{ex.Message}");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取同级最大序号
        /// </summary>
        private int GetMaxSequence(string directoryCode, string parentCode)
        {
            var query = _db.Set<StandardDirectoryFolder>()
                .Where(x => x.DirectoryCode == directoryCode 
                         && x.ParentCode == parentCode 
                         && x.Enable == true);

            // 先获取所有记录，然后在内存中处理
            var folders = query.ToList();
            if (folders.Count == 0)
                return 0;
            
            int maxSeq = 0;
            bool isRoot = string.IsNullOrEmpty(parentCode);
            foreach (var folder in folders)
            {
                var parts = folder.FolderCode.Split('|');
                var seqIndex = isRoot ? 2 : 3;
                var seqStr = seqIndex < parts.Length ? parts[seqIndex] : "S001";
                var numStr = seqStr.Replace(isRoot ? "L" : "S", "");
                if (int.TryParse(numStr, out int value) && value > maxSeq)
                    maxSeq = value;
            }
            return maxSeq;
        }

        #endregion

        #region 批量上传（新方案）

        /// <summary>
        /// 批量上传预初始化
        /// 接收客户端清单，预创建编码和数据库记录，返回增强清单
        /// 基于完整路径判定 create/replace 模式
        /// </summary>
        public async Task<WebResponseContent> UploadInit(UploadManifestRequest manifest)
        {
            try
            {
                // 1. 校验目录配置
                var config = _db.Set<StandardDirectoryConfig>()
                    .FirstOrDefault(x => x.DirectoryCode == manifest.DirectoryCode && x.Enable == true);
                if (config == null)
                    return new WebResponseContent().Error("目录配置不存在");

                // 2. 生成任务ID
                var taskId = Guid.NewGuid().ToString("N");

                // 3. 清理残留数据（IsValid=0 的脏数据，且不属于当前任务）
                await CleanupOrphanData(manifest.DirectoryCode, taskId);

                // 4. 处理文件夹列表（基于完整路径判定复用/新建）
                var enhancedFolders = new List<EnhancedFolderItem>();
                var folderMap = new Dictionary<string, string>(); // path -> folderCode
                var existingFolderMap = new Dictionary<string, StandardDirectoryFolder>(); // path -> existing folder

                // 按路径深度排序，确保父文件夹先处理
                var sortedFolders = manifest.Folders
                    .OrderBy(f => f.Path.Count(c => c == '/'))
                    .ThenBy(f => f.Path)
                    .ToList();

                foreach (var folder in sortedFolders)
                {
                    var pathParts = folder.Path.Split('/');
                    var folderName = pathParts.Last();
                    var parentPath = pathParts.Length > 1 ? string.Join("/", pathParts.Take(pathParts.Length - 1)) : "";

                    // 查找父文件夹编码
                    string parentCode = "";
                    if (!string.IsNullOrEmpty(parentPath) && folderMap.ContainsKey(parentPath))
                    {
                        parentCode = folderMap[parentPath];
                    }

                    // 基于完整路径（FullPath）判定文件夹是否已存在
                    var existingFolder = _db.Set<StandardDirectoryFolder>()
                        .FirstOrDefault(x => x.DirectoryCode == manifest.DirectoryCode
                                           && x.FullPath == folder.Path
                                           && x.IsValid == true);

                    if (existingFolder != null)
                    {
                        // 复用已有文件夹
                        folderMap[folder.Path] = existingFolder.FolderCode;
                        existingFolderMap[folder.Path] = existingFolder;
                        enhancedFolders.Add(new EnhancedFolderItem
                        {
                            FolderCode = existingFolder.FolderCode,
                            FolderName = folderName,
                            ParentCode = existingFolder.ParentCode,
                            Depth = existingFolder.Depth,
                            FullPath = folder.Path,
                            Mode = "reuse"
                        });
                    }
                    else
                    {
                        // 创建新文件夹
                        var depth = pathParts.Length;
                        var sortOrder = GetMaxSequenceForUpload(manifest.DirectoryCode, parentCode, taskId) + 1;
                        var folderCode = _codeGenerator.GenerateFolderCode(manifest.DirectoryCode, depth, sortOrder);

                        var newFolder = new StandardDirectoryFolder
                        {
                            FolderCode = folderCode,
                            DirectoryCode = manifest.DirectoryCode,
                            ParentCode = parentCode,
                            FolderName = folderName,
                            Depth = depth,
                            SortOrder = sortOrder,
                            Code = Guid.NewGuid().ToString("N"),
                            TaskId = taskId,
                            IsValid = false, // 预创建状态
                            FullPath = folder.Path,
                            CreateDate = DateTime.Now,
                            Enable = true
                        };

                        _db.Set<StandardDirectoryFolder>().Add(newFolder);
                        folderMap[folder.Path] = folderCode;
                        enhancedFolders.Add(new EnhancedFolderItem
                        {
                            FolderCode = folderCode,
                            FolderName = folderName,
                            ParentCode = parentCode,
                            Depth = depth,
                            FullPath = folder.Path,
                            Mode = "create"
                        });
                    }
                }

                await _db.SaveChangesAsync();

                // 5. 处理文件列表（基于完整路径判定 create/replace）
                var enhancedFiles = new List<EnhancedFileItem>();
                var standardCode = config.StandardCode.Replace(":", "").Replace("-", "").Replace(" ", "");
                var phaseCode = config.PhaseCode.Replace(":", "").Replace("-", "").Replace(" ", "");

                for (int i = 0; i < manifest.Files.Count; i++)
                {
                    var fileItem = manifest.Files[i];
                    var fullPath = fileItem.RelativePath; // 前端传入完整路径

                    // 查找所属文件夹
                    var pathParts = fullPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                    string parentFolderCode = manifest.DirectoryCode;
                    if (pathParts.Length > 1)
                    {
                        var folderPath = string.Join("/", pathParts.Take(pathParts.Length - 1));
                        if (folderMap.ContainsKey(folderPath))
                        {
                            parentFolderCode = folderMap[folderPath];
                        }
                    }

                    var fileName = pathParts.Last();

                    // 基于完整路径（FullPath）判定文件是否已存在
                    var existingFile = _db.Set<StandardDirectoryFile>()
                        .FirstOrDefault(x => x.DirectoryCode == manifest.DirectoryCode
                                           && x.FullPath == fullPath
                                           && x.IsValid == true);

                    if (existingFile != null)
                    {
                        // ===== replace 模式 =====
                        // 不创建新记录，标记旧记录 UploadStatus = "replacing"
                        existingFile.UploadStatus = "replacing";
                        existingFile.TaskId = taskId;
                        MarkModified(existingFile, nameof(StandardDirectoryFile.UploadStatus), nameof(StandardDirectoryFile.TaskId));

                        enhancedFiles.Add(new EnhancedFileItem
                        {
                            Index = i,
                            FileCode = existingFile.FileCode, // 保持旧编码
                            FileName = fileName,
                            RelativePath = fileItem.RelativePath,
                            FullPath = fullPath,
                            FileSize = fileItem.FileSize,
                            MimeType = fileItem.MimeType,
                            StoragePath = existingFile.StoragePath, // 保持旧路径
                            ParentFolderCode = parentFolderCode,
                            Mode = "replace",
                            ExistingFileCode = existingFile.FileCode,
                            ExistingFileId = existingFile.Id,
                            OldStoragePath = existingFile.StoragePath,
                            Status = "pending"
                        });
                    }
                    else
                    {
                        // ===== create 模式 =====
                        var fileCode = _codeGenerator.GenerateFileCode(parentFolderCode, fileName);
                        var storagePath = $"/{standardCode}/{phaseCode}/{parentFolderCode}/{fileName}";

                        var fileRecord = new StandardDirectoryFile
                        {
                            FileCode = fileCode,
                            FolderCode = parentFolderCode,
                            DirectoryCode = manifest.DirectoryCode,
                            FileName = fileName,
                            FileType = Path.GetExtension(fileName).TrimStart('.'),
                            Code = Guid.NewGuid().ToString("N"),
                            TaskId = taskId,
                            IsValid = false, // 预创建状态
                            UploadStatus = "pending",
                            StoragePath = storagePath,
                            FullPath = fullPath,
                            CreateDate = DateTime.Now,
                            Enable = true
                        };

                        _db.Set<StandardDirectoryFile>().Add(fileRecord);

                        enhancedFiles.Add(new EnhancedFileItem
                        {
                            Index = i,
                            FileCode = fileCode,
                            FileName = fileName,
                            RelativePath = fileItem.RelativePath,
                            FullPath = fullPath,
                            FileSize = fileItem.FileSize,
                            MimeType = fileItem.MimeType,
                            StoragePath = storagePath,
                            ParentFolderCode = parentFolderCode,
                            Mode = "create",
                            Status = "pending"
                        });
                    }
                }

                // 6. 创建任务记录
                var uploadTask = new UploadTask
                {
                    TaskId = taskId,
                    DirectoryCode = manifest.DirectoryCode,
                    TotalFiles = manifest.Files.Count,
                    TotalSize = manifest.Files.Sum(f => f.FileSize),
                    SuccessCount = 0,
                    Status = "initialized",
                    Creator = UserContext.Current?.UserName,
                    CreateDate = DateTime.Now,
                    ExpireTime = DateTime.Now.AddMinutes(30)
                };

                _db.Set<UploadTask>().Add(uploadTask);
                await _db.SaveChangesAsync();

                // 7. 返回增强清单
                var response = new UploadManifestResponse
                {
                    Status = "initialized",
                    TaskId = taskId,
                    DirectoryCode = manifest.DirectoryCode,
                    TotalFiles = manifest.Files.Count,
                    TotalSize = manifest.Files.Sum(f => f.FileSize),
                    Folders = enhancedFolders,
                    Files = enhancedFiles
                };

                return new WebResponseContent().OK(null, response);
            }
            catch (Exception ex)
            {
                var errMsg = $"预处理失败：{ex.Message}";
                if (ex.InnerException != null) errMsg += $" | Inner: {ex.InnerException.Message}";
                if (ex.InnerException?.InnerException != null) errMsg += $" | Inner2: {ex.InnerException.InnerException.Message}";
                Console.WriteLine($"[UploadInit ERROR] {errMsg}");
                Console.WriteLine($"[UploadInit ERROR] StackTrace: {ex.StackTrace}");
                return new WebResponseContent().Error(errMsg);
            }
        }

        /// <summary>
        /// 上传单个文件到MinIO（新版，基于taskId，支持 create/replace 模式）
        /// </summary>
        public async Task<WebResponseContent> UploadFileWithTask(IFormFile file, UploadFileRequest request)
        {
            try
            {
                Console.WriteLine($"[UploadFileWithTask] TaskId={request.TaskId}, FileCode={request.FileCode}, StoragePath={request.StoragePath}");
                // 1. 校验任务
                var task = _db.Set<UploadTask>()
                    .FirstOrDefault(x => x.TaskId == request.TaskId);
                Console.WriteLine($"[UploadFileWithTask] task found={task != null}, status={task?.Status}");
                if (task == null || task.Status != "initialized")
                    return new WebResponseContent().Error($"上传任务不存在或已过期 (TaskId={request.TaskId}, Status={task?.Status})");

                // 2. 校验文件记录（支持 create 和 replace 两种模式）
                //    create 模式：IsValid=false, UploadStatus=pending
                //    replace 模式：IsValid=true, UploadStatus=replacing
                var fileRecord = _db.Set<StandardDirectoryFile>()
                    .FirstOrDefault(x => x.FileCode == request.FileCode 
                                       && x.TaskId == request.TaskId
                                       && ((x.IsValid == false && x.UploadStatus == "pending")   // create
                                           || (x.IsValid == true && x.UploadStatus == "replacing"))); // replace
                if (fileRecord == null)
                    return new WebResponseContent().Error("文件记录不存在或状态异常");

                bool isReplaceMode = fileRecord.IsValid == true && fileRecord.UploadStatus == "replacing";
                var oldStoragePath = isReplaceMode ? fileRecord.StoragePath : null;

                // 3. 上传到MinIO
                var bucketName = _configuration["MinIO:BucketName"] ?? "cert-platform";
                var storagePath = request.StoragePath.TrimStart('/');

                // 确保Bucket存在
                var beArgs = new BucketExistsArgs().WithBucket(bucketName);
                bool found = await _minioClient.BucketExistsAsync(beArgs).ConfigureAwait(false);
                if (!found)
                {
                    var mbArgs = new MakeBucketArgs().WithBucket(bucketName);
                    await _minioClient.MakeBucketAsync(mbArgs).ConfigureAwait(false);
                }

                // 上传新文件
                using var stream = file.OpenReadStream();
                var putArgs = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(storagePath)
                    .WithStreamData(stream)
                    .WithObjectSize(file.Length)
                    .WithContentType(file.ContentType ?? "application/octet-stream");
                await _minioClient.PutObjectAsync(putArgs).ConfigureAwait(false);

                // 4. replace 模式：上传成功后删除旧 MinIO 对象
                if (isReplaceMode && !string.IsNullOrEmpty(oldStoragePath))
                {
                    try
                    {
                        var oldObjPath = oldStoragePath.TrimStart('/');
                        var removeArgs = new RemoveObjectArgs()
                            .WithBucket(bucketName)
                            .WithObject(oldObjPath);
                        await _minioClient.RemoveObjectAsync(removeArgs).ConfigureAwait(false);
                    }
                    catch
                    {
                        // 旧文件删除失败不影响新文件上传（可能是首次上传，旧文件不存在）
                    }
                }

                // 5. 更新文件记录状态
                fileRecord.UploadStatus = "uploaded"; // 标记为已上传，等待 confirm
                fileRecord.ModifyDate = DateTime.Now;
                MarkModified(fileRecord, nameof(StandardDirectoryFile.UploadStatus), nameof(StandardDirectoryFile.ModifyDate));
                Console.WriteLine($"[UploadFileWithTask] Updated fileRecord.UploadStatus to 'uploaded', FileCode={fileRecord.FileCode}");

                // 6. 更新任务成功数
                task.SuccessCount++;
                task.ModifyDate = DateTime.Now;
                MarkModified(task, nameof(UploadTask.SuccessCount), nameof(UploadTask.ModifyDate));

                var saveResult = await _db.SaveChangesAsync();
                Console.WriteLine($"[UploadFileWithTask] SaveChangesAsync returned: {saveResult}");

                return new WebResponseContent().OK($"文件上传成功：{fileRecord.FileName}");
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"上传失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 确认上传完成（支持 create/replace 模式）
        /// create 模式：IsValid=0 → 1
        /// replace 模式：更新旧记录文件信息，UploadStatus → active
        /// </summary>
        public WebResponseContent UploadConfirm(string taskId)
        {
            try
            {
                // 1. 校验任务
                var task = _db.Set<UploadTask>()
                    .FirstOrDefault(x => x.TaskId == taskId);
                if (task == null)
                    return new WebResponseContent().Error("上传任务不存在");

                // 2. 检查所有文件是否已上传（uploaded 状态表示文件已到 MinIO，等待确认）
                var pendingFiles = _db.Set<StandardDirectoryFile>()
                    .Where(x => x.TaskId == taskId && x.UploadStatus == "pending")
                    .Count();
                if (pendingFiles > 0)
                    return new WebResponseContent().Error($"还有 {pendingFiles} 个文件未上传完成");

                // 3. 批量激活文件夹（IsValid=0 → 1）
                var folders = _db.Set<StandardDirectoryFolder>()
                    .Where(x => x.TaskId == taskId && x.IsValid == false)
                    .ToList();
                foreach (var folder in folders)
                {
                    folder.IsValid = true;
                    folder.ModifyDate = DateTime.Now;
                    MarkModified(folder, nameof(StandardDirectoryFolder.IsValid), nameof(StandardDirectoryFolder.ModifyDate));
                }

                // 4. 处理文件（区分 create 和 replace 模式）
                // create 模式：IsValid=0, UploadStatus=uploaded → IsValid=1, UploadStatus=active
                // replace 模式：IsValid=1, UploadStatus=uploaded → 更新文件信息, UploadStatus=active
                var filesToActivate = _db.Set<StandardDirectoryFile>()
                    .Where(x => x.TaskId == taskId && x.UploadStatus == "uploaded")
                    .ToList();

                foreach (var file in filesToActivate)
                {
                    if (file.IsValid == false)
                    {
                        // ===== create 模式 =====
                        file.IsValid = true;
                        file.UploadStatus = "active";
                        file.ModifyDate = DateTime.Now;
                    }
                    else
                    {
                        // ===== replace 模式 =====
                        // 旧记录已经是 IsValid=1，只需更新文件信息和状态
                        file.UploadStatus = "active";
                        file.ModifyDate = DateTime.Now;
                        // StoragePath 在 upload-file-v2 时已更新为新路径
                    }
                    MarkModified(file, nameof(StandardDirectoryFile.IsValid), nameof(StandardDirectoryFile.UploadStatus), nameof(StandardDirectoryFile.ModifyDate));
                }

                // 5. 更新任务状态
                task.Status = "completed";
                task.ModifyDate = DateTime.Now;
                MarkModified(task, nameof(UploadTask.Status), nameof(UploadTask.ModifyDate));

                _db.SaveChanges();

                return new WebResponseContent().OK($"上传确认完成，共{task.TotalFiles}个文件");
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"确认失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 回滚上传任务（支持 create/replace 模式）
        /// create 模式：删除 IsValid=0 的新记录 + MinIO 新文件
        /// replace 模式：旧记录 UploadStatus → active（恢复），删除 MinIO 新文件
        /// </summary>
        public async Task<WebResponseContent> UploadCancel(string taskId)
        {
            try
            {
                // 1. 查找任务
                var task = _db.Set<UploadTask>()
                    .FirstOrDefault(x => x.TaskId == taskId);
                if (task == null)
                    return new WebResponseContent().Error("上传任务不存在");

                var bucketName = _configuration["MinIO:BucketName"] ?? "cert-platform";
                var restoredCount = 0;
                var deletedFiles = 0;
                var deletedFolders = 0;

                // 2. 处理所有关联文件
                var allFiles = _db.Set<StandardDirectoryFile>()
                    .Where(x => x.TaskId == taskId)
                    .ToList();

                foreach (var file in allFiles)
                {
                    if (file.IsValid == false)
                    {
                        // ===== create 模式：删除新记录 + MinIO 新文件 =====
                        if (!string.IsNullOrEmpty(file.StoragePath))
                        {
                            try
                            {
                                var storagePath = file.StoragePath.TrimStart('/');
                                var rmArgs = new RemoveObjectArgs()
                                    .WithBucket(bucketName)
                                    .WithObject(storagePath);
                                await _minioClient.RemoveObjectAsync(rmArgs).ConfigureAwait(false);
                            }
                            catch { /* 忽略MinIO删除失败 */ }
                        }
                        _db.Set<StandardDirectoryFile>().Remove(file);
                        deletedFiles++;
                    }
                    else if (file.UploadStatus == "replacing" || file.UploadStatus == "uploaded")
                    {
                        // ===== replace 模式：恢复旧记录状态 =====
                        // 如果已上传了新文件到 MinIO，需要删除新文件
                        if (file.UploadStatus == "uploaded" && !string.IsNullOrEmpty(file.StoragePath))
                        {
                            try
                            {
                                var storagePath = file.StoragePath.TrimStart('/');
                                var rmArgs = new RemoveObjectArgs()
                                    .WithBucket(bucketName)
                                    .WithObject(storagePath);
                                await _minioClient.RemoveObjectAsync(rmArgs).ConfigureAwait(false);
                            }
                            catch { /* 忽略MinIO删除失败 */ }
                        }
                        // 恢复状态为 active（旧文件内容未被修改）
                        file.UploadStatus = "active";
                        file.TaskId = null; // 清除任务关联
                        MarkModified(file, nameof(StandardDirectoryFile.UploadStatus), nameof(StandardDirectoryFile.TaskId));
                        restoredCount++;
                    }
                }

                // 3. 删除空文件夹（IsValid=0 的新文件夹）
                var foldersToDelete = _db.Set<StandardDirectoryFolder>()
                    .Where(x => x.TaskId == taskId && x.IsValid == false)
                    .ToList();

                foreach (var folder in foldersToDelete)
                {
                    // 检查文件夹下是否还有文件（非本次任务的）
                    var hasOtherFiles = _db.Set<StandardDirectoryFile>()
                        .Any(x => x.FolderCode == folder.FolderCode && x.TaskId != taskId);
                    
                    if (!hasOtherFiles)
                    {
                        _db.Set<StandardDirectoryFolder>().Remove(folder);
                        deletedFolders++;
                    }
                }

                // 4. 删除任务记录
                _db.Set<UploadTask>().Remove(task);

                await _db.SaveChangesAsync();

                var msg = $"回滚完成，已清理{deletedFiles}个新文件和{deletedFolders}个新文件夹";
                if (restoredCount > 0)
                    msg += $"，已恢复{restoredCount}个替换文件的原始状态";
                return new WebResponseContent().OK(msg);
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"回滚失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 查询上传任务状态
        /// </summary>
        public UploadStatusResponse GetUploadStatus(string taskId)
        {
            var task = _db.Set<UploadTask>()
                .FirstOrDefault(x => x.TaskId == taskId);
            if (task == null)
                return null;

            var files = _db.Set<StandardDirectoryFile>()
                .Where(x => x.TaskId == taskId)
                .Select(x => new FileStatusItem
                {
                    FileCode = x.FileCode,
                    FileName = x.FileName,
                    Status = x.UploadStatus
                })
                .ToList();

            return new UploadStatusResponse
            {
                TaskId = task.TaskId,
                Status = task.Status,
                TotalFiles = task.TotalFiles,
                SuccessCount = task.SuccessCount,
                FailCount = task.TotalFiles - task.SuccessCount,
                Files = files
            };
        }

        /// <summary>
        /// 清理残留数据（IsValid=0 的脏数据）
        /// </summary>
        private async Task CleanupOrphanData(string directoryCode, string currentTaskId)
        {
            // 查找该目录下所有 IsValid=0 的文件夹（清理所有残留，不仅限于不同 TaskId）
            var orphanFolders = _db.Set<StandardDirectoryFolder>()
                .Where(x => x.DirectoryCode == directoryCode 
                           && x.IsValid == false)
                .ToList();

            var bucketName = _configuration["MinIO:BucketName"] ?? "cert-platform";

            foreach (var folder in orphanFolders)
            {
                // 删除该文件夹下 IsValid=0 的文件
                var orphanFiles = _db.Set<StandardDirectoryFile>()
                    .Where(x => x.FolderCode == folder.FolderCode 
                               && x.IsValid == false)
                    .ToList();

                foreach (var file in orphanFiles)
                {
                    if (!string.IsNullOrEmpty(file.StoragePath))
                    {
                        try
                        {
                            var storagePath = file.StoragePath.TrimStart('/');
                            var rmArgs = new RemoveObjectArgs()
                                .WithBucket(bucketName)
                                .WithObject(storagePath);
                            await _minioClient.RemoveObjectAsync(rmArgs).ConfigureAwait(false);
                        }
                        catch { /* 忽略MinIO删除失败 */ }
                    }
                    _db.Set<StandardDirectoryFile>().Remove(file);
                }

                // 如果文件夹为空，删除文件夹
                var hasFiles = _db.Set<StandardDirectoryFile>()
                    .Any(x => x.FolderCode == folder.FolderCode);
                if (!hasFiles)
                {
                    _db.Set<StandardDirectoryFolder>().Remove(folder);
                }
            }

            // 也清理目录根级别的 IsValid=0 文件
            var rootOrphanFiles = _db.Set<StandardDirectoryFile>()
                .Where(x => x.DirectoryCode == directoryCode 
                           && x.IsValid == false)
                .ToList();

            foreach (var file in rootOrphanFiles)
            {
                if (!string.IsNullOrEmpty(file.StoragePath))
                {
                    try
                    {
                        var storagePath = file.StoragePath.TrimStart('/');
                        var rmArgs = new RemoveObjectArgs()
                            .WithBucket(bucketName)
                            .WithObject(storagePath);
                        await _minioClient.RemoveObjectAsync(rmArgs).ConfigureAwait(false);
                    }
                    catch { /* 忽略MinIO删除失败 */ }
                }
                _db.Set<StandardDirectoryFile>().Remove(file);
            }

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// 获取同级最大序号（上传版本，支持 taskId 隔离）
        /// </summary>
        private int GetMaxSequenceForUpload(string directoryCode, string parentCode, string taskId)
        {
            var query = _db.Set<StandardDirectoryFolder>()
                .Where(x => x.DirectoryCode == directoryCode 
                         && x.ParentCode == parentCode);

            var folders = query.ToList();
            if (folders.Count == 0)
                return 0;
            
            int maxSeq = 0;
            foreach (var folder in folders)
            {
                // FolderCode 格式: FD-{DirCode}|L{Level}|S{Sequence}
                // 最后一个 | 分段是 S{Sequence}
                var parts = folder.FolderCode.Split('|');
                var seqStr = parts.Length > 0 ? parts[parts.Length - 1] : "S001";
                var numStr = seqStr.Replace("S", "");
                if (int.TryParse(numStr, out int value) && value > maxSeq)
                    maxSeq = value;
            }
            return maxSeq;
        }

        /// <summary>
        /// 标记实体指定属性为已修改（解决 NoTracking 模式下 SaveChanges 检测不到变更的问题）
        /// </summary>
        private void MarkModified<TEntity>(TEntity entity, params string[] propertyNames) where TEntity : class
        {
            _db.Set<TEntity>().Attach(entity);
            foreach (var name in propertyNames)
            {
                _db.Entry(entity).Property(name).IsModified = true;
            }
        }

        #endregion
    }
}
