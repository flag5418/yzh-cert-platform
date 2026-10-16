
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SqlSugar;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Extensions;
using YZH.Core.Stand.Interfaces;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Admin.Entities.Doc;
using CertPlatform.Shared.Domain.Scope;
using CertPlatform.Shared.Services.DocExtraction;

namespace CertPlatform.Admin.Services.DocExtraction;

/// <summary>
/// 提取规则四元组作用域（S0，10 号 §三）：规则键 + 机构 + 标准 + 阶段。
/// <para><c>RuleKey</c> = 规则键（模板文件行 Code / FR 模板 Code）；<c>OrgCode</c> = 模板所属机构。</para>
/// </summary>

/// <summary>
/// 文档提取规则核心服务（业务层 #17 移植，partial：主体 + AI 编排）
/// <para>对照旧 DocExtractionRuleService.cs（1,094 行）业务编排完整保留，ORM 改写为 IDbOrm</para>
/// <para>规则键：StandardFileCode = 实际文件 FileCode（FL-xxx）或模板 Code（FR-xxx）</para>
/// </summary>
public partial class DocExtractionRuleService : IExtractionRuleQuery
{
    private readonly IDbOrm _db;
    /// <summary>核心资源检索唯一入口（铁律 L5）—— 标准目录文件按 Code 单查只走它。</summary>
    private readonly ICoreResourceQuery _core;
    private readonly ILogger<DocExtractionRuleService> _logger;
    protected readonly IConfiguration _configuration;
    protected readonly CertPlatform.Shared.DocExtraction.DocumentConvertClient _convertClient;
    /// <summary>
    /// ★ 转换内核（2026-10-09 接入）：本页的**实时兜底转换**必须走它 ——
    /// 内部含 anydoc 退出码 3 的**视觉 OCR 兜底**（图片直接识别 / 扫描件 PDF 逐页渲染后识别）。
    /// <para>⛔ 为什么不能继续直接用 <c>DocumentConvertClient</c>：后者只有一条 anydoc 命令，
    /// 图片与扫描件一律被 <c>ClassifyError</c> 判成 <c>NeedsOcr</c> ⇒ 本页把它们落 <c>unsupported</c>
    /// ⇒ **它们在「文档提取规则」页永远无法参与规则定义**。而标准目录上传链
    /// （<c>OfficeConvertService</c>）走的是 <see cref="CertPlatform.Shared.DocExtraction.IFileConvertCore"/>，
    /// 同一种文件上传时能 OCR、进本页却不能 —— 两条路径行为不一致（用户 2026-10-09 裁决：统一）。</para>
    /// </summary>
    protected readonly CertPlatform.Shared.DocExtraction.IFileConvertCore _convertCore;
    protected readonly CertPlatform.Shared.DocExtraction.LlmInvokeService _llm;
    protected readonly IObjectStorage _storage;
    /// <summary>S2③ 提取结果版本店（归档/回活/读取唯一口）</summary>
    private readonly EnterpriseExtractionResultStore _resultStore;

    // ★ 2026-10-13 归属模型 V2：本类原 `YzhStandardEnterpriseCode` 别名、旧 `YzhVirtualEnterprise`
    //   常量、以及 `CoreOrgCodes.StdEnv`（`yzh-std-env`）**均已删除** ——
    //   平台标准级 = **两列都空**（`OrgCode` 空 且 `EnterpriseCode` 空），⛔ 不再用任何常量标记。
    //   ⛔ 不要再在本类内重建别名 / 常量：别名 = 第二个字面量出口 = 下次改值必漏一处。

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public DocExtractionRuleService(
        IDbOrm db,
        IObjectStorage storage,
        CertPlatform.Shared.DocExtraction.DocumentConvertClient convertClient,
        CertPlatform.Shared.DocExtraction.IFileConvertCore convertCore,
        CertPlatform.Shared.DocExtraction.LlmInvokeService llm,
        IConfiguration configuration,
        ILogger<DocExtractionRuleService> logger,
        EnterpriseExtractionResultStore resultStore,
        ICoreResourceQuery core)
    {
        _resultStore = resultStore;
        _db = db;
        _core = core;
        _storage = storage;
        _convertClient = convertClient;
        _convertCore = convertCore;
        _llm = llm;
        _configuration = configuration;
        _logger = logger;
    }

    // ========================================================
    // 技能定义（对照旧 _skills 静态列表）
    // ========================================================

    private static readonly List<SkillInfo> Skills = new()
    {
        new SkillInfo
        {
            Code = "word",
            Name = "Word文档提取",
            Description = "提取Word文档内容",
            SupportedExtensions = new List<string> { ".docx", ".doc" }
        },
        new SkillInfo
        {
            Code = "excel",
            Name = "Excel表格提取",
            Description = "提取Excel表格数据",
            SupportedExtensions = new List<string> { ".xlsx", ".xls", ".csv" }
        },
        new SkillInfo
        {
            Code = "pdf",
            Name = "PDF文档提取",
            Description = "提取PDF文档文本内容",
            SupportedExtensions = new List<string> { ".pdf" }
        }
    };

    public List<SkillInfo> GetSkills() => Skills;

    /// <summary>按文件扩展名权威推导技能类型（单一约束原则：不依赖前端传入）</summary>
    public static string ResolveSkill(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "word";
        var ext = Path.GetExtension(fileName)?.TrimStart('.').ToLowerInvariant() ?? "";
        return ext switch
        {
            "docx" or "doc" => "word",
            "xlsx" or "xls" or "csv" => "excel",
            "pdf" => "pdf",
            _ => "word"
        };
    }

    // ========================================================
    // 保存（对照旧 SaveExtractionRuleAsync L353-518：单事务）
    // ========================================================

