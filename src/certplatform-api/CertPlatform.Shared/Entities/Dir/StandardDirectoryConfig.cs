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

        /// <summary>
        /// 后台域标记 —— <b>两值模型</b>（用户 2026-10-12/10-13 口径）。
        ///
        /// <para>模板行（<see cref="EnterpriseCode"/> 为空）⇒ 恒 <c>CoreOrgCodes.StdEnv</c>（<c>yzh-std-env</c>）
        /// —— 「所有机构的统称」，表示这行是<b>后台建立的标准级数据</b>。</para>
        /// <para>⛔ <b>具体机构不在本列</b>（本列已统一为后台域标记）—— 见 <see cref="OwnerOrgCode"/>。</para>
        /// <para>⚠️ 历史遗留：企业行（<c>EnterpriseCode</c> 有值）的 <c>OrgCode</c> 仍为机构 Code
        /// （2026-10-12 终裁「企业行不在本裁决范围，OrgCode 原样」）—— 待后续统一，见设计文档 §五。</para>
        /// </summary>
        [SugarColumn(Length = 50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>
        /// ★ <b>具体机构 Code</b>（= <c>sys_organization.Code</c> = <c>cert_certification_body.Code</c>）
        /// —— 目录的<b>归属机构</b>维度（2026-10-13 新增，承接用户口径「需要具体机构则增加新字段」）。
        ///
        /// <para>取值：<c>''</c>（不特指任何机构 —— 平台通用模板）或 <c>&lt;机构 Code&gt;</c>。</para>
        /// <para>★ <b>用途</b>：① 路径身份段（<c>PathBuilder.StandardFile</c>）② 唯一键第一列
        /// （<c>uk_owner_std_stage</c>）—— 恢复「按机构隔离」（决策⑳修订的本意）。</para>
        /// <para>⛔ <b>不要</b>把机构值写进 <see cref="EnterpriseCode"/>（守卫 <b>A-R11</b>）；
        /// ⛔ <b>不要</b>用 <see cref="OrgCode"/> 承载具体机构（它已统一为后台域标记）。</para>
        /// </summary>
        [SugarColumn(Length = 50)]
        public string OwnerOrgCode { get; set; } = string.Empty;

        /// <summary>
        /// 企业 Code。真实值 = 该企业该标准该阶段的企业目录；<b>空 = 不属于任何企业</b>
        /// （后台 / 机构行 —— 平台标准域行）。
        ///
        /// <para>★ <b>2026-10-13 判据收敛（修订 10-12 版）</b>：原写「<c>CoreOrgCodes.StdEnv</c> = 机构模板行」
        /// —— 那是把<b>机构维度的值填进企业列</b>，语义错位（实测：全库 <c>EnterpriseCode</c>
        /// 无任何一行是机构常量）。唯一判据入口 = <c>OrgScope.IsEnterpriseRow</c>（守卫 <b>A-R11</b>）。</para>
        /// <para>⛔ 禁 NULL（P13，2026-09-28）：uk 对 NULL 不生效，NULL/常量双口径必出脏数据。</para>
        /// <para>唯一键 <c>uk_owner_std_stage(OwnerOrgCode, EnterpriseCode, StandardCode, StageCode)</c>（P16 四列）。</para>
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
