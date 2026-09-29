using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Dir
{
    /// <summary>
    /// 标准目录配置实体 —— 一行 = 「某机构 × 某标准 × 某阶段」的标准目录（总表/主表）。
    ///
    /// <para>★ <b>按机构隔离</b>（决策⑳修订，2026-09-27 用户改判）：标准目录是<b>各机构的标准落地
    /// 目录模板</b>，不同机构对同一「标准 × 阶段」的目录结构与文件允许不同 ⇒ 本表设 <c>OrgCode</c> 列，
    /// 唯一键为 <c>(OrgCode, StandardCode, StageCode)</c>。原「平台全局库、不设 OrgCode」作废。
    /// 子表（folder/file）不加机构列 —— 经 <c>ConfigCode</c> → 本表 <c>OrgCode</c> 间接归属。</para>
    ///
    /// <para>★ <b>无感懒建</b>（决策㉑，2026-09-27）：本行由后端 Ensure 在「首次进入阶段 / 上传」时
    /// 自动创建，<b>不要求用户手工新建</b>；「目录配置」界面降级为管理入口（改根名 / 状态 / 级联清理）。
    /// 删除后再次进入会自动复活同 Code 行（子树保持已删 = 空目录）。</para>
    ///
    /// <para>命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase</para>
    /// </summary>
    [SugarTable("cert_standard_directory_config")]
    public class StandardDirectoryConfig : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────
        // ⛔ 复合编码 DirectoryCode（SDC-{标准}|{阶段}）已于 2026-09-26 删除，本行 Code 即业务键。

        /// <summary>机构 Code → <c>certification_body.Code</c>（决策⑳修订：目录归属主体）</summary>
        [SugarColumn(Length = 50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>
        /// 企业 Code：<c>YZH-STD-ENT</c>（<c>YzhVirtualEnterprise.Code</c>）= 机构模板行；真实值 = 该企业该标准该阶段的企业目录。
        /// <para>★ 与标准目录复用同一张表：EnterpriseCode 列区分「模板行」与「企业上传行」。</para>
        /// <para>⛔ 禁 NULL（P13，2026-09-28）：uk 对 NULL 不生效，NULL/常量双口径必出脏数据；
        /// 唯一键 <c>uk_enterprise_std_stage(OrgCode, EnterpriseCode, StandardCode, StageCode)</c>（P16 四列）。</para>
        /// </summary>
        [SugarColumn(Length = 36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>标准 Code → <c>cert_iso_standard.Code</c>（GUID）</summary>
        [SugarColumn(Length = 36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>阶段 Code → <c>cert_cert_stage.Code</c>（GUID）。原列名 <c>PhaseCode</c>，决策 ⑩ 统一为 StageCode</summary>
        [SugarColumn(Length = 36)]
        public string StageCode { get; set; } = string.Empty;

        [SugarColumn(Length = 200, IsNullable = true)]
        public string? RootFolderName { get; set; }

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? Status { get; set; } = "draft";

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? StatusField { get; set; } = "active";

        public int Sort { get; set; } = 0;

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Remark { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
