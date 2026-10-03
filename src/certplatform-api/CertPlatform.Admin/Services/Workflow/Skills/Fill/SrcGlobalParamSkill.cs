using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Office;
using YZH.Core.DataBase.Interfaces;
using SharedFill = CertPlatform.Shared.Fill;

namespace CertPlatform.Admin.Services.Workflow.Skills.Fill
{
    /// <summary>
    /// 全局参数取值 —— 按 <c>param_code</c>（+ 标准 / 阶段）取**最特异**的一条定义，
    /// 经「全项目唯一」的取值决策得到值，产出 <see cref="FillValue"/>。
    ///
    /// <para><b>★★ 与 39 号 §4.2 的一处刻意偏离（★ 必读）</b>：
    /// §4.2 原设计让本 Skill 直接查 <c>cert_fill_param_value</c>（企业填的值）。
    /// 但该实体 <c>FillParamValue</c> 定义在 <b><c>CertPlatform.Auditor</c></b> 项目里，
    /// 而引用方向是 <b><c>Auditor → Admin</c> 单向</b> ⇒
    /// <b>本 Skill（在 Admin 项目内）看不到该实体，按原文写会编译不过</b>。</para>
    ///
    /// <para><b>本实现的做法</b>：企业已填值由 <b>编排器预取</b>后经
    /// <paramref name="saved_value"/> 传入（编排层在 Auditor，两边都能看）。这样反而更正确：</para>
    /// <list type="number">
    ///   <item>本 Skill 变成<b>近乎纯函数</b>（定义 + 企业档案 + 已填值 → 值），可单测、可重放；</item>
    ///   <item>取值语义仍<b>只由 <c>ParamValueResolver.Resolve</c> 一处决定</b>
    ///         （⛔ 本 Skill 不自写一套 auto/manual/both 判定 —— 那正是「两套口径」缺陷的来源）；</item>
    ///   <item>不为了一个字段把实体搬家（跨项目重构），符合「最小改动」。</item>
    /// </list>
    ///
    /// <para>⚠️ 若日后编排层迁进 Admin，则需把 <c>FillParamValue</c> 实体移到
    /// <c>CertPlatform.Shared/Entities/Cert/</c>（与 <c>FillParamDef</c> 成对），
    /// 届时可把预取改回 Skill 内查询 —— 但<b>取值决策点仍只能是 <c>ParamValueResolver</c></b>。</para>
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
        /// <param name="db">数据访问（DI 注入）</param>
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

            [FromService] IDbOrm db = null!,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(param_code))
                return SkillResult.Fail("param_code 不能为空");

            var anchor = string.IsNullOrWhiteSpace(anchor_code) ? param_code : anchor_code!;
            var sc = standard_code ?? string.Empty;
            var pc = stage_code ?? string.Empty;
            var org = org_code ?? string.Empty;

            // ① 取候选定义。
            //    ★ 条件 = OrgCode + (StandardCode='' OR S) + (StageCode='' OR P) —— 这是 OR 组合，
            //      FilterItem 表达不了 ⇒ 必须手写表达式（同 38 号 §「生效参数集」）。
            //    ⚠️ 用两个分支而非「!hasOrg || ...」：把常量折叠交给 ORM 容易踩翻译坑，显式分支最稳。
            var defs = string.IsNullOrWhiteSpace(org)
                ? (await db.GetListAsync<FillParamDef>(x =>
                        x.ParamCode == param_code &&
                        (x.StandardCode == "" || x.StandardCode == sc) &&
                        (x.StageCode == "" || x.StageCode == pc))).Data
                : (await db.GetListAsync<FillParamDef>(x =>
                        x.ParamCode == param_code &&
                        x.OrgCode == org &&
                        (x.StandardCode == "" || x.StandardCode == sc) &&
                        (x.StageCode == "" || x.StageCode == pc))).Data;

            defs ??= new List<FillParamDef>();

            // ② 去重口径：全项目唯一 = PickMostSpecific（⛔ 不得另写一套）
            var def = SharedFill.ParamValueResolver.PickMostSpecific(defs).FirstOrDefault();
            if (def == null)
                return SkillResult.Fail(
                    $"未找到 param_code={param_code}, standard={sc}, stage={pc}, org={org}");

            // ③ 企业档案快照（仅当给了企业编码；auto/both 的自动带出需要它）
            var enterprise = new SharedFill.EnterpriseInfo();
            if (!string.IsNullOrWhiteSpace(enterprise_code))
            {
                // ⚠️ GetOneAsync 返回 Result<T>（不是 T）⇒ 必须取 .Data。
                //    ★ 这里用 GetOneAsync（带 IsValid=1）是**对的**：企业档案只应取有效行。
                //      （记忆 §二十㉖ 说「后台取数用 GetOneIgnoreValidAsync」是针对
                //       「取自己正在转换的那份文件」的场景，与本处语义不同。）
                var ent = (await db.GetOneAsync<Enterprise>(x => x.Code == enterprise_code)).Data;
                if (ent != null) enterprise = MapToInfo(ent);
            }

            // ④ 取值决策 + 组值（★ 抽成 BuildValue 以便单测，见下）
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
        /// <para>抽出来的目的：<b>可单测</b>（不依赖 DB / DI）。取值语义
        /// <b>只走 <c>ParamValueResolver.Resolve</c></b>，⛔ 本方法不重写 auto/manual/both 判定。</para>
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
        {
            if (def == null)
                return (false, null, "参数定义不能为空");

            // ★ 全项目唯一决策点（auto=恒实时取企业档案 / both=可覆盖 / manual=手工）
            var decision = SharedFill.ParamValueResolver.Resolve(
                def, enterprise, savedValue, savedValueSource, savedIsManualEdited);

            if (string.IsNullOrWhiteSpace(decision.Value))
            {
                // ⛔ 不返回空值 —— 空值会被当成「填了空」，无法区分「没找到」
                return (false, null, $"未取到 param_code={def.ParamCode} 的值（{decision.SourceRef}）");
            }

            // 值类型：显式入参 > 参数定义 > text（⚠️ 由配置给，⛔ 不由值猜）
            var kind = string.IsNullOrWhiteSpace(valueKind) ? def.ValueType : valueKind;

            var (ok, value, error) = FillValueFactory.TryCreate(
                anchorCode, decision.Value, kind, numberFormat);

            if (!ok)
                return (false, null, error);

            value!.Source = decision.SourceRef;
            value.Confidence = 1.0;
            return (true, value, null);
        }

        /// <summary>实体 → 引擎快照（引擎在 Shared，不引用 <c>YZH.Core.DataBase</c>，故必须映射）</summary>
        private static SharedFill.EnterpriseInfo MapToInfo(Enterprise e) => new()
        {
            Code = e.Code,
            Name = e.Name,
            ShortName = e.ShortName,
            CreditCode = e.CreditCode,
            LegalPerson = e.LegalPerson,
            Province = e.Province,
            City = e.City,
            Address = e.Address,
            IndustryType = e.IndustryType,
            EmployeeCount = e.EmployeeCount,
            CertScope = e.CertScope,
            ContactName = e.ContactName,
            ContactPhone = e.ContactPhone,
            ContactEmail = e.ContactEmail,
            EnterpriseNo = e.EnterpriseNo,
            ArchiveDate = e.ArchiveDate,
        };
    }
}
