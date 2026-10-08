using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CertPlatform.Shared.Entities.Cert;
using SqlSugar;
using YZH.Core.Stand.Annotations;

namespace CertPlatform.Admin.Entities.Cert
{
    /// <summary>
    /// CertStage 视图模型（V）— 用于列表显示，含字典翻译后的中文字段
    /// 
    /// 架构设计（T+V 模式）：
    /// - T = CertStage（实体表，用于增删改，位于 CertPlatform.Shared/Entities/Cert/）
    /// - V = CertStageView（视图，用于显示，包含关联字段）
    /// 
    /// 数据来源：v_cert_stage MySQL 视图
    ///
    /// ★ 归属（2026-10-08 guards R17）：后台端独占（仅 CertStageController 消费）
    ///   ⇒ 按 24-后端实体归属清单 §一 从 Shared/Entities/Cert/ 下沉到本目录
    ///
    /// ★ 2026-10-08 启用（用户裁决，24-后端实体归属清单 §⛔直接删 已同步移除本类）：
    /// - [ViewName]  → EntityService.GetQueryTableName 优先级 1：/filter /list 查询走 v_cert_stage
    /// - [SugarTable] → ⛔ 必须显式写：SqlSugar GetTableName(inherit:false) 不继承基类特性，
    ///                  不写则写入解析成表名 CertStageView（表不存在）
    /// - [SugarColumn(IsOnlyIgnoreInsert/Update=true)] → ⛔ 关键组合：
    ///   · IsIgnore=true 会把字段从 SELECT 也剔除 ⇒ /filter 返回恒空串（2026-10-08 实测踩坑）
    ///   · IsOnlyIgnore* 只从 INSERT/UPDATE 剔除、SELECT 保留 ⇒ 读到视图中文、写不报列不存在
    ///   · 仅 [NotMapped]（EF 语义）对 SqlSugar 不保证生效
    /// - 注：不要添加 [Table]（EF）属性，否则会覆盖父类的表名配置
    /// </summary>
    [ViewName("v_cert_stage")]
    [SugarTable("cert_cert_stage")]
    [NotMapped]
    public class CertStageView : CertStage  // 继承 CertStage 以支持 Cast
    {
        // ====== 视图特有字段（字典翻译后的中文）======

        /// <summary>分类中文名（流程阶段/审核阶段/证后阶段）— 来源 v_cert_stage 的字典 join</summary>
        [SugarColumn(IsOnlyIgnoreInsert = true, IsOnlyIgnoreUpdate = true)]
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>状态中文名（启用/停用）— 来源 v_cert_stage 的 CASE 派生列</summary>
        [SugarColumn(IsOnlyIgnoreInsert = true, IsOnlyIgnoreUpdate = true)]
        public string StatusName { get; set; } = string.Empty;
    }
}
