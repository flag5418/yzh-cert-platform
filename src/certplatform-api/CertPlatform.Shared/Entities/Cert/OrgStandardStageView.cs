using System.ComponentModel.DataAnnotations.Schema;
using SqlSugar;
using YZH.Core.Stand.Annotations;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// ★★ <b>「机构 → 标准 → 阶段」关联链视图（V）</b> —— 数据来源 MySQL 视图
    /// <c>v_org_standard_stage</c>（DDL：<c>scripts/db/views/v_org_standard_stage.sql</c>）。
    ///
    /// <para><b>★ 为什么用视图</b>（用户 2026-10-13 原话）：
    /// 「<i>系统数据量可能比较巨大，关系非常复杂，为了照顾效率和复杂度，我觉得可以适当引入视图
    /// ……需要使用视图的时候，不要吝啬通过视图进行设计</i>」。
    /// 本视图把 6 表 JOIN（机构 × 机构-标准 × 标准 × 机构-阶段 × 阶段 × 目录配置）
    /// 收成<b>一次取数</b>，替代 <c>StandardDirectoryService.GetOrganizationTreeAsync</c> 的
    /// 「6 次查询 + C# 内存组树」。<b>实测算法 = <c>MERGE</c></b>（无 <c>GROUP BY</c>/<c>UNION</c>/聚合
    /// ⇒ 谓词可下推到基表，<c>EXPLAIN</c> 全索引命中）。</para>
    ///
    /// <para><b>⚠️ 本类是纯只读视图实体 —— ⛔ 没有 T 表对应物</b>
    /// （不是 T+V 对，是<b>纯 V</b>）：它是「机构/标准/阶段」三张 T 表关联后的<b>投影</b>，
    /// 没有自己的物理表。写操作请走各自的 T 实体（<c>CertificationBody</c> / <c>ISOStandard</c> /
    /// <c>CertStage</c> / <c>CertOrgStandard</c> / <c>CertOrgStage</c> / <c>StandardDirectoryConfig</c>）。</para>
    ///
    /// <para><b>★ 有效性列（六列）</b>：视图<b>固化</b> <c>IsDeleted = 0</c>，
    /// <b>⛔ 不固化</b> <c>IsValid</c>（2026-10-13 用户裁定「视图不固化业务有效性」）
    /// ⇒ 业务启用状态一律由列暴露，由 <c>CoreResourceQuery</c> 统一判：
    /// <list type="bullet">
    ///   <item><c>OrgIsValid</c> —— <c>cert_certification_body.IsValid</c></item>
    ///   <item><c>StandardIsValid</c> —— <c>cert_iso_standard.IsValid</c></item>
    ///   <item><c>OrgStandardIsValid</c> —— <c>cert_org_standard.IsValid</c>（关联行有效性）</item>
    ///   <item><c>StageIsValid</c> —— <c>cert_cert_stage.IsValid</c></item>
    ///   <item><c>OrgStageIsValid</c> —— <c>cert_org_stage.IsValid</c>（关联行有效性）</item>
    ///   <item><c>ConfigIsValid</c> —— <c>cert_standard_directory_config.IsValid</c></item>
    /// </list>
    /// ⚠️ 六列全可空（<c>int?</c>）—— <c>LEFT JOIN</c> 未命中的一侧为 <c>NULL</c>
    /// ⇒ 消费方判 <c>== 1</c>（⛔ 不判 <c>!= 0</c>，否则 <c>NULL</c> 会被误当有效）。</para>
    ///
    /// <para><b>★ 行语义</b>：一行 = 一个「(机构, 标准, 阶段)」三元组。
    /// <b>无关联标准的机构</b>仍有行（<c>StandardCode</c> 为 <c>NULL</c>，<c>LEFT JOIN</c> 保证）
    /// ⇒ 消费方据 <c>StandardCode == null</c> 决定是否建标准节点。</para>
    ///
    /// <para><b>★ SqlSugar 接线（缺一即静默失效）</b>：
    /// <c>[ViewName]</c> ⇒ <c>SqlSugarDbOrm.QueryableRead</c> 走 <c>.AS(视图名)</c>；
    /// <c>[SugarTable]</c> ⇒ ⛔ 必须显式写（<c>GetTableName(inherit:false)</c> 不继承）；
    /// <c>[NotMapped]</c> ⇒ 阻止 EF Core 映射。<b>先例</b>：<c>CertStageView</c>。</para>
    /// </summary>
    [ViewName("v_org_standard_stage")]
    [SugarTable("v_org_standard_stage")]
    [NotMapped]
    public class OrgStandardStageView
    {
        // ──────────────────────────── 机构维度 ────────────────────────────

        /// <summary>机构 Code（= <c>cert_certification_body.Code</c>）。</summary>
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>机构名称。</summary>
        public string? OrgName { get; set; }

        /// <summary>机构排序号。</summary>
        public int? OrgSort { get; set; }

        /// <summary>机构有效性（1=启用）。</summary>
        public int? OrgIsValid { get; set; }

        // ──────────────────────────── 标准维度 ────────────────────────────

        /// <summary>标准 Code（GUID，= <c>cert_iso_standard.Code</c>）；<c>NULL</c> = 该机构未关联任何标准。</summary>
        public string? StandardCode { get; set; }

        /// <summary>标准编号（人读，如 <c>iso13485</c>）。</summary>
        public string? StandardNo { get; set; }

        /// <summary>标准名称。</summary>
        public string? StandardName { get; set; }

        /// <summary>版本年份。</summary>
        public int? VersionYear { get; set; }

        /// <summary>标准排序号。</summary>
        public int? StandardSort { get; set; }

        /// <summary>标准有效性（1=启用）。</summary>
        public int? StandardIsValid { get; set; }

        /// <summary>机构×标准 关联行有效性（<c>cert_org_standard.IsValid</c>）。</summary>
        public int? OrgStandardIsValid { get; set; }

        // ──────────────────────────── 阶段维度 ────────────────────────────

        /// <summary>阶段 Code（GUID，= <c>cert_cert_stage.Code</c>）；<c>NULL</c> = 该标准未关联任何阶段。</summary>
        public string? StageCode { get; set; }

        /// <summary>阶段编号（人读，如 <c>S2</c>）。</summary>
        public string? StageNo { get; set; }

        /// <summary>阶段名称。</summary>
        public string? StageName { get; set; }

        /// <summary>阶段排序号。</summary>
        public int? StageSort { get; set; }

        /// <summary>阶段有效性（1=启用）。</summary>
        public int? StageIsValid { get; set; }

        /// <summary>机构×阶段 关联行有效性（<c>cert_org_stage.IsValid</c>）。</summary>
        public int? OrgStageIsValid { get; set; }

        // ──────────────────────────── 目录配置 ────────────────────────────

        /// <summary>
        /// 目录配置 Code（两级兜底：机构行优先 → 平台通用行）；
        /// <c>NULL</c> = 该「标准 × 阶段」尚未建配置 ⇒ 前端不加载、不上传。
        /// </summary>
        public string? ConfigCode { get; set; }

        /// <summary>目录配置有效性（1=启用）。</summary>
        public int? ConfigIsValid { get; set; }
    }
}
