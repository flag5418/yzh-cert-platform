using System;

namespace YZH.Entity.Admin.Platform
{
    /// <summary>
    /// 唯一字段标记特性。
    /// 
    /// 标记在实体属性上，声明该字段值在数据库中必须唯一。
    /// 由通用校验方法 <see cref="YZH.Builder.Extensions.UniqueValidationExtensions"/> 在保存前自动检查。
    /// 
    /// 设计原则：
    /// - 声明式：只需在属性上加特性，无需在 Service 中手写校验代码
    /// - 通用性：所有继承 EntityBase 的实体均可使用
    /// - 支持联合唯一：通过 WithFields 指定联合唯一的其他字段
    /// 
    /// 使用示例：
    /// <code>
    /// public class CertificationBody : EntityBase
    /// {
    ///     [UniqueField("机构编号")]
    ///     [Column("cb_code")]
    ///     public string CbCode { get; set; }
    /// 
    ///     [UniqueField("机构名称")]
    ///     [Column("name")]
    ///     public string Name { get; set; }
    /// 
    ///     // 联合唯一示例：同一机构下编号不能重复
    ///     [UniqueField("阶段编号", WithFields = new[] { "OrgCode" })]
    ///     [Column("phase_code")]
    ///     public string PhaseCode { get; set; }
    /// }
    /// </code>
    /// 
    /// 放在 YZH.Entity 中而非 YZH.Core，因为 YZH.Entity 不能引用 YZH.Core（循环依赖）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class UniqueFieldAttribute : Attribute
    {
        /// <summary>
        /// 字段中文名（用于生成友好的错误提示消息）
        /// 如果为 null，则使用属性上的 [Display(Name=...)] 特性值
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 联合唯一的其他字段属性名列表（可选）。
        /// 
        /// 用于多字段联合唯一场景。例如：
        /// [UniqueField("阶段编号", WithFields = new[] { "OrgCode", "StandardCode" })]
        /// 表示在同一个 OrgCode + StandardCode 下，PhaseCode 不能重复。
        /// 
        /// 值为属性名（PascalCase），不是数据库列名。
        /// </summary>
        public string[] WithFields { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="description">字段中文名，用于错误提示</param>
        public UniqueFieldAttribute(string description = null)
        {
            Description = description;
        }
    }
}
