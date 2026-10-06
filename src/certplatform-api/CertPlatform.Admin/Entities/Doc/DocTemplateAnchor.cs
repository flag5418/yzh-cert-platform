using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;
using YZH.Entity.Admin.Platform;

namespace CertPlatform.Admin.Entities.Doc
{
    /// <summary>模板锚点规则（文档填写规则的最小单元）</summary>
    /// <para>表名：cert_doc_template_anchor</para>
    /// <para>
    /// ★ 业务定位：一行 = <b>一条「模板里的哪个位置 ← 填什么值」的规则</b>。
    /// 这是「标准文档填写规则」页面的核心数据，也是 <c>DocumentFillEngine</c> 的输入。
    /// </para>
    /// <para>
    /// ★ 三类锚点（<see cref="AnchorKind"/>）：
    /// ① <c>token</c> —— Word 正文里的 <c>{{FIELD_CODE}}</c> 文本锚点；
    /// ② <c>bookmark</c> —— Word 书签；
    /// ③ <c>range</c> —— Excel 区域（<see cref="SheetName"/> + <see cref="AnchorRef"/> 如 <c>Sheet1!A11:F11</c>）。
    /// </para>
    /// <para>
    /// ★ 定位列三元组 <see cref="SheetName"/> / <see cref="SectionIndex"/> / <see cref="HeaderKind"/>
    /// 是 <b>NOT NULL</b> 且默认空串/0 —— 它们参与唯一键 <c>uk_tpl_anchor</c>。
    /// ⛔ MySQL 唯一索引里 NULL 不互相冲突，所以「不适用」必须写空串/0，⛔ 不能写 NULL
    /// （否则同模板可插入任意多条「不适用」的重复锚点）。
    /// </para>
    /// <para>
    /// ★ 首版策略：33+ 列<b>全进实体</b>（避免后续补列），但页面只暴露录入必需字段
    /// （<c>TemplateCode</c> / <c>AnchorType</c> / <c>AnchorKind</c> / <c>AnchorRef</c> /
    /// <c>FieldCode</c> / <c>SourceSpec</c> / <c>ValueType</c> / <c>Required</c> / <c>Sort</c>）。
    /// </para>
    /// <para>ORM：SqlSugar（铁律：DB 列名 == C# 属性名，PascalCase 逐字一致）</para>
    /// <para>设计依据：37 号 §四（表结构）· 22 号（block 锚点补入）</para>
    [SugarTable("cert_doc_template_anchor")]
    [YZHDeleteStrategy(Mode = DeleteMode.Soft)]
    public class DocTemplateAnchor : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / CreateTime / CreateBy / UpdateTime / UpdateBy 由 BaseEntity 提供 ────

        // ──── 归属 ────

        /// <summary>★ 所属模板 → <c>cert_doc_template.Code</c></summary>
        [StringLength(36)]
        [UniqueField("模板锚点", WithFields = new[]
        {
            "AnchorType", "AnchorKind", "SheetName", "SectionIndex", "HeaderKind", "AnchorRef"
        })]
        public string TemplateCode { get; set; } = string.Empty;

        // ──── 定位（唯一键成员）────

        /// <summary>scalar / block / table / table_total / domain（★ block 为 22 号补入）</summary>
        [Required]
        [StringLength(20)]
        public string AnchorType { get; set; } = string.Empty;

        /// <summary>token = Token 文本 / bookmark = 书签 / range = Excel 区域</summary>
        [StringLength(20)]
        public string AnchorKind { get; set; } = "token";

        /// <summary><c>{{ENT_NAME}}</c> / <c>ROW_培训记录</c> / <c>Sheet1!A11:F11</c></summary>
        [Required]
        [StringLength(200)]
        public string AnchorRef { get; set; } = string.Empty;

        /// <summary>★ Excel 工作表名（空串 = 不适用；⛔ 不可为 NULL，见类注释）</summary>
        [StringLength(100)]
        public string SheetName { get; set; } = string.Empty;

        /// <summary>★ Word 分节序号（0 = 不适用；⛔ 不可为 NULL）</summary>
        public int SectionIndex { get; set; }

        /// <summary>★ Word 页眉页脚：default / first / even（空串 = 不适用；⛔ 不可为 NULL）</summary>
        [StringLength(20)]
        public string HeaderKind { get; set; } = string.Empty;

        // ──── 语义绑定 ────

