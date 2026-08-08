using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Sys
{
    /// <summary>
    /// 机构-标准关联视图模型（V）— 用于列表显示，含标准信息
    /// 
    /// T+V 架构：
    /// - T = CertOrgStandard（实体表，用于增删改）
    /// - V = CertOrgStandardView（视图，用于显示，包含标准信息）
    /// 
    /// 数据来源：v_cert_org_standard MySQL 视图
    /// </summary>
    [Table("v_cert_org_standard")]
    public class CertOrgStandardView : CertOrgStandard
    {
        // ====== 视图特有字段（来自关联的标准信息）======
        // 注意：不要加 [NotMapped]，否则 EF Core 不会从数据库读取这些字段！

        /// <summary>
        /// 标准编号
        /// </summary>
        public string StandardCode { get; set; }

        /// <summary>
        /// 标准名称
        /// </summary>
        public string StandardName { get; set; }

        /// <summary>
        /// 版本年份
        /// </summary>
        public int VersionYear { get; set; }

        /// <summary>
        /// 分类中文名
        /// </summary>
        public string CategoryName { get; set; }

        /// <summary>
        /// 状态中文名
        /// </summary>
        public string StatusName { get; set; }
    }
}
