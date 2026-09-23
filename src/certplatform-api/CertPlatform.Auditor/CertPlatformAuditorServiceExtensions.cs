using CertPlatform.Auditor.Services;
using Microsoft.Extensions.DependencyInjection;

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
        // 依赖 IDbOrm / EntityService<T> / PasswordHelper，均由 YzhWebBuilder 注册为 Scoped/Singleton
        services.AddScoped<AuditorRegisterService>();

        return services;
    }
}
