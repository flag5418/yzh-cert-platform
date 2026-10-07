
using CertPlatform.Admin.Services.DocExtraction;
using CertPlatform.Admin.Services.Ent;
using CertPlatform.Admin.Services.Ent.Executors;
using CertPlatform.Admin.Services.StandardDirectory;
using CertPlatform.Admin.Services.Audit;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai;
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
        // ★ 转换内核（36 号 T1.1）：PDF / Markdown / 格式归一 / OCR 兜底的**唯一实现**，
        //   无状态可注册单例。标准目录、企业资料库、企业原始资料三条链共用它 ——
        //   ⛔ 禁止在任何 Service 里再直接调 DocumentConvertClient（那是「状态机抄第二遍」的开端）。
        services.AddSingleton<IFileConvertCore, FileConvertCore>();

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

        // ──── 视觉识别（2026-10-03 从空壳换成真实现）────
        // 图片/扫描件交给视觉模型出 Markdown。★ 判定顺序在 FileConvertCore：
        //   anydoc 能解的直接解（**零 AI 成本**），只有 NeedsOcr（退出码 3 = 无文本层）才落到这里。
        //   ⛔ Provider 必须在 Admin 层 —— 读系统参数 ai_vision_config 需要 IDbOrm，
        //      而 CertPlatform.Shared 不引用 YZH.Core.DataBase（分层约束）。
        //   配置缺失 / 模型不是视觉模型时，VisionOcrProvider 自行返回 NotAvailable（如实报不支持），不抛异常。
        services.AddSingleton<IOcrProvider, VisionOcrProvider>();

        // ──── Prompt 模板管理 ────
        services.AddScoped<PromptTemplateService>();
        // ★ 2026-10-02 提示词工作台（AI 生成草稿 + 上传文件试跑）
        services.AddScoped<PromptWorkbenchService>();
        // ★ 2026-10-02 企业文档 Markdown 复用缓存（Redis，单例持连接；连不上自动降级为「每次重新转换」）
        services.AddSingleton<PromptMarkdownCache>();

        // ──── 工作流执行引擎 ────
        services.AddScoped<WorkflowConfigParser>();
        services.AddScoped<WorkflowLogger>();
        services.AddScoped<SkillExecutor>();
        services.AddScoped<CertSkillRegistry>();
        services.AddScoped<ISkillRegistry>(sp => sp.GetRequiredService<CertSkillRegistry>());
        services.AddScoped<LlmExtractSkill>();
        services.AddScoped<ISkillNode>(sp => sp.GetRequiredService<LlmExtractSkill>());

        // ★ 2026-10-04 AI 填充数据来源（src_semantic / src_ai_field / src_ai_table）的公共件。
        //   AiFillInvoker 是 LlmInvokeService（上面已注册为单例）的薄封装 —— 它自己**不持** IDbOrm
        //   （db 由调用方每次传入），故与 LlmInvokeService 同为单例。
        //   SkillExecutor 通过 [FromService] 从 scope 的 provider 解析它。
        services.AddSingleton<IAiFillInvoker, AiFillInvoker>();

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

        // ⛔⛔ 2026-10-06 停用：企业文档规范化执行器（ent_doc_normalize，Admin 侧旧实现）
        //   依据 `60` §六 裁决③（用户原话「填充代码可以彻底全部重新实现」）+ `60` §六之补二：
        //   旧执行器实测缺 6 处，最致命的是【读错表】—— LoadEnterpriseOriginalDocsMarkdownAsync
        //   方法名「企业原始文档」，实际查 StandardDirectoryFile（企业**成品**文档），
        //   把成品当原始资料喂给填充引擎。
        //   ⇒ 新编排器落在专家端 `CertPlatform.Auditor/Services/Ent/Normalize/DocumentFillOrchestrator.cs`，
        //      队列任务类型 `enterprise_normalize` 由 `CertPlatformAuditorServiceExtensions` 注册。
        //   ⚠️ 两套填充代码并存 = 静默漂移（取值/锁定/产物路径规则各写一套，两边都不报错）
        //      ⇒ 必须显式停用本处注册。类文件保留（供对照），但**不再注册进 DI**。
        //   ⛔ 不要因为「类还在」就把注册加回来。
        // services.AddScoped<EnterpriseDocNormalizationExecutor>();
        // services.AddSingleton<EnterpriseDocNormalizationExecutorAdapter>();
        // services.AddSingleton<IYzhTaskExecutor>(sp => sp.GetRequiredService<EnterpriseDocNormalizationExecutorAdapter>());

        // ★ 2026-10-06（S1-5）来源解析器 —— 改为 DI 注册。
        //   原先执行器里写的是 `new SourceResolver(db)`：手工 new 会让它无法接收
        //   FillParamValueProvider（Shared 层的取值内核），只能自己再 new 一份 ⇒
        //   「同一个取值内核」被实例化出多份，改一处忘一处 = 静默分叉（D1/D6 的成因模式）。
        //   ⚠️ 它依赖 IDbOrm（Scoped）⇒ 必须 Scoped；执行器本体也是 Scoped，生命周期匹配。
        services.AddScoped<SourceResolver>();

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
