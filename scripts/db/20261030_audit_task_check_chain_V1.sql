-- =============================================================================
-- 认证任务 · 检查项结论 · 证据链（V2 专项 — 检查方向优先）
-- 日期：2026-10-30
-- 目的：为独立认证检查方向建表 + 计划表补列
--
-- 背景：43 号（总体设计）· 44 号（数据模型 — 本文依据此定稿）
-- 本文是 44 号第⑨步落地：先建核心表（Task → Result → Evidence → Analysis → ImpactLog + NC + Point）
--
-- ① cert_audit_plan              补 4 列（PlanKind / CoverageMode / AuditYear / PreviousPlanCode）
-- ② cert_task                   任务头（合并 prep/onsite + TaskKind）
-- ③ cert_task_item              任务 × 检查项（执行状态 + RunMode + RerunState）
-- ④ cert_check_result           结论行（检查项 × 条款 + 锁定字段）
-- ⑤ cert_evidence               证据（挂检查项 + MD5 + LlmSummary）
-- ⑥ cert_point                  疑点（多条 + Origin + Fingerprint）
-- ⑦ cert_analysis               分析记录（指纹 + 跳过计数 + 提示词版本）
-- ⑧ cert_impact_log             影响分析日志（MD5 变化追踪）
-- ⑨ cert_nc                     NC 单（独立生命周期）
--
-- ⚠️ V2 与 V1（cert_expert_task 系列）平行运行，不互改
-- ★ 字符集 utf8mb4 + utf8mb4_general_ci
-- ★ 列名 PascalCase（DB / C# / TS 三处一致）
-- ★ 启用列 = IsValid
-- ★ 幂等：可重复执行
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

SELECT '========== 开始：认证任务 · 检查项结论 · 证据链 ==========' AS `step`;


-- ─────────────────────────────────────────────────────────────────────────────
-- ① cert_audit_plan 补 4 列（幂等：按列是否存在判断）
-- ─────────────────────────────────────────────────────────────────────────────

