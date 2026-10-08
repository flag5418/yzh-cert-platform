using System.Text.Json;
using Microsoft.Extensions.Logging;
using CertPlatform.Admin.Services.DocExtraction;
using CertPlatform.Admin.Services.Workflow.Models;
using CertPlatform.Shared.Constants;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Admin.Entities.Dir;
using CertPlatform.Shared.Entities.Rpt;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Auditor.Services.Expert;

/// <summary>
/// ★ 补录清单生成器（<c>R \ H</c> 差集 · 2026-09-30 用户裁决 J1）
/// </summary>
///
/// <para><b>核心机制</b>（25 号 §二）：</para>
/// <code>
/// R（需求集）= ∪ { 本次任务内 itemCode 的 DAG 里所有 docfield / doctable 节点 }
/// H（已有集）= 企业在 (OrgCode, StandardCode, StageCode) 范围内、按 RuleCode 收窄后的有效值
/// G（缺口）  = R \ H
/// </code>
///
/// <para><b>★ R 必须按 <paramref name="itemCodes"/> 局部收集</b>（J1 的可用性前提）：
/// 局部 NC 检查（5 条规则）只应产出这 5 条引用的字段缺口；⛔ 禁止按标准全量收集规则
/// （40 条规则 ⇒ 清单膨胀到数百行，无法使用）。这正是"本次是局部 NC / 局部报告章节"的业务诉求。</para>
///
/// <para><b>★ H 的取值必须走 <see cref="ExtractionDataResolver"/></b>，⛔ 不另拼 WHERE。
/// 否则守卫（执行期）与本类（开启任务时）口径分叉 ⇒ 出现"清单说齐了、执行说缺了"
/// ⇒ 补录夹具失去信号。</para>
///
/// <para><b>★ 记录粒度是字段级</b>（J1）：1 条 <c>cert_expert_task_data_gap</c> = 1 个待补项。
/// "影响 N 条规则"⛔ 不落库，由前端按 DAG 实时反查（见 <see cref="ImpactMap"/>）——
/// 规则是业务配置、变动频繁，缺口是任务快照，落库 <c>SourceItemCode</c> 必然漂移。</para>
///
/// <para><b>★ 不分文档</b>（J1）：<c>StandardFileCode</c> / <c>ExpectedFileName</c> 只作只读提示列，
/// ⛔ 不作分组键、不作存储键。补录按 <c>(RuleCode, FieldCode|TableCode)</c> 定位，与文件无关。</para>
/// </remarks>
public class GapDetector
{
    private readonly IDbOrm _db;
    private readonly ExtractionDataResolver _resolver;
    private readonly GapLabelResolver _labels;
    private readonly ILogger<GapDetector> _logger;

