using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.Sys
{
    /// <summary>
    /// YZH V3.0 字段级 UI 配置表
    /// <para>表名: yzh_field_config</para>
    /// <para>用途: 控制每个字段在表格列/弹窗表单/搜索区的显示与行为</para>
    /// </summary>
    [Entity(TableCnName = "YZH字段配置", TableName = "yzh_field_config", DBServer = "VOLContext")]
    [Table("yzh_field_config")]
    public class YzhFieldConfig : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [StringLength(50)]
        [Column("page_key")]
        public string PageKey { get; set; }

        [Required]
        [StringLength(50)]
        [Column("field_name")]
        public string FieldName { get; set; }

        [StringLength(100)]
        [Column("field_alias")]
        public string FieldAlias { get; set; } = "";

        // ====== A. 表格列配置 ======
        [Column("xs_flag")]
        public byte XsFlag { get; set; } = 1;

        [Column("column_sxh")]
        public int ColumnSxh { get; set; } = 0;

        [StringLength(100)]
        [Column("column_title")]
        public string ColumnTitle { get; set; } = "";

        [Column("column_width")]
        public int ColumnWidth { get; set; } = 120;

        [StringLength(10)]
        [Column("column_fixed")]
        public string ColumnFixed { get; set; } = "";

        [Column("sortable")]
        public byte Sortable { get; set; } = 1;

        [StringLength(50)]
        [Column("column_formatter")]
        public string ColumnFormatter { get; set; } = "";

        [Column("show_overflow")]
        public byte ShowOverflow { get; set; } = 1;

        [StringLength(10)]
        [Column("align")]
        public string Align { get; set; } = "left";

        // ====== B. 弹窗表单/Grid布局 ======
        [Column("bc_flag")]
        public byte BcFlag { get; set; } = 1;

        [StringLength(100)]
        [Column("form_title")]
        public string FormTitle { get; set; } = "";

        [StringLength(20)]
        [Column("control_type")]
        public string ControlType { get; set; } = "input";

        [Column("grid_row")]
        public int GridRow { get; set; } = 0;

        [Column("grid_col")]
        public int GridCol { get; set; } = 0;

        [Column("grid_row_span")]
        public int GridRowSpan { get; set; } = 1;

        [Column("grid_col_span")]
        public int GridColSpan { get; set; } = 1;

        [Column("required")]
        public byte Required { get; set; } = 0;

        [Column("maxlength")]
        public int MaxLength { get; set; } = 0;

        [StringLength(200)]
        [Column("placeholder")]
        public string Placeholder { get; set; } = "";

        [StringLength(500)]
        [Column("default_value")]
        public string DefaultValue { get; set; } = "";

        [Column("readonly")]
        public byte Readonly { get; set; } = 0;

        [Column("disabled")]
        public byte Disabled { get; set; } = 0;

        [Column("precision")]
        public int? Precision { get; set; }

        [Column("min_val")]
        public decimal? MinVal { get; set; }

        [Column("max_val")]
        public decimal? MaxVal { get; set; }

        [Column("textarea_rows")]
        public int TextareaRows { get; set; } = 3;

        // ====== 字典/数据源 ======
        [StringLength(50)]
        [Column("data_key")]
        public string DataKey { get; set; }

        [StringLength(255)]
        [Column("remote_url")]
        public string RemoteUrl { get; set; }

        // ====== 业务控制 ======
        [Column("group_index")]
        public int GroupIndex { get; set; } = 0;

        // ====== C. 搜索区配置 ======
        [Column("search_flag")]
        public byte SearchFlag { get; set; } = 0;

        [StringLength(100)]
        [Column("search_title")]
        public string SearchTitle { get; set; } = "";

        [StringLength(100)]
        [Column("search_placeholder")]
        public string SearchPlaceholder { get; set; } = "";

        [StringLength(20)]
        [Column("search_control_type")]
        public string SearchControlType { get; set; }

        [Column("search_width")]
        public int SearchWidth { get; set; } = 180;

        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; } = "";

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [StringLength(500)]
        [Column("remark")]
        public string Remark { get; set; }
    }
}
