using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using VOL.Core.Configuration;
using VOL.Core.DbSqlSugar;

namespace VOL.Core.DBManager
{
    public static class SqlSugarRegister
    {

        /// <summary>
        ///系统库链接
        /// </summary>
        /// <returns></returns>
        public static ConnectionConfig GetSysConnectionConfig()
        {
            var dbType = DbManger.GetDbType();
            return new ConnectionConfig()
            {
                DbType = dbType,// SqlSugar.DbType.SqlServer,
                ConnectionString = DBServerProvider.GetConnectionString(null),
                IsAutoCloseConnection = true,
                ConfigId = "default",
                MoreSettings = new ConnMoreSettings()
                {
                    PgSqlIsAutoToLower = false,
                    IsAutoToUpper = IsAutoToUpper(dbType)
                },
                ConfigureExternalServices = GetConfigureExternalServices(dbType),
            };
        }


        public static IServiceCollection UseSqlSugar(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            var dbType = DbManger.GetDbType();
            //缓存所有配置文件的中的数据库链接
            var configs = new List<ConnectionConfig>() { };

            services.AddSingleton<ISqlSugarClient>(s =>
            {
                SqlSugarScope sqlSugar = new SqlSugarScope(
                 GetSysConnectionConfig(),
               //这里自定义数据库链接
               //new List<ConnectionConfig>()
               //{
               //   sysConfig,
               //    new ConnectionConfig(){
               //    DbType = dbType,// SqlSugar.DbType.SqlServer,
               //    ConnectionString = DBServerProvider.SysConnectingString,
               //    IsAutoCloseConnection = true,
               //    ConfigId ="名字"// typeof(SysDbContext).Name,
               //  },

               //},
               db =>
               {
                   //单例参数配置，所有上下文生效
                   db.Aop.OnLogExecuting = (sql, pars) =>
                   {
                       if (AppSetting.ShowSqlLog)
                       {
                           Console.Write(sql);
                       }
                   };

               });
                return sqlSugar;
            });
            return services;
        }
        private static bool IsAutoToUpper(DbType dbType)
        {
            return dbType == DbType.Dm || dbType == DbType.Oracle;
        }
        /// <summary>
        /// 设置字段全大写
        /// </summary>
        /// <returns></returns>
        private static ConfigureExternalServices GetConfigureExternalServices(DbType dbType)
        {
            //https://www.donet5.com/Home/Doc?typeId=1182
            return new ConfigureExternalServices()
            {
                EntityNameService = (type, entityInfo) => { },
                EntityService = (property, column) =>
                {
                    if (IsAutoToUpper(dbType))
                    {
                        column.DbColumnName = property.Name.ToUpper();
                        //这里限制的Oralce数据库，DM数据库也会执行？
                        if (dbType == DbType.Oracle && column.PropertyInfo.PropertyType == typeof(int)
                           && property.DeclaringType.Name.StartsWith("Sys_"))
                        {
                            //oralce系统表设置自增
                            if (column.PropertyInfo.GetCustomAttribute<KeyAttribute>() != null)
                            {
                                column.IsIdentity = false;
                                column.OracleSequenceName = $"T_{property.DeclaringType.Name.ToUpper()}_SEQ";
                            }
                        }
                    }
                }
            };
        }
    }
}
