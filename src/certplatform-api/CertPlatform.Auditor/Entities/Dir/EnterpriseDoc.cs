using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Dir
{
  /// <summary>
  /// 企业文档（表 <c>cert_enterprise_doc</c>）—— 36 号 §4.1 表①
  ///
  /// <para><b>★★★ 唯一一层，无阶段 / 无标准 / 无槽位</b>（2026-10-29 用户裁定）：
  /// 一阶段与二阶段的企业资料<b>是同一批文件</b>，按阶段管理必然错位
  /// （实测：老表 237 份文件全挂 S2，唯一计划挂 AP）。
  /// ⇒ 本表只回答「这家企业有什么文件」，<b>不回答「这份文件属于哪个阶段/标准」</b>。
  /// 标准出现在<b>下游</b>：<see cref="DocumentExtraction"/>（文档 × 标准 = 一份提取产物）。</para>
  ///
  /// <para><b>端归属</b>：专家端独占（企业原始资料是专家端业务），⛔ 不放 <c>CertPlatform.Shared</c>。</para>
  ///
  /// <para><b>★ 定位一律用 <c>Code</c></b>（铁律四）：<c>Id</c> 零语义，⛔ 永不入 <c>WHERE</c> / 关联。</para>
  ///
  /// <para><b>★ 变更判定</b>：唯一键只有 <c>Code</c>；<c>(EnterpriseCode, RelFolderPath, FileName)</c>
  /// 不建联合唯一索引（3 列超 InnoDB 3072 B 上限 ⇒ <c>ERROR 1071</c>），
  /// 只建前缀普通索引 <c>idx_ent_path</c>。<b>判重走 <see cref="Sha256"/></b>：
  /// hash 不变 = 同一文件 = 幂等跳过（<see cref="VersionNumber"/> 不动）；hash 变 = 替换（见
  /// <c>EnterpriseDocVersion</c>）。</para>
  ///
  /// <para><b>★ 版本管理</b>：本行恒指<b>当前活跃版</b>，<see cref="StoragePath"/> 恒为当前路径 ——
  /// 替换时先把旧字节 <c>Rename</c> 到 <c>_archive/{名}.v{n}</c>，再向同一路径写新字节，
  /// 于是预览 / 下载 / 提取的路径<b>永不失效</b>。</para>
  ///
  /// <para><b>★ 状态列枚举铁律</b>：<see cref="ConvertStatus"/> / <see cref="MarkdownStatus"/> 只能取
  /// <c>none / pending / converting / completed / failed / unsupported</c>，
  /// 与前端 <c>cert-share/src/utils/convertStatus.ts</c> 逐字对齐。
  /// ⛔ 写 <c>converted</c> ⇒ 徽标 <c>CLASS_MAP</c> 未命中 ⇒ 页面显示「未知状态」<b>且无任何报错</b>。</para>
  /// </summary>
  [SugarTable("cert_enterprise_doc")]
  public class EnterpriseDoc : BaseEntity, ISoftDelete, IIsValid
  {
    // ──── Id / Code / 审计字段由 BaseEntity 统一提供 ────

    /// <summary>企业 Code → <c>cert_enterprise.Code</c>。⛔ 禁 NULL（定位索引含本列）</summary>
    [SugarColumn(Length = 36)]
    public string EnterpriseCode { get; set; } = string.Empty;

    /// <summary>
    /// 认证机构 Code（隔离冗余，与 <c>cert_audit_plan</c> 同源）。
    /// <para>⚠️ <b>⛔ 不作筛选条件</b>：列表按企业过滤即可，机构只用于留痕与跨端对账。</para>
    /// </summary>
    [SugarColumn(Length = 36, IsNullable = true)]
    public string? OrgCode { get; set; }

    /// <summary>当前激活上传批次 → <c>cert_enterprise_doc_upload_task.Code</c>；空 = 不属任何批次</summary>
    [SugarColumn(Length = 36, IsNullable = true)]
    public string? UploadTaskCode { get; set; }

    /// <summary>相对 <c>{Ent}/</c> 的文件夹路径，<c>/</c> 分隔，空串 = 根。⛔ 不入唯一索引</summary>
    [SugarColumn(Length = 512)]
    public string RelFolderPath { get; set; } = string.Empty;

    [SugarColumn(Length = 300)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>扩展名（小写，含点，如 <c>.docx</c>）</summary>
    [SugarColumn(Length = 20)]
    public string FileType { get; set; } = string.Empty;

    /// <summary>字节数（当前版）</summary>
    public long FileSize { get; set; }

    /// <summary>★ 当前版内容指纹 —— 变更判定唯一依据（hash 不变 = 同一文件 = 幂等）</summary>
    [SugarColumn(Length = 64)]
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>★ MinIO 源路径（恒指当前活跃版 ⇒ 外部引用永不失效）</summary>
    [SugarColumn(Length = 512)]
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>★ 替换递增；旧字节走 <c>PathBuilder.Archive</c>（<c>_archive/{名}.v{n}</c>）</summary>
    public int VersionNumber { get; set; } = 1;

    // ──── 转换（枚举对齐 convertStatus.ts）───

    /// <summary><c>none/pending/converting/completed/failed/unsupported</c>。⛔ 禁 <c>converted</c></summary>
    [SugarColumn(Length = 20)]
    public string ConvertStatus { get; set; } = "none";

    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? ConvertMessage { get; set; }

    /// <summary>PDF 产物（<c>PathBuilder.Product</c> 派生）—— 前端预览用</summary>
    [SugarColumn(Length = 512, IsNullable = true)]
    public string? PreviewPdfPath { get; set; }

    /// <summary>★ Markdown 产物（保留企业原文；<b>提取的唯一输入</b>）</summary>
    [SugarColumn(Length = 512, IsNullable = true)]
    public string? MarkdownPath { get; set; }

    /// <summary><c>none/pending/converting/completed/failed/unsupported</c>。⛔ 禁 <c>converted</c></summary>
    [SugarColumn(Length = 20)]
    public string MarkdownStatus { get; set; } = "none";

    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? MarkdownMessage { get; set; }

    /// <summary>双产物（PDF + Markdown）完成时间</summary>
    public DateTime? ConvertDate { get; set; }

    // ──── 提取聚合状态（明细见 cert_document_extraction）───

    /// <summary>
    /// 聚合状态：<c>none/partial/running/done/failed/skipped</c>。
    /// <para>本文件对「本企业计划内的所有标准」的提取汇总：
    /// <c>partial</c> = 有的标准跑完、有的没跑（新增标准后必现）。</para>
    /// </summary>
    [SugarColumn(Length = 20)]
    public string ExtractStatus { get; set; } = "none";

    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? ExtractMessage { get; set; }

    /// <summary>最近提取完成时间</summary>
    public DateTime? ExtractTime { get; set; }

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Remark { get; set; }

    // ──── ISoftDelete + IIsValid 显式实现（铁律九：启用唯一字段 = IsValid，⛔ 禁 Enable）───

    public bool IsDeleted { get; set; }
    public string? DeleteBy { get; set; }
    public DateTime? DeleteTime { get; set; }
    public int IsValid { get; set; } = 1;
  }
}
