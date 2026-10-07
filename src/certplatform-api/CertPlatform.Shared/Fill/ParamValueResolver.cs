using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Fill.Resolvers;

namespace CertPlatform.Shared.Fill;

/// <summary>
/// 参数取值决策 —— <b>★ 全项目唯一实现</b>。
///
/// <para><b>为什么必须唯一</b>：企业端「合并清单」（<c>FillParamValueController.MergeList</c>）
/// 与「填充预览」（<c>DocumentFillController.Preview</c>）都要回答同一个问题
/// ——「这个参数当前的值是什么、从哪来的、企业能不能改」。
/// 各写一份的后果：企业端显示「企业地址 = 某某路 1 号」，而文档里填出来是空的
/// （或反之）。<b>两边都不报错</b>，只是用户看到的和文档里的不一致 —— 这是最难查的一类缺陷。</para>
///
/// <para><b>★ 取值语义（<see cref="FillParamDef.MaintainMode"/> 决定一切）</b>：</para>
/// <list type="table">
///   <listheader><term>MaintainMode</term><description>行为</description></listheader>
///   <item><term><c>auto</c> 自动映射</term>
///         <description><b>恒实时</b>取企业档案，<b>忽略</b>参数值表（<c>cert_fill_param_value</c>）。
///         企业端只读。理由：<c>auto</c> 的语义是「永远等于企业档案」，
///         若允许存快照，企业改了档案而值表未刷新 → 文档印着旧值且无人察觉。</description></item>
///   <item><term><c>both</c> 可覆盖</term>
///         <description>有企业填写值 → 用企业值；<b>没有 → 用企业档案自动带出作为默认值</b>。
///         企业端可改。这是「先自动关联形成列表，让企业完善」的主场景。</description></item>
///   <item><term><c>manual</c> 手工</term>
///         <description>有企业填写值 → 用企业值；没有 → 用 <c>DefaultValue</c>；仍没有 → 空（待办）。</description></item>
/// </list>
///
/// <para>⚠️ <c>both</c> 的「自动带出」是<b>初值</b>而非实时值：企业一旦改动即落库，
/// 此后不再跟随企业档案变化（这正是「允许覆盖」的含义）。</para>
/// </summary>
public static class ParamValueResolver
{
    /// <summary>维护方式：自动映射（只读，实时取企业档案）</summary>
    public const string ModeAuto = "auto";

    /// <summary>维护方式：手工填写</summary>
    public const string ModeManual = "manual";

    /// <summary>维护方式：自动带出但允许覆盖</summary>
    public const string ModeBoth = "both";

    /// <summary>值来源：自动映射</summary>
    public const string SourceAuto = "auto";

    /// <summary>值来源：企业人工填写</summary>
    public const string SourceManual = "manual";

    // ⛔ 这里刻意**没有** `SourceAi`：2026-10-07 用户裁决「企业资料参数」页由人手填，
    //    `cert_fill_param_def.SourceKind` / `cert_fill_param_value.ValueSource` 的
    //    `ai` 取值已由迁移 20261007_fill_param_drop_ai_V1.sql 归一为 manual，
    //    专家端「生成提示词」按钮与 `ai-prompt` 端点一并删除。

    /// <summary>值来源：取自定义的默认值</summary>
    public const string SourceDefault = "default";

    /// <summary>值来源：无值（待办）</summary>
    public const string SourceEmpty = "empty";

    /// <summary>一次取值决策的结果</summary>
    /// <param name="Value">当前值（null 或空串 = 待完善）</param>
    /// <param name="ValueSource">auto | manual | default | empty</param>
    /// <param name="SourceRef">给用户看的值来源说明</param>
    /// <param name="Editable">企业端能否编辑（false 时前端应渲染为只读 + 「去企业管理修改」链接）</param>
    /// <param name="IsManualEdited">企业是否人工改过（决定后续自动带出是否还覆盖）</param>
    public sealed record Decision(
        string? Value,
        string ValueSource,
        string SourceRef,
        bool Editable,
        bool IsManualEdited);

