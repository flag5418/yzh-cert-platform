using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// 体系认证全局参数定义（后台管理维护）
    /// <para>表名：cert_fill_param_def</para>
    /// <para>业务定位：认证机构在后台按「机构 × 标准 × 阶段」预定义体系认证全局参数，
    /// 这些参数是标准文档填充时的主要取值来源（如企业全称、统一社会信用代码、文件编号前缀）。</para>
    /// <para>设计依据：05 册 <c>22-填写单元属性分类与取值来源模型-V1.md</c> §七、
    /// <c>23-全局填写规则与参数维护设计-V1.md</c> §四；实测必要性见 <c>25-纸面实验实测报告-V1.md</c> §8.5。</para>
    /// <para>ORM：SqlSugar（铁律：DB列名 == C#属性名，PascalCase 逐字一致）</para>
    /// </summary>
    [SugarTable("cert_fill_param_def")]
    public class FillParamDef : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / CreateTime / CreateBy / UpdateTime / UpdateBy 由 BaseEntity 提供 ────

        /// <summary>
        ///     机构编码（cert_certification_body.Code，租户隔离键）
        ///     ⛔ 不加 [Required]：服务端按当前机构填充，客户端不传；
        ///        加了会被 [ApiController] 自动模型校验拦成 400，OnBeforeAdd 没机会执行。
        /// </summary>
        [StringLength(50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>标准 Code（cert_iso_standard.Code，GUID）。空串表示「不限标准」</summary>
        [StringLength(36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>阶段 Code（cert_cert_stage.Code，GUID，⛔不是业务短码 jd01/03）。空串表示「不限阶段」</summary>
        [StringLength(36)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>参数编码（同一 机构+标准+阶段 下唯一，如 company_name / doc_prefix / legal_person）</summary>
        [Required]
        [StringLength(100)]
        [UniqueField("参数编码", WithFields = new[] { "OrgCode", "StandardCode", "StageCode" })]
        public string ParamCode { get; set; } = string.Empty;

        /// <summary>参数名称（展示用，如「企业全称」）</summary>
        [Required]
        [StringLength(200)]
        public string ParamName { get; set; } = string.Empty;

        /// <summary>分组名（界面按此分组展示，如「基础信息」/「体系信息」）</summary>
        [StringLength(50)]
        public string? GroupName { get; set; } = "基础信息";

        /// <summary>值类型：text | number | date | enum | bool</summary>
        [StringLength(20)]
        public string ValueType { get; set; } = "text";

        /// <summary>枚举选项 JSON（ValueType=enum 时必填）：[{"Value":"A","Label":"甲"}]</summary>
        public string? EnumOptions { get; set; }

        /// <summary>
        ///     取值来源类别：
        ///     global=全局参数 | replace=替换 | headerFooter=页眉页脚 |
        ///     ai=AI生成 | manual=人工填写 | compute=计算派生
        /// </summary>
        [StringLength(20)]
        public string SourceKind { get; set; } = "global";

        /// <summary>
        ///     自动取值表达式（SourceKind=global 时使用）：
        ///     enterprise.Name / enterprise.Address / enterprise.CreditCode / enterprise.LegalPerson /
        ///     enterprise.Province / enterprise.City / enterprise.IndustryType / enterprise.CertScope /
        ///     enterprise.ContactName / system.Date / system.OrgName
        /// </summary>
        [StringLength(200)]
        public string? SourceExpr { get; set; }

        /// <summary>维护方式：auto=自动映射（企业端只读+跳转） | manual=企业手工填 | both=自动带出但允许覆盖</summary>
        [StringLength(20)]
        public string MaintainMode { get; set; } = "auto";

        /// <summary>默认值（企业端首次带出时使用）</summary>
        [StringLength(500)]
        public string? DefaultValue { get; set; }

        /// <summary>输入提示</summary>
        [StringLength(200)]
        public string? Placeholder { get; set; }

        /// <summary>是否必填（未填则文档填充时产出待办，不阻断）</summary>
        public bool IsRequired { get; set; }

        /// <summary>是否内置参数（内置不可删除，如企业全称 / 统一社会信用代码）</summary>
        public bool IsBuiltin { get; set; }

        /// <summary>排序号</summary>
        public int SortOrder { get; set; }

        /// <summary>参数说明（给企业看的填写指引）</summary>
        [StringLength(500)]
        public string? Description { get; set; }

        // ──── 接口字段（BaseEntity 不含，必须声明在实体自身，否则全库过滤静默失效）────

        /// <summary>有效标志（1=有效，0=无效）</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code（仅软删除时赋值）</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间（仅软删除时赋值）</summary>
        public DateTime? DeleteTime { get; set; }
    }
}
