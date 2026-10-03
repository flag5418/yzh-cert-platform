using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Dir
{
    /// <summary>
    /// 企业文件历史版本实体
    /// <para>表名：<c>cert_enterprise_file_version</c></para>
    /// <para>职责：记录企业文件每次被替换时归档的旧版本信息。</para>
    /// <para>★ 与标准目录「只追加版本」哲学一致——旧版本物理保留在 MinIO History/ 下，本表仅记录元数据。</para>
    /// <para>命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase</para>
    /// </summary>
    [SugarTable("cert_enterprise_file_version")]
    public class EnterpriseFileVersion : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────

        /// <summary>父文件 Code → cert_standard_directory_file.Code（企业上传行）</summary>
        [SugarColumn(Length = 36)]
        public string FileCode { get; set; } = string.Empty;

        /// <summary>企业 Code（冗余，方便按企业查询）</summary>
        [SugarColumn(Length = 36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>版本号（从 1 开始递增，与 PathBuilder.Archive 的 versionNumber 对应）</summary>
        public int VersionNumber { get; set; } = 1;

        /// <summary>该版本的文件名</summary>
        [SugarColumn(Length = 500)]
        public string FileName { get; set; } = string.Empty;

        /// <summary>归档后的 MinIO 路径（History/xxx.doc.vN）</summary>
        [SugarColumn(Length = 512)]
        public string StoragePath { get; set; } = string.Empty;

        /// <summary>该版本文件大小（字节）</summary>
        public long FileSize { get; set; }

        /// <summary>替换原因（如：用户上传新文件覆盖）</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Reason { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
