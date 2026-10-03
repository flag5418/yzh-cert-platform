using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Dir
{
    /// <summary>
    /// 企业原始资料历史版本（表 <c>cert_enterprise_original_file_version</c>）—— 36 号 §3.2 表②
    /// <para><b>端归属</b>：专家端独占。</para>
    /// <para><b>D8 版本管理</b>（2026-10-03 用户拍板：版本管理必须有，且与企业资料库同构）。
    /// 对标物：<c>cert_enterprise_file_version</c>（<c>CertPlatform.Shared/Entities/Dir/EnterpriseFileVersion.cs</c>）。</para>
    /// <para><b>哲学一致</b>：旧版本<b>物理保留</b>在 MinIO <c>_archive/</c> 下，
    /// 本表<b>只追加</b>记录元数据，<b>行数永不减少</b>。</para>
    /// <para>⚠️ 归档路径算法<b>只有一个</b>：<c>PathBuilder.Archive(storagePath, versionNumber)</c>。
    /// ⛔ 不另写第二套（历史上 MinIO 里并存 5 套路径格式，根因就是「新增方法而不复用」）。</para>
    /// </summary>
    [SugarTable("cert_enterprise_original_file_version")]
    public class EnterpriseOriginalFileVersion : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────

        /// <summary>★ 父文件 Code → <c>EnterpriseOriginalFile.Code</c>（⛔ 不用 Id，D9）</summary>
        [SugarColumn(Length = 36)]
        public string FileCode { get; set; } = string.Empty;

        /// <summary>企业 Code（冗余，方便按企业查全部历史）</summary>
        [SugarColumn(Length = 36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>阶段 Code（冗余）</summary>
        [SugarColumn(Length = 36)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>★ 被归档内容的版本号（与 <c>PathBuilder.Archive(versionNumber)</c> 对应）</summary>
        public int VersionNumber { get; set; } = 1;

        /// <summary>该版本文件名</summary>
        [SugarColumn(Length = 300)]
        public string FileName { get; set; } = string.Empty;

        /// <summary>该版本扩展名</summary>
        [SugarColumn(Length = 20)]
        public string FileType { get; set; } = string.Empty;

        /// <summary>该版本字节数</summary>
        public long FileSize { get; set; }

        /// <summary>该版指纹（审计用：确认两版内容真的不同，而非误判重复上传）</summary>
        [SugarColumn(Length = 64)]
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>★ 归档后的 MinIO 路径（<c>…/_archive/{名}.v{n}</c>）</summary>
        [SugarColumn(Length = 512)]
        public string StoragePath { get; set; } = string.Empty;

        /// <summary>替换原因（如：用户上传新文件覆盖）</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Reason { get; set; }

        // ──── ISoftDelete + IIsValid ────

        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}