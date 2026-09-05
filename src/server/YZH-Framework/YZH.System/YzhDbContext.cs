using System;
using Microsoft.EntityFrameworkCore;
using YZH.System.Entities;

namespace YZH.System
{
    /// <summary>
    /// YZH 系统模块独立数据上下文。
    /// 完全脱离 VOL 的 VOLContext，仅映射系统管理相关表，保证与现有 MySQL 表结构一致。
    /// </summary>
    public class YzhDbContext : DbContext
    {
        public YzhDbContext(DbContextOptions<YzhDbContext> options) : base(options) { }

        public DbSet<SysUser> SysUsers { get; set; }
        public DbSet<SysRole> SysRoles { get; set; }
        public DbSet<SysMenu> SysMenus { get; set; }
        public DbSet<SysDepartment> SysDepartments { get; set; }
        public DbSet<SysDictionary> SysDictionaries { get; set; }
        public DbSet<SysDictionaryList> SysDictionaryLists { get; set; }
        public DbSet<SysRoleAuth> SysRoleAuths { get; set; }
        public DbSet<SysLog> SysLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Sys_Department 主键为 char(36) GUID，非自增，由业务层生成
            modelBuilder.Entity<SysDepartment>(e =>
            {
                e.HasKey(x => x.DepartmentId);
                e.Property(x => x.DepartmentId).HasColumnType("char(36)").HasMaxLength(36).ValueGeneratedNever();
                e.Property(x => x.ParentId).HasColumnType("char(36)").HasMaxLength(36);
            });

            // 整数自增主键显式声明（与 MySQL AUTO_INCREMENT 对应）
            modelBuilder.Entity<SysUser>(e => e.Property(x => x.User_Id).ValueGeneratedOnAdd());
            modelBuilder.Entity<SysRole>(e => e.Property(x => x.Role_Id).ValueGeneratedOnAdd());
            modelBuilder.Entity<SysMenu>(e => e.Property(x => x.Menu_Id).ValueGeneratedOnAdd());
            modelBuilder.Entity<SysDictionary>(e => e.Property(x => x.Dic_ID).ValueGeneratedOnAdd());
            modelBuilder.Entity<SysDictionaryList>(e => e.Property(x => x.DicList_ID).ValueGeneratedOnAdd());
            modelBuilder.Entity<SysRoleAuth>(e => e.Property(x => x.Auth_Id).ValueGeneratedOnAdd());
            modelBuilder.Entity<SysLog>(e => e.Property(x => x.Id).ValueGeneratedOnAdd());
        }
    }
}
