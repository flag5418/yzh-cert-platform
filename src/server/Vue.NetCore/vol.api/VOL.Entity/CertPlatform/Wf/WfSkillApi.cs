using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Wf
{
    /// <summary>
    /// WfSkillApi — api 型 Skill 信息（自定义工作流引擎 V1.2 §5.6，1:1，预留）
    /// <para>表名：wf_skill_api</para>
    /// <para>本期仅页面维护，执行由 HttpApiSkillNode 后续实现</para>
    /// </summary>
    [Table("wf_skill_api")]
    public class WfSkillApi : YZHBaseEntity
    {
        // ===== snake_case 审计字段覆盖 =====
        [Column("create_id")] public new int? CreateID { get; set; }
        [Column("creator")] [MaxLength(50)] public new string Creator { get; set; }
        [Column("create_date")] public new DateTime? CreateDate { get; set; } = DateTime.Now;
        [Column("modify_id")] public new int? ModifyID { get; set; }
        [Column("modifier")] [MaxLength(50)] public new string Modifier { get; set; }
        [Column("modify_date")] public new DateTime? ModifyDate { get; set; }
        [Column("delete_id")] public new int? DeleteID { get; set; }
        [Column("deleter")] [MaxLength(50)] public new string Deleter { get; set; }
        [Column("delete_time")] public new DateTime? DeleteTime { get; set; }
        [Column("code")] public new string Code { get; set; } = Guid.NewGuid().ToString("N");
        [Column("status")] public new string Status { get; set; }
        [Column("enable")] public new bool Enable { get; set; } = true;
        [Column("remark")] public new string Remark { get; set; }

        [Required][StringLength(100)][Column("skill_code")]
        public string SkillCode { get; set; }

        [Required][StringLength(500)][Column("url")]
        public string Url { get; set; }

        [StringLength(10)][Column("http_method")]
        public string HttpMethod { get; set; } = "POST";

        /// <summary>请求头 JSON（值可含 $sys. 引用）</summary>
        [Column("headers")]
        public string Headers { get; set; }

        /// <summary>鉴权 JSON：{"type":"bearer","tokenSource":"$sys.XXX"}（密钥不落库）</summary>
        [Column("auth_config")]
        public string AuthConfig { get; set; }

        /// <summary>参数映射：{"输入项名":"请求参数名"}</summary>
        [Column("param_mapping")]
        public string ParamMapping { get; set; }

        /// <summary>响应解析：{"输出项名":"$.data.xxx"}</summary>
        [Column("response_mapping")]
        public string ResponseMapping { get; set; }

        [Column("timeout_seconds")]
        public int TimeoutSeconds { get; set; } = 30;
    }
}
