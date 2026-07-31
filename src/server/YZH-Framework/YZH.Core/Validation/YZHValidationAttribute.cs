using System;

namespace YZH.Core.Validation
{
    /// <summary>
    /// 声明式校验特性基类。
    /// 
    /// 设计原则（对齐 YZH-建设原则-V1.md §2.1 声明式优于命令式）：
    /// - 通过特性声明校验规则，替代手写 if-throw 代码
    /// - 框架在保存前自动执行所有校验，统一异常格式
    /// - 支持组合多个校验规则，实现复杂业务校验逻辑
    /// 
    /// 使用示例：
    /// <code>
    /// public class CertificationBody : YZHBaseEntity
    /// {
    ///     [YZHRequired("机构名称不能为空")]
    ///     public string Name { get; set; }
    ///     
    ///     [YZHUnique("统一社会信用代码已存在")]
    ///     public string CreditCode { get; set; }
    ///     
    ///     [YZHLength(50, "机构简称不能超过50个字符")]
    ///     public string ShortName { get; set; }
    /// }
    /// </code>
    /// 
    /// 与 DataAnnotations 的关系：
    /// - YZH 校验特性是对 DataAnnotations 的增强和补充
    /// - 基础校验（必填、长度、范围）优先使用 DataAnnotations
    /// - YZH 提供业务级校验（唯一性、复杂条件、跨字段关联）
    /// - 两者可以混用，框架会合并执行
    /// 
    /// 状态：[TODO:P2] 待 Phase 2 实现完整校验体系
    /// 当前版本仅定义抽象基类，具体实现在 Phase 2 开发
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public abstract class YZHValidationAttribute : Attribute
    {
        #region 基础配置

        /// <summary>
        /// 校验失败时的错误消息
        /// 支持 {fieldName} 占位符，会被替换为实际字段名
        /// 示例："{fieldName}不能为空" → "机构名称不能为空"
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 校验优先级（数值越小越先执行）
        /// 默认值：100
        /// 
        /// 用途：
        /// - 必填校验优先级最高（0-20）
        /// - 格式校验次之（21-50）
        /// - 业务校验最后（51-100）
        /// - 这样可以在第一个错误时就快速失败，避免无谓的后续校验
        /// </summary>
        public int Priority { get; set; } = 100;

        /// <summary>
        /// 校验分组（用于按场景启用/禁用部分校验）
        /// 默认值：null（所有场景都生效）
        /// 
        /// 典型场景：
        /// - "Create": 仅在新增时校验
        /// - "Update": 仅在更新时校验
        /// - "Submit": 仅在提交审核时校验
        /// - null 或 "All": 所有场景都校验
        /// 
        /// TODO:P2 - Phase 2 实现分组校验逻辑
        /// </summary>
        public string Group { get; set; } = null;

        #endregion

        #region 抽象方法

        /// <summary>
        /// 执行校验逻辑（由子类实现）
        /// </summary>
        /// <param name="value">字段的当前值</param>
        /// <param name="fieldName">字段名称（自动填充）</param>
        /// <param name="entity">整个实体实例（用于跨字段校验）</param>
        /// <returns>校验结果（成功返回 null，失败返回错误信息）</returns>
        public abstract ValidationResult Validate(object value, string fieldName, object entity);

        #endregion
    }

    /// <summary>
    /// 校验结果
    /// </summary>
    public class ValidationResult
    {
        /// <summary>是否校验通过</summary>
        public bool IsValid { get; set; }

        /// <summary>错误消息（IsValid=false 时有值）</summary>
        public string ErrorMessage { get; set; }

        /// <summary>校验失败的字段名</summary>
        public string FieldName { get; set; }

        /// <summary>校验失败的优先级</summary>
        public int Priority { get; set; }

        /// <summary>创建成功结果</summary>
        public static ValidationResult Success() => new() { IsValid = true };

        /// <summary>创建失败结果</summary>
        public static ValidationResult Fail(string errorMessage, string fieldName, int priority = 100)
            => new() { IsValid = false, ErrorMessage = errorMessage, FieldName = fieldName, Priority = priority };
    }

    // ============================================================
    // 内置校验特性（Phase 2 实现，此处仅声明接口）
    // ============================================================

    /// <summary>
    /// 必填校验
    /// TODO:P2 - Phase 2 实现
    /// 比 [Required] 更强大，支持条件必填（如：当状态=已审核时，审核意见必填）
    /// </summary>
    public class YZHRequiredAttribute : YZHValidationAttribute
    {
        /// <summary>条件表达式（可选，为 null 时表示无条件必填）</summary>
        public string Condition { get; set; } = null;

        public override ValidationResult Validate(object value, string fieldName, object entity)
        {
            // TODO:P2 - Phase 2 实现必填校验逻辑
            throw new System.NotImplementedException();
        }
    }

    /// <summary>
    /// 唯一性校验
    /// TODO:P2 - Phase 2 实现
    /// 检查字段值在数据库中是否已存在（排除自身）
    /// </summary>
    public class YZHUniqueAttribute : YZHValidationAttribute
    {
        /// <summary>联合唯一的其他字段（可选，用于多字段联合唯一）</summary>
        public string[] WithFields { get; set; } = null;

        public override ValidationResult Validate(object value, string fieldName, object entity)
        {
            // TODO:P2 - Phase 2 实现唯一性校验逻辑（需要查询数据库）
            throw new System.NotImplementedException();
        }
    }

    /// <summary>
    /// 长度校验
    /// TODO:P2 - Phase 2 实现
    /// 支持 Min/Max/Exact 三种模式
    /// </summary>
    public class YZHLengthAttribute : YZHValidationAttribute
    {
        public int MaximumLength { get; set; }
        public int? MinimumLength { get; set; }

        public override ValidationResult Validate(object value, string fieldName, object entity)
        {
            // TODO:P2 - Phase 2 实现长度校验逻辑
            throw new System.NotImplementedException();
        }
    }

    /// <summary>
    /// 正则表达式校验
    /// TODO:P2 - Phase 2 实现
    /// 预置常用正则：手机号、邮箱、身份证、统一社会信用代码等
    /// </summary>
    public class YZHRegexAttribute : YZHValidationAttribute
    {
        public string Pattern { get; set; }

        /// <summary>预置模式名称（如 "MobilePhone"、"Email"、"IdCard"）</summary>
        public string PredefinedPattern { get; set; } = null;

        public override ValidationResult Validate(object value, string fieldName, object entity)
        {
            // TODO:P2 - Phase 2 实现正则校验逻辑
            throw new System.NotImplementedException();
        }
    }

    /// <summary>
    /// 范围校验（数值/日期）
    /// TODO:P2 - Phase 2 实现
    /// </summary>
    public class YZHRangeAttribute : YZHValidationAttribute
    {
        public object Minimum { get; set; }
        public object Maximum { get; set; }

        public override ValidationResult Validate(object value, string fieldName, object entity)
        {
            // TODO:P2 - Phase 2 实现范围校验逻辑
            throw new System.NotImplementedException();
        }
    }
}
