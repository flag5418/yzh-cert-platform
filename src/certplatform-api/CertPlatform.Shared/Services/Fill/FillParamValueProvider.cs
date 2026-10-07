using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CertPlatform.Shared.Entities.Cert;
using SqlSugar;
using YZH.Core.DataBase.Interfaces;
using SharedFill = CertPlatform.Shared.Fill;
using SharedOffice = CertPlatform.Shared.Office;

namespace CertPlatform.Shared.Services.Fill;

/// <summary>
/// 全局参数取值内核 —— <b>★ 全项目唯一的「定义 → 企业档案 → <see cref="SharedOffice.FillValue"/>」通道</b>。
///
/// <para><b>它解决什么问题（实测，2026-10-06 `57` / `59`）</b>：同一件事
/// 「按 <c>param_code</c> 取全局参数的值」在代码里有<b>三条互不相通的实现</b>（缺陷 D1）——</para>
/// <list type="number">
///   <item><c>Shared/Fill/ParamValueResolver</c>：<b>决策</b>（auto/both/manual 语义），唯一且正确 ✅</item>
///   <item><c>Admin/Services/Workflow/Skills/Fill/SrcGlobalParamSkill</c>：
///         <b>查定义 + 决策 + 组值</b>（支持企业已填值）</item>
///   <item><c>Admin/Services/Ent/SourceResolver.TryResolveGlobalAsync</c>：
///         <b>手抄了第 ② 条的查询与组值</b>（代码自己在 <c>:72-74</c> 留
///         <c>// TODO: 这里需要处理 saved_value</c> —— 即「已知缺口、未修」，
///         且<b>不报错、不告警</b>）。</item>
/// </list>
///
/// <para><b>★ 收敛后</b>：第 ②③ 条的「查定义」与「组值」都改为调本类 ——
/// <see cref="FindDefAsync"/>（唯一查询）与 <see cref="BuildValue"/>（唯一组装）。
/// 两条链从此<b>不可能给出不同的值</b>。</para>
///
/// <para><b>★ 为什么放在 <c>Shared/Services/</c> 而不是 <c>Shared/Fill/</c></b>：
/// 本类要查库（<c>IDbOrm</c>）。<c>Shared/Fill/**</c> 与 <c>Shared/Office/**</c> 是
/// <b>纯函数资产</b>（可单测、可离线跑），一旦掺入 <c>IDbOrm</c> 就失去这个性质
/// ⇒ 硬纪律：查库一律放本层（守卫 G-4：<c>grep -rn "IDbOrm" CertPlatform.Shared/Fill CertPlatform.Shared/Office</c> 恒为 0）。</para>
///
/// <para><b>★★ 为什么本类<b>不</b>直接查 <c>cert_fill_param_value</c>（企业已填值）</b>
/// —— 这是一个<b>刻意</b>的边界，⛔ 不是遗漏：</para>
/// <list type="bullet">
///   <item>该表的实体 <c>FillParamValue</c> 按 <c>24-后端实体归属清单-V1.md</c> §一 属
///         <b>专家端（Auditor）独占</b>（企业端「全局参数定义」完善清单是专家端功能）。
///         把它上移到 <c>Shared/Entities/</c> 会被守卫 <b>R17</b> 直接拦下
///         （判据：Shared 实体必须两端都用；实测 Admin=0 / Auditor=12）。</item>
///   <item>而 <c>Shared</c> ⛔ 不能引用 <c>Auditor</c>（<c>Auditor → Shared</c> 单向，反过来成环）。</item>
///   <item>⇒ 结论：<b>「企业已填值」由调用方预取后传入</b>（<see cref="BuildValue"/> 的
///         <c>savedValue</c> 三个入参）。能读该表的调用方在 Auditor（
///         <c>FillParamValueController</c> / <c>DocumentFillController</c>），
///         它们把值传进来即可 —— 与 <c>SrcGlobalParamSkill</c> 一贯的做法一致。</item>
/// </list>
///
/// <para>⚠️ <b>本类不重写取值语义</b>：auto/both/manual 的判定<b>只由
/// <see cref="SharedFill.ParamValueResolver.Resolve"/> 决定</b>，
/// 本类只负责「把料备齐 → 交给它 → 把结果包成 <see cref="SharedOffice.FillValue"/>」。</para>
/// </summary>
public sealed class FillParamValueProvider
{
    private readonly IDbOrm _db;

