using CertPlatform.Shared.Services.Fill;
using Microsoft.Extensions.DependencyInjection;

namespace CertPlatform.Shared;

/// <summary>
/// <c>CertPlatform.Shared</c> 的业务服务自注册入口。
///
/// <para>★ <b>为什么要有这个文件</b>（2026-10-06 用户裁定）：<c>Shared</c> 不再只是「只放实体的纯域层」，
/// 而是「<b>三端共用的完整业务层</b>」—— 它可以引 <c>YZH.Core.DataBase</c>、可以放查库服务、
/// 可以放 <c>Assets/EntityConfigs</c>（<b>只有 Controller 不放</b>）。</para>
///
/// <para>★ <b>为什么必须由宿主显式调用</b>：<c>YZH.Core.Web</c> 是启动工程，它<b>不感知</b>具体业务实现。
/// 各端通过「自注册扩展方法」把服务挂进容器，宿主只按顺序调用。
/// ⇒ 漏调用 = 该端服务<b>运行期解析失败</b>（DI 报错）或<b>静默走错实现</b>。</para>
///
/// <para>⚠️⚠️ <b>调用顺序敏感</b>：<c>Program.cs</c> 里本方法必须排在
/// <c>AddCertPlatformAdminServices()</c> / <c>AddCertPlatformAuditorServices()</c> <b>之前</b>。
/// 原因：<c>AddScoped</c> 是「<b>后注册覆盖先注册</b>」的语义 —— 同一服务被注册两次时，
/// <b>最后一次生效</b>。若 Admin 也注册了同名服务，Admin 的会赢，Shared 这份等于没注册
/// ⇒ 又是「配置了不生效」的静默分叉。
/// ⇒ <b>下沉服务后必须同步删掉端内的重复注册</b>（见 <c>58</c> §4.2）。</para>
///
/// <para>★ <b>生命周期判据</b>：<c>IDbOrm</c> / <c>IObjectStorage</c> 均为 <b>Scoped</b>
/// ⇒ 任何持它们的服务必须 <c>AddScoped</c>；无状态纯逻辑才可 <c>AddSingleton</c>。</para>
///
/// <para>⛔ <b>硬纪律</b>：本文件只注册 <c>Shared/Services/**</c> 下的服务。
/// <c>Shared/Fill/**</c> 与 <c>Shared/Office/**</c> 是<b>纯函数资产</b>（可单测、可离线跑），
/// 一律不注册、不查库（守卫 G-4）。</para>
/// </summary>
public static class CertPlatformSharedServiceExtensions
{
    /// <summary>
    /// 注册 <c>Shared</c> 层服务。<b>必须在 Admin / Auditor 之前调用</b>。
    /// </summary>
    public static IServiceCollection AddCertPlatformSharedServices(this IServiceCollection services)
    {
        // ────────────────────────────────────────────────────────────────
        // Shared/Services/Fill/ —— 取值内核（「搬『值』不搬『壳』」）
        // ────────────────────────────────────────────────────────────────
        // ★ S1-5 已落位（2026-10-06）：FillParamValueProvider
        //   = 「查 cert_fill_param_def（OR 组合 + PickMostSpecific）→ ParamValueResolver.Resolve
        //      → FillValueFactory.TryCreate」的**唯一实现**（BuildValue 为纯函数内核）。
        //   它同时消灭了：
        //     · D1 —— 原先「取全局参数值」有三条互不相通的实现；其中
        //             SourceResolver 那条是手抄副本（查询与组值各抄一遍）。
        //             现在 SourceResolver 与 SrcGlobalParamSkill 都走本类。
        //     · D6 —— EnterpriseInfoMapper（本目录）成为「企业实体 → 引擎快照」的唯一映射
        //             （原先 Admin 内手写 2 份）。
        //   ★ 生命周期：持 IDbOrm（Scoped）⇒ 必须 AddScoped。
        //   ⚠️ 本类**不查** cert_fill_param_value（企业已填值）：其实体 FillParamValue 属专家端独占
        //      （24 号 §一，守卫 R17），且 Shared ⛔ 不能引 Auditor ⇒ 由调用方预取后传入。
        services.AddScoped<FillParamValueProvider>();

        // ────────────────────────────────────────────────────────────────
        // Shared/Services/Ai/ —— AI 执行内核
        // ────────────────────────────────────────────────────────────────
        // ★ 待 S1-6 落位：AiFillInvoker（Scoped，构造注入 IDbOrm）/ AiFillPromptBuilder（Singleton，无状态）
        // ⚠️ 同时把 Admin 侧对 LlmInvokeService 的注册**移到这里** ——
        //    否则 Admin 不注册时（例如将来 Auditor 单独跑），Auditor 侧解析失败。
        // services.AddScoped<AiFillInvoker>();
        // services.AddSingleton<AiFillPromptBuilder>();
        // services.AddSingleton<LlmInvokeService>();

        // ────────────────────────────────────────────────────────────────
        // Shared/Services/Semantic/ —— 语义分析内核
        // ────────────────────────────────────────────────────────────────
        // ★ 待 S1-6 之后（`58` §3.2 / 待裁 C1）：把 PromptWorkbenchService 的**分析内核**拆下来。
        //   拆点 = 「有没有 HttpContext / 是不是给页面用的」：
        //     · AnalyzeForQueueAsync（队列口径）→ 下沉
        //     · GenerateAsync / TestAsync（工作台交互）→ ⛔ 留 Admin
        //   ⛔ 不要整体搬（会把后台专用变共用），也不要整体留（双端各抄一份口径 = D1 模式重演）。

        return services;
    }
}
