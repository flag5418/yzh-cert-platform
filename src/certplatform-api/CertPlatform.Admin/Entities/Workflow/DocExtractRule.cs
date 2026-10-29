using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Entities.Workflow;

/// <summary>
///     文档提取规则（每标准一行）
///
///     功能：绑定 ISO 标准 → 该国标 Markdown 知识库 + 提取提示词
///     路由映射：后台端 → /business/doc-extract-rule
///
///     数据库：cert_doc_extract_rules
///
///     业务规则：
///     1. 每标准唯一（uk_standard → StandardCode）
///     2. MarkdownContent 仅首次入库（Phase A）；后续只改 PromptContent
///     3. Version + IsPublished 支持版本追溯和灰度
/// </summary>
[SugarTable("cert_doc_extract_rules")]
public class DocExtractRule : BaseEntity, ISoftDelete, IIsValid
{
    /// <summary>标准 Code（cert_iso_standard.Code GUID）</summary>
    public string StandardCode { get; set; } = string.Empty;

    /// <summary>标准名称（冗余，方便列表展示）</summary>
    public string StandardName { get; set; } = string.Empty;

    /// <summary>该国标完整 Markdown 全文（仅首次入库；后续不改）</summary>
    public string? MarkdownContent { get; set; }

    /// <summary>该标准的文档提取提示词</summary>
    public string? PromptContent { get; set; }

    /// <summary>提示词变量列表（JSON 数组，如 ["document_content"]）</summary>
    public string? PromptVariables { get; set; }

    /// <summary>版本号（每次发布 +1）</summary>
    public int Version { get; set; } = 1;

    /// <summary>当前是否发布（1=发布）</summary>
    public int IsPublished { get; set; } = 0;

    /// <summary>排序号</summary>
    public int Sort { get; set; }

    public int IsValid { get; set; } = 1;
    public string? Remark { get; set; }

    // ── ISoftDelete ──
    public bool IsDeleted { get; set; }
    public string? DeleteBy { get; set; }
    public DateTime? DeleteTime { get; set; }

    // ── 业务状态 ──
    /// <summary>业务状态（保留字段）</summary>
    public string? Status { get; set; }
}
