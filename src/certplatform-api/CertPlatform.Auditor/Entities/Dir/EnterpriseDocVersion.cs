using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Dir
{
  /// <summary>
  /// 企业文档历史版本（表 <c>cert_enterprise_doc_version</c>）—— 36 号 §4.1 表②
  ///
  /// <para><b>只追加，不更新</b>：每次替换（同路径异 hash）写一行，记录被换下去的那一版。
  /// 主表 <see cref="EnterpriseDoc"/> 恒指当前活跃版。</para>
  ///
  /// <para>⚠️ <b>表名刻意避开 <c>cert_enterprise_file_version</c></b> ——
  /// 该表名已被「资料库归档版本」占用，复用会串数据。</para>
  ///
  /// <para>⚠️ 关联用 <see cref="FileCode"/>（父文档 <c>Code</c>），⛔ 不用 <c>Id</c>。</para>
  /// </summary>
  [SugarTable("cert_enterprise_doc_version")]
  public class EnterpriseDocVersion : BaseEntity, ISoftDelete, IIsValid
  {
    /// <summary>父文档 → <c>cert_enterprise_doc.Code</c>（⛔ 不用 Id）</summary>
    [SugarColumn(Length = 36)]
    public string FileCode { get; set; } = string.Empty;

    /// <summary>企业 Code（冗余，按企业查历史时免 join）</summary>
    [SugarColumn(Length = 36)]
    public string EnterpriseCode { get; set; } = string.Empty;

    /// <summary>被换下去的那一版的版本号（主表 <c>VersionNumber</c> 递增前的值）</summary>
    public int VersionNumber { get; set; } = 1;

    [SugarColumn(Length = 300)]
    public string FileName { get; set; } = string.Empty;

    [SugarColumn(Length = 20)]
    public string FileType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    [SugarColumn(Length = 64)]
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>归档路径 <c>_archive/{名}.v{n}</c>（由 <c>PathBuilder.Archive</c> 产出）</summary>
    [SugarColumn(Length = 512)]
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>替换原因（谁在什么场景换的）</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Reason { get; set; }

    public bool IsDeleted { get; set; }
    public string? DeleteBy { get; set; }
    public DateTime? DeleteTime { get; set; }
    public int IsValid { get; set; } = 1;
  }
}
