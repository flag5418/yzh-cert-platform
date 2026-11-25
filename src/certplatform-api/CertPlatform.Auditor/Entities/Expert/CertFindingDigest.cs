using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 审核发现概括（LLM 归纳缓存）
    /// <para>表名：<c>cert_finding_digest</c></para>
    /// <para>「审核发现」面板的每条事实来自企业文档提取产物（只读投影，不落 <c>cert_evidence</c>），
    /// 原文往往整段上千字，直接给审核员看没有帮助。本表把「文档 × 条款」这段原文归纳成 2-3 句</para>
    /// <para>缓存键 = <see cref="SourceCode"/> + <see cref="Anchor"/> + <see cref="SnippetMd5"/>；
    /// 同文档同条款同原文跨检查项复用，原文变化（MD5 变）则重新生成</para>
    /// </summary>
    [SugarTable("cert_finding_digest")]
    public class CertFindingDigest : BaseEntity, ISoftDelete, IIsValid
    {
        /// <summary>来源编码（FileCode）</summary>
        [StringLength(50)]
        public string SourceCode { get; set; } = string.Empty;

        /// <summary>锚点（条款号 / 召回理由）</summary>
        [StringLength(200)]
        public string Anchor { get; set; } = string.Empty;

        /// <summary>原文摘录 MD5（内容变化触发重新生成）</summary>
        [StringLength(32)]
        public string SnippetMd5 { get; set; } = string.Empty;

        /// <summary>概括（2-3 句）</summary>
        public string? Digest { get; set; }

        /// <summary>生成用的模型名</summary>
        [StringLength(50)]
        public string? Model { get; set; }

        /// <summary>有效标志（1=启用，0=停用）—— 铁律九统一用 IsValid</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        public DateTime? DeleteTime { get; set; }
    }
}
