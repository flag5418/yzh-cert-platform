
using CertPlatform.Admin.Services.DocExtraction;
using CertPlatform.Admin.Services.StandardDirectory;
using CertPlatform.Admin.Services.Audit;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Shared.DocExtraction;
using YZH.Core.Api.Services;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CertPlatform.Admin;

public static class CertPlatformAdminServiceExtensions
{
    public static IServiceCollection AddCertPlatformAdminServices(this IServiceCollection services)
    {
        // ──── 标准目录管理 ────
        services.AddScoped<StandardDirectoryService>();
        services.AddScoped<DirectoryTemplateService>();
        services.AddScoped<OfficeConvertService>();

        // ──── 文档提取规则 ────
        services.AddHttpClient();
        services.AddSingleton<DocumentConvertClient>();
        services.AddSingleton<LlmInvokeService>();
        services.AddScoped<DocExtractionRuleService>();
        // S2③：提取结果版本店（写入侧归档同用）
        services.AddScoped<CertPlatform.Admin.Services.DocExtraction.EnterpriseExtractionResultStore>();
        // S1：提取定位链唯一判定器（四元组 + Status=passed + Markdown 就位；执行器/批量端点共用）
        services.AddScoped<ExtractionScopeResolver>();
        // ★ 2026-09-30 裁决 J1/J2/J4：提取值取数唯一口径（RuleCode 收窄 + 人工优先 + IsBlankText 判空）。
        //   ⛔ 守卫（NodeExecutor）与缺口生成（GapDetector）都必须走它，禁止各拼 WHERE。
        services.AddScoped<CertPlatform.Admin.Services.DocExtraction.ExtractionDataResolver>();

        // ──── OCR 能力缝（★ 将来接入 OCR/视觉模型时，**只改这一行**） ────
        // 当前 DefaultOcrProvider 声明"不具备能力"：图片/扫描件不自动提取，
        // 由用户手工定义字段与表格、人工填写。接入时替换为具体实现即可，
        // 转换链 / 提取链 / 前端零改动。详见 IOcrProvider 的 XML 注释。
        services.AddSingleton<IOcrProvider, DefaultOcrProvider>();

        // ──── Prompt 模板管理 ────
        services.AddScoped<PromptTemplateService>();
        // ★ 2026-10-02 提示词工作台（AI 生成草稿 + 上传文件试跑；文件即弃）
        services.AddScoped<PromptWorkbenchService>();

        // ──── 工作流执行引擎 ────
        services.AddScoped<WorkflowConfigParser>();
        services.AddScoped<WorkflowLogger>();
        services.AddScoped<SkillExecutor>();
        services.AddScoped<CertSkillRegistry>();
        services.AddScoped<ISkillRegistry>(sp => sp.GetRequiredService<CertSkillRegistry>());
        services.AddScoped<LlmExtractSkill>();
        services.AddScoped<ISkillNode>(sp => sp.GetRequiredService<LlmExtractSkill>());
        services.AddScoped<AiNodeExecutor>();
        services.AddScoped<NodeExecutor>();
        services.AddScoped<WorkflowInterpreter>();
        services.AddScoped<TaskCacheService>();
        services.AddScoped<WfExecutionTaskService>();

        // ──── 队列相关（单例，executor/notifier/handler 必须为单例） ────
        services.AddSingleton<OfficeConvertTaskExecutor>();
        services.AddSingleton<CertQueueNotifier>();
        services.AddSingleton<UploadQueueCancelHandler>();
        services.AddSingleton<IYzhTaskExecutor>(sp => sp.GetRequiredService<OfficeConvertTaskExecutor>());
        services.AddSingleton<IYzhQueueNotifier>(sp => sp.GetRequiredService<CertQueueNotifier>());
        services.AddSingleton<IYzhQueueCancelHandler>(sp => sp.GetRequiredService<UploadQueueCancelHandler>());

        // ──── 操作日志数据库写入 ────
        // SysLogDbWriter 是 scoped（需要 IDbOrm），DbWriter 回调在调用时创建 scope
        services.AddScoped<SysLogDbWriter>();
        // 注册一个回调工厂，供 Program.cs 注入到 IYzhAuditLogger.DbWriter
        services.AddSingleton<Func<AuditLogEntry, Task>>(sp => async entry =>
        {
            using var scope = sp.CreateScope();
            var writer = scope.ServiceProvider.GetRequiredService<SysLogDbWriter>();
            await writer.WriteAsync(entry);
        });

        return services;
    }
}
