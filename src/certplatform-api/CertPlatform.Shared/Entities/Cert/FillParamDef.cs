using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// 企业资料参数字典（后台管理维护）
    /// <para>表名：cert_fill_param_def</para>
    /// <para>业务定位（2026-10-06 重定义，见 26-核心菜单功能设计 §3.1 裁决）：
    /// 「企业资料参数」是一个<b>按标准管理的简单字典</b> —— 定义专家端可完善的企业资料字段。
    /// 左树 = [通用] + 各ISO标准；每条参数唯一归属一节点（StandardCode='' 为通用）；
    /// 关键字段<b>不分机构</b>（OrgCode 恒空串）、不分阶段（StageCode 恒空串）。</para>
    /// <para>ORM：SqlSugar（铁律：DB列名 == C#属性名，PascalCase 逐字一致）</para>
    /// </summary>
    [SugarTable("cert_fill_param_def")]
    public class FillParamDef : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / CreateTime / CreateBy / UpdateTime / UpdateBy 由 BaseEntity 提供 ────

        /// <summary>
        ///     机构编码 —— <b>已废止为恒空串</b>（2026-10-06 起字典全局共享，不分机构）。
        ///     DB 列保留；OnBeforeAdd/OnBeforeUpdate 强制置 ''，⛔ 服务端不信任客户端传值。
        /// </summary>
        [StringLength(50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>标准 Code（cert_iso_standard.Code，GUID）。空串 = 「通用参数」；非空 = 该标准专属/覆写</summary>
        [StringLength(36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>阶段 Code —— <b>已废止为恒空串</b>（阶段维度随作用域模型一并砍除）。DB 列保留，服务端强制置 ''</summary>
        [StringLength(36)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>参数编码（同一 标准 下唯一，如 doc_prefix / main_products / quality_policy）</summary>
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
        ///     取值来源类别 —— <b>恒 'manual'</b>（2026-10-07 起，用户裁决：
        ///     「企业资料参数」页是按后台定义逐项手填的简单填写页，不由 AI 分析产出）。
        ///     迁移：<c>20261006_fill_param_standard_dict_V1.sql</c>（global→manual）、
        ///     <c>20261007_fill_param_drop_ai_V1.sql</c>（ai→manual）。
        ///     ⛔ 原「ai=AI 生成候选」语义与专家端「生成提示词」按钮一并删除。
        /// </summary>
        [StringLength(20)]
        public string SourceKind { get; set; } = "manual";

        /// <summary>
        ///     自动取值表达式 —— <b>已废弃</b>（2026-10-06 砍自动带出能力，迁移后恒 NULL）。
        ///     DB 列保留；ParamValueResolver 仍读取，但 MaintainMode 已全量 manual，auto/both 分支不可达。
        /// </summary>
        [StringLength(200)]
        public string? SourceExpr { get; set; }

        /// <summary>
        ///     维护方式 —— <b>已废弃为恒 'manual'</b>（2026-10-06 迁移归一）。
        ///     DB 列保留；OnBeforeAdd/OnBeforeUpdate 强制置 'manual'。
        /// </summary>
        [StringLength(20)]
        public string MaintainMode { get; set; } = "manual";

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
