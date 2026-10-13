-- ============================================================
-- 视图：v_cert_stage
-- 用途：认证阶段列表（T+V 的 V 端）—— 含字典翻译列 CategoryName / StatusName
-- 引用表：cert_cert_stage / sys_dictionary / sys_dictionarylist
-- 算法：MERGE（无 GROUP BY / UNION / DISTINCT / 聚合 ⇒ 谓词可下推到基表）
-- 日期：2026-10-13 补录（★ 库中早已存在、scripts/db/views/ 缺档 ⇒ 见规范 §一 偏差 D-1）
--
-- ★ 有效性规范（26-数据库视图设计规范-V1 §三）
--   · 固化 IsDeleted = 0 —— 主表 + 两张字典表（软删行 = 逻辑不存在）
--   · ⛔ 不固化 IsValid —— 以列暴露（s.IsValid AS IsValid），由消费侧判：
--       /filter /list → SqlSugarDbOrm.GetPageAsync 经 GetColumnName<T>("IsValid")
--       命中真实列 ⇒ 自动追加 `IsValid` = 1（2026-10-13 实测该路径生效）
--   · 补录时的加固说明：原库定义**未**对主表过滤 IsDeleted。
--     GetPageAsync 路径本就追加 IsDeleted = 0（口径不变）；
--     而 GetListAsync / CountAsync / ExistsAsync 走 IsDeclaredOnType 判据
--     （要求属性**直接声明在 V 类型上**，继承自 T 的不算）⇒ 原先不过滤软删。
--     本 DDL 固化后两条路径口径一致 —— 纯加固，页面行为不变。
--
-- ★ 归属：后台端独占（仅 Admin 端 CertStageController 消费）
--   T = CertPlatform.Shared/Entities/Cert/CertStage（写，[SugarTable("cert_cert_stage")]）
--   V = CertPlatform.Admin/Entities/Cert/CertStageView（读，[ViewName] 路由到本视图）
--   ⚠️ V 实体沿用 [SugarColumn(IsOnlyIgnoreInsert/Update=true)] 暴露 CategoryName/StatusName，
--      ⛔ 禁用 IsIgnore（会把列从 SELECT 一并剔除 ⇒ /filter 恒空串，2026-10-08 已踩）
--
-- ★ 排序规则：原库定义含 COLLATE utf8mb4_general_ci 提示；
--   20260923_unify_collation_V1 统一后相关列均已是 utf8mb4_general_ci
--   ⇒ 提示已成冗余，本 DDL 去除（语义等价，已核对 information_schema.COLUMNS）
--
-- 回滚：DROP VIEW IF EXISTS `v_cert_stage`;
-- ============================================================

CREATE OR REPLACE VIEW `v_cert_stage` AS
SELECT
    s.Id,
    s.Code,
    s.StageCode,
    s.StageName,
    s.Description,
    s.Sort,
    s.Category,
    cat.DicName AS CategoryName,
    s.Status,
    CASE s.Status
        WHEN 'active'   THEN '启用'
        WHEN 'inactive' THEN '停用'
        ELSE s.Status
    END AS StatusName,
    s.Remark,
    s.IsValid,
    s.CreateBy,
    s.CreateTime,
    s.UpdateBy,
    s.UpdateTime,
    s.DeleteBy,
    s.DeleteTime,
    s.IsDeleted
FROM cert_cert_stage s
LEFT JOIN sys_dictionary d
       ON d.DicNo = 'stage_category'
      AND d.IsDeleted = 0
LEFT JOIN sys_dictionarylist cat
       ON cat.DicCode = d.Code
      AND cat.DicValue = s.Category
      AND cat.IsDeleted = 0
WHERE s.IsDeleted = 0;
