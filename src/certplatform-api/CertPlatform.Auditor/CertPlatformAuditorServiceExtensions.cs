using CertPlatform.Auditor.Services;
using CertPlatform.Auditor.Services.Ent;
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

        // ──── 企业资料提取任务执行器（G-2c，TaskType=doc_extract）────
        services.AddSingleton<EnterpriseExtractTaskExecutor>();
        services.AddSingleton<IYzhTaskExecutor>(sp => sp.GetRequiredService<EnterpriseExtractTaskExecutor>());

        return services;
    }
}
