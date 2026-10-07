using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Doc
{
    /// <summary>
    /// 文档填写执行留痕
    /// <para>表名：cert_doc_fill_log</para>
    /// </summary>
    [SugarTable("cert_doc_fill_log")]
    [YZHDeleteStrategy(Mode = DeleteMode.Soft)]
    public class DocFillLog : BaseEntity, ISoftDelete, IIsValid
    {
        [StringLength(36)]
        public string OrgCode { get; set; } = string.Empty;

        [StringLength(36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        [StringLength(36)]
        public string StageCode { get; set; } = string.Empty;

        [StringLength(36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>
        ///     模板文件编码（cert_standard_directory_file.Code）
        ///     <para>⚠️ <b>2026-10-06 裁定（55 §4.5 同义列 #2）</b>：本列的<b>值就是「标准文件 Code」</b>
        ///     （执行器写入 <c>stdFile.Code</c>）⇒ ⛔ <b>不要再新增 <c>StandardFileCode</c></b>，
        ///     那是同一语义的第二列，会导致长期并存且互不相等（静默分叉）。</para>
        /// </summary>
        [StringLength(36)]
        public string TemplateFileCode { get; set; } = string.Empty;

        /// <summary>★ 锚点 → <c>cert_doc_template_anchor.Code</c>（单锚点试跑时填；整份填充时留空）</summary>
        [StringLength(36)]
        public string AnchorCode { get; set; } = string.Empty;

        /// <summary>★ 批次 → <c>yzh_queue.QueueCode</c>（26 号 A-3「生成批次」）</summary>
        [StringLength(36)]
        public string QueueCode { get; set; } = string.Empty;

        /// <summary>★ 单文件任务 → <c>yzh_queue_task.Code</c>（审计下钻到队列明细）</summary>
        [StringLength(36)]
        public string QueueTaskCode { get; set; } = string.Empty;

        /// <summary>文件类型：word/excel</summary>
        [StringLength(10)]
        public string FileKind { get; set; } = string.Empty;

        /// <summary>产物路径（MinIO）</summary>
        [StringLength(512)]
        public string OutputStoragePath { get; set; } = string.Empty;

        /// <summary>锚点总数</summary>
        public int TotalAnchors { get; set; }

        /// <summary>已解析数</summary>
        public int ResolvedCount { get; set; }

        /// <summary>待办数</summary>
        public int PendingCount { get; set; }

        /// <summary>完成度 (0-1)</summary>
        public decimal Completion { get; set; }

        /// <summary>
        ///     ★ 加权可信度 0.00~1.00（口径见 54 §4.5）。
        ///     <para>可由 <c>cert_doc_fill_value</c> 聚合得出，<b>本列是物化快照</b>（列表页直接读，⛔ 不实时聚合）。</para>
        ///     <para>⚠️ 只对 <c>FillStatus='filled'</c> 的行求均值 —— <c>pending</c>（无值）<b>不进分母</b>，
        ///     它的影响体现在<b>完成度</b>上，不体现在可信度上。</para>
        /// </summary>
        public decimal AvgConfidence { get; set; } = 1.00m;

        /// <summary>区域填充数（表格）</summary>
        public int RegionCount { get; set; }

        /// <summary>克隆行数</summary>
        public int ClonedRows { get; set; }

        /// <summary>自验收：无残留锚点且无残留标记</summary>
        public bool Verified { get; set; }

        /// <summary>残留锚点原文</summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public List<string>? LeftoverTokens { get; set; }

        /// <summary>待办明细</summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? PendingsJson { get; set; }

        /// <summary>召回的企业文档 Code 清单</summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public List<string>? RetrievedDocCodes { get; set; }

        /// <summary>Skill 调用轨迹</summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public List<string>? SkillTrace { get; set; }

        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int DurationMs { get; set; }

        /// <summary>状态：success/partial/failed</summary>
        [StringLength(20)]
        public string Status { get; set; } = "success";

        [StringLength(1024)]
        public string? Message { get; set; }

        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
