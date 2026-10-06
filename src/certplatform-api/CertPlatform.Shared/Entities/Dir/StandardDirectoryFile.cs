using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Dir
{
    /// <summary>标准目录文件实体</summary>
    /// <para>命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase</para>
    /// <para>⛔ 复合编码 <c>FileCode</c>（FL-{文件夹}|{文件名}，实测 42–72 字符且内嵌文件名）已于 2026-09-26 删除，
    /// 本行 <c>Code</c>（GUID）即业务键 —— 也是提取规则 <c>StandardFileCode</c> 的取值来源。</para>
    [SugarTable("cert_standard_directory_file")]
    public class StandardDirectoryFile : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────

        /// <summary>文件夹 Code → <c>cert_standard_directory_folder.Code</c>；<b>根级文件恒为 <c>""</c></b></summary>
        [SugarColumn(Length = 36)]
        public string FolderCode { get; set; } = string.Empty;

        /// <summary>父文件 Code → <c>cert_standard_directory_file.Code</c>（根级恒 <c>""</c>）。14 号 D19 要求 P0 即加</summary>
        [SugarColumn(Length = 36)]
        public string ParentFileCode { get; set; } = string.Empty;

        /// <summary>章节锚点（大文档切片定位用，可空）</summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string? SectionAnchor { get; set; }

        /// <summary>配置 Code → <c>cert_standard_directory_config.Code</c>（原列名 <c>DirectoryCode</c>）</summary>
        [SugarColumn(Length = 36)]
        public string ConfigCode { get; set; } = string.Empty;

        /// <summary>企业 Code：<c>YZH-STD-ENT</c>（<c>YzhVirtualEnterprise.Code</c>）= 模板文件定义行；真实值 = 该企业实际上传的文件行。⛔ 禁 NULL（P13，2026-09-28）。</summary>
        [SugarColumn(Length = 36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>标准 Code（冗余，来自 ConfigCode 关联的 config 表）</summary>
        [SugarColumn(Length = 36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>阶段 Code（冗余，来自 ConfigCode 关联的 config 表）</summary>
        [SugarColumn(Length = 36)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>关联标准文件Code → cert_standard_directory_file.Code（模板行）</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? StandardFileCode { get; set; }

        [SugarColumn(Length = 500)]
        public string FileName { get; set; } = string.Empty;

        [SugarColumn(Length = 50)]
        public string FileType { get; set; } = string.Empty;

        [SugarColumn(Length = 200, IsNullable = true)]
        public string? FilePattern { get; set; }

        public long? FileSize { get; set; }

        /// <summary>当前内容版本号：企业槽位版本链从 1 起、跨标准独立（02 号 §二）；模板行恒 1。
        /// confirm 首传显式落 1（G-1b），替换在归档时序中递增（G-3a）；B-08 四元组对账基准。</summary>
        public int VersionNumber { get; set; } = 1;

        public bool IsRequired { get; set; } = true;

        public int MaxFileSizeMB { get; set; } = 10;

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Description { get; set; }

        public int SortOrder { get; set; } = 0;

        public bool ExtractionEnabled { get; set; } = false;

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? ExtractionRules { get; set; }

        public bool PreCheckRequired { get; set; } = true;

        public bool ComplianceRequired { get; set; } = false;

        /// <summary>
        ///     ★ 文档分类：<c>fixed</c>=固定文档（营业执照/生产许可等，<b>匹配即终点</b>）/
        ///     <c>hybrid</c>=混合 / <c>editable</c>=可编写（进 ⑧ 提取 → ⑨ 填充）。
        ///     <para>⛔ 决定 04 号 §5.2 用哪套加权公式，不得留空（05 号 §3，默认 <c>editable</c>）。</para>
        ///     <para>34 号 §2.3：流程在 ⑦ 裁决后按本列分叉。</para>
        /// </summary>
        [SugarColumn(Length = 20)]
        public string DocCategory { get; set; } = "editable";

        /// <summary>匹配状态：<c>none</c> / <c>recalled</c> / <c>scored</c> / <c>conflicted</c> / <c>matched</c> / <c>unmatched</c></summary>
        [SugarColumn(Length = 20)]
        public string MatchState { get; set; } = "none";

        /// <summary>
        ///     实例状态：<c>none</c> / <c>pending</c> / <c>filling</c> / <c>filled</c> / <c>confirmed</c> / <c>archived</c>。
        ///     <para>⚠️ <c>DocCategory=fixed</c> 的文档裁决后直接到 <c>matched</c>，<b>不进 <c>filling</c></b>（34 号 §2.3 短路分支）。</para>
        /// </summary>
        [SugarColumn(Length = 20)]
        public string InstanceState { get; set; } = "none";

        /// <summary>关联契约 → <c>cert_standard_doc_contract.Code</c>（空串 = 该标准文档未配契约，不参与匹配）</summary>
        [SugarColumn(Length = 36)]
        public string ContractCode { get; set; } = string.Empty;

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? Status { get; set; } = "draft";

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? StatusField { get; set; } = "active";

        public int Sort { get; set; } = 0;

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Remark { get; set; }

        [SugarColumn(Length = 64, IsNullable = true)]
        public string? TaskId { get; set; }

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? UploadStatus { get; set; } = "active";

        [SugarColumn(Length = 512, IsNullable = true)]
        public string? StoragePath { get; set; }

        /// <summary>
        /// 逻辑相对路径（判重键）= 文件夹路径 + 文件名。★ 收窄到 500 —— 1024 会让唯一索引
        /// <c>uk_cfg_fullpath (ConfigCode, FullPath)</c> 超出 InnoDB 3072 字节上限。
        /// <para>⚠️ 与 <see cref="StoragePath"/>（物理键）**必须分离**：版本号只能进物理键，
        /// 否则每次上传都是新 FullPath ⇒ 永远判不出重复（陷阱 ㉙）。</para>
        /// </summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? FullPath { get; set; }

        /// <summary>
        /// 文件夹路径段（FullPath 剔除文件名后的部分）
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public string FolderPath => string.IsNullOrEmpty(FullPath) || string.IsNullOrEmpty(FileName)
            ? string.Empty
            : (FullPath.EndsWith(FileName) ? FullPath.Substring(0, FullPath.Length - FileName.Length).TrimEnd('/') : FullPath);

        /// <summary>⛔ 遗留字段（旧 .doc→.docx 单产物链），已停止写入新值；产物改用 <see cref="PreviewPdfPath"/> / <see cref="MarkdownPath"/></summary>
        [SugarColumn(Length = 512, IsNullable = true)]
        public string? ConvertedStoragePath { get; set; }

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? ConvertStatus { get; set; }

        /// <summary>
        /// 提取状态（★ 2026-09-30 与 <see cref="DocExtraction.EnterpriseExtractStatus"/> 对齐为 4 态）：
        /// <c>none</c>=未提取 / <c>completed</c>=已提取 / <c>failed</c>=有规则但执行失败 / <c>skipped</c>=无可用规则。
        /// <para>仅企业上传行（EnterpriseCode 有真实值）有意义；模板行恒为 NULL。</para>
        /// </summary>
        [SugarColumn(Length = 20)]
        public string ExtractStatus { get; set; } = "none";

        [SugarColumn(Length = 1024, IsNullable = true)]
        public string? ExtractMessage { get; set; }

        /// <summary>最高提取置信度（聚合自 cert_extraction_result，同 StandardFileCode 的所有字段取最高）</summary>
        [SugarColumn(IsNullable = true)]
        public decimal? MaxConfidence { get; set; }

        [SugarColumn(Length = 1024, IsNullable = true)]
        public string? ConvertMessage { get; set; }

        public DateTime? ConvertDate { get; set; }

        [SugarColumn(Length = 512, IsNullable = true)]
        public string? PreviewPdfPath { get; set; }

        [SugarColumn(Length = 512, IsNullable = true)]
        public string? MarkdownPath { get; set; }

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? MarkdownStatus { get; set; } = "none";

        [SugarColumn(Length = 1024, IsNullable = true)]
        public string? MarkdownMessage { get; set; }

        public DateTime? MarkdownDate { get; set; }

        // ──── ★ 归一链（2026-10-03，S-1）：旧二进制格式 → OOXML ────
        //   产物段 PathBuilder.EditableSegment（"editable"），与 pdf/ markdown/ 对称。
        //   ⚠️ 为什么必须有这条链：NPOI 2.7.2 **没有 NPOI.HWPF** ⇒ .doc 连读都读不了，
        //      而本库 668 份里 .doc 567 + .xls 44 = 91.5% 是旧格式。不归一 ⇒ 填写引擎无输入。

        /// <summary>
        /// ★ 归一后的可编辑版本路径（<c>.docx</c>/<c>.xlsx</c>/<c>.pptx</c>）；
        /// <b>空 = 尚未归一</b>（填写引擎此时应回退用 <see cref="StoragePath"/>）。
        /// <para>产物段见 <see cref="CertPlatform.Shared.Storage.PathBuilder.EditableSegment"/>：
        /// <c>…/editable/{完整原文件名}.docx</c>。</para>
        /// </summary>
        [SugarColumn(Length = 512, IsNullable = true)]
        public string? EditableStoragePath { get; set; }

        /// <summary>
        /// ★ 归一状态：<c>pending</c> / <c>completed</c> / <c>failed</c>；
        /// <b>空（NULL）= 不需要归一</b>（本来就是 <c>.docx</c>/<c>.xlsx</c>/<c>.pdf</c> 等）。
        ///
        /// <para>⛔ 「空」与「<c>pending</c>」语义<b>不可互换</b>：空 = 这条链对该文件不适用，
        /// <c>pending</c> = 已入队待跑。回填端点按「扩展名是否旧格式」决定投不投，
        /// 不按本列是否为空 —— 否则已归一的文件会被反复重投。</para>
        /// </summary>
        [SugarColumn(Length = 20, IsNullable = true)]
        public string? EditableStatus { get; set; }

        /// <summary>★ 归一失败原因（<c>completed</c> 时为 null 或提示语）</summary>
        [SugarColumn(Length = 1024, IsNullable = true)]
        public string? EditableMessage { get; set; }

        /// <summary>★ 最近归一时间</summary>
        public DateTime? EditableDate { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
