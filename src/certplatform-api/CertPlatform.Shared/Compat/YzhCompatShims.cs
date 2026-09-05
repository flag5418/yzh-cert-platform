using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YZH.Entity.SystemModels
{
    /// <summary>YZH 系统空基类（兼容 Vol SystemModels.BaseEntity）</summary>
    public class BaseEntity { }
}

namespace YZH.Entity.Admin.Platform
{
    /// <summary>
    /// YZH 业务实体基类存根
    /// 提供所有业务实体的审计字段骨架；使用 [NotMapped] 默认不映射
    /// 业务实体表按需通过 new + [Column("xxx")] 显式覆盖
    /// </summary>
    public abstract class EntityBase : YZH.Entity.SystemModels.BaseEntity
    {
        [NotMapped]
        public virtual object? IdObj { get; set; }

        // 默认全部 NotMapped，具体实体按需覆盖
        [NotMapped]
        [MaxLength(100)]
        public virtual string? Code { get; set; }

        [NotMapped]
        public virtual int? CreateID { get; set; }

        [NotMapped]
        [MaxLength(50)]
        public virtual string? Creator { get; set; }

        [NotMapped]
        public virtual DateTime? CreateDate { get; set; }

        [NotMapped]
        public virtual int? ModifyID { get; set; }

        [NotMapped]
        [MaxLength(50)]
        public virtual string? Modifier { get; set; }

        [NotMapped]
        public virtual DateTime? ModifyDate { get; set; }

        [NotMapped]
        public virtual int? DeleteID { get; set; }

        [NotMapped]
        [MaxLength(50)]
        public virtual string? Deleter { get; set; }

        [NotMapped]
        public virtual DateTime? DeleteTime { get; set; }

        [NotMapped]
        [MaxLength(50)]
        public virtual string? Status { get; set; }

        [NotMapped]
        public virtual bool Enable { get; set; }

        [NotMapped]
        [MaxLength(500)]
        public virtual string? Remark { get; set; }
    }

    /// <summary>基类别名（兼容 Vol 命名）</summary>
    public abstract class BaseEntity : EntityBase { }

    /// <summary>[UniqueField] 唯一约束特性</summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class UniqueFieldAttribute : Attribute
    {
        public string Description { get; set; }
        public string[] WithFields { get; set; }
        public UniqueFieldAttribute(string description = null) { Description = description; }
    }
}

namespace YZH.Entity.Admin.Platform.Base
{
    /// <summary>YZHBaseEntity 兼容存根</summary>
    public abstract class YZHBaseEntity : YZH.Entity.Admin.Platform.EntityBase { }
}

namespace YZH.Entity
{
    /// <summary>[Entity] 表映射特性</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class EntityAttribute : Attribute
    {
        public string TableName { get; set; }
        public string TableCnName { get; set; }
        public string DBServer { get; set; }
        public bool CurrentUserPermission { get; set; }
    }

    /// <summary>[Editable] 可编辑标记</summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class EditableAttribute : Attribute
    {
        public bool AllowEdit { get; set; } = true;
        public EditableAttribute(bool allowEdit = true) { AllowEdit = allowEdit; }
    }
}
