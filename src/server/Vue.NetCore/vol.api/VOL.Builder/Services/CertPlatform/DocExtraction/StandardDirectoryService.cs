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
using VOL.Core.SignalR;
using VOL.Core.Utilities;
using VOL.Entity.CertPlatform.Dir;
using VOL.Entity.CertPlatform.Cert;
using VOL.Entity.CertPlatform.DocExtraction;
using VOL.Entity.CertPlatform.Sys;
using VOL.Builder.IServices.CertPlatform;
using VOL.Builder.Services.CertPlatform;
using VOL.Entity.DomainModels;
using VOL.Core.ManageUser;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using YZH.Core.Queue;

namespace VOL.Builder.Services.CertPlatform
{
    public class StandardDirectoryService : IStandardDirectoryService
    {
        /// <summary>队列 payload 序列化选项（camelCase，与 yzh 队列框架约定一致）</summary>
        private static readonly JsonSerializerOptions _payloadJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly VOLContext _db;
        private readonly ICodeGeneratorService _codeGenerator;
        private readonly IConfiguration _configuration;
        private readonly IMinioClient _minioClient;
        private readonly OfficeConvertService _convertService;
        private readonly IHubContext<UploadProgressHub> _hubContext;
        
        // ===== 新增Helper服务 =====
        private readonly IMinIOHelper _minioHelper;
        private readonly IFolderFileManager _folderFileManager;
        private readonly IFileStorageService _fileStorageService;
        private readonly YzhQueueManager _queueManager;

