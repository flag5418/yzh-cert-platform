using System;
using System.Collections.Generic;

namespace CertPlatform.Shared.DocExtraction
{
    /// <summary>
    /// 文档提取提示词目录（★ 唯一事实源）。
    ///
    /// <para>把「文档画像判定的 ExtractionMode」↔「提示词 PromptCode」↔「页面显示名 / 分组」
    /// 集中在共享层，供 Admin 页面下拉（<c>DocExtractRuleController</c>）与 Auditor 运行时路由
    /// （<c>EnterpriseDocExtractionRouter</c>）共用 —— 杜绝「页面能选」与「运行时能跑」两张表漂移。</para>
    ///
    /// <para>分组：画像（分流总闸）/ 类型提取 / 兜底；<see cref="Entry.Mode"/> 为空 = 不参与 mode 路由。</para>
    /// </summary>
    public static class DocExtractionPromptCatalog
    {
        public const string GroupProfile = "画像（分流总闸）";
        public const string GroupType = "类型提取";
        public const string GroupFallback = "兜底";

        /// <summary>目录条目（Mode 空 = 不是 mode 路由用的提示词，如画像总闸）。</summary>
        public sealed record Entry(string PromptCode, string Mode, string DisplayName, string Group, int Sort);

        /// <summary>全部提示词（按 Sort 升序；页面下拉与运行时都以此为准）。</summary>
        public static IReadOnlyList<Entry> All { get; } = new Entry[]
        {
            new("doc_profile", "", "文档画像（分流总闸）", GroupProfile, 10),

            new("doc_extract_manual_chapter", "manual_chapter", "手册 / 方针章节", GroupType, 20),
            new("doc_extract_process_profile", "process_profile", "程序 / 过程文件", GroupType, 21),
            new("doc_extract_record_profile", "record_profile", "记录 / 表单", GroupType, 22),
            new("doc_extract_index", "index_only", "文件索引 / 清单", GroupType, 23),
            new("doc_extract_document_control", "document_control", "文件控制类", GroupType, 24),
            new("doc_extract_work_instruction_citation", "work_instruction_citation", "作业指导书", GroupType, 25),
            new("doc_extract_policy_profile", "policy_profile", "制度 / 管理办法", GroupType, 26),
            new("doc_extract_context_list", "context_list", "组织环境 / 相关方清单", GroupType, 27),
            new("doc_extract_audit", "audit_profile", "内审 / 管理评审", GroupType, 28),

            new("doc_clause_extract", "legacy", "条款逐条（兜底 · 仅 other）", GroupFallback, 90),
        };

        /// <summary>模式 → 提示词 Code；未知模式返回空串（与运行时同一映射）。</summary>
        public static string PromptCodeForMode(string? mode)
        {
            var m = (mode ?? string.Empty).Trim();
            foreach (var e in All)
                if (e.Mode.Length > 0 && string.Equals(e.Mode, m, StringComparison.Ordinal))
                    return e.PromptCode;
            return string.Empty;
        }
    }
}
