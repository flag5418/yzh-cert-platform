-- ==========================================================
-- ReportDefinition 迁移：补 rpt_report_section 缺失字段
-- 日期：2026-09-15
-- 说明：表为空，安全执行
-- ==========================================================

-- 1. 补 rpt_report_section 缺失字段
ALTER TABLE `rpt_report_section`
  ADD COLUMN `SectionNameEn` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '英文名称' AFTER `SectionName`,
  ADD COLUMN `IsActive` tinyint(1) NOT NULL DEFAULT '1' COMMENT '是否启用' AFTER `SortOrder`,
  ADD COLUMN `WorkflowConfig` text COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '工作流DAG JSON' AFTER `WorkflowCode`,
  ADD COLUMN `LayoutJson` text COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '工作流布局JSON' AFTER `WorkflowConfig`,
  ADD COLUMN `SectionJson` text COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '章节配置JSON' AFTER `LayoutJson`;
