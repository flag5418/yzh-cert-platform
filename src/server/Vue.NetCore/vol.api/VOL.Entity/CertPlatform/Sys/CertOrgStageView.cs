using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Sys
{
    /// <summary>
    /// 机构-阶段关联视图模型（V）— 用于列表显示，含阶段信息
    /// 
    /// T+V 架构：
    /// - T = CertOrgStage（实体表，用于增删改）
    /// - V = CertOrgStageView（视图，用于显示，包含阶段信息）
    /// 
    /// 数据来源：v_cert_org_stage MySQL 视图
    /// </summary>
    [Table("v_cert_org_stage")]
    public class CertOrgStageView : CertOrgStage
    {
        // ====== 视图特有字段（来自关联的阶段信息和字典翻译）======
        // 注意：不要加 [NotMapped]，否则 EF Core 不会从数据库读取这些字段！

        /// <summary>
        /// 阶段名称（cert_cert_stage.StageName）
        /// </summary>
        public string StageName { get; set; }

        /// <summary>
        /// 排序号（cert_cert_stage.SortOrder）
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// 分类中文名（字典翻译）
        /// </summary>
        public string CategoryName { get; set; }

        /// <summary>
        /// 状态中文名（字典翻译）
        /// </summary>
        public string StatusName { get; set; }
    }
}
