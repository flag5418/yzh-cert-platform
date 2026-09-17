using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Entity.Admin.Platform.Sys
{
    /// <summary>
    /// 全局系统参数配置实体
    /// <para>表名：cert_sys_config</para>
    /// <para>列名规范：PascalCase（与 BaseEntity 标准属性名完全一致，无需映射）</para>
    /// 
    /// 覆盖基类审计字段（Id, Code, CreateTime, UpdateTime, IsValid, Sort）
    /// 业务字段：ConfigKey, ConfigValue, ConfigType, Category, DisplayName, Description, IsReadonly
    /// </summary>
    [SugarTable("cert_sys_config")]
    public class SysConfig : BaseEntity
    {
        // ──── 覆盖基类审计字段（DB列名 == 属性名，自动映射） ────

        /// <summary>主键（物理自增 bigint）</summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public new long Id { get; set; }

        /// <summary>业务编码</summary>
        public new string Code { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>创建人 Code</summary>
        public new string? CreateBy { get; set; }

        /// <summary>创建时间</summary>
        public new DateTime CreateTime { get; set; } = DateTime.UtcNow;

        /// <summary>更新人 Code</summary>
        public new string? UpdateBy { get; set; }

        /// <summary>更新时间</summary>
        public new DateTime? UpdateTime { get; set; }

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>有效标志（1=有效，0=无效）</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        /// <summary>备注</summary>
        [MaxLength(500)]
        public string? Remark { get; set; }

        // ──── 系统参数业务字段（PascalCase，自动映射） ────

        /// <summary>配置键（唯一键）</summary>
        [Required]
        [StringLength(100)]
        [UniqueField("配置键")]
        public string ConfigKey { get; set; } = string.Empty;

        /// <summary>配置值</summary>
        [StringLength(4000)]
        public string? ConfigValue { get; set; }

        /// <summary>配置类型（string/int/bool/json）</summary>
        [StringLength(20)]
        public string ConfigType { get; set; } = "string";

        /// <summary>分类</summary>
        [StringLength(50)]
        public string? Category { get; set; }

        /// <summary>显示名称（前端展示用）</summary>
        [StringLength(200)]
        public string? DisplayName { get; set; }

        /// <summary>描述</summary>
        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>是否只读（管理员不可修改）</summary>
        public int IsReadonly { get; set; }
    }
}
