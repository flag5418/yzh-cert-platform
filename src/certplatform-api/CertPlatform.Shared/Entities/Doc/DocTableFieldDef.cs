using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Doc
{
    /// <summary>
    /// DocTableFieldDef 文档表格字段定义（表格的列）
    /// <para>表名：cert_doc_table_field_def（唯一键 uk_table_column: TableCode + ColumnCode）</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("cert_doc_table_field_def")]
    public class DocTableFieldDef : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        // ──── 业务字段 ────
        /// <summary>表格编码（关联 cert_doc_table_def.Code）</summary>
        [SugarColumn(Length = 100)]
        public string TableCode { get; set; } = "";

        [SugarColumn(Length = 100)]
        public string ColumnName { get; set; } = "";

        /// <summary>列编码（英文驼峰）</summary>
        [SugarColumn(Length = 100)]
        public string ColumnCode { get; set; } = "";

        /// <summary>数据类型：string/number/date</summary>
        public string DataType { get; set; } = "string";

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        /// <summary>备注</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        /// <summary>状态</summary>
        public string Status { get; set; } = "active";

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
