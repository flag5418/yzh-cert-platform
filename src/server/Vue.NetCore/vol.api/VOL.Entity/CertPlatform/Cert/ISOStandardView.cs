using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.CertPlatform.Cert;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// ISO 标准视图模型（V）— 用于列表显示，含字典翻译后的中文字段
    /// 
    /// T+V 架构：
    /// - T = ISOStandard（实体表，用于增删改）
    /// - V = ISOStandardView（视图，用于显示，包含关联字段）
    /// 
    /// 数据来源：v_iso_standard MySQL 视图
    /// </summary>
    [Table("v_iso_standard")]
    public class ISOStandardView : ISOStandard  // 继承 ISOStandard 以支持 Cast
    {
        // ====== 视图特有字段（字典翻译后的中文）======
        // 注意：不要加 [NotMapped]，否则 EF Core 不会从数据库读取这些字段！

        /// <summary>
        /// 分类中文名（质量管理/环境管理/医疗器械等）
        /// </summary>
        public string CategoryName { get; set; }

        /// <summary>
        /// 状态中文名（草稿/已发布/已停用）
        /// </summary>
        public string StatusName { get; set; }
    }
}
