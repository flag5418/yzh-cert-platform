using System;

namespace YZH.Core.Audit
{
    /// <summary>
    /// 实体级审计标注特性。标记实体需要审计追踪。
    /// 
    /// 设计原则（对齐 YZH-建设原则-V1.md §2.1 声明式优于命令式）：
    /// - 通过 [YZHAudited] 特性声明"需要审计"，框架自动记录增删改操作
    /// - 业务代码无需手写日志代码，实现零侵入审计
    /// 
    /// 使用示例：
    /// <code>
    /// [YZHAudited(TrackChanges = true, Category = AuditCategory.Certification)]
    /// public class CertificationBody : YZHBaseEntity { }
    /// </code>
    /// 
    /// 状态：[TODO:P2] 待 Phase 2 实现完整审计逻辑
    /// 当前版本仅定义接口和参数，不包含实际审计写入逻辑
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class YZHAuditedAttribute : Attribute
    {
        #region 基础配置

        /// <summary>
        /// 是否记录字段变更（新旧值对比）
        /// 默认值：false（只记录操作，不记录具体变更）
        /// 
        /// TODO:P2 - Phase 2 实现 TrackChanges 逻辑
        /// 启用后，框架会在更新时自动对比新旧实体，记录变更的字段列表
        /// </summary>
        public bool TrackChanges { get; set; } = false;

        /// <summary>
        /// 审计表名（默认使用约定：{EntityName}_AuditLog）
        /// 仅当需要自定义审计表时设置
        /// </summary>
        public string TableName { get; set; } = null; // null 表示使用约定命名

        #endregion

        #region 分类与范围

        /// <summary>
        /// 审计分类（用于区分不同业务域的审计日志）
        /// 默认值：General（通用）
        /// 
        /// 用途：
        /// - 按分类查询审计日志
        /// - 不同分类可配置不同的保留策略
        /// - 权限控制（某些角色只能查看特定分类的日志）
        /// </summary>
        public AuditCategory Category { get; set; } = AuditCategory.General;

        /// <summary>
        /// 审计追踪级别
        /// 默认值：Crud（只追踪增删改操作）
        /// 
        /// 级别说明：
        /// - Crud: 记录谁在什么时候做了什么操作（Insert/Update/Delete）
        /// - Audit: 在 Crud 基础上，增加业务上下文（如审批意见、状态变更原因）
        /// - All: 最详细，包括查询操作和字段级变更
        /// </summary>
        public AuditScope Scope { get; set; } = AuditScope.Crud;

        #endregion

        #region 敏感字段配置

        /// <summary>
        /// 需要脱敏的字段名列表（逗号分隔）
        /// 这些字段在审计日志中会自动脱敏处理
        /// 
        /// 示例："MobilePhone,IDCard,BankAccount"
        /// 
        /// TODO:P2 - Phase 2 实现敏感字段脱敏逻辑
        /// 脱敏规则：手机号 138****1234，身份证 110***********1234
        /// </summary>
        public string SensitiveFields { get; set; } = null;

        /// <summary>
        /// 排除审计的字段名列表（逗号分隔）
        /// 这些字段的变更不会被记录到审计日志
        /// 
        /// 典型场景：
        /// - 排除大文本字段（如 Remark、Content）减少日志体积
        /// - 排除频繁变化的非关键字段（如 LastAccessTime）
        /// </summary>
        public string ExcludeFields { get; set; } = null;

        #endregion
    }

    /// <summary>
    /// 审计分类枚举
    /// 对应体系认证平台的业务域划分
    /// </summary>
    public enum AuditCategory
    {
        /// <summary>通用（未指定分类时默认）</summary>
        General = 0,
        
        /// <summary>认证管理（机构、证书、标准等）</summary>
        Certification = 100,
        
        /// <summary>审核流程（审核任务、审核记录）</summary>
        Audit = 200,
        
        /// <summary>报告生成</summary>
        Report = 300,
        
        /// <summary>系统管理（用户、角色、权限）</summary>
        System = 400,
        
        /// <summary>企业端操作</summary>
        Enterprise = 500
    }

    /// <summary>
    /// 审计追踪级别枚举
    /// </summary>
    public enum AuditScope
    {
        /// <summary>基础 CRUD 操作记录（谁 + 时间 + 操作类型）</summary>
        Crud = 0,
        
        /// <summary>增强版（CRUD + 业务上下文 + 状态变更原因）</summary>
        Audit = 1,
        
        /// <summary>完整版（Audit + 查询操作 + 字段级变更 diff）</summary>
        All = 2
    }
}
