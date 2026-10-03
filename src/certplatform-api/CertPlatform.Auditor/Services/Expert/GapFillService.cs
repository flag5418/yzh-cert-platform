using Microsoft.Extensions.Logging;
using CertPlatform.Admin.Services.DocExtraction;
using CertPlatform.Admin.Entities.Doc;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Auditor.Services.Expert;

/// <summary>
/// ★ 人工补录服务（2026-09-30 用户裁决 J2 / J3 / J4）
/// </summary>
///
/// <para><b>落库去向</b>（J2）：补录<b>直接写</b> <c>cert_extraction_result</c> /
/// <c>cert_table_extraction_result</c>，用 <c>ValueSource='manual'</c> 与自动提取值区分。
/// ⛔ <b>不建独立补录表</b>（05 号 D06）—— 值改了所有任务都受益。</para>
///
/// <para><b>新增 vs 更新</b>：先查 <c>(OrgCode, RuleCode, FieldCode, ValueSource='manual', IsValid=1)</c>，
/// 命中 → 改值；未命中 → <b>INSERT</b>。★ <b>不归档同键的 auto 行</b> ——
/// 两者并存，取数时人工优先（<c>ExtractionDataResolver</c>），这样提取时间线可完整还原。</para>
///
/// <para><b>★ 关键设计（J3 + J4）</b>：<c>FileCode</c> 是 <c>NOT NULL varchar(36)</c> 且进了唯一键，
/// 但<b>企业可能根本没上传那个文件</b>。⛔ <b>不建虚拟文件 / 虚拟槽位</b>（用户明确否决）。
/// 解法：<c>FileCode</c> 填<b>规则声明的 <c>StandardFileCode</c></b>（本来就有的 GUID），
/// 补录行的归属由 <see cref="ExtractionResult.RuleCode"/> 承载（裁决 J4：1 文件 = 1 规则）。
/// 因为清理与取数都<b>按 RuleCode 而非 FileCode</b>，这一列只需非空即可，语义不影响正确性。</para>
///
/// <para><b>留痕</b>：每次补录写一条 <c>cert_extraction_change_log</c>（<c>ChangeAction='manual_edit'</c>）。
/// <b>值相同也要记</b> —— 专家打开编辑框确认保存是一次有意的确认动作，
/// 审计上「他看过并认可」和「没人看过」是两回事（05 号 §5.2）。</para>
/// </remarks>
public class GapFillService
{
    private readonly IDbOrm _db;
    private readonly ExtractionDataResolver _resolver;
    private readonly ILogger<GapFillService> _logger;

    public GapFillService(IDbOrm db, ExtractionDataResolver resolver, ILogger<GapFillService> logger)
    {
        _db = db;
        _resolver = resolver;
        _logger = logger;
    }

    /// <summary>单条补录结果</summary>
    public sealed class FillOutcome
    {
        public string GapCode { get; set; } = "";
        public string GapType { get; set; } = "";
        public string GapLabel { get; set; } = "";
        public string GapStatus { get; set; } = "";
        /// <summary>受影响的检查项 / 章节 Code（供前端询问"是否重跑"）</summary>
        public List<string> ImpactedItemCodes { get; set; } = new();
    }

    /// <summary>批量补录结果</summary>
    public sealed class BatchFillResult
    {
        public int FilledGapCount { get; set; }
        public List<FillOutcome> Items { get; set; } = new();
        /// <summary>去重后的受影响检查项</summary>
        public List<string> ImpactedItemCodes { get; set; } = new();
    }

    // ── ① 单条补录 ────────────────────────────────────────────

