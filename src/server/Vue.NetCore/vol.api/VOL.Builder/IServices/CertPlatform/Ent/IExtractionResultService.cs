using System.Collections.Generic;
using System.Threading.Tasks;
using VOL.Core.Utilities;
using VOL.Core.Extensions.AutofacManager;

namespace VOL.Builder.IServices.CertPlatform
{
    /// <summary>
    /// 提取结果保存服务接口
    /// 职责：AI 提取结果写入 ent_extraction_result + ent_table_extraction_result
    /// 核心关联：standard_file_code → cert_file_requirement.code
    /// 冗余字段：org_code / standard_code / phase_code 方便过滤和工作流引用
    /// </summary>
    public interface IExtractionResultService : IDependency
    {
        /// <summary>
        /// 保存提取结果（字段级 + 表格级）
        /// </summary>
        /// <param name="fileCode">企业文件编码（ent_enterprise_file.code）</param>
        /// <param name="enterpriseCode">企业编码</param>
        /// <param name="standardFileCode">标准文件编码（核心枢纽，关联 cert_file_requirement.code）</param>
        /// <param name="ruleCode">规则编码</param>
        /// <param name="orgCode">机构编码（冗余）</param>
        /// <param name="standardCode">标准编码（冗余）</param>
        /// <param name="phaseCode">阶段编码（冗余）</param>
        /// <param name="fields">字段提取结果（key=fieldCode, value=提取值）</param>
        /// <param name="tables">表格提取结果（key=tableCode, value=行数据列表）</param>
        Task<WebResponseContent> SaveExtractionResultAsync(
            string fileCode, string enterpriseCode, string standardFileCode, string ruleCode,
            string orgCode, string standardCode, string phaseCode,
            Dictionary<string, object> fields,
            Dictionary<string, List<Dictionary<string, object>>> tables);

        /// <summary>
        /// 按文件编码获取提取结果
        /// </summary>
        Task<(List<object> fields, List<object> tables)> GetByFileCodeAsync(string fileCode);
    }
}
