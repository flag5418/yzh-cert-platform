using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Core.Utilities;
using VOL.Entity.CertPlatform.Ent;
using VOL.Core.Extensions.AutofacManager;

namespace VOL.Builder.IServices.CertPlatform
{
    /// <summary>
    /// 企业管理服务接口
    /// 职责：企业 CRUD、企业编码生成、企业阶段初始化
    /// </summary>
    public interface IEnterpriseService : IDependency
    {
        /// <summary>
        /// 获取企业列表（分页）
        /// </summary>
        Task<(List<Enterprise> items, int total)> GetListAsync(string orgCode, int page, int rows);

        /// <summary>
        /// 获取企业详情
        /// </summary>
        Task<Enterprise> GetDetailAsync(string code);

        /// <summary>
        /// 创建企业（自动生成 EnterpriseNo，初始化企业阶段）
        /// </summary>
        Task<WebResponseContent> CreateAsync(Enterprise entity);

        /// <summary>
        /// 更新企业信息
        /// </summary>
        Task<WebResponseContent> UpdateAsync(Enterprise entity);

        /// <summary>
        /// 删除企业（软删除）
        /// </summary>
        Task<WebResponseContent> DeleteAsync(string code);

        /// <summary>
        /// 生成企业短编码（ENT-2026-0001 格式）
        /// </summary>
        Task<string> GenerateEnterpriseNoAsync();
    }
}
