extern alias SharedEntities;

using CertPlatform.Admin.Services.DocExtraction;
using CertPlatform.Admin.Services.StandardDirectory;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Admin.Services.Workflow.Skills;
using SharedEntities::CertPlatform.Shared.DocExtraction;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CertPlatform.Admin;

public static class CertPlatformAdminServiceExtensions
{
    public static IServiceCollection AddCertPlatformAdminServices(this IServiceCollection services)
    {
        // ──── 标准目录管理 ────
        services.AddScoped<CodeGeneratorService>();
        services.AddScoped<StandardDirectoryService>();
        services.AddScoped<DirectoryTemplateService>();
        services.AddScoped<OfficeConvertService>();

        // ──── 文档提取规则 ────
        services.AddHttpClient();
        services.AddSingleton<DocumentConvertClient>();
        services.AddSingleton<LlmInvokeService>();
        services.AddScoped<DocExtractionRuleService>();

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

        return services;
    }
}
