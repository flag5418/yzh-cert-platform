using System;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// Skill 类级特性（必填）。声明 Skill 的编码、名称、返回类型、说明。
    /// <para>移植自：旧 YZH.Core.Workflow.SkillAttributes.cs（结构零改动，仅 namespace 调整）</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class SkillAttribute : Attribute
    {
        /// <summary>Skill 编码，如 "compare"</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>中文名，如 "值比较"</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>result 的类型：string / number / date / boolean / json</summary>
        public string ReturnType { get; set; } = "json";

        /// <summary>作用说明</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// ★ <b>主输出端口名</b>（2026-10-12 新增）—— 决定 <c>SkillExecutor</c> 把
        /// <c>Outputs</c> 里的哪个键提到统一信封的 <c>result</c> 上。
        ///
        /// <para><b>为什么必须有</b>：连线只把「源节点 ID」写进下游输入（源端口名被丢弃），
        /// 而所有 skill 的输出都是多键字典 ⇒ 下游必须能可靠地拿到「这一个最该被连线的值」。
        /// 没有它时 <c>result</c> 只能塞整包字典 ⇒ <c>is_empty</c> 恒判「非空」（实测缺陷）。</para>
        ///
        /// <para><b>不声明时的推断顺序</b>（<c>SkillExecutor.ResolveMainOutput</c>）：
        /// ① <c>ResultPort</c> 指定的键 → ② 业务输出里的 <c>result</c> 键
        /// → ③ 只有一个键时取该键 → ④ 兜底 = 整包字典（并打 Warning）。</para>
        ///
        /// <para>示例：<c>assemble → assembled_text</c>；<c>get_field → field_value</c>；
        /// <c>get_table → rows</c>；<c>compare → result</c>；<c>is_empty → result</c>。</para>
        /// </summary>
        public string? ResultPort { get; set; }
    }

    /// <summary>
    /// Skill 参数级特性（可选）。用于补充参数的中文描述、绑定模式、字典来源。
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public class SkillParamAttribute : Attribute
    {
        /// <summary>参数中文描述</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 绑定模式（默认 LinkOrConstant）
        /// </summary>
        public SkillParamBindMode BindMode { get; set; } = SkillParamBindMode.LinkOrConstant;

        /// <summary>
        /// 字典编码（BindMode=Enum 时必填）。
        /// 前端据此加载选项列表（旧 Sys_Dictionary.DicNo 对应物）。
        /// </summary>
        public string? EnumSource { get; set; }

        /// <summary>
        /// ★ <b>该参数是否允许为空</b>（null / 空串 / 纯空白）—— 三态（2026-10-12 新增）。
        ///
        /// <para><b>⚠️ 为什么是枚举而不是 <c>bool?</c></b>：最初写的是 <c>bool?</c>（用 null 表示
        /// 「未表态」），但 <b>C# 规范禁止特性具名实参使用可空值类型</b>（<c>Nullable&lt;T&gt;</c> 不在
        /// 合法特性参数类型清单内）⇒ 全部 59 处 <c>AllowEmpty = true/false</c> 编译报
        /// <c>CS0655</c>。改用三值枚举后<b>语义完全不变</b>，且比 <c>object?</c> 更安全：
        /// 若用 <c>object?</c>，误写 <c>AllowEmpty = "true"</c> 会被静默忽略、回落到
        /// <c>HasDefaultValue</c> —— 正是本项目反复踩的「配了不生效且不报错」陷阱；
        /// 枚举则由编译器直接拦下。</para>
        ///
        /// <para><b>为什么必须保留「未表态」这一态</b>：全项目 17 个 skill 共 60+ 个参数，
        /// 一次性把所有参数都改成「必须非空」会<b>大面积误伤</b>
        /// （如 <c>nc_validate</c> 的 <c>reason</c> 只在 conclusion 阶段用，
        /// dimensions 阶段本就该为空）⇒ 需要「未表态 = 沿用旧行为」这个中间态。</para>
        ///
        /// <para><b>三态语义</b>：</para>
        /// <list type="table">
        /// <item><term><see cref="SkillParamEmptyPolicy.Unspecified"/>（未声明，默认）</term>
        ///       <description>沿用旧判据 <c>!ParameterInfo.HasDefaultValue</c>
        ///       —— 即「没有 C# 默认值 = 必填」。<b>零行为变化</b>，不引入回归。</description></item>
        /// <item><term><see cref="SkillParamEmptyPolicy.Allow"/></term>
        ///       <description>明确允许为空 —— 即使该参数没有 C# 默认值，也不算缺参。
        ///       典型：<c>get_field.enterprise_code</c>（描述写着「空 = 虚拟企业常量」）。</description></item>
        /// <item><term><see cref="SkillParamEmptyPolicy.Deny"/></term>
        ///       <description>明确不允许为空 —— 即使该参数有 C# 默认值（<c>= null</c>），
        ///       空值也算缺参。<b>这是「值判断」安全网的开关</b>：上游没给值时该节点
        ///       <b>明确失败</b>而不是拿着空值静默算出一个错结论。</description></item>
        /// </list>
        ///
        /// <para><b>⛔ 规则作者在画布上看不到这个开关</b> —— 它是 <b>skill 开发者</b>在代码里
        /// 声明一次的（用户 2026-10-12 裁定：「控制空值由节点自身行为判断，人工干预容易出问题」）。
        /// 界面上只能通过 <c>Required</c>（REQ 标签）间接看到结果。</para>
        ///
        /// <para><b>失败后果</b>：引擎在 <c>SkillExecutor</c> 里统一校验，
        /// 命中即返回 <c>ErrorCode = PRECONDITION_EMPTY</c> ⇒
        /// <c>NodeExecutor</c> 抛 <c>WorkflowDataMissingException</c> ⇒
        /// <b>全局中断当前检查项/章节</b> + 落补录缺口 + 标「不可判定」。</para>
        /// </summary>
        public SkillParamEmptyPolicy AllowEmpty { get; set; } = SkillParamEmptyPolicy.Unspecified;
    }

    /// <summary>
    /// ★ <b>参数空值策略（三态）</b>（2026-10-12 新增）。
    ///
    /// <para>只作 <see cref="SkillParamAttribute.AllowEmpty"/> 的类型载体存在 ——
    /// 必须用<b>非可空枚举</b>，因为特性具名实参不接受可空值类型
    /// （<c>bool?</c> 会报 <c>CS0655</c>）。</para>
    ///
    /// <para>判读唯一入口 = <c>SkillExecutor.AllowsEmpty</c>（执行期与元数据端共用同一口径）。</para>
    /// </summary>
    public enum SkillParamEmptyPolicy
    {
        /// <summary>未表态（默认）⇒ 沿用旧判据 <c>ParameterInfo.HasDefaultValue</c>，零行为变化</summary>
        Unspecified = 0,

        /// <summary>明确允许为空 ⇒ 空值不算缺参，节点照常执行</summary>
        Allow = 1,

        /// <summary>明确不允许为空 ⇒ 空值即缺参，节点以 <c>PRECONDITION_EMPTY</c> 失败</summary>
        Deny = 2
    }

    /// <summary>
    /// Skill 参数绑定模式枚举。
    /// </summary>
    public enum SkillParamBindMode
    {
        /// <summary>仅连线：参数值必须来自上游节点输出</summary>
        Link = 0,

        /// <summary>连线或常量：画布上可切换为手动输入（默认）</summary>
        LinkOrConstant = 1,

        /// <summary>字典选择：从字典服务按 EnumSource 加载选项</summary>
        Enum = 2,

        /// <summary>仅常量：静态配置值，直接输入文字；不下发连线选择器（2026-10-10 新增）</summary>
        ConstantOnly = 3
    }

    /// <summary>
    /// 标记依赖注入参数。反射时跳过此参数，运行时从 DI 容器获取。
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public class FromServiceAttribute : Attribute { }
}
