/*
 * 机构-标准 / 机构-阶段 关联管理 Service 接口
 * 
 * 职责：
 *   1. SyncOrgStandards — 批量同步机构-标准关联（勾选即保存）
 *   2. GetOrgStdIds — 查询某机构已关联的标准 ID 列表
 *   3. SyncOrgStages — 批量同步机构-阶段关联（勾选即保存）
 *   4. GetOrgStageIds — 查询某机构已关联的阶段 ID 列表
 */
using VOL.Core.BaseProvider;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.Utilities;

namespace VOL.Builder.IServices.CertPlatform
{
    public interface IOrgLinkService : IDependency
    {
        /// <summary>
        /// 同步机构-标准关联
        /// </summary>
        /// <param name="cbCode">机构编码</param>
        /// <param name="addIds">新增的标准 ID 列表</param>
        /// <param name="removeIds">移除的标准 ID 列表</param>
        /// <returns>同步结果</returns>
        WebResponseContent SyncOrgStandards(string cbCode, long[] addIds, long[] removeIds);

        /// <summary>
        /// 获取机构已关联的标准 ID 列表
        /// </summary>
        /// <param name="cbCode">机构编码</param>
        /// <returns>标准 ID 列表</returns>
        object GetOrgStdIds(string cbCode);

        /// <summary>
        /// 同步机构-阶段关联
        /// </summary>
        /// <param name="cbCode">机构编码</param>
        /// <param name="addIds">新增的阶段 ID 列表</param>
        /// <param name="removeIds">移除的阶段 ID 列表</param>
        /// <returns>同步结果</returns>
        WebResponseContent SyncOrgStages(string cbCode, long[] addIds, long[] removeIds);

        /// <summary>
        /// 获取机构已关联的阶段 ID 列表
        /// </summary>
        /// <param name="cbCode">机构编码</param>
        /// <returns>阶段 ID 列表</returns>
        object GetOrgStageIds(string cbCode);
    }
}
