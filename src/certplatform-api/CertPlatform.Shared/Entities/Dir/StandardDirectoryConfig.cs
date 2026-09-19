using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Dir
{
    /// <summary>标准目录配置实体</summary>
    /// <para>命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase</para>
    [SugarTable("cert_standard_directory_config")]
    public class StandardDirectoryConfig : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────

        [SugarColumn(Length = 100)]
        public string DirectoryCode { get; set; } = string.Empty;

        [SugarColumn(Length = 50)]
        public string StandardCode { get; set; } = string.Empty;

        [SugarColumn(Length = 50)]
        public string PhaseCode { get; set; } = string.Empty;

        [SugarColumn(Length = 200, IsNullable = true)]
        public string? RootFolderName { get; set; }

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? Status { get; set; } = "draft";

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? StatusField { get; set; } = "active";

        public bool Enable { get; set; } = true;

        public bool EnableField { get; set; } = true;

        public int Sort { get; set; } = 0;

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Remark { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
