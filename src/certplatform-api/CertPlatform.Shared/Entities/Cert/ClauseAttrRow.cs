using SqlSugar;

namespace CertPlatform.Shared.Entities.Cert;

/// <summary>
/// <b>只读投影</b>：<c>cert_clause_attr</c>（L1 条款属性配置）的<b>最小列集</b>，给专家端读取用。
///
/// <para><b>★★ 为什么会有这份「第二实体」</b>（2026-10-29）：
/// 权威实体 <c>CertPlatform.Admin.Entities.Cert.ClauseAttr</c> 在后台端，而
/// <c>Auditor ⛔ 引 Admin</c>（三方结构铁律）。但专家端的「按标准提取条款」
/// 必须读 <c>DocCheckMode</c> 才知道<b>哪些条款要靠企业资料去查</b>
/// —— 不读就会把 <c>none</c> 的条款也塞进提示词，模型为不需要资料的条款硬凑内容。</para>
///
/// <para><b>★ 边界（⛔ 不许越界）</b>：
/// <list type="bullet">
///   <item>本类<b>只读</b>。写入一律走 Admin 的 <c>ClauseAttr</c>（那边有 <c>FilterCode</c> 生成列等约束）。</item>
///   <item>列集<b>刻意最小</b>：只声明调用方真正要用的列。⚠️ 加列前先问「专家端真的要用吗」，
///         否则两个实体逐列分叉，某天字段语义改了只改一处，另一处静默读到旧语义。</item>
///   <item><c>StandardCode</c> 是<b>短码</b>（<c>iso13485</c>），⛔ 不是 <c>cert_iso_standard.Code</c>（GUID）。
///         这与 <c>cert_document_extraction.StandardCode</c>（GUID）<b>口径不同</b>，转换只在
///         <c>ClauseExtractExecutor</c> 一处发生。</item>
/// </list></para>
/// </summary>
[SugarTable("cert_clause_attr")]
public class ClauseAttrRow
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    /// <summary>标准短码（<c>iso9001</c> / <c>iso13485</c> …）</summary>
    [SugarColumn(Length = 36)]
    public string StandardCode { get; set; } = string.Empty;

    /// <summary>条款 Code（<c>cert_iso_clause.Code</c>，GUID）</summary>
    [SugarColumn(Length = 36)]
    public string ClauseCode { get; set; } = string.Empty;

    /// <summary>属性码（<c>cert_clause_attr_def.Code</c>，如 <c>DocCheckMode</c>）</summary>
    [SugarColumn(Length = 50)]
    public string AttrCode { get; set; } = string.Empty;

    /// <summary>属性值（<c>DocCheckMode</c> 取 <c>required</c>/<c>optional</c>/<c>none</c>）—— DB 列是 <c>text</c></summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? AttrValue { get; set; }

    public int Sort { get; set; }

    public int IsValid { get; set; } = 1;

    public bool IsDeleted { get; set; }
}
