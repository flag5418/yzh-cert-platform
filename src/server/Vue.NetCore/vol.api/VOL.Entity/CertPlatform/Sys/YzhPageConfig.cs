using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.Sys
{
    /// <summary>
    /// YZH V3.0 页面级 UI 配置表
    /// <para>表名: yzh_page_config</para>
    /// <para>用途: 描述一个 CRUD 页面的整体行为（弹窗尺寸、工具栏按钮、搜索模式等）</para>
    /// </summary>
    [Entity(TableCnName = "YZH页面配置", TableName = "yzh_page_config", DBServer = "VOLContext")]
    [Table("yzh_page_config")]
    public class YzhPageConfig : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [StringLength(50)]
        [Column("page_key")]
        public string PageKey { get; set; }

        [Required]
        [StringLength(100)]
        [Column("page_title")]
        public string PageTitle { get; set; }

        [Required]
        [StringLength(100)]
        [Column("entity_name")]
        public string EntityName { get; set; }

        [Required]
        [StringLength(100)]
        [Column("table_name")]
        public string TableName { get; set; }

        [Required]
        [StringLength(100)]
        [Column("controller_name")]
        public string ControllerName { get; set; }

        [StringLength(50)]
        [Column("key_field")]
        public string KeyField { get; set; } = "Id";

        [StringLength(10)]
        [Column("key_field_type")]
        public string KeyFieldType { get; set; } = "number";

        [StringLength(50)]
        [Column("sort_field")]
        public string SortField { get; set; }

        [StringLength(5)]
        [Column("sort_order")]
        public string SortOrder { get; set; } = "desc";

        [Column("dialog_width")]
        public int DialogWidth { get; set; } = 960;

        [StringLength(20)]
        [Column("dialog_max_height")]
        public string DialogMaxHeight { get; set; } = "85vh";

        [Column("dialog_label_width")]
        public int DialogLabelWidth { get; set; } = 120;

        [StringLength(10)]
        [Column("row_height")]
        public string RowHeight { get; set; } = "default";

        [Column("stripe")]
        public byte Stripe { get; set; } = 1;

        [Column("show_row_number")]
        public byte ShowRowNumber { get; set; } = 1;

        [StringLength(10)]
        [Column("search_mode")]
        public string SearchMode { get; set; } = "fixed";

        /// <summary>
        /// JSON 数组: ["add","refresh","export","import","batchDelete","columnSetting"]
        /// </summary>
        [Column("visible_buttons")]
        public string VisibleButtons { get; set; }

        [Column("show_action_column")]
        public byte ShowActionColumn { get; set; } = 1;

        [Column("checkbox_selection")]
        public byte CheckboxSelection { get; set; } = 1;

        [Column("incremental_update")]
        public byte IncrementalUpdate { get; set; } = 1;

        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; } = "";

        [Column("is_active")]
        public byte IsActive { get; set; } = 1;

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [StringLength(500)]
        [Column("remark")]
        public string Remark { get; set; }
    }
}
