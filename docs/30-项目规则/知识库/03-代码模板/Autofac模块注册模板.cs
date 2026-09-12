using Autofac;
using Microsoft.Extensions.DependencyInjection;
using YZH.Core;
using YZH.Core.Filters;
using YZH.Core.Idempotent;

namespace vol.api
{
    /// <summary>
    /// Autofac 模块注册模板
    /// 
    /// 使用方式：
    /// 1. 在 vol.api 的 Program.cs 中：
    ///    builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
    ///    builder.Host.ConfigureContainer<ContainerBuilder>(builder =>
    ///    {
    ///        builder.RegisterModule<YZH.Core.YZHModule>();
    ///    });
    /// 
    /// 2. 在 vol.api 的 Startup.cs 中（如果使用 Startup 模式）：
    ///    public void ConfigureContainer(ContainerBuilder builder)
    ///    {
    ///        builder.RegisterModule<YZH.Core.YZHModule>();
    ///    }
    /// 
    /// 注意：
    /// - 必须先安装 Autofac.Extensions.DependencyInjection NuGet 包
    /// - YZH.Core 项目需要引用 Autofac
    /// - YZHModule 注册后，所有 YZH 的服务、Filter、Repository 自动可用
    /// </summary>

    // ============================================================
    // 方式一：Program.cs（.NET 6+ 推荐）
    // ============================================================
    // 在 vol.api/Program.cs 中添加：

    /*
    using Autofac;
    using Autofac.Extensions.DependencyInjection;
    using YZH.Core;

    var builder = WebApplication.CreateBuilder(args);

    // 1. 替换默认 DI 容器为 Autofac
    builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

    // 2. 注册 YZH 模块
    builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
    {
        containerBuilder.RegisterModule<YZHModule>();
    });

    // 3. 注册 YZH 全局 Filter（注意顺序）
    builder.Services.AddControllers(options =>
    {
        // YZH Filter 应在 Vol 权限 Filter 之前
        options.Filters.Add<YZHIdempotentActionFilter>(int.MinValue + 100);
        options.Filters.Add<YZHGlobalExceptionFilter>();
    });

    var app = builder.Build();
    app.Run();
    */

    // ============================================================
    // 方式二：Startup.cs（传统模式）
    // ============================================================

    /*
    using Autofac;
    using YZH.Core;
    using YZH.Core.Filters;

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // ... Vol 原有服务注册 ...

            services.AddControllers(options =>
            {
                options.Filters.Add<YZHIdempotentActionFilter>(int.MinValue + 100);
                options.Filters.Add<YZHGlobalExceptionFilter>();
            });
        }

        // Autofac 容器配置（方法名固定为 ConfigureContainer）
        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterModule<YZHModule>();
        }
    }
    */
}
