using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Dir
{
    /// <summary>上传任务实体</summary>
    /// <para>命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase</para>
    [SugarTable("cert_upload_task")]
    public class UploadTask : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────

        [SugarColumn(Length = 64)]
        public string TaskId { get; set; } = string.Empty;

        [SugarColumn(Length = 128)]
        public string DirectoryCode { get; set; } = string.Empty;

        public int TotalFiles { get; set; } = 0;

        public long TotalSize { get; set; } = 0;

        public int SuccessCount { get; set; } = 0;

        [SugarColumn(Length = 20)]
        public string Status { get; set; } = "initialized";

        public DateTime? ExpireTime { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