    /// <summary>
    /// 保存提取规则（单事务：规则 + 字段定义 + 表格定义 + 同步提取值）
    /// </summary>
    public async Task<(bool Success, string Message)> SaveExtractionRuleAsync(SaveExtractionRuleRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.FileCode))
            return (false, "文件编码不能为空");

        // 1. 四元组作用域（S0，10 号 §三）：规则键 + 机构 + 标准 + 阶段。
        //    页面只传 fileCode，机构/标准/阶段一律后端权威推导（同 Skill 单一约束原则）。
        var scope = await ResolveRuleScopeAsync(request.FileCode);
        // ★ 2026-10-13 归属模型 V2：scope.OrgCode = 机构 Code（空 = 平台标准级 ⇒ 推导不到机构）
        if (!OrgScope.IsOrgRow(scope.OrgCode))
        {
            // 后端推导不到时才用页面显式值兜底；两者都空 ⇒ 无法按机构追溯，拒绝保存
            var fallbackOrg = (request.OrgCode ?? "").Trim();
            if (string.IsNullOrWhiteSpace(fallbackOrg))
                return (false, "无法确定规则所属机构（文件所属目录配置缺少 OrgCode）");
            scope = scope with { OrgCode = fallbackOrg };
        }
        var orgCode = scope.OrgCode;
        var standardCode = string.IsNullOrWhiteSpace(request.StandardCode) ? scope.StandardCode : request.StandardCode;
        var stageCode = string.IsNullOrWhiteSpace(request.StageCode) ? scope.StageCode : request.StageCode;

        // 1.1 查找规则（准则 A：存在性 = 四元组，不靠 Id 分流）
        var rule = await GetRuleByScopeAsync(orgCode, standardCode, stageCode, request.FileCode);
        var isNew = rule == null;

        if (isNew)
        {
            rule = new DocExtractionRule
            {
                Code = Guid.NewGuid().ToString(),
                OrgCode = orgCode,
                StandardFileCode = request.FileCode,
                StandardCode = standardCode,
                StageCode = stageCode,
                CreateTime = DateTime.Now
            };
        }
        else
        {
            // 非空才覆盖：推导失败时保留原值，避免把已有关联清空
            rule.OrgCode = orgCode;
            if (!string.IsNullOrWhiteSpace(standardCode)) rule.StandardCode = standardCode;
            if (!string.IsNullOrWhiteSpace(stageCode)) rule.StageCode = stageCode;
        }

        // P3：保存前快照（规则变更 → 自动标记待重提取的 diff 依据；isNew 无旧快照）
        var before = isNew ? null : await SnapshotRuleAsync(rule);

        // 2. 更新规则信息（技能类型后端权威推导）
        rule.Skill = ResolveSkill(request.FileCode);
        rule.Prompt = request.Prompt;
        rule.DocIsValid = request.IsValid;
        rule.Status = request.IsValid ? "configured" : "failed";
        rule.UpdateTime = DateTime.Now;

        // ★ 2026-10-14 D1（根因修复）：补落 sample_data —— 工作流 test-field / test-table 的唯一数据源。
        //   为什么以前不落：sample_data 全仓只有 VerifyPromptAsync 一处写入，且被 `if (rule != null)` 包住；
        //   而页面流程是「先验证 → 后保存」⇒ 首次配置时规则行还不存在 ⇒ 永远不落库
        //   ⇒ NC/报告设计器里点「测试提取」恒报「该规则尚未执行验证」。
        //   而前端 saveRule **早已**把 extractionData 传上来（SaveExtractionRuleRequest.ExtractionData
        //   也早已存在）—— 属典型「配了不生效」。此处与验证路径共用 BuildSampleDataJson，口径同源。
        //   ⛔ 无内容时**保留原值**，不清空：改字段 / 改提示词后未重新验证的场景下清空 = 静默回退。
        if (request.ExtractionData != null)
        {
            var sampleJson = BuildSampleDataJson(request.ExtractionData);
            if (sampleJson != null)
            {
                rule.SampleData = sampleJson;
                rule.VerifyMessage = "验证成功";
            }
        }

        using var tx = _db.BeginTransaction();
        try
        {
            if (isNew)
            {
                var add = await _db.InsertAsync(rule);
                if (!add.Success) { tx.Rollback(); return (false, add.Error ?? "创建规则失败"); }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(rule.Code)) { tx.Rollback(); return (false, "更新失败：缺少业务键 Code"); }
                var upd = await _db.UpdateAsync(rule);
                if (!upd.Success) { tx.Rollback(); return (false, upd.Error ?? "更新规则失败"); }
            }

            // 3. 删除旧字段定义（rule_code 关联），再写入新定义
            var oldFields = (await _db.GetListAsync<DocFieldDef>(x => x.RuleCode == rule.Code)).Data ?? new();
            foreach (var f in oldFields)
                await _db.DeleteByCodeAsync<DocFieldDef>(f.Code);

            if (request.Fields?.Any() == true)
            {
                var sortOrder = 0;
                foreach (var fieldDto in request.Fields)
                {
                    var field = new DocFieldDef
                    {
                        Code = Guid.NewGuid().ToString("N"),
                        RuleCode = rule.Code,
                        FieldName = fieldDto.Name,
                        FieldCode = string.IsNullOrEmpty(fieldDto.Code) ? ToCamel(fieldDto.Name) : fieldDto.Code,
                        DataType = fieldDto.DataType,
                        Description = fieldDto.Description,
                        IsManual = fieldDto.IsManual,
                        IsAiRecommended = fieldDto.IsAiRecommended,
                        Sort = sortOrder++,
                        CreateTime = DateTime.Now
                    };
                    var r = await _db.InsertAsync(field);
                    if (!r.Success) { tx.Rollback(); return (false, r.Error ?? "保存字段定义失败"); }
                }
            }

            // 4. 删除旧表格定义 + 表格字段定义，再写入新定义
            var oldTables = (await _db.GetListAsync<DocTableDef>(x => x.RuleCode == rule.Code)).Data ?? new();
            foreach (var t in oldTables)
            {
                var oldCols = (await _db.GetListAsync<DocTableFieldDef>(x => x.TableCode == t.Code)).Data ?? new();
                foreach (var c in oldCols)
                    await _db.DeleteByCodeAsync<DocTableFieldDef>(c.Code);
                await _db.DeleteByCodeAsync<DocTableDef>(t.Code);
            }

            if (request.Tables?.Any() == true)
            {
                var tableSortOrder = 0;
                foreach (var tableDto in request.Tables)
                {
                    // 表格定义 Code 为 GUID；TableCode 同时作为表格字段定义外键（对照旧逻辑）
                    var tableCode = Guid.NewGuid().ToString("N");
                    var table = new DocTableDef
                    {
                        Code = tableCode,
                        RuleCode = rule.Code,
                        TableName = tableDto.Name,
                        TableCode = string.IsNullOrEmpty(tableDto.Code) ? ToCamel(tableDto.Name) : tableDto.Code,
                        Description = tableDto.Description,
                        Sort = tableSortOrder++,
                        CreateTime = DateTime.Now
                    };
                    var rt = await _db.InsertAsync(table);
                    if (!rt.Success) { tx.Rollback(); return (false, rt.Error ?? "保存表格定义失败"); }

                    if (tableDto.Columns?.Any() == true)
                    {
                        var colSortOrder = 0;
                        foreach (var colDto in tableDto.Columns)
                        {
                    var col = new DocTableFieldDef
                    {
                        Code = Guid.NewGuid().ToString("N"),
                        TableCode = tableCode,
                        ColumnName = colDto.Name,
                                ColumnCode = string.IsNullOrEmpty(colDto.Code) ? ToCamel(colDto.Name) : colDto.Code,
                                DataType = colDto.DataType,
                                Sort = colSortOrder++,
                                CreateTime = DateTime.Now
                            };
                            var rc = await _db.InsertAsync(col);
                            if (!rc.Success) { tx.Rollback(); return (false, rc.Error ?? "保存表格字段失败"); }
                        }
                    }
                }
            }

            // 5. 同步提取结果到 B-08/B-09（标准域 = ''），供工作流验证
            await SyncExtractionResultToB08B09Async(rule, request);

            tx.Commit();

            // P3（2026-09-29 用户裁决）：规则内容实质变更 ⇒ 自动标记相关企业文件「待重新提取」。
            //   实质变更 = 字段/表格定义变 OR Prompt 变 OR 可用性由可用转停用；不自动入队（改完由用户决定何时提取）。
            //   新增规则（isNew）不标：原本就无规则、无结果可言；回执附影响面供管理员感知。
            var marked = 0;
            if (before != null)
            {
                var after = await SnapshotRuleAsync(rule);   // 事务已提交：读到的是新定义
                var contentChanged = before.DefFingerprint != after.DefFingerprint || before.Prompt != after.Prompt;
                var usabilityLost = IsUsableStatus(before.Status) && !IsUsableStatus(after.Status);
                if (contentChanged || usabilityLost)
                    marked = await MarkFilesStaleAsync(rule, "提取规则已更新，待重新提取");
            }

            _logger.LogInformation("[DocExtractionRule] 保存成功: {FileCode}, fields={F}, tables={T}, marked={M}",
                request.FileCode, request.Fields?.Count ?? 0, request.Tables?.Count ?? 0, marked);
            return (true, marked > 0 ? $"保存成功，已标记 {marked} 个文档待重新提取" : "保存成功");
        }
        catch (Exception ex)
        {
            tx.Rollback();
            _logger.LogError(ex, "[DocExtractionRule] 保存失败: {FileCode}", request.FileCode);
            return (false, $"保存失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 将规则保存请求中的提取数据同步到 B-08/B-09（G-2a：企业域参数化）。
    /// <para>★ 2026-10-13 归属模型 V2（收口 Task #33）：默认 <paramref name="enterpriseCode"/> = <c>''</c>
    /// = <b>平台标准级样例域</b>（非企业行）—— 与读侧 <c>string.IsNullOrEmpty(x.EnterpriseCode)</c> 一致。</para>
    /// <para>规则对照旧 SyncExtractionResultToB08B09Async（L528-612）：</para>
    /// <para>1. extractionData 为空 → 跳过；2. 物理删除旧结果（绕过软删拦截 + 唯一约束兜底）；</para>
    /// <para>3. 一致性过滤（只写定义中存在的 code）；4. 空值/空表格不写入</para>
    /// </summary>
    private async Task SyncExtractionResultToB08B09Async(DocExtractionRule rule, SaveExtractionRuleRequest request,
        string enterpriseCode = "")
    {
        var extractionData = request.ExtractionData;
        if (extractionData == null) return;

        var now = DateTime.Now;
        var fileCode = rule.StandardFileCode ?? "";

        // 已保存定义中的合法 code 集合 + fieldCode→中文名映射
        var fieldNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var validFieldCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in request.Fields ?? new())
        {
            var code = string.IsNullOrEmpty(f.Code) ? ToCamel(f.Name) : f.Code;
            if (string.IsNullOrEmpty(code)) continue;
            validFieldCodes.Add(code);
            fieldNameMap[code] = f.Name ?? code;
        }
        var validTableCodes = (request.Tables ?? new())
            .Select(t => string.IsNullOrEmpty(t.Code) ? ToCamel(t.Name) : t.Code)
            .Where(c => !string.IsNullOrEmpty(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1. 物理删除旧结果（对照旧注释：软删残留会与唯一约束冲突，必须原生 SQL）
        await _db.Client.Deleteable<ExtractionResult>()
            .Where(x => x.EnterpriseCode == enterpriseCode && x.StandardFileCode == fileCode)
            .ExecuteCommandAsync();
        await _db.Client.Deleteable<TableExtractionResult>()
            .Where(x => x.EnterpriseCode == enterpriseCode && x.StandardFileCode == fileCode)
            .ExecuteCommandAsync();

        // 2. 字段级 → B-08（LabelTag = field_code，对照 V4 评审报告 §7）
        if (extractionData.Fields != null)
        {
            foreach (var kv in extractionData.Fields)
            {
                if (!validFieldCodes.Contains(kv.Key)) continue;
                var value = kv.Value?.ToString();
                if (string.IsNullOrWhiteSpace(value)) continue;

                var er = new ExtractionResult
                {
                    Code = Guid.NewGuid().ToString("N"),
                    EnterpriseCode = enterpriseCode,
                    StandardFileCode = fileCode,
                    StandardCode = rule.StandardCode,
                    StageCode = rule.StageCode,
                    FileCode = fileCode,
                    VersionNumber = 1,
                    RuleCode = rule.Code,
                    FieldCode = kv.Key,
                    FieldName = fieldNameMap.TryGetValue(kv.Key, out var fn) ? fn : kv.Key,
                    LabelTag = kv.Key,
                    ExtractedValue = value,
                    ExtractedAt = now,
                    CreateTime = DateTime.Now
                };
                await _db.Client.Insertable(er).ExecuteCommandAsync();
            }
        }

        // 3. 表格级 → B-09（每表格一条，extracted_json = 行数组 JSON）
        if (extractionData.Tables != null)
        {
            var tableIndex = 1;
            foreach (var kv in extractionData.Tables)
            {
                if (!validTableCodes.Contains(kv.Key)) continue;
                var rows = kv.Value;
                if (rows == null || rows.Count == 0) continue;

                var tr = new TableExtractionResult
                {
                    Code = Guid.NewGuid().ToString("N"),
                    EnterpriseCode = enterpriseCode,
                    StandardFileCode = fileCode,
                    StandardCode = rule.StandardCode,
                    StageCode = rule.StageCode,
                    FileCode = fileCode,
                    VersionNumber = 1,
                    RuleCode = rule.Code,
                    TableIndex = tableIndex++,
                    ExtractedJson = System.Text.Json.JsonSerializer.Serialize(rows, JsonOptions),
                    ExtractedAt = now,
                    CreateTime = DateTime.Now
                };
                await _db.Client.Insertable(tr).ExecuteCommandAsync();
            }
        }
    }

    /// <summary>
    /// ★ 2026-10-14：<c>sample_data</c> 的**唯一构造口** —— 验证路径与保存路径同源。
    ///
    /// <para><b>为什么必须抽出来（实测缺陷）</b>：<c>sample_data</c> 是工作流
    /// <c>test-field</c> / <c>test-table</c> 配置期试运行的**唯一数据源**。
    /// 此前全仓只有一处写它（<c>VerifyPromptAsync</c>），且被 <c>if (rule != null)</c> 包住；
    /// 而页面流程是「<b>先验证 → 后保存</b>」（<c>doc-extraction-rule/index.vue</c> 的
    /// <c>onVerifyPrompt</c> → <c>onSave</c>）⇒ 首次配置某文件时规则行<b>尚不存在</b>
    /// ⇒ 该分支进不去 ⇒ <c>sample_data</c> <b>永远不落库</b>
    /// ⇒ 工作流节点测试恒报「该规则尚未执行验证」。
    /// <para>实测证据：<c>cert_extraction_result</c>（B-08 标准域）已有 4 行真实值，
    /// 而 <c>cert_doc_extraction_rule.SampleData</c> 长度 0 —— 数据进了 B-08，没进 sample_data。</para></para>
    ///
    /// <para><b>口径</b>：字段 + 表格**合并**进同一个对象（键 = <c>field_code</c> / <c>table_code</c>），
    /// 与 <c>TestFieldAsync</c> / <c>TestTableAsync</c> 的读取口径一一对应；空键 / <c>null</c> 值不写入。</para>
    ///
    /// <para>⚠️ 与改造前的唯一行为差异：**全部值为空**时旧代码仍会写 <c>"{}"</c> 并把
    /// <c>DocIsValid</c> 置 true（「零数据却声称已验证」）；本方法改为返回 <c>null</c> ⇒ 调用方不写，
    /// 与旧代码里 <c>Status = sampleDict.Count &gt; 0 ? "configured" : rule.Status</c> 的判断同向。</para>
    /// </summary>
    /// <returns>无有效内容时返回 <c>null</c>（调用方据此决定「不覆盖原值」）。</returns>
    private static string? BuildSampleDataJson(ExtractionData? extraction)
    {
        if (extraction == null) return null;

        var sampleDict = new Dictionary<string, object>();
        if (extraction.Fields != null)
            foreach (var kv in extraction.Fields)
                if (!string.IsNullOrEmpty(kv.Key) && kv.Value != null)
                    sampleDict[kv.Key] = kv.Value;
        if (extraction.Tables != null)
            foreach (var kv in extraction.Tables)
                if (!string.IsNullOrEmpty(kv.Key) && kv.Value != null)
                    sampleDict[kv.Key] = kv.Value;

        return sampleDict.Count == 0 ? null : JsonSerializer.Serialize(sampleDict, JsonOptions);
    }

    /// <summary>
    /// 由文件行推导提取规则四元组（S0 唯一作用域入口，10 号 §三）。
    /// <para>规则键 <c>RuleKey</c>：企业槽位行取 <c>StandardFileCode</c>（模板文档 Code），模板行取自身 Code。</para>
    /// <para>标准/阶段以**模板文件行为准**（规则在模板域保存，企业槽位行同源于模板配置）。</para>
    /// <para>机构 <c>OrgCode</c> = 模板文件行 ConfigCode → <c>cert_standard_directory_config.OrgCode</c>
    /// （子表经 ConfigCode 间接归属，与 config 表「子表不加机构列」约定一致）。</para>
    /// </summary>
    public async Task<RuleScope> ResolveRuleScopeAsync(StandardDirectoryFile file)
    {
        if (file == null) return new RuleScope("", "", "", "");

        var ruleKey = string.IsNullOrEmpty(file.StandardFileCode) ? file.Code ?? "" : file.StandardFileCode!;

        // ① 模板行 = 规则键所属行（模板行自身即模板行，不必回查）
        var templateRow = ruleKey == file.Code ? file
            : await _core.GetDirectoryFileByCodeAsync(ruleKey);

        // ② 标准/阶段以模板行为准；模板行缺失时退回文件行
        var standardCode = templateRow?.StandardCode ?? "";
        var stageCode = templateRow?.StageCode ?? "";
        if (string.IsNullOrEmpty(standardCode)) standardCode = file.StandardCode ?? "";
        if (string.IsNullOrEmpty(stageCode)) stageCode = file.StageCode ?? "";

        // ③ 机构 = 模板行所属配置的 OrgCode（缺失时用文件行自身的配置）
        var orgCode = "";
        var configCode = string.IsNullOrEmpty(templateRow?.ConfigCode) ? file.ConfigCode : templateRow!.ConfigCode;
        if (!string.IsNullOrEmpty(configCode))
        {
            var config = await _core.GetDirectoryConfigByCodeAsync(configCode);
            if (config != null)
            {
                orgCode = config.OrgCode ?? "";
                if (string.IsNullOrEmpty(standardCode)) standardCode = config.StandardCode;
                if (string.IsNullOrEmpty(stageCode)) stageCode = config.StageCode;
            }
        }

        // ④ 兜底：文件要求模板（FR-xxx 不是目录文件行）—— 本表自带 OrgCode/StandardCode
        if (string.IsNullOrEmpty(orgCode))
        {
            var fr = (await _db.GetOneAsync<FileRequirement>(x => x.Code == ruleKey)).Data;
            if (fr != null)
            {
                orgCode = fr.OrgCode ?? "";
                if (string.IsNullOrEmpty(standardCode)) standardCode = fr.StandardCode ?? "";
                if (string.IsNullOrEmpty(stageCode))
                {
                    var folder = await _core.GetDirectoryFolderByCodeAsync(fr.FolderCode ?? "");
                    var frConfig = folder == null
                        ? null
                        : await _core.GetDirectoryConfigByCodeAsync(folder.ConfigCode);
                    if (frConfig != null) stageCode = frConfig.StageCode;
                }
            }
        }

        return new RuleScope(ruleKey, orgCode, standardCode, stageCode);
    }

    /// <summary>按文件编码推导作用域后取规则（<paramref name="fileCode"/> 即规则键来源）。</summary>
    public async Task<RuleScope> ResolveRuleScopeAsync(string fileCode)
    {
        if (string.IsNullOrWhiteSpace(fileCode)) return new RuleScope("", "", "", "");
        var file = await _core.GetDirectoryFileByCodeAsync(fileCode);
        if (file == null) return new RuleScope(fileCode, "", "", "");
        return await ResolveRuleScopeAsync(file);
    }

    /// <summary>
    /// ★ 按四元组取规则的**唯一入口**（S0）：机构 + 标准 + 阶段 + 规则键。
    /// <para>⛔ 禁止只按 <c>StandardFileCode</c> 单列查规则 —— 同一文档 Code 在不同机构下是不同规则。</para>
    /// </summary>
    public async Task<DocExtractionRule?> GetRuleByScopeAsync(
        string orgCode, string standardCode, string stageCode, string standardFileCode)
    {
        if (string.IsNullOrWhiteSpace(standardFileCode)) return null;
        var rules = await _core.GetDocExtractionRulesAsync(orgCode);
        return rules.FirstOrDefault(x =>
            x.StandardCode == standardCode &&
            x.StageCode == stageCode &&
            x.StandardFileCode == standardFileCode);
    }

    /// <summary>由文件编码推导四元组后取规则（管理端详情/删除/AI 分析/验证用）。</summary>
    public async Task<DocExtractionRule?> GetRuleByFileAsync(string fileCode)
    {
        if (string.IsNullOrWhiteSpace(fileCode)) return null;

        var scope = await ResolveRuleScopeAsync(fileCode);
        if (OrgScope.IsOrgRow(scope.OrgCode))
            return await GetRuleByScopeAsync(scope.OrgCode, scope.StandardCode, scope.StageCode, scope.RuleKey);

        // 遗留兜底：文件行不存在（FR 模板缺失 / 历史测试 code）⇒ 推不出四元组，退回单列定位。
        // 企业提取不走本方法（执行器按四元组严格口径），此处仅供管理端详情/删除/AI 配置期使用。
        var legacy = await _core.GetDocExtractionRulesByStandardFileCodesAsync(new[] { fileCode });
        return legacy.FirstOrDefault();
    }

    // ========================================================
    // 企业域落库（G-2c 写入段，02 号 V-P1 甲路线：旧行 IsValid=0 归档，不物理删）
    // ========================================================

    /// <summary>
    /// 提取结果落 B-08/B-09 **企业域**（真实 EnterpriseCode + 真实 VersionNumber）。
    /// <para>与 <see cref="SyncExtractionResultToB08B09Async"/>（模板域：物理删重写）的关键差异：
    /// 企业域按版本链审计（02 号 §二），历史行必须归档保留 —— 先 UPDATE IsValid=0 归档同键旧行，再插新行。</para>
    /// <para>⚠️ B-08/B-09 实体未声明 IsValid 接口（ORM 自动过滤不生效），归档/读取一律走版本店的原生条件。</para>
    /// <para><b>D3</b>：<c>StandardCode</c>/<c>StageCode</c> 取<b>槽位文件行</b>（调用方传入），
    /// ⛔ 不取规则 —— 规则四元组可跨标准/阶段复用，取规则会写错列（§七② 索引将指不到东西）。</para>
    /// <para><b>D5</b>：归档走 <c>VersionNumber &lt;= 当前版本</c> 精确谓词（只翻 IsValid，行数不减）。</para>
    /// </summary>
    /// <param name="standardCode">槽位行的标准编码（D3；空则回退规则值）</param>
    /// <param name="stageCode">槽位行的阶段编码（D3；空则回退规则值）</param>
    public async Task<(int FieldCount, int TableCount)> SaveEnterpriseExtractionResultsAsync(
        DocExtractionRule rule, ExtractionData extractionData,
        string enterpriseCode, string fileCode, int versionNumber,
        string standardCode = "", string stageCode = "")
    {
        var now = DateTime.Now;
        var standardFileCode = rule.StandardFileCode ?? "";
        if (string.IsNullOrWhiteSpace(standardCode)) standardCode = rule.StandardCode ?? "";
        if (string.IsNullOrWhiteSpace(stageCode)) stageCode = rule.StageCode ?? "";

        // 1. 归档同 (企业, 文件)、版本 <= 当前版本的活跃行（IsValid=0 保留版本链，行数不减）
        await _resultStore.InvalidateActiveAsync(enterpriseCode, fileCode, versionNumber);

        // 2. 字段中文名映射（规则定义为准；ExtractionData 键已由 MapOutputs 归一为 field_code）
        var fieldDefs = (await _db.GetListAsync<DocFieldDef>(x => x.RuleCode == rule.Code)).Data ?? new();
        var fieldNameMap = fieldDefs
            .GroupBy(f => f.FieldCode ?? "", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().FieldName ?? g.Key, StringComparer.OrdinalIgnoreCase);

        var fieldCount = 0;
        foreach (var kv in extractionData.Fields ?? new())
        {
            var value = kv.Value?.ToString();
            if (string.IsNullOrWhiteSpace(value)) continue;

            await _db.Client.Insertable(new ExtractionResult
            {
                Code = Guid.NewGuid().ToString("N"),
                EnterpriseCode = enterpriseCode,
                StandardFileCode = standardFileCode,
                StandardCode = standardCode,
                StageCode = stageCode,
                FileCode = fileCode,
                VersionNumber = versionNumber,
                RuleCode = rule.Code ?? "",
                FieldCode = kv.Key,
                FieldName = fieldNameMap.TryGetValue(kv.Key, out var fn) ? fn : kv.Key,
                LabelTag = kv.Key,
                ExtractedValue = value,
                ExtractedAt = now,
                CreateTime = now
            }).ExecuteCommandAsync();
            fieldCount++;
        }

        var tableCount = 0;
        var tableIndex = 1;
        foreach (var kv in extractionData.Tables ?? new())
        {
            var rows = kv.Value;
            if (rows == null || rows.Count == 0) continue;

            await _db.Client.Insertable(new TableExtractionResult
            {
                Code = Guid.NewGuid().ToString("N"),
                EnterpriseCode = enterpriseCode,
                StandardFileCode = standardFileCode,
                StandardCode = standardCode,
                StageCode = stageCode,
                FileCode = fileCode,
                VersionNumber = versionNumber,
                RuleCode = rule.Code ?? "",
                TableCode = kv.Key,
                TableIndex = tableIndex++,
                ExtractedJson = JsonSerializer.Serialize(rows, JsonOptions),
                ExtractedAt = now,
                CreateTime = now
            }).ExecuteCommandAsync();
            tableCount++;
        }

        return (fieldCount, tableCount);
    }

    // ========================================================
    // 详情（对照旧 GetRuleDetailAsync L624-738）
    // ========================================================

    /// <summary>
    /// 获取规则详情（含字段/表格定义 + 已保存的提取值回显）
    /// </summary>
    public async Task<RuleDetailResponse?> GetRuleDetailAsync(string standardFileCode)
    {
        // S0：四元组定位（文件编码 → 机构/标准/阶段 → 规则），不再单列查 StandardFileCode
        var rule = await GetRuleByFileAsync(standardFileCode);
        if (rule == null) return null;

        // 读取平台标准级样例域提取结果（B-08 字段值 / B-09 表格行），保存后重新进入可完整回显
        // ★ 2026-10-13 归属模型 V2（已收口 Task #33）：
        //   ① DB 列已改名 `OrgCode` → `EnterpriseCode`（SQL §4），实体属性名与列名一致；
        //   ② 写侧默认参 `enterpriseCode = ""`（见 :329）⇒ **写读同为「非企业行」**，回显一致。
        var b08Rows = await _db.Client.Queryable<ExtractionResult>()
            .Where(x => string.IsNullOrEmpty(x.EnterpriseCode) && x.StandardFileCode == standardFileCode && !x.IsDeleted)
            .Select(x => new B08Row { FieldCode = x.FieldCode, ExtractedValue = x.ExtractedValue })
            .ToListAsync();
        var b08Map = b08Rows.GroupBy(x => x.FieldCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().ExtractedValue ?? "", StringComparer.OrdinalIgnoreCase);

        var b09Rows = await _db.Client.Queryable<TableExtractionResult>()
            .Where(x => string.IsNullOrEmpty(x.EnterpriseCode) && x.StandardFileCode == standardFileCode)
            .OrderBy(x => x.TableIndex)
            .Select(x => new B09Row { TableIndex = x.TableIndex, ExtractedJson = x.ExtractedJson })
            .ToListAsync();
        var b09List = b09Rows;

        // 字段定义（rule_code 关联，按 Sort 排序）
        var fields = (await _db.GetListAsync<DocFieldDef>(x => x.RuleCode == rule.Code)).Data ?? new();
        var fieldDtos = fields.OrderBy(x => x.Sort).Select(x => new FieldDefDto
        {
            Name = x.FieldName,
            // NameEn 与 Code 同源（DB 只有一列 FieldCode）——不填会让前端「英文名」输入框回显为空
            NameEn = x.FieldCode,
            Code = x.FieldCode,
            DataType = x.DataType,
            Description = x.Description,
            IsManual = x.IsManual,
            IsAiRecommended = x.IsAiRecommended ?? true
        }).ToList();
        foreach (var f in fieldDtos)
        {
            if (!string.IsNullOrEmpty(f.Code) && b08Map.TryGetValue(f.Code, out var val))
                f.ExtractedValue = val;
        }

        // 表格定义 + 列定义
        var tables = (await _db.GetListAsync<DocTableDef>(x => x.RuleCode == rule.Code)).Data ?? new();
        var tableDtos = new List<TableDefDto>();
        foreach (var table in tables.OrderBy(x => x.Sort))
        {
            var columns = (await _db.GetListAsync<DocTableFieldDef>(x => x.TableCode == table.Code)).Data ?? new();
            tableDtos.Add(new TableDefDto
            {
                Name = table.TableName,
                NameEn = table.TableCode,
                Code = table.TableCode,
                Description = table.Description,
                Columns = columns.OrderBy(x => x.Sort).Select(x => new TableColumnDto
                {
                    Name = x.ColumnName,
                    NameEn = x.ColumnCode,
                    Code = x.ColumnCode,
                    DataType = x.DataType
                }).ToList()
            });
        }

        // 表格提取行数据回显（B-09 按 TableIndex 与表格定义顺序一一对应）
        for (int i = 0; i < tableDtos.Count && i < b09List.Count; i++)
        {
            try
            {
                var rows = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(b09List[i].ExtractedJson ?? "[]", JsonOptions);
                if (rows != null) tableDtos[i].ExtractedData = rows;
            }
            catch { /* JSON 损坏时跳过该行回显 */ }
        }

        // ★ 2026-10-14 验证结果回显：把已落库的提取值组装成 ExtractionData 返回。
        //   数据源 = 上面已查出的 B-08/B-09（保存/验证时写入、口径与字段/表格定义同源过滤），
        //   ⛔ 不另读 rule.SampleData —— 那是工作流试运行的合并字典（field/table 键混在一起），
        //   需要再按定义拆一次键；而 B-08/B-09 本来就按定义拆好了，且与「自动分析」页签回显同源。
        var extractionData = BuildDetailExtractionData(b08Map, tableDtos);

        return new RuleDetailResponse
        {
            Id = rule.Id,
            Code = rule.Code,
            StandardFileCode = rule.StandardFileCode ?? "",
            OrgCode = rule.OrgCode ?? "",
            StandardCode = rule.StandardCode ?? "",
            StageCode = rule.StageCode ?? "",
            Skill = rule.Skill,
            Prompt = rule.Prompt,
            IsValid = rule.DocIsValid,
            Status = rule.Status ?? "none",
            Fields = fieldDtos,
            Tables = tableDtos,
            ExtractionData = extractionData,
            CreateTime = rule.CreateTime,
            UpdateTime = rule.UpdateTime
        };
    }

    /// <summary>
    /// 把已经查好的 B-08/B-09 回显组装成 ExtractionData，供详情接口返回（避免 detail
    /// 与「提示词与验证」页签之间出现「验证通过但提取结果全空」的视觉断裂）。
    /// <para>口径：字段只保留规则定义内 code 的值；表格只保留有行的（空表不入 ExtractionData，
    /// 避免与 AI 分析页签展示「未提取到」混淆）。</para>
    /// </summary>
    private static ExtractionData? BuildDetailExtractionData(
        Dictionary<string, string> b08Map, List<TableDefDto> tableDtos)
    {
        if (b08Map.Count == 0 && tableDtos.All(t => t.ExtractedData == null || t.ExtractedData.Count == 0))
            return null;

        var fields = b08Map.Count > 0
            ? new Dictionary<string, object>(b08Map.Count)
            : null;
        if (fields != null)
            foreach (var kv in b08Map) fields[kv.Key] = kv.Value;

        var tables = new Dictionary<string, List<Dictionary<string, object>>>();
        foreach (var td in tableDtos)
            if (!string.IsNullOrEmpty(td.Code) && td.ExtractedData?.Count > 0)
                tables[td.Code] = td.ExtractedData;

        return new ExtractionData
        {
            Fields = fields,
            Tables = tables.Count > 0 ? tables : null
        };
    }

    // ========================================================
    // 删除（对照旧 DeleteRuleAsync L740-775：级联删定义）
    // ========================================================

    public async Task<bool> DeleteRuleAsync(string standardFileCode)
    {
        // S0：四元组定位（避免误删另一机构同文档 Code 的规则）
        var rule = await GetRuleByFileAsync(standardFileCode);
        if (rule == null) return false;

        // P3：删除规则 = 该四元组不再可用 ⇒ 先标记相关企业文件待重提取（结果归档 + 状态回 none）
        await MarkFilesStaleAsync(rule, "提取规则已删除，待重新配置后提取");

        // 级联删除字段和表格定义
        var fields = (await _db.GetListAsync<DocFieldDef>(x => x.RuleCode == rule.Code)).Data ?? new();
        foreach (var f in fields)
            await _db.DeleteByCodeAsync<DocFieldDef>(f.Code);

        var tables = (await _db.GetListAsync<DocTableDef>(x => x.RuleCode == rule.Code)).Data ?? new();
        foreach (var t in tables)
        {
            var cols = (await _db.GetListAsync<DocTableFieldDef>(x => x.TableCode == t.Code)).Data ?? new();
            foreach (var c in cols)
                await _db.DeleteByCodeAsync<DocTableFieldDef>(c.Code);
            await _db.DeleteByCodeAsync<DocTableDef>(t.Code);
        }

        // 提取结果同步删除（物理删除，与 save 逻辑对齐）
        await _db.Client.Deleteable<ExtractionResult>()
            .Where(x => string.IsNullOrEmpty(x.EnterpriseCode) && x.StandardFileCode == standardFileCode)
            .ExecuteCommandAsync();
        await _db.Client.Deleteable<TableExtractionResult>()
            .Where(x => string.IsNullOrEmpty(x.EnterpriseCode) && x.StandardFileCode == standardFileCode)
            .ExecuteCommandAsync();

        await _db.DeleteByCodeAsync<DocExtractionRule>(rule.Code);
        return true;
    }

    // ========================================================
    // P3：规则变更 → 自动标记待重提取（2026-09-29 用户裁决）
    // ========================================================

    /// <summary>保存前规则快照（Prompt / Status / 定义指纹），供保存后 diff。</summary>
    private sealed record RuleSnapshot(string Prompt, string Status, string DefFingerprint);

    /// <summary>规则内容是否可用（与 <see cref="ExtractionScopeResolver.IsUsableRule"/> 同口径）。</summary>
    private static bool IsUsableStatus(string? status)
        => status == "configured" || status == "passed";

    private async Task<RuleSnapshot> SnapshotRuleAsync(DocExtractionRule rule)
        => new(rule.Prompt ?? "", rule.Status ?? "", await DefFingerprintAsync(rule.Code));

    /// <summary>
    /// 字段 + 表格 + 列定义的稳定指纹（排序后拼接，直接字符串比较）。
    /// <para>表小（每规则几十行以内），不做 hash ——  diff 失败时可用肉眼核对差异。</para>
    /// </summary>
    private async Task<string> DefFingerprintAsync(string? ruleCode)
    {
        var fields = (await _db.GetListAsync<DocFieldDef>(x => x.RuleCode == ruleCode)).Data ?? new();
        var tables = (await _db.GetListAsync<DocTableDef>(x => x.RuleCode == ruleCode)).Data ?? new();
        var tableCodes = tables.Select(t => t.Code).ToList();

        var sb = new System.Text.StringBuilder();
        foreach (var f in fields.OrderBy(x => x.FieldCode).ThenBy(x => x.Sort))
            sb.Append("F|").Append(f.FieldCode).Append('|').Append(f.FieldName).Append('|')
              .Append(f.DataType).Append('|').Append(f.Description).Append('|').Append(f.IsManual).Append(';');
        foreach (var t in tables.OrderBy(x => x.TableCode).ThenBy(x => x.Sort))
        {
            sb.Append("T|").Append(t.TableCode).Append('|').Append(t.TableName).Append('|').Append(t.Description).Append(';');
            var cols = tableCodes.Contains(t.Code)
                ? (await _db.GetListAsync<DocTableFieldDef>(x => x.TableCode == t.Code)).Data ?? new()
                : new List<DocTableFieldDef>();
            foreach (var c in cols.OrderBy(x => x.ColumnCode).ThenBy(x => x.Sort))
                sb.Append("C|").Append(c.ColumnCode).Append('|').Append(c.ColumnName).Append('|')
                  .Append(c.DataType).Append(';');
        }
        return sb.ToString();
    }

    /// <summary>
    /// ★ P3：标记该规则命中的企业文件「待重新提取」——归档活跃结果（版本店唯一口）+
    /// <c>ExtractStatus='none'</c> + 原因落 <c>ExtractMessage</c>。<b>不自动入队</b>。
    /// </summary>
    /// <returns>实际标记的文件数</returns>
    private async Task<int> MarkFilesStaleAsync(DocExtractionRule rule, string reason)
    {
        // 文件行无机构列 ⇒ 经 ConfigCode → StandardDirectoryConfig.OrgCode 间接归属（01 §3）
        // ★ 1.B.6d-2：取数收口 CoreResourceQuery（全量有效文件 + 内存三键过滤 ——
        //   文件行是目录槽位级数据（百行级），全取优于再造一个复用度=1 的按键方法）
        var rows = (await _core.GetDirectoryFilesAsync())
            .Where(x => x.StandardCode == rule.StandardCode && x.StageCode == rule.StageCode &&
                        x.StandardFileCode == rule.StandardFileCode)
            .ToList();

        var cfgCodes = rows.Select(x => x.ConfigCode).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        // ★ 1.B.6d-1：取数收口 CoreResourceQuery（等价：全部有效目录配置）
        var orgByCfg = cfgCodes.Count == 0
            ? new Dictionary<string, string>()
            : (await _core.GetDirectoryConfigsAsync())
                .Where(c => cfgCodes.Contains(c.Code))
                .GroupBy(c => c.Code)
                .ToDictionary(g => g.Key, g => g.First().OrgCode ?? "");

        var n = 0;
        var now = DateTime.Now;
        foreach (var row in rows)
        {
            // 只标「有活跃结果可作废」的行：completed 才有结果；pending/skipped 本来就无结果
            if (row.ExtractStatus != EnterpriseExtractStatus.Completed) continue;
            if (string.IsNullOrEmpty(row.EnterpriseCode)) continue;
            // ★ 2026-10-13 归属模型 V2：机构常量 yzh-std-env 已废除 ⇒ 原「机构值误入企业列」防御取消
            if (!orgByCfg.TryGetValue(row.ConfigCode ?? "", out var org) || org != rule.OrgCode) continue;

            var ent = row.EnterpriseCode!;
            // 归档尽力而为（结果可能已在别处作废 = 0 行）；completed 状态一律回 none ——
            // 否则「显示已提取 + 结果读出来是空」两头不报错（与 3 态铁律同源的不一致态）
            await _resultStore.InvalidateActiveAsync(ent, row.Code ?? "");

            row.ExtractStatus = EnterpriseExtractStatus.None;
            row.ExtractMessage = reason;
            row.UpdateTime = now;
            var upd = await _db.UpdateAsync(row,
                nameof(StandardDirectoryFile.ExtractStatus),
                nameof(StandardDirectoryFile.ExtractMessage),
                nameof(StandardDirectoryFile.UpdateTime));
            if (upd.Success) n++;
        }

        if (n > 0)
            _logger.LogInformation("[DocExtractionRule] 规则变更标记: {OrgCode}/{StandardCode}/{StageCode}/{RuleKey} → {N} 个文档（{Reason}）",
                rule.OrgCode, rule.StandardCode, rule.StageCode, rule.StandardFileCode, n, reason);
        return n;
    }

    // ========================================================
    // 已配置规则列表（对照旧 GetConfiguredRulesAsync L853-879）
    // ========================================================

    /// <summary>获取已配置提取规则的文档列表（供工作流配置页面选择文档）</summary>
    /// <remarks>
    /// ⚠️ 列名对照真实表结构（2026-09-19 snake→Pascal 迁移后校准）：
    /// cert_doc_extraction_rule / cert_standard_directory_file 均已是 PascalCase 列名。
    /// 另：SqlSugarDbOrm.SqlQueryAsync 失败时返回 Result.Fail 而非抛异常（被 ORM 吞掉），
    /// SQL 列名错误只会表现为“接口 200 + 空数组”，需结合后端日志排查。
    /// </remarks>
    public async Task<List<object>> GetConfiguredRulesAsync()
    {
        // ★ 2026-10-13：有效性判据显式落在消费侧 —— 视图 v_cert_configured_rules 已按
        // 26-数据库视图设计规范-V1 §三 去掉 WHERE 内固化的 IsValid = 1，改为暴露 RuleIsValid 列。
        // 这里补上等价的谓词，线上可见行与改造前逐行一致（A/B 已核对）。
        var result = await _db.GetListAsync<ConfiguredRuleView>(x => x.RuleIsValid == 1);
        // D-5 契约：显式经 ConfiguredRuleRow 输出 camelCase；直接序列化实体会是 PascalCase，
        // 前端读 r.standardFileCode 全部 undefined → 状态 map 为空 → 树标签恒为「未配置」
        var rows = (result.Data ?? new List<ConfiguredRuleView>())
            .Select(x => new ConfiguredRuleRow
            {
                RuleCode = x.RuleCode,
                StandardFileCode = x.StandardFileCode,
                FileName = x.FileName,
                // ★ 2026-10-15：文件夹路径（树形下拉用）—— 与实体 StandardDirectoryFile.FolderPath
                //   计算属性同款：FullPath 去 FileName 后缀再 TrimEnd('/')。
                //   文件行缺失（视图 COALESCE FullPath=''）→ folderPath='' → 前端归根级。
                FolderPath = ComputeFolderPath(x.FullPath, x.FileName),
                StandardCode = x.StandardCode,
                StageCode = x.StageCode,
                Skill = x.Skill,
                DocIsValid = x.DocIsValid,
                Status = x.Status
            })
            .Cast<object>()
            .ToList();
        return rows;
    }

    /// <summary>
    /// FullPath → 文件夹路径（FullPath 去 FileName 后缀）。
    /// <para>与 <c>StandardDirectoryFile.FolderPath</c> 计算属性逐字同款 —— 视图行是匿名投影，
    /// 无法直接复用实体计算属性，故在此内联（⛔ 不要改逻辑，两处必须一致）。</para>
    /// </summary>
    private static string ComputeFolderPath(string? fullPath, string? fileName)
    {
        if (string.IsNullOrEmpty(fullPath) || string.IsNullOrEmpty(fileName)) return "";
        return fullPath.EndsWith(fileName, StringComparison.Ordinal)
            ? fullPath[..^fileName.Length].TrimEnd('/')
            : fullPath;
    }

    // ========================================================
    // 字段/表格定义查询（对照旧 GetFieldsAndTablesAsync L881-913）
    // ========================================================

    /// <summary>获取规则的字段和表格定义（供 docField/docTable 节点选择）</summary>
    public async Task<object?> GetFieldsAndTablesAsync(string ruleCode)
    {
        var fields = (await _db.GetListAsync<DocFieldDef>(x => x.RuleCode == ruleCode)).Data ?? new();
        var tables = (await _db.GetListAsync<DocTableDef>(x => x.RuleCode == ruleCode)).Data ?? new();

        return new
        {
            fields = fields.OrderBy(x => x.Sort).Select(x => new
            {
                fieldCode = x.FieldCode,
                fieldName = x.FieldName,
                dataType = x.DataType,
                description = x.Description
            }).ToList(),
            tables = tables.OrderBy(x => x.Sort).Select(x => new
            {
                tableCode = x.TableCode,
                tableName = x.TableName,
                description = x.Description
            }).ToList()
        };
    }

    // ========================================================
    // 辅助
    // ========================================================

    /// <summary>中文名 → 英文驼峰（对照旧 ToPascalCase 后首字母小写的实际效果）</summary>
    private static string ToCamel(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        return name.ToCamelCase();
    }

    // raw SQL 行 DTO
    private class B08Row
    {
        public string FieldCode { get; set; } = "";
        public string? ExtractedValue { get; set; }
    }

    private class B09Row
    {
        public int TableIndex { get; set; }
        public string? ExtractedJson { get; set; }
    }

    /// <summary>
    /// 已配置规则行 DTO（供工作流设计器 docField/docTable 下拉）
    /// <para>序列化约定（D-5 契约）：显式 camelCase — 旧后端 GetConfiguredRulesAsync 返回匿名对象 camelCase
    /// （ruleCode/fileName/standardFileCode），前端 NodePropertyForm 按 camelCase 消费，
    /// 迁移为 DTO 类时若依赖默认序列化会变 PascalCase 导致下拉 label/value 为空。</para>
    /// </summary>
    private class ConfiguredRuleRow
    {
        [System.Text.Json.Serialization.JsonPropertyName("ruleCode")]
        public string RuleCode { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("standardFileCode")]
        public string StandardFileCode { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("fileName")]
        public string FileName { get; set; } = "";

        /// <summary>文件夹路径（'/' 分隔，无文件名）—— el-tree-select 构树用（2026-10-15）</summary>
        [System.Text.Json.Serialization.JsonPropertyName("folderPath")]
        public string FolderPath { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("standardCode")]
        public string StandardCode { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("stageCode")]
        public string StageCode { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("skill")]
        public string Skill { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("isValid")]
        public bool DocIsValid { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("createDate")]
        public DateTime? CreateTime { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("modifyDate")]
        public DateTime? UpdateTime { get; set; }
    }
}
