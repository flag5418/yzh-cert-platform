using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Entity.CertPlatform.Cert;
using VOL.Entity.DomainModels;

namespace VOL.Builder.IServices.CertPlatform
{
    public interface IISOClauseService : IDependency
    {
        /// <summary>
        /// 获取所有 ISO 标准列表（供标准条款页面选择）
        /// </summary>
        Task<List<object>> GetStandardsAsync();

        /// <summary>
        /// 获取指定标准的条款树形数据
        /// </summary>
        Task<List<ClauseTreeNode>> GetClauseTreeAsync(string standardCode);

        /// <summary>
        /// 获取条款列表（平铺，支持标准过滤）
        /// </summary>
        Task<List<ISOClause>> GetListAsync(string standardCode);

        /// <summary>
        /// 保存条款（新增/编辑）
        /// </summary>
        Task<bool> SaveAsync(ISOClause entity);

        /// <summary>
        /// 删除条款
        /// </summary>
        Task<bool> DeleteAsync(long id);
    }

    /// <summary>
    /// 条款树节点 DTO
    /// </summary>
    public class ClauseTreeNode
    {
        public long Id { get; set; }
        public string Code { get; set; }
        public string Label { get; set; }
        public string ClauseNumber { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int SortOrder { get; set; }
        public string ParentCode { get; set; }
        public List<ClauseTreeNode> Children { get; set; } = new List<ClauseTreeNode>();
    }
}