    public FillParamValueProvider(IDbOrm db)
    {
        _db = db;
    }

    // ────────────────────────────────────────────────────────────────
    // ① 查定义
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 取「最特异」的一条参数定义。
    ///
    /// <para>条件 = <c>ParamCode</c> + <c>(StandardCode='' OR S)</c> + <c>(StageCode='' OR P)</c>
    /// （给了 <paramref name="orgCode"/> 再加机构过滤）——
    /// ★ 这是 <b>OR 组合</b>，<c>FilterItem</c> 表达不了 ⇒ 必须手写表达式。</para>
    ///
    /// <para>⚠️ 用两个分支而非 <c>!hasOrg || x.OrgCode == org</c>：把常量折叠交给 ORM
    /// 容易踩表达式翻译坑，显式分支最稳。</para>
    ///
    /// <para>去重口径 = <see cref="SharedFill.ParamValueResolver.PickMostSpecific"/>
    /// （⛔ 不得另写一套：那是「标准 × 阶段裁剪」的执行点）。</para>
    /// </summary>
    public async Task<FillParamDef?> FindDefAsync(
        string paramCode,
        string? standardCode = null,
        string? stageCode = null,
        string? orgCode = null)
    {
        if (string.IsNullOrWhiteSpace(paramCode)) return null;

        var sc = standardCode ?? string.Empty;
        var pc = stageCode ?? string.Empty;

        var defs = string.IsNullOrWhiteSpace(orgCode)
            ? (await _db.GetListAsync<FillParamDef>(x =>
                    x.ParamCode == paramCode &&
                    (x.StandardCode == "" || x.StandardCode == sc) &&
                    (x.StageCode == "" || x.StageCode == pc))).Data
            : (await _db.GetListAsync<FillParamDef>(x =>
                    x.ParamCode == paramCode &&
                    x.OrgCode == orgCode &&
                    (x.StandardCode == "" || x.StandardCode == sc) &&
                    (x.StageCode == "" || x.StageCode == pc))).Data;

        return SharedFill.ParamValueResolver
            .PickMostSpecific(defs ?? new List<FillParamDef>())
            .FirstOrDefault();
    }

    /// <summary>
    ///     ★ <b>该 <c>param_code</c> 是否「曾被定义、后被<b>有意软删</b>」</b> —— <b>归因专用</b>。
    ///
    ///     <para><b>为什么必须有</b>：<see cref="FindDefAsync"/> 查不到定义时有两种
    ///     <b>语义完全不同</b>的原因，指向<b>相反</b>的修复动作：</para>
    ///     <list type="number">
    ///       <item><b>从来没定义过</b> ⇒ 模板锚点写错了参数名 ⇒ 去「企业资料参数」<b>新增</b>。</item>
    ///       <item><b>曾定义、后被有意移除</b> ⇒ 模板锚点引用了一个<b>已被裁决废弃</b>的编码
    ///         ⇒ ⛔ <b>不要重新新增</b>（那会推翻既有裁决），要<b>改配来源</b>。</item>
    ///     </list>
    ///
    ///     <para>本方法判定的就是第 2 种。真实案例：2026-10-06 迁移
    ///     （<c>scripts/db/fix/20261006_fill_param_standard_dict_V1.sql</c> §第 1 节）
    ///     <b>有意软删</b>了 9 条「档案镜像参数」——
    ///     <c>company_name</c>/<c>company_short_name</c>/<c>credit_code</c>/<c>legal_person</c>/
    ///     <c>company_address</c>/<c>industry_type</c>/<c>cert_scope</c>/<c>employee_count</c>/
    ///     <c>contact_name</c>，理由（脚本原话）：
    ///     「<b>企业基本资料应由「企业基本资料」关联带出，字典不需要</b>」。
    ///     ⇒ 这些编码<b>不是</b>「忘了配」，而是「<b>被有意废掉</b>」。</para>
    ///
    ///     <para>⚠️ 若归因不区分这两者，就会对第 2 种说「请去新增该参数编码」
    ///     ⇒ 用户照着做 = <b>把刚删掉的镜像参数又加回来</b>，与 2026-10-06 裁决直接冲突。
    ///     （同 <c>DocumentFillController:237-240</c> 的教训：「<b>指错方向比不报错更耗时</b>」。）</para>
    ///
    ///     <para>⛔ <b>必须用 <c>Client.Queryable</c>（完全不过滤）</b>：
    ///     <see cref="FindDefAsync"/> 走的 <c>GetListAsync</c> 恒带 <c>IsDeletedCondition</c>，
    ///     软删行<b>根本取不到</b> —— 用它来判「是否曾被软删」永远是 <c>false</c>。</para>
    /// </summary>
    /// <param name="paramCode">参数编码（<c>cert_fill_param_def.ParamCode</c>）</param>
    /// <returns>存在 <c>IsDeleted=1</c> 的行 ⇒ <c>true</c></returns>
    public async Task<bool> WasSoftDeletedAsync(string paramCode)
    {
        if (string.IsNullOrWhiteSpace(paramCode)) return false;

        var rows = await _db.Client.Queryable<FillParamDef>()
            .Where(x => x.ParamCode == paramCode && x.IsDeleted)
            .Take(1)
            .ToListAsync();

        return rows.Count > 0;
    }

