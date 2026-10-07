using CertPlatform.Auditor.Services;
using CertPlatform.Auditor.Services.Ent;
using CertPlatform.Auditor.Services.Ent.Normalize;
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

        // ──── 企业原始资料（36 号）────
        //   ⚠️ 与上面 EnterpriseFileService 是**两套东西**（输入库 vs 输出库、标准无关 vs 标准槽位），
        //      见 36 号 §2.1 判别表。切勿合并。
        services.AddScoped<EnterpriseOriginalService>();
        // ★ TaskType = enterprise_original_ingest（格式归一 + 双产物）⚠️ 2026-10-06 起不再入队，仅被文件级编排器组合调用 + 读历史队列
        services.AddSingleton<EnterpriseOriginalIngestExecutor>();
        services.AddSingleton<IYzhTaskExecutor>(sp => sp.GetRequiredService<EnterpriseOriginalIngestExecutor>());
        // ★ TaskType = enterprise_original_analyze（doc_group + doc_content + 画像 upsert）⚠️ 同上：被文件级编排器组合调用 + 读历史队列
        services.AddSingleton<EnterpriseOriginalAnalyzeExecutor>();
        services.AddSingleton<IYzhTaskExecutor>(sp => sp.GetRequiredService<EnterpriseOriginalAnalyzeExecutor>());
        // ★★ TaskType = enterprise_original_file（2026-10-06 队列重构后**唯一入队口**）：
        //   一个文件 = 一个队列 = 一个任务，任务内串行「转换段 → 分析段」，组合复用上面两个子执行器。
        services.AddSingleton<EnterpriseOriginalFileExecutor>();
        services.AddSingleton<IYzhTaskExecutor>(sp => sp.GetRequiredService<EnterpriseOriginalFileExecutor>());

        // ──── 提取结果版本店（S2③：归档/回活/读取唯一口，行数只翻 IsValid 不删）────
        // ⚠️ EnterpriseExtractionResultStore 只在 Admin 侧注册一次（36 号 A4 修双注册）。
        //    Auditor 通过根容器懒解析取到的是同一个实例。

        // ──── 企业资料提取任务执行器（G-2c，TaskType=doc_extract）────
        services.AddSingleton<EnterpriseExtractTaskExecutor>();
        services.AddSingleton<IYzhTaskExecutor>(sp => sp.GetRequiredService<EnterpriseExtractTaskExecutor>());

        // ════════════════════════════════════════════════════════════════
        // ──── ★ 企业资料规范化（P1 核心链路，`60` §三 第 2 批）────
        // ════════════════════════════════════════════════════════════════

        // ★ 单文件编排器 —— 「企业资料 → 规范化标准文档」的唯一真编排器（七步）。
        //   依赖 IDbOrm / IObjectStorage / SourceResolver（均 Scoped）⇒ Scoped。
        //   ⚠️ 它同时依赖 Admin 的 SourceResolver 与 IAiFillInvoker（后者单例），
        //      这两者已在 AddCertPlatformAdminServices 注册，宿主 Program.cs 两个都调用。
        services.AddScoped<DocumentFillOrchestrator>();

        // ★ 队列执行器（TaskType = enterprise_normalize）—— 单例壳 + 每次 CreateScope。
        //   ⚠️ 漏注册 = 任务静默分发不到（QueueManager 按 TaskType 建字典分发，不报错）。
        //   ⛔ 旧执行器 ent_doc_normalize（Admin 侧）已按 `60` §六 裁决③ 显式停用。
        services.AddScoped<EnterpriseNormalizeExecutor>();
        services.AddSingleton<EnterpriseNormalizeExecutorAdapter>();
        services.AddSingleton<IYzhTaskExecutor>(sp => sp.GetRequiredService<EnterpriseNormalizeExecutorAdapter>());

        // ════════════════════════════════════════════════════════════════
        // ──── 专家任务系统（任务中心 / NC 结果 / 报告结论）────
        // ════════════════════════════════════════════════════════════════

        // 任务核心服务（建 → 找 → 跑队列；D36 业务锁 / 创建事务 / 范围解析）
        services.AddScoped<ExpertTaskService>();

        // ★ 2026-09-30 补录清单生成器（R \ H 差集 · 裁决 J1）。
        //   依赖 Admin 层的 ExtractionDataResolver（取数唯一口径）—— ⛔ 不得另写一份判空。
        services.AddScoped<GapDetector>();

        // ★ 2026-10-07 缺口中文名解析器（「关键信息补录」裁决）。
        //   唯一权威来源 = 文档提取规则页定义的字段/表格（DocFieldDef / DocTableDef /
        //   DocTableFieldDef，均在 Shared）⇒ ⛔ 不再回退英文码。
        services.AddScoped<GapLabelResolver>();

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
