using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Cert
{
    /// <summary>
    /// 企业全局参数值（企业端完善）
    /// <para>表名：cert_fill_param_value</para>
    /// <para>业务定位：企业端「企业全局参数定义」页面把「企业基本信息 + 后台定义的全局参数」
    /// 合并成一张待完善清单；本表存企业侧的最终值。</para>
    /// <para>★ 值来源语义（对应 22 册 §七 的去重裁定）：
    /// <c>ValueSource=auto</c> = 由 <c>SourceExpr</c> 自动映射带出（如企业全称直接取 <c>cert_enterprise.Name</c>）；
    /// <c>ValueSource=manual</c> = 企业人工填写。企业人工改过的行 <c>IsManualEdited=1</c>，后续自动映射不再覆盖。</para>
    /// <para>ORM：SqlSugar（铁律：DB列名 == C#属性名，PascalCase 逐字一致）</para>
    /// </summary>
    [SugarTable("cert_fill_param_value")]
    public class FillParamValue : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / CreateTime / CreateBy / UpdateTime / UpdateBy 由 BaseEntity 提供 ────

        /// <summary>机构编码（租户隔离键）。⛔ 不加 [Required]，由服务端填充</summary>
        [StringLength(50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>企业 Code（cert_enterprise.Code）。⛔ 不加 [Required]，由服务端填充</summary>
        [StringLength(36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>企业名称（冗余快照，列表页免 JOIN）</summary>
        [StringLength(200)]
        public string? EnterpriseName { get; set; }

        /// <summary>标准 Code（cert_iso_standard.Code，GUID）。空串表示「不限标准」</summary>
        [StringLength(36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>阶段 Code（cert_cert_stage.Code，GUID）。空串表示「不限阶段」</summary>
        [StringLength(36)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>参数编码（对应 cert_fill_param_def.ParamCode）</summary>
        [Required]
        [StringLength(100)]
        [UniqueField("参数编码", WithFields = new[] { "EnterpriseCode", "StandardCode", "StageCode" })]
        public string ParamCode { get; set; } = string.Empty;

        /// <summary>参数名称（冗余快照，定义被改后仍可追溯）</summary>
        [StringLength(200)]
        public string? ParamName { get; set; }

        /// <summary>分组名（冗余快照）</summary>
        [StringLength(50)]
        public string? GroupName { get; set; }

        /// <summary>值类型（冗余快照）：text | number | date | enum | bool</summary>
        [StringLength(20)]
        public string ValueType { get; set; } = "text";

        /// <summary>枚举选项 JSON（冗余快照，企业端下拉用）</summary>
        public string? EnumOptions { get; set; }

        /// <summary>参数值（企业填写的最终值）</summary>
        public string? ParamValue { get; set; }

        /// <summary>值来源：auto=自动映射带出 | manual=企业手工填写 | ai=AI生成 | import=批量导入</summary>
        [StringLength(20)]
        public string ValueSource { get; set; } = "auto";

        /// <summary>自动取值来源说明（如「企业基础信息 · 企业全称」），用于界面提示「此项来自企业管理」</summary>
        [StringLength(200)]
        public string? SourceRef { get; set; }

        /// <summary>企业是否人工改过（=1 时不再被自动映射覆盖）</summary>
        public bool IsManualEdited { get; set; }

        /// <summary>维护方式（冗余快照，决定企业端可编辑性）：auto | manual | both</summary>
        [StringLength(20)]
        public string MaintainMode { get; set; } = "auto";

        /// <summary>是否必填（冗余快照）</summary>
        public bool IsRequired { get; set; }

        /// <summary>是否已完善（列表角标 / 完成度统计用）</summary>
        public bool IsFilled { get; set; }

        /// <summary>排序号（冗余快照）</summary>
        public int SortOrder { get; set; }

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
