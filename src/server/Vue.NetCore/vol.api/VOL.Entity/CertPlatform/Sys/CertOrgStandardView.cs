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
    /// 注意：不要添加 [Table] 属性，否则会覆盖父类的表名配置
    /// </summary>
    [NotMapped]
    public class CertOrgStandardView : CertOrgStandard
    {
        // ====== 视图特有字段（来自关联的标准信息）======

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
