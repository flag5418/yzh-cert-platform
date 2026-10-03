using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Dir
{
    /// <summary>
    /// 企业原始资料文件行（表 <c>cert_enterprise_original_file</c>）—— 36 号 §3.1 表①
    /// <para><b>端归属</b>：<b>专家端独占</b>（企业原始资料是专家端业务）。
    /// ⛔ <b>不放 <c>CertPlatform.Shared/Entities</c></b> —— 放进去就与「标准目录」「工作流」等
    /// 双端共用的实体混在一起，无法回答「哪些表属于后台、哪些属于专家端」。</para>
    /// <para>规格：docs/20-体系认证/03-详细设计/05-企业资料规范化/36-企业原始资料管理设计-V1.md</para>
    ///
    /// <para><b>★ D9（2026-10-03 用户拍板）Id 零语义</b>：<c>Id</c> 只是自增主键，
    /// ⛔ <b>永不入</b> <c>WHERE</c> / 关联 / <c>Id&gt;0</c>·<c>Id==0</c> 的 add-update 分流 / 存在性判定；
    /// 定位·删除·更新·传参<b>一律只用 <c>Code</c></b>（铁律四）。</para>
    ///
    /// <para><b>★ D7 变更判定</b>：唯一键只有 <c>Code</c>。
    /// ⛔ <b>不建 <c>(EnterpriseCode, StageCode, RelFolderPath, FileName)</c> 联合唯一索引</b> ——
    /// 该 4 列联合索引 = (36+36+512+300)×4 = <b>3536 B &gt; InnoDB 上限 3072 B</b>，
    /// MySQL 8.0 实跑报 <c>ERROR 1071 Specified key was too long</c>。
    /// ⇒ 判重走 <see cref="Sha256"/>：<b>hash 不变 = 同一文件 = 幂等跳过</b>（<see cref="VersionNumber"/> 不动）；
    /// hash 变 = 替换（见 <c>EnterpriseOriginalFileVersion</c>）。</para>
    ///
    /// <para><b>★ D8 版本管理</b>：本行恒指<b>当前活跃版</b>，<see cref="StoragePath"/> 恒为原始路径 ——
    /// 替换时先把旧字节 <c>Rename</c> 到 <c>_archive/{名}.v{n}</c>，再向同一路径写新字节，
    /// 于是预览 / 下载 / 语义分析的路径<b>永不失效</b>。</para>
    ///
    /// <para><b>★ 状态列枚举铁律</b>：<see cref="ConvertStatus"/> / <see cref="MarkdownStatus"/> 只能取
    /// <c>none / pending / converting / completed / failed / unsupported</c>，
    /// 与前端 <c>cert-share/src/utils/convertStatus.ts</c> 逐字对齐。
    /// ⛔ 写 <c>converted</c> ⇒ 徽标 <c>CLASS_MAP</c> 未命中 ⇒ 页面显示「未知状态」<b>且无任何报错</b>。</para>
    /// </summary>
    [SugarTable("cert_enterprise_original_file")]
    public class EnterpriseOriginalFile : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供（Id 零语义，见类注释 D9）───

        /// <summary>企业 Code → <c>cert_enterprise.Code</c>。⛔ 禁 NULL（定位索引含本列）</summary>
        [SugarColumn(Length = 36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>阶段 Code → <c>cert_cert_stage.Code</c>。⛔ 禁 NULL（定位索引含本列）</summary>
        [SugarColumn(Length = 36)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>认证机构 Code（冗余，P13）</summary>
        [SugarColumn(Length = 36)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>当前激活上传批次 → <c>EnterpriseOriginalUploadTask.Code</c>；空 = 不属任何批次</summary>
        [SugarColumn(Length = 36)]
        public string UploadTaskCode { get; set; } = string.Empty;

        /// <summary>相对 <c>{Ent}/{Stage}/</c> 的文件夹路径，<c>/</c> 分隔，空串 = 根。⛔ 不入唯一索引</summary>
        [SugarColumn(Length = 512)]
        public string RelFolderPath { get; set; } = string.Empty;

        [SugarColumn(Length = 300)]
        public string FileName { get; set; } = string.Empty;

        /// <summary>扩展名（小写，含点，如 <c>.docx</c>）</summary>
        [SugarColumn(Length = 20)]
        public string FileType { get; set; } = string.Empty;

        /// <summary>字节数（当前版）</summary>
        public long FileSize { get; set; }

        /// <summary>★ 当前版内容指纹 —— D7 变更判定唯一依据（hash 不变 = 同一文件 = 幂等）</summary>
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

        /// <summary>PDF 产物（<c>PathBuilder.Product</c> 派生）</summary>
        [SugarColumn(Length = 512, IsNullable = true)]
        public string? PreviewPdfPath { get; set; }

        /// <summary>★ Markdown 产物（语义分析输入）</summary>
        [SugarColumn(Length = 512, IsNullable = true)]
        public string? MarkdownPath { get; set; }

        /// <summary><c>none/pending/converting/completed/failed/unsupported</c>。⛔ 禁 <c>converted</c></summary>
        [SugarColumn(Length = 20)]
        public string MarkdownStatus { get; set; } = "none";

        [SugarColumn(Length = 1024, IsNullable = true)]
        public string? MarkdownMessage { get; set; }

        /// <summary>双产物完成时间</summary>
        public DateTime? ConvertDate { get; set; }

        // ──── 分析 ────

        /// <summary><c>pending/analyzing/analyzed/failed/skipped</c></summary>
        [SugarColumn(Length = 20)]
        public string AnalyzeStatus { get; set; } = "pending";

        [SugarColumn(Length = 1024, IsNullable = true)]
        public string? AnalyzeMessage { get; set; }

        /// <summary>最近分析完成时间</summary>
        public DateTime? AnalyzeTime { get; set; }

        // ──── 分析策略（36 号 §3.5 三层区分：只作用于 L2「数据来源」层）───

        /// <summary><c>analyze/skip/ignore</c>（字典 <c>ANALYZE_POLICY</c>）</summary>
        [SugarColumn(Length = 20)]
        public string AnalyzePolicy { get; set; } = "analyze";

        /// <summary>
        /// 策略原因（字典 <c>POLICY_REASON</c>）或 AI 建议说明。
        /// <para>⚠️ <b>非空 + 默认空串</b>：DB 列是 <c>NOT NULL DEFAULT ''</c>，而实体若声明成
        /// <c>string?</c>，SqlSugar 会<b>显式插入 NULL</b> ⇒ <c>Column 'PolicyReason' cannot be null</c>。
        /// 2026-10-03 实测踩到，且 <c>IDbOrm.InsertAsync</c> <b>吞异常只返回 Result.Fail</b>，
        /// 调用方不检查就拿不到 FileCode ⇒ 后续整条链路静默失败。</para>
        /// </summary>
        [SugarColumn(Length = 200)]
        public string PolicyReason { get; set; } = string.Empty;

        /// <summary><c>ai</c>（建议，未确认前 ⛔ 不改 AnalyzePolicy）/ <c>manual</c>（人工确认）</summary>
        [SugarColumn(Length = 10, IsNullable = true)]
        public string? PolicySource { get; set; }

        /// <summary>策略决策人（留痕）</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string? PolicyDecidedBy { get; set; }

        /// <summary>策略决策时间（留痕）</summary>
        public DateTime? PolicyDecidedTime { get; set; }

        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        // ──── ISoftDelete + IIsValid 显式实现（铁律九：启用唯一字段 = IsValid，⛔ 禁 Enable）───

        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}