namespace YZH.Core.DeleteStrategy
{
    /// <summary>
    /// 删除策略接口：统一管理软删除/硬删除/级联删除。
    /// 
    /// 设计原则（对齐 YZH-建设原则-V1.md §2.2 约定优于配置）：
    /// - 默认使用逻辑删除（Logical），符合审计合规要求
    /// - 物理删除需要显式声明 [YZHDeleteStrategy(Physical)]
    /// - 通过接口抽象，便于未来扩展自定义删除策略
    /// 
    /// 使用示例：
    /// <code>
    /// // 默认逻辑删除（推荐）
    /// public class CertificationBody : YZHBaseEntity { }
    /// 
    /// // 显式物理删除（如临时文件表）
    /// [YZHDeleteStrategy(Mode = DeleteMode.Physical)]
    /// public class TempFile : YZHBaseEntity { }
    /// 
    /// // 级联删除（如主从表）
    /// [YZHDeleteStrategy(Mode = DeleteMode.Cascade, CascadeEntities = typeof(Detail[]))]
    /// public class Order : YZHBaseEntity { }
    /// </code>
    /// 
    /// 状态：[TODO:P3] 待 Phase 3 实现完整删除策略
    /// 当前版本仅定义接口和枚举，不包含实际删除逻辑
    /// 
    /// 术语说明（统一使用 Logical/Physical，避免 Soft/Hard 混淆）：
    /// - Logical: 逻辑删除（标记 Enable=false + 填充 DeleteTime）
    /// - Physical: 物理删除（直接从数据库移除记录）
    /// - Cascade: 级联删除（删除主表时同时删除关联从表）
    /// </summary>
    public interface IDeleteStrategy
    {
        /// <summary>当前删除模式</summary>
        DeleteMode Mode { get; }

        /// <summary>
        /// 判断是否允许删除该实体
        /// 可用于业务规则校验（如：有未完成审核的申请不允许删除机构）
        /// </summary>
        bool CanDelete(object entityId);

        /// <summary>
        /// 执行删除操作
        /// </summary>
        /// <param name="entity">要删除的实体</param>
        /// <param name="userId">操作人 ID</param>
        /// <param name="userName">操作人姓名</param>
        void ExecuteDelete(object entity, int userId, string userName);
    }

    /// <summary>
    /// 删除模式枚举
    /// 统一术语：Logical / Physical / Cascade
    /// </summary>
    public enum DeleteMode
    {
        /// <summary>
        /// 逻辑删除（默认，推荐）
        /// 行为：设置 Enable = false + 填充 DeleteID/Deleter/DeleteTime
        /// 优点：数据可恢复、符合审计要求、支持数据追溯
        /// 适用场景：90% 的业务表
        /// </summary>
        Logical = 0,

        /// <summary>
        /// 物理删除
        /// 行为：直接从数据库 DELETE 记录
        /// 需要显式使用 [YZHDeleteStrategy(Physical)] 特性声明
        /// 优点：释放存储空间、无残留数据
        /// 缺点：不可恢复、无法追溯
        /// 适用场景：临时文件、日志缓存、明确不需要保留的数据
        /// </summary>
        Physical = 1,

        /// <summary>
        /// 级联删除
        /// 行为：删除主表记录时，同时删除关联的从表记录
        /// 需要配置 CascadeEntities 指定级联实体类型
        /// 注意：级联部分默认使用逻辑删除，除非从表也标记了 Physical
        /// 适用场景：主从表结构（订单-订单明细、申请-附件）
        /// 
        /// TODO:P3 - Phase 3 实现级联删除逻辑
        /// </summary>
        Cascade = 2
    }

    /// <summary>
    /// 删除策略特性（声明式配置）
    /// 标记在实体类上，声明该实体的删除行为
    /// 
    /// 如果不标注此特性，默认使用 Logical（逻辑删除）
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false)]
    public class YZHDeleteStrategyAttribute : System.Attribute
    {
        /// <summary>删除模式（默认 Logical）</summary>
        public DeleteMode Mode { get; set; } = DeleteMode.Logical;

        /// <summary>
        /// 级联删除的实体类型列表（仅 Mode=Cascade 时有效）
        /// TODO:P3 - Phase 3 实现级联配置解析
        /// </summary>
        public Type[] CascadeEntities { get; set; } = null;

        /// <summary>
        /// 是否允许已有关联数据时强制删除（默认 false）
        /// false: 有关联数据时禁止删除（抛出异常）
        /// true: 忽略关联数据，强制删除（慎用！）
        /// </summary>
        public bool ForceDelete { get; set; } = false;

        /// <summary>
        /// 删除前的业务校验方法（可选）
        /// 用于实现复杂的删除前置条件检查
        /// 格式："命名空间.类名+方法名"
        /// 
        /// 示例："CertPlatform.Services.CertificationBodyService+CanDelete"
        /// 方法签名：bool CanDelete(object entityId)
        /// 
        /// TODO:P3 - Phase 3 实现动态方法调用
        /// </summary>
        public string ValidationMethod { get; set; } = null;
    }
}