    /// <summary>DAG 反序列化选项（与 <c>WorkflowConfigParser</c> 逐字一致，避免两处口径不同）</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public GapDetector(
        IDbOrm db,
        ExtractionDataResolver resolver,
        GapLabelResolver labels,
        ILogger<GapDetector> logger)
    {
        _db = db;
        _resolver = resolver;
        _labels = labels;
        _logger = logger;
    }

    /// <summary>★ 专家工作区（租户）编码 —— 由调用方（<c>ExpertTaskService</c>）注入。
    /// <para><c>cert_expert_task_data_gap.OrgCode</c> 是 NOT NULL 租户隔离键，
    /// 与 <c>cert_extraction_result.OrgCode</c>（存企业 Code）<b>不是同一个口径</b>，勿混。</para>
    /// </summary>
    public string OrgCode { get; set; } = "";

    // ── 内部模型 ──────────────────────────────────────────────

    /// <summary>一条数据依赖（来自 DAG 的一个取数节点）</summary>
    public sealed class DataDependency
    {
        /// <summary><c>field</c> | <c>table</c> | <c>unknown</c></summary>
        public string GapType { get; init; } = "";
        /// <summary>★ 规则 Code（J4：主键成分）</summary>
        public string RuleCode { get; init; } = "";
        public string? FieldCode { get; init; }
        public string? TableCode { get; init; }
        public string NodeId { get; init; } = "";
        /// <summary>依赖它的任务项（NC 检查项 / 报告章节）的 Code</summary>
        public string SourceItemCode { get; init; } = "";
        public string SourceItemName { get; init; } = "";
        public string SourceItemType { get; init; } = "";
        public string? ClauseCode { get; init; }
        /// <summary>规则声明的归属标准文件行 Code（<c>docType</c> 不是它！见 25 号 B5）</summary>
        public string? StandardFileCode { get; init; }
        public string? ExpectedFileName { get; init; }
    }

    /// <summary>生成结果</summary>
    public sealed class DetectResult
    {
        /// <summary>本次新增的缺口行（已 INSERT）</summary>
        public List<CertExpertTaskDataGap> Inserted { get; set; } = new();
        /// <summary>需求集大小（去重后）</summary>
        public int DemandCount { get; set; }
        /// <summary>其中已满足的（不在差集里）</summary>
        public int SatisfiedCount { get; set; }
        /// <summary>未知节点类型（登记为 gap 供人工确认，⛔ 不静默跳过）</summary>
        public List<DataDependency> UnknownNodes { get; set; } = new();
    }

    // ── ① 依赖收集 ────────────────────────────────────────────

    /// <summary>
    /// 反向解析规则/章节的工作流 DAG，抽出所有"取数节点"引用的字段/表格。
    /// </summary>
    /// <param name="itemCodes">★ 本次任务的检查项 / 章节 Code 列表（<b>局部</b>，不是全量规则）</param>
    /// <param name="itemType"><c>nc_check</c> | <c>report_section</c></param>
    public async Task<List<DataDependency>> CollectDependenciesAsync(
        IReadOnlyCollection<string> itemCodes, string itemType)
    {
        var deps = new List<DataDependency>();
        if (itemCodes == null || itemCodes.Count == 0) return deps;

        // ★ 两张表的 DAG 列名不同（实测 2026-09-30）
        var isNc = !string.Equals(itemType, "report_section", StringComparison.OrdinalIgnoreCase);

        // ★ 两类任务项分表取，且 DAG 列名不同（RuleJson vs WorkflowConfig）⇒ 先归一成一个形状
        var items = await LoadItemsAsync(itemCodes, isNc);

        foreach (var it in items)
        {
            if (string.IsNullOrWhiteSpace(it.DagJson)) continue;

            WorkflowConfig? dag;
            try
            {
                dag = JsonSerializer.Deserialize<WorkflowConfig>(it.DagJson, JsonOptions);
            }
            catch (JsonException ex)
            {
                // ⛔ 解析失败不静默跳过：登记为 unknown，交人工确认（否则"永远判不缺"）
                _logger.LogWarning(ex, "[GapDetector] DAG 解析失败 item={Item}（{Type}）", it.Code, itemType);
                deps.Add(new DataDependency
                {
                    GapType = "unknown",
                    RuleCode = it.Code,
                    NodeId = "",
                    SourceItemCode = it.Code,
                    SourceItemName = it.Name,
                    SourceItemType = itemType,
                    ClauseCode = it.ClauseCode
                });
                continue;
            }

            foreach (var node in dag?.Nodes ?? new List<WorkflowNodeConfig>())
            {
                var ruleCode = GetConfig(node, "ruleCode");

                // ★ 节点类型必须 ToLowerInvariant —— DB 实测是 "docField" 驼峰（缺陷 B4）
                switch (node.NodeType?.ToLowerInvariant())
                {
                    case "docfield":
                    {
                        var fieldCode = GetConfig(node, "fieldCode");
                        if (string.IsNullOrWhiteSpace(fieldCode)) break;
                        deps.Add(new DataDependency
                        {
                            GapType = "field",
                            RuleCode = ruleCode,
                            FieldCode = fieldCode,
                            NodeId = node.NodeId ?? "",
                            SourceItemCode = it.Code,
                            SourceItemName = it.Name,
                            SourceItemType = itemType,
                            ClauseCode = it.ClauseCode
                        });
                        break;
                    }

                    case "doctable":
                    {
                        var tableCode = GetConfig(node, "tableCode");
                        if (string.IsNullOrWhiteSpace(tableCode)) break;
                        deps.Add(new DataDependency
                        {
                            GapType = "table",
                            RuleCode = ruleCode,
                            TableCode = tableCode,
                            NodeId = node.NodeId ?? "",
                            SourceItemCode = it.Code,
                            SourceItemName = it.Name,
                            SourceItemType = itemType,
                            ClauseCode = it.ClauseCode
                        });
                        break;
                    }

                    // ★ 以下节点不产生数据依赖（依赖上游输出，不是直接取数）
                    case "start":
                    case "end":
                    case "branch":
                    case "skill":
                    case "ai_node":
                        break;

                    default:
                        // ★ 未知节点类型不静默跳过：若引擎将来新增取数节点，漏了就"永远判不缺"
                        _logger.LogWarning(
                            "[GapDetector] 未知工作流节点类型 {NodeType}（item={Item} node={NodeId}），完备性检查未识别其依赖",
                            node.NodeType, it.Code, node.NodeId);
                        deps.Add(new DataDependency
                        {
                            GapType = "unknown",
                            RuleCode = ruleCode,
                            NodeId = node.NodeId ?? "",
                            SourceItemCode = it.Code,
                            SourceItemName = it.Name,
                            SourceItemType = itemType,
                            ClauseCode = it.ClauseCode
                        });
                        break;
                }
            }
        }

        return deps;
    }

    /// <summary>归一后的任务项（NC 检查项 or 报告章节），屏蔽两表差异</summary>
    private sealed class ItemShape
    {
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public string? DagJson { get; init; }
        public string? ClauseCode { get; init; }
    }

    private async Task<List<ItemShape>> LoadItemsAsync(IReadOnlyCollection<string> itemCodes, bool isNc)
    {
        if (isNc)
        {
            // ⚠️ ValidationRule 只继承 BaseEntity（无 ISoftDelete / IIsValid）⇒ 启用判据是 IsActive。
            //    这违反铁律九（启用字段唯一 = IsValid），属独立已知问题（README P0-19），
            //    此处与 ExpertTaskService.ResolveScope 保持同口径，不在本次修。
            var rules = (await _db.GetListAsync<ValidationRule>(x =>
                itemCodes.Contains(x.Code!) && x.IsValid == 1)).Data ?? new List<ValidationRule>();

            return rules.Select(r => new ItemShape
            {
                Code = r.Code ?? "",
                Name = r.RuleName ?? "",
                DagJson = r.RuleJson,
                ClauseCode = r.ClauseCode
            }).ToList();
        }

        var sections = (await _db.GetListAsync<ReportSection>(x =>
            itemCodes.Contains(x.Code!) && x.IsValid == 1 && !x.IsDeleted)).Data
            ?? new List<ReportSection>();

        return sections.Select(s => new ItemShape
        {
            Code = s.Code ?? "",
            Name = s.SectionName ?? "",
            DagJson = s.WorkflowConfig,
            ClauseCode = s.ClauseCode
        }).ToList();
    }

    // ── ② 缺口判定 + 落库 ────────────────────────────────────

    /// <summary>
    /// 计算 <c>R \ H</c> 并把缺口写入 <c>cert_expert_task_data_gap</c>（幂等：已存在不重复插）。
    /// </summary>
    /// <param name="deps">由 <see cref="CollectDependenciesAsync"/> 收集的依赖</param>
    public async Task<DetectResult> DetectAndPersistAsync(
        List<DataDependency> deps,
        string taskCode, string subTaskCode,
        string enterpriseCode, string standardCode, string stageCode)
    {
        var result = new DetectResult();

        if (deps == null || deps.Count == 0) return result;

        var unknown = deps.Where(d => d.GapType == "unknown").ToList();
        result.UnknownNodes = unknown;

        // 需求集去重：★ 字段级粒度（J1）—— (GapType, RuleCode, FieldCode|TableCode)
        var demand = deps
            .Where(d => d.GapType is "field" or "table")
            .GroupBy(d => (d.GapType, d.RuleCode, Key: d.FieldCode ?? d.TableCode ?? ""))
            .Select(g => g.First())
            .ToList();

        result.DemandCount = demand.Count;
        if (demand.Count == 0)
        {
            // 仍然把 unknown 登记进去
            foreach (var u in unknown) result.Inserted.Add(BuildGap(u, taskCode, subTaskCode, enterpriseCode, standardCode, stageCode));
            if (result.Inserted.Count > 0) await InsertGapsAsync(result.Inserted, OrgCode);
            return result;
        }

        // ★ H：走 Resolver 唯一口径（RuleCode 收窄 + 人工优先 + IsBlankText 判空）
        var (fieldValues, tableValues) = await _resolver.GetByScopeAsync(
            enterpriseCode, demand.Select(d => d.RuleCode).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(),
            standardCode, stageCode);

        var pending = new List<CertExpertTaskDataGap>();
        var satisfied = 0;

        // ★★ 中文名批量解析（2026-10-07 用户裁决 · 「关键信息补录」）
        //   唯一权威来源 = 文档提取规则页（/business/doc-extraction-rule）定义的字段/表格
        //   （DocFieldDef / DocTableDef）。⛔ 不要用 cert_extraction_result.FieldName 当主来源：
        //   那一列只在「已存在提取结果行」时才有值，而缺口场景恰恰是结果行不存在
        //   ⇒ 必然拿到 null ⇒ 旧实现回退成英文码（F3 第一层根因）。
        var labelMap = await _labels.ResolveLabelsAsync(
            demand.Select(d => (d.GapType, (string?)d.RuleCode, d.FieldCode ?? d.TableCode)));

        foreach (var d in demand)
        {
            bool hasValue;
            // 结果行上残留的中文名（有结果行、但值为空时的次级来源）
            string? fromResult = null;

            if (d.GapType == GapLabelResolver.TypeField)
            {
                var key = ExtractionDataResolver.FieldKey(d.RuleCode, d.FieldCode);
                if (fieldValues.TryGetValue(key, out var fv)) { hasValue = fv.HasValue; fromResult = fv.FieldName; }
                else hasValue = false;
            }
            else
            {
                var key = ExtractionDataResolver.TableKey(d.RuleCode, d.TableCode);
                if (tableValues.TryGetValue(key, out var tv)) hasValue = tv.HasValue;
                else hasValue = false;
            }

            if (hasValue) { satisfied++; continue; }

            // ★ 中文名三级：规则定义 → 结果行残留名 → 人话兜底
            //   ⛔ 绝不回退英文码 —— 宁可说「未命名字段」，也不要甩 appendix_three_... 给审核员
            var dataCode = d.FieldCode ?? d.TableCode;
            var fallback = d.GapType == GapLabelResolver.TypeField
                ? GapLabelResolver.UnnamedField
                : GapLabelResolver.UnnamedTable;
            var label = labelMap.TryGetValue(GapLabelResolver.Key(d.GapType, d.RuleCode, dataCode), out var ln)
                        && !string.IsNullOrWhiteSpace(ln)
                ? ln
                : (string.IsNullOrWhiteSpace(fromResult) ? fallback : fromResult!);

            // ★ 补齐归属文件提示（规则 → StandardFileCode → 模板文件名）
            var (sfc, fileName) = await ResolveSourceFileAsync(d.RuleCode);
            pending.Add(BuildGap(d, taskCode, subTaskCode, enterpriseCode, standardCode, stageCode, label, sfc, fileName));
        }

        result.SatisfiedCount = satisfied;
        foreach (var u in unknown)
            pending.Add(BuildGap(u, taskCode, subTaskCode, enterpriseCode, standardCode, stageCode));

        if (pending.Count > 0)
        {
            var inserted = await InsertGapsAsync(pending, OrgCode);
            result.Inserted.AddRange(inserted);
        }

        return result;
    }

    // ── ③ 「影响 N 条规则」实时反查（⛔ 不落库） ────────────────

    /// <summary>
    /// 由依赖列表反查"每个待补项被哪些检查项/章节引用"，供前端展示"影响 N 条规则"。
    /// </summary>
    /// <param name="deps">同一批任务的依赖列表</param>
    /// <returns>分组键（<c>GapType|RuleCode|FieldCode|TableCode</c>）→ 引用它的任务项列表</returns>
    public static Dictionary<string, List<GapImpact>> BuildImpactMap(IEnumerable<DataDependency> deps)
    {
        var map = new Dictionary<string, List<GapImpact>>(StringComparer.Ordinal);

        foreach (var d in deps)
        {
            var key = ImpactKey(d.GapType, d.RuleCode, d.FieldCode, d.TableCode);
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<GapImpact>();
                map[key] = list;
            }
            // 同一任务项只记一次
            if (!list.Any(x => x.ItemCode == d.SourceItemCode))
                list.Add(new GapImpact
                {
                    ItemCode = d.SourceItemCode,
                    ItemName = d.SourceItemName,
                    ItemType = d.SourceItemType,
                    ClauseCode = d.ClauseCode
                });
        }

        return map;
    }

    /// <summary>影响反查的分组键</summary>
    public static string ImpactKey(string gapType, string ruleCode, string? fieldCode, string? tableCode)
        => $"{gapType}|{ruleCode}|{fieldCode ?? "-"}|{tableCode ?? "-"}";

    public sealed class GapImpact
    {
        public string ItemCode { get; init; } = "";
        public string ItemName { get; init; } = "";
        public string ItemType { get; init; } = "";
        public string? ClauseCode { get; init; }
    }

    // ── 内部工具 ──────────────────────────────────────────────

    private static string GetConfig(WorkflowNodeConfig? node, string key)
        => node?.Config?.GetValueOrDefault(key)?.ToString() ?? "";

    private CertExpertTaskDataGap BuildGap(
        DataDependency d,
        string taskCode, string subTaskCode,
        string enterpriseCode, string standardCode, string stageCode,
        string? label = null, string? standardFileCode = null, string? expectedFileName = null)
    {
        var isField = d.GapType == "field";
        var now = DateTime.Now;

        return new CertExpertTaskDataGap
        {
            Code = Guid.NewGuid().ToString("N"),
            OrgCode = "",   // 由 InsertGapsAsync 统一回填
            TaskCode = taskCode,
            SubTaskCode = subTaskCode,
            // ★ RuleCode：J4 的主键成分，也是 GapKey 的一部分
            RuleCode = string.IsNullOrWhiteSpace(d.RuleCode) ? null : d.RuleCode,
            EnterpriseCode = enterpriseCode,
            StandardCode = standardCode,
            StageCode = stageCode,
            GapType = d.GapType,
            // ★ 中文名兜底（2026-10-07 用户裁决）：⛔ 不回退英文码
            //   宁可显示「未命名字段」，也不要甩 appendix_three_program_file_list 给审核员
            GapLabel = !string.IsNullOrWhiteSpace(label) ? label!
                      : isField ? GapLabelResolver.UnnamedField : GapLabelResolver.UnnamedTable,
            FieldCode = isField ? d.FieldCode : null,
            TableCode = isField ? null : d.TableCode,
            // ★ 只读提示列（J1「不分文档」）：不作分组键、不作存储键
            StandardFileCode = standardFileCode,
            ExpectedFileName = expectedFileName,
            SourceItemType = d.SourceItemType,
            SourceItemCode = d.SourceItemCode,
            SourceItemName = d.SourceItemName,
            ClauseCode = d.ClauseCode,
            GapStatus = ExpertTaskConst.GapStatus.Pending,
            Sort = 0,
            Status = "active",
            CreateTime = now,
            UpdateTime = now,
            IsValid = 1,
            IsDeleted = false
        };
    }

    /// <summary>规则 → StandardFileCode → 模板文件名</summary>
    private async Task<(string? StandardFileCode, string? FileName)> ResolveSourceFileAsync(string ruleCode)
    {
        // ★ B5 修正：⛔ 不能用 config.docType（实测值 = "standard"，不是文件码）
        //   唯一真来源 = config.ruleCode → cert_doc_extraction_rule.StandardFileCode
        string? sfc = null;
        if (!string.IsNullOrWhiteSpace(ruleCode))
        {
            var rule = (await _db.GetOneAsync<DocExtractionRule>(x => x.Code == ruleCode)).Data;
            sfc = rule?.StandardFileCode;
        }
        if (string.IsNullOrWhiteSpace(sfc)) return (null, null);

        var tpl = (await _db.GetOneAsync<StandardDirectoryFile>(x =>
            x.Code == sfc && x.EnterpriseCode == YzhVirtualEnterprise.Code)).Data;
        return (sfc, tpl?.FileName);
    }

    /// <summary>
    /// 幂等插入：靠 <c>uk_gap(TaskCode, GapKey)</c> 去重，已存在的跳过。
    /// <para>★ 同一缺口被反复检测（重跑完备性检查）不应产生重复行。</para>
    /// </summary>
    private async Task<List<CertExpertTaskDataGap>> InsertGapsAsync(
        List<CertExpertTaskDataGap> gaps, string orgCode)
    {
        var inserted = new List<CertExpertTaskDataGap>();
        if (gaps.Count == 0) return inserted;

        foreach (var g in gaps)
        {
            g.OrgCode = orgCode;
            // GapKey 是 MySQL 生成列，⛔ 应用层不得赋值（会 ERROR 3105）。
            // 这里本地算一份只用于判重查询。
            var expectKey = $"{g.GapType}|{g.RuleCode ?? "-"}|{g.FieldCode ?? "-"}|{g.TableCode ?? "-"}";

            var existed = (await _db.GetListAsync<CertExpertTaskDataGap>(x =>
                x.TaskCode == g.TaskCode && x.GapKey == expectKey && !x.IsDeleted)).Data
                ?? new List<CertExpertTaskDataGap>();

            if (existed.Count > 0)
            {
                // 已存在 → 只补齐可能为空的提示字段，不改状态
                var hit = existed[0];
                if (string.IsNullOrEmpty(hit.StandardFileCode) && !string.IsNullOrEmpty(g.StandardFileCode))
                    hit.StandardFileCode = g.StandardFileCode;
                if (string.IsNullOrEmpty(hit.ExpectedFileName) && !string.IsNullOrEmpty(g.ExpectedFileName))
                    hit.ExpectedFileName = g.ExpectedFileName;
                if (string.IsNullOrEmpty(hit.RuleCode) && !string.IsNullOrEmpty(g.RuleCode))
                    hit.RuleCode = g.RuleCode;
                hit.UpdateTime = DateTime.Now;
                await _db.UpdateAsync(hit,
                    nameof(CertExpertTaskDataGap.StandardFileCode),
                    nameof(CertExpertTaskDataGap.ExpectedFileName),
                    nameof(CertExpertTaskDataGap.RuleCode),
                    nameof(CertExpertTaskDataGap.UpdateTime));
                continue;
            }

            var r = await _db.InsertAsync(g);
            if (r.Success) inserted.Add(g);
            else _logger.LogError("[GapDetector] 缺口插入失败 key={Key} err={Err}", expectKey, r.Error);
        }

        return inserted;
    }
}
