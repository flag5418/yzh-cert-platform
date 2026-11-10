-- ============================================================================
-- 20261106_rebuild_expert_item_tables_V1.sql
--
-- 背景（2026-11-06 故障）：
--    11-05 灾难恢复 DROP DATABASE + CodeFirst 重建后，7 个含 decimal 的实体
--    （[SqlSugar] ColumnDataType="decimal(x,y)" + DecimalDigits=N 组合）建表失败
--    —— SqlSugar 生成 SQL 丢失 `decimal` 前缀（报 near '(10,2) NOT NULL'）。
--    11-06 已通过 `scripts/db/20260930_expert_task_tables_V1.sql` 补齐 6 张缺失表
--    （cert_expert_task / _standard / _queue / _nc_result / _report_result / _log）。
--
-- 本脚本处理剩余两项：
--   ① cert_ai_usage_log —— 同类失败实体，从未建成，本次补建；
--   ② 4 张「0 行但结构落后」的专家表（早期版本所建，缺全部业务唯一键/索引，
--      且 data_gap.GapKey 是普通列而非生成列）—— DROP 后按 DDL 权威重建：
--        · cert_expert_nc_item
--        · cert_expert_report_section_item
--        · cert_expert_task_queue_item
--        · cert_expert_task_data_gap（★ GapKey 采用 25 号裁决 J1 表达式 + idx_rule）
--
-- DDL 权威：
--   · scripts/db/20260930_expert_task_tables_V1.sql（A4/A5/B1/B3 段）
--   · scripts/db/fix/fix-extraction-value-source-20260930.sql §5（J1 GapKey 重定义 + idx_rule）
--
-- 前提核验（2026-11-06 实测）：4 张表均 0 行 ⇒ DROP 零数据风险。
--
-- 幂等：全部 CREATE TABLE IF NOT EXISTS；视图由 scripts/db/views/*.sql 另行重建。
-- 执行：docker exec -i yzh-mysql mysql -uroot -p*** --default-character-set=utf8mb4 yzh_cert_platform < 本文件
-- ============================================================================

USE `yzh_cert_platform`;
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

SELECT '========== 0. 前置核验：4 张表行数（必须全为 0） ==========' AS `step`;
SELECT 'cert_expert_nc_item' AS Tbl, COUNT(*) AS Rows_ FROM cert_expert_nc_item
UNION ALL SELECT 'cert_expert_report_section_item', COUNT(*) FROM cert_expert_report_section_item
UNION ALL SELECT 'cert_expert_task_queue_item', COUNT(*) FROM cert_expert_task_queue_item
UNION ALL SELECT 'cert_expert_task_data_gap', COUNT(*) FROM cert_expert_task_data_gap;


-- ============================================================================
-- ① DROP 4 张结构落后的旧表
-- ============================================================================
SELECT '========== 1. DROP 4 张 0 行旧表 ==========' AS `step`;
DROP TABLE IF EXISTS `cert_expert_nc_item`;
DROP TABLE IF EXISTS `cert_expert_report_section_item`;
DROP TABLE IF EXISTS `cert_expert_task_queue_item`;
DROP TABLE IF EXISTS `cert_expert_task_data_gap`;


-- ============================================================================
-- ② 重建 4 张表（DDL 取自 20260930_expert_task_tables_V1.sql）
-- ============================================================================

-- ── B1. cert_expert_nc_item — NC 检查项（企业级长期实体） ──
CREATE TABLE IF NOT EXISTS `cert_expert_nc_item` (
  `Id`            bigint      NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`          varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID，业务键）',
  `OrgCode`       varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★专家工作区编码（租户隔离键）',
  `CreateBy`      varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateName`    varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime`    datetime     DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy`      varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime`    datetime     DEFAULT NULL,
  `DeleteBy`      varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime`    datetime     DEFAULT NULL,
  `Status`        varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Sort`          int          DEFAULT 0,
  `Remark`        varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted`     tinyint(1)   NOT NULL DEFAULT 0,
  `IsValid`       int          NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）',

  `EnterpriseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '企业编码',
  `StageCode`      varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '阶段编码（★cert_cert_stage.Code，GUID）',
  `StandardCode`   varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '标准编码',

  `RuleCode`       varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★cert_validation_rule.Code（业务键）',
  `RuleNumber`     varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★规则编号（仅展示）',
  `RuleName`       varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '规则名称（快照）',
  `RuleNameEn`     varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '规则名称英文（快照）',
  `ClauseCode`     varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '条款编码（cert_iso_clause.Code）',
  `ClauseNumber`   varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '条款号（快照，如 8.4）',
  `ClauseTitle`    varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '条款标题（快照）',
  `JudgeMode`      varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '审核方式：auto | semi | manual',
  `SeverityDefault` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '严重度预设（SeverityIfViolated）',
  `WorkflowCode`   varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '工作流编码',
  `RuleVersion`    int          NOT NULL DEFAULT 1 COMMENT '★规则版本（结论可回溯到哪版规则）',
  `RuleContextHash` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★DAG 指纹',

  `CurrentResultCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★当前生效的结果行（cert_expert_nc_result.Code）',
  `RoundCount`      int          DEFAULT 0 COMMENT '★累计结果轮次数',
  `LastAuditedTime` datetime     DEFAULT NULL COMMENT '★上次检查时间（★沿用项不更新，D30）',
  `LastAuditedTaskCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '上次检查的任务',
  `LastReviewTime`  datetime     DEFAULT NULL COMMENT '上次专家认可/修改时间',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`     (`Code`),
  UNIQUE KEY `uk_scope_rule` (`OrgCode`, `EnterpriseCode`, `StageCode`, `StandardCode`, `RuleCode`),
  KEY `idx_scope`          (`OrgCode`, `EnterpriseCode`, `StageCode`, `StandardCode`),
  KEY `idx_current`        (`CurrentResultCode`),
  KEY `idx_audited`        (`EnterpriseCode`, `LastAuditedTime`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='★专家任务-NC检查项（企业级长期实体）';


-- ── B3. cert_expert_report_section_item — 报告章节（企业级长期实体） ──
CREATE TABLE IF NOT EXISTS `cert_expert_report_section_item` (
  `Id`         bigint      NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`       varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID，业务键）',
  `OrgCode`    varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★专家工作区编码',
  `CreateBy`   varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateName` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime     DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy`   varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime     DEFAULT NULL,
  `DeleteBy`   varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime     DEFAULT NULL,
  `Status`     varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Sort`       int          DEFAULT 0 COMMENT '章节排序（SortOrder 快照）',
  `Remark`     varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted`  tinyint(1)   NOT NULL DEFAULT 0,
  `IsValid`    int          NOT NULL DEFAULT 1,

  `EnterpriseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `StageCode`      varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '阶段编码（GUID）',
  `StandardCode`   varchar(36) COLLATE utf8mb4_general_ci NOT NULL,

  `SectionCode`    varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★配置层章节（cert_report_section.Code）',
  `SectionName`    varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '章节名称（快照）',
  `SectionNameEn`  varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `SectionTemplateContent` longtext COLLATE utf8mb4_general_ci COMMENT '★模板示例正文快照（★专家编辑时对照用，只读）',
  `ClauseCode`     varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★可空（概述/结论章节不映射条款）',
  `ClauseNumber`   varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ClauseTitle`    varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `WorkflowCode`   varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `RuleVersion`    int          NOT NULL DEFAULT 1 COMMENT '★规则版本',
  `RuleContextHash` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,

  `CurrentResultCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★当前生效结果',
  `RoundCount`      int          DEFAULT 0,
  `LastAuditedTime` datetime     DEFAULT NULL COMMENT '★上次检查时间（沿用不更新，D30）',
  `LastAuditedTaskCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `LastReviewTime`  datetime     DEFAULT NULL,

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`      (`Code`),
  UNIQUE KEY `uk_scope_sec` (`OrgCode`, `EnterpriseCode`, `StageCode`, `StandardCode`, `SectionCode`),
  KEY `idx_scope`          (`OrgCode`, `EnterpriseCode`, `StageCode`, `StandardCode`),
  KEY `idx_current`        (`CurrentResultCode`),
  KEY `idx_audited`        (`EnterpriseCode`, `LastAuditedTime`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='★专家任务-报告章节（企业级长期实体）';


-- ── A4. cert_expert_task_queue_item — 队列项（1 任务项 = 1 队列项） ──
CREATE TABLE IF NOT EXISTS `cert_expert_task_queue_item` (
  `Id`            bigint      NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`          varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID，业务键）',
  `OrgCode`       varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★专家工作区编码（租户隔离键）',
  `CreateBy`      varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateName`    varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime`    datetime     DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy`      varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime`    datetime     DEFAULT NULL,
  `DeleteBy`      varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime`    datetime     DEFAULT NULL,
  `Status`        varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Sort`          int          DEFAULT 0,
  `Remark`        varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted`     tinyint(1)   NOT NULL DEFAULT 0,
  `IsValid`       int          NOT NULL DEFAULT 1,

  `QueueCode`       varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属队列编码',
  `TaskCode`        varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属任务编码（冗余，免 JOIN）',
  `TaskItemCode`    varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★任务项编码 = 实体层行 Code（cert_expert_nc_item.Code / cert_expert_report_section_item.Code），⛔ 不是结果行 Code',
  `ItemType`        varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT '项类型：nc_check | report_section',
  `ItemCode`        varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '规则/章节业务键（冗余，免 JOIN 定位）',
  `Seq`             int          DEFAULT 0 COMMENT '执行顺序（同标准内串行）',

  `Payload`         json         DEFAULT NULL COMMENT '★执行载荷（规则Code/工作流Code/企业/标准/阶段/版本戳）',
  `ExtractVersionStamp` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL
                    COMMENT '★提取结果版本戳（入队时记录，执行时比对，防文件中途变更）',

  `ItemStatus`      varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'pending'
                    COMMENT '队列项状态：pending | running | completed | failed | skipped | cancelled',
  `ErrorType`       varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '错误分类：retryable | permanent',
  `ErrorMessage`    varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '错误摘要',
  `RetryCount`      int DEFAULT 0 COMMENT '已重试次数',
  `MaxRetryCount`   int DEFAULT 3 COMMENT '最大重试次数',
  `NextRetryAt`     datetime     DEFAULT NULL COMMENT '下次重试时间（指数退避）',

  `LockCode`        varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `LockedUntil`     datetime     DEFAULT NULL COMMENT '锁租约到期（10 分钟 > 单项 LLM 超时上限）',

  `WorkflowExecTaskCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL
                    COMMENT '★关联的执行任务（wf_execution_task.Code），执行后回填，可下钻引擎细节',
  `StartTime`       datetime     DEFAULT NULL,
  `FinishTime`      datetime     DEFAULT NULL,

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`      (`Code`),
  UNIQUE KEY `uk_queue_item`(`QueueCode`, `TaskItemCode`),
  KEY `idx_claim`           (`QueueCode`, `ItemStatus`, `Seq`),
  KEY `idx_task`            (`TaskCode`),
  KEY `idx_taskitem`        (`TaskItemCode`),
  KEY `idx_retry`           (`ItemStatus`, `NextRetryAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='专家任务队列项';


-- ── A5. cert_expert_task_data_gap — 数据缺口 / 补录清单 ──
-- ★ GapKey 生成列表达式 = 25 号裁决 J1（字段级粒度，含 RuleCode），
--   权威：scripts/db/fix/fix-extraction-value-source-20260930.sql §5
CREATE TABLE IF NOT EXISTS `cert_expert_task_data_gap` (
  `Id`           bigint      NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`         varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID，业务键）',
  `OrgCode`      varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★专家工作区编码（租户隔离键）',
  `CreateBy`     varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateName`   varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime`   datetime     DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy`     varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime`   datetime     DEFAULT NULL,
  `DeleteBy`     varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime`   datetime     DEFAULT NULL,
  `Status`       varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Sort`         int          DEFAULT 0,
  `Remark`       varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted`    tinyint(1)   NOT NULL DEFAULT 0,
  `IsValid`      int          NOT NULL DEFAULT 1,

  `TaskCode`        varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属任务编码',
  `SubTaskCode`     varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属标准子任务编码',
  `RuleCode`        varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★提取规则编码（cert_doc_extraction_rule.Code；补录定位主键成分）',
  `EnterpriseCode`  varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '企业编码（冗余）',
  `StageCode`       varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '阶段编码（GUID）',
  `StandardCode`    varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '标准编码',

  `GapType`         varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT '缺口类型：field | table',
  `GapLabel`        varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★展示名（界面直接显示）',
  `FieldCode`       varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '字段编码（GapType=field）',
  `TableCode`       varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '表格定义编码（GapType=table）',
  `StandardFileCode` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '该数据应来自哪个标准文件',
  `ExpectedFileName` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★应上传的文件名（提示专家去补文件）',

  `SourceItemType`  varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT '依赖来源类型：nc_check | report_section',
  `SourceItemCode`  varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★依赖它的规则/章节业务键',
  `SourceItemName`  varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '依赖它的规则/章节名（冗余）',
  `ClauseCode`      varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '关联条款编码',

  `GapStatus`       varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'pending'
                    COMMENT '缺口状态：pending=待处理 | filled=已补录 | skipped=已跳过',
  `FilledValue`     longtext    COLLATE utf8mb4_general_ci COMMENT '补录的值（快照）',
  `FilledBy`        varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `FilledName`      varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `FilledTime`      datetime     DEFAULT NULL,
  `SkipBy`          varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `SkipName`        varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `SkipTime`        datetime     DEFAULT NULL,
  `SkipReason`      varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,

  -- ★★ 去重键：必须是生成列 —— MySQL 索引列【不允许函数表达式】，
  --    且裸 NULL 不参与唯一性判定（NULL != NULL）⇒ 多行 NULL 同时通过
  -- ★ J1 表达式（字段级粒度）：GapType | IFNULL(RuleCode,'-') | IFNULL(FieldCode,'-') | IFNULL(TableCode,'-')
  `GapKey` varchar(300) COLLATE utf8mb4_general_ci GENERATED ALWAYS AS (
      CONCAT(`GapType`, '|', IFNULL(`RuleCode`, '-'), '|', IFNULL(`FieldCode`, '-'), '|', IFNULL(`TableCode`, '-'))
  ) STORED COMMENT '★去重键（25 号 J1：字段级粒度；RuleCode/FieldCode 用 - 占位，避免 NULL 逃逸唯一性判定）',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`      (`Code`),
  UNIQUE KEY `uk_gap`       (`TaskCode`, `GapKey`),
  KEY `idx_task_status`     (`TaskCode`, `GapStatus`),
  KEY `idx_field`           (`FieldCode`),
  KEY `idx_table`           (`TableCode`),
  KEY `idx_sourceitem`      (`SourceItemCode`),
  KEY `idx_subtask`         (`SubTaskCode`),
  KEY `idx_rule`            (`RuleCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='专家任务数据缺口（补录清单）';


-- ============================================================================
-- ③ cert_ai_usage_log —— 同类 CodeFirst 失败实体（SqlSugar decimal 缺陷），本次补建
--    依据：实体 src/certplatform-api/CertPlatform.Admin/Entities/Doc/AiUsageLog.cs
--          + scripts/db/all_tables_ddl.sql（冻结快照）+ 20260921_snake_to_pascal_final_V1.sql
-- ============================================================================
SELECT '========== 2. 重建 cert_ai_usage_log ==========' AS `step`;
CREATE TABLE IF NOT EXISTS `cert_ai_usage_log` (
  `Id`            bigint      NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`          varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID，业务键）',
  `CallId`        varchar(64) COLLATE utf8mb4_general_ci NOT NULL COMMENT '调用唯一ID(GUID)',
  `BusinessType`  varchar(50) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'doc_extraction' COMMENT '业务类型',
  `BusinessRef`   varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务关联（如文件编码）',
  `Skill`         varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '技能名称：analyze/verify',
  `Provider`      varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '模型提供商：qwen/deepseek',
  `Model`         varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '模型名称：qwen-turbo/qwen-plus 等',
  `PromptTokens`  int          NOT NULL DEFAULT 0 COMMENT '输入 token 数',
  `CompletionTokens` int       NOT NULL DEFAULT 0 COMMENT '输出 token 数',
  `TotalTokens`   int          NOT NULL DEFAULT 0 COMMENT '总 token 数',
  `CostUsd`       decimal(10,6) NOT NULL DEFAULT 0.000000 COMMENT '本次费用（美元）',
  `DurationMs`    bigint       NOT NULL DEFAULT 0 COMMENT '耗时（毫秒）',
  `Success`       tinyint(1)   NOT NULL DEFAULT 1 COMMENT '是否成功',
  `ErrorMessage`  varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '失败原因',

  `CreateTime`    datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '调用时间',
  `CreateBy`      varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime`    datetime     DEFAULT NULL,
  `UpdateBy`      varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime`    datetime     DEFAULT NULL,
  `DeleteBy`      varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted`     tinyint(1)   NOT NULL DEFAULT 0,
  `IsValid`       int          NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`        (`Code`),
  UNIQUE KEY `uk_call_id`     (`CallId`),
  KEY `idx_create_time`       (`CreateTime`),
  KEY `idx_model`             (`Model`),
  KEY `idx_success`           (`Success`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='AI 调用日志（用于费用统计）';


-- ============================================================================
-- 验收
-- ============================================================================
SELECT '========== 验收 1：专家表 10 张 + cert_ai_usage_log ==========' AS `step`;
SELECT TABLE_NAME, TABLE_COMMENT
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND (TABLE_NAME LIKE 'cert_expert%' OR TABLE_NAME = 'cert_ai_usage_log')
ORDER BY TABLE_NAME;

SELECT '========== 验收 2：4 张重建表索引 ==========' AS `step`;
SELECT TABLE_NAME, INDEX_NAME, NON_UNIQUE, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX SEPARATOR ',') AS `Cols`
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_expert_nc_item','cert_expert_report_section_item','cert_expert_task_queue_item','cert_expert_task_data_gap')
GROUP BY TABLE_NAME, INDEX_NAME, NON_UNIQUE
ORDER BY TABLE_NAME, INDEX_NAME;

SELECT '========== 验收 3：data_gap.GapKey 生成列（期望 1 行） ==========' AS `step`;
SELECT TABLE_NAME, COLUMN_NAME, GENERATION_EXPRESSION
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cert_expert_task_data_gap'
  AND GENERATION_EXPRESSION <> '';

SELECT '========== 验收 4：★ 铁律九 —— 不得存在 Enable 列 ==========' AS `step`;
SELECT TABLE_NAME, COLUMN_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND (TABLE_NAME LIKE 'cert_expert%' OR TABLE_NAME = 'cert_ai_usage_log')
  AND LOWER(COLUMN_NAME) IN ('enable', 'enabled');
-- 期望 0 行

SELECT '========== 验收 5：★ 铁律七 —— 不得有 snake_case 列 ==========' AS `step`;
SELECT TABLE_NAME, COLUMN_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND (TABLE_NAME LIKE 'cert_expert%' OR TABLE_NAME = 'cert_ai_usage_log')
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin NOT REGEXP '^[A-Z]';
-- 期望 0 行

SELECT '========== 全部完成 ==========' AS `step`;
