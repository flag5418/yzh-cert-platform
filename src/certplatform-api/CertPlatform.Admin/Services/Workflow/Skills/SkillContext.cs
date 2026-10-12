using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// Skill 执行上下文与结果
    /// <para>移植自：旧 YZH.Core.Workflow.SkillContext.cs（结构零改动）</para>
    /// </summary>
    public class SkillContext
    {
        public IDictionary<string, object> Inputs { get; set; } = new Dictionary<string, object>();
        public string WorkflowInstanceId { get; set; } = string.Empty;
        public string NodeId { get; set; } = string.Empty;
        public ILogger? Logger { get; set; }
    }

    /// <summary>
    /// Skill 执行结果（标准输出 + 元数据）
    /// </summary>
    public class SkillResult
    {
        public bool Success { get; set; }
        public IDictionary<string, object> Outputs { get; set; } = new Dictionary<string, object>();
        public double? Confidence { get; set; }
        public string? Error { get; set; }

        /// <summary>
        /// ★ <b>失败分类码</b>（2026-10-12 新增；成功时为 null）。
        ///
        /// <para>取值见 <see cref="CertPlatform.Admin.Services.Workflow.Models.WorkflowErrorCodes"/>。
        /// 这是「统一判断标准」的第二个字段 —— 引擎判断后续动作<b>只看
        /// <c>Success</c> + <c>ErrorCode</c></b>，⛔ 不为任何单个 skill 写特殊逻辑：</para>
        /// <list type="table">
        /// <item><term><c>PRECONDITION_EMPTY</c></term>
        ///       <description>入参为空且该参数声明了 <c>AllowEmpty = SkillParamEmptyPolicy.Deny</c>
        ///       ⇒ <b>全局中断当前检查项</b> + 落补录缺口。</description></item>
        /// <item><term><c>CONFIG_ERROR</c></term>
        ///       <description>配置错（规则 / 字段 / 表格 Code 不存在、找不到类型或方法）
        ///       ⇒ 终止当前<b>路径</b>。</description></item>
        /// <item><term><c>SYSTEM_ERROR</c></term>
        ///       <description>系统异常（反射失败 / IO / 未预期）
        ///       ⇒ 终止当前<b>路径</b>。</description></item>
        /// </list>
        ///
        /// <para><b>⚠️ 为什么必须区分</b>：三者界面上都是「失败」，但处置人不同 ——
        /// <c>PRECONDITION_EMPTY</c> 找企业补资料、<c>CONFIG_ERROR</c> 找规则作者改配置、
        /// <c>SYSTEM_ERROR</c> 报开发。只有一句 <c>Error</c> 文本时三者长得一样（补录夹具失去信号）。</para>
        /// </summary>
        public string? ErrorCode { get; set; }

        public long DurationMs { get; set; }
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }

        public static SkillResult Ok(IDictionary<string, object>? outputs = null, double? confidence = null)
            => new() { Success = true, Outputs = outputs ?? new Dictionary<string, object>(), Confidence = confidence };

        /// <summary>
        /// 构造失败结果。
        /// <para>⚠️ 2026-10-12 起<b>必须尽量传 <paramref name="errorCode"/></b> ——
        /// 不传时 <c>SkillExecutor</c> 会兜底归为 <c>CONFIG_ERROR</c>（多数 skill 的
        /// <c>Fail</c> 是「入参/前置不满足业务规则」，规则作者可修）。</para>
        /// </summary>
        public static SkillResult Fail(string error, string? errorCode = null)
            => new() { Success = false, Error = error, ErrorCode = errorCode };
    }

    /// <summary>Skill 元数据（反射分析结果，用于验证/画布端口推断）</summary>
    public class SkillMetadata
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ReturnType { get; set; } = "json";
        public string Description { get; set; } = string.Empty;
        public string ClassPath { get; set; } = string.Empty;
        public string MethodName { get; set; } = "ExecuteAsync";

        /// <summary>★ 主输出端口名（<c>[Skill(ResultPort=…)]</c>，2026-10-12 新增）</summary>
        public string? ResultPort { get; set; }

        public List<SkillPortInfo> InputPorts { get; set; } = new();
    }

    /// <summary>Skill 输入端口信息</summary>
    public class SkillPortInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "json";
        public bool Required { get; set; }
        public string? DefaultValue { get; set; }
        public string Description { get; set; } = string.Empty;
        /// <summary>绑定模式：Link / LinkOrConstant / Enum / ConstantOnly</summary>
        public string BindMode { get; set; } = "LinkOrConstant";
        /// <summary>字典编码（BindMode=Enum 时有值）</summary>
        public string? EnumSource { get; set; }

        /// <summary>
        /// ★ 该参数是否允许为空（2026-10-12 新增）。
        /// <para><c>false</c> ⇒ 执行期该参数为空会 <b>中断当前检查项</b>
        /// （<c>PRECONDITION_EMPTY</c> + 落补录缺口）。</para>
        /// <para>与 <see cref="Required"/> 的关系：<c>Required == !AllowEmpty</c>，两者同源
        /// （唯一判据 = <c>SkillExecutor.IsRequired</c>），界面上 <c>Required</c> 渲染成 REQ 标签。</para>
        /// </summary>
        public bool AllowEmpty { get; set; } = true;
    }
}
