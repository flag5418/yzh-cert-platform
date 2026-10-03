using System;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Entities.Cert
{
    /// <summary>
    /// ★ 认证结论判定规则（表：cert_conclusion_rule）
    /// <para>18 号文档 §五。★ 行业标准要求的判定规则全部落此表，标准确认后只改配置不改代码。</para>
    /// <para>⚠️ <b>表中的阈值默认值是占位值，不是行业标准</b>，必须由认证机构 / 资深审核员确认
    /// （18 号 §十一 Q1-Q7）。未确认前仅可用于跑通链路，<b>不可用于真实认证结论</b>。</para>
    /// </summary>
    [SugarTable("cert_conclusion_rule")]
    public class ConclusionRule : BaseEntity, ISoftDelete, IIsValid
    {
        // ─── 归属 ───
        /// <summary>认证机构编码（租户隔离键）</summary>
        [SugarColumn(Length = 50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>标准编码（★空串 = 全部标准通用规则）</summary>
        [SugarColumn(Length = 36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>阶段编码（★NULL = 全阶段共用）</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? PhaseCode { get; set; }

        // ─── 结论档位（枚举词可改，改这里不动代码）───
        [SugarColumn(Length = 30)]
        public string LevelFull { get; set; } = "完全符合";

        [SugarColumn(Length = 30)]
        public string LevelBasic { get; set; } = "基本符合";

        [SugarColumn(Length = 30)]
        public string LevelReject { get; set; } = "不符合";

        /// <summary>结论档位顺序 JSON 数组，如 ["完全符合","基本符合","不符合"]（供枚举校验与降级判断）</summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? ConclusionOrder { get; set; }

        // ─── 阈值（★占位值，待确认）───
        /// <summary>一般不符合 ≥ N → 基本符合</summary>
        public int MinorToBasicThreshold { get; set; } = 3;

        /// <summary>一般不符合 ≥ N → 不符合</summary>
        public int MinorRejectThreshold { get; set; } = 5;

        // ─── 跨过程失效升级（★单节点多维度范式独有能力）───
        /// <summary>是否启用跨过程失效升级</summary>
        public bool CrossProcessEnabled { get; set; } = true;

        /// <summary>同过程 ≥ N 个维度不符合 → 升级为严重不符合</summary>
        public int CrossProcessThreshold { get; set; } = 2;

        // ─── 未检查项处理 ───
        /// <summary>cap_basic=封顶到中间档 | reject=直接判不符合 | ignore=不处理</summary>
        [SugarColumn(Length = 30)]
        public string UnverifiedPolicy { get; set; } = "cap_basic";

        // ─── 结论生成模式（18 号 §九）───
        /// <summary>RULE_ONLY=仅规则不调AI | AI_ASSISTED=规则算结论+AI写理由 | EXPERT_MANUAL=专家全人工</summary>
        [SugarColumn(Length = 20)]
        public string ConclusionMode { get; set; } = "AI_ASSISTED";

        /// <summary>专家是否必须签署（★RULE_ONLY 且论证充分时可=0，报告须标注「未经人工签署」）</summary>
        public bool ManualRequired { get; set; }

        /// <summary>结论改动率告警阈值（%），专家改判率超此值提示规则需重新论证</summary>
        public int OverrideAlertThreshold { get; set; } = 30;

        // ─── Prompt 绑定 ───
        [SugarColumn(Length = 100, IsNullable = true)]
        public string? JudgePromptCode { get; set; }

        [SugarColumn(Length = 100, IsNullable = true)]
        public string? ConcludePromptCode { get; set; }

        // ─── 审计 ───
        /// <summary>★规则来源（标准号+条款号+版本），换版时改配置并更新此列，历史结论可回溯</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? SourceRef { get; set; }

        [SugarColumn(Length = 50, IsNullable = true)]
        public string? Status { get; set; }

        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        public int Sort { get; set; }

        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }

    /// <summary>
    /// ★ nc判断 判定框架 Prompt（表：cert_nc_judge_prompt）
    /// <para>18 号 §9.3：★<b>全局复用一份</b>。专家逐条写的是【判定标准】
    /// （cert_validation_rule.dimensions.JudgeCriteria），这里只写【判定框架】——
    /// 四态语义 / 证据要求 / 维度关联提示 / 输出格式。</para>
    /// <para>★ 规则数增加时本表<b>不变</b>，这就是省掉专家编写 Prompt 成本的关键。</para>
    /// </summary>
    [SugarTable("cert_nc_judge_prompt")]
    public class NcJudgePrompt : BaseEntity, ISoftDelete, IIsValid
    {
        [SugarColumn(Length = 50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>Prompt 编码（规则表 JudgePromptCode 引用此值）</summary>
        [SugarColumn(Length = 100)]
        public string PromptCode { get; set; } = string.Empty;

        [SugarColumn(Length = 200)]
        public string PromptName { get; set; } = string.Empty;

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? SystemPrompt { get; set; }

        [SugarColumn(ColumnDataType = "longtext")]
        public string UserTemplate { get; set; } = string.Empty;

        [SugarColumn(Length = 100, IsNullable = true)]
        public string? Model { get; set; }

        /// <summary>★低温保证可复现（0=最确定）</summary>
        public decimal Temperature { get; set; } = 0.10m;

        public int MaxTokens { get; set; } = 2000;

        /// <summary>★版本（Prompt 变更留痕，历史结论可回溯）</summary>
        public int Version { get; set; } = 1;

        /// <summary>是否默认（同一 PromptCode 只能一条为 1）</summary>
        public bool IsDefault { get; set; }

        [SugarColumn(Length = 50, IsNullable = true)]
        public string? Status { get; set; }

        public int Sort { get; set; }
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }

    /// <summary>
    /// ★ nc结论 理由撰写 Prompt（表：cert_nc_conclude_prompt）
    /// <para>18 号 §3.4：★ 结论已由规则引擎算好并注入，AI 只负责写"为什么"，<b>不得改结论</b>。</para>
    /// </summary>
    [SugarTable("cert_nc_conclude_prompt")]
    public class NcConcludePrompt : BaseEntity, ISoftDelete, IIsValid
    {
        [SugarColumn(Length = 50)]
        public string OrgCode { get; set; } = string.Empty;

        [SugarColumn(Length = 100)]
        public string PromptCode { get; set; } = string.Empty;

        [SugarColumn(Length = 200)]
        public string PromptName { get; set; } = string.Empty;

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? SystemPrompt { get; set; }

        [SugarColumn(ColumnDataType = "longtext")]
        public string UserTemplate { get; set; } = string.Empty;

        [SugarColumn(Length = 100, IsNullable = true)]
        public string? Model { get; set; }

        public decimal Temperature { get; set; } = 0.20m;

        public int MaxTokens { get; set; } = 500;

        public int Version { get; set; } = 1;

        public bool IsDefault { get; set; }

        [SugarColumn(Length = 50, IsNullable = true)]
        public string? Status { get; set; }

        public int Sort { get; set; }
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
