using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Dir
{
  /// <summary>
  /// 文档提取产物（表 <c>cert_document_extraction</c>）—— 36 号 §4.1 表④
  ///
  /// <para><b>★★★ 粒度 = 文档 × 标准 = 一份 JSON</b>（2026-10-29 用户裁定）：
  /// 上传时不带标准，<b>标准在这一层才出现</b> —— 同一份《质量手册》对 iso9001 与 iso13485
  /// 各跑一次，产出两份提取结果（条款号体系不同，无法合并）。</para>
  ///
  /// <para><b>为什么不是「文档 = 一份 JSON 含所有标准」</b>：
  /// 新增一个标准时，旧 JSON 必须重算；而重算会覆盖已有标准的结果 ⇒ 一处失败全废。
  /// 按 (FileCode, StandardCode) 拆开 ⇒ 单标准可独立重跑、独立失败、独立留痕。</para>
  ///
  /// <para><b>★ <see cref="SourceSha256"/> 是重跑判据</b>：文档被替换（hash 变）⇒ 旧产物置
  /// <see cref="IsStale"/>=1 待重跑；hash 不变 ⇒ 不重跑（省钱且幂等）。</para>
  ///
  /// <para><b>★ 提示词留痕</b>：<see cref="PromptCode"/> + <see cref="PromptVersion"/> 记录
  /// 产出它的提示词版本 —— 提示词改了要能看出「哪些产物是旧提示词产的」。</para>
  /// </summary>
  [SugarTable("cert_document_extraction")]
  public class DocumentExtraction : BaseEntity, ISoftDelete, IIsValid
  {
    /// <summary>文档 → <c>cert_enterprise_doc.Code</c></summary>
    [SugarColumn(Length = 36)]
    public string FileCode { get; set; } = string.Empty;

    /// <summary>标准 → <c>cert_iso_standard.Code</c>（GUID）</summary>
    [SugarColumn(Length = 36)]
    public string StandardCode { get; set; } = string.Empty;

    /// <summary>企业 Code（冗余，按企业查免 join）</summary>
    [SugarColumn(Length = 36)]
    public string EnterpriseCode { get; set; } = string.Empty;

    /// <summary>
    /// 本产物覆盖的条款号，逗号分隔（如 <c>4.2.4,4.2.5,7.5.1</c>）。
    /// <para>⚠️ 查询用 <c>FIND_IN_SET('4.2.5', ClauseNumbers)</c> —— <b>逗号精确匹配</b>，
    /// ⛔ 不能用 <c>LIKE '%4.2.5%'</c>（会误中 <c>4.2.55</c>）。</para>
    /// </summary>
    [SugarColumn(Length = 500)]
    public string ClauseNumbers { get; set; } = string.Empty;

    /// <summary>
    /// 提取内容 JSON：<c>{ docMeta, clauses[] }</c>。
    /// <para>★ 铁律：只产「读到什么」（原文摘录 + 位置 + 置信度），<b>⛔ 不产结论</b>
    /// （符合/不符合由检查表判定，见 35 号）。找不到就交空，<b>⛔ 禁推测</b>。</para>
    /// </summary>
    [SugarColumn(ColumnDataType = "longtext", IsNullable = true)]
    public string? ContentJson { get; set; }

    /// <summary>切片条数（clauses[] 长度，便于列表快速判断产出量）</summary>
    public int ClauseCount { get; set; }

    /// <summary>产出时的文档指纹；与主表 <c>Sha256</c> 不一致 ⇒ 本产物已过期</summary>
    [SugarColumn(Length = 64)]
    public string SourceSha256 { get; set; } = string.Empty;

    /// <summary>产出它的提示词 Code（留痕）</summary>
    [SugarColumn(Length = 50)]
    public string PromptCode { get; set; } = string.Empty;

    /// <summary>产出它的提示词版本（留痕；提示词改版后能看出哪些产物是旧的）</summary>
    [SugarColumn(Length = 20)]
    public string PromptVersion { get; set; } = string.Empty;

    /// <summary><c>pending/running/done/failed/skipped</c></summary>
    [SugarColumn(Length = 20)]
    public string ExtractStatus { get; set; } = "pending";

    /// <summary>失败/跳过原因。⚠️ <b>⛔ 不许静默</b>：跳过必须写清为什么（如「该标准本条款不适用」）</summary>
    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? ExtractMessage { get; set; }

    public DateTime? ExtractedAt { get; set; }

    /// <summary>文档被替换后置 1（待重跑）；跑完清零</summary>
    public bool IsStale { get; set; }

    public int Sort { get; set; }

    public bool IsDeleted { get; set; }
    public string? DeleteBy { get; set; }
    public DateTime? DeleteTime { get; set; }
    public int IsValid { get; set; } = 1;
  }
}
