using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Entities.Wf
{
    /// <summary>
    /// 技能实体 - 用于工作流技能管理
    /// 继承 BaseEntity，映射 wf_skill 表
    ///
    /// 命名规范（YZH 铁律）：
    ///   DB 列名 = C# 属性名 = PascalCase
    ///   审计字段和接口字段从 BaseEntity + ISoftDelete/IIsValid 继承，禁止重复声明
    /// </summary>
    [SugarTable("wf_skill")]
    [YZHDeleteStrategy(Mode = DeleteMode.Soft)]
    public class Skill : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────
        // ★ 2026-10-14：原 SkillCode 属性已删 —— 2026-10-13 迁移（20261013_unify_fields_V1.sql §4）
        //   已把 wf_skill 双业务键（Code + SkillCode）统一为单一 Code（Code 对齐为原 SkillCode 值后删列）。
        //   实体残留 SkillCode 属性会导致 SqlSugar SELECT 含已删列 ⇒
        //   /filter 报 Unknown column 'SkillCode' in 'field list'（nc-config / report-rule-config 设计器分页失败）。

        // === 业务字段 ===

        /// <summary>技能名称（DB: Name）</summary>
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>技能类型（DB: SkillType，如 method/api/llm）</summary>
        [StringLength(50)]
        public string SkillType { get; set; } = "manual";

        /// <summary>
        /// 分类编码（DB: CategoryCode）
        /// 关联值 = 字典「技能分类」(DicNo='skill_category') 字典项的 DicValue（如 data_access）；
        /// 分类本身在「字典管理」页面维护（2026-09-26 分类字典化，原 wf_skill_category 已废弃）
        /// </summary>
        [StringLength(64)]
        public string? CategoryCode { get; set; }

        /// <summary>
        /// ★ 是否可用于 NC 规则配置（DB: IsUseForNcConfig，2026-10-11）
        /// <para>nc-config 设计器（workflowType='validation'）的节点库只列 <c>true</c> 的 skill。</para>
        /// <para>默认 <c>false</c> —— 新 skill <b>不会自动出现</b>在规则节点库里，必须显式开启。</para>
        /// </summary>
        public bool IsUseForNcConfig { get; set; } = false;

        /// <summary>
        /// ★ 是否可用于报告规则配置（DB: IsUseForReport，2026-10-11）
        /// <para>report-rule-config 设计器（workflowType='report'）的节点库只列 <c>true</c> 的 skill。</para>
        /// <para>默认 <c>false</c>。同一个 skill 可以两个字段都为 <c>true</c>。</para>
        /// </summary>
        public bool IsUseForReport { get; set; } = false;

        /// <summary>是否有副作用（DB: SideEffect）</summary>
        public bool SideEffect { get; set; } = false;

        /// <summary>描述（DB: Description）</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Description { get; set; }

        /// <summary>Prompt模板（DB: PromptTemplate）</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? PromptTemplate { get; set; }

        /// <summary>是否激活（DB: IsActive）</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>输出严格模式（DB: OutputStrict）</summary>
        public bool OutputStrict { get; set; } = true;

        /// <summary>返回类型（DB: ReturnType，如 json/text）</summary>
        [StringLength(20)]
        public string? ReturnType { get; set; } = "json";

        /// <summary>版本号（DB: Version）</summary>
        [StringLength(20)]
        public string? Version { get; set; } = "1.0";

        /// <summary>图标（DB: Icon）</summary>
        [StringLength(50)]
        public string? Icon { get; set; }

        /// <summary>颜色（DB: Color）</summary>
        [StringLength(20)]
        public string? Color { get; set; }

        /// <summary>排序号（DB: Sort）</summary>
        public int Sort { get; set; } = 0;

        /// <summary>备注（DB: Remark）</summary>
        [StringLength(500)]
        public string? Remark { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