        public StandardDirectoryService(VOLContext db, ICodeGeneratorService codeGenerator,
                                        IConfiguration configuration, OfficeConvertService convertService,
                                        IHubContext<UploadProgressHub> hubContext,
                                        IMinIOHelper minioHelper,
                                        IFolderFileManager folderFileManager,
                                        IFileStorageService fileStorageService,
                                        YzhQueueManager queueManager)
        {
            _db = db;
            _codeGenerator = codeGenerator;
            _configuration = configuration;
            _convertService = convertService;
            _hubContext = hubContext;
            _minioHelper = minioHelper;
            _folderFileManager = folderFileManager;
            _fileStorageService = fileStorageService;
            _queueManager = queueManager;

            // 初始化 MinIO 客户端（保留用于兼容旧代码）
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
                    .OrderBy(x => x.Id)
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
                    // V3 架构：关联表用 org_code 列存储的是机构实体的 Code 字段值（如 CB001-CODE）
                    var orgNode = new
                    {
                        id = org.Code,
                        label = org.Name,
                        type = "organization",
                        cbCode = org.Code,
                        children = new List<object>()
                    };

                    // 获取该机构关联的标准（关联表 cert_org_standard.org_code 存储的是 org.Code）
                    var orgStdCodes = orgStandards
                        .Where(x => x.OrgCode == org.Code)
                        .Select(x => x.StdCode)
                        .ToList();

                    var orgStandardsList = standards
                        .Where(x => orgStdCodes.Contains(x.Code))
                        .ToList();

                    foreach (var std in orgStandardsList)
                    {
                        var stdNode = new
                        {
                            id = $"{org.Code}|{std.StandardCode}",
                            label = $"{std.StandardCode} - {std.StandardName}",
                            type = "standard",
                            cbCode = org.Code,
                            stdCode = std.Code,
                            standardCode = std.StandardCode,
                            standardName = std.StandardName,
                            children = new List<object>()
                        };

                        // 获取该机构+标准关联的阶段（关联表用 org.Code 匹配；standard_code 可能为 NULL，表示该阶段适用于所有标准）
                        var orgStageCodes = orgStages
                        .Where(x => x.OrgCode == org.Code && (x.StdCode == null || x.StdCode == std.Code))
                            .Select(x => x.StageCode)
                            .ToList();

                        var orgStagesList = stages
                            .Where(x => orgStageCodes.Contains(x.StageCode))
                            .ToList();

                        foreach (var stage in orgStagesList)
                        {
                            var phaseNode = new
                            {
                                id = $"{org.Code}|{std.StandardCode}|{stage.StageCode}",
                                label = $"{stage.StageCode} - {stage.StageName}",
                                type = "phase",
                                cbCode = org.Code,
                                stdCode = std.Code,
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
                Console.WriteLine($"[GetOrganizationTree] Error: {ex}");
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
        /// 获取阶段的完整文件树（含规则属性）
        /// 用于文档提取规则管理页面，单次返回所有层级的文件夹和文件
        /// </summary>
        public async Task<StageFileTreeResponse> GetStageFileTree(string directoryCode)
        {
            try
            {
                // 1. 查询所有启用的文件夹
                var allFolders = _db.Set<StandardDirectoryFolder>()
                    .Where(x => x.DirectoryCode == directoryCode && x.Enable == true && x.IsValid == true)
                    .OrderBy(x => x.SortOrder)
                    .ToList();

                // 2. 查询所有启用的文件
                var allFiles = _db.Set<StandardDirectoryFile>()
                    .Where(x => x.DirectoryCode == directoryCode && x.Enable == true && x.IsValid == true)
                    .OrderBy(x => x.SortOrder)
                    .ToList();

                // 3. 构建树形结构（根级文件夹）
                var rootFolders = allFolders
                    .Where(x => string.IsNullOrEmpty(x.ParentCode))
                    .OrderBy(x => x.SortOrder)
                    .ToList();

                // 3.1 规则状态权威来源：cert_doc_extraction_rule（按 standard_file_code 关联）
                //     历史实现读 file.ExtractionRules / file.ExtractionEnabled 列，但规则保存时从不写这两列，
                //     导致树中 RuleStatus 永远为 none。规则表才是唯一权威（单一约束原则）。
                var ruleStatusMap = new Dictionary<string, string>();
                try
                {
                    var stageFileCodes = allFiles.Select(f => f.FileCode).ToList();
                    if (stageFileCodes.Count > 0)
                    {
                        var ruleList = await _db.Set<CertDocExtractionRule>()
                            .Where(r => stageFileCodes.Contains(r.StandardFileCode))
                            .Select(r => new { r.StandardFileCode, r.Status })
                            .ToListAsync();
                        ruleStatusMap = ruleList
                            .GroupBy(x => x.StandardFileCode)
                            .ToDictionary(g => g.Key, g => g.Last().Status);
                    }
                }
                catch (Exception ruleEx)
                {
                    // 规则状态查询失败不影响文件树加载，降级为 none
                    Console.WriteLine($"[GetStageFileTree] 规则状态查询失败: {ruleEx.Message}");
                }

                var folderNodes = new List<StageFolderNode>();
                int totalFolders = 0;
                int totalFiles = 0;
                int configuredFiles = 0;

                foreach (var root in rootFolders)
                {
                    var node = BuildStageFolderNode(root, allFolders, allFiles, ruleStatusMap, ref totalFolders, ref totalFiles, ref configuredFiles);
                    folderNodes.Add(node);
                }

                // 4. 根目录级文件（FolderCode 未关联任何文件夹节点）
                // 历史数据中直接上传到目录根部（无子文件夹）的文件 FolderCode 会退化为 DirectoryCode，
                // 不归属任何 FD- 文件夹，导致在文档提取规则等页面树中不可见（pdf/jpg 等尤其常见）。
                // 这里统一挂到一个虚拟的“根目录”节点下展示。
                var folderCodeSet = new HashSet<string>(allFolders.Select(f => f.FolderCode));
                var rootOrphanFiles = allFiles
                    .Where(f => !folderCodeSet.Contains(f.FolderCode))
                    .ToList();

                if (rootOrphanFiles.Count > 0)
                {
                    var rootNode = new StageFolderNode
                    {
                        Code = directoryCode,
                        Name = "根目录",
                        ParentCode = string.Empty,
                        Depth = 0,
                        SortOrder = 0
                    };
                    foreach (var file in rootOrphanFiles)
                    {
                        totalFiles++;
                        var fileRuleStatus = ruleStatusMap.TryGetValue(file.FileCode, out var rs) ? rs : "none";
                        bool hasRule = fileRuleStatus == "configured" || fileRuleStatus == "failed";
                        if (hasRule) configuredFiles++;

                        rootNode.Files.Add(new StageFileNode
                        {
                            FileCode = file.FileCode,
                            FileName = file.FileName,
                            FolderCode = file.FolderCode,
                            StoragePath = file.StoragePath,
                            ConvertedStoragePath = file.ConvertedStoragePath,
                            ConvertStatus = file.ConvertStatus,
                            ConvertMessage = file.ConvertMessage,
                            FileSize = file.FileSize,
                            MimeType = file.FileType,
                            RuleStatus = fileRuleStatus,
                            ExtractFieldCount = 0,
                            TableDefCount = 0
                        });
                    }
                    folderNodes.Insert(0, rootNode);
                }

                // 5. 返回结果
                return new StageFileTreeResponse
                {
                    DirectoryCode = directoryCode,
                    Folders = folderNodes,
                    Statistics = new StageFileStatistics
                    {
                        TotalFolders = totalFolders,
                        TotalFiles = totalFiles,
                        ConfiguredFiles = configuredFiles
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetStageFileTree] Error: {ex.Message}");
                throw new Exception($"获取阶段文件树失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 递归构建阶段文件夹节点
        /// </summary>
        private StageFolderNode BuildStageFolderNode(
            StandardDirectoryFolder folder, 
            List<StandardDirectoryFolder> allFolders,
            List<StandardDirectoryFile> allFiles,
            Dictionary<string, string> ruleStatusMap,
            ref int totalFolders,
            ref int totalFiles,
            ref int configuredFiles)
        {
            totalFolders++;

            var node = new StageFolderNode
            {
                Code = folder.FolderCode,
                Name = folder.FolderName,
                ParentCode = folder.ParentCode,
                Depth = folder.Depth,
                SortOrder = folder.SortOrder
            };

            // 递归处理子文件夹
            var children = allFolders
                .Where(x => x.ParentCode == folder.FolderCode)
                .OrderBy(x => x.SortOrder)
                .ToList();

            foreach (var child in children)
            {
                node.Children.Add(BuildStageFolderNode(child, allFolders, allFiles, ruleStatusMap, ref totalFolders, ref totalFiles, ref configuredFiles));
            }

            // 获取该文件夹下的文件
            var files = allFiles
                .Where(x => x.FolderCode == folder.FolderCode)
                .OrderBy(x => x.SortOrder)
                .ToList();

            foreach (var file in files)
            {
                totalFiles++;
                
                // 规则状态：以 cert_doc_extraction_rule 表为权威（configured/failed/none）
                var fileRuleStatus = ruleStatusMap.TryGetValue(file.FileCode, out var rs) ? rs : "none";
                bool hasRule = fileRuleStatus == "configured" || fileRuleStatus == "failed";
                if (hasRule) configuredFiles++;

                node.Files.Add(new StageFileNode
                {
                    FileCode = file.FileCode,
                    FileName = file.FileName,
                    FolderCode = file.FolderCode,
                    StoragePath = file.StoragePath,
                    ConvertedStoragePath = file.ConvertedStoragePath,
                    ConvertStatus = file.ConvertStatus,
                    ConvertMessage = file.ConvertMessage,
                    FileSize = file.FileSize,
                    MimeType = file.FileType,
                    RuleStatus = fileRuleStatus,
                    ExtractFieldCount = 0,
                    TableDefCount = 0
                });
            }

            return node;
        }

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
                var dirLockErr = CheckDirLockErrorAsync(folder.DirectoryCode).GetAwaiter().GetResult();
                if (dirLockErr != null)
                    return new WebResponseContent().Error(dirLockErr);

                // 生成编码
                folder.Code = Guid.NewGuid().ToString("N");
                int maxSeq = GetMaxSequence(folder.DirectoryCode, folder.Depth);
                folder.FolderCode = _codeGenerator.GenerateFolderCode(
                    folder.DirectoryCode, 
                    folder.Depth, 
                    maxSeq + 1
                );
                folder.CreateDate = DateTime.Now;
                folder.Status = "draft";
                folder.Enable = true;
                // 有效标志：非上传预创建，创建即有效（否则 GetFolderTree 过滤后看不到）
                folder.IsValid = true;
                // 计算名称路径（FullPath）：基于父文件夹路径 + 自身名称
                folder.FullPath = BuildFolderFullPath(folder);

                _db.Set<StandardDirectoryFolder>().Add(folder);
                _db.SaveChanges();

                return new WebResponseContent().OK($"创建成功，文件夹编码：{folder.FolderCode}");
            }
            catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062)
            {
                // Duplicate entry：序号计算仍有冲突，尝试递增直到找到可用编码
                for (int i = 0; i < 100; i++)
                {
                    try
                    {
                        folder.FolderCode = _codeGenerator.GenerateFolderCode(
                            folder.DirectoryCode, folder.Depth, GetMaxSequence(folder.DirectoryCode, folder.Depth) + 1 + i);
                        _db.Set<StandardDirectoryFolder>().Add(folder);
                        _db.SaveChanges();
                        return new WebResponseContent().OK($"创建成功，文件夹编码：{folder.FolderCode}");
                    }
                    catch (MySqlConnector.MySqlException ex2) when (ex2.Number == 1062)
                    {
                        continue; // 继续尝试下一个序号
                    }
                }
                return new WebResponseContent().Error("创建失败：无法生成唯一编码，请重试");
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"创建失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 更新标准目录文件夹
        /// </summary>
        /// <summary>
        /// 更新文件夹（重命名）— 委托给FolderFileManager处理
        /// TODO: 后续绑定工作流/校验规则时，重命名前需检查关联数据
        /// </summary>
        public WebResponseContent UpdateFolder(StandardDirectoryFolder folder)
        {
            try
            {
                var existingFolder = _db.Set<StandardDirectoryFolder>()
                    .FirstOrDefault(x => x.FolderCode == folder.FolderCode && x.Enable == true);
                if (existingFolder == null)
                    return new WebResponseContent().Error("文件夹不存在或已被禁用");
                var dirLockErr = CheckDirLockErrorAsync(existingFolder.DirectoryCode).GetAwaiter().GetResult();
                if (dirLockErr != null)
                    return new WebResponseContent().Error(dirLockErr);

                var result = _folderFileManager.RenameFolderAsync(
                    folder.FolderCode, 
                    folder.FolderName, 
                    force: folder.Force).GetAwaiter().GetResult();

                if (!result)
                    return new WebResponseContent().Error("文件夹不存在或已被禁用");

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
        /// <summary>
        /// 删除文件夹 — 委托给FolderFileManager处理
        /// TODO: 后续绑定工作流/校验规则时，删除前需检查关联数据
        /// </summary>
        public WebResponseContent DeleteFolder(string folderCode)
        {
            try
            {
                var existingFolder = _db.Set<StandardDirectoryFolder>()
                    .FirstOrDefault(x => x.FolderCode == folderCode && x.Enable == true);
                if (existingFolder == null)
                    return new WebResponseContent().Error("文件夹不存在");
                var dirLockErr = CheckDirLockErrorAsync(existingFolder.DirectoryCode).GetAwaiter().GetResult();
                if (dirLockErr != null)
                    return new WebResponseContent().Error(dirLockErr);

                var (foldersDeleted, filesDeleted) = _folderFileManager.DeleteFolderAsync(folderCode).GetAwaiter().GetResult();
                
                return new WebResponseContent().OK(
                    $"删除成功，共删除 {foldersDeleted} 个子文件夹和 {filesDeleted} 个文件");
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
                var query = _db.Set<StandardDirectoryFile>()
                    .Where(x => x.Enable == true && x.IsValid == true);
                if (!string.IsNullOrEmpty(folderCode))
                    query = query.Where(x => x.FolderCode == folderCode);
                var files = query.OrderBy(x => x.SortOrder).ToList();

                var result = new WebResponseContent().OK(null, files);
                return result;
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"获取失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 获取目录下所有文件（不含子文件夹中的文件）
        /// </summary>
        public WebResponseContent GetFilesByDirectory(string directoryCode)
        {
            try
            {
                var files = _db.Set<StandardDirectoryFile>()
                    .Where(x => x.DirectoryCode == directoryCode && x.Enable == true && x.IsValid == true)
                    .OrderBy(x => x.SortOrder)
                    .ToList();

                return new WebResponseContent().OK(null, files);
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
                var lockParent = _db.Set<StandardDirectoryFolder>()
                    .FirstOrDefault(x => x.FolderCode == file.FolderCode && x.Enable == true);
                var dirLockErr = CheckDirLockErrorAsync(lockParent?.DirectoryCode).GetAwaiter().GetResult();
                if (dirLockErr != null)
                    return new WebResponseContent().Error(dirLockErr);

                // 生成编码（简化版）
                file.Code = Guid.NewGuid().ToString("N");
                file.FileCode = _codeGenerator.GenerateFileCode(
                    file.FolderCode,
                    file.FileName
                );
                file.CreateDate = DateTime.Now;
                file.Status = "draft";
                file.Enable = true;
                file.IsValid = true;
                // 计算名称路径：父文件夹 FullPath + 文件名
                var parentFolder = _db.Set<StandardDirectoryFolder>()
                    .FirstOrDefault(x => x.FolderCode == file.FolderCode && x.Enable == true);
                var parentPath = parentFolder?.FullPath?.Trim('/') ?? "";
                file.FullPath = string.IsNullOrEmpty(parentPath)
                    ? file.FileName
                    : $"{parentPath}/{file.FileName}";

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
        /// <summary>
        /// 更新文件（重命名/修改属性）— 委托给FileStorageService处理
        /// TODO: 后续绑定了校验规则/提取规则后，重命名前需检查关联数据
        /// </summary>
        public WebResponseContent UpdateFile(StandardDirectoryFile file)
        {
            try
            {
                var fileLockErr = CheckFileLockErrorAsync(file.FileCode).GetAwaiter().GetResult();
                if (fileLockErr != null)
                    return new WebResponseContent().Error(fileLockErr);

                var result = _fileStorageService.RenameFileAsync(file.FileCode, file.FileName).GetAwaiter().GetResult();
                
                if (!result)
                    return new WebResponseContent().Error("文件不存在或重命名失败");
                
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
        /// <summary>
        /// 删除文件 — 委托给FileStorageService处理
        /// TODO: 后续绑定了提取规则/校验规则后，删除前需检查关联数据
        /// </summary>
        public WebResponseContent DeleteFile(string fileCode)
        {
            try
            {
                var fileLockErr = CheckFileLockErrorAsync(fileCode).GetAwaiter().GetResult();
                if (fileLockErr != null)
                    return new WebResponseContent().Error(fileLockErr);

                var result = _fileStorageService.DeleteFileAsync(fileCode).GetAwaiter().GetResult();
                
                if (!result)
                    return new WebResponseContent().Error("文件不存在");
                
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
        /// 将选中的文件夹和文件打包成ZIP
        /// 1. 从 MinIO 下载选中的文件到本地临时目录
        /// 2. 用 ZipArchive 生成 ZIP 流
        /// 3. 删除临时目录
        /// </summary>
        public async Task<Stream> ExportAsZip(string directoryCode, List<string> selectedFolderCodes, List<string> selectedFileCodes)
        {
            var config = _db.Set<StandardDirectoryConfig>()
                .FirstOrDefault(x => x.DirectoryCode == directoryCode && x.Enable == true);
            if (config == null)
                throw new ArgumentException("目录配置不存在");

            var allFolders = _db.Set<StandardDirectoryFolder>()
                .Where(x => x.DirectoryCode == directoryCode && x.Enable == true)
                .ToList();

            // 展开选中的文件夹：如果选了父文件夹，需要包含其所有子文件夹中的文件
            var expandedFolderCodes = ExpandFolderCodes(allFolders, selectedFolderCodes);
            var expandedFileCodes = new HashSet<string>(selectedFileCodes ?? new List<string>());

            // 如果选了文件夹，把该文件夹下的所有文件也加进来
            if (expandedFolderCodes.Count > 0)
            {
                var allFiles = _db.Set<StandardDirectoryFile>()
                    .Where(x => expandedFolderCodes.Contains(x.FolderCode) && x.Enable == true)
                    .ToList();
                foreach (var f in allFiles)
                {
                    expandedFileCodes.Add(f.FileCode);
                }
            }

            if (expandedFileCodes.Count == 0)
                throw new ArgumentException("没有找到可导出的文件");

            // 只查询选中的文件
            var filesToExport = _db.Set<StandardDirectoryFile>()
                .Where(x => expandedFileCodes.Contains(x.FileCode) && x.Enable == true)
                .OrderBy(x => x.SortOrder)
                .ToList();

            var bucketName = _configuration["MinIO:BucketName"] ?? "cert-platform";

            // 1. 创建临时目录，从 MinIO 下载文件
            var tempDir = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                foreach (var file in filesToExport)
                {
                    // 计算文件在 zip 内的相对路径（基于文件夹层级）
                    var folderPath = GetFolderPath(allFolders, file.FolderCode);
                    var entryPath = string.IsNullOrEmpty(folderPath)
                        ? file.FileName
                        : $"{folderPath}/{file.FileName}";

                    // 确定 MinIO objectName
                    var objectName = file.StoragePath;
                    if (string.IsNullOrEmpty(objectName))
                    {
                        objectName = $"{config.StandardCode}/{config.PhaseCode}/{file.FolderCode}/{file.FileName}";
                    }
                    objectName = objectName.TrimStart('/');

                    // 下载到临时目录
                    var localPath = Path.Combine(tempDir, entryPath.Replace('/', Path.DirectorySeparatorChar));
                    var localDir = Path.GetDirectoryName(localPath);
                    if (!string.IsNullOrEmpty(localDir))
                        Directory.CreateDirectory(localDir);

                    try
                    {
                        var getArgs = new GetObjectArgs()
                            .WithBucket(bucketName)
                            .WithObject(objectName)
                            .WithFile(localPath);
                        await _minioClient.GetObjectAsync(getArgs);
                    }
                    catch (Exception ex)
                    {
                        await File.WriteAllTextAsync(localPath,
                            $"[文件未找到] 原始路径: {objectName}\n错误: {ex.Message}");
                    }
                }

                // 2. 用 ZipArchive 压缩临时目录
                var ms = new MemoryStream();
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    AddDirectoryToZip(archive, tempDir, "");
                }
                ms.Position = 0;
                return ms;
            }
            finally
            {
                // 3. 清理临时目录
                try { Directory.Delete(tempDir, true); } catch (Exception ex) { Console.WriteLine($"[StandardDirectoryService] Error: {ex.Message}"); }
            }
        }

        /// <summary>
        /// 展开文件夹编码：如果选了父文件夹，递归包含所有子文件夹
        /// </summary>
        private HashSet<string> ExpandFolderCodes(List<StandardDirectoryFolder> allFolders, List<string> folderCodes)
        {
            var result = new HashSet<string>(folderCodes ?? new List<string>());
            // 对每个选中的文件夹，递归加入子文件夹
            foreach (var code in folderCodes ?? Enumerable.Empty<string>())
            {
                AddChildFolderCodes(allFolders, code, result);
            }
            return result;
        }

        private void AddChildFolderCodes(List<StandardDirectoryFolder> allFolders, string parentCode, HashSet<string> result)
        {
            var children = allFolders.Where(x => x.ParentCode == parentCode);
            foreach (var child in children)
            {
                result.Add(child.FolderCode);
                AddChildFolderCodes(allFolders, child.FolderCode, result);
            }
        }

        /// <summary>
        /// 根据文件夹列表，递归计算某文件夹的完整路径
        /// </summary>
        private string GetFolderPath(List<StandardDirectoryFolder> allFolders, string folderCode)
        {
            var parts = new List<string>();
            string current = folderCode;
            while (!string.IsNullOrEmpty(current))
            {
                var folder = allFolders.FirstOrDefault(x => x.FolderCode == current);
                if (folder == null) break;
                parts.Insert(0, folder.FolderName);
                current = folder.ParentCode;
            }
            return string.Join("/", parts);
        }

        /// <summary>
        /// 递归将目录内容写入 ZipArchive
        /// </summary>
        private void AddDirectoryToZip(ZipArchive archive, string sourceDir, string entryPrefix)
        {
            foreach (var filePath in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(filePath);
                var entryName = string.IsNullOrEmpty(entryPrefix)
                    ? fileName
                    : $"{entryPrefix}/{fileName}";
                archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
            }
            foreach (var dirPath in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dirPath);
                var newPrefix = string.IsNullOrEmpty(entryPrefix)
                    ? dirName
                    : $"{entryPrefix}/{dirName}";
                archive.CreateEntry($"{newPrefix}/");
                AddDirectoryToZip(archive, dirPath, newPrefix);
            }
        }

        /// <summary>
        /// 从 MinIO 下载单个文件
        /// </summary>
        public async Task<(Stream stream, string contentType, string fileName)> DownloadFile(string storagePath)
        {
            var bucketName = _configuration["MinIO:BucketName"] ?? "cert-platform";
            var objectName = storagePath.TrimStart('/');

            var ms = new MemoryStream();
            StatObjectArgs statArgs = null;
            try
            {
                statArgs = new StatObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName);
                var stat = await _minioClient.StatObjectAsync(statArgs);

                var getArgs = new GetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithCallbackStream(async (stream, ct) =>
                    {
                        await stream.CopyToAsync(ms, ct);
                        ms.Position = 0;
                    });
                await _minioClient.GetObjectAsync(getArgs);

                var fileName = Path.GetFileName(objectName.Replace('/', '\\'));
                var contentType = stat.ContentType ?? "application/octet-stream";
                ms.Position = 0;
                return (ms, contentType, fileName);
            }
            catch (Exception)
            {
                ms.Dispose();
                throw new FileNotFoundException($"文件不存在: {objectName}");
            }
        }

        #endregion

        #region 文件上传

        /// <summary>
        /// 上传文件到标准目录（存储到MinIO）- V2版本使用四级路径结构
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

                // 解析机构编码（机构与登录人无关，优先从目录节点关系解析）
                var orgCode = await ResolveOrgCodeAsync(directoryCode);
                if (string.IsNullOrEmpty(orgCode))
                {
                    return new WebResponseContent().Error("无法确定机构信息，请从组织树选择机构节点后操作");
                }

                // 队列互斥：该机构/标准/阶段下已有转换队列运行则拒绝
                var runningQueue = await GetRunningQueueForDirectoryAsync(directoryCode);
                if (runningQueue != null)
                    return new WebResponseContent().Error($"该机构/标准/阶段下已有队列任务 {runningQueue.QueueCode}（文档转换）正在执行，请等待完成后再上传");

                // 服务端文件类型白名单校验（relativePath 是目录路径，真实文件名在 file.FileName）
                var typeErr = ValidateUploadFileType(file.FileName);
                if (typeErr != null)
                {
                    return new WebResponseContent().Error(typeErr);
                }

                // 解析相对路径，获取文件夹结构
                var pathParts = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                string parentCode = "";
                string folderCode = directoryCode;
                var folderNames = new List<string>();

                // 如果有子文件夹，逐级创建或查找
                if (pathParts.Length > 1)
                {
                    for (int i = 0; i < pathParts.Length - 1; i++)
                    {
                        var folderName = pathParts[i];
                        folderNames.Add(folderName);
                        
                        var existingFolder = _db.Set<StandardDirectoryFolder>()
                            .FirstOrDefault(x => x.DirectoryCode == directoryCode 
                                               && x.ParentCode == parentCode 
                                               && x.FolderName == folderName 
                                               && x.Enable == true);
                        
                        if (existingFolder == null)
                        {
                            // 创建新文件夹
                            var depth = i + 1;
                            var sortOrder = GetMaxSequence(directoryCode, depth) + 1;
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
                var fileExt = Path.GetExtension(fileName).TrimStart('.').ToLower();
                
                // 生成文件编码
                var fileCode = _codeGenerator.GenerateFileCode(folderCode, fileName);
                
                // 构建文件夹路径（用于 MinIO 路径）
                var folderPath = string.Join("/", folderNames);
                
                // 使用 V3 路径生成器（双顶层文件夹：/standard-directory/{Org}/{Standard}/{Phase}/{Folder}/{File}）
                var storagePath = _codeGenerator.GenerateStandardDirectoryPath(
                    orgCode, config.StandardCode, config.PhaseCode, folderPath, fileName);

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

                // 创建文件记录（文件大小以后端实际接收字节数为准）
                var fileRecord = new StandardDirectoryFile
                {
                    FileCode = fileCode,
                    DirectoryCode = directoryCode,
                    FolderCode = folderCode,
                    FileName = fileName,
                    FileType = fileExt,
                    StoragePath = storagePath,
                    FileSize = file.Length,
                    Code = Guid.NewGuid().ToString("N"),
                    CreateDate = DateTime.Now,
                    Enable = true
                };

                // 如果是旧版 Office 格式，标记为待转换
                if (fileExt == "doc" || fileExt == "xls")
                {
                    fileRecord.ConvertStatus = "pending";
                }

                _db.Set<StandardDirectoryFile>().Add(fileRecord);
                await _db.SaveChangesAsync();

                // 如果是旧版 Office 文件，创建转换队列（单任务）
                if (fileExt == "xls" || fileExt == "doc")
                {
                    var convertType = fileExt == "xls" ? "xls2xlsx" : "doc2docx";
                    var targetPath = _convertService.GenerateTargetPathPublic(storagePath, convertType);
                    var payload = new FileConvertPayload
                    {
                        FileCode = fileRecord.FileCode,
                        FileName = fileName,
                        SourcePath = storagePath,
                        TargetPath = targetPath,
                        ConvertType = convertType
                    };
                    var scopeKey = $"{orgCode}|{config.StandardCode}|{config.PhaseCode}";
                    var req = new YzhQueueManager.CreateQueueRequest
                    {
                        QueueType = "file_convert",
                        QueueName = $"文档转换-1个文件",
                        ScopeKey = scopeKey,
                        SourceType = "upload_file",
                        SourceId = fileRecord.FileCode,
                        UserId = UserContext.Current.UserId,
                        UserName = UserContext.Current.UserName,
                        OrgCode = orgCode,
                        ResourceLocks = new List<YzhQueueManager.ResourceLockItem>
                        {
                            new YzhQueueManager.ResourceLockItem { ResourceTable = YzhQueueManager.RESOURCE_DIR, ResourceCode = directoryCode, ResourceName = directoryCode, TaskNo = null },
                            new YzhQueueManager.ResourceLockItem { ResourceTable = YzhQueueManager.RESOURCE_FILE, ResourceCode = fileRecord.FileCode, ResourceName = fileName, TaskNo = 1 }
                        },
                        Tasks = new List<YzhQueueManager.TaskItem>
                        {
                            new YzhQueueManager.TaskItem { TaskType = "file_convert", Payload = JsonSerializer.Serialize(payload, _payloadJsonOptions), TaskId = null }
                        }
                    };
                    var (qOk, qErr, qCode, qCount) = await _queueManager.CreateQueueAsync(req);
                    if (!qOk)
                    {
                        Console.WriteLine($"[UploadFile] 创建转换队列失败: {qErr}");
                    }
                    else
                    {
                        // 文件置为无效隐藏，转换完成/失败后恢复
                        fileRecord.IsValid = false;
                        await _db.SaveChangesAsync();
                    }
                }

                return new WebResponseContent().OK($"文件上传成功：{fileName}");
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? "无内部异常";
                Console.WriteLine($"[UploadFile] 上传失败: {ex.Message}, 内部异常: {innerMessage}");
                Console.WriteLine($"[UploadFile] 堆栈: {ex.StackTrace}");
                return new WebResponseContent().Error($"上传失败：{ex.Message}，内部：{innerMessage}");
            }
        }

        /// <summary>
        /// 获取当前登录用户的机构编码（审核员绑定机构后使用；维护/管理端不依赖此值）
        /// </summary>
        private async Task<string> GetCurrentOrgCodeAsync()
        {
            try
            {
                var userId = UserContext.Current.UserId;
                if (userId <= 0)
                {
                    return null;
                }

                var user = await _db.Set<Sys_User>()
                    .Where(x => x.User_Id == userId)
                    .Select(x => new { x.OrgCode })
                    .FirstOrDefaultAsync();

                return user?.OrgCode;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 解析机构编码（架构约定：机构与登录人无关）
        /// <para>优先级：</para>
        /// <para>1. 前端显式传入（组织树节点 cbCode，维护/管理端主路径）</para>
        /// <para>2. 目录已有数据推导（存储路径首段，如 /CB001/... → CB001）</para>
        /// <para>3. 登录用户机构（审核员注册绑定机构后使用）</para>
        /// </summary>
        private async Task<string> ResolveOrgCodeAsync(string directoryCode, string preferredOrgCode = null)
        {
            // 1. 显式传入（前端从组织树节点点击得到）
            if (!string.IsNullOrEmpty(preferredOrgCode))
                return preferredOrgCode.Trim();

            // 2. 从目录已有文件推导（存储路径首段即为机构编码）
            if (!string.IsNullOrEmpty(directoryCode))
            {
                var samplePath = _db.Set<StandardDirectoryFile>()
                    .Where(f => f.DirectoryCode == directoryCode && f.DeleteTime == null && f.StoragePath != null)
                    .Select(f => f.StoragePath)
                    .FirstOrDefault();
                var derived = DeriveOrgCodeFromPath(samplePath);
                if (!string.IsNullOrEmpty(derived))
                    return derived;
            }

            // 3. 登录用户机构（审核员绑定机构后使用）
            return await GetCurrentOrgCodeAsync();
        }

        #endregion

        #region 队列互斥与资源锁（队列中心 V3）

        /// <summary>
        /// 查询某目录（机构/标准/阶段）下运行中的队列
        /// </summary>
        private async Task<YzhQueue> GetRunningQueueForDirectoryAsync(string directoryCode, string preferredOrgCode = null)
        {
            var config = _db.Set<StandardDirectoryConfig>()
                .FirstOrDefault(x => x.DirectoryCode == directoryCode && x.Enable == true);
            if (config == null) return null;
            var orgCode = await ResolveOrgCodeAsync(directoryCode, preferredOrgCode);
            if (string.IsNullOrEmpty(orgCode)) return null;
            return await _queueManager.FindRunningQueueByScopeKeyAsync($"{orgCode}|{config.StandardCode}|{config.PhaseCode}");
        }

        /// <summary>
        /// 公开接口：查询某目录下的运行中队列（供前端横幅展示队列状态）
        /// </summary>
        public async Task<object> GetActiveQueueAsync(string directoryCode)
        {
            var q = await GetRunningQueueForDirectoryAsync(directoryCode);
            if (q == null) return new { exists = false };
            return new
            {
                exists = true,
                queueCode = q.QueueCode,
                queueName = q.QueueName,
                status = q.Status,
                progress = q.Progress,
                totalCount = q.TotalCount,
                completedCount = q.CompletedCount,
                failedCount = q.FailedCount,
                pendingCount = q.PendingCount,
                startTime = q.StartTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                endTime = q.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                scopeKey = q.ScopeKey
            };
        }

        /// <summary>
        /// 查询指定文件编码在运行中队列中是否被锁定，返回 queueCode 或 null
        /// </summary>
        public async Task<Dictionary<string, string>> GetFileLockStatusAsync(List<string> fileCodes)
        {
            if (fileCodes == null || fileCodes.Count == 0) return new Dictionary<string, string>();
            var hit = await _queueManager.FindResourceLockAsync(YzhQueueManager.RESOURCE_FILE, fileCodes);
            if (hit == null) return new Dictionary<string, string>();
            return new Dictionary<string, string> { [hit.ResourceCode] = hit.QueueCode };
        }

        /// <summary>
        /// 范围互斥检查：该机构/标准/阶段下已有转换队列运行则返回错误文案
        /// </summary>
        private async Task<string> GetQueueLockErrorAsync(string directoryCode, string preferredOrgCode = null)
        {
            var q = await GetRunningQueueForDirectoryAsync(directoryCode, preferredOrgCode);
            if (q == null) return null;
            return $"该机构/标准/阶段下已有队列任务 {q.QueueCode}（文档转换）正在执行，请等待完成后再操作";
        }

        /// <summary>
        /// 目录资源锁检查（目录被队列锁定则整个目录的文件夹/文件禁止增删改）
        /// </summary>
        private async Task<string> CheckDirLockErrorAsync(string directoryCode)
        {
            if (string.IsNullOrEmpty(directoryCode)) return null;
            var hit = await _queueManager.FindResourceLockAsync(YzhQueueManager.RESOURCE_DIR, new List<string> { directoryCode });
            if (hit == null) return null;
            return $"该目录正被队列 {hit.QueueCode}（文档转换）处理中，队列完成前禁止修改，请稍后操作";
        }

        /// <summary>
        /// 文件资源锁检查（文件本身 + 所在目录）
        /// </summary>
        private async Task<string> CheckFileLockErrorAsync(string fileCode)
        {
            var file = _db.Set<StandardDirectoryFile>()
                .FirstOrDefault(x => x.FileCode == fileCode && x.Enable == true);
            if (file == null) return null;
            var dirErr = await CheckDirLockErrorAsync(file.DirectoryCode);
            if (dirErr != null) return dirErr;
            var hit = await _queueManager.FindResourceLockAsync(YzhQueueManager.RESOURCE_FILE, new List<string> { fileCode });
            if (hit == null) return null;
            return $"文件「{hit.ResourceName ?? fileCode}」正被队列 {hit.QueueCode}（文档转换）处理中，请稍后操作";
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 体系认证上传文件类型白名单：仅允许文档/表格/图片等认证材料文件。
        /// 过滤 .DS_Store、临时文件等无关文件，避免后期文件比对出现问题。
        /// </summary>
        private static readonly HashSet<string> _allowedUploadExts = new HashSet<string>
        {
            // 文档
            "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "rtf",
            // 图片
            "jpg", "jpeg", "png", "gif", "bmp", "webp", "tif", "tiff"
        };

        /// <summary>校验文件是否允许上传；返回 null 表示通过，否则返回错误消息</summary>
        private static string ValidateUploadFileType(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return "文件路径不能为空";

            var fileName = relativePath.Replace('\\', '/').Split('/').Last();

            // 过滤隐藏文件（.DS_Store、.gitignore 等）
            if (fileName.StartsWith("."))
                return $"不允许上传系统文件：{fileName}（仅支持文档/图片）";

            var ext = Path.GetExtension(fileName)?.TrimStart('.').ToLower() ?? "";
            if (!_allowedUploadExts.Contains(ext))
                return $"不支持的文件类型：{fileName}（仅支持文档/图片，如 pdf/doc/xls/图片等）";

            return null;
        }

        /// <summary>
        /// 获取同级最大序号
        /// </summary>
        /// <summary>
        /// 计算同一目录、同一层级下已用过的最大序号（含软删除记录，防止删除后序号复用导致编码冲突）。
        /// 按 (DirectoryCode, Depth) 全局分配，避免不同父节点下同级文件夹编码碰撞。
        /// </summary>
        private int GetMaxSequence(string directoryCode, int depth)
        {
            var folders = _db.Set<StandardDirectoryFolder>()
                .Where(x => x.DirectoryCode == directoryCode
                         && x.Depth == depth)
                .ToList();
            if (folders.Count == 0)
                return 0;

            int maxSeq = 0;
            foreach (var folder in folders)
            {
                // FolderCode格式: FD-{DirCode}|L{Level}|S{Sequence}
                // 最后一段是S{Sequence}，直接取parts.Last()
                var parts = folder.FolderCode.Split('|');
                var seqStr = parts.Length > 0 ? parts[parts.Length - 1] : "S001";
                var numStr = seqStr.Replace("S", "");
                if (int.TryParse(numStr, out int value) && value > maxSeq)
                    maxSeq = value;
            }
            return maxSeq;
        }

        /// <summary>
        /// 新建文件夹时计算名称路径（FullPath）。
        /// 优先复用父文件夹 FullPath；父级为历史数据无 FullPath 时沿父链拼名称。
        /// </summary>
        private string BuildFolderFullPath(StandardDirectoryFolder folder)
        {
            if (string.IsNullOrEmpty(folder.ParentCode))
                return folder.FolderName;

            var parent = _db.Set<StandardDirectoryFolder>()
                .FirstOrDefault(x => x.FolderCode == folder.ParentCode && x.Enable == true);
            var parentPath = parent?.FullPath?.Trim('/') ?? "";
            if (!string.IsNullOrEmpty(parentPath))
                return $"{parentPath}/{folder.FolderName}";

            var parts = new List<string> { folder.FolderName };
            var code = folder.ParentCode;
            var guard = 0;
            while (!string.IsNullOrEmpty(code) && guard++ < 100)
            {
                var p = _db.Set<StandardDirectoryFolder>()
                    .FirstOrDefault(x => x.FolderCode == code && x.Enable == true);
                if (p == null)
                    break;
                parts.Insert(0, p.FolderName);
                code = p.ParentCode;
            }
            return string.Join("/", parts);
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
                // 1. 校验或自动创建目录配置
                var config = _db.Set<StandardDirectoryConfig>()
                    .FirstOrDefault(x => x.DirectoryCode == manifest.DirectoryCode);
                 if (config == null)
                {
                    // 从 DirectoryCode 解析 StandardCode 和 PhaseCode
                    // 格式：SDC-{StandardCode}|{PhaseCode}
                    var dirParts = manifest.DirectoryCode.Split('|');
                    var stdCodeRaw = dirParts.Length > 0 ? dirParts[0].Replace("SDC-", "") : "";
                    var phaseCode = dirParts.Length > 1 ? dirParts[1] : "";

                    config = new StandardDirectoryConfig
                    {
                        Code = Guid.NewGuid().ToString("N"),
                        DirectoryCode = manifest.DirectoryCode,
                        StandardCode = stdCodeRaw,
                        PhaseCode = phaseCode,
                        Enable = true,
                        CreateDate = DateTime.Now,
                    };
                    _db.Set<StandardDirectoryConfig>().Add(config);
                    await _db.SaveChangesAsync();
                    Console.WriteLine($"[UploadInit] 自动创建目录配置: {manifest.DirectoryCode} (StandardCode={stdCodeRaw}, PhaseCode={phaseCode})");
                }

                // 1.1 队列互斥：该机构/标准/阶段下已有转换队列运行则拒绝上传
                var queueLockErr = await GetQueueLockErrorAsync(manifest.DirectoryCode, manifest.OrgCode);
                if (queueLockErr != null)
                    return new WebResponseContent().Error(queueLockErr);

                // 2. 生成任务ID
                var taskId = Guid.NewGuid().ToString("N");

                // 3. 清理残留数据（IsValid=0 的脏数据，且不属于当前任务）
                await CleanupOrphanData(manifest.DirectoryCode, taskId);

                // 4. 处理文件夹列表（基于完整路径判定复用/新建）
                var enhancedFolders = new List<EnhancedFolderItem>();
                var folderMap = new Dictionary<string, string>(); // path -> folderCode
                var existingFolderMap = new Dictionary<string, StandardDirectoryFolder>(); // path -> existing folder
                var seqCounter = new Dictionary<string, int>(); // "directoryCode|depth" -> maxSeq（同深度全局计数，避免不同父级下相同depth生成重复FolderCode）

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
                        // 先从数据库查最大序号，再与内存计数器取较大值（均按 目录+层级 全局分配，防止编码冲突）
                        var dbMaxSeq = GetMaxSequenceForUpload(manifest.DirectoryCode, depth);
                        var seqKey = $"{manifest.DirectoryCode}|{depth}";
                        var memMaxSeq = seqCounter.ContainsKey(seqKey) ? seqCounter[seqKey] : 0;
                        var sortOrder = Math.Max(dbMaxSeq, memMaxSeq) + 1;
                        seqCounter[seqKey] = sortOrder;
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
                
                // 解析机构编码（用于生成四级路径；优先前端节点 cbCode，其次目录已有数据，最后登录用户）
                var orgCode = await ResolveOrgCodeAsync(manifest.DirectoryCode, manifest.OrgCode);
                if (string.IsNullOrEmpty(orgCode))
                {
                    return new WebResponseContent().Error("无法确定机构信息，请从组织树选择机构节点后上传");
                }

                for (int i = 0; i < manifest.Files.Count; i++)
                {
                    var fileItem = manifest.Files[i];
                    var fullPath = fileItem.RelativePath; // 前端传入完整路径

                    // 服务端文件类型白名单校验（防止绕过前端过滤上传 .DS_Store 等垃圾文件）
                    // 命中时跳过该文件而不是拒绝整个批次，保证正常文件不受影响
                    var validateResult = ValidateUploadFileType(fullPath);
                    if (validateResult != null)
                    {
                        Console.WriteLine($"[UploadInit] 跳过不允许上传的文件: {validateResult}");
                        continue;
                    }

                    // 查找所属文件夹
                    var pathParts = fullPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                    string parentFolderCode = null;
                    if (pathParts.Length > 1)
                    {
                        var folderPath = string.Join("/", pathParts.Take(pathParts.Length - 1));
                        if (folderMap.ContainsKey(folderPath))
                        {
                            parentFolderCode = folderMap[folderPath];
                        }
                    }
                    // 如果文件在根目录（无子文件夹），使用根文件夹编码
                    if (string.IsNullOrEmpty(parentFolderCode))
                    {
                        var rootFolder = _db.Set<StandardDirectoryFolder>()
                            .FirstOrDefault(x => x.DirectoryCode == manifest.DirectoryCode
                                               && string.IsNullOrEmpty(x.ParentCode)
                                               && x.IsValid == true);
                        parentFolderCode = rootFolder?.FolderCode;
                    }
                    // 兜底：如果仍为空，使用目录编码作为文件夹编码（保持向后兼容）
                    if (string.IsNullOrEmpty(parentFolderCode))
                        parentFolderCode = manifest.DirectoryCode;

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
                        // 标记 replace 来源（取消时用于区分 create/replace：create 删除、replace 恢复）
                        existingFile.Remark = (existingFile.Remark ?? "") + $"[upload-replace:{taskId}]";

                        // 存储路径统一按 V3 约定生成（/standard-directory/{Org}/{Std}/{Phase}/{Folder}/{File}）
                        // 不沿用旧 V2 路径（/CB001CODE/...）——否则重传/替换后新文件仍落在旧结构，
                        // 导致 MinIO 目录树与约定不一致；旧 V2 对象在 UploadFileWithTask 上传成功后删除
                        var oldStorage = existingFile.StoragePath;
                        var replaceFolderPath = pathParts.Length > 1
                            ? string.Join("/", pathParts.Take(pathParts.Length - 1))
                            : "";
                        var newStoragePath = _codeGenerator.GenerateStandardDirectoryPath(
                            orgCode, config.StandardCode, config.PhaseCode, replaceFolderPath, fileName);
                        existingFile.StoragePath = newStoragePath;
                        MarkModified(existingFile, nameof(StandardDirectoryFile.UploadStatus), nameof(StandardDirectoryFile.TaskId),
                            nameof(StandardDirectoryFile.Remark), nameof(StandardDirectoryFile.StoragePath));

                        enhancedFiles.Add(new EnhancedFileItem
                        {
                            Index = i,
                            FileCode = existingFile.FileCode, // 保持旧编码
                            FileName = fileName,
                            RelativePath = fileItem.RelativePath,
                            FullPath = fullPath,
                            FileSize = fileItem.FileSize,
                            MimeType = fileItem.MimeType,
                            StoragePath = newStoragePath,
                            ParentFolderCode = parentFolderCode,
                            Mode = "replace",
                            ExistingFileCode = existingFile.FileCode,
                            ExistingFileId = existingFile.Id,
                            OldStoragePath = oldStorage,
                            Status = "pending"
                        });
                    }
                    else
                    {
                        // ===== create 模式 =====
                        var fileCode = _codeGenerator.GenerateFileCode(parentFolderCode, fileName);
                        var fileExt = Path.GetExtension(fileName).TrimStart('.').ToLower();
                        
                        // 构建文件夹路径（用于 MinIO 四级路径）
                        var folderPath = pathParts.Length > 1 
                            ? string.Join("/", pathParts.Take(pathParts.Length - 1)) 
                            : "";
                        
                        // 使用 V3 路径生成器：/standard-directory/{org}/{standard}/{phase}/{folder}/{file}
                        var storagePath = _codeGenerator.GenerateStandardDirectoryPath(
                            orgCode, config.StandardCode, config.PhaseCode, folderPath, fileName);

                        var fileRecord = new StandardDirectoryFile
                        {
                            FileCode = fileCode,
                            FolderCode = parentFolderCode,
                            DirectoryCode = manifest.DirectoryCode,
                            FileName = fileName,
                            FileType = fileExt,
                            Code = Guid.NewGuid().ToString("N"),
                            TaskId = taskId,
                            IsValid = false, // 预创建状态
                            UploadStatus = "pending",
                            StoragePath = storagePath,
                            FullPath = fullPath,
                            CreateDate = DateTime.Now,
                            Enable = true
                        };
                        
                        // 如果是旧版 Office 格式，标记为待转换
                        if (fileExt == "doc" || fileExt == "xls")
                        {
                            fileRecord.ConvertStatus = "pending";
                        }

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

                // 全部文件都被过滤时（如整批都是 .DS_Store），返回明确错误
                if (enhancedFiles.Count == 0)
                {
                    return new WebResponseContent().Error("没有可上传的有效文件（文件均被过滤）");
                }

                // 6. 创建任务记录（统计基于实际接受的 enhancedFiles，过滤掉 .DS_Store 等）
                var uploadTask = new UploadTask
                {
                    TaskId = taskId,
                    DirectoryCode = manifest.DirectoryCode,
                    TotalFiles = enhancedFiles.Count,
                    TotalSize = enhancedFiles.Sum(f => f.FileSize),
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
                    TotalFiles = enhancedFiles.Count,
                    TotalSize = enhancedFiles.Sum(f => f.FileSize),
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
                // 存储路径以后端为准：使用 UploadInit 阶段生成并写入 DB 的 StoragePath（V3 约定路径），
                // 不信任前端传入的 StoragePath——前端可能被篡改或与 DB 不一致，导致 MinIO 与页面逻辑混乱
                var bucketName = _configuration["MinIO:BucketName"] ?? "cert-platform";
                var storagePath = (fileRecord.StoragePath ?? request.StoragePath)?.TrimStart('/');
                if (string.IsNullOrEmpty(storagePath))
                    return new WebResponseContent().Error("文件存储路径缺失，请重新发起上传");

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
                //    注意：UploadInit 对 replace 复用旧 StoragePath（同路径覆盖写入），新旧对象是同一个，
                //    此时不能删除——否则会把刚上传的新内容删掉，导致后续文档转换 ObjectNotFound
                if (isReplaceMode && !string.IsNullOrEmpty(oldStoragePath)
                    && oldStoragePath.TrimStart('/') != storagePath)
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

                // 5. 更新文件记录状态（文件大小以后端实际接收的字节数为准，不信任前端）
                fileRecord.UploadStatus = "uploaded"; // 标记为已上传，等待 confirm
                fileRecord.FileSize = file.Length;
                fileRecord.ModifyDate = DateTime.Now;
                MarkModified(fileRecord, nameof(StandardDirectoryFile.UploadStatus), nameof(StandardDirectoryFile.FileSize), nameof(StandardDirectoryFile.ModifyDate));
                Console.WriteLine($"[UploadFileWithTask] Updated fileRecord.UploadStatus to 'uploaded', FileCode={fileRecord.FileCode}, Size={file.Length}");

                // 6. 更新任务成功数
                task.SuccessCount++;
                task.ModifyDate = DateTime.Now;
                MarkModified(task, nameof(UploadTask.SuccessCount), nameof(UploadTask.ModifyDate));

                var saveResult = await _db.SaveChangesAsync();
                Console.WriteLine($"[UploadFileWithTask] SaveChangesAsync returned: {saveResult}");

                // 广播上传进度到 SignalR
                await BroadcastUploadProgressAsync(request.TaskId);

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
        public async Task<WebResponseContent> UploadConfirm(string taskId)
        {
            try
            {
                // 1. 校验任务
                var task = _db.Set<UploadTask>()
                    .FirstOrDefault(x => x.TaskId == taskId);
                if (task == null)
                    return new WebResponseContent().Error("上传任务不存在");

                // 1.1 队列互斥：该机构/标准/阶段下已有转换队列运行则拒绝确认
                var scopeErr = await GetQueueLockErrorAsync(task.DirectoryCode);
                if (scopeErr != null)
                    return new WebResponseContent().Error(scopeErr);

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

                // 5. 为 .doc/.xls 文件创建转换队列（.doc→.docx, .xls→.xlsx）
                var userId = UserContext.Current.UserId;
                var userName = UserContext.Current.UserName;
                var orgCode = await ResolveOrgCodeAsync(task.DirectoryCode);
                var convertCount = 0;
                var queuedFiles = new List<StandardDirectoryFile>();
                var specs = new List<FileConvertPayload>();
                foreach (var file in filesToActivate)
                {
                    if (string.IsNullOrEmpty(file.StoragePath)) continue;

                    var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLower();
                    string convertType = null;
                    if (ext == "xls") convertType = "xls2xlsx";
                    else if (ext == "doc") convertType = "doc2docx";
                    else continue;

                    var targetPath = _convertService.GenerateTargetPathPublic(file.StoragePath, convertType);
                    specs.Add(new FileConvertPayload
                    {
                        FileCode = file.FileCode,
                        FileName = file.FileName,
                        SourcePath = file.StoragePath,
                        TargetPath = targetPath,
                        ConvertType = convertType
                    });
                    queuedFiles.Add(file);
                }

                if (specs.Count > 0)
                {
                    var config = _db.Set<StandardDirectoryConfig>()
                        .FirstOrDefault(x => x.DirectoryCode == task.DirectoryCode && x.Enable == true);
                    var scopeKey = $"{orgCode}|{config?.StandardCode}|{config?.PhaseCode}";
                    var scopeInfo = JsonSerializer.Serialize(new
                    {
                        orgCode,
                        standardCode = config?.StandardCode,
                        phaseCode = config?.PhaseCode,
                        directoryCode = task.DirectoryCode
                    });

                    var locks = new List<YzhQueueManager.ResourceLockItem>
                    {
                        // 队列级目录锁（整个目录在队列期间禁止增删改）
                        new YzhQueueManager.ResourceLockItem
                        {
                            ResourceTable = YzhQueueManager.RESOURCE_DIR,
                            ResourceCode = task.DirectoryCode,
                            ResourceName = task.DirectoryCode,
                            TaskNo = null
                        }
                    };
                    locks.AddRange(specs.Select((s, i) => new YzhQueueManager.ResourceLockItem
                    {
                        ResourceTable = YzhQueueManager.RESOURCE_FILE,
                        ResourceCode = s.FileCode,
                        ResourceName = s.FileName,
                        TaskNo = i + 1
                    }));

                    var req = new YzhQueueManager.CreateQueueRequest
                    {
                        QueueType = "file_convert",
                        QueueName = $"文档转换-{specs.Count}个文件",
                        ScopeKey = scopeKey,
                        ScopeInfoJson = scopeInfo,
                        SourceType = "upload_task",
                        SourceId = taskId,
                        UserId = userId,
                        UserName = userName,
                        OrgCode = orgCode,
                        ResourceLocks = locks,
                        Tasks = specs.Select(s => new YzhQueueManager.TaskItem
                        {
                            TaskType = "file_convert",
                            Payload = JsonSerializer.Serialize(s, _payloadJsonOptions),
                            TaskId = taskId
                        }).ToList()
                    };

                    var (ok, queueError, queueCode, count) = await _queueManager.CreateQueueAsync(req);
                    if (!ok)
                    {
                        // 队列创建失败（资源被其他队列锁定）：不落库，文件保持"已上传未确认"，用户可稍后重试确认
                        return new WebResponseContent().Error(queueError);
                    }
                    // 队列中的文件置为无效（文档提取规则页隐藏），转换完成/失败后由执行器恢复
                    foreach (var f in queuedFiles)
                    {
                        f.IsValid = false;
                        f.ConvertStatus = "pending";
                        f.ConvertedStoragePath = null;
                        f.ConvertMessage = null;
                        MarkModified(f, nameof(StandardDirectoryFile.IsValid), nameof(StandardDirectoryFile.ConvertStatus),
                            nameof(StandardDirectoryFile.ConvertedStoragePath), nameof(StandardDirectoryFile.ConvertMessage));
                    }
                    convertCount = count;
                }

                // 6. 更新任务状态
                task.Status = "completed";
                task.ModifyDate = DateTime.Now;
                MarkModified(task, nameof(UploadTask.Status), nameof(UploadTask.ModifyDate));

                _db.SaveChanges();

                var msg = convertCount > 0
                    ? $"上传确认完成，共{task.TotalFiles}个文件，{convertCount}个文件正在转换"
                    : $"上传确认完成，共{task.TotalFiles}个文件";
                // 广播上传完成进度
                await BroadcastUploadProgressAsync(taskId);
                return new WebResponseContent().OK(msg, new { taskId, convertCount });
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"确认失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 彻底取消上传任务（支持 create/replace 模式）
        /// 语义：取消 = 彻底干掉本次上传过程，不允许重试。
        /// create 模式：删除本次任务新建的文件记录 + MinIO 对象（含原文件与转换后文件）
        /// replace 模式：取消替换，恢复为普通可用文件（原内容已被覆盖无法恢复，保留当前内容）
        /// 同时删除本次任务创建的空文件夹与上传任务记录。
        /// </summary>
        public async Task<WebResponseContent> UploadCancel(string taskId)
        {
            try
            {
                // 0. 该上传任务若仍有未结束的转换队列，先取消队列（幂等：已取消/已结束则跳过）
                var activeQueue = _db.Set<YzhQueue>()
                    .FirstOrDefault(q => q.SourceType == "upload_task"
                                      && q.SourceId == taskId
                                      && q.Status != "completed" && q.Status != "failed" && q.Status != "cancelled");
                if (activeQueue != null)
                {
                    var (_, cancelErr) = await _queueManager.CancelQueueAsync(activeQueue.QueueCode);
                    if (cancelErr != null)
                        Console.WriteLine($"[UploadCancel] 取消关联转换队列失败: {cancelErr}");
                }

                var bucketName = _configuration["MinIO:BucketName"] ?? "cert-platform";
                var restoredCount = 0;
                var deletedFiles = 0;
                var deletedFolders = 0;

                // 1. 彻底删除本次任务关联的所有文件（含转换中/已转换），不再跳过任何状态
                // 按 TaskId 幂等清理：即使任务记录已被清理（如递归取消场景）也能正确删除
                var allFiles = _db.Set<StandardDirectoryFile>()
                    .Where(x => x.TaskId == taskId)
                    .ToList();

                foreach (var file in allFiles)
                {
                    // 删除 MinIO 对象：原文件 + 转换后文件
                    foreach (var objPath in new[] { file.StoragePath, file.ConvertedStoragePath })
                    {
                        if (string.IsNullOrEmpty(objPath)) continue;
                        try
                        {
                            var rmArgs = new RemoveObjectArgs()
                                .WithBucket(bucketName)
                                .WithObject(objPath.TrimStart('/'));
                            await _minioClient.RemoveObjectAsync(rmArgs).ConfigureAwait(false);
                        }
                        catch (Exception ex) { Console.WriteLine($"[StandardDirectoryService] MinIO删除失败: {ex.Message}"); /* 忽略单对象删除失败 */ }
                    }

                    // create 模式（本次任务新建的记录）：直接删除；replace 模式：取消替换并恢复
                    var replaceMarker = $"[upload-replace:{taskId}]";
                    if ((file.Remark ?? "").Contains(replaceMarker))
                    {
                        // ===== replace 模式：取消替换 =====
                        // 原文件内容已被覆盖无法恢复，保留当前内容，恢复为普通可用文件记录
                        file.UploadStatus = "active";
                        file.TaskId = null; // 清除任务关联
                        file.ConvertStatus = null;
                        file.ConvertedStoragePath = null;
                        file.ConvertMessage = null;
                        file.Remark = (file.Remark ?? "").Replace(replaceMarker, "").Trim();
                        MarkModified(file, nameof(StandardDirectoryFile.UploadStatus), nameof(StandardDirectoryFile.TaskId),
                            nameof(StandardDirectoryFile.ConvertStatus), nameof(StandardDirectoryFile.ConvertedStoragePath),
                            nameof(StandardDirectoryFile.ConvertMessage), nameof(StandardDirectoryFile.Remark));
                        restoredCount++;
                    }
                    else
                    {
                        // ===== create 模式：删除新记录 =====
                        _db.Set<StandardDirectoryFile>().Remove(file);
                        deletedFiles++;
                    }
                }

                // 2. 删除本次任务创建的空文件夹（含已激活的；若被其他任务复用了文件则保留）
                var taskFolders = _db.Set<StandardDirectoryFolder>()
                    .Where(x => x.TaskId == taskId)
                    .ToList();

                foreach (var folder in taskFolders)
                {
                    var hasFiles = _db.Set<StandardDirectoryFile>()
                        .Any(x => x.FolderCode == folder.FolderCode);
                    if (!hasFiles)
                    {
                        _db.Set<StandardDirectoryFolder>().Remove(folder);
                        deletedFolders++;
                    }
                }

                // 3. 删除上传任务记录（若仍存在）
                var task = _db.Set<UploadTask>()
                    .FirstOrDefault(x => x.TaskId == taskId);
                if (task != null)
                    _db.Set<UploadTask>().Remove(task);

                await _db.SaveChangesAsync();

                var msg = $"已彻底清理本次上传：删除{deletedFiles}个文件和{deletedFolders}个文件夹";
                if (restoredCount > 0)
                    msg += $"，已恢复{restoredCount}个替换文件";
                return new WebResponseContent().OK(msg);
            }
            catch (Exception ex)
            {
                return new WebResponseContent().Error($"清理失败：{ex.Message}");
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
            // 用原生 SQL 清理 IsValid=0 的脏数据，避免 EF Core change tracker 冲突
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();

            // 删除 IsValid=0 的文件（无条件清理：预创建记录未确认即孤儿；
            // 注意 UploadInit 对 .doc/.xls 预创建时就置 ConvertStatus='pending'，
            // 若此处按 convert_status 过滤会永远清不掉这些孤儿，下次上传撞唯一键失败。
            // 转换只发生在 confirm 后的 IsValid=1 记录上，清理 IsValid=0 不影响转换）
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM cert_standard_directory_file WHERE DirectoryCode = @dc AND IsValid = 0";
                var p = cmd.CreateParameter();
                p.ParameterName = "@dc";
                p.Value = directoryCode;
                cmd.Parameters.Add(p);
                await cmd.ExecuteNonQueryAsync();
            }

            // 删除 IsValid=0 的文件夹
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM cert_standard_directory_folder WHERE DirectoryCode = @dc AND IsValid = 0";
                var p = cmd.CreateParameter();
                p.ParameterName = "@dc";
                p.Value = directoryCode;
                cmd.Parameters.Add(p);
                await cmd.ExecuteNonQueryAsync();
            }

            // 删除 IsValid=0 的任务
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM cert_upload_task WHERE DirectoryCode = @dc AND Status != 'completed'";
                var p = cmd.CreateParameter();
                p.ParameterName = "@dc";
                p.Value = directoryCode;
                cmd.Parameters.Add(p);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// 获取同级最大序号（上传版本，支持 taskId 隔离）
        /// </summary>
        private int GetMaxSequenceForUpload(string directoryCode, int depth)
        {
            var query = _db.Set<StandardDirectoryFolder>()
                .Where(x => x.DirectoryCode == directoryCode
                         && x.Depth == depth);

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

        /// <summary>
        /// 重试失败的文档转换
        /// 扫描 convert_status=failed 以及无活跃资源锁的 pending 孤儿 doc/xls 文件，
        /// 按目录分组重新创建转换队列（资源锁 + 防重复入队逻辑与上传确认一致）。
        /// </summary>
        public async Task<WebResponseContent> RetryFailedConversionsAsync()
        {
            try
            {
                var userId = UserContext.Current.UserId;
                var userName = UserContext.Current.UserName;

                // 1. 候选文件：doc/xls 且 failed 或 pending（pending 多为历史遗留孤儿，无活跃队列）
                var candidates = _db.Set<StandardDirectoryFile>()
                    .Where(f => f.DeleteTime == null
                                && (f.FileType == "doc" || f.FileType == "xls")
                                && (f.ConvertStatus == "failed" || f.ConvertStatus == "pending"))
                    .ToList();

                if (candidates.Count == 0)
                    return new WebResponseContent().OK("没有需要重试的失败转换文件", new { enqueued = 0, queueCount = 0 });

                // 2. 排除仍在队列中（有活跃资源锁）的文件；源文件在 MinIO 已不存在的无法转换，跳过并提示
                var activeLocks = _db.Set<YzhQueueResourceLock>().AsNoTracking()
                    .Where(r => r.Status == "locked" && r.ResourceTable == YzhQueueManager.RESOURCE_FILE)
                    .Select(r => r.ResourceCode)
                    .ToList();
                var toRetry = new List<StandardDirectoryFile>();
                var missingSources = new List<string>();
                foreach (var f in candidates)
                {
                    if (activeLocks.Contains(f.FileCode)) continue;
                    if (!await SourceExistsAsync(f.StoragePath))
                    {
                        missingSources.Add(f.FileName);
                        continue;
                    }
                    toRetry.Add(f);
                }
                if (toRetry.Count == 0)
                {
                    var tip = missingSources.Count > 0
                        ? $"没有可重试的文件（{missingSources.Count} 个文件源文件已不存在，无法转换）"
                        : "失败文件均在队列处理中，无需重复重试";
                    return new WebResponseContent().OK(tip, new { enqueued = 0, queueCount = 0, missingCount = missingSources.Count });
                }

                // 3. 按目录分组建队（每组一个队列，目录锁保证与上传/删除互斥）
                var groups = toRetry.GroupBy(f => f.DirectoryCode).ToList();
                var enqueued = 0;
                var queueCodes = new List<string>();
                var skipped = new List<string>();
                var seq = 0;

                foreach (var group in groups)
                {
                    var config = _db.Set<StandardDirectoryConfig>().AsNoTracking()
                        .FirstOrDefault(c => c.DirectoryCode == group.Key && c.Enable == true);
                    // 机构编码从存储路径推导：/CB001/标准/阶段/...
                    var orgCode = DeriveOrgCodeFromPath(group.First().StoragePath);
                    var scopeKey = $"{orgCode}|{config?.StandardCode}|{config?.PhaseCode}";

                    var specs = new List<FileConvertPayload>();
                    foreach (var file in group)
                    {
                        var ext = (file.FileType ?? "").ToLower();
                        var convertType = ext == "doc" ? "doc2docx" : "xls2xlsx";
                        specs.Add(new FileConvertPayload
                        {
                            FileCode = file.FileCode,
                            FileName = file.FileName,
                            SourcePath = file.StoragePath,
                            TargetPath = _convertService.GenerateTargetPathPublic(file.StoragePath, convertType),
                            ConvertType = convertType
                        });
                    }

                    var locks = new List<YzhQueueManager.ResourceLockItem>
                    {
                        new YzhQueueManager.ResourceLockItem
                        {
                            ResourceTable = YzhQueueManager.RESOURCE_DIR,
                            ResourceCode = group.Key,
                            ResourceName = group.Key,
                            TaskNo = null
                        }
                    };
                    locks.AddRange(specs.Select((s, i) => new YzhQueueManager.ResourceLockItem
                    {
                        ResourceTable = YzhQueueManager.RESOURCE_FILE,
                        ResourceCode = s.FileCode,
                        ResourceName = s.FileName,
                        TaskNo = i + 1
                    }));

                    var req = new YzhQueueManager.CreateQueueRequest
                    {
                        QueueType = "file_convert",
                        QueueName = $"失败重试-{specs.Count}个文件",
                        ScopeKey = scopeKey,
                        SourceType = "retry_failed",
                        // uk_source 唯一：每次调用每个目录的 SourceId 必须唯一
                        SourceId = $"retry_{DateTime.Now:yyyyMMddHHmmss}{seq++}_{group.Key}",
                        UserId = userId,
                        UserName = userName,
                        OrgCode = orgCode,
                        ResourceLocks = locks,
                        Tasks = specs.Select(s => new YzhQueueManager.TaskItem
                        {
                            TaskType = "file_convert",
                            Payload = JsonSerializer.Serialize(s, _payloadJsonOptions),
                            TaskId = group.Key
                        }).ToList()
                    };

                    var (ok, queueError, queueCode, count) = await _queueManager.CreateQueueAsync(req);
                    if (!ok)
                    {
                        // 该目录被其他运行中队列占用，跳过并记录
                        skipped.Add($"{group.Key}（{queueError}）");
                        continue;
                    }

                    // 文件置为隐藏 + pending，转换完成后由执行器恢复可见
                    foreach (var f in group)
                    {
                        f.IsValid = false;
                        f.ConvertStatus = "pending";
                        f.ConvertedStoragePath = null;
                        f.ConvertMessage = null;
                        MarkModified(f, nameof(StandardDirectoryFile.IsValid), nameof(StandardDirectoryFile.ConvertStatus),
                            nameof(StandardDirectoryFile.ConvertedStoragePath), nameof(StandardDirectoryFile.ConvertMessage));
                    }
                    await _db.SaveChangesAsync();
                    enqueued += count;
                    queueCodes.Add(queueCode);
                }

                var skipTip = new List<string>();
                if (skipped.Count > 0) skipTip.Add($"{skipped.Count} 个目录被其他队列占用已跳过");
                if (missingSources.Count > 0) skipTip.Add($"{missingSources.Count} 个文件源文件不存在已跳过");
                var msg = enqueued > 0
                    ? $"已重新入队 {enqueued} 个失败文件（{queueCodes.Count} 个队列），" + (skipTip.Count > 0 ? string.Join("，", skipTip) : "正在后台转换")
                    : "没有可重试的文件" + (skipTip.Count > 0 ? $"（{string.Join("，", skipTip)}）" : "");
                return new WebResponseContent().OK(msg, new { enqueued, queueCount = queueCodes.Count, skipped, missingCount = missingSources.Count });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StandardDirectoryService] 重试失败转换出错: {ex.Message}");
                return new WebResponseContent().Error($"重试失败转换出错：{ex.Message}");
            }
        }

        /// <summary>从 MinIO 存储路径推导机构编码：/CB001/标准/阶段/... → CB001</summary>
        private static string DeriveOrgCodeFromPath(string storagePath)
        {
            if (string.IsNullOrEmpty(storagePath)) return null;
            var segments = storagePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) return null;
            // 兼容 V3 双顶层文件夹：/standard-directory/{Org}/... 与 /enterprise-documents/{Ent}/{Org}/...
            // 首段是固定前缀时取下一段作为机构编码
            int idx = 0;
            if (segments[idx] == "standard-directory")
                idx = 1;
            else if (segments[idx] == "enterprise-documents")
                idx = 2; // {Ent}/{Org}/...
            return segments.Length > idx ? segments[idx] : null;
        }

        /// <summary>检查 MinIO 源文件是否存在（缺失的文件无法转换，重试时跳过）</summary>
        private async Task<bool> SourceExistsAsync(string storagePath)
        {
            if (string.IsNullOrEmpty(storagePath)) return false;
            try
            {
                var bucketName = _configuration["MinIO:BucketName"] ?? "cert-platform";
                var statArgs = new StatObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(storagePath.TrimStart('/'));
                await _minioClient.StatObjectAsync(statArgs);
                return true;
            }
            catch (Minio.Exceptions.ObjectNotFoundException)
            {
                return false;
            }
            catch
            {
                // 其他异常（网络等）不阻塞重试，交由队列执行器判断
                return true;
            }
        }

        /// <summary>
        /// 广播上传进度到 SignalR 客户端组
        /// </summary>
        private async Task BroadcastUploadProgressAsync(string taskId)
        {
            try
            {
                var task = await _db.Set<UploadTask>()
                    .FirstOrDefaultAsync(x => x.TaskId == taskId);
                if (task == null) return;

                var files = await _db.Set<StandardDirectoryFile>()
                    .Where(x => x.TaskId == taskId)
                    .ToListAsync();

                var uploaded = files.Count(f => f.UploadStatus == "uploaded" || f.UploadStatus == "active");
                var pending = files.Count(f => f.UploadStatus == "pending" || f.UploadStatus == "replacing");

                var progress = new
                {
                    taskId,
                    status = task.Status,
                    totalFiles = task.TotalFiles,
                    uploadedFiles = uploaded,
                    pendingFiles = pending,
                    percent = task.TotalFiles > 0 ? (int)((decimal)uploaded / task.TotalFiles * 100) : 0,
                    updateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                await _hubContext.Clients.Group($"upload_{taskId}").SendAsync("ReceiveUploadProgress", progress);
            }
            catch
            {
                // 广播失败不影响主流程
            }
        }

        #endregion
    }
}
