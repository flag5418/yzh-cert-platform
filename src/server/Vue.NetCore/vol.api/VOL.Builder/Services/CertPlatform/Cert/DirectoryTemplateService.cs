using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.ManageUser;
using VOL.Core.Utilities;
using VOL.Entity.CertPlatform.Cert;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// 标准目录模板管理服务实现
    /// 包含文件夹树 CRUD + 模板文件上传/下载/删除/改名
    /// </summary>
    public class DirectoryTemplateService : IDirectoryTemplateService
    {
        private readonly VOLContext _db;
        private readonly IMinIOHelper _minio;
        private readonly ICodeGeneratorService _codeGenerator;

        public DirectoryTemplateService(VOLContext dbContext, IMinIOHelper minio, ICodeGeneratorService codeGenerator)
        {
            _db = dbContext;
            _minio = minio;
            _codeGenerator = codeGenerator;
        }

        /// <summary>
        /// 获取标准-阶段配置下的目录树
        /// </summary>
        public async Task<List<object>> GetTreeAsync(string configCode)
        {
            var folders = await _db.Set<DirectoryTemplate>()
                .Where(x => x.ConfigCode == configCode && x.Enable == true)
                .OrderBy(x => x.SortOrder)
                .Select(x => new
                {
                    x.Code,
                    x.ConfigCode,
                    x.ParentCode,
                    x.FolderName,
                    x.SortOrder
                })
                .ToListAsync();

            return folders.Cast<object>().ToList();
        }

        /// <summary>
        /// 新增文件夹
        /// </summary>
        public async Task<WebResponseContent> AddFolderAsync(DirectoryTemplate entity)
        {
            if (string.IsNullOrEmpty(entity.FolderName))
                return new WebResponseContent().Error("文件夹名称不能为空");
            if (string.IsNullOrEmpty(entity.ConfigCode))
                return new WebResponseContent().Error("配置编码不能为空");

            entity.Code = Guid.NewGuid().ToString("N");
            entity.Enable = true;
            entity.Status = "active";

            var userId = UserContext.Current?.UserId ?? 0;
            var userName = UserContext.Current?.UserName ?? "system";
            entity.FillCreateInfo(userId, userName);

            _db.Set<DirectoryTemplate>().Add(entity);
            await _db.SaveChangesAsync();

            return new WebResponseContent().OK("创建成功", entity.Code);
        }

        /// <summary>
        /// 修改文件夹
        /// </summary>
        public async Task<WebResponseContent> UpdateFolderAsync(DirectoryTemplate entity)
        {
            if (string.IsNullOrEmpty(entity.Code))
                return new WebResponseContent().Error("文件夹编码不能为空");

            var existing = await _db.Set<DirectoryTemplate>()
                .FirstOrDefaultAsync(x => x.Code == entity.Code && x.Enable == true);
            if (existing == null)
                return new WebResponseContent().Error("文件夹不存在");

            existing.FolderName = entity.FolderName;
            existing.ParentCode = entity.ParentCode;
            existing.SortOrder = entity.SortOrder;

            var userId = UserContext.Current?.UserId ?? 0;
            var userName = UserContext.Current?.UserName ?? "system";
            existing.FillModifyInfo(userId, userName);

            await _db.SaveChangesAsync();
            return new WebResponseContent().OK("更新成功");
        }

        /// <summary>
        /// 删除文件夹（级联删除子文件夹和文件要求）
        /// </summary>
        public async Task<WebResponseContent> DeleteFolderAsync(string folderCode)
        {
            var folder = await _db.Set<DirectoryTemplate>()
                .FirstOrDefaultAsync(x => x.Code == folderCode);
            if (folder == null)
                return new WebResponseContent().Error("文件夹不存在");

            // 收集所有子文件夹编码（递归）
            var allCodes = new List<string> { folderCode };
            await CollectChildCodesAsync(folderCode, allCodes);

            // 删除文件要求（含 MinIO 上的模板文件）
            var requirements = await _db.Set<FileRequirement>()
                .Where(x => allCodes.Contains(x.FolderCode))
                .ToListAsync();

            foreach (var req in requirements)
            {
                if (!string.IsNullOrEmpty(req.TemplateStoragePath))
                {
                    try { await _minio.DeleteAsync(req.TemplateStoragePath); }
                    catch { /* MinIO 删除失败不阻断 DB 操作 */ }
                }
            }
            _db.Set<FileRequirement>().RemoveRange(requirements);

            // 删除子文件夹
            var folders = await _db.Set<DirectoryTemplate>()
                .Where(x => allCodes.Contains(x.Code))
                .ToListAsync();
            _db.Set<DirectoryTemplate>().RemoveRange(folders);

            await _db.SaveChangesAsync();
            return new WebResponseContent().OK("删除成功");
        }

        /// <summary>
        /// 获取文件夹下的文件要求列表
        /// </summary>
        public async Task<List<FileRequirement>> GetFileRequirementsAsync(string folderCode)
        {
            return await _db.Set<FileRequirement>()
                .Where(x => x.FolderCode == folderCode && x.Enable == true)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
        }

        /// <summary>
        /// 新增/修改文件要求
        /// </summary>
        public async Task<WebResponseContent> SaveFileRequirementAsync(FileRequirement entity)
        {
            var userId = UserContext.Current?.UserId ?? 0;
            var userName = UserContext.Current?.UserName ?? "system";

            if (string.IsNullOrEmpty(entity.Code))
            {
                // 新增
                entity.Code = Guid.NewGuid().ToString("N");
                entity.Enable = true;
                entity.Status = "active";
                entity.FillCreateInfo(userId, userName);
                _db.Set<FileRequirement>().Add(entity);
            }
            else
            {
                // 修改
                var existing = await _db.Set<FileRequirement>()
                    .FirstOrDefaultAsync(x => x.Code == entity.Code);
                if (existing == null)
                    return new WebResponseContent().Error("文件要求不存在");

                existing.FileNameTemplate = entity.FileNameTemplate;
                existing.FileType = entity.FileType;
                existing.IsRequired = entity.IsRequired;
                existing.MaxSizeMB = entity.MaxSizeMB;
                existing.Description = entity.Description;
                existing.SortOrder = entity.SortOrder;
                existing.StandardCode = entity.StandardCode;
                existing.FillModifyInfo(userId, userName);
            }

            await _db.SaveChangesAsync();
            return new WebResponseContent().OK("保存成功", entity.Code);
        }

        /// <summary>
        /// 删除文件要求
        /// </summary>
        public async Task<WebResponseContent> DeleteFileRequirementAsync(string requirementCode)
        {
            var req = await _db.Set<FileRequirement>()
                .FirstOrDefaultAsync(x => x.Code == requirementCode);
            if (req == null)
                return new WebResponseContent().Error("文件要求不存在");

            // 同时删除 MinIO 上的模板文件
            if (!string.IsNullOrEmpty(req.TemplateStoragePath))
            {
                try { await _minio.DeleteAsync(req.TemplateStoragePath); }
                catch { /* MinIO 删除失败不阻断 DB 操作 */ }
            }

            _db.Set<FileRequirement>().Remove(req);
            await _db.SaveChangesAsync();
            return new WebResponseContent().OK("删除成功");
        }

        // ===== 模板文件管理（标准目录参考文件） =====

        /// <summary>
        /// 上传标准目录模板文件到 MinIO
        /// OSS 路径：/standard-directory/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
        /// </summary>
        public async Task<WebResponseContent> UploadTemplateFileAsync(
            string requirementCode, string fileName, Stream stream, long fileSize)
        {
            if (string.IsNullOrEmpty(requirementCode))
                return new WebResponseContent().Error("文件要求编码不能为空");

            // 1. 查询文件要求记录
            var req = await _db.Set<FileRequirement>()
                .FirstOrDefaultAsync(x => x.Code == requirementCode && x.Enable == true);
            if (req == null)
                return new WebResponseContent().Error("文件要求不存在");

            // 2. 通过 folder_code → cert_directory_template.config_code → cert_standard_phase_config 获取维度信息
            var folderInfo = await _db.Set<DirectoryTemplate>()
                .Where(x => x.Code == req.FolderCode)
                .Select(x => new { x.ConfigCode, x.FolderName, x.ParentCode })
                .FirstOrDefaultAsync();
            if (folderInfo == null)
                return new WebResponseContent().Error("文件夹不存在");

            var configInfo = await _db.Set<StandardPhaseConfig>()
                .Where(x => x.Code == folderInfo.ConfigCode)
                .Select(x => new { x.StandardCode, x.PhaseCode })
                .FirstOrDefaultAsync();
            if (configInfo == null)
                return new WebResponseContent().Error("标准阶段配置不存在");

            // 3. 构建文件夹路径（从树结构中构建完整路径）
            var folderPath = await BuildFolderPathAsync(req.FolderCode);

            // 4. 生成 OSS 存储路径
            // 标准目录模板是全局的，OrgCode 使用 "GLOBAL" 作为占位
            var storagePath = _codeGenerator.GenerateStandardDirectoryPath(
                "GLOBAL", configInfo.StandardCode,
                configInfo.PhaseCode, folderPath, fileName);

            // 5. 如果已有旧文件，先删除
            if (!string.IsNullOrEmpty(req.TemplateStoragePath))
            {
                try { await _minio.DeleteAsync(req.TemplateStoragePath); }
                catch { /* 旧文件可能已被删除，忽略错误 */ }
            }

            // 6. 上传到 MinIO
            await _minio.UploadAsync(storagePath, stream, fileSize);

            // 7. 更新数据库记录
            req.TemplateStoragePath = storagePath;
            req.TemplateFileName = fileName;

            var userId = UserContext.Current?.UserId ?? 0;
            var userName = UserContext.Current?.UserName ?? "system";
            req.FillModifyInfo(userId, userName);

            // 同时更新 standard_code（冗余字段）
            if (string.IsNullOrEmpty(req.StandardCode))
                req.StandardCode = configInfo.StandardCode;

            // 显式标记为 Modified，确保 EF Core 变更跟踪
            _db.Entry(req).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            await _db.SaveChangesAsync();

            return new WebResponseContent().OK("模板文件上传成功", new
            {
                requirementCode,
                storagePath,
                fileName
            });
        }

        /// <summary>
        /// 下载标准目录模板文件
        /// </summary>
        public async Task<(Stream stream, string fileName, string contentType)> DownloadTemplateFileAsync(string requirementCode)
        {
            var req = await _db.Set<FileRequirement>()
                .FirstOrDefaultAsync(x => x.Code == requirementCode && x.Enable == true);
            if (req == null || string.IsNullOrEmpty(req.TemplateStoragePath))
                return (null, null, null);

            var (stream, contentType) = await _minio.DownloadAsync(req.TemplateStoragePath);
            return (stream, req.TemplateFileName ?? req.FileNameTemplate, contentType);
        }

        /// <summary>
        /// 删除标准目录模板文件
        /// </summary>
        public async Task<WebResponseContent> DeleteTemplateFileAsync(string requirementCode)
        {
            var req = await _db.Set<FileRequirement>()
                .FirstOrDefaultAsync(x => x.Code == requirementCode);
            if (req == null)
                return new WebResponseContent().Error("文件要求不存在");

            if (string.IsNullOrEmpty(req.TemplateStoragePath))
                return new WebResponseContent().Error("无模板文件");

            // 删除 MinIO 上的文件
            await _minio.DeleteAsync(req.TemplateStoragePath);

            // 清除数据库中的路径
            req.TemplateStoragePath = null;
            req.TemplateFileName = null;

            var userId = UserContext.Current?.UserId ?? 0;
            var userName = UserContext.Current?.UserName ?? "system";
            req.FillModifyInfo(userId, userName);

            await _db.SaveChangesAsync();
            return new WebResponseContent().OK("模板文件已删除");
        }

        /// <summary>
        /// 重命名标准目录模板文件
        /// </summary>
        public async Task<WebResponseContent> RenameTemplateFileAsync(string requirementCode, string newFileName)
        {
            if (string.IsNullOrEmpty(newFileName))
                return new WebResponseContent().Error("新文件名不能为空");

            var req = await _db.Set<FileRequirement>()
                .FirstOrDefaultAsync(x => x.Code == requirementCode);
            if (req == null)
                return new WebResponseContent().Error("文件要求不存在");

            if (string.IsNullOrEmpty(req.TemplateStoragePath))
                return new WebResponseContent().Error("无模板文件");

            // 构建新路径（替换最后一部分文件名）
            var oldPath = req.TemplateStoragePath;
            var lastSlash = oldPath.LastIndexOf('/');
            var newPath = oldPath.Substring(0, lastSlash + 1) + newFileName;

            // MinIO 重命名（Copy + Delete）
            await _minio.RenameAsync(oldPath, newPath);

            // 更新数据库
            req.TemplateStoragePath = newPath;
            req.TemplateFileName = newFileName;

            var userId = UserContext.Current?.UserId ?? 0;
            var userName = UserContext.Current?.UserName ?? "system";
            req.FillModifyInfo(userId, userName);

            await _db.SaveChangesAsync();
            return new WebResponseContent().OK("重命名成功", new { newPath });
        }

        // ===== 私有方法 =====

        /// <summary>
        /// 递归收集子文件夹编码
        /// </summary>
        private async Task CollectChildCodesAsync(string parentCode, List<string> codes)
        {
            var children = await _db.Set<DirectoryTemplate>()
                .Where(x => x.ParentCode == parentCode && x.Enable == true)
                .Select(x => x.Code)
                .ToListAsync();

            foreach (var child in children)
            {
                if (!codes.Contains(child))
                {
                    codes.Add(child);
                    await CollectChildCodesAsync(child, codes);
                }
            }
        }

        /// <summary>
        /// 从当前文件夹向上遍历，构建完整文件夹路径
        /// 如：1质量手册/程序文件
        /// </summary>
        private async Task<string> BuildFolderPathAsync(string folderCode)
        {
            var parts = new List<string>();
            var currentCode = folderCode;

            while (!string.IsNullOrEmpty(currentCode))
            {
                var folder = await _db.Set<DirectoryTemplate>()
                    .Where(x => x.Code == currentCode)
                    .Select(x => new { x.FolderName, x.ParentCode })
                    .FirstOrDefaultAsync();

                if (folder == null) break;

                parts.Insert(0, folder.FolderName);
                currentCode = folder.ParentCode;
            }

            return string.Join("/", parts);
        }
    }
}
