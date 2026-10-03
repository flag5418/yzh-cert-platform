using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CertPlatform.Admin.Services.DocExtraction;
using CertPlatform.Shared.Constants;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Admin.Entities.Dir;
using CertPlatform.Admin.Entities.Doc;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using YzhQueueTask = YZH.Core.Stand.Models.Queue.YzhQueueTask;

namespace CertPlatform.Auditor.Services.Ent;

/// <summary>
/// 企业资料提取任务执行器（06 册 G-2c，TaskType = "doc_extract"）
///
/// <para>链路：四元组定位规则（机构+标准+阶段+模板文档 Code，S0）→ 读企业行 <c>MarkdownPath</c>
/// → 跑 LLM → 写 B-08/B-09 企业域（真实 VersionNumber；V-P1 甲路线：旧行 IsValid=0 归档不物理删）
/// → 回写 <c>ExtractStatus/MaxConfidence/ExtractMessage</c>。</para>
///
/// <para>★ 4 态（2026-09-30 用户裁决，见 <see cref="EnterpriseExtractStatus"/>）：
/// <c>completed</c> 已提取 / <c>failed</c> 有规则但执行失败（文档与规则不匹配 / 无法解析 / LLM 返回空）/
/// <c>skipped</c> 无可用规则（不算失败）/ <c>none</c> 尚未提取。
/// 失败原因落 <c>ExtractMessage</c>（页面 tooltip），队列层据 <c>Retryable</c> 决定重试。</para>
///
/// <para>QueueManager 是单例 ⇒ 本类必须单例；Scoped 依赖经 IServiceProvider.CreateScope() 获取。</para>
/// </summary>
public class EnterpriseExtractTaskExecutor : IYzhTaskExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EnterpriseExtractTaskExecutor> _logger;

    public EnterpriseExtractTaskExecutor(IServiceProvider serviceProvider, ILogger<EnterpriseExtractTaskExecutor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public string TaskType => "doc_extract";

    private class ExtractPayload
    {
        public string Code { get; set; } = "";
        public string EnterpriseCode { get; set; } = "";
        public string StageCode { get; set; } = "";
    }

    public async Task<TaskExecutionResult> ExecuteAsync(YzhQueueTask task, CancellationToken cancellationToken)
    {
        ExtractPayload? payload = null;
        try
        {
            if (string.IsNullOrEmpty(task.Payload))
                return Fail("Payload 为空", retryable: false);

            payload = JsonSerializer.Deserialize<ExtractPayload>(task.Payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (payload == null || string.IsNullOrWhiteSpace(payload.Code) || string.IsNullOrWhiteSpace(payload.EnterpriseCode))
                return Fail("Payload 缺少 Code/EnterpriseCode", retryable: false);

            // 信封不变量：提取只允许写真实企业域，禁写模板域（虚拟企业 Code）
            if (payload.EnterpriseCode == YzhVirtualEnterprise.Code)
                return Fail("禁止对标准模板域（虚拟企业 Code）执行企业提取", retryable: false);

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbOrm>();
            var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
            var ruleService = scope.ServiceProvider.GetRequiredService<DocExtractionRuleService>();
            var resolver = scope.ServiceProvider.GetRequiredService<ExtractionScopeResolver>();

            // 1. 企业文件行（含中间态：转换链会临时置 IsValid=0，这里用 IgnoreValid 口径）
            var file = (await db.GetOneIgnoreValidAsync<StandardDirectoryFile>(
                x => x.Code == payload.Code && x.EnterpriseCode == payload.EnterpriseCode)).Data;
            if (file == null)
                return Fail($"企业文件记录不存在: {payload.Code}", retryable: false);

            async Task SetStatusAsync(string status, string? message, decimal? confidence)
            {
                // ★ 4 态收敛（2026-09-30）：写入值经 Normalize 出口 —— completed/failed/skipped/none 原样落库，
                //   其他（pending/processing）回落 none。「为什么没提取」同时落 ExtractMessage（页面 tooltip）
                var st = EnterpriseExtractStatus.Normalize(status);
                file.ExtractStatus = st;
                file.ExtractMessage = message;
                file.MaxConfidence = confidence;
                file.UpdateTime = DateTime.Now;
                await db.UpdateAsync(file,
                    nameof(StandardDirectoryFile.ExtractStatus),
                    nameof(StandardDirectoryFile.ExtractMessage),
                    nameof(StandardDirectoryFile.MaxConfidence),
                    nameof(StandardDirectoryFile.UpdateTime));

                // G-3d 留痕：终态一律记 extract_done（含成功/失败/跳过，02 号 §六 + 4 态）
                if (st is "completed" or "failed" or "none" or "skipped")
                {
                    try
                    {
                        await db.InsertAsync(new EnterpriseFileOpLog
                        {
                            Code = Guid.NewGuid().ToString("N"),
                            EnterpriseCode = payload.EnterpriseCode,
                            StageCode = string.IsNullOrEmpty(payload.StageCode) ? file.StageCode : payload.StageCode,
                            FileCode = file.Code ?? "",
                            OpType = "extract_done",
                            VersionNumber = Math.Max(file.VersionNumber, 1),
                            Detail = JsonSerializer.Serialize(new { status = st, message, confidence }),
                            // ⚠️ BaseEntity 不兜底：漏写 CreateTime 会落成 UTC，与 replace/delete 等本地时间混用（同一列两种时区）
                            IsValid = 1,
                            IsDeleted = false,
                            CreateTime = System.DateTime.Now
                        });
                    }
                    catch (Exception oex) { _logger.LogWarning(oex, "[EnterpriseExtract] op_log 写入失败: {FileCode}", file.Code); }
                }
            }

            // 2. ★ S1 定位链唯一判定（10 号 §六）：四元组 + 可用规则（configured/passed）+ Markdown 就位
            //    ①②③④ 收口于 ExtractionScopeResolver —— 端点/批量/执行器共用同一结论，
            //    无规则 = 正常态 ⇒ 状态 skipped，队列任务不算失败
            var slot = await resolver.ResolveAsync(file);
            if (string.IsNullOrEmpty(slot.RuleCode))
            {
                await SetStatusAsync("skipped", "该文件未配置提取规则，已跳过", null);
                return new TaskExecutionResult { Success = true, Message = "无可用提取规则，跳过" };
            }

            var rule = (await db.GetOneAsync<DocExtractionRule>(x => x.Code == slot.RuleCode)).Data;
            if (rule == null)
            {
                await SetStatusAsync("skipped", "提取规则已失效，已跳过", null);
                return new TaskExecutionResult { Success = true, Message = "提取规则已失效，跳过" };
            }

            // P1 护栏：Prompt 空且字段/表格定义全空 ⇒ 默认提示词也无从构建，不空跑 LLM
            //（Prompt 空但有定义属正常 —— TestFieldWithMarkdownAsync 会用定义构建默认提示词）
            var fDefN = (await db.GetListAsync<DocFieldDef>(x => x.RuleCode == rule.Code)).Data?.Count ?? 0;
            var tDefN = (await db.GetListAsync<DocTableDef>(x => x.RuleCode == rule.Code)).Data?.Count ?? 0;
            if (string.IsNullOrWhiteSpace(rule.Prompt) && fDefN == 0 && tDefN == 0)
            {
                await SetStatusAsync("skipped", "规则未配置 Prompt 且无字段/表格定义，已跳过（请在管理端补全规则）", null);
                return new TaskExecutionResult { Success = true, Message = "规则内容为空，跳过" };
            }

            // ★ 2026-09-30 用户裁决：「文档不能转 markdown = 该文档不能被识别」，有规则就必须提取，
            //   拿不到正文 ⇒ failed，**失败原因 = 解析失败**。与「有正文但 LLM 提不出」区分开：
            //   这里连正文都没有，专家该做的是【重传文件】而不是重试（故 retryable=false）。
            if (string.IsNullOrEmpty(file.MarkdownPath))
            {
                var mdStatus = (file.MarkdownStatus ?? "").Trim().ToLowerInvariant();
                if (mdStatus is "failed" or "unsupported")
                {
                    var parseMsg = mdStatus == "unsupported"
                        ? "文档解析失败：该格式不支持转换为文本，请重传为 docx"
                        : "文档解析失败：转换未成功（文件损坏或内容异常），请重传为 docx";
                    // 转换器有更具体的报错时优先用它（截断，避免 tooltip 过长）
                    var detail = file.MarkdownMessage?.Trim();
                    if (!string.IsNullOrWhiteSpace(detail))
                    {
                        if (detail.Length > 120) detail = detail.Substring(0, 120) + "…";
                        parseMsg = $"文档解析失败：{detail}";
                    }
                    await SetStatusAsync("failed", parseMsg, null);
                    return Fail(parseMsg, retryable: false);
                }

                // 尚未转换（none/pending）：不是解析失败，稍后可重试
                await SetStatusAsync("failed", "正文尚未转换完成，请稍后重试", null);
                return Fail("正文尚未转换完成", retryable: true);
            }

            // ★ processing 不落库（4 态无该值）：队列任务状态已表达「进行中」，
            //   DB 侧保持原状态直至终态（completed / failed / skipped）
            file.ExtractStatus = EnterpriseExtractStatus.None;
            file.ExtractMessage = "提取中";

            // 3. 读 Markdown 产物
            string markdown;
            try
            {
                var (stream, _) = await storage.DownloadAsync(file.MarkdownPath!.TrimStart('/'), cancellationToken);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, cancellationToken);
                markdown = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            }
            catch (Exception ex)
            {
                await SetStatusAsync("failed", $"Markdown 读取失败：{ex.Message}", null);
                return Fail(ex.Message, retryable: true);
            }
            if (string.IsNullOrWhiteSpace(markdown))
            {
                await SetStatusAsync("failed", "Markdown 产物内容为空", null);
                return Fail("Markdown 产物内容为空", retryable: false);
            }

            // 4. LLM 提取（规则已按四元组解析，直传；无规则分支见上，不再从错误串里猜）
            var (ok, error, data) = await ruleService.TestFieldWithMarkdownAsync(rule, markdown, payload.EnterpriseCode);
            if (!ok || data == null)
            {
                await SetStatusAsync("failed", error, null);
                return Fail(error ?? "提取失败", retryable: true);
            }

            // 5. 落 B-08/B-09 企业域（V-P1：归档旧行 + 插新行，VersionNumber = 槽位当前版本）
            var (fieldCount, tableCount) = await ruleService.SaveEnterpriseExtractionResultsAsync(
                rule, data, payload.EnterpriseCode, file.Code ?? "", Math.Max(file.VersionNumber, 1),
                slot.StandardCode, slot.StageCode);

            // 6. 回写状态
            var message = $"提取字段 {fieldCount} 个、表格 {tableCount} 张";
            if (fieldCount == 0 && tableCount == 0)
            {
                await SetStatusAsync("failed", "AI 未提取到任何字段/表格", null);
                return Fail("AI 未提取到任何字段/表格", retryable: false);
            }
            await SetStatusAsync("completed", message, null);

            _logger.LogInformation("[EnterpriseExtract] 完成: {FileCode} ent={Ent} v={Ver} → {Msg}",
                file.Code, payload.EnterpriseCode, file.VersionNumber, message);
            return new TaskExecutionResult { Success = true, Message = message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EnterpriseExtract] 执行失败: {TaskCode}", task.Code);
            return Fail(ex.Message, retryable: true);
        }
    }

    public Task OnTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message)
    {
        _logger.LogInformation("企业提取任务状态变更: {TaskCode} → {Status} ({Message})", task.Code, newStatus, message);
        return Task.CompletedTask;
    }

    private static TaskExecutionResult Fail(string? message, bool retryable) =>
        new() { Success = false, Message = message ?? "提取失败", Retryable = retryable };
}
