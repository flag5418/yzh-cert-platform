using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// 认证阶段（全局基础资料）
    /// <para>表名：cert_cert_stage（列表读视图 v_cert_stage，见 CertStageView T+V）</para>
    /// <para>★ 分类 Category 来自字典 stage_category（process 流程阶段 / audit 审核阶段 /
    /// post 证后阶段）—— 仅作标签与左树分组用，<b>不参与</b> NC / 报告关联；
    /// 阶段关联权威键 = 本表 Code（GUID）。</para>
    /// <para>★ 数据现状（2026-10-08，种子 20261008_cert_stage_standard_seed_V1.sql +
    /// 迁移 20261008_stage_code_unify_V1.sql）：共 14 行 = process: AP/CR/SP/CD、
    /// audit: S1/S2/SV/RC/SA/TT、post: CE/SUS/REV/CAN。
    /// 原测试行 jd01 初审 / jd02 预审 / 03 复审 已删除（Q3），其引用按 Q2 统一为
    /// 本表 Code（GUID）：jd01→S2、jd02→S1、03→RC。
    /// 「哪些阶段出 NC / 出报告」见
    /// docs/20-体系认证/05-业务知识库/认证阶段业务全景与NC报告产出矩阵-V1.md §二</para>
    /// <para>ORM：SqlSugar（§16 铁律：DB列名 == C#属性名，PascalCase）</para>
    /// </summary>
    [SugarTable("cert_cert_stage")]
    public class CertStage : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id 已由 BaseEntity 基类统一提供 ────

        /// <summary>阶段编码（人读编号：AP/CR/SP/CD · S1/S2/SV/RC/SA/TT · CE/SUS/REV/CAN）</summary>
        [Required]
        [StringLength(50)]
        [UniqueField("阶段编码")]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>阶段名称</summary>
        [Required]
        [StringLength(200)]
        public string StageName { get; set; } = string.Empty;

        /// <summary>分类（字典 stage_category：process/audit/post）</summary>
        [StringLength(50)]
        public string Category { get; set; } = "process";

        /// <summary>排序号（1~9）</summary>
        public int SortOrder { get; set; }

        /// <summary>阶段说明</summary>
        public string? Description { get; set; }

        /// <summary>状态（active/inactive）</summary>
        [StringLength(50)]
        public string Status { get; set; } = "active";

        /// <summary>备注</summary>
        [StringLength(500)]
        public string? Remark { get; set; }

        // ──── 接口字段（BaseEntity 不包含，由接口继承提供） ────

        /// <summary>有效标志（1=有效，0=无效）</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记（false=正常，true=已删除）</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code（仅软删除时赋值）</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间（仅软删除时赋值）</summary>
        public DateTime? DeleteTime { get; set; }
    }
}
