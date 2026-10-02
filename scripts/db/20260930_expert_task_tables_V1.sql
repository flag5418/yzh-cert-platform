-- ============================================================================
-- 20260930_expert_task_tables_V1.sql
-- 专家任务系统 · 建表（A 组任务执行域 5 张 + B 组结果域 4 张 + C 组日志 1 张 = 10 张）
-- 依据：docs/40-实施/专家任务设计/06-数据库设计-V1.md §2.1 ~ §2.7
--       12 号 D01/D02/D11/D27/D30/D31/D36 ｜ 02 号 §六 ｜ 23 号 §四
--
-- ★ 命名铁律（N1 / 铁律七）：业务表 cert_ 前缀；DB 列名 = C# 属性名 = TS 字段名 = PascalCase
-- ★ 铁律九：启用唯一字段 = IsValid（int 0/1），⛔ 无 Enable 列
-- ★ 准则 A：定位/关联/传参一律用 Code，⛔ Id 永不进 WHERE
-- ★ 租户隔离：所有表 OrgCode NOT NULL（专家工作区 Code）
--
-- ⚠️ 生成列两处（⛔ 应用层不得赋值，否则 MySQL ERROR 3105）：
--     cert_expert_task.ActiveLockKey   ← D36 业务锁并发兜底
--     cert_expert_task_data_gap.GapKey ← 缺口去重键（索引列不允许函数表达式）
--
-- ⚠️ 实体映射：ActiveLockKey / GapKey 必须带
--     [SugarColumn(IsOnlyIgnoreInsert = true, IsOnlyIgnoreUpdate = true)]
-- ============================================================================

USE `yzh_cert_platform`;
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

SELECT '========== 开始建表：专家任务系统 ==========' AS `step`;


-- ============================================================================
-- A 组 · 任务执行域（5 张）
-- ============================================================================

