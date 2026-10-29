using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Dir
{
  /// <summary>
  /// 企业文档上传批次（表 <c>cert_enterprise_doc_upload_task</c>）—— 36 号 §4.1 表③
  ///
  /// <para><b>用途</b>：一次「选择 N 个文件 → 上传 → 确认」= 一个批次。
  /// 干跑（init）与确认（confirm）之间，文件行先挂在批次上；用户在确认前取消 ⇒ 按批次整体撤草稿。</para>
  ///
  /// <para><b>★ 与阶段无关</b>：老表 <c>cert_enterprise_original_upload_task</c> 带 <c>StageCode</c>，
  /// 本表已去掉（一阶段二阶段资料相同，不按阶段管理）。</para>
  ///
  /// <para><b>状态机</b>：<c>draft → uploading → confirmed</c>；
  /// 任一步失败 ⇒ <c>failed</c>；用户取消 ⇒ <c>cancelled</c>。
  /// ⚠️ <c>ExpireTime</c> 超时未 confirm 的草稿由清理任务置 <c>cancelled</c>。</para>
  /// </summary>
  [SugarTable("cert_enterprise_doc_upload_task")]
  public class EnterpriseDocUploadTask : BaseEntity, ISoftDelete, IIsValid
  {
    /// <summary>企业 Code → <c>cert_enterprise.Code</c></summary>
    [SugarColumn(Length = 36)]
    public string EnterpriseCode { get; set; } = string.Empty;

    /// <summary>计划份数</summary>
    public int TotalFiles { get; set; }

    /// <summary>计划字节数</summary>
    public long TotalSize { get; set; }

    /// <summary>成功写入数</summary>
    public int SuccessCount { get; set; }

    /// <summary>幂等跳过数（同 hash）</summary>
    public int SkipCount { get; set; }

    /// <summary>替换数（异 hash，主表 VersionNumber 已递增）</summary>
    public int ReplaceCount { get; set; }

    /// <summary><c>draft/uploading/confirmed/cancelled/failed</c></summary>
    [SugarColumn(Length = 20)]
    public string Status { get; set; } = "draft";

    /// <summary>失败/取消原因。⚠️ 失败 ⛔ 不许静默，必须写这里</summary>
    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? Message { get; set; }

    /// <summary>草稿过期时间（init 后超时未 confirm 即作废）</summary>
    public DateTime? ExpireTime { get; set; }

    public bool IsDeleted { get; set; }
    public string? DeleteBy { get; set; }
    public DateTime? DeleteTime { get; set; }
    public int IsValid { get; set; } = 1;
  }
}
