using System;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Expert
{
    /// <summary>
    /// 专家任务数据缺口 / 补录清单（专家任务系统 A 组）
    /// <para>表名：<c>cert_expert_task_data_gap</c></para>
    ///
    /// <para><b>存在意义</b>：自动判定依赖企业资料。若某条规则需要的字段/表格<b>根本不存在</b>，
    /// 结论就不是「符合」也不是「不符合」，而是<b>无法判定</b>。此时系统不猜，
    /// 而是生成一条<b>缺口</b>告诉专家「缺哪个文件、哪个字段」，由专家补录或跳过。</para>
    ///
    /// <para><b>★ <see cref="GapKey"/> 必须是 MySQL 生成列</b>（两个硬原因）：</para>
    /// <list type="number">
    ///   <item>MySQL 索引列<b>不允许函数表达式</b> ⇒ 无法用 <c>CONCAT(...)</c> 直接建唯一索引；</item>
    ///   <item>裸 NULL 不参与唯一性判定（<c>NULL != NULL</c>）⇒ 若拼接结果含 NULL 会多行同时通过，
    ///         去重失效。故 <c>IFNULL(...,'-')</c> 占位。</item>
    /// </list>
    /// <para>唯一键 <c>uk_gap(TaskCode, GapKey)</c> ⇒ 同一任务内同一缺口只出现一次（幂等重跑）。</para>
    /// </summary>
    [SugarTable("cert_expert_task_data_gap")]
    public class CertExpertTaskDataGap : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 提供 ────

        /// <summary>★ 专家工作区编码（租户隔离键；★ 禁止为 NULL）</summary>
        [SugarColumn(Length = 50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>创建人姓名（冗余）</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string? CreateName { get; set; }

        /// <summary>业务状态（保留）</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? Status { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        /// <summary>备注</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        // ──── 归属（四元组） ────

        /// <summary>所属任务编码</summary>
        [SugarColumn(Length = 36)]
        public string TaskCode { get; set; } = string.Empty;

        /// <summary>所属标准子任务编码</summary>
        [SugarColumn(Length = 36)]
        public string SubTaskCode { get; set; } = string.Empty;

        /// <summary>★提取规则编码（<c>cert_doc_extraction_rule.Code</c>）
        /// <para>★ 2026-09-30 落库（DDL：<c>fix-extraction-value-source-20260930.sql</c> ④）。</para>
        /// <para><b>为什么必须有</b>：裁决 J4「1 文件 = 1 规则」（DB 由
        /// <c>uk_rule_scope(OrgCode,StandardCode,StageCode,StandardFileCode)</c> 唯一索引强制）
        /// ⇒ <b>RuleCode 是取数与补录回写的主键成分</b>。缺了它，补录不知道往
        /// <c>cert_extraction_result</c> 的哪一行写。</para>
        /// <para>同时进 <see cref="GapKey"/> 生成列 ⇒ 同一 <c>TaskCode</c> 下
        /// 同一规则引用的同一字段只会产生 1 条缺口（裁决 J1「不分文档」）。</para>
        /// </summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? RuleCode { get; set; }

        /// <summary>企业编码（冗余）</summary>
        [SugarColumn(Length = 36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>阶段编码（GUID）</summary>
        [SugarColumn(Length = 36)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>标准编码</summary>
        [SugarColumn(Length = 36)]
        public string StandardCode { get; set; } = string.Empty;

        // ──── 缺口描述 ────

        /// <summary>缺口类型：<c>field</c> | <c>table</c></summary>
        [SugarColumn(Length = 20)]
        public string GapType { get; set; } = string.Empty;

        /// <summary>★展示名（界面直接显示）</summary>
        [SugarColumn(Length = 200)]
        public string GapLabel { get; set; } = string.Empty;

        /// <summary>字段编码（GapType=field）</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? FieldCode { get; set; }

        /// <summary>表格定义编码（GapType=table）</summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string? TableCode { get; set; }

        /// <summary>该数据应来自哪个标准文件
        /// <para>★ 2026-09-30 降级为<b>只读提示列</b>（裁决 J1「不分文档」）：
        /// ⛔ 不作分组键、⛔ 不作存储键。补录按 <see cref="RuleCode"/> 定位，与文件无关。</para>
        /// </summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string? StandardFileCode { get; set; }

        /// <summary>★应上传的文件名（提示专家去补文件）</summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string? ExpectedFileName { get; set; }

        // ──── 依赖来源 ────

        /// <summary>依赖来源类型：<c>nc_check</c> | <c>report_section</c></summary>
        [SugarColumn(Length = 20)]
        public string SourceItemType { get; set; } = string.Empty;

        /// <summary>★依赖它的规则/章节业务键</summary>
        [SugarColumn(Length = 36)]
        public string SourceItemCode { get; set; } = string.Empty;

        /// <summary>依赖它的规则/章节名（冗余）</summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string? SourceItemName { get; set; }

        /// <summary>关联条款编码</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? ClauseCode { get; set; }

        // ──── 处理结果 ────

        /// <summary>缺口状态：<c>pending</c>=待处理 | <c>filled</c>=已补录 | <c>skipped</c>=已跳过</summary>
        [SugarColumn(Length = 20)]
        public string GapStatus { get; set; } = "pending";

        /// <summary>补录的值（快照）</summary>
        [SugarColumn(ColumnDataType = "longtext", IsNullable = true)]
        public string? FilledValue { get; set; }

        /// <summary>补录人 Code</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? FilledBy { get; set; }

        /// <summary>补录人姓名（冗余）</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string? FilledName { get; set; }

        /// <summary>补录时间</summary>
        public DateTime? FilledTime { get; set; }

        /// <summary>跳过人 Code</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? SkipBy { get; set; }

        /// <summary>跳过人姓名（冗余）</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string? SkipName { get; set; }

        /// <summary>跳过时间</summary>
        public DateTime? SkipTime { get; set; }

        /// <summary>跳过原因</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? SkipReason { get; set; }

        // ──── ★ 生成列（去重键） ────

        /// <summary>
        /// ★去重键 = <c>GapType|IFNULL(RuleCode,'-')|IFNULL(FieldCode,'-')|IFNULL(TableCode,'-')</c>。
        /// <para>★ <b>2026-09-30 变更（裁决 J1）</b>：原式含 <c>SourceItemCode</c>（规则级粒度，
        /// 同一字段被 3 条规则引用 ⇒ 3 条缺口）。现式<b>去掉 <c>SourceItemCode</c>、加入
        /// <c>RuleCode</c>（字段级粒度，1 条 = 1 个待补项）。</para>
        /// <para><b>为什么去掉</b>：规则是业务配置、变动频繁；缺口是任务快照、创建后不变。
        /// 两者生命周期不一致 ⇒ 落库 <c>SourceItemCode</c> 必然漂移（规则改了缺口还指向旧规则）。
        /// 「影响 N 条规则」改由前端按工作流 DAG <b>实时反查</b>。</para>
        /// <para>DDL：<c>scripts/db/fix/fix-extraction-value-source-20260930.sql</c> ⑤</para>
        /// <para><b>MySQL 生成列（STORED）</b> —— ⛔ 应用层不得赋值，否则 <c>ERROR 3105</c>。</para>
        /// </summary>
        [SugarColumn(Length = 300, IsNullable = true,
            IsOnlyIgnoreInsert = true, IsOnlyIgnoreUpdate = true)]
        public string? GapKey { get; set; }

        // ──── ISoftDelete / IIsValid ────

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        public DateTime? DeleteTime { get; set; }

        /// <summary>有效标志（1=有效，0=无效）</summary>
        public int IsValid { get; set; } = 1;
    }
}