-- ── A1. cert_expert_task — 任务头 ──────────────────────────────────────────
CREATE TABLE IF NOT EXISTS `cert_expert_task` (
  `Id`               bigint      NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`             varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID，业务键）',
  `OrgCode`          varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★专家工作区编码（租户隔离键，★非认证机构，★禁止为 NULL）',
  `CreateBy`         varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateName`       varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '创建人姓名（冗余，防改名后追溯断裂）',
  `CreateTime`       datetime     DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy`         varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime`       datetime     DEFAULT NULL,
  `DeleteBy`         varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime`       datetime     DEFAULT NULL,
  `Status`           varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务状态（保留）',
  `Sort`             int          DEFAULT 0   COMMENT '排序号',
  `Remark`           varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted`        tinyint(1)   NOT NULL DEFAULT 0,
  `IsValid`          int          NOT NULL DEFAULT 1 COMMENT '有效标志（1=有效，0=无效）',

  `TaskName`         varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★任务名称（D29 专家自定义必填）',
  `TaskNumber`       varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '任务编号 TSK-yyyyMMdd-NNNN（系统生成，仅展示，不作关联键）',
  `TaskType`         varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT '任务类型：NC_CHECK | REPORT_GENERATE',
  `ScopeType`        varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT '范围类型：FULL=全局 | PARTIAL=局部',
  `TaskSource`       varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'NEW'
                     COMMENT '★任务来源（D25）：NEW=全新 | REDO=整体重执行 | PATCH=局部更新',
  `ScopeSnapshot`    json         DEFAULT NULL COMMENT '范围快照（勾选/沿用/排除的 Code 数组），FULL 时为 NULL',

  `EnterpriseCode`   varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '企业编码（cert_enterprise.Code）',
  `EnterpriseName`   varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '企业名称（冗余快照）',

  `StageCode`        varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '阶段编码（★cert_cert_stage.Code，GUID，不是业务码 jd01/03）',
  `StageName`        varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '阶段名称（冗余快照）',

  `StandardCodes`    json         DEFAULT NULL COMMENT '涉及标准 Code 数组（冗余，列表页免 JOIN）',
  `StandardNames`    json         DEFAULT NULL COMMENT '涉及标准名称数组',
  `StandardCount`    int          DEFAULT 0   COMMENT '涉及标准数',

  `ExecStatus`       varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'draft'
                     COMMENT '★第1层 执行状态（机器管）：draft | pending_run | running | completed | failed',
  `ReviewStatus`     varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'not_started'
                     COMMENT '★第2层 结果状态（专家管）：not_started | pending_review | reviewed | modified | skipped | frozen。⚠️ 仅保留字段，界面不展示（结论级复核状态在结果表）',
  `LifecycleStatus`  varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'active'
                     COMMENT '★第3层 存续状态（管理控制）：active | archived | cancelled | voided',
  `AuditEventCode`   varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '预留：认证周期审核事件（D26 本期不建表，仅留字段）',
  `AuditEventName`   varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '预留：事件名（如 SV-1 第一次监督审核）',

  `TotalItemCount`   int          DEFAULT 0   COMMENT '本轮涉及的任务项总数',
  `AckedCount`       int          DEFAULT 0   COMMENT '已认可数（reviewed）',
  `ModifiedCount`    int          DEFAULT 0   COMMENT '已修改数（modified）',
  `SkippedCount`     int          DEFAULT 0   COMMENT '已跳过数（skipped）',
  `PendingCount`     int          DEFAULT 0   COMMENT '待认可数（pending_review）',
  `FailedCount`      int          DEFAULT 0   COMMENT '执行失败数',
  `GapCount`         int          DEFAULT 0   COMMENT '未处理数据缺口数',
  `Progress`         decimal(5,2) DEFAULT 0.00 COMMENT '执行进度 %（队列执行进度，非认可进度）',

  `SkipAllGaps`      tinyint(1)   NOT NULL DEFAULT 0 COMMENT '专家是否点了「跳过全部补录」',
  `SkipAllGapsTime`  datetime     DEFAULT NULL,

  `SubmitTime`       datetime     DEFAULT NULL COMMENT '提交执行时间',
  `FinishTime`       datetime     DEFAULT NULL COMMENT '全部队列结束时间',

  `ActiveLockKey`    varchar(200) COLLATE utf8mb4_general_ci
                     GENERATED ALWAYS AS (
                       IF(`LifecycleStatus` = 'active' AND `ExecStatus` <> 'completed',
                          CONCAT_WS('|', `OrgCode`, `EnterpriseCode`, `StageCode`, `TaskType`),
                          NULL)
                     ) STORED
                     COMMENT '★业务锁键（D36；锁键=Org|Ent|Stage|TaskType；已结束=NULL）。唯一索引允许多个 NULL ⇒ 已结束任务不冲突',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`          (`Code`),
  UNIQUE KEY `uk_org_tasknum`   (`OrgCode`, `TaskNumber`),
  UNIQUE KEY `uk_active_lock`   (`ActiveLockKey`),
  KEY `idx_bizlock`             (`OrgCode`, `EnterpriseCode`, `StageCode`, `TaskType`, `LifecycleStatus`, `ExecStatus`, `IsDeleted`),
  KEY `idx_ent_list`            (`OrgCode`, `EnterpriseCode`, `CreateTime`),
  KEY `idx_status_list`         (`OrgCode`, `ExecStatus`, `CreateTime`),
  KEY `idx_type_list`           (`OrgCode`, `TaskType`, `CreateTime`),
  KEY `idx_stage_code`          (`StageCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='专家任务（NC检查/报告生成）';


-- ── A2. cert_expert_task_standard — 标准子任务（1 任务 → N 子任务 → N 队列） ──
CREATE TABLE IF NOT EXISTS `cert_expert_task_standard` (
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
  `Sort`          int          DEFAULT 0   COMMENT '排序号',
  `Remark`        varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted`     tinyint(1)   NOT NULL DEFAULT 0,
  `IsValid`       int          NOT NULL DEFAULT 1,

  `TaskCode`      varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属任务编码',
  `TaskNumber`    varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '任务编号（冗余，列表免 JOIN）',
  `StandardCode`  varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '标准编码（cert_iso_standard.Code）',
  `StandardName`  varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '标准名称（冗余快照）',

  `ExecStatus`    varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'draft'
                  COMMENT '★执行状态（与任务头同枚举）',
  `LifecycleStatus` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'active'
                  COMMENT '★存续状态：active | archived | cancelled | voided',
  `QueueCode`     varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '队列编码（1子任务=1队列）',

  `ItemCount`     int DEFAULT 0 COMMENT '任务项总数',
  `DoneCount`     int DEFAULT 0 COMMENT '已执行完（含失败/跳过）',
  `AckedCount`    int DEFAULT 0 COMMENT '已认可数',
  `ModifiedCount` int DEFAULT 0 COMMENT '已修改数',
  `SkippedCount`  int DEFAULT 0 COMMENT '已跳过数',
  `PendingCount`  int DEFAULT 0 COMMENT '待认可数',
  `FailedCount`   int DEFAULT 0 COMMENT '执行失败数',
  `GapCount`      int DEFAULT 0 COMMENT '未处理缺口数',
  `Progress`      decimal(5,2) DEFAULT 0.00 COMMENT '执行进度 %',

  `StartTime`     datetime DEFAULT NULL,
  `FinishTime`    datetime DEFAULT NULL,
  `LastError`     varchar(1000) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '最后一次错误摘要',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`      (`Code`),
  UNIQUE KEY `uk_task_std`  (`TaskCode`, `StandardCode`),
  KEY `idx_task`            (`TaskCode`, `Sort`),
  KEY `idx_queue`           (`QueueCode`),
  KEY `idx_standard`        (`OrgCode`, `StandardCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='专家任务-标准子任务';


-- ── A3. cert_expert_task_queue — 队列头（1 子任务 = 1 队列） ──
CREATE TABLE IF NOT EXISTS `cert_expert_task_queue` (
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
  `Status`        varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务状态（保留）',
  `Sort`          int          DEFAULT 0,
  `Remark`        varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted`     tinyint(1)   NOT NULL DEFAULT 0,
  `IsValid`       int          NOT NULL DEFAULT 1,

  `TaskCode`      varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属任务编码',
  `SubTaskCode`   varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属标准子任务编码',
  `StandardCode`  varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '标准编码',
  `QueueType`     varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT '队列类型：nc_check | report_generate',
  `ScopeKey`      varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★互斥键，单一口径 = SubTaskCode',

  `QueueStatus`   varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'pending'
                  COMMENT '队列状态：pending | running | completed | failed | cancelled',

  `TotalCount`    int DEFAULT 0 COMMENT '总项数',
  `DoneCount`     int DEFAULT 0 COMMENT '已完成数',
  `FailedCount`   int DEFAULT 0 COMMENT '失败数',
  `SkippedCount`  int DEFAULT 0 COMMENT '跳过数',
  `Progress`      decimal(5,2) DEFAULT 0.00 COMMENT '进度 %',

  `Priority`      int DEFAULT 0 COMMENT '优先级（越大越先）',
  `LockCode`      varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '当前持有者（worker 标识）',
  `LockedUntil`   datetime     DEFAULT NULL COMMENT '锁租约到期',
  `RetryCount`    int DEFAULT 0 COMMENT '队列级重试次数',
  `MaxRetryCount` int DEFAULT 3 COMMENT '队列级最大重试',

  `StartTime`     datetime DEFAULT NULL,
  `FinishTime`    datetime DEFAULT NULL,
  `LastError`     varchar(1000) COLLATE utf8mb4_general_ci DEFAULT NULL,

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`        (`Code`),
  UNIQUE KEY `uk_subtask`     (`SubTaskCode`),
  KEY `idx_claim`             (`QueueStatus`, `LockedUntil`, `Priority`, `CreateTime`),
  KEY `idx_task`              (`TaskCode`),
  KEY `idx_org_list`          (`OrgCode`, `QueueStatus`, `CreateTime`),
  KEY `idx_scopekey`          (`ScopeKey`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='专家任务队列（头）';


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
  `GapKey` varchar(300) COLLATE utf8mb4_general_ci GENERATED ALWAYS AS (
      CONCAT(`GapType`, '|', IFNULL(`FieldCode`, '-'), '|', IFNULL(`TableCode`, '-'), '|', `SourceItemCode`)
  ) STORED COMMENT '★去重键（NULL 用 - 占位，保证参与唯一性判定）',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`      (`Code`),
  UNIQUE KEY `uk_gap`       (`TaskCode`, `GapKey`),
  KEY `idx_task_status`     (`TaskCode`, `GapStatus`),
  KEY `idx_field`           (`FieldCode`),
  KEY `idx_table`           (`TableCode`),
  KEY `idx_sourceitem`      (`SourceItemCode`),
  KEY `idx_subtask`         (`SubTaskCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='专家任务数据缺口（补录清单）';


-- ============================================================================
-- B 组 · 结果域（4 张）—— 实体层（长期）+ 结果层（多轮）
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


-- ── B2. cert_expert_nc_result — NC 结果（多轮） ──
CREATE TABLE IF NOT EXISTS `cert_expert_nc_result` (
  `Id`         bigint      NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`       varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID，业务键）',
  `OrgCode`    varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★专家工作区编码',
  `CreateBy`   varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateName` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime     DEFAULT CURRENT_TIMESTAMP COMMENT '★本轮生成时间',
  `UpdateBy`   varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime     DEFAULT NULL,
  `DeleteBy`   varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime     DEFAULT NULL,
  `Status`     varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Sort`       int          DEFAULT 0,
  `Remark`     varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted`  tinyint(1)   NOT NULL DEFAULT 0,
  `IsValid`    int          NOT NULL DEFAULT 1,

  `ItemCode`      varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★所属检查项（cert_expert_nc_item.Code）',
  `EnterpriseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '企业编码（冗余，免 JOIN）',
  `StageCode`     varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '阶段编码',
  `StandardCode`  varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '标准编码',
  `RoundNo`       int          NOT NULL DEFAULT 1 COMMENT '★业务轮次（与引擎 Attempt 独立编号）',
  `TaskCode`      varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★产生本轮的任务；沿用轮次为空',

  `AutoStatus`      varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'none'
                    COMMENT '自动结果：none | ok | ng | skipped | failed',
  `AutoResult`      json        DEFAULT NULL COMMENT '★业务侧结论快照（原始 prompt/输出在 wf_node_execution）',
  `AutoSeverity`    varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '自动判定的严重度',
  `AutoDescription` longtext    COLLATE utf8mb4_general_ci COMMENT '自动判定的说明原文',
  `AutoConfidence`  decimal(3,2) DEFAULT NULL COMMENT 'AI 置信度（0.00-1.00）',
  `ExecutionTaskCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★引擎执行任务（wf_execution_task.Code），可下钻溯源',
  `AutoEvaluatedAt` datetime    DEFAULT NULL,

  `SkipCategory` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL
                 COMMENT '跳过分类：data_gap | data_gap_skipped | no_rule | rule_disabled | manual_mode | exec_failed',
  `SkipReason`   varchar(1000) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★跳过原因（界面必须完整展示）',

  `Conformity`   varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★判定：conform | nonconform | observation | na',
  `Severity`     varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★严重度：major | minor | observation',
  `ContentText`  longtext    COLLATE utf8mb4_general_ci COMMENT '不符合描述',
  `EvidenceRef`  text        COLLATE utf8mb4_general_ci COMMENT '★客观证据引用',

  `ReviewStatus` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'not_started'
                 COMMENT '★not_started | pending_review | reviewed | modified | skipped | frozen',
  `IsModified`   tinyint(1)   NOT NULL DEFAULT 0 COMMENT '★是否被专家改过（导出「结论来源」列用）',
  `ReviewBy`     varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ReviewName`   varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ReviewTime`   datetime     DEFAULT NULL,
  `ReviewRemark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,

  `NcCode`  varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★生成的 NC（cert_nc.Code）',
  `NcCount` int          DEFAULT 0,

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`      (`Code`),
  UNIQUE KEY `uk_item_round` (`ItemCode`, `RoundNo`),
  KEY `idx_item`           (`ItemCode`, `RoundNo` DESC),
  KEY `idx_task`           (`TaskCode`),
  KEY `idx_review`         (`ReviewStatus`),
  KEY `idx_nc`             (`NcCode`),
  KEY `idx_exec`           (`ExecutionTaskCode`),
  KEY `idx_scope_time`     (`OrgCode`, `EnterpriseCode`, `StageCode`, `StandardCode`, `CreateTime`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='★专家任务-NC检查结果（多轮）';


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


-- ── B4. cert_expert_report_result — 报告章节结果（多轮） ──
CREATE TABLE IF NOT EXISTS `cert_expert_report_result` (
  `Id`         bigint      NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`       varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID，业务键）',
  `OrgCode`    varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `CreateBy`   varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateName` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime     DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy`   varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime     DEFAULT NULL,
  `DeleteBy`   varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime     DEFAULT NULL,
  `Status`     varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Sort`       int          DEFAULT 0,
  `Remark`     varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted`  tinyint(1)   NOT NULL DEFAULT 0,
  `IsValid`    int          NOT NULL DEFAULT 1,

  `ItemCode`       varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★所属章节实体',
  `EnterpriseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `StageCode`      varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `StandardCode`   varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `RoundNo`        int          NOT NULL DEFAULT 1 COMMENT '★业务轮次',
  `TaskCode`       varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★产生本轮的任务；沿用轮次为空',

  `AutoStatus`      varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'none'
                    COMMENT '自动结果：none | ok | skipped | failed | degraded（章节未配DAG，用模板示例）',
  `AutoResult`      json        DEFAULT NULL COMMENT '业务侧结论快照',
  `AutoContent`     longtext    COLLATE utf8mb4_general_ci COMMENT '★AI 生成的章节正文',
  `AutoConfidence`  decimal(3,2) DEFAULT NULL COMMENT 'AI 置信度（<0.5 时界面黄色提示）',
  `ExecutionTaskCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★引擎执行任务（可下钻）',
  `AutoEvaluatedAt` datetime    DEFAULT NULL,
  `SkipCategory`    varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `SkipReason`      varchar(1000) COLLATE utf8mb4_general_ci DEFAULT NULL,

  `ContentText`    longtext    COLLATE utf8mb4_general_ci COMMENT '★章节正文（纯文本，★前端 HTML 渲染须先转义）',
  `ContentFormat`  varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'plain' COMMENT '正文格式：plain（D19，不用富文本）',
  `InheritFromCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★本轮沿用了哪一轮（沿用机制，D30）',

  `ReviewStatus` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'not_started'
                 COMMENT '★not_started | pending_review | reviewed | modified | skipped | frozen',
  `IsModified`   tinyint(1)   NOT NULL DEFAULT 0,
  `ReviewBy`     varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ReviewName`   varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ReviewTime`   datetime     DEFAULT NULL,
  `ReviewRemark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`      (`Code`),
  UNIQUE KEY `uk_item_round` (`ItemCode`, `RoundNo`),
  KEY `idx_item`           (`ItemCode`, `RoundNo` DESC),
  KEY `idx_task`           (`TaskCode`),
  KEY `idx_review`         (`ReviewStatus`),
  KEY `idx_exec`           (`ExecutionTaskCode`),
  KEY `idx_scope_time`     (`OrgCode`, `EnterpriseCode`, `StageCode`, `StandardCode`, `CreateTime`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='★专家任务-报告章节结果（多轮）';


-- ============================================================================
-- C 组 · 日志域（1 张）—— ★ 只允许 INSERT / SELECT，⛔ 禁止 UPDATE / DELETE
-- ============================================================================

-- ── C1. cert_expert_task_log — 运行 / 认可 / 修改日志 ──
CREATE TABLE IF NOT EXISTS `cert_expert_task_log` (
  `Id`            bigint      NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`          varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID，业务键）',
  `OrgCode`       varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '★专家工作区编码（租户隔离键）',
  `CreateBy`      varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateName`    varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime`    datetime     DEFAULT CURRENT_TIMESTAMP COMMENT '★=OperateTime，冗余便于按列排序',
  `UpdateBy`      varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★恒为 NULL（日志不可变）',
  `UpdateTime`    datetime     DEFAULT NULL COMMENT '★恒为 NULL',
  `DeleteBy`      varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★恒为 NULL',
  `DeleteTime`    datetime     DEFAULT NULL COMMENT '★恒为 NULL',
  `Status`        varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Sort`          int          DEFAULT 0,
  `Remark`        varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted`     tinyint(1)   NOT NULL DEFAULT 0 COMMENT '★恒为 0（日志不删）',
  `IsValid`       int          NOT NULL DEFAULT 1,

  `TaskCode`        varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '所属任务编码',
  `SubTaskCode`     varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '所属标准子任务编码',
  `QueueCode`       varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★所属队列编码（23 号 §四 埋点需要）',
  `TaskItemCode`    varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★任务项编码；NULL=任务级动作',
  `ItemType`        varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT 'nc_check | report_section | NULL',
  `StandardCode`    varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '标准编码（冗余）',
  `ItemName`        varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '项名称（冗余快照）',

  `LogAction`       varchar(40) COLLATE utf8mb4_general_ci NOT NULL
                    COMMENT '动作：task.created | task.lock.acquired | task.submitted | task.progress | task.paused | task.resumed | task.finished | task.cancelled | task.archived | scope.resolved | item.derived | queue.created | queue.started | queue.paused | queue.progress | queue.finished | item.extract.ok | item.extract.retry | item.extract.fail | item.judge.ok | item.judge.nc | item.skip | gap.created | AUTO_GENERATE | AUTO_FAIL | SKIP | ACKNOWLEDGE | MODIFY | CREATE | SUBMIT | APPROVE | CANCEL | RERUN | SKIP_ALL_GAPS | EXPORT',
  `LogLevel`        varchar(10) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'info'
                    COMMENT '★日志级别（23 号 §四）：info | warn | error',
  `FieldName`       varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '改的字段名（MODIFY 逐字段）',
  `FieldLabel`      varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '字段中文名',
  `OldValue`        text        COLLATE utf8mb4_general_ci COMMENT '旧值（标量/短文本）',
  `NewValue`        text        COLLATE utf8mb4_general_ci COMMENT '新值（标量/短文本）',
  `OldValueText`    longtext    COLLATE utf8mb4_general_ci COMMENT '★旧值（长文本，章节正文用）',
  `NewValueText`    longtext    COLLATE utf8mb4_general_ci COMMENT '★新值（长文本，章节正文用）',

  `BeforeStatus`    varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '改前 ReviewStatus',
  `AfterStatus`     varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '改后 ReviewStatus',
  `SkipCategory`    varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '跳过分类（LogAction=SKIP 时）',
  `Confidence`      decimal(3,2) DEFAULT NULL COMMENT 'AI 置信度（记录自动结果时）',

  `Message`         varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★人读消息（23 号 §四）',
  `Payload`         json          DEFAULT NULL COMMENT '★结构化载荷（关键入参，⛔ 不存文件内容）',
  `DurationMs`      int           DEFAULT NULL COMMENT '★耗时毫秒（任务/队列/项级均记录）',

  `IsAutoResult`    tinyint(1)   NOT NULL DEFAULT 0 COMMENT '针对的是否自动生成的结果',
  `OperatorCode`    varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `OperatorName`    varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '★操作人姓名（冗余）',
  `OperateTime`     datetime     NOT NULL COMMENT '操作时间',
  `ClientIp`        varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '客户端IP（可选）',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`    (`Code`),
  KEY `idx_task`          (`TaskCode`, `CreateTime`),
  KEY `idx_queue`         (`QueueCode`, `CreateTime`),
  KEY `idx_item`          (`TaskItemCode`, `CreateTime`),
  KEY `idx_operator`      (`OperatorCode`, `OperateTime`),
  KEY `idx_action`        (`LogAction`, `CreateTime`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='专家任务日志（运行/认可/修改，★不可变）';


-- ============================================================================
-- 验收
-- ============================================================================
SELECT '========== 验收 1：表清单（期望 10 张） ==========' AS `step`;
SELECT TABLE_NAME, TABLE_COMMENT
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME LIKE 'cert_expert%'
ORDER BY TABLE_NAME;

SELECT '========== 验收 2：生成列（期望 2 个） ==========' AS `step`;
SELECT TABLE_NAME, COLUMN_NAME, GENERATION_EXPRESSION
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME LIKE 'cert_expert%'
  AND GENERATION_EXPRESSION <> '';

SELECT '========== 验收 3：业务锁唯一键（期望 1 行） ==========' AS `step`;
SELECT TABLE_NAME, INDEX_NAME, NON_UNIQUE, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) AS `列`
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cert_expert_task'
  AND INDEX_NAME = 'uk_active_lock'
GROUP BY TABLE_NAME, INDEX_NAME, NON_UNIQUE;

SELECT '========== 验收 4：★ 铁律九 —— 全库不得存在 Enable 列 ==========' AS `step`;
SELECT TABLE_NAME, COLUMN_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME LIKE 'cert_expert%'
  AND LOWER(COLUMN_NAME) IN ('enable', 'enabled');
-- 期望 0 行

SELECT '========== 验收 5：★ 铁律七 —— 新表不得有 snake_case 列 ==========' AS `step`;
SELECT TABLE_NAME, COLUMN_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME LIKE 'cert_expert%'
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin NOT REGEXP '^[A-Z]';
-- 期望 0 行

SELECT '========== 验收 6：OrgCode 全 NOT NULL（期望 0 行可空） ==========' AS `step`;
SELECT TABLE_NAME, COLUMN_NAME, IS_NULLABLE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME LIKE 'cert_expert%'
  AND COLUMN_NAME = 'OrgCode' AND IS_NULLABLE = 'YES';
-- 期望 0 行

SELECT '========== 建表完成 ==========' AS `step`;