-- PlanKind
SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'cert_audit_plan'
    AND COLUMN_NAME = 'PlanKind'
);
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `cert_audit_plan` ADD COLUMN `PlanKind` VARCHAR(20) NULL DEFAULT NULL COMMENT ''计划类型：stage1 / stage2 / surveillance / recert / special'' AFTER `Standards`',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- CoverageMode
SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'cert_audit_plan'
    AND COLUMN_NAME = 'CoverageMode'
);
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `cert_audit_plan` ADD COLUMN `CoverageMode` VARCHAR(20) NULL DEFAULT NULL COMMENT ''覆盖模式：full / sample / scope_change'' AFTER `PlanKind`',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- AuditYear
SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'cert_audit_plan'
    AND COLUMN_NAME = 'AuditYear'
);
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `cert_audit_plan` ADD COLUMN `AuditYear` INT NULL DEFAULT NULL COMMENT ''审核年度（用于跨年比较与抽查依据）'' AFTER `CoverageMode`',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- PreviousPlanCode
SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'cert_audit_plan'
    AND COLUMN_NAME = 'PreviousPlanCode'
);
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `cert_audit_plan` ADD COLUMN `PreviousPlanCode` VARCHAR(50) NULL DEFAULT NULL COMMENT ''上一年计划 Code（抽查取上年 NC 条款；NULL 表示首年）'' AFTER `AuditYear`',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 补充索引（跨年比较用）
SET @idx_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'cert_audit_plan'
    AND INDEX_NAME = 'idx_audit_year'
);
SET @ddl := IF(@idx_exists = 0,
  'ALTER TABLE `cert_audit_plan` ADD KEY `idx_audit_year` (`AuditYear`, `IsValid`, `IsDeleted`)',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;


-- ─────────────────────────────────────────────────────────────────────────────
-- ② cert_task — 任务头（合并 prep/onsite + TaskKind）
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS `cert_task` (
  `Id`              BIGINT       NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`            VARCHAR(50)  NOT NULL COMMENT '业务键（GUID）',
  `TaskNo`          VARCHAR(50)  NOT NULL COMMENT '展示号（P-20261030-01 / C-20261030-01，自动生成）',

  `TaskKind`        VARCHAR(20)  NOT NULL DEFAULT 'onsite'
                    COMMENT '方向：prep=认证准备 / onsite=认证检查（当前优先 onsite）',
  `PlanCode`        VARCHAR(50)  NOT NULL COMMENT '所属计划（关联 cert_audit_plan.Code）',
  `EnterpriseCode`  VARCHAR(50)  NOT NULL COMMENT '企业编码（快照，便于列表筛选）',
  `StageCode`       VARCHAR(50)           DEFAULT NULL COMMENT '阶段编码（快照）',

  `AuditType`       VARCHAR(20)           DEFAULT NULL COMMENT '仅 onsite：initial / surveillance / recert / special',
  `AuditDateFrom`   DATE                  DEFAULT NULL COMMENT '审核开始日（仅 onsite）',
  `AuditDateTo`     DATE                  DEFAULT NULL COMMENT '审核截止日（仅 onsite）',
  `TeamJson`        TEXT                  DEFAULT NULL COMMENT '审核组（JSON 数组：{code, name, role}；仅 onsite）',

  `Status`          VARCHAR(20)  NOT NULL DEFAULT 'pending'
                    COMMENT 'pending / running / paused / done / partial / failed',
  `ItemTotal`       INT          NOT NULL DEFAULT 0  COMMENT '检查项总数（进度口径）',
  `ItemDone`        INT          NOT NULL DEFAULT 0  COMMENT '已完成项数',
  `ItemFailed`      INT          NOT NULL DEFAULT 0  COMMENT '失败项数',

  `PromptCode`      VARCHAR(50)           DEFAULT NULL COMMENT '本次提示词模板 Code（结果可追溯）',
  `PromptVersion`   VARCHAR(20)           DEFAULT NULL COMMENT '提示词版本号',
  `GuardJson`       TEXT                  DEFAULT NULL COMMENT '启动前校验快照（计划唯一/有条款/有部门/属性覆盖/证据源）',
  `Message`         VARCHAR(500)          DEFAULT NULL COMMENT '汇总消息（⛔ 不许静默跳过）',

  `StartedAt`       DATETIME              DEFAULT NULL COMMENT '启动时间',
  `FinishedAt`      DATETIME              DEFAULT NULL COMMENT '完成时间',

  `Sort`            INT          NOT NULL DEFAULT 0  COMMENT '排序号',
  `Remark`          TEXT                  DEFAULT NULL COMMENT '备注',

  `IsValid`         INT          NOT NULL DEFAULT 1  COMMENT '有效标志',
  `IsDeleted`       TINYINT(1)   NOT NULL DEFAULT 0  COMMENT '软删除标记',
  `CreateTime`      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy`        VARCHAR(64)           DEFAULT NULL COMMENT '创建人 Code',
  `UpdateTime`      DATETIME     NULL     DEFAULT NULL COMMENT '更新时间',
  `UpdateBy`        VARCHAR(64)           DEFAULT NULL COMMENT '更新人 Code',
  `DeleteTime`      DATETIME              DEFAULT NULL COMMENT '删除时间',
  `DeleteBy`        VARCHAR(64)           DEFAULT NULL COMMENT '删除人 Code',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_taskno` (`TaskNo`),
  KEY `idx_plan_code` (`PlanCode`),
  KEY `idx_enterprise_code` (`EnterpriseCode`),
  KEY `idx_status` (`Status`),
  KEY `idx_taskkind` (`TaskKind`),
  KEY `idx_isvalid_deleted` (`IsValid`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='认证任务头（V2 — 合并 prep/onsite + TaskKind；由计划派生）';


-- ─────────────────────────────────────────────────────────────────────────────
-- ③ cert_task_item — 任务 × 检查项（执行状态 + RunMode + RerunState）
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS `cert_task_item` (
  `Id`                  BIGINT       NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`                VARCHAR(50)  NOT NULL COMMENT '业务键（GUID）',

  `TaskCode`            VARCHAR(50)  NOT NULL COMMENT '所属任务（关联 cert_task.Code）',
  `CheckItemCode`       VARCHAR(50)  NOT NULL COMMENT '检查项 Code = cert_audit_plan_detail.Code',

  `Status`              VARCHAR(20)  NOT NULL DEFAULT 'waiting'
                        COMMENT 'waiting / ready / analyzing / analyzed / reviewed / failed',
  `EvidenceState`       VARCHAR(20)  NOT NULL DEFAULT 'none'
                        COMMENT 'none / partial / ready（按方向：prep 看资料证据，onsite 看现场证据）',
  `AnalysisState`       VARCHAR(20)  NOT NULL DEFAULT 'none'
                        COMMENT 'none / done / stale（证据变了就 stale）',

  `AnalyzeCount`        INT          NOT NULL DEFAULT 0  COMMENT '反复执行计数',
  `LastAnalyzedAt`      DATETIME              DEFAULT NULL COMMENT '最近分析时间',
  `EvidenceCount`       INT          NOT NULL DEFAULT 0  COMMENT '本方向可见证据条数',

  `RunMode`             VARCHAR(20)  NOT NULL DEFAULT 'incremental'
                        COMMENT '重跑模式：full / incremental / selective',
  `SkipLocked`          TINYINT(1)   NOT NULL DEFAULT 1  COMMENT '1 = incremental 时跳过 IsLocked=1 的结论项（LLM 节流开关）',

  `RerunState`          VARCHAR(20)  NOT NULL DEFAULT 'none'
                        COMMENT 'none / suggested / forced — 证据变更触发的建议重跑状态',
  `RerunReason`         VARCHAR(1000)         DEFAULT NULL COMMENT '原因列表 JSON：[{evidence_code, kind, old_md5, new_md5, snippet}]',
  `RerunTriggeredAt`    DATETIME              DEFAULT NULL COMMENT '触发时间',
  `RerunTriggeredBy`    VARCHAR(20)           DEFAULT NULL COMMENT '触发来源：evidence_change / doc_reupload / manual',

  `Message`             VARCHAR(500)          DEFAULT NULL COMMENT '失败原因（必填当 failed）或提示信息',

  `Sort`                INT          NOT NULL DEFAULT 0  COMMENT '排序号',
  `Remark`              TEXT                  DEFAULT NULL COMMENT '备注',

  `IsValid`             INT          NOT NULL DEFAULT 1  COMMENT '有效标志',
  `IsDeleted`           TINYINT(1)   NOT NULL DEFAULT 0  COMMENT '软删除标记',
  `CreateTime`          DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy`            VARCHAR(64)           DEFAULT NULL COMMENT '创建人 Code',
  `UpdateTime`          DATETIME     NULL     DEFAULT NULL COMMENT '更新时间',
  `UpdateBy`            VARCHAR(64)           DEFAULT NULL COMMENT '更新人 Code',
  `DeleteTime`          DATETIME              DEFAULT NULL COMMENT '删除时间',
  `DeleteBy`            VARCHAR(64)           DEFAULT NULL COMMENT '删除人 Code',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_task_checkitem` (`TaskCode`, `CheckItemCode`),
  KEY `idx_task_code` (`TaskCode`),
  KEY `idx_check_item_code` (`CheckItemCode`),
  KEY `idx_status` (`Status`),
  KEY `idx_analysis_state` (`AnalysisState`),
  KEY `idx_rerun_state` (`RerunState`),
  KEY `idx_isvalid_deleted` (`IsValid`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='任务 × 检查项（V2 — 执行状态 + RunMode + RerunState + 锁定节流）';


-- ─────────────────────────────────────────────────────────────────────────────
-- ④ cert_check_result — 结论行（检查项 × 条款 + 锁定）
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS `cert_check_result` (
  `Id`                  BIGINT        NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`                VARCHAR(50)   NOT NULL COMMENT '业务键（GUID）',

  `CheckItemCode`       VARCHAR(50)   NOT NULL COMMENT '检查项 Code（关联 cert_audit_plan_detail.Code）',
  `ClauseCode`          VARCHAR(50)   NOT NULL COMMENT '条款 Code（关联 cert_clause_attr_def.Code 或 ISOClause.Code）',
  `StandardCode`        VARCHAR(50)            DEFAULT NULL COMMENT '标准 Code（冗余便于展示）',
  `ClauseNumber`        VARCHAR(50)            DEFAULT NULL COMMENT '条款序号（如 "8.3"；冗余便于展示）',

  -- ═══ 准备组（prep 方向写）═══
  `PrepState`           VARCHAR(20)            DEFAULT NULL COMMENT 'found / partial / missing / unknown / na',
  `MissingKindsJson`    TEXT                   DEFAULT NULL COMMENT '缺的材料（JSON 数组）',
  `ConflictsJson`       TEXT                   DEFAULT NULL COMMENT '跨文档矛盾（JSON 数组）',
  `IsMandatory`         TINYINT(1)             DEFAULT NULL COMMENT '是否必须执行（检查可覆盖，写 IsMandatoryBy）',
  `VerifyLevel`         VARCHAR(20)            DEFAULT NULL COMMENT 'doc / remote / onsite / test（验到什么程度）',
  `PrepNcHint`          TEXT                   DEFAULT NULL COMMENT '准备期"建议开 NC 吗" + 理由（只是给现场的建议）',

  -- ═══ 检查组（check 方向写）═══
  `Conclusion`          VARCHAR(20)            DEFAULT NULL COMMENT 'pass / ofi / nc / na（人填为最终）',
  `NcSuggested`         TINYINT(1)             DEFAULT NULL COMMENT '检查期是否建议开 NC（≠ 已开 NC，见 cert_nc）',
  `Severity`            VARCHAR(20)            DEFAULT NULL COMMENT '实际严重程度：major / minor',
  `SeveritySource`      VARCHAR(10)            DEFAULT NULL COMMENT 'attr / ai / manual',
  `SeverityBaseline`    VARCHAR(20)            DEFAULT NULL COMMENT '条款基线严重程度（prep 可校正）',
  `Reason`              TEXT                   DEFAULT NULL COMMENT '判定依据摘要（必填）',
  `ConclusionBy`        VARCHAR(64)            DEFAULT NULL COMMENT '判定人 Code',
  `ConclusionAt`        DATETIME               DEFAULT NULL COMMENT '判定时间',

  -- ═══ 锁定保护（重跑节流）═══
  `IsLocked`            TINYINT(1)    NOT NULL DEFAULT 0  COMMENT '0/1。1 = 审核员已确认/人工修改，重跑不覆盖（LLM 节流核心）',
  `LockedBy`            VARCHAR(64)            DEFAULT NULL COMMENT '锁定人 Code',
  `LockedAt`            DATETIME               DEFAULT NULL COMMENT '锁定时间',
  `LockReason`          VARCHAR(200)           DEFAULT NULL COMMENT 'manual_confirm / auditor_fixed / other',

  -- ═══ 其他 ═══
  `RollupTo`            TINYINT(1)    NOT NULL DEFAULT 1  COMMENT '该行是否参与检查项汇总（0=na 不参与）',
  `ResultStale`         TINYINT(1)    NOT NULL DEFAULT 0  COMMENT '计划被编辑后置 1（条款/部门变了 → 结论须重跑）',
  `StaleReason`         VARCHAR(200)           DEFAULT NULL COMMENT '导致 stale 的原因：clause_change / dept_change',

  `Sort`                INT           NOT NULL DEFAULT 0  COMMENT '排序号',
  `Remark`              TEXT                   DEFAULT NULL COMMENT '备注',

  `IsValid`             INT           NOT NULL DEFAULT 1  COMMENT '有效标志',
  `IsDeleted`           TINYINT(1)    NOT NULL DEFAULT 0  COMMENT '软删除标记',
  `CreateTime`          DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy`            VARCHAR(64)            DEFAULT NULL COMMENT '创建人 Code',
  `UpdateTime`          DATETIME      NULL     DEFAULT NULL COMMENT '更新时间',
  `UpdateBy`            VARCHAR(64)            DEFAULT NULL COMMENT '更新人 Code',
  `DeleteTime`          DATETIME               DEFAULT NULL COMMENT '删除时间',
  `DeleteBy`            VARCHAR(64)            DEFAULT NULL COMMENT '删除人 Code',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_checkitem_clause` (`CheckItemCode`, `ClauseCode`),
  KEY `idx_check_item_code` (`CheckItemCode`),
  KEY `idx_clause_code` (`ClauseCode`),
  KEY `idx_islocked` (`IsLocked`),
  KEY `idx_conclusion` (`Conclusion`),
  KEY `idx_isvalid_deleted` (`IsValid`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='检查项 × 条款 → 结论（V2 — prep 组 + check 组同行 + IsLocked 锁定节流）';


-- ─────────────────────────────────────────────────────────────────────────────
-- ⑤ cert_evidence — 证据（挂检查项 + MD5 + LlmSummary）
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS `cert_evidence` (
  `Id`                  BIGINT        NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`                VARCHAR(50)   NOT NULL COMMENT '业务键（GUID）',

  `CheckItemCode`       VARCHAR(50)   NOT NULL COMMENT '检查项 Code（关联 cert_audit_plan_detail.Code）',
  `ClauseCode`          VARCHAR(50)            DEFAULT NULL COMMENT '条款 Code（可空 = 检查项级）',

  `CapturedIn`          VARCHAR(20)   NOT NULL DEFAULT 'manual'
                        COMMENT '谁提供的：prep / onsite / manual / doc / pq',
  `CapturedTaskCode`    VARCHAR(50)            DEFAULT NULL COMMENT '采集它的任务 Code（可空 = 导入/手工）',

  `EvidenceKind`        VARCHAR(30)   NOT NULL DEFAULT 'manual'
                        COMMENT 'doc_slice / pq_result / lead / onsite_media / interview / sample / manual',
  `SourceCode`          VARCHAR(50)            DEFAULT NULL COMMENT 'FileCode / PqResultCode / 人工自填',
  `SourceName`          VARCHAR(200)           DEFAULT NULL COMMENT '来源名称（冗余便于展示）',

  `Anchor`              VARCHAR(500)           DEFAULT NULL COMMENT '锚点定位（如页码/段落号）',
  `Snippet`             TEXT                   DEFAULT NULL COMMENT '原文摘录',
  `Confidence`          DECIMAL(5,4)           DEFAULT NULL COMMENT '置信度（0-1；doc_slice 用）',

  -- ═══ LLM 消费字段 ═══
  `ContentMD5`          VARCHAR(32)            DEFAULT NULL COMMENT '证据内容 MD5（Snippet 或 LlmSummary 的 MD5；用于修改检测）',
  `LlmSummary`          TEXT                   DEFAULT NULL COMMENT 'LLM 可消费的标准化文本（OCR 结果 / 转录文本 / 清洗后原文；这是实际喂给模型的内容）',
  `TokenEstimate`       INT                    DEFAULT NULL COMMENT 'LlmSummary 的预估 token 数（聚合截断用）',
  `PrevContentMD5`      VARCHAR(32)            DEFAULT NULL COMMENT '上次内容 MD5（修改检测：≠ 新 MD5 则触发影响分析）',

  -- ═══ 媒体与附件 ═══
  `MediaKind`           VARCHAR(20)            DEFAULT NULL COMMENT 'audio / video / image',
  `DurationSec`         INT                    DEFAULT NULL COMMENT '媒体时长（秒）',
  `TranscriptText`      TEXT                   DEFAULT NULL COMMENT '现场录音/视频的转录文本',
  `AttachmentsJson`      TEXT                   DEFAULT NULL COMMENT '附件 JSON 数组：[{path, type, name, size}]（图片/PDF）',

  -- ═══ 取证信息 ═══
  `CapturedAt`          DATETIME               DEFAULT NULL COMMENT '取证时间',
  `CapturedPlace`       VARCHAR(200)           DEFAULT NULL COMMENT '取证地点',
  `PresentPersons`      TEXT                   DEFAULT NULL COMMENT '在场人 JSON 数组：[{name, role}]',

  `Status`              VARCHAR(20)   NOT NULL DEFAULT 'collected'
                        COMMENT 'collected / confirmed / dropped（dropped 不进分析输入）',
  `DroppedReason`       VARCHAR(500)           DEFAULT NULL COMMENT '废弃原因（status=dropped 时必填）',

  `Sort`                INT           NOT NULL DEFAULT 0  COMMENT '排序号',
  `Remark`              TEXT                   DEFAULT NULL COMMENT '备注',

  `IsValid`             INT           NOT NULL DEFAULT 1  COMMENT '有效标志',
  `IsDeleted`           TINYINT(1)    NOT NULL DEFAULT 0  COMMENT '软删除标记',
  `CreateTime`          DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy`            VARCHAR(64)            DEFAULT NULL COMMENT '创建人 Code',
  `UpdateTime`          DATETIME      NULL     DEFAULT NULL COMMENT '更新时间',
  `UpdateBy`            VARCHAR(64)            DEFAULT NULL COMMENT '更新人 Code',
  `DeleteTime`          DATETIME               DEFAULT NULL COMMENT '删除时间',
  `DeleteBy`            VARCHAR(64)            DEFAULT NULL COMMENT '删除人 Code',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_check_item_code` (`CheckItemCode`, `Status`),
  KEY `idx_clause_code` (`ClauseCode`),
  KEY `idx_captured_in` (`CapturedIn`),
  KEY `idx_evidence_kind` (`EvidenceKind`),
  KEY `idx_source_code` (`SourceCode`),
  KEY `idx_status` (`Status`),
  KEY `idx_isvalid_deleted` (`IsValid`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='证据（V2 — 挂检查项 + MD5 修改检测 + LlmSummary 标准化文本）';


-- ─────────────────────────────────────────────────────────────────────────────
-- ⑥ cert_point — 疑点（多条 + Origin + Fingerprint）
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS `cert_point` (
  `Id`                  BIGINT       NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`                VARCHAR(50)  NOT NULL COMMENT '业务键（GUID）',

  `CheckItemCode`       VARCHAR(50)  NOT NULL COMMENT '检查项 Code（关联 cert_audit_plan_detail.Code）',
  `ClauseCode`          VARCHAR(50)           DEFAULT NULL COMMENT '条款 Code（可空）',
  `DeptCode`            VARCHAR(50)           DEFAULT NULL COMMENT '部门 Code（可空）',

  `Origin`              VARCHAR(20)  NOT NULL DEFAULT 'onsite'
                        COMMENT '来源：prep / onsite / pq / manual — 两方向疑点各自留，互不清除',
  `PointKind`           VARCHAR(20)           DEFAULT NULL COMMENT 'communicate / observe / interview / verify',
  `Mode`                VARCHAR(10)           DEFAULT NULL COMMENT 'remote / onsite',

  `Reason`              TEXT                  DEFAULT NULL COMMENT '疑点原因（为什么列这条疑点）',
  `Action`              TEXT                  DEFAULT NULL COMMENT '建议动作（审核员应该做什么）',
  `Question`            TEXT                  DEFAULT NULL COMMENT '拟提问内容',
  `TargetRole`          VARCHAR(100)          DEFAULT NULL COMMENT '目标角色（如"质量负责人"）',
  `EstMinutes`          INT                   DEFAULT NULL COMMENT '预计耗时（分钟）',
  `Priority`            INT                   NOT NULL DEFAULT 0  COMMENT '优先级（越大越优先）',
  `Fingerprint`         VARCHAR(64)           DEFAULT NULL COMMENT '重跑幂等键（同指纹不重复插入）',

  `Status`              VARCHAR(20)  NOT NULL DEFAULT 'open'
                        COMMENT 'open / resolved / dropped + 理由 + 留痕',
  `ResolveReason`       TEXT                  DEFAULT NULL COMMENT '销账原因',
  `ResolvedBy`          VARCHAR(64)           DEFAULT NULL COMMENT '销账人',
  `ResolvedAt`          DATETIME              DEFAULT NULL COMMENT '销账时间',

  `Sort`                INT          NOT NULL DEFAULT 0  COMMENT '排序号',
  `Remark`              TEXT                  DEFAULT NULL COMMENT '备注',

  `IsValid`             INT          NOT NULL DEFAULT 1  COMMENT '有效标志',
  `IsDeleted`           TINYINT(1)   NOT NULL DEFAULT 0  COMMENT '软删除标记',
  `CreateTime`          DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy`            VARCHAR(64)           DEFAULT NULL COMMENT '创建人 Code',
  `UpdateTime`          DATETIME     NULL     DEFAULT NULL COMMENT '更新时间',
  `UpdateBy`            VARCHAR(64)           DEFAULT NULL COMMENT '更新人 Code',
  `DeleteTime`          DATETIME              DEFAULT NULL COMMENT '删除时间',
  `DeleteBy`            VARCHAR(64)           DEFAULT NULL COMMENT '删除人 Code',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_check_item_code` (`CheckItemCode`),
  KEY `idx_origin` (`Origin`),
  KEY `idx_status` (`Status`),
  KEY `idx_fingerprint` (`Fingerprint`),
  KEY `idx_isvalid_deleted` (`IsValid`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='疑点（V2 — 多条 + Origin 方向 + Fingerprint 幂等）';


-- ─────────────────────────────────────────────────────────────────────────────
-- ⑦ cert_analysis — 分析记录（追加型：指纹 + 跳过计数）
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS `cert_analysis` (
  `Id`                  BIGINT       NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`                VARCHAR(50)  NOT NULL COMMENT '业务键（GUID）',

  `TaskCode`            VARCHAR(50)  NOT NULL COMMENT '关联 cert_task.Code',
  `TaskItemCode`        VARCHAR(50)  NOT NULL COMMENT '关联 cert_task_item.Code',
  `TaskKind`            VARCHAR(20)           DEFAULT NULL COMMENT 'prep / onsite（冗余便于筛选）',

  `EvidenceFingerprint` VARCHAR(64)           DEFAULT NULL COMMENT '本次证据指纹 = MD5(GROUP_CONCAT(CONCAT(e.Code, '':'', e.ContentMD5, '':'', e.Status) ORDER BY e.Code ASC))',
  `LockedSkippedCount`  INT          NOT NULL DEFAULT 0  COMMENT '本次跳过的已锁定结论条数（LLM 节流指标）',
  `EvidenceUsedCount`   INT          NOT NULL DEFAULT 0  COMMENT '本次实际喂给 LLM 的证据条数',

  `PromptCode`          VARCHAR(50)           DEFAULT NULL COMMENT '提示词模板 Code',
  `PromptVersion`       VARCHAR(20)           DEFAULT NULL COMMENT '提示词版本号',
  `ModelName`           VARCHAR(50)           DEFAULT NULL COMMENT '模型名（如 claude-opus-4-6）',

  `InputTokenCount`     INT                   DEFAULT NULL COMMENT '本次分析输入 token 数',
  `OutputTokenCount`    INT                   DEFAULT NULL COMMENT '本次分析输出 token 数',

  `ResultSummaryJson`   TEXT                  DEFAULT NULL COMMENT '产出摘要 JSON：{point_count, nc_suggested_count, prep_state_distribution, conclusion_distribution}',

  `CreatedAt`           DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy`            VARCHAR(64)           DEFAULT NULL COMMENT '创建人 Code / system',
  `DurationMs`          INT                   DEFAULT NULL COMMENT '本次分析耗时（毫秒）',
  `ErrorMessage`        TEXT                  DEFAULT NULL COMMENT '失败原因（若分析失败）',

  `IsValid`             INT          NOT NULL DEFAULT 1  COMMENT '有效标志',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_task_code` (`TaskCode`),
  KEY `idx_task_item_code` (`TaskItemCode`),
  KEY `idx_fingerprint` (`EvidenceFingerprint`),
  KEY `idx_created_at` (`CreatedAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='分析记录（V2 — 追加型：证据指纹 + LockedSkippedCount LLM 节流指标）';


-- ─────────────────────────────────────────────────────────────────────────────
-- ⑧ cert_impact_log — 影响分析日志（MD5 变化追踪）
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS `cert_impact_log` (
  `Id`                    BIGINT       NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`                  VARCHAR(50)  NOT NULL COMMENT '业务键（GUID）',

  `TriggerKind`           VARCHAR(30)  NOT NULL DEFAULT 'doc_reupload'
                          COMMENT '触发类型：doc_reupload / evidence_change / manual',
  `TriggerSource`         VARCHAR(50)  NOT NULL COMMENT '触发来源：FileCode（文档重传）/ EvidenceCode（证据修改）/ 操作 Code',

  `OldMD5`                VARCHAR(32)           DEFAULT NULL COMMENT '变更前 MD5（文档级或证据级，可为空）',
  `NewMD5`                VARCHAR(32)           DEFAULT NULL COMMENT '变更后 MD5（文档级或证据级，可为空）',

  `AffectedCheckItemCount` INT         NOT NULL DEFAULT 0  COMMENT '受影响的检查项数量',
  `AffectedTaskItemCount` INT          NOT NULL DEFAULT 0  COMMENT '受影响的任务项数量',
  `SkippedLockedCount`    INT          NOT NULL DEFAULT 0  COMMENT '因锁定而跳过 LLM 节流的项数',

  `AffectedItemsJson`     TEXT                  DEFAULT NULL COMMENT '受影响项清单 JSON：[{CheckItemCode, TaskItemCode, Action, LockedState}]',

  `CreatedBy`             VARCHAR(64)           DEFAULT NULL COMMENT '操作人 Code',
  `CreatedAt`             DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',

  `IsValid`               INT          NOT NULL DEFAULT 1  COMMENT '有效标志',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_trigger_source` (`TriggerSource`),
  KEY `idx_trigger_kind` (`TriggerKind`),
  KEY `idx_created_at` (`CreatedAt`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='影响分析日志（V2 — 证据/文档 MD5 变化 → 影响检查项追踪）';


-- ─────────────────────────────────────────────────────────────────────────────
-- ⑨ cert_nc — NC 单（独立生命周期）
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS `cert_nc` (
  `Id`                  BIGINT        NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`                VARCHAR(50)   NOT NULL COMMENT '业务键（GUID）',
  `NcNo`                VARCHAR(50)   NOT NULL COMMENT 'NC 编号（NC-20261030-001，报告引用）',

  `CheckItemCode`       VARCHAR(50)   NOT NULL COMMENT '来源检查项 Code',
  `ClauseCode`          VARCHAR(50)            DEFAULT NULL COMMENT '主条款 Code（用于报告定位）',
  `ClauseCodes`         TEXT                   DEFAULT NULL COMMENT '涉及多条条款时用的 JSON 条款 Code 数组（同根因跨条款合并）',

  `Severity`            VARCHAR(20)            DEFAULT NULL COMMENT 'major / minor',
  `FactDesc`            TEXT                   DEFAULT NULL COMMENT '不符合事实描述',
  `RequirementRef`      TEXT                   DEFAULT NULL COMMENT '违反条款原文引用',

  `CorrectiveReq`       TEXT                   DEFAULT NULL COMMENT '整改要求',
  `DueDate`             DATE                   DEFAULT NULL COMMENT '整改截止日期',
  `Responsible`         VARCHAR(200)           DEFAULT NULL COMMENT '责任人',

  `Status`              VARCHAR(20)   NOT NULL DEFAULT 'pending_review'
                        COMMENT 'pending_review / issued / rectifying / verifying / closed / escalated',

  `Origin`              VARCHAR(20)            DEFAULT NULL COMMENT 'ai_suggest / manual（系统只建议，人签发）',
  `IssuedBy`            VARCHAR(64)            DEFAULT NULL COMMENT '签发人 Code',
  `IssuedAt`            DATETIME               DEFAULT NULL COMMENT '签发时间',

  `CreatedInTaskCode`   VARCHAR(50)            DEFAULT NULL COMMENT '哪次检查任务开的（跨年可比较）',

  `CloseEvidenceJson`   TEXT                   DEFAULT NULL COMMENT '关闭证据 JSON：[{evidence_code, desc}]',
  `ClosedBy`            VARCHAR(64)            DEFAULT NULL COMMENT '关闭人 Code',
  `ClosedAt`            DATETIME               DEFAULT NULL COMMENT '关闭时间',

  `Sort`                INT           NOT NULL DEFAULT 0  COMMENT '排序号',
  `Remark`              TEXT                   DEFAULT NULL COMMENT '备注',

  `IsValid`             INT           NOT NULL DEFAULT 1  COMMENT '有效标志',
  `IsDeleted`           TINYINT(1)    NOT NULL DEFAULT 0  COMMENT '软删除标记',
  `CreateTime`          DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy`            VARCHAR(64)            DEFAULT NULL COMMENT '创建人 Code',
  `UpdateTime`          DATETIME      NULL     DEFAULT NULL COMMENT '更新时间',
  `UpdateBy`            VARCHAR(64)            DEFAULT NULL COMMENT '更新人 Code',
  `DeleteTime`          DATETIME               DEFAULT NULL COMMENT '删除时间',
  `DeleteBy`            VARCHAR(64)            DEFAULT NULL COMMENT '删除人 Code',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_nc_no` (`NcNo`),
  KEY `idx_check_item_code` (`CheckItemCode`),
  KEY `idx_clause_code` (`ClauseCode`),
  KEY `idx_status` (`Status`),
  KEY `idx_created_in_task` (`CreatedInTaskCode`),
  KEY `idx_isvalid_deleted` (`IsValid`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='NC 单（V2 — 独立生命周期：AI 建议 → 人签发 → 整改 → 关闭）';


-- ─────────────────────────────────────────────────────────────────────────────
-- ⑩ 自检输出
-- ─────────────────────────────────────────────────────────────────────────────

SELECT '--- cert_audit_plan 新列确认（应含 PlanKind/CoverageMode/AuditYear/PreviousPlanCode）---' AS `check`;
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_audit_plan'
  AND COLUMN_NAME IN ('PlanKind', 'CoverageMode', 'AuditYear', 'PreviousPlanCode');

SELECT '--- V2 新建表确认（应含 8 张表）---' AS `check`;
SELECT TABLE_NAME, TABLE_COMMENT
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN (
    'cert_task',
    'cert_task_item',
    'cert_check_result',
    'cert_evidence',
    'cert_point',
    'cert_analysis',
    'cert_impact_log',
    'cert_nc'
  );

SELECT '--- cert_task 关键列确认 ---' AS `check`;
SELECT COLUMN_NAME, COLUMN_TYPE, COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_task'
  AND COLUMN_NAME IN ('Code', 'TaskNo', 'TaskKind', 'PlanCode', 'PromptCode', 'Status');

SELECT '--- cert_task_item 关键列确认 ---' AS `check`;
SELECT COLUMN_NAME, COLUMN_TYPE, COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_task_item'
  AND COLUMN_NAME IN ('Code', 'TaskCode', 'CheckItemCode', 'RunMode', 'RerunState', 'SkipLocked', 'RerunReason');

SELECT '--- cert_check_result 锁定/结论列确认 ---' AS `check`;
SELECT COLUMN_NAME, COLUMN_TYPE, COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_check_result'
  AND COLUMN_NAME IN ('Code', 'CheckItemCode', 'ClauseCode', 'Conclusion', 'IsLocked', 'LockedBy', 'LockedAt', 'LockReason', 'ResultStale');

SELECT '--- cert_evidence MD5/LLM 列确认 ---' AS `check`;
SELECT COLUMN_NAME, COLUMN_TYPE, COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_evidence'
  AND COLUMN_NAME IN ('Code', 'CheckItemCode', 'CapturedIn', 'EvidenceKind', 'ContentMD5', 'LlmSummary', 'TokenEstimate', 'PrevContentMD5', 'Status', 'TranscriptText', 'AttachmentsJson');

SELECT '--- cert_analysis 指纹相关列确认 ---' AS `check`;
SELECT COLUMN_NAME, COLUMN_TYPE, COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_analysis'
  AND COLUMN_NAME IN ('Code', 'TaskCode', 'TaskItemCode', 'EvidenceFingerprint', 'LockedSkippedCount');

SELECT '--- cert_impact_log 列确认 ---' AS `check`;
SELECT COLUMN_NAME, COLUMN_TYPE, COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_impact_log'
  AND COLUMN_NAME IN ('Code', 'TriggerKind', 'TriggerSource', 'OldMD5', 'NewMD5', 'AffectedCheckItemCount', 'SkippedLockedCount');

SELECT '--- cert_nc 列确认 ---' AS `check`;
SELECT COLUMN_NAME, COLUMN_TYPE, COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_nc'
  AND COLUMN_NAME IN ('Code', 'NcNo', 'CheckItemCode', 'ClauseCode', 'Severity', 'Status', 'Origin', 'CreatedInTaskCode');

SELECT '========== 完成 ==========' AS `step`;
