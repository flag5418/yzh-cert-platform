using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Wf;

namespace VOL.Builder.IServices.CertPlatform
{
    public interface IWfSkillCategoryService : IDependency
    {
        /// <summary>启用分类列表（页面左侧导航，按 sort_order）</summary>
        Task<List<WfSkillCategory>> GetListAsync();

        /// <summary>保存（新建/编辑，category_code 唯一校验）</summary>
        Task<(bool ok, string message)> SaveAsync(WfSkillCategory entity);

        /// <summary>删除（分类下仍有启用 Skill 时拒绝）</summary>
        Task<(bool ok, string message)> DeleteAsync(long id);

        /// <summary>启停切换（enable）</summary>
        Task<bool> ToggleActiveAsync(long id);
    }
}
