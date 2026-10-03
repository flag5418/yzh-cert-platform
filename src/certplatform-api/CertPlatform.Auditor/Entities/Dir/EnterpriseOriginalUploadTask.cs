using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Dir
{
    /// <summary>
    /// 企业原始资料上传批次（表 <c>cert_enterprise_original_upload_task</c>）—— 36 号 §3.3 表③
    /// <para><b>端归属</b>：专家端独占。</para>
    ///
    /// <para><b>为什么必须有</b>：五段式上传的 <c>upload/cancel</c> 要能「撤草稿行」，
    /// 而文件行（表①）<b>不能拿批次号当唯一键</b>（否则一批 N 个文件互相覆盖一个 TaskId）⇒ 必须另立批次表。
    /// 对标物：<c>cert_upload_task</c> + <c>EnterpriseFileService.GetDraftRowAsync(fileCode, taskId)</c>。</para>
    /// </summary>
    [SugarTable("cert_enterprise_original_upload_task")]
    public class EnterpriseOriginalUploadTask : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────

        /// <summary>企业 Code</summary>
        [SugarColumn(Length = 36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>阶段 Code</summary>
        [SugarColumn(Length = 36)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>计划份数</summary>
        public int TotalFiles { get; set; }

        /// <summary>计划字节数</summary>
        public long TotalSize { get; set; }

        /// <summary>已完成份数</summary>
        public int SuccessCount { get; set; }

        /// <summary>幂等跳过份数（D7：同 hash，<c>VersionNumber</c> 不变、不重跑 LLM）</summary>
        public int SkipCount { get; set; }

        /// <summary>替换份数（D8：异 hash，<c>VersionNumber+1</c> + 追加一行版本表）</summary>
        public int ReplaceCount { get; set; }

        /// <summary><c>draft/uploading/confirmed/cancelled/failed</c></summary>
        [SugarColumn(Length = 20)]
        public string Status { get; set; } = "draft";

        [SugarColumn(Length = 1024, IsNullable = true)]
        public string? Message { get; set; }

        /// <summary>草稿过期时间（init 后超时未 confirm 即作废，防悬空草稿行永久占位）</summary>
        public DateTime? ExpireTime { get; set; }

        // ──── ISoftDelete + IIsValid ────

        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}