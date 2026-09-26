-- =============================================================================
-- 清理 Vol 框架遗留表（2026-09-25）
-- -----------------------------------------------------------------------------
-- 【背景】用户要求清理库中遗留表。**实测：库中不存在任何 `bak` 命名的表**
--   （`unify_enable_to_isvalid_V1/V2.sql` 曾建 `_bak_20260924_*`，但已被
--    2026-09-24 的 `rebuild_db.sh`（DROP DATABASE + 重建）冲掉，现已不存在）。
--   实际存在的是 **9 张 Vol 框架遗留表**（表单设计器 Demo / 销售 Demo / 测试表 / Quartz / 工作流审计日志）。
--
-- 【判据（三条全中才删）】
--   ① 0 行数据（实测全部 0 行）
--   ② 全仓无 `[SugarTable]` 实体映射、无任何代码引用
--   ③ 属旧 Vol 框架产物：`Quartz` 仅被 `src/old/**` 引用（新架构未使用调度器）
--
-- 【⛔ 不删的未映射表（它们不是垃圾，是"还没建实体"的真实业务表）】
--   `audit_*`(7) / `rpt_report_task` / `rpt_report_section_source` /
--   `cert_application` / `cert_clause_extraction_rule` / `cert_validation_rule_source` /
--   `wf_field_label_mapping` / `wf_skill_api` / `wf_workflow_execution_log`
--   + 框架表 `Sys_City` / `Sys_Province` / `sys_config` / `Sys_RoleAuth`
--   → **"没有实体" ≠ "垃圾"**（这些后续功能要用）
--
-- 【备份】执行前已全库备份 → `backup/full-backup-before-junk-cleanup-20260925.sql`（791KB / 86 表）
--
-- 幂等：DROP TABLE IF EXISTS，可重复执行。
-- =============================================================================

USE `yzh_cert_platform`;

-- 1. 表单设计器 Demo（Vol 框架自带示例）
DROP TABLE IF EXISTS `FormCollectionObject`;
DROP TABLE IF EXISTS `FormDesignOptions`;

-- 2. 销售订单 Demo（Vol 框架自带示例）
DROP TABLE IF EXISTS `SellOrderList`;
DROP TABLE IF EXISTS `SellOrder`;

-- 3. 框架自测表
DROP TABLE IF EXISTS `TestDb`;
DROP TABLE IF EXISTS `TestService`;

-- 4. Quartz 调度（新架构未使用 Quartz；仅 src/old/** 引用）
DROP TABLE IF EXISTS `Sys_QuartzLog`;
DROP TABLE IF EXISTS `Sys_QuartzOptions`;

-- 5. Vol 工作流表审计日志（新架构用 yzh_queue_task / wf_* 替代）
DROP TABLE IF EXISTS `Sys_WorkFlowTableAuditLog`;

-- =============================================================================
-- 验证
-- =============================================================================
SELECT '--- 9 张遗留表应全部消失（期望 0 行）---' AS Info;
SELECT TABLE_NAME FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND TABLE_NAME IN ('FormCollectionObject','FormDesignOptions','SellOrder','SellOrderList',
                     'TestDb','TestService','Sys_QuartzLog','Sys_QuartzOptions','Sys_WorkFlowTableAuditLog');

SELECT '--- 清理后表总数 ---' AS Info;
SELECT COUNT(*) AS BaseTables FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_TYPE = 'BASE TABLE';

SELECT '--- 前缀分布 ---' AS Info;
SELECT SUBSTRING_INDEX(TABLE_NAME, '_', 1) AS Prefix, COUNT(*) AS Cnt
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_TYPE = 'BASE TABLE'
GROUP BY Prefix ORDER BY Cnt DESC;

SELECT '--- 悬空外键（期望 0 行）---' AS Info;
SELECT rc.TABLE_NAME, rc.CONSTRAINT_NAME, rc.REFERENCED_TABLE_NAME
FROM information_schema.REFERENTIAL_CONSTRAINTS rc
LEFT JOIN information_schema.TABLES t
  ON t.TABLE_SCHEMA = rc.CONSTRAINT_SCHEMA AND t.TABLE_NAME = rc.REFERENCED_TABLE_NAME
WHERE rc.CONSTRAINT_SCHEMA = 'yzh_cert_platform' AND t.TABLE_NAME IS NULL;
