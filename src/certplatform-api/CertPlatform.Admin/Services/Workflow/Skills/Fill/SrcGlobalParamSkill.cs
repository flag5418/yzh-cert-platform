using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Office;
using CertPlatform.Shared.Services.Fill;
using SharedFill = CertPlatform.Shared.Fill;

namespace CertPlatform.Admin.Services.Workflow.Skills.Fill
{
    /// <summary>
    /// 全局参数取值 —— 按 <c>param_code</c>（+ 标准 / 阶段）取**最特异**的一条定义，
    /// 经「全项目唯一」的取值决策得到值，产出 <see cref="FillValue"/>。
    ///
    /// <para><b>★★ 2026-10-06（S1-5）改造后的定位</b>：
    /// 「查定义 + 取企业已填值」已下沉到
    /// <see cref="FillParamValueProvider"/>（<c>Shared/Services/Fill/</c>），
    /// 本 Skill 改为调它；本类只保留
    /// ① <b>Skill 壳</b>（参数声明 / <c>SkillResult</c> 包装）
    /// ② <b>纯函数 <see cref="BuildValue"/></b>（可单测，取值语义仍只由
    /// <c>ParamValueResolver.Resolve</c> 决定，⛔ 本类不重写 auto/manual/both 判定）。</para>
    ///
    /// <para><b>★ 为什么这样拆</b>：同一个「取全局参数值」在项目里曾有三条互不相通的实现
    /// （缺陷 D1）—— <c>Shared/Fill/ParamValueResolver</c>（决策，唯一正确）、
    /// 本 Skill（查询 + 决策 + 组值）、
    /// <c>Ent/SourceResolver.TryResolveGlobalAsync</c>（手抄本 Skill 的查询，且恒不传企业已填值 ⇒ 静默漂移）。
    /// 下沉到 provider 后两条链共用同一个类 ⇒ <b>口径必然一致</b>。</para>
    ///
    /// <para><b>★ 关于企业已填值（<c>saved_value</c>）</b>：
    /// 它<b>只能由调用方预取后传入</b> —— 该表的实体 <c>FillParamValue</c> 属<b>专家端独占</b>
    /// （<c>24-后端实体归属清单</c> §一；守卫 R17 会把「上移到 Shared」拦下），
    /// 而 <c>Shared</c> ⛔ 不能引用 <c>Auditor</c>（<c>Auditor → Shared</c> 单向）。
    /// ⇒ 能读该表的调用方在 Auditor（<c>FillParamValueController</c> / <c>DocumentFillController</c>），
    /// 由它们把值传进来。这样 <see cref="BuildValue"/> 保持<b>纯函数</b>、可单测、可重放。</para>
    /// </summary>
    [Skill(
        Code = "src_global_param",
        Name = "全局参数取值",
        ReturnType = "json",
        Description = "按参数编码取全局参数值（标准/阶段特化，取最特异一条）。产出 FillValue。"
    )]
    public static class SrcGlobalParamSkill
    {
        /// <summary>本 Skill 编码（写进 <c>FillSession.AnchorOwners</c> 与轨迹）</summary>
        public const string SkillCode = "src_global_param";

