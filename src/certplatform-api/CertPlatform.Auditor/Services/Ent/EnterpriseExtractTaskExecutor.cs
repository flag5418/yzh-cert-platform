using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CertPlatform.Admin.Services.DocExtraction;
using CertPlatform.Shared.Constants;
using CertPlatform.Shared.Entities.Dir;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using YzhQueueTask = YZH.Core.Stand.Models.Queue.YzhQueueTask;

namespace CertPlatform.Auditor.Services.Ent;

/// <summary>
/// 企业资料提取任务执行器（06 册 G-2c，TaskType = "doc_extract"）
///
/// <para>链路：读企业行 <c>MarkdownPath</c> → 按该文件 <c>StandardFileCode</c> 的提取规则跑 LLM
/// → 写 B-08/B-09 企业域（真实 VersionNumber；V-P1 甲路线：旧行 IsValid=0 归档不物理删）
/// → 回写 <c>ExtractStatus/MaxConfidence/ExtractMessage</c>。</para>
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

            // 1. 企业文件行（含中间态：转换链会临时置 IsValid=0，这里用 IgnoreValid 口径）
            var file = (await db.GetOneIgnoreValidAsync<StandardDirectoryFile>(
                x => x.Code == payload.Code && x.EnterpriseCode == payload.EnterpriseCode)).Data;
            if (file == null)
                return Fail($"企业文件记录不存在: {payload.Code}", retryable: false);

            async Task SetStatusAsync(string status, string? message, decimal? confidence)
            {
                file.ExtractStatus = status;
                file.ExtractMessage = message;
                file.MaxConfidence = confidence;
                file.UpdateTime = DateTime.Now;
                await db.UpdateAsync(file,
                    nameof(StandardDirectoryFile.ExtractStatus),
                    nameof(StandardDirectoryFile.ExtractMessage),
                    nameof(StandardDirectoryFile.MaxConfidence),
                    nameof(StandardDirectoryFile.UpdateTime));

                // G-3d 留痕：终态一律记 extract_done（含成功/失败，02 号 §六）
                if (status is "completed" or "failed" or "none")
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
                            Detail = JsonSerializer.Serialize(new { status, message, confidence })
                        });
                    }
                    catch (Exception oex) { _logger.LogWarning(oex, "[EnterpriseExtract] op_log 写入失败: {FileCode}", file.Code); }
                }
            }

            if (string.IsNullOrEmpty(file.MarkdownPath))
            {
                await SetStatusAsync("failed", "Markdown 产物不存在，无法提取", null);
                return Fail("Markdown 产物不存在", retryable: false);
            }

            await SetStatusAsync("processing", null, null);

            // 2. 读 Markdown 产物
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

            // 3. LLM 提取（规则键 = 模板文件 Code；未配置规则不算失败，按「跳过」落状态）
            var ruleKey = string.IsNullOrEmpty(file.StandardFileCode) ? file.Code ?? "" : file.StandardFileCode!;
            var (ok, error, data) = await ruleService.TestFieldWithMarkdownAsync(ruleKey, markdown, payload.EnterpriseCode);
            if (!ok || data == null)
            {
                if (error == "该文件未配置提取规则")
                {
                    await SetStatusAsync("none", "该文件未配置提取规则，已跳过", null);
                    return new TaskExecutionResult { Success = true, Message = "无提取规则，跳过" };
                }
                await SetStatusAsync("failed", error, null);
                return Fail(error ?? "提取失败", retryable: true);
            }

            // 4. 落 B-08/B-09 企业域（V-P1：归档旧行 + 插新行，VersionNumber = 槽位当前版本）
            var rule = await ruleService.GetRuleByStandardFileCodeAsync(ruleKey);
            if (rule == null)
            {
                await SetStatusAsync("failed", "提取规则在提取后不可见（并发删除？）", null);
                return Fail("提取规则不存在", retryable: false);
            }
            var (fieldCount, tableCount) = await ruleService.SaveEnterpriseExtractionResultsAsync(
                rule, data, payload.EnterpriseCode, file.Code ?? "", Math.Max(file.VersionNumber, 1));

            // 5. 回写状态
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
