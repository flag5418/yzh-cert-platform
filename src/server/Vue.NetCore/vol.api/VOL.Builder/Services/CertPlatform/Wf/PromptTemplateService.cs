using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VOL.Core.BaseProvider;
using VOL.Core.Utilities;

using VOL.Core.EFDbContext;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Wf;
using VOL.Builder.IServices.CertPlatform;
using VOL.Builder.IRepositories.CertPlatform;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// Prompt 模板服务实现。
    /// <para>职责：CRUD + 按类型/技能筛选 + 激活管理。</para>
    /// </summary>
    public class PromptTemplateService : IPromptTemplateService, IDependency
    {
        private readonly IPromptTemplateRepository _repository;

        public static IPromptTemplateService Instance
        {
            get { return AutofacContainerModule.GetService<IPromptTemplateService>(); }
        }

        [ActivatorUtilitiesConstructor]
        public PromptTemplateService(IPromptTemplateRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// 获取提示词列表（可按类型和适用技能筛选）。
        /// </summary>
        public async Task<WebResponseContent> GetListAsync(string? promptType = null, string? skillTarget = null)
        {
            var query = _repository.DbContext.Set<PromptTemplate>()
                .Where(x => x.Enable == true);

            if (!string.IsNullOrWhiteSpace(promptType))
                query = query.Where(x => x.PromptType == promptType);
            if (!string.IsNullOrWhiteSpace(skillTarget))
                query = query.Where(x => x.SkillTarget == skillTarget || x.SkillTarget == null);

            var list = await query.OrderBy(x => x.PromptType).ThenBy(x => x.PromptCode).ToListAsync();
            return new WebResponseContent().OK(data: list);
        }

        /// <summary>
        /// 根据 prompt_code 获取单条提示词。
        /// </summary>
        public async Task<PromptTemplate?> GetByCodeAsync(string promptCode)
        {
            return await _repository.DbContext.Set<PromptTemplate>()
                .FirstOrDefaultAsync(x => x.PromptCode == promptCode && x.Enable == true);
        }

        /// <summary>
        /// 获取指定类型当前生效的提示词（按 skillTarget 优先匹配，回退到 all）。
        /// </summary>
        public async Task<PromptTemplate?> GetActiveAsync(string promptType, string? skillTarget = null)
        {
            var query = _repository.DbContext.Set<PromptTemplate>()
                .Where(x => x.PromptType == promptType
                     && x.IsActive == true
                     && x.Enable == true);

            if (!string.IsNullOrWhiteSpace(skillTarget))
            {
                var specific = await query.FirstOrDefaultAsync(x => x.SkillTarget == skillTarget);
                if (specific != null) return specific;
            }

            return await query.FirstOrDefaultAsync(x => x.SkillTarget == null || x.SkillTarget == "all");
        }

        /// <summary>
        /// 创建或更新提示词（按 prompt_code 幂等匹配）。
        /// </summary>
        public async Task<bool> SaveAsync(PromptTemplate entity)
        {
            var existing = await _repository.DbContext.Set<PromptTemplate>()
                .FirstOrDefaultAsync(x => x.PromptCode == entity.PromptCode && x.Enable == true);

            if (existing == null)
            {
                entity.Code = System.Guid.NewGuid().ToString("N");
                entity.Version = 1;
                entity.IsActive = true;
                _repository.Add(entity);
            }
            else
            {
                entity.Code = existing.Code;
                entity.Version = existing.Version + 1;
                entity.IsActive = true;
                entity.CreateID = existing.CreateID;
                entity.Creator = existing.Creator;
                entity.CreateDate = existing.CreateDate;
                entity.DeleteID = null;
                entity.Deleter = null;
                entity.DeleteTime = null;
                entity.Enable = true;

                _repository.Update(entity);
            }

            return await _repository.DbContext.SaveChangesAsync() > 0;
        }

        /// <summary>
        /// 删除提示词（逻辑删除）。
        /// </summary>
        public async Task<bool> DeleteAsync(string promptCode)
        {
            var entity = await _repository.DbContext.Set<PromptTemplate>()
                .FirstOrDefaultAsync(x => x.PromptCode == promptCode && x.Enable == true);
            if (entity == null) return false;

            entity.Enable = false;
            return await _repository.DbContext.SaveChangesAsync() > 0;
        }

        /// <summary>
        /// 激活提示词（同类型其他版本设为不活跃）。
        /// </summary>
        public async Task<bool> ActivateAsync(string promptCode)
        {
            var target = await _repository.DbContext.Set<PromptTemplate>()
                .FirstOrDefaultAsync(x => x.PromptCode == promptCode && x.Enable == true);
            if (target == null) return false;

            var others = await _repository.DbContext.Set<PromptTemplate>()
                .Where(x => x.PromptType == target.PromptType
                     && x.PromptCode != promptCode
                     && x.Enable == true)
                .ToListAsync();
            foreach (var item in others)
            {
                item.IsActive = false;
                _repository.Update(item);
            }

            target.IsActive = true;
            _repository.Update(target);
            return await _repository.DbContext.SaveChangesAsync() > 0;
        }
    }
}
