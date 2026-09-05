using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace YZH.System
{
    /// <summary>
    /// YZH 系统模块服务注册（独立于 VOL，直接接入 YZH.WebApi）。
    /// 使用项目自身连接字符串与 SystemDbContext，不依赖 VOLContext。
    /// </summary>
    public static class SystemBuilder
    {
        public static void AddYzhSystem(this IServiceCollection services, IConfiguration configuration)
        {
            var connStr = configuration["Connection:DbConnectionString"];
            if (string.IsNullOrEmpty(connStr))
                throw new InvalidOperationException("未找到 Connection:DbConnectionString 配置");

            services.AddDbContext<SystemDbContext>(options =>
                options.UseMySql(connStr, new MySqlServerVersion(new Version(8, 0, 11))));

            services.AddScoped(typeof(DomainService<>));
        }
    }
}