    /// <summary>
    /// 补录一条缺口：写提取结果表 + 记 change_log + 同键缺口全部置 filled + 回写 GapCount。
    /// </summary>
    /// <param name="gap">缺口行（须 <c>GapStatus='pending'</c>）</param>
    /// <param name="value">补录的值；表格型必须是 JSON 数组字符串</param>
    /// <param name="taskCode">所属任务（冗余进 change_log）</param>
    /// <param name="operatorCode">/ <param name="operatorName">操作人</param>
    /// <param name="remark">备注</param>
    public async Task<Result<FillOutcome>> FillAsync(
        CertExpertTaskDataGap gap, string value, string? taskCode,
        string? operatorCode, string? operatorName, string? remark)
    {
        if (gap == null) return Result<FillOutcome>.Fail("缺口不存在");
        if (gap.GapStatus != ExpertTaskConst.GapStatus.Pending)
            return Result<FillOutcome>.Fail($"缺口已{DescribeStatus(gap.GapStatus)}，不能重复补录");

        // ★ J4：RuleCode 是主键成分，缺了不知道往哪行写
        if (string.IsNullOrWhiteSpace(gap.RuleCode))
            return Result<FillOutcome>.Fail("缺口缺少 RuleCode（工作流节点未配置 ruleCode），无法定位写入位置");

        var now = DateTime.Now;
        var outcome = new FillOutcome
        {
            GapCode = gap.Code ?? "",
            GapType = gap.GapType,
            GapLabel = gap.GapLabel,
            GapStatus = gap.GapStatus
        };

        Result r = gap.GapType == "table"
            ? await FillTableAsync(gap, value, taskCode, operatorCode, operatorName, remark, now)
            : gap.GapType == "field"
                ? await FillFieldAsync(gap, value, taskCode, operatorCode, operatorName, remark, now)
                : Result.Fail($"未识别的缺口类型「{gap.GapType}」，请检查工作流节点类型");

        if (!r.Success) return Result<FillOutcome>.Fail(r.Error ?? "补录失败");

        // ★ 同一 (RuleCode, FieldCode/TableCode) 的所有 pending gap 一并置 filled
        //   （字段被多条规则引用时，一次补录解决多条）
        var flipped = await FlipGapsAsync(gap, operatorCode, operatorName, now);
        outcome.GapStatus = ExpertTaskConst.GapStatus.Filled;

        // 受影响的检查项（前端"是否立即重跑"用）
        var impacted = (await _db.GetListAsync<CertExpertTaskDataGap>(x =>
            x.TaskCode == gap.TaskCode
            && x.RuleCode == gap.RuleCode
            && (gap.GapType == "field" ? x.FieldCode == gap.FieldCode : x.TableCode == gap.TableCode)
            && !x.IsDeleted)).Data ?? new List<CertExpertTaskDataGap>();

        outcome.ImpactedItemCodes = impacted
            .Select(x => x.SourceItemCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct().ToList();

        await RefreshGapCountAsync(gap.TaskCode ?? "");

        _logger.LogInformation(
            "[GapFill] 补录成功 gap={Gap} type={Type} rule={Rule} code={Code} 连带置filled={Flipped} 影响项={Impacted}",
            gap.Code, gap.GapType, gap.RuleCode,
            gap.FieldCode ?? gap.TableCode, flipped, outcome.ImpactedItemCodes.Count);

        return Result<FillOutcome>.Ok(outcome);
    }

    // ── ② 批量补录（单事务，失败整体回滚） ─────────────────────

    /// <summary>批量补录。上限 200 条。</summary>
    public async Task<Result<BatchFillResult>> BatchFillAsync(
        List<(string GapCode, string Value)> items,
        string? taskCode, string? operatorCode, string? operatorName, string? remark)
    {
        if (items == null || items.Count == 0) return Result<BatchFillResult>.Fail("没有要补录的项");
        if (items.Count > 200) return Result<BatchFillResult>.Fail("单次最多补录 200 项");

        var gaps = (await _db.GetListAsync<CertExpertTaskDataGap>(x =>
            items.Select(i => i.GapCode).Contains(x.Code!) && !x.IsDeleted)).Data
            ?? new List<CertExpertTaskDataGap>();

        var map = gaps.Where(g => !string.IsNullOrEmpty(g.Code))
                      .ToDictionary(g => g.Code!, g => g, StringComparer.Ordinal);

        var missing = items.Where(i => !map.ContainsKey(i.GapCode)).Select(i => i.GapCode).ToList();
        if (missing.Count > 0)
            return Result<BatchFillResult>.Fail($"以下缺口不存在：{string.Join("、", missing.Take(10))}");

        var result = new BatchFillResult();
        var impacted = new List<string>();
        var now = DateTime.Now;

        using var tx = _db.BeginTransaction();
        try
        {
            foreach (var (gapCode, value) in items)
            {
                var gap = map[gapCode];
                var r = await FillAsync(gap, value, taskCode, operatorCode, operatorName, remark);
                if (!r.Success) { tx.Rollback(); return Result<BatchFillResult>.Fail($"补录「{gap.GapLabel}」失败：{r.Error}"); }

                result.Items.Add(r.Data!);
                result.FilledGapCount++;
                impacted.AddRange(r.Data!.ImpactedItemCodes);
            }
            tx.Commit();
        }
        catch (Exception ex)
        {
            tx.Rollback();
            _logger.LogError(ex, "[GapFill] 批量补录失败，已整体回滚");
            return Result<BatchFillResult>.Fail($"批量补录失败：{ex.Message}");
        }

        result.ImpactedItemCodes = impacted.Distinct().ToList();
        return Result<BatchFillResult>.Ok(result);
    }

    // ── ③ 字段级落库 ──────────────────────────────────────────

    private async Task<Result> FillFieldAsync(
        CertExpertTaskDataGap gap, string value, string? taskCode,
        string? operatorCode, string? operatorName, string? remark, DateTime now)
    {
        var enterpriseCode = gap.EnterpriseCode;
        var ruleCode = gap.RuleCode!;
        var fieldCode = gap.FieldCode ?? "";

        // ★ 查找键 = (OrgCode, RuleCode, FieldCode, ValueSource='manual', IsValid=1)
        //   ⛔ 不按 FileCode（补录行的 FileCode 填的是 StandardFileCode，不是文件槽位 Code）
        var existing = (await _db.GetListAsync<ExtractionResult>(x =>
            x.EnterpriseCode == enterpriseCode
            && x.RuleCode == ruleCode
            && x.FieldCode == fieldCode
            && x.ValueSource == ExtractionValueSource.Manual
            && x.IsValid == 1)).Data ?? new List<ExtractionResult>();

        // ★ 归属文件：规则声明的 StandardFileCode（本就存在的 GUID，非虚拟文件）
        var standardFileCode = gap.StandardFileCode
            ?? (await _db.GetOneAsync<DocExtractionRule>(x => x.Code == ruleCode)).Data?.StandardFileCode
            ?? "";

        var hit = existing.FirstOrDefault();
        string? oldValue = null;
        string? oldSource = null;

        if (hit != null)
        {
            oldValue = hit.ExtractedValue;
            oldSource = hit.ValueSource;

            hit.ExtractedValue = value;
            hit.ExtractedAt = now;
            hit.UpdateBy = operatorCode;
            hit.UpdateTime = now;
            hit.Confidence = null;         // 人工录入无 AI 置信度
            hit.PositionInfo = null;      // 无原文出处
            hit.IsManualEdited = true;
            hit.ValueSource = ExtractionValueSource.Manual;

            var u = await _db.UpdateAsync(hit,
                nameof(ExtractionResult.ExtractedValue), nameof(ExtractionResult.ExtractedAt),
                nameof(ExtractionResult.Confidence), nameof(ExtractionResult.PositionInfo),
                nameof(ExtractionResult.IsManualEdited), nameof(ExtractionResult.ValueSource),
                nameof(ExtractionResult.UpdateBy), nameof(ExtractionResult.UpdateTime));
            if (!u.Success) return Result.Fail(u.Error);
        }
        else
        {
            var row = new ExtractionResult
            {
                Code = Guid.NewGuid().ToString("N"),
                EnterpriseCode = enterpriseCode,
                StandardFileCode = string.IsNullOrEmpty(standardFileCode) ? null : standardFileCode,
                StandardCode = gap.StandardCode,
                StageCode = gap.StageCode,
                // ★ FileCode NOT NULL：填规则声明的归属标准文件行 Code（J3：不建虚拟文件）
                FileCode = standardFileCode,
                VersionNumber = 1,          // 补录不参与文件版本链
                RuleCode = ruleCode,
                FieldCode = fieldCode,
                FieldName = gap.GapLabel,
                LabelTag = fieldCode,
                ExtractedValue = value,
                Confidence = null,          // 人工录入无 AI 置信度
                PositionInfo = null,
                IsManualEdited = true,
                ValueSource = ExtractionValueSource.Manual,
                ExtractedAt = now,
                CreateBy = operatorCode,
                CreateTime = now,
                UpdateBy = operatorCode,
                UpdateTime = now,
                IsDeleted = false,
                IsValid = 1
            };
            var i = await _db.InsertAsync(row);
            if (!i.Success) return Result.Fail(i.Error);
            hit = row;
            oldSource = null;   // 原无人工行
        }

        // ★ 留痕：值相同也要记（05 号 §5.2）
        await WriteLogAsync(ExtractionChangeLog.ActionManualEdit, "field", hit.Code!,
            enterpriseCode, ruleCode, gap, oldValue, value, oldSource, ExtractionValueSource.Manual,
            taskCode, operatorCode, operatorName, remark, now);

        return Result.Ok();
    }

    // ── ④ 表格级落库 ──────────────────────────────────────────

    private async Task<Result> FillTableAsync(
        CertExpertTaskDataGap gap, string value, string? taskCode,
        string? operatorCode, string? operatorName, string? remark, DateTime now)
    {
        // ★ 表格值必须是 JSON 数组（D22 阶段前端用 textarea 输 JSON，10 号 Q11）
        var rows = ExtractionDataResolver.ParseRows(value);
        if (rows.Count == 0)
            return Result.Fail($"表格「{gap.GapLabel}」的内容解析后没有数据行，请检查 JSON 格式");
        if (!ExtractionDataResolver.IsTableMeaningful(rows))
            return Result.Fail($"表格「{gap.GapLabel}」所有单元格都是空的，这不算有效数据");

        var enterpriseCode = gap.EnterpriseCode;
        var ruleCode = gap.RuleCode!;
        var tableCode = gap.TableCode ?? "";

        var existing = (await _db.GetListAsync<TableExtractionResult>(x =>
            x.EnterpriseCode == enterpriseCode
            && x.RuleCode == ruleCode
            && x.TableCode == tableCode
            && x.ValueSource == ExtractionValueSource.Manual
            && x.IsValid == 1)).Data ?? new List<TableExtractionResult>();

        var standardFileCode = gap.StandardFileCode
            ?? (await _db.GetOneAsync<DocExtractionRule>(x => x.Code == ruleCode)).Data?.StandardFileCode
            ?? "";

        var json = System.Text.Json.JsonSerializer.Serialize(rows);
        var hit = existing.OrderByDescending(x => x.TableIndex).FirstOrDefault();
        string? oldValue = null;
        string? oldSource = null;

        if (hit != null)
        {
            oldValue = hit.ExtractedJson;
            oldSource = hit.ValueSource;

            hit.ExtractedJson = json;
            hit.ExtractedAt = now;
            hit.Confidence = null;
            hit.PositionInfo = null;
            hit.IsManualEdited = true;
            hit.ValueSource = ExtractionValueSource.Manual;
            hit.UpdateBy = operatorCode;
            hit.UpdateTime = now;

            var u = await _db.UpdateAsync(hit,
                nameof(TableExtractionResult.ExtractedJson), nameof(TableExtractionResult.ExtractedAt),
                nameof(TableExtractionResult.Confidence), nameof(TableExtractionResult.PositionInfo),
                nameof(TableExtractionResult.IsManualEdited), nameof(TableExtractionResult.ValueSource),
                nameof(TableExtractionResult.UpdateBy), nameof(TableExtractionResult.UpdateTime));
            if (!u.Success) return Result.Fail(u.Error);
        }
        else
        {
            var row = new TableExtractionResult
            {
                Code = Guid.NewGuid().ToString("N"),
                EnterpriseCode = enterpriseCode,
                StandardFileCode = string.IsNullOrEmpty(standardFileCode) ? null : standardFileCode,
                StandardCode = gap.StandardCode,
                StageCode = gap.StageCode,
                FileCode = standardFileCode,   // ★ J3：不建虚拟文件，填规则声明的归属
                VersionNumber = 1,
                RuleCode = ruleCode,
                TableCode = tableCode,
                TableIndex = 1,
                ExtractedJson = json,
                Confidence = null,
                PositionInfo = null,
                IsManualEdited = true,
                ValueSource = ExtractionValueSource.Manual,
                ExtractedAt = now,
                CreateBy = operatorCode,
                CreateTime = now,
                UpdateBy = operatorCode,
                UpdateTime = now,
                IsDeleted = false,
                IsValid = 1
            };
            var i = await _db.InsertAsync(row);
            if (!i.Success) return Result.Fail(i.Error);
            hit = row;
        }

        await WriteLogAsync(ExtractionChangeLog.ActionManualEdit, "table", hit.Code!,
            enterpriseCode, ruleCode, gap, oldValue, json, oldSource, ExtractionValueSource.Manual,
            taskCode, operatorCode, operatorName, remark, now);

        return Result.Ok();
    }

    // ── ⑤ 内部工具 ────────────────────────────────────────────

    /// <summary>同 (TaskCode, GapType, RuleCode, FieldCode|TableCode) 的 pending gap 一并置 filled</summary>
    private async Task<int> FlipGapsAsync(
        CertExpertTaskDataGap gap, string? operatorCode, string? operatorName, DateTime now)
    {
        var targets = (await _db.GetListAsync<CertExpertTaskDataGap>(x =>
            x.TaskCode == gap.TaskCode
            && x.GapType == gap.GapType
            && x.RuleCode == gap.RuleCode
            && (gap.GapType == "field" ? x.FieldCode == gap.FieldCode : x.TableCode == gap.TableCode)
            && x.GapStatus == ExpertTaskConst.GapStatus.Pending
            && !x.IsDeleted)).Data ?? new List<CertExpertTaskDataGap>();

        if (targets.Count == 0) return 0;

        foreach (var t in targets)
        {
            t.GapStatus = ExpertTaskConst.GapStatus.Filled;
            t.FilledBy = operatorCode;
            t.FilledName = operatorName;
            t.FilledTime = now;
            t.UpdateBy = operatorCode;
            t.UpdateTime = now;
            await _db.UpdateAsync(t,
                nameof(CertExpertTaskDataGap.GapStatus),
                nameof(CertExpertTaskDataGap.FilledBy), nameof(CertExpertTaskDataGap.FilledName),
                nameof(CertExpertTaskDataGap.FilledTime),
                nameof(CertExpertTaskDataGap.UpdateBy), nameof(CertExpertTaskDataGap.UpdateTime));
        }
        return targets.Count;
    }

    private async Task RefreshGapCountAsync(string taskCode)
    {
        if (string.IsNullOrWhiteSpace(taskCode)) return;
        var task = (await _db.GetOneAsync<CertExpertTask>(x => x.Code == taskCode)).Data;
        if (task == null) return;

        task.GapCount = (await _db.CountAsync<CertExpertTaskDataGap>(x =>
            x.TaskCode == taskCode
            && x.GapStatus == ExpertTaskConst.GapStatus.Pending
            && !x.IsDeleted)).Data;
        task.UpdateTime = DateTime.Now;
        await _db.UpdateAsync(task, nameof(CertExpertTask.GapCount), nameof(CertExpertTask.UpdateTime));
    }

    /// <summary>★ 只 INSERT（表不可变）。「值相同也要记」—— 05 号 §5.2。</summary>
    private async Task WriteLogAsync(
        string action, string resultType, string resultCode,
        string enterpriseCode, string ruleCode,
        CertExpertTaskDataGap gap,
        string? oldValue, string? newValue,
        string? oldSource, string? newSource,
        string? taskCode, string? operatorCode, string? operatorName,
        string? remark, DateTime now)
    {
        var log = new ExtractionChangeLog
        {
            Code = Guid.NewGuid().ToString("N"),
            OrgCode = gap.OrgCode,
            ResultType = resultType,
            ResultCode = resultCode,
            EnterpriseCode = enterpriseCode,
            RuleCode = ruleCode,
            StandardCode = gap.StandardCode,
            StageCode = gap.StageCode,
            StandardFileCode = gap.StandardFileCode,
            FieldCode = gap.FieldCode,
            TableCode = gap.TableCode,
            FieldLabel = gap.GapLabel,
            ChangeAction = action,
            OldValue = Truncate(oldValue),
            NewValue = Truncate(newValue),
            OldValueSource = oldSource,
            NewValueSource = newSource,
            TaskCode = taskCode,
            GapCode = gap.Code,
            OperatorCode = operatorCode,
            OperatorName = operatorName,
            OperateTime = now,
            Remark = remark,
            CreateBy = operatorCode,
            CreateTime = now,
            IsValid = 1
        };
        var r = await _db.InsertAsync(log);
        if (!r.Success)
            _logger.LogError("[GapFill] 补录日志写入失败 gap={Gap} err={Err}", gap.Code, r.Error);
    }

    /// <summary>值截断（longtext 够大，但日志展示与内存占用仍需上限）</summary>
    private static string? Truncate(string? v, int max = 60000)
    {
        if (string.IsNullOrEmpty(v)) return v;
        return v.Length <= max ? v : v[..max] + $"…(已截断，原长 {v.Length})";
    }

    private static string DescribeStatus(string? s) => s switch
    {
        ExpertTaskConst.GapStatus.Filled  => "补录",
        ExpertTaskConst.GapStatus.Skipped => "跳过",
        _ => "处理"
    };
}
