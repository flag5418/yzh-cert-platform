using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Sys
{
    /// <summary>站内消息实体</summary>
    /// <para>命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase</para>
    [SugarTable("cert_message")]
    public class CertMessage : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────

        [SugarColumn(Length = 64)]
        public string UserCode { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        [SugarColumn(Length = 20)]
        public string MessageType { get; set; } = "system";

        public int IsRead { get; set; }

        public string ExtraData { get; set; } = string.Empty;

        public DateTime? ReadDate { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