        /// <summary>
        /// 执行 —— 查定义 → 取值决策 → 组 <see cref="FillValue"/>。
        /// </summary>
        /// <param name="param_code">参数编码（必填）</param>
        /// <param name="anchor_code">锚点编码（空 ⇒ 用 <paramref name="param_code"/>）</param>
        /// <param name="standard_code">标准编码（空 ⇒ 只命中「不限标准」的定义）</param>
        /// <param name="stage_code">阶段编码（空 ⇒ 只命中「不限阶段」的定义）</param>
        /// <param name="org_code">机构编码（空 ⇒ 不加机构过滤；⚠️ 多机构库中应显式传）</param>
        /// <param name="enterprise_code">企业编码（给了才按企业档案自动带出 <c>auto</c>/<c>both</c> 的初值）</param>
        /// <param name="value_kind">
        /// 值类型：<c>text</c>/<c>number</c>/<c>date</c>/<c>bool</c>/<c>enum</c>。
        /// <para>空 ⇒ 用参数定义 <see cref="FillParamDef.ValueType"/>；仍空 ⇒ <c>text</c>。</para>
        /// </param>
        /// <param name="number_format">格式串（<b>.NET 方言</b>，见 39 号 §十六）</param>
        /// <param name="saved_value">★ 企业已填写的参数值（由编排器预取，见类注释）</param>
        /// <param name="saved_value_source">已填值的来源：<c>auto</c>/<c>manual</c>/<c>ai</c>/<c>import</c></param>
        /// <param name="saved_is_manual_edited">企业是否人工改过（=true 时不再被自动映射覆盖）</param>
        /// <param name="provider">★ 取值内核（DI 注入；查定义 + 取企业已填值）</param>
        /// <param name="ct">取消令牌</param>
        public static async Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "参数编码，如 company_name")]
            string param_code,

            [SkillParam(Description = "锚点编码（写进 FillValue.AnchorCode；空=用 param_code）")]
            string? anchor_code = null,

            [SkillParam(Description = "标准编码（空=只命中「不限标准」的定义）")]
            string? standard_code = null,

            [SkillParam(Description = "阶段编码（空=只命中「不限阶段」的定义）")]
            string? stage_code = null,

            [SkillParam(Description = "机构编码（空=不加机构过滤）")]
            string? org_code = null,

            [SkillParam(Description = "企业编码（给了才按企业档案自动带出）")]
            string? enterprise_code = null,

            [SkillParam(Description = "值类型：text/number/date/bool/enum（空=用参数定义的值类型）")]
            string? value_kind = null,

            [SkillParam(Description = "格式串（.NET 方言），如 #,##0.00 / yyyy年MM月dd日")]
            string? number_format = null,

            [SkillParam(Description = "★ 企业已填写的参数值（由编排器预取；本 Skill 不查 cert_fill_param_value）")]
            string? saved_value = null,

            [SkillParam(Description = "已填值的来源：auto/manual/ai/import")]
            string? saved_value_source = null,

            [SkillParam(Description = "企业是否人工改过（true=不再被自动映射覆盖）")]
            bool saved_is_manual_edited = false,

            // ★ 2026-10-06（S1-5）：取值内核。由 SkillExecutor 从 DI 容器解析
            //   （[FromService] 参数不参与业务参数绑定、不进 LLM 可见 schema）。
            //   注册点 = CertPlatform.Shared/CertPlatformSharedServiceExtensions.cs
            //   ⚠️ 本方法原先还有一个 [FromService] IDbOrm db —— 查询下沉后已无用，已删
            //      （保留会让读代码的人以为「这里还在自己查库」）。
            [FromService] FillParamValueProvider provider = null!,

            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(param_code))
                return SkillResult.Fail("param_code 不能为空");

            var anchor = string.IsNullOrWhiteSpace(anchor_code) ? param_code : anchor_code!;
            var sc = standard_code ?? string.Empty;
            var pc = stage_code ?? string.Empty;
            var org = org_code ?? string.Empty;

            // ① 取候选定义（★ 2026-10-06：改为调 Shared 的唯一实现，⛔ 本 Skill 不再手写查询）。
            //    OR 组合条件（OrgCode + (StandardCode='' OR S) + (StageCode='' OR P)）
            //    与 PickMostSpecific 去重口径都在 provider 内，此处不再重复。
            var def = await provider.FindDefAsync(param_code, standard_code, stage_code, org_code);
            if (def == null)
                return SkillResult.Fail(
                    $"未找到 param_code={param_code}, standard={sc}, stage={pc}, org={org}");

            // ② 企业档案快照（仅当给了企业编码；auto/both 的自动带出需要它）
            var enterprise = await provider.LoadEnterpriseInfoAsync(enterprise_code);

            // ③ 取值决策 + 组值（★ 抽成 BuildValue 以便单测，见下）
            var (ok, value, error) = BuildValue(
                def, enterprise, saved_value, saved_value_source, saved_is_manual_edited,
                anchor, value_kind, number_format);

            if (!ok)
                return SkillResult.Fail(error ?? "取值失败");

            return SkillResult.Ok(new Dictionary<string, object>
            {
                ["value"] = value!,
                ["anchor_code"] = anchor,
                ["hit"] = true,
                ["param_code"] = def.ParamCode,
                ["param_name"] = def.ParamName ?? def.ParamCode,
                ["specificity"] = SharedFill.ParamValueResolver.Specificity(def),
            }, value!.Confidence);
        }

        /// <summary>
        /// ★ <b>纯函数</b>部分：定义 + 企业档案 + 已填值 → <see cref="FillValue"/>。
        ///
        /// <para>抽出来的目的：<b>可单测</b>（不依赖 DB / DI）。
        /// ★ 2026-10-06（S1-5）：方法体已<b>委托</b>给全项目唯一实现
        /// <see cref="FillParamValueProvider.BuildValue"/>（签名保持不变，
        /// 既有单测继续钉住语义：取值语义只走 <c>ParamValueResolver.Resolve</c>，
        /// ⛔ 本类不重写 auto/manual/both 判定）。</para>
        /// </summary>
        /// <returns><c>(Ok, Value, Error)</c></returns>
        public static (bool Ok, FillValue? Value, string? Error) BuildValue(
            FillParamDef def,
            SharedFill.EnterpriseInfo enterprise,
            string? savedValue,
            string? savedValueSource,
            bool savedIsManualEdited,
            string anchorCode,
            string? valueKind,
            string? numberFormat)
            => FillParamValueProvider.BuildValue(
                def, enterprise, savedValue, savedValueSource, savedIsManualEdited,
                anchorCode, valueKind, numberFormat);

        // ★ 2026-10-06（S1-5）：原 private static MapToInfo(Enterprise) 已删除 ——
        //   它与 EnterpriseDocNormalizationExecutor.MapToInfo 是同一段 16 字段搬运的**两份手写副本**
        //   （缺陷 D6）。现统一为 Shared 层的 EnterpriseInfoMapper.ToInfo（唯一实现）。
        //   出口门：grep "MapToInfo" CertPlatform.Admin ⇒ 必须为 0。
    }
}
