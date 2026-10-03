using System;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// NC 检查项 —— <b>企业级长期实体</b>（专家任务系统 B 组 · 结果域）
    /// <para>表名：<c>cert_expert_nc_item</c></para>
    ///
    /// <para><b>★★ 21 号核心判断：「执行实例」与「业务实体」必须分离。</b>
    /// 依据是 11 号 §4 的实测 —— <b>同一检查项在不同轮次被执行过 15 次</b>。
    /// 若把检查项和结果揉成一张表，每跑一轮就要复制一份检查项，
    /// 「上次检查时间」「当前生效结论」这类<b>跨轮次</b>信息将无处安放。</para>
    ///
    /// <para><b>分工</b>：</para>
    /// <list type="bullet">
    ///   <item>本表（实体层）= <b>长期</b>：一个企业 + 一个阶段 + 一个标准 + 一条规则 = 一行，<b>永不因重跑而新增</b>。
    ///         唯一键 <c>uk_scope_rule</c> 保证。</item>
    ///   <item><c>cert_expert_nc_result</c>（结果层）= <b>多轮</b>：每次执行追加一行，<see cref="RoundCount"/> 记录累计轮次。</item>
    /// </list>
    ///
    /// <para><b>★ <see cref="LastAuditedTime"/>（D30 沿用项语义）</b>：本轮<b>实际检查了</b>才更新。
    /// 被「沿用」的项保留上次检查时间 —— 否则界面会误报「刚检查过」，掩盖数据陈旧的事实。</para>
    ///
    /// <para><b>★ <see cref="CurrentResultCode"/>（当前生效结果指针）</b>：结果页只显示这一轮，
    /// 历史轮次仍可下钻。⛔ 不要在列表查询里 JOIN 取 MAX(RoundNo) —— 用指针，快且唯一。</para>
    /// </summary>
    [SugarTable("cert_expert_nc_item")]
    public class CertExpertNcItem : BaseEntity, ISoftDelete, IIsValid
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

        // ──── 作用域（唯一键四元组） ────

        /// <summary>企业编码</summary>
        [SugarColumn(Length = 36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>阶段编码（★<c>cert_cert_stage.Code</c>，GUID）</summary>
        [SugarColumn(Length = 36)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>标准编码</summary>
        [SugarColumn(Length = 36)]
        public string StandardCode { get; set; } = string.Empty;

        // ──── 规则快照 ────

        /// <summary>★<c>cert_validation_rule.Code</c>（业务键）</summary>
        [SugarColumn(Length = 36)]
        public string RuleCode { get; set; } = string.Empty;

        /// <summary>★规则编号（仅展示）</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? RuleNumber { get; set; }

        /// <summary>规则名称（快照）</summary>
        [SugarColumn(Length = 200)]
        public string RuleName { get; set; } = string.Empty;

        /// <summary>规则名称英文（快照）</summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string? RuleNameEn { get; set; }

        /// <summary>条款编码（<c>cert_iso_clause.Code</c>）</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? ClauseCode { get; set; }

        /// <summary>条款号（快照，如 8.4）</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? ClauseNumber { get; set; }

        /// <summary>条款标题（快照）</summary>
        [SugarColumn(Length = 200, IsNullable = true)]
        public string? ClauseTitle { get; set; }

        /// <summary>
        /// 审核方式（<c>cert_validation_rule.JudgeMode</c> 快照）：<c>auto</c> | <c>semi</c> | <c>manual</c>。
        /// <para>★ 人工/自动按「审核方法」划分：查阅文件 = 可自动；现场观察 / 访谈 = 必须人工。</para>
        /// </summary>
        [SugarColumn(Length = 20, IsNullable = true)]
        public string? JudgeMode { get; set; }

        /// <summary>严重度预设（SeverityIfViolated 快照）</summary>
        [SugarColumn(Length = 20, IsNullable = true)]
        public string? SeverityDefault { get; set; }

        /// <summary>工作流编码</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? WorkflowCode { get; set; }

        /// <summary>★规则版本（结论可回溯到哪一版规则）</summary>
        public int RuleVersion { get; set; } = 1;

        /// <summary>★DAG 指纹</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string? RuleContextHash { get; set; }

        // ──── 跨轮次状态 ────

        /// <summary>★当前生效的结果行（<c>cert_expert_nc_result.Code</c>）</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? CurrentResultCode { get; set; }

        /// <summary>★累计结果轮次数</summary>
        public int RoundCount { get; set; }

        /// <summary>★上次检查时间（★沿用项不更新，D30）</summary>
        public DateTime? LastAuditedTime { get; set; }

        /// <summary>上次检查的任务</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? LastAuditedTaskCode { get; set; }

        /// <summary>上次专家认可/修改时间</summary>
        public DateTime? LastReviewTime { get; set; }

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
