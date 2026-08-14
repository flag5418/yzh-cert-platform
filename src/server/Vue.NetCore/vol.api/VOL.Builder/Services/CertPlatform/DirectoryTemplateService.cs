using System;
using System.Collections.Generic;
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
    /// </summary>
    public class DirectoryTemplateService : IDirectoryTemplateService
    {
        private readonly VOLContext _db;

        public DirectoryTemplateService(VOLContext dbContext)
        {
            _db = dbContext;
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

            // 删除文件要求
            var requirements = await _db.Set<FileRequirement>()
                .Where(x => allCodes.Contains(x.FolderCode))
                .ToListAsync();
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
                existing.FillModifyInfo(userId, userName);
            }

            await _db.SaveChangesAsync();
            return new WebResponseContent().OK("保存成功");
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

            _db.Set<FileRequirement>().Remove(req);
            await _db.SaveChangesAsync();
            return new WebResponseContent().OK("删除成功");
        }

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
    }
}
