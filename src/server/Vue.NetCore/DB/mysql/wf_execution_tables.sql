-- ============================================================================
-- 工作流执行层 - 数据库迁移脚本
-- 版本：V3.0
-- 日期：2026-08-23
-- 说明：
--   1. wf_execution_task：一次业务触发的执行任务（对应 yzh_queue 层）
--   2. wf_execution_task_item：一个检查项的执行记录（对应 yzh_queue_task 层）
--   3. wf_node_execution：每个节点的执行状态（跨路径复用的核心载体）
--   4. ALTER wf_workflow_execution_log：增加 task_code/node_id 等字段
--
-- V3 变更：
--   - 列名全部 snake_case（与实体 [Column("snake_case")] 映射一致）
--   - 新增 is_success 字段（wf_execution_task_item，业务成功标志）
--   - 新增 cache_keys 字段（task 和 item，记录预热的缓存键列表）
--   - 移除 org_code 列（YZHBaseEntity 已移除 OrgCode 字段）
--   - 改为 CREATE TABLE IF NOT EXISTS 幂等写法
-- ============================================================================

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ============================================================================
-- D-01 wf_execution_task（执行任务）
-- 定位：一次业务触发 = 一个任务，是执行层的顶层入口
-- 对应：yzh_queue（队列层）
-- ============================================================================

