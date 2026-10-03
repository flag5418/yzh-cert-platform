using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Entity.Admin.Platform;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Entities.Wf
{
    /// <summary>
    /// WfNodeApproval - 节点审批记录
    /// <para>表名：wf_node_approval</para>
    /// <para>定位：专家对单个节点执行结果的认可/修改，形成可审计的决策轨迹</para>
    /// <para>唯一键 (TaskCode, NodeId) 保证每节点每轮任务只有一条审批记录</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("wf_node_approval")]
    public class WfNodeApproval : BaseEntity, ISoftDelete, IIsValid
    {
        /// <summary>wf_execution_task.Code</summary>
        [Required]
        [MaxLength(36)]
        public string TaskCode { get; set; } = string.Empty;

        /// <summary>节点 ID（与 wf_node_execution.NodeId 对应）</summary>
        [Required]
        [MaxLength(64)]
        public string NodeId { get; set; } = string.Empty;

        /// <summary>审批人编码（Sys_User.Code）</summary>
        [Required]
        [MaxLength(36)]
        public string ApprverCode { get; set; } = string.Empty;

        /// <summary>审批人姓名（冗余，避免联表）</summary>
        [MaxLength(50)]
        public string? ApprverName { get; set; }

        /// <summary>审批状态：pending / approved / rejected</summary>
        [Required]
        [MaxLength(20)]
        public string ApprovalStatus { get; set; } = "pending";

        /// <summary>审批时间</summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>审批意见</summary>
        [SugarColumn(ColumnDataType = "text")]
        public string? Comment { get; set; }

        /// <summary>专家手动修改后的节点输出（JSON；null=未修改）</summary>
        [SugarColumn(ColumnDataType = "json")]
        public string? ManualResult { get; set; }

        /// <summary>专家可信度评分 0.00~1.00</summary>
        public decimal? Confidence { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
