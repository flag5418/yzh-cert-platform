using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YZH.Core.Api.Services;

namespace YZH.Core.Web;

/// <summary>
/// 启动时注入操作日志数据库写入回调到 IYzhAuditLogger
/// 必须在所有服务注册完成后运行（IHostedService 保证）
/// </summary>
public class AuditLogDbWriterInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuditLogDbWriterInitializer> _logger;

    public AuditLogDbWriterInitializer(
        IServiceProvider serviceProvider,
        ILogger<AuditLogDbWriterInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var auditLogger = _serviceProvider.GetRequiredService<IYzhAuditLogger>();
        auditLogger.DbWriter = async entry =>
        {
            using var scope = _serviceProvider.CreateScope();
            var writer = scope.ServiceProvider.GetRequiredService<CertPlatform.Admin.Services.Audit.SysLogDbWriter>();
            await writer.WriteAsync(entry);
        };
        _logger.LogInformation("[AuditLogDbWriterInitializer] DbWriter 已注入到 IYzhAuditLogger");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
