using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Sys
{
    /// <summary>
    /// 全局系统参数配置实体
    /// <para>表名：cert_sys_config</para>
    /// <para>列名规范：PascalCase（与 BaseEntity 标准属性名完全一致，无需映射）</para>
    /// </summary>
    [SugarTable("cert_sys_config")]
    public class SysConfig : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id 已由 BaseEntity 基类统一提供 ────

        // ──── 接口字段（BaseEntity 不包含，由接口继承提供） ────

        /// <summary>有效标志（1=有效，0=无效）</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记（false=正常，true=已删除）</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code（仅软删除时赋值）</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间（仅软删除时赋值）</summary>
        public DateTime? DeleteTime { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        /// <summary>备注</summary>
        [MaxLength(500)]
        public string? Remark { get; set; }

        // ──── 系统参数业务字段 ────

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
