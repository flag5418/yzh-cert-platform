using EntityFrameworkCore.UseRowNumberForPaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyModel;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using YZH.Core.Configuration;
using YZH.Core.Const;
using YZH.Core.DBManager;
using YZH.Core.Enums;
using YZH.Core.Extensions;
using YZH.Core.Extensions.AutofacManager;
using YZH.Entity;
using YZH.Entity.SystemModels;

namespace YZH.Core.EFDbContext
{
    public class VOLContext : BaseDbContext, IDependency
    {
        /// <summary>
        /// 数据库连接名称 
        /// </summary>
        public string DataBaseName = null;
        public VOLContext()
                : base()
        {
        }
        public VOLContext(string connction)
            : base()
        {
            DataBaseName = connction;
        }

        public VOLContext(DbContextOptions<VOLContext> options)
            : base(options)
        {

        }
        public override void Dispose()
        {
            base.Dispose();
        }
        public override int SaveChanges()
        {
            try
            {
                return base.SaveChanges();
            }
            catch (Exception ex)//DbUpdateException 
            {
                throw (ex.InnerException as Exception ?? ex);
            }
        }
        public override DbSet<TEntity> Set<TEntity>()
        {
            return base.Set<TEntity>();
        }

        /// <summary>
        /// 设置跟踪状态
        /// </summary>
        public bool QueryTracking
        {
            set
            {
                this.ChangeTracker.QueryTrackingBehavior =
                       value ? QueryTrackingBehavior.TrackAll
                       : QueryTrackingBehavior.NoTracking;
            }
        }
        private static bool UseSqlserver2008 = AppSetting.GetSection("Connection")["UseSqlserver2008"] == "1";
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
            {
                return;
            }
            string connectionString = DBServerProvider.GetConnectionString(null);
            if (Const.DBType.Name == Enums.DbCurrentType.MySql.ToString())
            {
                optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 11)));
            }
            else if (Const.DBType.Name == Enums.DbCurrentType.PgSql.ToString())
            {
                optionsBuilder.UseNpgsql(connectionString);
            }
            else if (Const.DBType.Name == Enums.DbCurrentType.DM.ToString())
            {
                optionsBuilder.UseDm(connectionString);
            }
            else if (Const.DBType.Name == Enums.DbCurrentType.Oracle.ToString())
            {
                optionsBuilder.UseOracle(connectionString,x=>x.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
               // optionsBuilder.UseOracle(connectionString, b => b.UseOracleSQLCompatibility("11"));
            }
            else
            {
                if (UseSqlserver2008)
                {
                    optionsBuilder.UseSqlServer(connectionString, x => x.UseRowNumberForPaging());
                }
                else {
                    optionsBuilder.UseSqlServer(connectionString, o => o.UseCompatibilityLevel(120));
                }   
            }
            //默认禁用实体跟踪
            optionsBuilder = optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
           // optionsBuilder.AddInterceptors(new SqlCommandInterceptor());
            base.OnConfiguring(optionsBuilder);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            Type type = null;
            try
            {
                //获取所有类库
                var compilationLibrary = DependencyContext
                    .Default
                    .RuntimeLibraries
                    .Where(x => !x.Serviceable && x.Type != "package" && (x.Type == "project" || x.Name.StartsWith("VOL.")));
                foreach (var _compilation in compilationLibrary)
                {
                    //加载指定类
                    Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(_compilation.Name));

                    assembly.GetTypes()
                    .Where(x =>
                        typeof(BaseEntity).IsAssignableFrom(x)
                        && x != typeof(BaseEntity)
                        && !x.IsAbstract
                        && x.GetCustomAttribute<NotMappedAttribute>() == null)
                        .ToList().ForEach(t =>
                        {
                            // 只有标记了对应 DBServer 的实体才加入此上下文
                            var entityAttr = t.GetCustomAttribute<EntityAttribute>();
                            if (entityAttr == null || string.IsNullOrEmpty(entityAttr.DBServer) || entityAttr.DBServer == nameof(VOLContext))
                            {
                                modelBuilder.Entity(t);
                            }
                        });
                }

                //Oracle数据库指定表名与列名全部大写
                if (DBType.Name == DbCurrentType.Oracle.ToString() || DBType.Name == DbCurrentType.DM.ToString())
                {
                    foreach (var entity in modelBuilder.Model.GetEntityTypes())
                    {
                        string tableName = entity.GetTableName().ToUpper();
                        {
                            entity.SetTableName(entity.GetTableName().ToUpper());
                            foreach (var property in entity.GetProperties())
                            {
                                property.SetColumnName(property.Name.ToUpper());
                                if (property.ClrType == typeof(Guid))
                                {
                                    property.SetValueConverter(new ValueConverter<Guid, string>(v => v.ToString(), v => new Guid(v)));
                                }
                                else if (property.ClrType == typeof(Guid?))
                                {
                                    property.SetValueConverter(new ValueConverter<Guid?, string>(v => v.ToString(), v => new Guid(v)));
                                }
                            }
                        }
                    }
                }

                base.OnModelCreating(modelBuilder);

                // ===== YZH ViewName 路由 =====
                // 说明：视图查询通过 FromSqlInterpolated 动态路由，不需要在注册表中映射。
                // 实体类的 [ViewName("xxx")] 标记在运行时由 ViewNameExtensions.UseViewIfExists() 检测。
                // SaveChanges 始终针对 [Table] 映射的物理表，[NotMapped] 字段自动忽略。
            }
            catch (Exception ex)
            {
                string mapPath = ($"Log/").MapPath();
                Utilities.FileHelper.WriteFile(mapPath,
                    $"syslog_{DateTime.Now.ToString("yyyyMMddHHmmss")}.txt",
                    type?.Name + "--------" + ex.Message + ex.StackTrace + ex.Source);
            }

        }
    }
}
