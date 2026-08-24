using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.CertPlatform;

namespace VOL.Entity.CertPlatform.Wf
{
    /// <summary>
    /// WfNodeExecution - 节点执行状态
    /// <para>表名：wf_node_execution</para>
    /// <para>定位：每个节点一次执行的状态记录，是结果复用和断点续跑的核心载体</para>
    /// <para>跨路径复用：同一 item 下同 node_id 已执行 → 读库复用，不重跑</para>
    /// </summary>
    [Table("wf_node_execution")]
    public class WfNodeExecution : YZHBaseEntity
    {
        /// <summary>wf_execution_task.code</summary>
        [Required]
        [MaxLength(36)]
        [UniqueField("任务编码", WithFields = new[] { "ItemCode", "NodeId" })]
        [Column("task_code")]
        public string TaskCode { get; set; }

        /// <summary>wf_execution_task_item.code</summary>
        [Required]
        [MaxLength(36)]
        [Column("item_code")]
        public string ItemCode { get; set; }

        /// <summary>节点ID（前端生成的 classCode_n序号）</summary>
        [Required]
        [MaxLength(64)]
        [Column("node_id")]
        public string NodeId { get; set; }

        /// <summary>节点类型：start|end|skill|ai_node|logic|branch|docField|docTable</summary>
        [MaxLength(30)]
        [Column("node_type")]
        public string NodeType { get; set; }

        /// <summary>节点名称快照</summary>
        [MaxLength(128)]
        [Column("node_title")]
        public string NodeTitle { get; set; }

        /// <summary>Skill编码（功能节点）</summary>
        [MaxLength(64)]
        [Column("skill_code")]
        public string SkillCode { get; set; }

        /// <summary>执行状态：pending|executing|completed|failed|skipped</summary>
        [Required]
        [MaxLength(20)]
        [Column("exec_status")]
        public string ExecStatus { get; set; } = "pending";

        /// <summary>节点输出（所有端口的JSON）</summary>
        [Column("output_json", TypeName = "json")]
        public string OutputJson { get; set; }

        /// <summary>执行错误信息</summary>
        [MaxLength(1000)]
        [Column("error_message")]
        public string ErrorMessage { get; set; }

        /// <summary>开始执行时间</summary>
        [Column("started_at")]
        public DateTime? StartedAt { get; set; }

        /// <summary>完成时间</summary>
        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }

        /// <summary>执行耗时(ms)</summary>
        [Column("execution_time_ms")]
        public int? ExecutionTimeMs { get; set; }

        /// <summary>是否复用了历史结果：0=新执行 1=复用</summary>
        [Column("is_reused")]
        public int IsReused { get; set; } = 0;
    }
}
