using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Dir
{
    /// <summary>
    /// 企业文件操作日志实体（只追加，不修改不删除）
    /// <para>表名：<c>cert_enterprise_file_op_log</c>（DDL 权威：06 册 02 号 §六）</para>
    /// <para>职责：企业文件资产审计（upload/replace/delete/restore/extract_trigger/extract_done 六类）；
    /// 与 07 号册 <c>cert_doc_match_decision</c>（匹配/裁决审计）并行、互不重复。</para>
    /// <para>命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase</para>
    /// </summary>
    [SugarTable("cert_enterprise_file_op_log")]
    public class EnterpriseFileOpLog : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / CreateTime / CreateBy / UpdateTime / UpdateBy 由 BaseEntity 提供 ────

        /// <summary>企业 Code → cert_enterprise.Code（真实企业）</summary>
        [SugarColumn(Length = 36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>阶段 Code → cert_cert_stage.Code</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? StageCode { get; set; }

        /// <summary>→ cert_standard_directory_file.Code（企业行）</summary>
        [SugarColumn(Length = 36)]
        public string FileCode { get; set; } = string.Empty;

        /// <summary>操作类型：upload/replace/delete/restore/extract_trigger/extract_done</summary>
        [SugarColumn(Length = 20)]
        public string OpType { get; set; } = string.Empty;

        /// <summary>本次操作产生的版本号（无关操作为 NULL）</summary>
        public int? VersionNumber { get; set; }

        /// <summary>JSON：文件名/大小/映射来源/失败原因/Reason 双写等</summary>
        [SugarColumn(Length = 1000, IsNullable = true)]
        public string? Detail { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
