using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CertPlatform.Shared.Fill;
using CertPlatform.Shared.Office;
using CertPlatform.Shared.Services.Fill;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Ent
{
    /// <summary>
    /// 来源解析器 —— 负责解析 DocTemplateAnchor.SourceSpec 并执行取值逻辑。
    /// <para>遵循 22 号 §三 / §八 的取值来源模型。</para>
    ///
    /// <para><b>★★ 2026-10-06（S1-5）改造：本类不再是「取值的第二实现」</b></para>
    /// <para>此前 <c>TryResolveGlobalAsync</c> <b>手抄</b>了 <c>SrcGlobalParamSkill</c> 的
    /// 「查定义 + PickMostSpecific + Resolve + FillValueFactory」四步，
    /// 且代码自己在 <c>:72-74</c> 留着 <c>// TODO: 这里需要处理 saved_value</c> ——
    /// 「已知缺口、未修、不报错」。</para>
    /// <para>现在 <c>global</c> 取值<b>整体委托</b>
    /// <see cref="FillParamValueProvider.ResolveAsync"/>（与 Skill 链同一个类）
    /// ⇒ 两条链口径必然一致（消灭 D1）。</para>
    /// <para>⚠️ 本类只保留「<b>壳</b>」的职责：解析 <c>SourceSpec</c> JSON、
    /// 把 <c>SourceSpecEntry</c> 翻译成 provider 的入参、把结果包成 <c>(ok, value)</c>。
    /// ⛔ 本类<b>不再直接查 <c>cert_fill_param_def</c></b>。</para>
    ///
    /// <para><b>★ 关于企业已填值（<c>savedValue</c>）</b>：本方法<b>接收</b>它但<b>不负责取</b>它 ——
    /// 因为 <c>cert_fill_param_value</c> 的实体 <c>FillParamValue</c> 属<b>专家端独占</b>
    /// （<c>24-后端实体归属清单</c> §一，守卫 R17 会把「上移到 Shared」拦下），
    /// 而本类在 Admin 端、⛔ 看不见它（C2：Admin 不引 Auditor）。
    /// ⇒ <b>由能读该表的调用方（Auditor 侧编排器）预取后传入</b>。
    /// ⚠️ 当前唯一调用方 <c>EnterpriseDocNormalizationExecutor</c>（Admin，**待停用**）
    /// 无法提供 ⇒ 显式传 <c>null</c>；第 2 批 P1 的新编排器落在 Auditor，届时传入真实值。</para>
    /// </summary>
    public class SourceResolver
    {
        private readonly IDbOrm _db;
        private readonly FillParamValueProvider _paramValues;

        public SourceResolver(IDbOrm db, FillParamValueProvider paramValues)
        {
            _db = db;
            _paramValues = paramValues;
        }

        /// <summary>解析 SourceSpec JSON 字符串</summary>
        public SourceSpecModel? Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<SourceSpecModel>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     ★ <b>取参数定义</b> —— 供调用方做「<b>为什么取不到值</b>」的归因。
        ///
        ///     <para><b>为什么需要它</b>：写入器只会说「未提供值」，而「为什么没有」有四种
        ///     截然不同的原因（模板没配数据源 / 参数没在后台定义 / 参数定义了但企业没填 /
        ///     来源链全未命中），指向四个不同的修复动作。
        ///     ⛔ 不区分 = <b>指错方向</b>（`DocumentFillController:237-240` 原话：
        ///     「把人引到企业端去找一个根本不存在的参数 —— 指错方向比不报错更耗时」）。</para>
        ///
        ///     <para>⚠️ 本方法<b>只透传</b>，⛔ 不另写一套查询 —— 与取值走同一个内核
        ///     （<see cref="FillParamValueProvider.FindDefAsync"/> 是全项目唯一查询）。</para>
        /// </summary>
        public Task<FillParamDef?> FindDefAsync(
            string paramCode, string? standardCode, string? stageCode, string? orgCode)
            => _paramValues.FindDefAsync(paramCode, standardCode, stageCode, orgCode);

        /// <summary>
        ///     ★ <b>该参数编码是否「曾被定义、后被有意软删」</b> —— 归因链的<b>第二级判据</b>。
        ///
        ///     <para><b>为什么归因需要它</b>：<see cref="FindDefAsync"/> 返 <c>null</c> 时，
        ///     「从来没配过」与「配过但被裁决废弃了」<b>修复动作相反</b> ——
        ///     前者要<b>新增</b>，后者<b>绝不能新增</b>（要改配来源）。
        ///     ⛔ 不区分就会把用户引向「把刚删掉的参数又加回来」。</para>
        ///
        ///     <para>真实案例见 <see cref="FillParamValueProvider.WasSoftDeletedAsync"/> 的注释
        ///     （2026-10-06 有意软删的 9 条档案镜像参数）。</para>
        ///
        ///     <para>⚠️ 本方法<b>只透传</b>，⛔ 不另写一套查询。</para>
        /// </summary>
        public Task<bool> WasSoftDeletedAsync(string paramCode)
            => _paramValues.WasSoftDeletedAsync(paramCode);

        /// <summary>
        /// 尝试从 global 来源取值 —— ★ 委托 <see cref="FillParamValueProvider.ResolveAsync"/>。
        /// </summary>
        /// <param name="entry">来源条目（<c>Kind</c> 必须是 <c>global</c>，<c>Ref</c> = <c>param_code</c>）</param>
        /// <param name="anchor">目标锚点（提供 <c>FieldCode</c>/<c>AnchorRef</c>/<c>ValueType</c>/<c>NumberFormat</c>）</param>
        /// <param name="entInfo">企业档案快照（由调用方一次性加载，避免逐锚点查库）</param>
        /// <param name="standardCode">标准编码</param>
        /// <param name="stageCode">阶段编码</param>
        /// <param name="orgCode">机构编码</param>
        /// <param name="enterpriseCode">企业编码（仅作上下文留痕；本方法不据此查库）</param>
        /// <param name="savedValue">
        /// ★ 企业已填值 —— <b>由调用方预取传入</b>（见类注释：本端看不见
        /// <c>cert_fill_param_value</c> 的实体）。传 <c>null</c> 等价于「该企业没有已填值」。
        /// </param>
        /// <param name="savedValueSource">已填值的来源：<c>auto</c>/<c>manual</c>/<c>ai</c>/<c>import</c></param>
        /// <param name="savedIsManualEdited">企业是否人工改过（=true 时不再被自动映射覆盖）</param>
        public async Task<(bool ok, FillValue? value)> TryResolveGlobalAsync(
            SourceSpecEntry entry,
            DocTemplateAnchor anchor,
            CertPlatform.Shared.Fill.EnterpriseInfo entInfo,
            string? standardCode,
            string? stageCode,
            string? orgCode,
            string? enterpriseCode = null,
            string? savedValue = null,
            string? savedValueSource = null,
            bool savedIsManualEdited = false)
        {
            if (entry.Kind != "global" || string.IsNullOrEmpty(entry.Ref))
                return (false, null);

            var outcome = await _paramValues.ResolveAsync(
                paramCode: entry.Ref!,
                anchorCode: anchor.FieldCode ?? anchor.AnchorRef,
                enterprise: entInfo,
                standardCode: standardCode,
                stageCode: stageCode,
                orgCode: orgCode,
                savedValue: savedValue,
                savedValueSource: savedValueSource,
                savedIsManualEdited: savedIsManualEdited,
                valueKind: anchor.ValueType,
                numberFormat: anchor.NumberFormat);

            return outcome.Ok && outcome.Value != null ? (true, outcome.Value) : (false, null);
        }

        /// <summary>
        /// 尝试从 manual 来源取值（从历史裁决或人工录入库中取）
        /// </summary>
        public async Task<(bool ok, FillValue? value)> TryResolveManualAsync(
            SourceSpecEntry entry,
            DocTemplateAnchor anchor,
            string enterpriseCode)
        {
            if (entry.Kind != "manual") return (false, null);

            // 1. 优先从 AI 建议裁决表中找（人工选定或改写的值）
            var suggestion = (await _db.GetOneAsync<DocAiSuggestion>(x => 
                x.EnterpriseCode == enterpriseCode && 
                x.AnchorCode == anchor.AnchorRef && 
                x.IsSelected == true &&
                x.IsValid == 1)).Data;

            if (suggestion != null)
            {
                var valStr = string.IsNullOrEmpty(suggestion.ManualValue) ? suggestion.SuggestedValue : suggestion.ManualValue;
                if (!string.IsNullOrEmpty(valStr))
                {
                    var (createOk, val, _) = FillValueFactory.TryCreate(
                        anchor.FieldCode ?? anchor.AnchorRef,
                        valStr,
                        anchor.ValueType,
                        anchor.NumberFormat);

                    if (createOk && val != null)
                    {
                        val.Source = "manual_suggestion";
                        val.Confidence = 1.0;
                        return (true, val);
                    }
                }
            }

            // 2. ⛔ 2026-10-06 删除的死代码：
            //    原先这里还有一段「备选：从全局参数定义表里找」——
            //    它查了 FillParamDef 却把结果赋给一个**从未被使用的局部变量**
            //    （`var paramValue = ...` 后再无引用），注释还自认「这里逻辑待完善」。
            //    留着它比删掉更危险：读代码的人会以为「manual 已经查过参数表了」。
            //    ★ 真正的「参数表 → 值」通道只有一条 = FillParamValueProvider。
            return (false, null);
        }
    }

    public class SourceSpecModel
    {
        public string Combine { get; set; } = "firstHit";
        public string? Separator { get; set; }
        public string? Expr { get; set; }
        public List<SourceSpecEntry> Sources { get; set; } = new();
    }

    /// <summary>
    ///     来源条目（<c>SourceSpec.sources[i]</c>）。
    ///
    ///     <para><b>★ 2026-10-09：模型收敛为「一个锚点 = 一个来源」</b> ——
    ///     <c>Sources</c> 数组**长度恒为 1**（编排器仍按数组遍历，保持向后兼容）。
    ///     用户裁定删除「组合方式」（<c>combine</c>）与「企业资料画像」（<c>profile</c>）。</para>
    ///
    ///     <para><b>★ 前端已不再写</b> <c>Combine</c> / <c>Separator</c> / <c>Expr</c> /
    ///     <c>OnMissing</c> —— 这几项都有默认值，⛔ 删字段会破坏老数据反序列化，故保留。</para>
    /// </summary>
    public class SourceSpecEntry
    {
        /// <summary><c>global</c> / <c>ai_semantic</c> / <c>ai_field</c> / <c>ai_table</c> / <c>manual</c>（旧值 <c>ai</c> / <c>profile</c> 仍可解析）</summary>
        public string Kind { get; set; } = string.Empty;
        /// <summary><c>global</c> 专用 = 参数编码；<c>ai_*</c> 且 <see cref="HasParam"/>=true 时同义</summary>
        public string? Ref { get; set; }
        public string? Field { get; set; }
        public double? MinConfidence { get; set; }
        public string? PromptGroup { get; set; }
        public string OnMissing { get; set; } = "next";

        // ── ★ 2026-10-09 新增：AI 节点的固定 3 属性（前端 ② 段）──
        //    ⛔ 不加这三个字段的话，前端写进 JSON 的值会被**静默丢弃**（反序列化忽略未知属性）。

        /// <summary>★ <c>ai_*</c> 专用：① 有无参数（有 ⇒ <see cref="Ref"/> 就是全局参数编码）</summary>
        public bool HasParam { get; set; }

        /// <summary>★ <c>ai_*</c> 专用：② 是否依赖企业资料（不依赖 ⇒ 只靠「参数 + 提示词」生成）</summary>
        public bool DependsOnEnterprise { get; set; }

        /// <summary>★ <c>ai_*</c> 专用：③ 提示词（其中可用 <c>{{参数}}</c> 引用 <see cref="Params"/> 里的全局参数）</summary>
        public string? Prompt { get; set; }

        /// <summary>
        ///     ★ <c>ai_*</c> 专用：④ <b>引用的全局参数（多选）</b> —— 「带参数」的落地。
        ///
        ///     <para>依据：<c>48</c> §2.2 ①（三类 AI 共用的「是否带参数」+ 多选全局参数）·
        ///     <c>61</c> S-1（「勾选后提示词 <c>{{参数}}</c> 引用」）· 原型 V4。
        ///     用户在 AI 节点的提示词里写 <c>{{参数编码}}</c>，运行期由
        ///     <c>AiFillPromptBuilder.RenderParamRefs</c> 替换成真实取值。</para>
        ///
        ///     <para>⚠️ <b>兼容单参数老写法</b>：<c>Params</c> 为空但 <see cref="HasParam"/>=true 时，
        ///     回退到 <see cref="Ref"/>（2026-10-09 之前 UI 只支持单选一个参数）。⛔ 不要删 <see cref="Ref"/>：
        ///     库里可能已有该形态的行，删字段会让它们反序列化后静默丢参数。</para>
        /// </summary>
        public List<string> Params { get; set; } = new();

        /// <summary>
        ///     生效的<b>参数引用列表</b>（去空、去重保序）—— <b>唯一判据</b>。
        ///     <para>新写法 <see cref="Params"/> 优先；为空时回退单值 <see cref="Ref"/>（见 <see cref="Params"/> 注释）。</para>
        /// </summary>
        public IReadOnlyList<string> AiParamRefs()
        {
            var list = new List<string>();
            if (Params != null)
            {
                foreach (var p in Params)
                {
                    var t = (p ?? string.Empty).Trim();
                    if (t.Length > 0 && !list.Contains(t)) list.Add(t);
                }
            }

            if (list.Count == 0 && HasParam && !string.IsNullOrWhiteSpace(Ref))
                list.Add(Ref!.Trim());

            return list;
        }
    }
}
