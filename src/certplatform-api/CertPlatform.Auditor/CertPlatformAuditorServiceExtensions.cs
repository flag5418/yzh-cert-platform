using CertPlatform.Auditor.Services;
using CertPlatform.Auditor.Services.Ent;
using CertPlatform.Auditor.Services.Expert;
using Microsoft.Extensions.DependencyInjection;
using YZH.Core.Stand.Interfaces;

namespace CertPlatform.Auditor;

/// <summary>
/// 专家端业务服务自注册
///
/// <para>与 <c>CertPlatformAdminServiceExtensions</c> 对称：启动工程（<c>YZH.Core.Web/Program.cs</c>）
/// 只调用本扩展方法，不感知具体业务实现。</para>
/// </summary>
public static class CertPlatformAuditorServiceExtensions
{
    public static IServiceCollection AddCertPlatformAuditorServices(this IServiceCollection services)
    {
        // ──── 专家注册 ────
        services.AddScoped<AuditorRegisterService>();

        // ──── 工作区上下文解析 ────
        services.AddScoped<WorkspaceContextService>();

        // ──── 企业资料服务 ────
        services.AddScoped<EnterpriseFileService>();
        // ──── 提取结果版本店（S2③：归档/回活/读取唯一口，行数只翻 IsValid 不删）────
        services.AddScoped<CertPlatform.Admin.Services.DocExtraction.EnterpriseExtractionResultStore>();

        // ──── 企业资料提取任务执行器（G-2c，TaskType=doc_extract）────
        services.AddSingleton<EnterpriseExtractTaskExecutor>();
        services.AddSingleton<IYzhTaskExecutor>(sp => sp.GetRequiredService<EnterpriseExtractTaskExecutor>());

        // ════════════════════════════════════════════════════════════════
        // ──── 专家任务系统（任务中心 / NC 结果 / 报告结论）────
        // ════════════════════════════════════════════════════════════════

        // 任务核心服务（建 → 找 → 跑队列；D36 业务锁 / 创建事务 / 范围解析）
        services.AddScoped<ExpertTaskService>();

        // ★ 2026-09-30 补录清单生成器（R \ H 差集 · 裁决 J1）。
        //   依赖 Admin 层的 ExtractionDataResolver（取数唯一口径）—— ⛔ 不得另写一份判空。
        services.AddScoped<GapDetector>();

        // ★ 人工补录服务（写提取结果表 + change_log 留痕 · 裁决 J2/J3/J4）
        services.AddScoped<GapFillService>();

        // 队列项执行器（★ 单例：被单例 BackgroundService 通过 scope 解析时，
        //   实际按 Scoped 注册亦可，但注册为 Singleton 可避免每次执行重复构造）
        services.AddSingleton<ExpertNcCheckExecutor>();
        services.AddSingleton<IExpertItemExecutor>(sp => sp.GetRequiredService<ExpertNcCheckExecutor>());
        services.AddSingleton<ExpertReportExecutor>();
        services.AddSingleton<IExpertItemExecutor>(sp => sp.GetRequiredService<ExpertReportExecutor>());

        // 专家任务队列后台 Worker（★ 专用队列表，D10：不复用 yzh_queue）
        services.AddHostedService<ExpertTaskQueueRunner>();

        return services;
    }
}
