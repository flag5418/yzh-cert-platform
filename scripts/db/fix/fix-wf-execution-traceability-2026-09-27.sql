-- ============================================================
-- 执行追溯与节点翻译 — DB 改造脚本
-- 日期：2026-09-27
-- 变更：
--   1. wf_execution_task 补规则上下文快照列
--   2. wf_node_execution 补来源/AI/审批/描述列
--   3. 新建 wf_node_approval 节点审批表
-- 幂等：DDL 均按 information_schema 判存，可重复执行
-- ============================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ═══════════════════════════════════════════════════════════
-- 1. wf_execution_task — 规则上下文快照
-- ═══════════════════════════════════════════════════════════

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_execution_task' AND COLUMN_NAME = 'RuleName');
SET @sql := IF(@c = 0,
  'ALTER TABLE `wf_execution_task` ADD COLUMN `RuleName` varchar(200) NULL DEFAULT NULL COMMENT ''规则中文名称（执行时快照）'' AFTER `TaskType`',
  'DO 0');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_execution_task' AND COLUMN_NAME = 'SeverityIfViolated');
SET @sql := IF(@c = 0,
  'ALTER TABLE `wf_execution_task` ADD COLUMN `SeverityIfViolated` varchar(20) NULL DEFAULT NULL COMMENT ''违规严重级别 major/minor/observation'' AFTER `RuleName`',
  'DO 0');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ═══════════════════════════════════════════════════════════
-- 2. wf_node_execution — 节点证据 + 描述列
-- ═══════════════════════════════════════════════════════════

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_node_execution' AND COLUMN_NAME = 'SourceFileCode');
SET @sql := IF(@c = 0,
  'ALTER TABLE `wf_node_execution` ADD COLUMN `SourceFileCode` varchar(200) NULL DEFAULT NULL COMMENT ''源文件编码 cert_extraction_result.FileCode'' AFTER `SkillCode`',
  'DO 0');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_node_execution' AND COLUMN_NAME = 'SourceFieldName');
SET @sql := IF(@c = 0,
  'ALTER TABLE `wf_node_execution` ADD COLUMN `SourceFieldName` varchar(200) NULL DEFAULT NULL COMMENT ''源字段中文名（展示用）'' AFTER `SourceFileCode`',
  'DO 0');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_node_execution' AND COLUMN_NAME = 'SourceVersion');
SET @sql := IF(@c = 0,
  'ALTER TABLE `wf_node_execution` ADD COLUMN `SourceVersion` int NULL DEFAULT NULL COMMENT ''源文件版本号（cert_extraction_result.VersionNumber）'' AFTER `SourceFieldName`',
  'DO 0');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_node_execution' AND COLUMN_NAME = 'AiModel');
SET @sql := IF(@c = 0,
  'ALTER TABLE `wf_node_execution` ADD COLUMN `AiModel` varchar(100) NULL DEFAULT NULL COMMENT ''LLM 模型名（如 qwen-turbo）'' AFTER `LlmDurationMs`',
  'DO 0');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_node_execution' AND COLUMN_NAME = 'AiPrompt');
SET @sql := IF(@c = 0,
  'ALTER TABLE `wf_node_execution` ADD COLUMN `AiPrompt` longtext NULL DEFAULT NULL COMMENT ''渲染后送 API 的完整 prompt（8KB 截断）'' AFTER `AiModel`',
  'DO 0');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_node_execution' AND COLUMN_NAME = 'NodeDescription');
SET @sql := IF(@c = 0,
  'ALTER TABLE `wf_node_execution` ADD COLUMN `NodeDescription` varchar(500) NULL DEFAULT NULL COMMENT ''该节点在此工作流中的具体作用描述（由设计器填写）'' AFTER `NodeTitle`',
  'DO 0');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_node_execution' AND COLUMN_NAME = 'ApprovedOutputJson');
SET @sql := IF(@c = 0,
  'ALTER TABLE `wf_node_execution` ADD COLUMN `ApprovedOutputJson` json NULL DEFAULT NULL COMMENT ''专家审批后最终输出（null=未审批或无修改）'' AFTER `OutputJson`',
  'DO 0');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ═══════════════════════════════════════════════════════════
-- 3. 新建 wf_node_approval — 节点级审批记录
-- ═══════════════════════════════════════════════════════════

SET @tbl_exists := (SELECT COUNT(*) FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_node_approval');
SET @sql := IF(@tbl_exists = 0,
  'CREATE TABLE `wf_node_approval` ('
  + '`Id` bigint NOT NULL AUTO_INCREMENT COMMENT '"'"'主键'"'"','
  + '`Code` varchar(36) NOT NULL COMMENT '"'"'全局唯一编码（GUID）'"'"','
  + '`TaskCode` varchar(36) NOT NULL COMMENT '"'"'wf_execution_task.Code'"'"','
  + '`NodeId` varchar(64) NOT NULL COMMENT '"'"'节点 ID'"'"','
  + '`ApprverCode` varchar(36) NOT NULL COMMENT '"'"'审批人编码（Sys_User.Code）'"'"','
  + '`ApprverName` varchar(50) NULL DEFAULT NULL COMMENT '"'"'审批人姓名'"'"','
  + '`ApprovalStatus` varchar(20) NOT NULL DEFAULT '"'"'pending'"'"' COMMENT '"'"'pending/approved/rejected'"'"','
  + '`ApprovedAt` datetime NULL DEFAULT NULL COMMENT '"'"'审批时间'"'"','
  + '`Comment` text NULL COMMENT '"'"'审批意见'"'"','
  + '`ManualResult` json NULL COMMENT '"'"'专家手动修改后的节点输出（JSON）'"'"','
  + '`Confidence` decimal(5,2) NULL DEFAULT NULL COMMENT '"'"'专家可信度评分 0.00~1.00'"'"','
  + '`IsDeleted` tinyint(1) NOT NULL DEFAULT 0,'
  + '`DeleteBy` varchar(50) NULL,'
  + '`DeleteTime` datetime NULL,'
  + '`IsValid` int NOT NULL DEFAULT 1,'
  + '`CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,'
  + '`UpdateTime` datetime NULL,'
  + '`CreateBy` varchar(50) NULL,'
  + '`UpdateBy` varchar(50) NULL,'
  + '  PRIMARY KEY (`Id`),'
  + '  UNIQUE KEY `uk_code` (`Code`),'
  + '  UNIQUE KEY `uk_task_node` (`TaskCode`,`NodeId`),'
  + '  KEY `idx_task` (`TaskCode`),'
  + '  KEY `idx_approver` (`ApprverCode`)'
  + ') ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci'
  + ' COMMENT='"'"'节点审批记录（专家对单个节点执行结果的认可/修改）'"'"'',
  'DO 0');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ═══════════════════════════════════════════════════════════
-- 验证
-- ═══════════════════════════════════════════════════════════
-- SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, COLUMN_COMMENT
--   FROM information_schema.COLUMNS
--  WHERE TABLE_SCHEMA = DATABASE()
--    AND TABLE_NAME IN ('wf_execution_task','wf_node_execution','wf_node_approval')
--    AND COLUMN_NAME IN ('RuleName','SeverityIfViolated','SourceFileCode','SourceFieldName',
--                        'SourceVersion','AiModel','AiPrompt','NodeDescription','ApprovedOutputJson')
-- ORDER BY TABLE_NAME, COLUMN_NAME;
