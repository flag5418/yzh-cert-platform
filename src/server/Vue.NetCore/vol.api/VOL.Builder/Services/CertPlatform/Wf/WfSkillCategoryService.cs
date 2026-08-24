using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Core.ManageUser;
using VOL.Core.Utilities;
using VOL.Entity.CertPlatform.Wf;
using VOL.Builder.IRepositories.CertPlatform;
using VOL.Builder.IServices.CertPlatform;

namespace VOL.Builder.Services.CertPlatform
{
    public class WfSkillCategoryService : IWfSkillCategoryService
    {
        private readonly IWfSkillCategoryRepository _repository;

        public WfSkillCategoryService(IWfSkillCategoryRepository repository)
        {
            _repository = repository;
        }

        public async Task<(bool ok, string message)> SaveAsync(WfSkillCategory entity)
        {
            if (string.IsNullOrWhiteSpace(entity.CategoryCode)) return (false, "分类编码不能为空");
            if (string.IsNullOrWhiteSpace(entity.CategoryName)) return (false, "分类名称不能为空");

            if (entity.Id > 0)
            {
                var existing = await _repository.FindFirstAsync(x => x.Id == entity.Id);
                if (existing == null) return (false, "分类不存在");
                if (await _repository.ExistsAsync(x => x.CategoryCode == entity.CategoryCode && x.Id != entity.Id))
                    return (false, $"分类编码 {entity.CategoryCode} 已存在");
                existing.CategoryCode = entity.CategoryCode;
                existing.CategoryName = entity.CategoryName;
                existing.Icon = entity.Icon;
                existing.Color = entity.Color;
                existing.SortOrder = entity.SortOrder;
                existing.Remark = entity.Remark;
                existing.ModifyDate = DateTime.Now;
                existing.Modifier = UserContext.Current?.UserName;
                _repository.Update(existing, new[]
                {
                    "CategoryCode", "CategoryName", "Icon", "Color", "SortOrder", "Remark", "ModifyDate", "Modifier"
                }, true);
                return (true, "保存成功");
            }

            if (await _repository.ExistsAsync(x => x.CategoryCode == entity.CategoryCode))
                return (false, $"分类编码 {entity.CategoryCode} 已存在");
            entity.Code = Guid.NewGuid().ToString("N");
            entity.Enable = true;
            entity.Status = "active";
            entity.CreateDate = DateTime.Now;
            entity.Creator = UserContext.Current?.UserName;
            await _repository.AddAsync(entity);
            await _repository.SaveChangesAsync();
            return (true, "保存成功");
        }

        public async Task<List<WfSkillCategory>> GetListAsync()
        {
            return await _repository.FindAsIQueryable(x => x.Enable)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.CategoryCode)
                .ToListAsync();
        }

        public async Task<(bool ok, string message)> DeleteAsync(long id)
        {
            var entity = await _repository.FindFirstAsync(x => x.Id == id);
            if (entity == null) return (false, "分类不存在");
            // 分类下仍有启用 Skill 时拒绝删除
            bool hasSkill = await _repository.ExistsAsync<Skill>(x => x.Category == entity.CategoryCode && x.IsActive && x.Enable);
            if (hasSkill) return (false, $"分类「{entity.CategoryName}」下仍有启用 Skill，请先迁移或停用");
            _repository.Delete(entity, true);
            return (true, "删除成功");
        }

        public async Task<bool> ToggleActiveAsync(long id)
        {
            var entity = await _repository.FindFirstAsync(x => x.Id == id);
            if (entity == null) return false;
            entity.Enable = !entity.Enable;
            entity.ModifyDate = DateTime.Now;
            entity.Modifier = UserContext.Current?.UserName;
            _repository.Update(entity, new[] { "Enable", "ModifyDate", "Modifier" }, true);
            return true;
        }
    }
}