    // ────────────────────────────────────────────────────────────────
    // ② 企业档案快照
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 读企业档案并映射为引擎快照（<c>auto</c>/<c>both</c> 的自动带出来源）。
    /// 企业不存在 / 编码为空 ⇒ 返回空快照（由 <c>Resolve</c> 产出「待补齐」决策）。
    ///
    /// <para>⚠️ 用 <c>GetOneAsync</c>（带 <c>IsValid=1</c>）是<b>对的</b>：企业档案只应取有效行。
    /// （记忆 §二十㉖ 说「后台取数用 <c>GetOneIgnoreValidAsync</c>」是针对
    /// 「取自己正在转换的那份文件」的场景，与本处语义不同。）</para>
    /// </summary>
    public async Task<SharedFill.EnterpriseInfo> LoadEnterpriseInfoAsync(string? enterpriseCode)
    {
        if (string.IsNullOrWhiteSpace(enterpriseCode)) return new SharedFill.EnterpriseInfo();

        var ent = (await _db.GetOneAsync<Enterprise>(x => x.Code == enterpriseCode)).Data;
        return EnterpriseInfoMapper.ToInfo(ent);
    }

    // ────────────────────────────────────────────────────────────────
    // ③ ★ 唯一「组装」内核（纯函数）
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// ★ <b>纯函数</b>：定义 + 企业档案 + 已填值 → <see cref="SharedOffice.FillValue"/>。
    ///
    /// <para>这是全项目<b>唯一</b>的「组值」实现 ——
    /// <c>SrcGlobalParamSkill.BuildValue</c> 与 <see cref="ResolveAsync"/> 都委托到这里
    /// （前者是 Skill 的对外契约，签名保持不变以便既有单测继续钉住语义）。</para>
    ///
    /// <para><b>失败语义（⛔ 必须保留）</b>：取不到值 ⇒ <c>Ok=false</c>。
    /// <b>绝不返回「空值」</b> —— 空值会被下游当成「填了空」，无法与「没找到」区分
    /// （<c>FillValueFactory.TryCreate</c> 对空串返回 <c>Ok=true</c>，故此处必须自己拦）。</para>
    /// </summary>
    /// <param name="def">参数定义</param>
    /// <param name="enterprise">企业档案快照（<c>auto</c>/<c>both</c> 的自动带出来源）</param>
    /// <param name="savedValue">企业在 <c>cert_fill_param_value</c> 里已填的值（无 ⇒ null）</param>
    /// <param name="savedValueSource">已填值的来源：<c>auto</c>/<c>manual</c>/<c>import</c></param>
    /// <param name="savedIsManualEdited">企业是否人工改过（=true 时不再被自动映射覆盖）</param>
    /// <param name="anchorCode">锚点键（写进 <c>FillValue.AnchorCode</c>）</param>
    /// <param name="valueKind">
    /// 值类型：<c>text</c>/<c>number</c>/<c>date</c>/<c>bool</c>/<c>enum</c>。
    /// 空 ⇒ 用参数定义 <see cref="FillParamDef.ValueType"/>；仍空 ⇒ <c>text</c>。
    /// ⚠️ 由配置给，⛔ 不由值猜。
    /// </param>
    /// <param name="numberFormat">格式串（<b>.NET 方言</b>）</param>
    public static (bool Ok, SharedOffice.FillValue? Value, string? Error) BuildValue(
        FillParamDef def,
        SharedFill.EnterpriseInfo enterprise,
        string? savedValue,
        string? savedValueSource,
        bool savedIsManualEdited,
        string anchorCode,
        string? valueKind,
        string? numberFormat)
    {
        if (def == null)
            return (false, null, "参数定义不能为空");

        // ★ 全项目唯一决策点（auto=恒实时取企业档案 / both=可覆盖 / manual=手工）
        var decision = SharedFill.ParamValueResolver.Resolve(
            def,
            enterprise ?? new SharedFill.EnterpriseInfo(),
            savedValue,
            savedValueSource,
            savedIsManualEdited);

        if (string.IsNullOrWhiteSpace(decision.Value))
        {
            // ⛔ 不返回空值 —— 空值会被当成「填了空」，无法区分「没找到」
            return (false, null, $"未取到 param_code={def.ParamCode} 的值（{decision.SourceRef}）");
        }

        // 值类型：显式入参 > 参数定义 > text
        var kind = string.IsNullOrWhiteSpace(valueKind) ? def.ValueType : valueKind;

        var (ok, value, error) = SharedOffice.FillValueFactory.TryCreate(
            anchorCode, decision.Value, kind, numberFormat);

        if (!ok || value == null)
            return (false, null, error);

        value.Source = decision.SourceRef;
        value.Confidence = 1.0;
        return (true, value, null);
    }

