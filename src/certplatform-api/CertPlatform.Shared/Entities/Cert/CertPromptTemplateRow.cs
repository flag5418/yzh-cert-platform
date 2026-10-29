using SqlSugar;

namespace CertPlatform.Shared.Entities.Cert;

/// <summary>
/// <b>只读投影</b>：<c>cert_prompt_template</c>（提示词统一表）的最小列集，给专家端读取用。
///
/// <para><b>★★ 为什么不复用 Admin 的 <c>PromptTemplate</c></b>：那是后台端独占实体
/// （<c>CertPlatform.Admin/Entities/Cert/</c>），而 <c>Auditor ⛔ 引 Admin</c>。
/// 专家端的「按标准提取条款」要读 <c>doc_clause_extract</c> 提示词正文，
/// 只能靠本投影（策略与 <see cref="ClauseAttrRow"/> 一致：只读、列集最小、不写库）。</para>
///
/// <para><b>★ 与 <c>wf_prompt_template</c> 的区别</b>：本表是 V2 条款主链的提示词唯一来源
/// （<c>clause_attr_gen</c> / <c>doc_extract</c> / <c>doc_clause_extract</c>）；
/// <c>wf_prompt_template</c> 是 Dify 工作流模板体系，字段语义与生命周期完全不同，⛔ 不要混用。</para>
///
/// <para><b>作用域解析</b>：本投影只取<b>平台级</b>行（<c>OrgCode IS NULL AND ScopeKey IS NULL</c>）
/// —— 条款提取的判据是全平台统一的，按机构/标准分叉只会让同一份文档在不同机构出不同结果。
/// 需要按标准覆盖时，改用 <c>ScopeKey = 标准短码</c> 的解析顺序（见 <c>DocExtractPromptService</c>）。</para>
/// </summary>
[SugarTable("cert_prompt_template")]
public class CertPromptTemplateRow
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    /// <summary>提示词代码（程序入口，如 <c>doc_clause_extract</c>）</summary>
    [SugarColumn(Length = 100)]
    public string PromptCode { get; set; } = string.Empty;

    [SugarColumn(Length = 200, IsNullable = true)]
    public string? PromptName { get; set; }

    /// <summary>机构 Code（空 = 平台级）</summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? OrgCode { get; set; }

    /// <summary>细分维度（标准短码 / 阶段 Code；空 = 该作用域默认）</summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? ScopeKey { get; set; }

    /// <summary>提示词正文（含 <c>{{placeholders}}</c>）—— DB 列是 <c>text</c>（64KB 上限）</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Template { get; set; }

    /// <summary><c>active</c> / <c>fallback</c> / …（本投影只认 <c>active</c> 之外一律当没配）</summary>
    [SugarColumn(Length = 20, IsNullable = true)]
    public string? Status { get; set; }

    public int IsValid { get; set; } = 1;

    public bool IsDeleted { get; set; }
}
