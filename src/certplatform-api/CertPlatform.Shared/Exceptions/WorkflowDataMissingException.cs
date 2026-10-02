using System;

namespace CertPlatform.Shared.Exceptions
{
    /// <summary>
    /// ★ 工作流「缺失必要数据」异常（2026-09-30 用户裁决）。
    ///
    /// <para><b>为什么需要它</b>：工作流引用的字段/表格取不到<b>可用值</b>时，旧实现有两种坏行为：</para>
    /// <list type="bullet">
    /// <item><b>记录不存在</b> ⇒ 抛裸 <c>InvalidOperationException</c>，与「节点配置错」无法区分；</item>
    /// <item>★ <b>记录存在但值为空/空白</b> ⇒ 静默返回 <c>""</c> ⇒ <b>流程照常跑完，结论却是错的</b>
    ///       （用户原话：「虽然不影响流程的执行，但对结果有非常严重的影响」）。</item>
    /// </list>
    ///
    /// <para><b>语义</b>：这不是「系统故障」，也不是「不符合」，而是<b>无法判定</b>。
    /// 因此工作流必须<b>自动失败</b>并提示「缺失必要数据」，⛔ 不得产出一个看起来正常的结论。</para>
    ///
    /// <para><b>上层用法</b>：<c>catch (WorkflowDataMissingException ex)</c> ⇒ 按
    /// <see cref="DataKind"/> + <see cref="DataCode"/> + <see cref="RuleCode"/> 生成
    /// <c>cert_expert_task_data_gap</c> 补录记录。</para>
    ///
    /// <para><b>★ 2026-09-30 补 <see cref="RuleCode"/></b>（用户裁决 J4「1 文件 = 1 规则」）：
    /// 补录回写 <c>cert_extraction_result</c> 时以 <c>(OrgCode, RuleCode, FieldCode)</c> 定位，
    /// 没有 RuleCode 就不知道往哪一行写 ⇒ 必须随异常一起带出来。</para>
    /// </summary>
    public class WorkflowDataMissingException : Exception
    {
        /// <summary>数据类型：<c>field</c> | <c>table</c></summary>
        public string DataKind { get; }

        /// <summary>数据编码：FieldCode 或 TableCode（= 工作流节点配置里引用的那个键）</summary>
        public string DataCode { get; }

        /// <summary>缺失原因：<c>not_found</c>=无提取记录 | <c>is_empty</c>=有记录但值为空</summary>
        public string Reason { get; }

        /// <summary>该数据应来自哪个标准文件（有值时用于提示专家补哪个文件）</summary>
        public string? StandardFileCode { get; }

        /// <summary>★ 提取规则编码（<c>cert_doc_extraction_rule.Code</c>）
        /// <para>2026-09-30 新增。用户裁决 J4：1 文件 = 1 规则（DB 由
        /// <c>uk_rule_scope(OrgCode,StandardCode,StageCode,StandardFileCode)</c> 唯一索引强制）
        /// ⇒ <b>RuleCode 是取数与补录回写的主键成分</b>。</para>
        /// </summary>
        public string? RuleCode { get; }

        /// <summary>归属作用域（用于生成 gap 的冗余字段）</summary>
        public string? EnterpriseCode { get; }
        public string? StandardCode { get; }
        public string? StageCode { get; }

        public WorkflowDataMissingException(
            string dataKind, string dataCode, string reason, string message,
            string? standardFileCode = null,
            string? ruleCode = null,
            string? enterpriseCode = null, string? standardCode = null, string? stageCode = null)
            : base(message)
        {
            DataKind = dataKind;
            DataCode = dataCode;
            Reason = reason;
            StandardFileCode = standardFileCode;
            RuleCode = ruleCode;
            EnterpriseCode = enterpriseCode;
            StandardCode = standardCode;
            StageCode = stageCode;
        }
    }
}
