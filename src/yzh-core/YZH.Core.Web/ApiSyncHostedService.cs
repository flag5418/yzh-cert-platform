using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Services;

namespace YZH.Core.Web;

/// <summary>
/// 接口权限同步后台服务
/// 在应用启动时自动执行接口同步
/// TODO: 开发中特性，暂时禁用
/// </summary>
public class ApiSyncHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApiSyncHostedService> _logger;
    
    public ApiSyncHostedService(
        IServiceProvider serviceProvider,
        ILogger<ApiSyncHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // TODO: 开发中特性，暂时禁用
        // _logger.LogInformation("开始同步接口权限...");
        // using var scope = _serviceProvider.CreateScope();
        // var scanner = scope.ServiceProvider.GetRequiredService<ApiScanner>();
        // var syncService = scope.ServiceProvider.GetRequiredService<ApiSyncService>();
        // var apis = scanner.Scan();
        // var result = await syncService.SyncAsync(apis);
        _logger.LogInformation("接口权限同步服务已跳过（开发中特性）");
    }
    
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
