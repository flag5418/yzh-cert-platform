using System;
using System.Threading.Tasks;

namespace YZH.Core.CodeRule
{
    /// <summary>
    /// 编码规则引擎接口。
    /// 
    /// 设计原则（对齐 YZH-建设原则-V1.md §2.1 声明式优于命令式）：
    /// - 通过 [YZHCodeRule] 特性声明编码规则，框架在新增时自动生成
    /// - 业务代码无需手动拼接编码，避免重复和冲突
    /// - 保证幂等性和并发安全（分布式环境下不重复、不遗漏）
    /// 
    /// 使用示例：
    /// <code>
    /// [YZHCodeRule(Prefix = "CB", DateFormat = "yyyyMM", SerialLength = 4)]
    /// public class CertificationBody : YZHBaseEntity { }
    /// // 自动生成：CB2026070001, CB2026070002, ...
    /// </code>
    /// 
    /// 线程安全说明：
    /// - 单机环境：使用 lock 保证并发安全
    /// - 分布式环境：使用 Redis SET NX EX 分布式锁（Phase 3 实现）
    /// - 当前版本（Phase 2）仅支持单机环境
    /// 
    /// 幂等性保证：
    /// - 同一配置 + 同一日期 + 同一序号 → 永远生成相同编码
    /// - 即使多次调用 Generate()，只要参数不变，结果不变
    /// - 如果实体已有 Code 且符合规则，不会重新生成（幂等更新）
    /// 
    /// 状态：[TODO:P2] 待 Phase 2 实现基础编码逻辑
    /// 当前版本仅定义接口和配置类，不包含实际生成算法
    /// </summary>
    public interface ICodeRule
    {
        /// <summary>
        /// 按规则生成业务编码（同步版本）
        /// 
        /// 幂等性保证：
        /// - 相同的 config 输入 → 相同的输出
        /// - 线程安全：多线程并发调用不会产生重复编码
        /// 
        /// </summary>
        /// <param name="config">编码规则配置</param>
        /// <returns>生成的业务编码</returns>
        string Generate(CodeRuleConfig config);

        /// <summary>
        /// 按规则生成业务编码（异步版本，用于分布式锁场景）
        /// TODO:P3 - Phase 3 实现 Redis 分布式锁版本
        /// </summary>
        Task<string> GenerateAsync(CodeRuleConfig config);

        /// <summary>
        /// 验证编码是否符合当前规则
        /// 用于数据迁移或手动录入时的校验
        /// </summary>
        bool Validate(string code, CodeRuleConfig config);

        /// <summary>
        /// 解析编码，提取各组成部分
        /// 返回 null 如果编码不符合规则
        /// </summary>
        CodeRuleParseResult Parse(string code, CodeRuleConfig config);
    }

    /// <summary>
    /// 编码规则配置
    /// 定义业务编码的生成模式
    /// </summary>
    public class CodeRuleConfig
    {
        #region 基础配置

        /// <summary>前缀（如 "CB" 表示认证机构，"AP" 表示申请）</summary>
        public string Prefix { get; set; }

        /// <summary>
        /// 日期格式（嵌入编码中）
        /// 常用格式：
        /// - null: 不包含日期
        /// - "yyyy": 年份（4位），如 CB20260001
        /// - "yyyyMM": 年月（6位），如 CB2026070001
        /// - "yyyyMMdd": 年月日（8位），如 CB202607310001
        /// </summary>
        public string DateFormat { get; set; } = "yyyyMM";

        /// <summary>序列号位数（默认 4 位，支持 0000-9999）</summary>
        public int SerialLength { get; set; } = 4;

        #endregion

        #region 序列号重置规则

        /// <summary>
        /// 序列号重置规则
        /// 默认按月重置（每月从 0001 开始）
        /// </summary>
        public SerialResetRule ResetRule { get; set; } = SerialResetRule.Monthly;

        /// <summary>
        /// 序列号起始值（默认 1）
        /// 某些场景可能需要从特定数字开始（如接续旧系统编号）
        /// </summary>
        public int StartSerial { get; set; } = 1;

        #endregion

        #region 高级配置

        /// <summary>
        /// 自定义分隔符（默认无分隔符）
        /// 示例：Separator = "-" → CB-202607-0001
        /// </summary>
        public string Separator { get; set; } = "";

        /// <summary>
        /// 是否包含校验位（默认 false）
        /// 启用后会在末尾追加一位校验码（Mod 11 算法）
        /// TODO:P3 - Phase 3 实现校验位算法
        /// </summary>
        public bool IncludeCheckDigit { get; set; } = false;

        /// <summary>
        /// 关联的实体类型（用于确定序列号的存储键）
        /// 由框架自动设置，通常不需要手动指定
        /// </summary>
        public Type EntityType { get; set; }

        #endregion
    }

    /// <summary>
    /// 序列号重置规则枚举
    /// </summary>
    public enum SerialResetRule
    {
        /// <summary>不重置（全局递增，可能很长）</summary>
        None = 0,
        
        /// <summary>每日重置（如 CB202607310001）</summary>
        Daily = 1,
        
        /// <summary>每月重置（推荐，如 CB2026070001）</summary>
        Monthly = 2,
        
        /// <summary>每年重置（如 CB20260001）</summary>
        Yearly = 3
    }

    /// <summary>
    /// 编码解析结果
    /// 用于 Parse() 方法的返回值
    /// </summary>
    public class CodeRuleParseResult
    {
        /// <summary>是否解析成功</summary>
        public bool Success { get; set; }

        /// <summary>提取的前缀</summary>
        public string Prefix { get; set; }

        /// <summary>提取的日期部分</summary>
        public DateTime? Date { get; set; }

        /// <summary>提取的序列号</summary>
        public int? SerialNumber { get; set; }

        /// <summary>原始编码</summary>
        public string OriginalCode { get; set; }
    }

    /// <summary>
    /// 编码规则特性（声明式配置）
    /// 标记在实体类上，声明该实体的编码生成规则
    /// 
    /// 如果不标注此特性，Code 字段需要手动设置或使用其他机制
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false)]
    public class YZHCodeRuleAttribute : System.Attribute
    {
        /// <summary>编码前缀（必填）</summary>
        public string Prefix { get; set; }

        /// <summary>日期格式（默认 yyyyMM）</summary>
        public string DateFormat { get; set; } = "yyyyMM";

        /// <summary>序列号位数（默认 4）</summary>
        public int SerialLength { get; set; } = 4;

        /// <summary>序列号重置规则（默认 Monthly）</summary>
        public SerialResetRule ResetRule { get; set; } = SerialResetRule.Monthly;

        /// <summary>自定义分隔符</summary>
        public string Separator { get; set; } = "";
    }
}
