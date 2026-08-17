using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Wf
{
    /// <summary>
    /// WfSkillCategory — Skill 分类（基础资料维护）
    /// <para>表名：wf_skill_category</para>
    /// <para>用途：页面左侧分类导航 + 面板分组；category_code 与 wf_skill.category 对应</para>
    /// </summary>
    [Table("wf_skill_category")]
    public class WfSkillCategory : YZHBaseEntity
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

        [Required][StringLength(50)][Column("category_code")]
        public string CategoryCode { get; set; }

        [Required][StringLength(100)][Column("category_name")]
        public string CategoryName { get; set; }

        [StringLength(50)][Column("icon")]
        public string Icon { get; set; }

        [StringLength(20)][Column("color")]
        public string Color { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }
    }
}
