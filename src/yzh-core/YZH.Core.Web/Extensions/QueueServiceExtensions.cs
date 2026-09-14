using Microsoft.Extensions.DependencyInjection;
using YZH.Core.DataBase.Services;
using YZH.Core.Stand.Interfaces;

namespace YZH.Core.Web.Extensions;

/// <summary>
/// 队列引擎 DI 注册扩展
/// </summary>
public static class QueueServiceExtensions
{
    public static IServiceCollection AddYzhQueue(this IServiceCollection services)
    {
        // QueueManager 作为单例（内部通过 IServiceProvider 按需创建 scope）
        services.AddSingleton<QueueManager>();

        // QueueHostedService 后台 Worker
        services.AddHostedService<QueueHostedService>();

        // IYzhTaskExecutor 实现由业务层注册（AddScoped<IYzhTaskExecutor, XxxTaskExecutor>()）
        // IYzhQueueNotifier 实现由业务层注册
        // IYzhQueueCancelHandler 实现由业务层注册

        return services;
    }
}