    // ────────────────────────────────────────────────────────────────
    // ④ 完整入口（查定义 → 组值）
    // ────────────────────────────────────────────────────────────────

    /// <summary>一次取值的产出。<c>Ok=false</c> 时 <c>Error</c> 是原因、<c>Value</c> 为 null。</summary>
    public readonly record struct Outcome(bool Ok, SharedOffice.FillValue? Value, string? Error);

    /// <summary>
    /// 完整取值：<b>查定义 → 组装</b>（组装的唯一实现在 <see cref="BuildValue"/>）。
    ///
    /// <para>⚠️ <b>企业已填值不由本方法查询</b> —— 见类注释「为什么本类不直接查
    /// <c>cert_fill_param_value</c>」：该实体属专家端，调用方预取后经
    /// <paramref name="savedValue"/> 三个入参传入。</para>
    /// </summary>
    public async Task<Outcome> ResolveAsync(
        string paramCode,
        string anchorCode,
        SharedFill.EnterpriseInfo enterprise,
        string? standardCode = null,
        string? stageCode = null,
        string? orgCode = null,
        string? savedValue = null,
        string? savedValueSource = null,
        bool savedIsManualEdited = false,
        string? valueKind = null,
        string? numberFormat = null)
    {
        if (string.IsNullOrWhiteSpace(paramCode))
            return new Outcome(false, null, "paramCode 不能为空");

        var def = await FindDefAsync(paramCode, standardCode, stageCode, orgCode);
        if (def == null)
            return new Outcome(false, null,
                $"未找到 param_code={paramCode}, standard={standardCode}, stage={stageCode}, org={orgCode}");

        var (ok, value, error) = BuildValue(
            def, enterprise, savedValue, savedValueSource, savedIsManualEdited,
            anchorCode, valueKind, numberFormat);

        return new Outcome(ok, value, error);
    }
}