    /// <summary>
    /// 决策单个参数的当前值。
    /// </summary>
    /// <param name="def">参数定义</param>
    /// <param name="enterprise">企业档案快照（<c>auto</c> / <c>both</c> 的自动带出来源）</param>
    /// <param name="savedValue">参数值表中已落库的值（无行则传 null）</param>
    /// <param name="savedValueSource">已落库行的值来源（无行则传 null）</param>
    /// <param name="savedIsManualEdited">已落库行是否被人工改过</param>
    public static Decision Resolve(
        FillParamDef def,
        EnterpriseInfo enterprise,
        string? savedValue = null,
        string? savedValueSource = null,
        bool savedIsManualEdited = false)
    {
        var mode = string.IsNullOrWhiteSpace(def.MaintainMode) ? ModeAuto : def.MaintainMode;
        var hasSaved = !string.IsNullOrWhiteSpace(savedValue);
        var (autoValue, autoRef) = ResolveExpr(def.SourceExpr, enterprise);

        switch (mode)
        {
            case ModeAuto:
            {
                // 恒实时；企业端只读
                if (!string.IsNullOrWhiteSpace(autoValue))
                    return new Decision(autoValue, SourceAuto, autoRef, Editable: false, IsManualEdited: false);

                // ★ 区分两种「空」：
                //   ① 表达式没问题、只是企业档案没填 → 引导去「企业管理」补齐（用户能自己解决）
                //   ② 表达式本身有问题（没配 / 不是 enterprise.* / 属性名写错）→ 是后台配置问题，
                //      引导用户去「企业管理」会让他在那儿白找一通。必须把真实原因原样抛出。
                var isConfigOk = autoRef.StartsWith("企业基础信息", StringComparison.Ordinal);
                var reason = isConfigOk
                    ? $"待补齐（{autoRef}）—— 请到「企业管理」完善"
                    : $"配置异常：{autoRef}";

                return new Decision(null, SourceEmpty, reason, Editable: false, IsManualEdited: false);
            }

            case ModeBoth:
            {
                if (hasSaved)
                    return new Decision(savedValue, savedValueSource ?? SourceManual,
                        $"企业填写 · {def.ParamName}", Editable: true, IsManualEdited: savedIsManualEdited);

                // ★ 没有企业填写值 → 用企业档案自动带出作为初值（这正是「先自动关联」的落点）
                if (!string.IsNullOrWhiteSpace(autoValue))
                    return new Decision(autoValue, SourceAuto,
                        $"{autoRef}（自动带出，可覆盖）", Editable: true, IsManualEdited: false);

                return new Decision(Fallback(def), SourceDefault,
                    $"待完善 · {def.ParamName}", Editable: true, IsManualEdited: false);
            }

            default: // manual
            {
                if (hasSaved)
                    return new Decision(savedValue, savedValueSource ?? SourceManual,
                        $"企业填写 · {def.ParamName}", Editable: true, IsManualEdited: savedIsManualEdited);

                var fallback = Fallback(def);
                return string.IsNullOrWhiteSpace(fallback)
                    ? new Decision(null, SourceEmpty, $"待完善 · {def.ParamName}", Editable: true, IsManualEdited: false)
                    : new Decision(fallback, SourceDefault, $"默认值 · {def.ParamName}", Editable: true, IsManualEdited: false);
            }
        }
    }

    private static string? Fallback(FillParamDef def) => def.DefaultValue;

    /// <summary>
    /// 解析自动取值表达式（当前仅支持 <c>enterprise.*</c>）。
    /// <para>★ 与 <see cref="ReplaceResolver"/> 共用 <see cref="EnterpriseInfo.Get"/> 与
    /// <see cref="ReplaceResolver.EnterpriseAttrLabels"/> —— 保证「企业端显示的值」
    /// 与「文档里填的值」永远一致。</para>
    /// </summary>
    public static (string? Value, string SourceRef) ResolveExpr(string? sourceExpr, EnterpriseInfo enterprise)
    {
        var expr = sourceExpr?.Trim();
        if (string.IsNullOrWhiteSpace(expr))
            return (null, "未配置取值表达式");

        const string ns = "enterprise.";
        if (!expr.StartsWith(ns, StringComparison.OrdinalIgnoreCase))
            return (null, $"暂不支持的表达式「{expr}」（当前仅支持 enterprise.*）");

        var attr = expr[ns.Length..].Trim();
        if (!ReplaceResolver.EnterpriseAttrLabels.TryGetValue(attr, out var label))
            return (null, $"未知的企业属性「{attr}」");

        return (enterprise.Get(attr), $"企业基础信息 · {label}");
    }

    /// <summary>定义的「具体度」：指定标准 + 指定阶段 &gt; 只指定其一 &gt; 全通配</summary>
    public static int Specificity(FillParamDef d)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(d.StandardCode)) score += 2;
        if (!string.IsNullOrWhiteSpace(d.StageCode)) score += 1;
        return score;
    }

    /// <summary>
    /// 定义去重：同一 <c>ParamCode</c> 取「更具体」的那条，平手取先创建者。
    /// <para>这是 <c>23</c> §四「标准 × 阶段裁剪」的执行点 —— 允许机构配一条「不限标准」的
    /// 通用参数，再为某个标准覆写一条同名参数。</para>
    /// </summary>
    public static List<FillParamDef> PickMostSpecific(IEnumerable<FillParamDef> defs) =>
        defs.GroupBy(d => d.ParamCode)
            .Select(g => g.OrderByDescending(Specificity).ThenBy(d => d.Id).First())
            .OrderBy(d => d.SortOrder).ThenBy(d => d.ParamCode)
            .ToList();

    /// <summary>值表定位键（镜像定义的 <c>StandardCode</c> / <c>StageCode</c>）</summary>
    public static string ValueKey(string? standardCode, string? stageCode, string paramCode) =>
        $"{standardCode ?? string.Empty}\u0001{stageCode ?? string.Empty}\u0001{paramCode}";
}
