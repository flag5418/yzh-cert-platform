using System;

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

namespace YZH.Entity.Admin.Platform
{
    /// <summary>[UniqueField] 唯一约束特性</summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class UniqueFieldAttribute : Attribute
    {
        public string Description { get; set; }
        public string[] WithFields { get; set; }
        public UniqueFieldAttribute(string description = null) { Description = description; }
    }
}
