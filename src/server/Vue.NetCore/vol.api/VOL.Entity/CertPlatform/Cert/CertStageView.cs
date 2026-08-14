using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// CertStage 视图模型（V）— 用于列表显示，含字典翻译后的中文字段
    /// 
    /// 架构设计（T+V 模式）：
    /// - T = CertStage（实体表，用于增删改）
    /// - V = CertStageView（视图，用于显示，包含关联字段）
    /// 
    /// 数据来源：v_cert_stage MySQL 视图（如不存在则用内存翻译）
    /// 注意：不要添加 [Table] 属性，否则会覆盖父类的表名配置
    /// </summary>
    [NotMapped]
    public class CertStageView : CertStage  // 继承 CertStage 以支持 Cast
    {
        // ====== 视图特有字段（字典翻译后的中文）======

        /// <summary>
        /// 分类中文名（流程阶段/审核阶段/证后阶段）
        /// 来源：v_cert_stage 视图的 LEFT JOIN
        /// </summary>
        public string CategoryName { get; set; }

        /// <summary>
        /// 状态中文名（启用/停用）
        /// 来源：v_cert_stage 视图的 LEFT JOIN
        /// </summary>
        public string StatusName { get; set; }
    }
}
