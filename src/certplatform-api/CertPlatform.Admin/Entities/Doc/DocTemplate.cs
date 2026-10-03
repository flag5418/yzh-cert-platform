using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;
using YZH.Entity.Admin.Platform;

namespace CertPlatform.Admin.Entities.Doc
{
    /// <summary>标准文档模板（空白模板登记）</summary>
    /// <para>表名：cert_doc_template</para>
    /// <para>
    /// ★ 业务定位：<b>「文档填写规则」的宿主</b> —— 一行 = 一份已登记进系统的空白模板。
    /// 锚点规则（<c>cert_doc_template_anchor</c>）与全文填写提示词（<c>cert_doc_fill_prompt</c>）
    /// 都挂在模板 Code 之下；填写引擎产出的 <c>cert_doc_fill_log</c> 也引用本表。
    /// </para>
    /// <para>
    /// ★ 本表<b>只登记，不生成</b>：模板字节由人工上传后落在
    /// <c>PathBuilder.TemplateFile()</c> 指向的 <c>_template/</c> 段，
    /// 本表只记 <see cref="StoragePath"/>。
    /// </para>
    /// <para>
    /// ⛔ 扫描类列（<see cref="ScanStatus"/> / <see cref="PartCount"/> / <see cref="ViolationJson"/> …）
    /// 首版<b>只读不写</b> —— 用户已裁定「扫描分析当前材料没有意义」，等真实模板到位再启用。
    /// </para>
    /// <para>ORM：SqlSugar（铁律：DB 列名 == C# 属性名，PascalCase 逐字一致）</para>
    /// <para>设计依据：37 号 §四（表结构）· 38 号 §15.8（S0 DDL）</para>
    [SugarTable("cert_doc_template")]
    [YZHDeleteStrategy(Mode = DeleteMode.Soft)]
    public class DocTemplate : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / CreateTime / CreateBy / UpdateTime / UpdateBy 由 BaseEntity 提供 ────

        // ──── 身份段（⛔ 禁 NULL，服务端填充）────

        /// <summary>★ 宿主标准文件 Code → <c>cert_standard_directory_file.Code</c></summary>
        [StringLength(36)]
        [UniqueField("标准文件Code")]
        public string StandardFileCode { get; set; } = string.Empty;

        /// <summary>冗余：认证机构编码</summary>
        [StringLength(36)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>冗余：标准 Code（GUID）</summary>
        [StringLength(36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>冗余：阶段 Code（GUID，⛔ 不是业务短码 jd01/03）</summary>
        [StringLength(36)]
        public string StageCode { get; set; } = string.Empty;

        // ──── 模板文件 ────

        /// <summary>docx / xlsx（上传侧已统一，只有这两种）</summary>
        [Required]
        [StringLength(10)]
        public string FileKind { get; set; } = string.Empty;

        /// <summary>上传时原始文件名</summary>
        [Required]
        [StringLength(500)]
        public string FileName { get; set; } = string.Empty;

        /// <summary>模板路径 → <c>PathBuilder.TemplateFile()</c>（<c>_template/</c> 段下）</summary>
        [Required]
        [StringLength(512)]
        public string StoragePath { get; set; } = string.Empty;

        /// <summary>模板指纹：相同则跳过重扫</summary>
        [StringLength(64)]
        public string? SourceSha256 { get; set; }

        /// <summary>全文填写规则 → <c>cert_doc_fill_prompt.PromptCode</c>（空 = 不走全文规则）</summary>
        [StringLength(100)]
        public string? FillPromptCode { get; set; }

        // ──── 扫描/校验结果（首版只读不写，见类注释）────

        /// <summary>pending / processing / completed / failed</summary>
        [StringLength(20)]
        public string ScanStatus { get; set; } = "pending";

        /// <summary>扫描失败原因</summary>
        [StringLength(1024)]
        public string? ScanMessage { get; set; }

        /// <summary>最近扫描时间</summary>
        public DateTime? ScanTime { get; set; }

        /// <summary>识别到的锚点总数</summary>
        public int PartCount { get; set; }

        /// <summary>识别到的书签数</summary>
        public int BookmarkCount { get; set; }

        /// <summary>识别到的 YZH_Mark 标记数</summary>
        public int MarkCount { get; set; }

        /// <summary>【W1-W9/E1-E8 校验结果】<c>[{"code":"E6","level":"error","message":"…","anchor":"…"}]</c></summary>
        public string? ViolationJson { get; set; }

        /// <summary>概览 <c>{"sections":3,"headers":["default","first"],"bookmarks":8}</c></summary>
        public string? SummaryJson { get; set; }

        // ──── 状态 ────

        /// <summary>draft=已上传未扫描 / scanned=已扫描待处理 / ready=校验通过待发布 / published=已发布</summary>
        [StringLength(20)]
        public string PublishStatus { get; set; } = "draft";

        [StringLength(500)]
        public string? Remark { get; set; }

        // ──── 接口字段（BaseEntity 不含，必须声明在实体自身，否则全库过滤静默失效）────

        /// <summary>有效标志（1=有效，0=无效）。⛔ 禁 Enable</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code（仅软删除时赋值）</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间（仅软删除时赋值）</summary>
        public DateTime? DeleteTime { get; set; }
    }
}
