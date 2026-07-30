using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using VOL.Core.CacheManager;
using VOL.Core.Configuration;
using VOL.Core.Enums;
using VOL.Core.Extensions.AutofacManager;
using VOL.Core.ManageUser;
using VOL.Core.ObjectActionValidator;
using VOL.Core.Services;
using VOL.Core.Const;

namespace VOL.Core.Extensions
{
    public static class AutofacContainerModuleExtension
    {
        //  private static bool _isMysql = false;
        public static IServiceCollection AddModule(this IServiceCollection services, IConfiguration configuration)
        {
            //初始化配置文件
            AppSetting.Init(services, configuration);
            Type baseType = typeof(IDependency);
            var compilationLibrary = DependencyContext.Default
                .RuntimeLibraries
                .Where(x => !x.Serviceable
                && x.Type == "project")
                .ToList();
            var count1 = compilationLibrary.Count;
            List<Assembly> assemblyList = new List<Assembly>();

            foreach (var _compilation in compilationLibrary)
            {
                try
                {
                    assemblyList.Add(AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(_compilation.Name)));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(_compilation.Name + ex.Message);
                }
            }
           
            foreach (var _compilation in compilationLibrary)
            {
                var types = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(_compilation.Name)).GetTypes();

                var implementedInterfaces = types.Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Length > 0)
                    .Where(type => baseType.IsAssignableFrom(type) && !type.IsAbstract)
                    .Select(t => (serviceType: t.GetInterfaces(), implementationType: t))
                    .ToList();

                foreach (var (serviceType, implementationType) in implementedInterfaces)
                {
                    if (serviceType.Length == 1 && serviceType.Any(x => x == baseType))
                    {
                        services.AddScoped(implementationType);
                    }
                    else
                    {
                        services.AddScoped(serviceType[0], implementationType);
                      
                    }
                }
            }
            services.AddScoped<UserContext>();
            services.AddScoped<ActionObserver>();
            services.AddScoped<ObjectModelValidatorState>();
            // services.AddScoped<WeChatService>();
            services.AddSingleton(typeof(ICacheService), AppSetting.UseRedis ? typeof(RedisCacheService) : typeof(MemoryCacheService));
            if (DBType.Name == DbCurrentType.PgSql.ToString())
            {
                AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
                AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);
            }
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return services;
        }

    }
}
