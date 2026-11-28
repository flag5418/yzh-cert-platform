using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Entities.Cert;

/// <summary>
/// 提示词模板（统一管理全部提示词）
///
/// <para><b>存储策略</b>：一张表管所有提示词，<see cref="PromptCode"/> 区分类型，
/// <see cref="ScopeKey"/> 区分作用域（空=平台级 / 标准Code=标准级 / 阶段Code=阶段级）。</para>
///
/// <para><b>作用域优先级</b>（解析时从具体到通用）：
/// <list type="number">
/// <item><c>OrgCode + ScopeKey</c>（机构 × 细分维度）</item>
/// <item><c>OrgCode</c>（机构默认）</item>
/// <item><c>ScopeKey</c>（平台级 × 细分维度）</item>
/// <item>平台默认（<see cref="OrgCode"/> = 空 &amp; <see cref="ScopeKey"/> = 空）</item>
/// </list></para>
///
/// <para><b>已接管的提示词类型</b>（2026-11-27 起为<b>全系统唯一提示词源</b>）：
/// <list type="bullet">
/// <item><c>clause_attr_gen</c> —— 条款属性预生成（原 cert_sys_config · clause_attr_prompt）</item>
/// <item><c>doc_extract</c> —— 文档提取提示词（原 cert_doc_extract_rules.PromptContent）</item>
/// <item><c>audit_prep</c> / <c>onsite_plan</c> / <c>nc_suggest</c> / <c>evidence_link</c> —— 检查链提示词</item>
/// <item><c>clause_evidence_findings</c> / <c>audit_check_evidence</c> —— 两段式检查提示词</item>
/// <item><c>doc_group</c> / <c>doc_content</c> / <c>doc_essential</c> / <c>prompt_generator</c> —— 队列语义提示词</item>
/// </list></para>
///
/// <para><b>已接管 <c>wf_prompt_template</c></b>（2026-11-27 退役）：原 Dify 工作流模板体系字段
/// （<c>PromptType</c> → <see cref="PromptCode"/>、<c>StandardCode</c>GUID → <see cref="ScopeKey"/>短码、
/// <c>ModelName</c>/<c>MaxTokens</c>/<c>Temperature</c> 逐字迁入），旧表已 <c>RENAME</c> 为
/// <c>zz_retired_wf_prompt_template</c>（⛔ 不 DROP）。</para>
///
/// <para>ORM：SqlSugar（铁律：DB 列名 == C# 属性名，PascalCase 逐字一致）</para>
/// </summary>
[SugarTable("cert_prompt_template")]
public class PromptTemplate : BaseEntity, ISoftDelete, IIsValid
{
    /// <summary>提示词代码（英文标识，如 clause_attr_gen / doc_extract）—— 程序入口，唯一区分类型</summary>
    public string PromptCode { get; set; } = string.Empty;

    /// <summary>提示词名称（人类可读，如"条款属性预生成提示词"）</summary>
    public string PromptName { get; set; } = string.Empty;

    /// <summary>机构 Code（机构专属提示词，空=平台级）</summary>
    public string? OrgCode { get; set; }

    /// <summary>细分维度（标准 Code / 阶段 Code；空=该作用域下的默认提示词）</summary>
    public string? ScopeKey { get; set; }

    /// <summary>提示词正文（含 {{placeholders}} 占位符）</summary>
    public string Template { get; set; } = string.Empty;

    /// <summary>占位符注册表（JSON 数组：[{token, desc, renderer}]）</summary>
    public string? Placeholders { get; set; }

    /// <summary>备注（提示词的作用说明）</summary>
    public string? Remark { get; set; }

    /// <summary>排序号</summary>
    public int Sort { get; set; }

    public int IsValid { get; set; } = 1;
    public string? Status { get; set; }

    // ── 模板级模型参数（2026-11-27 由 wf_prompt_template 迁入，D1）──
    /// <summary>模型名（为空 = 用平台默认）</summary>
    public string? ModelName { get; set; }

    /// <summary>最大输出 tokens（为空 = 用平台默认）</summary>
    public int? MaxTokens { get; set; }

    /// <summary>温度（为空 = 用平台默认）</summary>
    public decimal? Temperature { get; set; }

    // ── ISoftDelete ──
    public bool IsDeleted { get; set; }
    public string? DeleteBy { get; set; }
    public DateTime? DeleteTime { get; set; }
}
