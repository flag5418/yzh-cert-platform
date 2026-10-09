using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Doc
{
    /// <summary>
    ///     企业资料规范化动作留痕（表 <c>cert_doc_normalize_action</c>）—— <b>只追加</b>
    ///
    ///     <para><b>端归属</b>：专家端（<c>CertPlatform.Auditor</c>）—— 写入方是锁定/重写/钉住/批量运行
    ///     各端点与编排器，读取方是审计抽屉「历史留痕时间线」。</para>
    ///
    ///     <para><b>DDL 权威</b>：<c>scripts/db/20261006_doc_normalize_V1.sql</c> 与
    ///     [<c>参考/41-01-数据模型与两个口径-V1.md</c> §一·2] —— <b>两处必须同步维护</b>。</para>
    ///
    ///     <para><b>★★ 为什么不能用宿主表行内 3 列</b>：26 号 A-2 要求「<b>解锁也留痕</b>」。
    ///     <c>LockedBy</c>/<c>LockedTime</c> 只能记<b>最后一次</b>，
    ///     而锁定/解锁/重写是<b>反复发生</b>的动作 ⇒ 必须只追加表。</para>
    ///
    ///     <para><b>⛔⛔ 三条硬约束（违反即破坏审计链）</b>：</para>
    ///     <list type="number">
    ///         <item><b>不建 <c>IsDeleted</c></b>（32 号 §八）：动作历史<b>永不可删</b>；
    ///         误操作只能<b>再写一条反向动作</b>（如误锁 ⇒ 写一条 <c>unlock</c>）。</item>
    ///         <item><b>全仓 <c>UPDATE</c> / <c>DELETE</c> 命中数 = 0</b>（41-01 A-7 判据）——
    ///         本表<b>只有 INSERT</b>。</item>
    ///         <item>⛔ 本表<b>没有 <c>IsValid</c> 列</b>（无「启用/禁用」概念，也不参与全局有效过滤）。</item>
    ///     </list>
    /// </summary>
    [SugarTable("cert_doc_normalize_action")]
    public class DocNormalizeAction : BaseEntity
    {
        // ──── Id / Code / CreateTime（= 动作发生时间）/ CreateBy（= 操作人）由 BaseEntity 提供 ────

        /// <summary>
        ///     ⛔ <b>屏蔽基类的 <c>UpdateTime</c></b> —— 本表 DB 里<b>没有这一列</b>（只追加，永不更新）。
        ///
        ///     <para><b>⚠️ 不屏蔽的后果（2026-10-09 实测踩中）</b>：<c>_db.InsertAsync</c> 会把
        ///     <c>UpdateTime</c> 拼进 INSERT 的字段列表 ⇒ 报
        ///     <c>Unknown column 'UpdateTime' in 'field list'</c> ⇒ <b>留痕永远写不进去</b>，
        ///     症状是「动作明明生效了，审计链却是空的」。</para>
        ///
        ///     <para>★ 同款先例：<c>Sys_RoleMenu</c> / <c>Sys_RoleUser</c> 用同样手法屏蔽基类列。</para>
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public new DateTime? UpdateTime { get; set; }

        /// <summary>⛔ 屏蔽基类的 <c>UpdateBy</c> —— 同上，本表无此列</summary>
        [SugarColumn(IsIgnore = true)]
        public new string? UpdateBy { get; set; }

        /// <summary>认证机构 Code</summary>
        [SugarColumn(Length = 36)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>企业 Code</summary>
        [SugarColumn(Length = 36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>标准 Code</summary>
        [SugarColumn(Length = 36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>阶段 Code</summary>
        [SugarColumn(Length = 36)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>
        ///     ★ 动作：<c>lock</c> / <c>unlock</c> / <c>rewrite</c> / <c>pin</c> / <c>unpin</c> /
        ///     <c>batch_run</c> / <c>batch_cancel</c>（对齐 26 号 §9.4；字典 <c>normalize_action</c>）。
        /// </summary>
        [SugarColumn(Length = 20)]
        public string ActionType { get; set; } = string.Empty;

        /// <summary>作用范围 <c>file</c>/<c>folder</c>/<c>stage</c>/<c>standard</c>/<c>enterprise</c>（对应五级粒度）</summary>
        [SugarColumn(Length = 20)]
        public string ScopeType { get; set; } = string.Empty;

        /// <summary>范围标识（文件 / 文件夹 Code）</summary>
        [SugarColumn(Length = 36)]
        public string ScopeCode { get; set; } = string.Empty;

        /// <summary>范围名称快照（如「4 记录文件」）—— ⚠️ 快照，不是外键（范围可能被重命名/删除）</summary>
        [SugarColumn(Length = 200)]
        public string ScopeName { get; set; } = string.Empty;

        /// <summary>直接目标（文件 Code）；<b>批量时留空</b></summary>
        [SugarColumn(Length = 36)]
        public string TargetCode { get; set; } = string.Empty;

        /// <summary>锚点（改单元格来源 / 钉住时填；否则空）</summary>
        [SugarColumn(Length = 36)]
        public string AnchorCode { get; set; } = string.Empty;

        /// <summary>变更前快照 <c>{IsLocked, SourceKind, ValueText, ...}</c></summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? BeforeJson { get; set; }

        /// <summary>变更后快照</summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? AfterJson { get; set; }

        /// <summary>★ 理由（<c>unlock</c> / <c>rewrite</c> <b>⛔ 必填</b>，审计要求）</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Reason { get; set; }

        /// <summary>关联批次 → <c>yzh_queue.QueueCode</c>（<c>batch_run</c> / <c>batch_cancel</c> 时填）</summary>
        [SugarColumn(Length = 36)]
        public string QueueCode { get; set; } = string.Empty;
    }
}
