using System;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// 企业-阶段-标准关联表（多对多，专家端）
    /// <para>表名：<c>cert_enterprise_stage</c></para>
    /// <para>关联：<c>cert_enterprise.Code</c> ↔ <c>cert_cert_stage.StageCode</c> ↔ <c>cert_iso_standard.Code</c></para>
    ///
    /// <para><b>为什么是三元组而不是两张关联表</b>：用户 2026-09-25 明确
    /// 「一个企业在**不同阶段**，可能申请**一个到多个标准**的认证」——
    /// 阶段与标准是**组合**关系（同一阶段可以只申请 9001，也可以同时申请 9001+14001）。
    /// 拆成「企业-阶段」+「企业-标准」两张表会丢掉这个组合语义。</para>
    ///
    /// <para><b>为什么用硬删除</b>：本表是纯关联，无历史价值，取消勾选 = 删行。
    /// 若走软删除，<c>uk_ent_stage_std</c> 唯一键会在「取消后再次勾选」时被占用
    /// （同一组合被软删两次 → 撞唯一键，即 N58 的坑）。</para>
    ///
    /// <para><b>为什么阶段表统一用 cert_cert_stage</b>：用户 2026-09-25 裁定。
    /// 菜单 <c>MENU_00203</c>「认证阶段定义」已指向它，机构-阶段关联
    /// （<c>cert_org_stage</c>）也读它；<c>cert_phase_definition</c> 是重复实现的孤儿
    /// （前端零引用、0 行），本次不动。</para>
    ///
    /// 命名规范（YZH 铁律七）：DB 列名 = C# 属性名 = TS 字段名 = PascalCase
    /// </summary>
    [SugarTable("cert_enterprise_stage")]
    [YZHDeleteStrategy(Mode = DeleteMode.Hard)]
    public class CertEnterpriseStage : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────

        /// <summary>企业编码（关联 cert_enterprise.Code）</summary>
        [SugarColumn(Length = 50)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>阶段编码（关联 cert_cert_stage.StageCode）</summary>
        [SugarColumn(Length = 50)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>标准编码（关联 cert_iso_standard.Code）</summary>
        [SugarColumn(Length = 50)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>业务状态（none=未开始；后续流程节点会改写）</summary>
        [SugarColumn(Length = 20)]
        public string Status { get; set; } = "none";

        /// <summary>备注</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        // ──── ISoftDelete / IIsValid 接口字段 ────
        // 硬删除策略下 IsDeleted 恒为 false；保留列是为了 EntityService 的
        // 「IsValid=1 AND IsDeleted=0」统一过滤能正常生成 SQL。

        /// <summary>软删除标记（硬删除策略下恒 false）</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code（硬删除策略下不写）</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间（硬删除策略下不写）</summary>
        public DateTime? DeleteTime { get; set; }

        /// <summary>有效标志（1=有效，0=无效）</summary>
        public int IsValid { get; set; } = 1;
    }
}
