using System;

namespace CertPlatform.Admin.Services.Workflow
{
    /// <summary>
    /// ★ <b>节点执行失败（带分类码）</b> —— 2026-10-12 新增。
    ///
    /// <para><b>为什么需要它</b>：修复前 <c>NodeExecutor.ExecuteSkillAsync</c> 里那句
    /// <c>if (!skillResult.Success) throw new InvalidOperationException(...)</c> <b>永不触发</b>
    /// （因为 <c>SkillExecutor</c> 恒返回 <c>SkillResult.Ok</c>），而且就算触发了，
    /// <c>NodeExecutor</c> 的 <c>catch (Exception)</c> 也只会写死
    /// <c>ErrorCode = SYSTEM_ERROR</c> —— <c>CONFIG_ERROR</c> / <c>PRECONDITION_EMPTY</c>
    /// 这类<b>能告诉用户该找谁</b>的信息在那一层被抹平了。</para>
    ///
    /// <para><b>为什么不用 <c>WorkflowDataMissingException</c> 一把梭</b>：那个异常带 8 个
    /// 结构化属性，语义是「<b>缺失必要数据</b>」⇒ 上层会落 <c>cert_expert_task_data_gap</c>
    /// 补录缺口、把任务项标成 <c>skipped</c>。但「配置错」不是补录能解决的
    /// （补了数据还是错的）⇒ 必须区分开，否则会给用户一个<b>永远补不上的待办</b>。</para>
    ///
    /// <para><b>引擎动作</b>：<c>NodeExecutor</c> 捕获后置
    /// <c>NodeExecutionResult.ErrorCode = ErrorCode</c> ⇒ 路径终止 ⇒
    /// <c>WorkflowInterpreter</c> 冒泡到 <c>ItemExecutionResult.ErrorCode</c> ⇒ UI 显示对应提示。</para>
    /// </summary>
    public class WorkflowNodeFailureException : Exception
    {
        /// <summary>失败分类码（见 <see cref="WorkflowErrorCodes"/>）；为空时 <c>NodeExecutor</c> 兜底为 <c>SYSTEM_ERROR</c></summary>
        public string? ErrorCode { get; }

        /// <summary>失败发生的节点 ID（用于把错误定位到画布上的具体节点）</summary>
        public string? NodeId { get; }

        public WorkflowNodeFailureException(string message, string? errorCode = null, string? nodeId = null)
            : base(message)
        {
            ErrorCode = errorCode;
            NodeId = nodeId;
        }
    }
}
