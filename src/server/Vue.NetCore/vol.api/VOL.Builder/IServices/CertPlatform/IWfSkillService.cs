using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Wf;
using VOL.Entity.DomainModels;

namespace VOL.Builder.IServices.CertPlatform
{
    public interface IWfSkillService : IDependency
    {
        /// <summary>分页列表（keyword 过滤编码/名称，category 过滤分类）</summary>
        Task<PageGridData<dynamic>> GetPageDataAsync(PageDataOptions options, string keyword = null, string category = null);

        /// <summary>详情（主表 + 输入模板 + 输出契约 + 反射 + API）</summary>
        Task<SkillDetailDto> GetDetailAsync(string skillCode);

        /// <summary>主子表事务保存（新建/编辑）</summary>
        Task<(bool ok, string message)> SaveAsync(SkillDetailDto dto);

        /// <summary>物理删除（先删子表，避免 wf_skill_reflection 外键约束）</summary>
        Task<bool> DeleteAsync(long id);

        /// <summary>启停切换（is_active）</summary>
        Task<bool> ToggleActiveAsync(long id);

        /// <summary>启用 Skill 完整描述（供工作流配置面板引用）</summary>
        Task<List<SkillDetailDto>> GetActiveSkillsAsync();

        /// <summary>
        /// 获取功能节点目录（V1.3 新增）
        /// 返回启用的功能性 Skill 列表（含输入/输出端口声明），供画布设计器渲染节点面板。
        /// 特殊节点不在此返回，由前端硬编码。
        /// </summary>
        Task<List<object>> GetCatalogAsync();
    }
}
