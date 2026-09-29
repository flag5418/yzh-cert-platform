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

        /// <summary>⛔ 遗留字段（旧 .doc→.docx 单产物链），已停止写入新值；产物改用 <see cref="PreviewPdfPath"/> / <see cref="MarkdownPath"/></summary>
        [SugarColumn(Length = 512, IsNullable = true)]
        public string? ConvertedStoragePath { get; set; }

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? ConvertStatus { get; set; }

        /// <summary>
        /// 提取状态：none=未触发 / pending=入队等待 / processing=提取中 / completed=完成 / failed=失败
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

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