        /// <summary>域子类：text = 写值 / auto = 交给 Word 算（PAGE / NUMPAGES）</summary>
        [StringLength(20)]
        public string? DomainKind { get; set; }

        /// <summary>★ 绑定的语义字段（须与 <c>cert_doc_field_def.FieldCode</c> 对齐）</summary>
        [StringLength(100)]
        public string? FieldCode { get; set; }

        // ──── 表格 / 修饰 / 来源 ────

        /// <summary>表格列顺序 <c>["TRAIN_DATE","TRAIN_TOPIC","HOURS"]</c></summary>
        public string? ColumnsJson { get; set; }

        /// <summary>修饰符 <c>{"fmt":"0.00","def":"—","src":"global"}</c></summary>
        public string? TokenModifiersJson { get; set; }

        /// <summary>取值来源规格：<c>{combine,separator,expr,sources[]}</c>；sources 有序</summary>
        public string? SourceSpec { get; set; }

        /// <summary>来源摘要（列表展示用，由 <see cref="SourceSpec"/> 生成）</summary>
        [StringLength(500)]
        public string? SourceSummary { get; set; }

        // ──── 写入行为 ────

        /// <summary>replace / overwrite / append / remove</summary>
        [StringLength(20)]
        public string WriteMode { get; set; } = "overwrite";

        /// <summary>原值快照（仅 replace / remove 需要，支撑差异与回滚）</summary>
        public string? OriginalText { get; set; }

        /// <summary>写入条件（仅 remove 必填）</summary>
        public string? ConditionJson { get; set; }

        // ──── 值形态 ────

        /// <summary>值类型：text / number / date / bool / enum</summary>
        [StringLength(20)]
        public string ValueType { get; set; } = "text";

        /// <summary>空值兜底文案</summary>
        [StringLength(200)]
        public string? DefaultText { get; set; }

        /// <summary>★ 格式串（.NET 方言，见 39 号 §十六）</summary>
        [StringLength(64)]
        public string? NumberFormat { get; set; }

        // ──── 样式（只读，不改模板）────

        /// <summary><c>{"rowSpan":1,"colSpan":3,"anchor":"A3"}</c></summary>
        public string? MergeJson { get; set; }

        /// <summary>写入需保留的样式快照（只读，不改）</summary>
        public string? StyleJson { get; set; }

        /// <summary>人工标记样式名（YZH_Mark），自验收依据</summary>
        [StringLength(50)]
        public string? MarkStyleName { get; set; }

        // ──── 状态 ────

        /// <summary>是否必填</summary>
        public bool Required { get; set; }

        /// <summary>
        ///     ★ <b>锚点锁定</b> = 实施人员<b>已认可</b>该锚点的设置规则，<b>配置就此冻结</b>。
        ///     <para><b>① 不可再修改配置</b>（用户 2026-10-05 裁定「锁定的问题，就是不能再修改配置」）：
        ///     <c>save-batch</c> / <c>UpdateCore</c> 命中锁定行且<b>配置列有实质变化</b>时一律拒绝；
        ///     <c>Clear</c>（清空模板锚点）也<b>跳过</b>锁定行。解锁走 <c>lock</c> 端点（<c>locked=false</c>）。</para>
        ///     <para><b>② 换版重扫保留配置</b>：新模板里仍有同唯一键锚点 ⇒ 软删 + 复活 ⇒ 配置自动保留
        ///     （无需额外搬运代码）；已消失的由 <c>Scan</c> 如实回报 <c>Locked.LostRefs</c>。</para>
        ///     <para>⛔ 不另设 <c>LockedBy</c> / <c>LockedTime</c>：<c>BaseEntity</c> 的
        ///     <c>UpdateBy</c> / <c>UpdateTime</c> 已记录「谁在何时认可的」。</para>
        /// </summary>
        public bool IsLocked { get; set; }

        /// <summary>★ 重传后消失的锚点（不删，标记保留，因其可能已有填过的值）</summary>
        public bool IsOrphan { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        [StringLength(500)]
        public string? Remark { get; set; }

        // ──── 接口字段（BaseEntity 不含，必须声明在实体自身，否则全库过滤静默失效）────

        /// <summary>有效标志（1=有效，0=无效）。⛔ 禁 Enable</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code（仅软删除时赋值）</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间（仅软删除时赋值）</summary>
        public DateTime? DeleteTime { get; set; }
    }
}
