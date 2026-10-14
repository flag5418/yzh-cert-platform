using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Auditor.Services.Ent.Normalize;
using CertPlatform.Shared.Domain.Scope;
using CertPlatform.Shared.Fill;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    ///     ★ <b>企业资料参数通用服务</b> —— 「查询 + 补录」的唯一入口（`66` V2.1 §二）。
    ///
    ///     <para><b>为什么需要它</b>（用户 2026-10-14 D-4-2 裁决原话：
    ///     「查询和补录都需要一个通用的 service 而不是 controller，需要复用的时候可以直接通过 service 获取」）：
    ///     同一件事（取定义 × 取值 × 决策 × 分组）此前在
    ///     <c>FillParamValueController.MergeList</c> / <c>Save</c> 与
    ///     <c>EnterpriseNormalizeController.BuildParamGapsAsync</c> <b>各写了一遍</b>
    ///     ⇒ 收口到本服务，⛔ 不允许再在 Controller 里各写一份。</para>
    ///
    ///     <para><b>★ 铁律：入参 / 谓词 / 关联只用 <c>Code</c></b>（`AGENTS.md` ④ 双关键字准则 A），⛔ 无 <c>Id</c>。</para>
    ///
    ///     <para><b>★ 落位约束</b>：必须在 <c>Auditor</c> —— 本服务读写 <c>FillParamValue</c>
    ///     （<c>Auditor/Entities/Cert/</c> 独占实体，R17 裁决：实测 Admin=0 / Auditor=12），
    ///     而 <c>Shared</c> ⛔ 不能引 <c>Auditor</c>（反向成环，<b>A-R2 零容忍</b>）。</para>
    ///
    ///     <para><b>★ 依赖</b>（全部已存在，零新增底层能力）：<see cref="IDbOrm"/>（读写值表 + 查企业档案）
    ///     + <see cref="ICoreResourceQuery"/>（取参数定义 + 翻译标准名 —— 复用 Shared 收口）
    ///     + <see cref="ParamValueResolver"/>（纯函数决策，⛔ 不重写）。</para>
    /// </summary>
    public interface IFillParamService
    {
        /// <summary>
        ///     ① <b>合并清单</b>（纯读）—— 「后台定义 × 企业已填值」按 <c>GroupName</c> 分组 + 完成度。
        ///     <para>失败时 <c>Error</c> 非空（企业不存在 / 越权），由调用方包成业务拒绝信封。</para>
        /// </summary>
        Task<Result<FillParamListResult>> GetMergedListAsync(
            FillParamScope scope, CancellationToken ct = default);

        /// <summary>
        ///     ② <b>缺参清单</b>（纯读）—— 按「涉及标准」算缺失项，供规范化入队前预检 + 补参弹窗。
        ///
        ///     <para><paramref name="includeOptional"/> = <c>false</c> 时只算必填
        ///     （= <c>run</c> 的拦截口径，与既有 <c>BuildParamGapsAsync</c> 语义一致）；
        ///     <c>true</c> 时「必填 + 选填」全出（= 补参弹窗口径，D-4-3）。</para>
        ///
        ///     <para>⛔ 纯读零副作用；永不失败（查不到就是空清单）。</para>
        /// </summary>
        /// <returns>
        ///     按标准分组的缺参组。⚠️ 复用既有的 <see cref="NormalizeParamGapGroup"/>
        ///     （与 <c>NormalizePlanResult.ParamGaps</c> 同一类型）⇒ 调用方可直接赋值，
        ///     ⛔ 不新建同形类型（两份同形 DTO 必然漂移）。
        /// </returns>
        Task<List<NormalizeParamGapGroup>> GetGapListAsync(
            FillParamScope scope, IEnumerable<string> standardCodes,
            bool includeOptional, CancellationToken ct = default);

        /// <summary>
        ///     ③ <b>批量补录</b>（写）—— 只落 <c>manual</c> / <c>both</c>；<c>auto</c> 项忽略并回报。
        ///     <para>失败时 <c>Error</c> 非空（无内容 / 企业不存在 / 越权 / 未定义编码 / 保存异常）。</para>
        /// </summary>
        Task<Result<FillParamSaveResult>> SaveAsync(
            FillParamScope scope, IReadOnlyList<FillParamSaveItem> items,
            string userCode, CancellationToken ct = default);
    }

    /// <summary>
    ///     <see cref="IFillParamService"/> 的唯一实现。
    ///
    ///     <para>⚠️ <b>本类由三处 Controller 的私有实现原样搬运而来</b>（2026-10-14 第 0 批纯重构）：
    ///     <c>FillParamValueController.MergeList</c>（<c>:245-388</c>）→ <see cref="GetMergedListAsync"/>；
    ///     <c>FillParamValueController.Save</c>（<c>:400-546</c>）→ <see cref="SaveAsync"/>；
    ///     <c>EnterpriseNormalizeController.BuildParamGapsAsync</c>（<c>:414-495</c>）→ <see cref="GetGapListAsync"/>。</para>
    ///
    ///     <para><b>★ 搬运过程中被刻意修正的两处既有缺陷</b>（详见各方法内的 ★ 注释）——
    ///     两处都按 `66` V2.1 §1.4 / §2.6 的<b>显式口径</b>落地，⛔ 不是「顺手改」：
    ///     ① 缺参预检的定义过滤由「模板机构 Code」改为<b>平台级 <c>OrgCode == ''</c></b>
    ///        （原写法因 <c>cert_fill_param_def.OrgCode</c> 恒空串而<b>恒取不到定义</b> ⇒ 缺参拦截是死代码）；
    ///     ② 标准名翻译由「按 <c>ISOStandard.StandardCode</c>（业务编号）查」改为
    ///        <b>按 <c>ISOStandard.Code</c>（标准行 Code）查</b>（原写法恒查不到 ⇒ 名称恒回退「未登记标准」）。</para>
    /// </summary>
    public sealed class FillParamService : IFillParamService
    {
        private readonly IDbOrm _db;
        private readonly ICoreResourceQuery _core;

        public FillParamService(IDbOrm db, ICoreResourceQuery core)
        {
            _db = db;
            _core = core;
        }

        // ════════════════════════════════════════════════════════════════════
        //  ① 合并清单（= 原 FillParamValueController.MergeList 下沉）
        // ════════════════════════════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<Result<FillParamListResult>> GetMergedListAsync(
            FillParamScope scope, CancellationToken ct = default)
        {
            // ── 0. 企业定位 + 越权防护（企业归属 = 工作区 Code）──
            // ★ A-R6 / 铁律 L5：企业取数**必须**收口 CoreResourceQuery，
            //   端内 ⛔ 不得裸 `Queryable<Enterprise>()`。
            //   `GetEnterpriseByCodeAsync` = `GetOneAsync<Enterprise>(x => x.Code == ec)`
            //   ⇒ 框架自动追加 `IsDeleted == 0 ∧ IsValid == 1`（Enterprise 自身声明了这两列）。
            //   ⚠️ 与原写法（`Queryable<Enterprise>().Where(Code == ec && IsDeleted == false)`）
            //      相比**多一层 `IsValid == 1`** —— 即「已禁用企业」不再返回。这是 L5 收口的既定语义
            //      （全仓 7 处调用同此：QueueMonitorController:176/201 · EnterpriseFileService:1991/2015/2291 ·
            //      EnterpriseOriginalService:2060/2070 · EnterpriseNormalizeController:2036 等），
            //      且本页语义是「为某企业维护参数」，已禁用企业本就不该开放。
            //      ⛔ 本批 A/B 不可分辨（库中唯一企业 IsValid=1）。
            var ent = await _core.GetEnterpriseByCodeAsync(scope.EnterpriseCode);

            if (ent == null)
                return Result<FillParamListResult>.Fail("企业不存在或已删除");

            if (!string.Equals(ent.OrgCode, scope.WorkspaceCode, StringComparison.Ordinal))
                return Result<FillParamListResult>.Fail("无权查看其他工作区的企业参数");

            // ★ 参数定义全平台共享（2026-10-06 起不分机构：OrgCode 恒空串 —— 见 26 号 §3.1 裁决）
            var orgCode = string.Empty;
            var stdCode = scope.StandardCode ?? string.Empty;
            var stageCode = scope.StageCode ?? string.Empty;

            // ── 1. 取定义（含通配），按 ParamCode 去重取「更具体」的那条 ──
            // ★ 1.B.6d-1：取数收口 CoreResourceQuery
            //   等价：OrgCode=="" + 「通配("")/等值」两维；框架默认 IsDeleted==0 ∧ IsValid==1
            var defs = (await _core.GetFillParamDefsAsync(stdCode, stageCode))
                .OrderBy(d => d.Sort)
                .ToList();

            var picked = ParamValueResolver.PickMostSpecific(defs);

            // ── 2. 取已落库的企业填写值 ──
            var paramCodes = picked.Select(d => d.ParamCode).ToList();
            var saved = paramCodes.Count == 0
                ? new List<FillParamValue>()
                : await _db.Client.Queryable<FillParamValue>()
                    .Where(v => v.EnterpriseCode == ent.Code && v.IsDeleted == false
                                && paramCodes.Contains(v.ParamCode))
                    .ToListAsync();

            // 值行的 (StandardCode, StageCode) 镜像定义的；用「定义键 → 值」精确匹配，
            // 避免「同一 ParamCode 在不同标准下各有值」时串行
            var valueMap = saved
                .GroupBy(v => ParamValueResolver.ValueKey(v.StandardCode, v.StageCode, v.ParamCode))
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var entInfo = ToEnterpriseInfo(ent);

            // ── 3. 逐条合并（取值决策统一走 ParamValueResolver，⛔ 不要在此另写一份）──
            var items = new List<MergedItem>();
            foreach (var def in picked)
            {
                valueMap.TryGetValue(
                    ParamValueResolver.ValueKey(def.StandardCode, def.StageCode, def.ParamCode), out var row);

                var d = ParamValueResolver.Resolve(def, entInfo, row?.ParamValue, row?.ValueSource,
                    row?.IsManualEdited ?? false);

                items.Add(new MergedItem
                {
                    ParamCode = def.ParamCode,
                    ParamName = def.ParamName,
                    GroupName = string.IsNullOrWhiteSpace(def.GroupName) ? "其他" : def.GroupName!,
                    ValueType = def.ValueType,
                    EnumOptions = def.EnumOptions,
                    MaintainMode = def.MaintainMode,
                    SourceKind = def.SourceKind,
                    SourceExpr = def.SourceExpr,
                    IsRequired = def.IsRequired,
                    IsBuiltin = def.IsBuiltin,
                    Sort = def.Sort,
                    Description = def.Description,
                    Placeholder = def.Placeholder,

                    ParamValue = d.Value,
                    ValueSource = d.ValueSource,
                    SourceRef = d.SourceRef,
                    IsManualEdited = d.IsManualEdited,
                    Editable = d.Editable,
                    IsFilled = !string.IsNullOrWhiteSpace(d.Value),
                });
            }

            // ── 4. 分组 + 完成度（★ 业务判定，⛔ 不进视图）──
            var groups = items
                .GroupBy(i => i.GroupName)
                .Select(g => new FillParamGroup
                {
                    GroupName = g.Key,
                    Items = g.OrderBy(i => i.Sort).ThenBy(i => i.ParamCode).ToList(),
                })
                .ToList();

            var total = items.Count;
            var filledCount = items.Count(i => i.IsFilled);
            var requiredItems = items.Where(i => i.IsRequired).ToList();

            return Result<FillParamListResult>.Ok(new FillParamListResult
            {
                // ★ 整个响应一律 PascalCase（含外层与 Enterprise 段）—— 由 Program.cs 的
                //   PropertyNamingPolicy = null 保证；⛔ 本端点曾用 camelCase：前端按 `enterprise.Name`
                //   读恒得 undefined ⇒「企业名永远显示为空且不报错」。
                OrgCode = orgCode,
                WorkspaceCode = scope.WorkspaceCode,
                StandardCode = stdCode,
                StageCode = stageCode,
                Enterprise = new FillParamEnterpriseSnapshot
                {
                    Code = ent.Code,
                    Name = ent.Name,
                    ShortName = ent.ShortName,
                    CreditCode = ent.CreditCode,
                    LegalPerson = ent.LegalPerson,
                    Address = ent.Address,
                    IndustryType = ent.IndustryType,
                    EmployeeCount = ent.EmployeeCount,
                    CertScope = ent.CertScope,
                    ContactName = ent.ContactName,
                    ContactPhone = ent.ContactPhone,
                    ContactEmail = ent.ContactEmail,
                    EnterpriseNo = ent.EnterpriseNo,
                    Province = ent.Province,
                    City = ent.City,
                },
                Groups = groups,
                Progress = new FillParamProgress
                {
                    Total = total,
                    Filled = filledCount,
                    Empty = total - filledCount,
                    // ★ 完成度是 **0~1 的比值**（不是百分数），前端负责 ×100。
                    // ⛔ Total=0 记 0 而不是 1：与前端 liveCompletion 口径一致，
                    //    且「没有参数定义」应由界面显示空态，不该显示「100% 已完善」。
                    Completion = total == 0 ? 0.0 : Math.Round((double)filledCount / total, 4),
                    Required = requiredItems.Count,
                    RequiredFilled = requiredItems.Count(i => i.IsFilled),
                },
            });
        }

        // ════════════════════════════════════════════════════════════════════
        //  ② 缺参清单（= 原 EnterpriseNormalizeController.BuildParamGapsAsync 下沉 + 选填开关）
        // ════════════════════════════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<List<NormalizeParamGapGroup>> GetGapListAsync(
            FillParamScope scope, IEnumerable<string> standardCodes,
            bool includeOptional, CancellationToken ct = default)
        {
            var result = new List<NormalizeParamGapGroup>();

            var standards = (standardCodes ?? Enumerable.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (standards.Count == 0 || string.IsNullOrWhiteSpace(scope.EnterpriseCode)) return result;

            var enterpriseCode = scope.EnterpriseCode;
            var stageCode = scope.StageCode ?? string.Empty;

            // ── ① 参数定义全集 ──
            // ★ 口径（`66` V2.1 §1.4「取定义谓词」）：OrgCode == '' ∧ (StandardCode == '' || == S)
            //   ∧ (StageCode == '' || == stage)；IsRequired 由 includeOptional 决定是否收窄。
            //
            // ★★ 这里**修正**了原 BuildParamGapsAsync 的一处既有缺陷（`66` V2.1 §2.6 显式要求
            //    复用 GetFillParamDefsAsync ⇒ 平台级 OrgCode == ''）：
            //    原写法是 `d.OrgCode == orgCode`，而 orgCode = `scope.Published.FirstOrDefault()?.OrgCode`
            //    = **模板所属认证机构 Code**（非空，DocTemplateController.PrepareAsync:745 强制）；
            //    而 cert_fill_param_def.OrgCode **恒为空串**（FillParamDef.cs:25-29 + OnBeforeAdd/Update 强制置 ''）。
            //    ⇒ 两者永不相等 ⇒ defs.Count == 0 ⇒ 缺参清单**恒为空** ⇒ run 的缺参拦截是**死代码**。
            //    ⚠️ 本批数据不可分辨（全库已发布模板 = 0 ⇒ 该分支本就不可达），故 A/B 无法暴露。
            //
            // ⚠️ GetFillParamDefsAsync 只支持**单个**标准 ⇒ 逐标准取后按
            //    (StandardCode, StageCode, ParamCode) 去重（通配定义会被每个标准各取到一次）。
            var defsByIdentity = new Dictionary<string, FillParamDef>(StringComparer.Ordinal);
            foreach (var std in standards)
            {
                foreach (var d in await _core.GetFillParamDefsAsync(std, stageCode))
                    defsByIdentity[ParamValueResolver.ValueKey(d.StandardCode, d.StageCode, d.ParamCode)] = d;
            }

            var defs = defsByIdentity.Values.AsEnumerable();
            if (!includeOptional) defs = defs.Where(d => d.IsRequired);
            var defList = defs.ToList();
            if (defList.Count == 0) return result;

            // ── ② 企业已填的有效值：该企业 × (阶段==stage 或 不限) × (标准∈涉及 或 不限) ──
            var filled = (await _db.GetListAsync<FillParamValue>(v =>
                v.EnterpriseCode == enterpriseCode
                && v.IsValid == 1
                && (v.StageCode == "" || v.StageCode == stageCode)
                && (v.StandardCode == "" || standards.Contains(v.StandardCode)))).Data
                ?? new List<FillParamValue>();
            var filledCodes = filled
                .Where(v => !string.IsNullOrWhiteSpace(v.ParamValue))
                .GroupBy(v => v.StandardCode, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(v => v.ParamCode)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToHashSet(StringComparer.Ordinal));

            // ── ③ 标准名翻译：★ 按 Code 索引 ──
            // ★★ 这里**修正**了原实现的另一处既有缺陷（`66` V2.1 §1.4「标准名翻译」表：
            //    `GetStandardsByCodesAsync(codes)` → `ToDictionary(s => s.Code)`）：
            //    原写法是「按 ISOStandard.StandardCode（业务编号，如 iso13485）建字典，再用标准行 Code
            //    （GUID）去查」⇒ 键类型不同 ⇒ 永不命中 ⇒ StandardName 恒回退「未登记标准（短码）」。
            //    入参 `standardCodes` 就是标准行 Code（= cert_iso_standard.Code）。
            var stdMap = (await _core.GetStandardsByCodesAsync(standards))
                // ⚠️ ISOStandard.Code 声明为 string?（BaseEntity.Code）—— 查表键用 `!` 收敛，
                //    ⛔ 不改变行为：`standards` 已过滤空串，查不到的键本来就取不到。
                .GroupBy(s => s.Code!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            // ── ④ 逐标准算差集 ──
            foreach (var std in standards)
            {
                // 该标准适用的参数 = def 的 StandardCode ∈ {std, ''}
                var required = defList
                    .Where(d => d.StandardCode == "" || d.StandardCode == std)
                    .Select(d => d.ParamCode)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (required.Count == 0) continue;

                // 已填 = 该标准的通配值 + 该标准专属值（非空）
                var filledForStd = new HashSet<string>(StringComparer.Ordinal);
                if (filledCodes.TryGetValue("", out var wc)) filledForStd.UnionWith(wc);
                if (filledCodes.TryGetValue(std, out var sc)) filledForStd.UnionWith(sc);

                var missing = required.Except(filledForStd, StringComparer.Ordinal).ToList();
                if (missing.Count == 0) continue;

                stdMap.TryGetValue(std, out var iso);

                result.Add(new NormalizeParamGapGroup
                {
                    StandardCode = std,
                    // ★ 查不到主数据时给「未登记标准（短码）」—— ⛔ 不回退裸 GUID
                    StandardName = NormalizeLabels.StandardLabel(iso?.StandardName, iso?.StandardCode, std),
                    // ⚠️ 沿用原实现的 `First`（不按 Specificity 取最具体者）—— 保持搬运前语义，
                    //    ⛔ 本批不改；`cert_fill_param_def.StandardCode` 当前全空 ⇒ 该歧义不可达。
                    Items = missing
                        .Select(pc => defList.First(d => d.ParamCode == pc
                                                         && (d.StandardCode == "" || d.StandardCode == std)))
                        .Select(d => new NormalizeParamGapItem
                        {
                            ParamCode = d.ParamCode,
                            ParamName = d.ParamName,
                            GroupName = d.GroupName ?? string.Empty,
                            ValueType = d.ValueType,
                            // ⚠️ 原实现硬编码 true（因为它只取了必填定义）；includeOptional=true 时
                            //    必须回传定义的真实 IsRequired，否则弹窗无法区分必填/选填。
                            IsRequired = d.IsRequired,
                        })
                        .ToList(),
                });
            }

            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        //  ③ 批量补录（= 原 FillParamValueController.Save 下沉）
        // ════════════════════════════════════════════════════════════════════

        /// <inheritdoc />
        public async Task<Result<FillParamSaveResult>> SaveAsync(
            FillParamScope scope, IReadOnlyList<FillParamSaveItem> items,
            string userCode, CancellationToken ct = default)
        {
            if (items == null || items.Count == 0)
                return Result<FillParamSaveResult>.Fail("没有需要保存的内容");

            // ★ 同上（A-R6 / L5）：企业取数收口 CoreResourceQuery，⛔ 不裸查。
            var ent = await _core.GetEnterpriseByCodeAsync(scope.EnterpriseCode);

            if (ent == null)
                return Result<FillParamSaveResult>.Fail("企业不存在或已删除");

            if (!string.Equals(ent.OrgCode, scope.WorkspaceCode, StringComparison.Ordinal))
                return Result<FillParamSaveResult>.Fail("无权修改其他工作区的企业参数");

            // ★ 参数值行 OrgCode = 工作区 Code（数据归属，见 WorkspaceContextService 对照表）；
            //   参数定义 OrgCode 恒为后台标准级 ''
            //   （2026-10-12 铁律取代 10-06「恒空串」裁决 —— 见跨端规则 §二）
            var workspaceCode = scope.WorkspaceCode;
            var stdCode = scope.StandardCode ?? string.Empty;
            var stageCode = scope.StageCode ?? string.Empty;

            // 定义全集（校验 paramCode 合法性 —— 不认识的一律报错，不静默丢弃）
            var defs = await _core.GetFillParamDefsAsync(stdCode, stageCode);

            var defMap = defs
                .GroupBy(d => d.ParamCode)
                .ToDictionary(g => g.Key,
                    // ★ tie-break 用 Id 是准则明确允许的（「OrderBy(Id) 允许」），⛔ 不是违规
                    g => g.OrderByDescending(ParamValueResolver.Specificity).ThenBy(d => d.Id).First(),
                    StringComparer.Ordinal);

            var unknown = items
                .Where(i => !defMap.ContainsKey(i.ParamCode))
                .Select(i => i.ParamCode)
                .ToList();
            if (unknown.Count > 0)
                return Result<FillParamSaveResult>.Fail(
                    $"以下参数编码未在当前标准作用域下定义（含通配）：{string.Join("、", unknown)}");

            var now = DateTime.Now;
            var savedCount = 0;
            var ignoredAuto = new List<string>();

            using var tx = _db.BeginTransaction();
            try
            {
                foreach (var item in items)
                {
                    var def = defMap[item.ParamCode];

                    // ★ auto 项不接受覆盖：语义是「永远等于企业档案」
                    if (string.Equals(def.MaintainMode, ParamValueResolver.ModeAuto, StringComparison.Ordinal))
                    {
                        ignoredAuto.Add(def.ParamName);
                        continue;
                    }

                    // 值行的 (StandardCode, StageCode) 镜像定义，与唯一键 uk_ent_std_stage_param 对齐
                    var rowStd = def.StandardCode;
                    var rowStage = def.StageCode;

                    // ★ 唯一键 `uk_ent_std_stage_param` **不含 `IsDeleted`** ⇒ 查重必须「含已删」：
                    //   命中已软删行 ⇒ 就地复活；⛔ 不能走新建（新建必撞唯一键）。
                    //
                    // ⚠️⚠️ 这里**不能**用 `_db.GetOneIgnoreValidAsync`（原实现用的就是它）——
                    //   它只「绕过 `IsValid` 过滤」，**`IsDeleted == 0` 照加**
                    //   （`SqlSugarDbOrm.cs:75-78`；其 XML 注释亦明写「仅过滤软删除」）
                    //   ⇒ 名字里的「IgnoreValid」指的是 `IsValid` 那一列，**不是**「什么有效性都不过滤」
                    //   ⇒ 它**永远查不到已软删的行**，与原实现的假设正好相反。
                    //   后果：原实现实际总走 INSERT 分支 ⇒ 撞唯一键 ⇒ `InsertAsync` 返回的 `Fail`
                    //   **被丢弃**（返回值没人看）⇒ 接口回「已保存 N 项」而**库里零变化**。
                    //   实测证据（2026-10-14 后端日志）：
                    //     `InsertAsync 失败，Type=FillParamValue` /
                    //     `Duplicate entry '26d6dff4…---company_profile' for key
                    //      'cert_fill_param_value.uk_ent_std_stage_param'`
                    //     同一请求却返回 `success:true, savedCount:1`。
                    //   ⇒ 症状：某企业某参数**只要曾被删过一次**，之后在填写页就再也存不进去，
                    //     而且界面报成功（2026-10-14 用户裁定「本批修掉」）。
                    //   故改为裸 Queryable 显式只按「四键」定位、**不滤软删**
                    //   （本文件 `GetMergedListAsync` 读值表已用同款写法；`FillParamValue` 无
                    //   `[ViewName]` ⇒ 走物理表，与 ORM 包装路径等价）。
                    var existing = await _db.Client.Queryable<FillParamValue>()
                        .Where(v => v.EnterpriseCode == ent.Code
                                    && v.StandardCode == rowStd
                                    && v.StageCode == rowStage
                                    && v.ParamCode == def.ParamCode)
                        .FirstAsync();

                    var value = item.ParamValue ?? string.Empty;

                    if (existing != null)
                    {
                        existing.ParamValue = value;
                        // ★ 本页是「按后台定义逐项手填」的简单填写页 ⇒ 值来源恒 manual
                        //   （2026-10-07 用户裁决：信息由人填写，不由 AI 分析产出）
                        existing.ValueSource = "manual";
                        existing.IsManualEdited = true;
                        existing.IsFilled = !string.IsNullOrWhiteSpace(value);
                        existing.IsDeleted = false;
                        existing.DeleteBy = null;
                        existing.DeleteTime = null;
                        existing.UpdateTime = now;
                        existing.UpdateBy = userCode;
                        // ★ R-A：⛔ 禁单参 `UpdateAsync(entity)`（全列写回 = 陷阱 ㉑ / P1-14）——
                        //   单参版走 `.IgnoreColumns(Code/Id/CreateTime/CreateBy)` ⇒ 把**全部**其余列
                        //   按本对象的值写回；本对象是「先读后改」，若期间有并发写，其余列会被陈旧值覆盖。
                        //   显式列名 ⇒ 只写本次真正改动的 9 列，语义等价且并发安全。
                        //   ⚠️ 这 9 列与上方赋值一一对应；⛔ 加赋值必须同步加列名，否则「改了不落库」且不报错。
                        //   ⚠️ 定位键仍是 `Code`（BuildUpdateLocator 只认 Code，⛔ 无 Id）。
                        // ★ 写操作**必须检查返回值** —— ⛔ 不能丢弃 `Result`：
                        //   丢弃 = 「改了不落库」而接口仍报成功（正是上方长注释里那个坑的成因）。
                        //   抛出让外层 catch 归一为业务拒绝信封（`success:false` + `err` 非空）。
                        var upd = await _db.UpdateAsync(existing,
                            "ParamValue", "ValueSource", "IsManualEdited", "IsFilled",
                            "IsDeleted", "DeleteBy", "DeleteTime", "UpdateTime", "UpdateBy");
                        if (!upd.Success)
                            throw new InvalidOperationException(
                                upd.Error ?? $"保存参数「{def.ParamName}」失败");
                    }
                    else
                    {
                        var entity = new FillParamValue
                        {
                            Code = Guid.NewGuid().ToString("N"),
                            // ★ 值行 OrgCode = 工作区 Code（数据归属），⛔ 不是认证机构 Code
                            OrgCode = workspaceCode,
                            // ⚠️ ent.Code 声明为 string?（BaseEntity.Code），但它是业务键（DB NOT NULL）
                            //    且本行的企业就是按它查出来的 ⇒ `!` 收敛，⛔ 不改行为。
                            EnterpriseCode = ent.Code!,
                            EnterpriseName = ent.Name,
                            StandardCode = rowStd,
                            StageCode = rowStage,
                            ParamCode = def.ParamCode,
                            ParamName = def.ParamName,
                            GroupName = def.GroupName,
                            ValueType = def.ValueType,
                            EnumOptions = def.EnumOptions,
                            ParamValue = value,
                            ValueSource = "manual",
                            SourceRef = $"全局参数 · {def.ParamName}",
                            IsManualEdited = true,
                            MaintainMode = def.MaintainMode,
                            IsRequired = def.IsRequired,
                            IsFilled = !string.IsNullOrWhiteSpace(value),
                            Sort = def.Sort,
                            IsValid = 1,
                            IsDeleted = false,
                            CreateTime = now,
                            CreateBy = userCode,
                        };
                        // ★ 同上：写失败必须响亮失败，⛔ 不能静默（原实现丢弃了这里的 Fail）。
                        var ins = await _db.InsertAsync(entity);
                        if (!ins.Success)
                            throw new InvalidOperationException(
                                ins.Error ?? $"新增参数「{def.ParamName}」失败");
                    }

                    savedCount++;
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return Result<FillParamSaveResult>.Fail($"保存失败：{ex.Message}");
            }

            // ⚠️⚠️ 属性名**必须**小写开头（savedCount / ignoredAuto / message）——
            //     原实现是匿名对象 new { savedCount, ignoredAuto, message }，序列化后即 camelCase；
            //     前端按 res.savedCount 读。改成 PascalCase = 破坏契约。⛔ 不要「顺手规范命名」。
            return Result<FillParamSaveResult>.Ok(new FillParamSaveResult
            {
                savedCount = savedCount,
                ignoredAuto = ignoredAuto,
                message = ignoredAuto.Count == 0
                    ? $"已保存 {savedCount} 项"
                    : $"已保存 {savedCount} 项；{ignoredAuto.Count} 项由企业档案自动带出，未保存（如需修改请到「企业管理」）",
            });
        }

        // ════════════════════════════════════════════════════════════════════
        //  私有工具
        // ════════════════════════════════════════════════════════════════════

        // ⛔ 此处刻意**不**再放 Specificity / ValueKey / ResolveAuto ——
        //    它们已上提为 ParamValueResolver 的公开成员。取值口径只能有一份：
        //    「企业端显示的值」与「文档里填的值」必须永远一致，各写一份必然漂移且都不报错。

        /// <summary>企业档案 → 引擎快照（<c>ParamValueResolver</c> 的 <c>auto</c>/<c>both</c> 取值来源）</summary>
        private static EnterpriseInfo ToEnterpriseInfo(Enterprise e) => new()
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
