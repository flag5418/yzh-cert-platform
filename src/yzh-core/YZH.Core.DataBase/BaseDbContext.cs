using Microsoft.EntityFrameworkCore;
using YZH.Core.Stand.Models;

namespace YZH.Core.DataBase;

/// <summary>
///     EF Core DbContext 基类
///     业务项目继承此类，通过 OnModelCreating 注册所有实体
/// </summary>
public abstract class BaseDbContext : DbContext
{
    protected BaseDbContext(DbContextOptions options) : base(options) { }

    /// <summary>
    ///     自动注册所有 IEntityConfiguration 实现
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 自动扫描当前程序集中的所有 IEntityConfiguration<T> 实现
        var entityTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(BaseEntity)))
            .ToList();

        foreach (var entityType in entityTypes)
        {
            modelBuilder.Entity(entityType);
        }
    }
}
