using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Entity.Admin.Platform;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Sys
{
    /// <summary>
    /// YZH V3.0 页面级 UI 配置表
    /// <para>表名: yzh_page_config</para>
    /// <para>用途: 描述一个 CRUD 页面的整体行为（弹窗尺寸、工具栏按钮、搜索模式等）</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("yzh_page_config")]
    public class PageConfig : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────

        [Required]
        [StringLength(50)]
        [UniqueField("页面标识", WithFields = new[] { "OrgCode" })]
        public string PageKey { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string PageTitle { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string EntityName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string TableName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ControllerName { get; set; } = string.Empty;

        [StringLength(50)]
        public string KeyField { get; set; } = "Id";

        [StringLength(10)]
        public string KeyFieldType { get; set; } = "number";

        [StringLength(50)]
        public string SortField { get; set; } = string.Empty;

        [StringLength(5)]
        public string SortOrder { get; set; } = "desc";

        public int DialogWidth { get; set; } = 960;

        [StringLength(20)]
        public string DialogMaxHeight { get; set; } = "85vh";

        public int DialogLabelWidth { get; set; } = 120;

        [StringLength(10)]
        public string RowHeight { get; set; } = "default";

        public byte Stripe { get; set; } = 1;

        public byte ShowRowNumber { get; set; } = 1;

        [StringLength(10)]
        public string SearchMode { get; set; } = "fixed";

        /// <summary>JSON 数组: ["add","refresh","export","import","batchDelete","columnSetting"]</summary>
        public string VisibleButtons { get; set; } = string.Empty;

        public byte ShowActionColumn { get; set; } = 1;

        public byte CheckboxSelection { get; set; } = 1;

        public byte IncrementalUpdate { get; set; } = 1;

        [StringLength(50)]
        public string OrgCode { get; set; } = string.Empty;

        public byte IsActive { get; set; } = 1;

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Remark { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
