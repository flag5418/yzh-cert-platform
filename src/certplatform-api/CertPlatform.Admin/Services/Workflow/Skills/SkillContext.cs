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

        /// <summary>
        /// ★ <b>运行上下文</b>（2026-10-15 新增）—— 引擎开跑前装配的<b>业务上下文</b>。
        ///
        /// <para><b>键名</b>见 <c>WorkflowContextKeys</c>（唯一出处，⛔ 禁止裸写字面量；值一律 <b>PascalCase</b>）：
        /// <c>OrgCode</c> / <c>StandardCode</c> / <c>PhaseCode</c> / <c>EnterpriseCode</c> /
        /// <c>RuleCode</c>，以及<b>运行级登记池</b>（见 <c>WorkflowContextKeys.JudgePool</c>）。</para>
        ///
        /// <para><b>取用方式</b>：skill 参数标 <c>[FromContext("OrgCode")]</c> ⇒
        /// 该参数<b>不出现在画布上</b>（<c>SkillExecutor.Analyze</c> 跳过它），
        /// 值由引擎在此注入 ⇒ 规则作者<b>不再手填 GUID</b>。</para>
        ///
        /// <para>⚠️ <b>只读语义</b>：skill ⛔ 不得修改本字典（唯一例外 = 登记池的追加，
        /// 由 <c>SkillExecutor</c> 按 <c>[Skill(RegistersTo)]</c> 统一执行）。</para>
        /// </summary>
        public IDictionary<string, object> ContextParams { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// ★ <b>本节点已连接的输入端口名</b>（2026-10-15 新增）—— 来自 <c>node.Inputs</c> 的键集合。
        ///
        /// <para><b>为什么必须单独传</b>：运行期「<b>端口没接</b>」与「<b>接了但上游返回空</b>」
        /// 在 <c>Inputs</c> 取值上<b>都是 <c>null</c></b>，⛔ 无法区分。
        /// 而 <c>stamp_judge_level</c> 的语义恰恰要求区分二者：</para>
        /// <list type="bullet">
        /// <item>两个端口<b>都没接</b> ⇒ <b>配置错</b>（<c>CONFIG_ERROR</c>）</item>
        /// <item>接了「证据」但<b>材料没拿到</b> ⇒ <b>不可判定</b>（<c>unverifiable</c>，合法）</item>
        /// </list>
        ///
        /// <para>⇒ 判据 = <b>端口名是否出现在本集合里</b>（<c>NodeExecutor.ResolveInputs</c>
        /// 只为已声明的端口写 <c>resolved[portName]</c> ⇒ <b>未连的端口键不存在</b>）。</para>
        ///
        /// <para>⚠️ <b>skill 侧取法</b>：不直接读本字段，而是用
        /// <c>[FromContext(WorkflowContextKeys.ConnectedPorts)]</c> —— 由 <c>NodeExecutor</c>
        /// 把它以同名键放进 <see cref="ContextParams"/>（唯一绑定通道）。</para>
        /// </summary>
        public ISet<string> ConnectedPorts { get; set; } = new HashSet<string>();
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
        /// <para>取值见 <see cref="CertPlatform.Shared.Workflow.Models.WorkflowErrorCodes"/>。
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