CREATE TABLE IF NOT EXISTS `wf_execution_task` (
    -- 基类字段（snake_case）
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_id` int DEFAULT NULL COMMENT '创建人ID',
    `creator` varchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `create_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `modify_id` int DEFAULT NULL COMMENT '修改人ID',
    `modifier` varchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `modify_date` datetime DEFAULT NULL COMMENT '修改时间',
    `delete_id` int DEFAULT NULL COMMENT '删除人ID',
    `deleter` varchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    `status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `sort` int DEFAULT 0 COMMENT '排序号',
    `remark` varchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `task_type` varchar(20) NOT NULL COMMENT '任务类型：TEST | NC_CHECK | REPORT_GENERATE',
    `task_status` varchar(20) NOT NULL DEFAULT 'queued' COMMENT '执行状态：queued|executing|completed|failed|cancelled',

    `config_snapshot` json NOT NULL COMMENT '执行时的工作流配置快照（从cert_validation_rule.rule_json锁定）',

    `rule_code` varchar(64) DEFAULT NULL COMMENT 'cert_validation_rule.rule_code（配置来源）',
    `enterprise_code` varchar(50) DEFAULT NULL COMMENT '企业编码（运行时绑定）',
    `phase_code` varchar(30) DEFAULT NULL COMMENT '审核阶段',

    `queue_code` varchar(64) DEFAULT NULL COMMENT 'yzh_queue.queue_code（关联队列层）',

    `cache_keys` text DEFAULT NULL COMMENT '预热的缓存键列表（JSON数组，任务级缓存方案）',

    `result_summary` json DEFAULT NULL COMMENT '执行结果摘要（end节点输出）',
    `error_message` varchar(2000) DEFAULT NULL COMMENT '失败原因',

    `started_at` datetime DEFAULT NULL COMMENT '开始执行时间',
    `completed_at` datetime DEFAULT NULL COMMENT '完成时间',
    `duration_ms` int DEFAULT NULL COMMENT '执行耗时(ms)',

    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_type_status` (`task_type`, `task_status`),
    KEY `idx_rule` (`rule_code`),
    KEY `idx_enterprise` (`enterprise_code`),
    KEY `idx_phase` (`phase_code`),
    KEY `idx_queue` (`queue_code`),
    KEY `idx_status` (`task_status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='工作流执行任务（TEST/NC_CHECK/REPORT_GENERATE）';

-- ============================================================================
-- D-02 wf_execution_task_item（执行项）
-- 定位：一个NC检查项的独立执行单元
-- 对应：yzh_queue_task（子任务层）
-- ============================================================================

CREATE TABLE IF NOT EXISTS `wf_execution_task_item` (
    -- 基类字段（snake_case）
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_id` int DEFAULT NULL COMMENT '创建人ID',
    `creator` varchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `create_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `modify_id` int DEFAULT NULL COMMENT '修改人ID',
    `modifier` varchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `modify_date` datetime DEFAULT NULL COMMENT '修改时间',
    `delete_id` int DEFAULT NULL COMMENT '删除人ID',
    `deleter` varchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    `status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `sort` int DEFAULT 0 COMMENT '排序号',
    `remark` varchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `task_code` varchar(36) NOT NULL COMMENT 'wf_execution_task.code（所属任务）',

    `rule_code` varchar(64) NOT NULL COMMENT 'cert_validation_rule.rule_code（关联配置定义）',
    `item_type` varchar(20) NOT NULL COMMENT 'NC_CHECK | REPORT_GENERATE',
    `item_status` varchar(20) NOT NULL DEFAULT 'queued' COMMENT 'queued|executing|completed|failed|cancelled',
    `is_success` tinyint DEFAULT NULL COMMENT '业务成功标志：1=成功 0=失败 NULL=未完成',

    `cache_keys` text DEFAULT NULL COMMENT '预热的缓存键列表（JSON数组，任务级缓存方案）',

    `result_summary` json DEFAULT NULL COMMENT '本项执行结果（end节点输出）',
    `error_message` varchar(2000) DEFAULT NULL COMMENT '失败原因',

    `started_at` datetime DEFAULT NULL COMMENT '开始执行时间',
    `completed_at` datetime DEFAULT NULL COMMENT '完成时间',
    `duration_ms` int DEFAULT NULL COMMENT '执行耗时(ms)',

    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_task_rule` (`task_code`, `rule_code`),
    KEY `idx_task` (`task_code`),
    KEY `idx_task_status` (`task_code`, `item_status`),
    KEY `idx_rule` (`rule_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='执行项（一个NC检查项 / 一个报告章节）';

-- ============================================================================
-- D-03 wf_node_execution（节点执行状态）
-- 定位：每个节点一次执行的状态记录，是结果复用和断点续跑的核心载体
-- 跨路径复用：同一item下，同node_id已执行 → 读库复用，不重跑
-- ============================================================================

CREATE TABLE IF NOT EXISTS `wf_node_execution` (
    -- 基类字段（snake_case）
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_id` int DEFAULT NULL COMMENT '创建人ID',
    `creator` varchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `create_date` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `modify_id` int DEFAULT NULL COMMENT '修改人ID',
    `modifier` varchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `modify_date` datetime DEFAULT NULL COMMENT '修改时间',
    `delete_id` int DEFAULT NULL COMMENT '删除人ID',
    `deleter` varchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    `status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `sort` int DEFAULT 0 COMMENT '排序号',
    `remark` varchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `task_code` varchar(36) NOT NULL COMMENT 'wf_execution_task.code',
    `item_code` varchar(36) NOT NULL COMMENT 'wf_execution_task_item.code',

    `node_id` varchar(64) NOT NULL COMMENT '节点ID（前端生成的 classCode_n序号）',
    `node_type` varchar(30) DEFAULT NULL COMMENT 'start|end|skill|ai_node|logic|branch|docField|docTable',
    `node_title` varchar(128) DEFAULT NULL COMMENT '节点名称快照',
    `skill_code` varchar(64) DEFAULT NULL COMMENT 'Skill编码（功能节点）',

    `exec_status` varchar(20) NOT NULL DEFAULT 'pending' COMMENT 'pending|executing|completed|failed|skipped',
    `output_json` json DEFAULT NULL COMMENT '节点输出（所有端口的JSON）',
    `error_message` varchar(1000) DEFAULT NULL COMMENT '执行错误信息',

    `started_at` datetime DEFAULT NULL COMMENT '开始执行时间',
    `completed_at` datetime DEFAULT NULL COMMENT '完成时间',
    `execution_time_ms` int DEFAULT NULL COMMENT '执行耗时(ms)',

    `is_reused` tinyint DEFAULT 0 COMMENT '0=新执行 1=复用了历史结果',

    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_task_item_node` (`task_code`, `item_code`, `node_id`),
    KEY `idx_task_item` (`task_code`, `item_code`),
    KEY `idx_task` (`task_code`),
    KEY `idx_node_id` (`node_id`),
    KEY `idx_exec_status` (`exec_status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='节点执行状态（跨路径复用的核心载体）';

-- ============================================================================
-- D-04 ALTER wf_workflow_execution_log（扩展现有执行日志表）
-- 幂等处理：使用 INFORMATION_SCHEMA 检查列是否已存在
-- ============================================================================

-- 检查并添加 task_code 列
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_workflow_execution_log' AND COLUMN_NAME = 'task_code');
SET @sql = IF(@col_exists = 0,
    'ALTER TABLE `wf_workflow_execution_log` ADD COLUMN `task_code` varchar(36) DEFAULT NULL COMMENT ''wf_execution_task.code（新版关联键）'' AFTER `WorkflowCode`',
    'SELECT ''task_code already exists'' AS msg');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 检查并添加 item_code 列
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_workflow_execution_log' AND COLUMN_NAME = 'item_code');
SET @sql = IF(@col_exists = 0,
    'ALTER TABLE `wf_workflow_execution_log` ADD COLUMN `item_code` varchar(36) DEFAULT NULL COMMENT ''wf_execution_task_item.code'' AFTER `task_code`',
    'SELECT ''item_code already exists'' AS msg');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 检查并添加 node_type 列
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_workflow_execution_log' AND COLUMN_NAME = 'node_type');
SET @sql = IF(@col_exists = 0,
    'ALTER TABLE `wf_workflow_execution_log` ADD COLUMN `node_type` varchar(30) DEFAULT NULL COMMENT ''节点类型（start/end/skill/ai_node/logic/branch）'' AFTER `NodeId`',
    'SELECT ''node_type already exists'' AS msg');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 检查并添加 event_type 列
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_workflow_execution_log' AND COLUMN_NAME = 'event_type');
SET @sql = IF(@col_exists = 0,
    'ALTER TABLE `wf_workflow_execution_log` ADD COLUMN `event_type` varchar(20) DEFAULT NULL COMMENT ''事件类型：node_started|node_completed|node_failed|branch_decision|task_completed'' AFTER `execution_status`',
    'SELECT ''event_type already exists'' AS msg');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 检查并添加 branch_result 列
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_workflow_execution_log' AND COLUMN_NAME = 'branch_result');
SET @sql = IF(@col_exists = 0,
    'ALTER TABLE `wf_workflow_execution_log` ADD COLUMN `branch_result` json DEFAULT NULL COMMENT ''分支决策明细（选中/跳过的线路）'' AFTER `event_type`',
    'SELECT ''branch_result already exists'' AS msg');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 检查并添加 is_reused 列
SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_workflow_execution_log' AND COLUMN_NAME = 'is_reused');
SET @sql = IF(@col_exists = 0,
    'ALTER TABLE `wf_workflow_execution_log` ADD COLUMN `is_reused` tinyint DEFAULT NULL COMMENT ''0=新执行 1=复用了历史结果'' AFTER `branch_result`',
    'SELECT ''is_reused already exists'' AS msg');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 检查并添加索引
SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_workflow_execution_log' AND INDEX_NAME = 'idx_task_code');
SET @sql = IF(@idx_exists = 0,
    'ALTER TABLE `wf_workflow_execution_log` ADD INDEX `idx_task_code` (`task_code`)',
    'SELECT ''idx_task_code already exists'' AS msg');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_workflow_execution_log' AND INDEX_NAME = 'idx_item_code');
SET @sql = IF(@idx_exists = 0,
    'ALTER TABLE `wf_workflow_execution_log` ADD INDEX `idx_item_code` (`item_code`)',
    'SELECT ''idx_item_code already exists'' AS msg');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET FOREIGN_KEY_CHECKS = 1;
