using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Entity.Admin.Platform;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Sys
{
    /// <summary>
    /// YZH V3.0 字段级 UI 配置表
    /// <para>表名: yzh_field_config</para>
    /// <para>用途: 控制每个字段在表格列/弹窗表单/搜索区的显示与行为</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("yzh_field_config")]
    public class FieldConfig : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────

        [Required]
        [StringLength(50)]
        [UniqueField("页面标识", WithFields = new[] { "FieldName", "OrgCode" })]
        public string PageKey { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string FieldName { get; set; } = string.Empty;

        [StringLength(100)]
        public string FieldAlias { get; set; } = string.Empty;

        // ====== A. 表格列配置 ======
        public byte XsFlag { get; set; } = 1;

        public int ColumnSxh { get; set; } = 0;

        [StringLength(100)]
        public string ColumnTitle { get; set; } = string.Empty;

        public int ColumnWidth { get; set; } = 120;

        [StringLength(10)]
        public string ColumnFixed { get; set; } = string.Empty;

        public byte Sortable { get; set; } = 1;

        [StringLength(50)]
        public string ColumnFormatter { get; set; } = string.Empty;

        public byte ShowOverflow { get; set; } = 1;

        [StringLength(10)]
        public string Align { get; set; } = "left";

        // ====== B. 弹窗表单/Grid布局 ======
        public byte BcFlag { get; set; } = 1;

        [StringLength(100)]
        public string FormTitle { get; set; } = string.Empty;

        [StringLength(20)]
        public string ControlType { get; set; } = "input";

        public int GridRow { get; set; } = 0;

        public int GridCol { get; set; } = 0;

        public int GridRowSpan { get; set; } = 1;

        public int GridColSpan { get; set; } = 1;

        public byte Required { get; set; } = 0;

        public int MaxLength { get; set; } = 0;

        [StringLength(200)]
        public string Placeholder { get; set; } = string.Empty;

        [StringLength(500)]
        public string DefaultValue { get; set; } = string.Empty;

        public byte Readonly { get; set; } = 0;

        public byte Disabled { get; set; } = 0;

        public int? Precision { get; set; }

        public decimal? MinVal { get; set; }

        public decimal? MaxVal { get; set; }

        public int TextareaRows { get; set; } = 3;

        // ====== 字典/数据源 ======
        [StringLength(50)]
        public string DataKey { get; set; } = string.Empty;

        [StringLength(255)]
        public string RemoteUrl { get; set; } = string.Empty;

        // ====== 业务控制 ======
        public int GroupIndex { get; set; } = 0;

        // ====== C. 搜索区配置 ======
        public byte SearchFlag { get; set; } = 0;

        [StringLength(100)]
        public string SearchTitle { get; set; } = string.Empty;

        [StringLength(100)]
        public string SearchPlaceholder { get; set; } = string.Empty;

        [StringLength(20)]
        public string SearchControlType { get; set; } = string.Empty;

        public int SearchWidth { get; set; } = 180;

        [StringLength(50)]
        public string OrgCode { get; set; } = string.Empty;

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Remark { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
