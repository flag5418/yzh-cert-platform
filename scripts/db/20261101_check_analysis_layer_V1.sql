-- =============================================================================
-- 认证准备 / 认证检查 · 分析层落地（V2 专项 — 五层数据模型）
-- 日期：2026-11-01
-- 依据：docs/70-V2设计/46-认证准备与认证检查-执行链路设计-V1.md
--       §4.5 入场分析独立表 · §10 锁定三道闸门
--
-- 背景（为什么必须改）：
--   ① cert_check_result 唯一键 uk_checkitem_clause (CheckItemCode, ClauseCode)
--      ⇒ 一个检查项在一个条款上只能有一行 ⇒ 准备分支与检查分支只能挤同一行（"一行两段"）
--      ⇒ 谁后跑谁覆盖，重跑必然互相踩（设计文档 F13）
--   ② 证据只挂 CheckItemCode，没有"这次分析用了哪几条证据"的记录 ⇒ 报告无法冻结证据链（F14）
--   ③ 锁挂在结果行上 ⇒ 一个检查项 3 条款要锁 3 次，且重跑后锁跟着旧行变成历史（F15）
--
-- 本脚本做七件事：
--   ① 存量探测 + 备份（cert_check_result 若有行 → 先整表备份，绝不静默丢数据）
--   ② 新建 cert_check_analysis            入场分析（一次分析一行 · 分支隔离）
--   ③ 新建 cert_check_analysis_evidence   分析 × 证据（证据链 + 内容快照）
--   ④ 新建 cert_check_lock                检查项锁（检查项级 · 与分支正交）
--   ⑤ 改造 cert_check_result              唯一键改为 (AnalysisCode, ClauseCode)，移出锁 4 列
--   ⑥ cert_point 加 AnalysisCode          疑点归属到"哪一次分析"
--   ⑦ cert_task 加 ItemSkipped            全锁场景的进度口径（§10.3）
--
-- ★ 字符集 utf8mb4 / utf8mb4_general_ci（与既有 cert_* 一致）
-- ★ 列名 PascalCase（DB / C# / TS 三处一致）
-- ★ 启用列 = IsValid；软删除 = IsDeleted
-- ★ 幂等：可重复执行（建表 IF NOT EXISTS，改列/改索引先查 INFORMATION_SCHEMA）
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

SELECT '========== 开始：认证准备/检查 分析层落地 V1 ==========' AS `step`;


-- ─────────────────────────────────────────────────────────────────────────────
-- ① 存量探测 + 备份
--    设计文档 §4.5.8：这批表 2026-10-30 才建，预期行数 < 50。
--    ⛔ 但脚本不许自作主张清空 —— 有行就先备份，再继续。
-- ─────────────────────────────────────────────────────────────────────────────

SET @rows_before := (SELECT COUNT(*) FROM `cert_check_result`);
SELECT CONCAT('cert_check_result 存量行数 = ', @rows_before) AS `step`;

-- 有存量才备份（备份表已存在则跳过，避免重复覆盖）
SET @bak_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_check_result_bak_20261101'
);
SET @ddl := IF(@rows_before > 0 AND @bak_exists = 0,
  'CREATE TABLE `cert_check_result_bak_20261101` AS SELECT * FROM `cert_check_result`',
  'SELECT ''无需备份（表为空或备份已存在）'' AS `skip`');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;


-- ─────────────────────────────────────────────────────────────────────────────
-- ② cert_check_analysis — ★ 入场分析（一次分析一行 · 分支隔离）
--
--    "入场分析是独立的表，因为入场分析包含 原始证据 + 疑点 + 审核员证据，
--     而且要区分哪些是认证准备的、哪些是认证检查的"
--
--    一行 = 「某检查项 × 某分支 × 第 N 次分析」这个容器本身。
--    结果行（cert_check_result）挂在它下面；证据链（_evidence）挂在它下面；疑点（cert_point）挂在它下面。
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS `cert_check_analysis` (
  `Id`                   BIGINT       NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`                 VARCHAR(50)  NOT NULL COMMENT '业务键（GUID）',

  `CheckItemCode`        VARCHAR(50)  NOT NULL COMMENT '检查项 Code（= cert_audit_plan_detail.Code）',
  `StageKind`            VARCHAR(20)  NOT NULL COMMENT '分支：prep=认证准备 / onsite=认证检查',

  `EnterpriseCode`       VARCHAR(50)           DEFAULT NULL COMMENT '企业编码（冗余，免 join）',
  `PlanCode`             VARCHAR(50)           DEFAULT NULL COMMENT '计划 Code（冗余，免 join）',
  `TaskCode`             VARCHAR(50)           DEFAULT NULL COMMENT '哪次任务跑的（关联 cert_task.Code；NULL=手工发起）',
  `TaskItemCode`         VARCHAR(50)           DEFAULT NULL COMMENT '任务项 Code（关联 cert_task_item.Code）',

  -- ★ 唯一键技巧：当前行 = 1，历史行 = NULL（MySQL 唯一索引允许多 NULL）
  --   ⛔ 不要用 0 表历史：那样 (item, prep, 0) 只能有一条，重跑两次就插入失败（设计文档 R10）
  `IsCurrent`            TINYINT               DEFAULT NULL COMMENT '1=当前生效；NULL=历史（⛔ 不要用 0）',
  `RunNo`                INT          NOT NULL DEFAULT 1 COMMENT '该 (检查项, 分支) 的第几次分析，从 1 起',

  `Status`               VARCHAR(20)  NOT NULL DEFAULT 'running'
                         COMMENT 'running / done / failed / stale（stale=锁定项证据变更，见 §10.4）',

  `PromptType`           VARCHAR(50)           DEFAULT NULL COMMENT '用的提示词类型：audit_prep / audit_check',
  `PromptCode`           VARCHAR(50)           DEFAULT NULL COMMENT '提示词模板 Code（可追溯）',
  `PromptVersion`        VARCHAR(20)           DEFAULT NULL COMMENT '提示词版本号',
  `ModelName`            VARCHAR(100)          DEFAULT NULL COMMENT '模型名',

  `EvidenceFingerprint`  VARCHAR(64)           DEFAULT NULL COMMENT '输入证据指纹（判定"证据变没变"；只用于过期告警，不用于跳过）',
  `InputSnapshotJson`    LONGTEXT              DEFAULT NULL COMMENT '★ 装配包快照（条款+属性+摘录+现场证据），回放用',
  `ResultSummaryJson`    TEXT                  DEFAULT NULL COMMENT '产出摘要（几条结果 / 几条疑点 / 状态分布）',

  `AnalyzedAt`           DATETIME              DEFAULT NULL COMMENT '分析完成时间',
  `DurationMs`           INT                   DEFAULT NULL COMMENT '耗时（毫秒）',
  `InputTokenCount`      INT                   DEFAULT NULL COMMENT '输入 token',
  `OutputTokenCount`     INT                   DEFAULT NULL COMMENT '输出 token',
  `ErrorMessage`         TEXT                  DEFAULT NULL COMMENT '失败原因（Status=failed 时必填）',

  `Sort`                 INT          NOT NULL DEFAULT 0  COMMENT '排序号',
  `Remark`               TEXT                  DEFAULT NULL COMMENT '备注',
  `IsValid`              INT          NOT NULL DEFAULT 1  COMMENT '有效标志',
  `IsDeleted`            TINYINT(1)   NOT NULL DEFAULT 0  COMMENT '软删除标记',
  `CreateTime`           DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy`             VARCHAR(64)           DEFAULT NULL COMMENT '创建人 Code',
  `UpdateTime`           DATETIME              DEFAULT NULL COMMENT '更新时间',
  `UpdateBy`             VARCHAR(64)           DEFAULT NULL COMMENT '更新人 Code',
  `DeleteTime`           DATETIME              DEFAULT NULL COMMENT '删除时间',
  `DeleteBy`             VARCHAR(64)           DEFAULT NULL COMMENT '删除人 Code',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  -- ★ 一个 (检查项, 分支) 只能有一条当前行；历史行 IsCurrent=NULL 可无限多条
  UNIQUE KEY `uk_current` (`CheckItemCode`, `StageKind`, `IsCurrent`),
  KEY `idx_item_stage_runs` (`CheckItemCode`, `StageKind`, `RunNo`),
  KEY `idx_task` (`TaskCode`),
  KEY `idx_plan_enterprise` (`PlanCode`, `EnterpriseCode`),
  KEY `idx_status` (`Status`),
  KEY `idx_isvalid_deleted` (`IsValid`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='入场分析（一次分析一行 · StageKind 区分准备/检查 · IsCurrent 当前生效）';


-- ─────────────────────────────────────────────────────────────────────────────
-- ③ cert_check_analysis_evidence — 分析 × 证据（证据链 + 内容快照）
--
--    为什么必须有快照：证据是资产、会被改（PrevContentMD5 机制就是干这个的）。
--    报告三个月后要能回答"当时凭什么判的"——只存 EvidenceCode 不够，必须存当时的内容。
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS `cert_check_analysis_evidence` (
  `Id`                    BIGINT       NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`                  VARCHAR(50)  NOT NULL COMMENT '业务键（GUID）',

  `AnalysisCode`          VARCHAR(50)  NOT NULL COMMENT '所属分析（关联 cert_check_analysis.Code）',
  `EvidenceCode`          VARCHAR(50)  NOT NULL COMMENT '证据 Code（关联 cert_evidence.Code）',

  `UsedInPrompt`          TINYINT(1)   NOT NULL DEFAULT 1 COMMENT '0/1：是否真的喂给了模型（Status=dropped 的不喂，但保留关联）',
  `SnippetSnapshot`       TEXT                  DEFAULT NULL COMMENT '★ 分析当时该证据的 LlmSummary 快照',
  `ContentMD5Snapshot`    VARCHAR(64)           DEFAULT NULL COMMENT '★ 分析当时的内容 MD5（证明"当时用的是这版"）',
  `TokenEstimate`         INT                   DEFAULT NULL COMMENT '该条占多少 token（截断决策留痕）',
  `OrderNo`               INT          NOT NULL DEFAULT 0 COMMENT '在提示词里的顺序',

  `Remark`                TEXT                  DEFAULT NULL COMMENT '备注',
  `IsValid`               INT          NOT NULL DEFAULT 1 COMMENT '有效标志',
  `IsDeleted`             TINYINT(1)   NOT NULL DEFAULT 0 COMMENT '软删除标记',
  `CreateTime`            DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy`              VARCHAR(64)           DEFAULT NULL COMMENT '创建人 Code',
  `UpdateTime`            DATETIME              DEFAULT NULL COMMENT '更新时间',
  `UpdateBy`              VARCHAR(64)           DEFAULT NULL COMMENT '更新人 Code',
  `DeleteTime`            DATETIME              DEFAULT NULL COMMENT '删除时间',
  `DeleteBy`              VARCHAR(64)           DEFAULT NULL COMMENT '删除人 Code',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_analysis_evidence` (`AnalysisCode`, `EvidenceCode`),
  KEY `idx_evidence` (`EvidenceCode`),
  KEY `idx_isvalid_deleted` (`IsValid`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='分析 × 证据（证据链 · 含内容快照，报告可冻结证据）';


-- ─────────────────────────────────────────────────────────────────────────────
-- ④ cert_check_lock — 检查项锁（★ 检查项级 · 与分支正交）
--
--    用户口径：「一旦一个检查项被锁定了，即使选择了这个检查项，
--              但我们队列执行的时候，也要忽略该检查项」
--
--    为什么不在 cert_check_result 上锁：
--      结果行是"分析 × 条款"，一个检查项 3 条款就有 3 行，锁要锁 3 次；
--      而且重跑产生新分析后，锁就跟着旧行一起变成历史了 —— 锁必须独立于分析次数。
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS `cert_check_lock` (
  `Id`             BIGINT       NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `Code`           VARCHAR(50)  NOT NULL COMMENT '业务键（GUID）',

  `CheckItemCode`  VARCHAR(50)  NOT NULL COMMENT '检查项 Code（= cert_audit_plan_detail.Code）',
  `StageKind`      VARCHAR(20)  NOT NULL DEFAULT 'all'
                   COMMENT '锁的粒度：all=整项锁死（默认）/ prep=只锁准备 / onsite=只锁检查',

  `LockedBy`       VARCHAR(64)           DEFAULT NULL COMMENT '锁定人 Code',
  `LockedAt`       DATETIME              DEFAULT NULL COMMENT '锁定时间',
  `LockReason`     VARCHAR(200)          DEFAULT NULL COMMENT '锁定原因：manual_confirm / auditor_fixed / other',
  `LockSource`     VARCHAR(20)  NOT NULL DEFAULT 'manual' COMMENT '来源：manual=人工锁 / auto_nc=开 NC 后自动锁',

  `IsActive`       TINYINT(1)   NOT NULL DEFAULT 1 COMMENT '1=锁定生效；0=已解锁（留痕不删行）',
  `UnlockedBy`     VARCHAR(64)           DEFAULT NULL COMMENT '解锁人 Code',
  `UnlockedAt`     DATETIME              DEFAULT NULL COMMENT '解锁时间',

  `Remark`         TEXT                  DEFAULT NULL COMMENT '备注',
  `IsValid`        INT          NOT NULL DEFAULT 1 COMMENT '有效标志',
  `IsDeleted`      TINYINT(1)   NOT NULL DEFAULT 0 COMMENT '软删除标记',
  `CreateTime`     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy`       VARCHAR(64)           DEFAULT NULL COMMENT '创建人 Code',
  `UpdateTime`     DATETIME              DEFAULT NULL COMMENT '更新时间',
  `UpdateBy`       VARCHAR(64)           DEFAULT NULL COMMENT '更新人 Code',
  `DeleteTime`     DATETIME              DEFAULT NULL COMMENT '删除时间',
  `DeleteBy`       VARCHAR(64)           DEFAULT NULL COMMENT '删除人 Code',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  -- 一个检查项在一个粒度上只有一条锁记录（解锁是 IsActive=0，不是删行）
  UNIQUE KEY `uk_item_stage` (`CheckItemCode`, `StageKind`),
  KEY `idx_active` (`IsActive`),
  KEY `idx_isvalid_deleted` (`IsValid`, `IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='检查项锁（检查项级 · StageKind=all/prep/onsite · 硬跳过，任何 RunMode 绕不过）';


-- ─────────────────────────────────────────────────────────────────────────────
-- ⑤ 改造 cert_check_result — 从"检查项 × 条款"改为"分析 × 条款"
--
--    唯一键：uk_checkitem_clause (CheckItemCode, ClauseCode)
--         →  uk_analysis_clause  (AnalysisCode,  ClauseCode)
--
--    字段归属不变（仍是两段），但现在是"一行只填一段"：
--      prep   分支只写 PrepState / MissingKinds / Conflicts / IsMandatory / VerifyLevel / PrepNcHint
--             ⇒ Conclusion / NcSuggested / Severity 必须为 NULL
--      onsite 分支只写 Conclusion / NcSuggested / Severity / SeveritySource
--             ⇒ PrepState 等必须为 NULL
-- ─────────────────────────────────────────────────────────────────────────────

-- 5.1 加 AnalysisCode（先允许 NULL，存量回填后再收紧）
SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_check_result' AND COLUMN_NAME = 'AnalysisCode'
);
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `cert_check_result` ADD COLUMN `AnalysisCode` VARCHAR(50) NULL DEFAULT NULL COMMENT ''所属分析（关联 cert_check_analysis.Code）'' AFTER `Code`',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 5.2 加 StageKind（冗余，便于按分支过滤，不用每次 join 分析表）
SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_check_result' AND COLUMN_NAME = 'StageKind'
);
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `cert_check_result` ADD COLUMN `StageKind` VARCHAR(20) NULL DEFAULT NULL COMMENT ''分支冗余：prep / onsite（便于按分支过滤）'' AFTER `AnalysisCode`',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 5.3 删旧唯一键 uk_checkitem_clause（这是"一行两段"的结构性根因）
SET @idx_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_check_result' AND INDEX_NAME = 'uk_checkitem_clause'
);
SET @ddl := IF(@idx_exists > 0,
  'ALTER TABLE `cert_check_result` DROP INDEX `uk_checkitem_clause`',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 5.4 加新唯一键 uk_analysis_clause (AnalysisCode, ClauseCode)
--     ⚠️ 要求 AnalysisCode 非空才有意义，所以先回填（5.5）再建索引？不行 ——
--     唯一索引允许多 NULL，先建索引不会报错；但为了让"同一分析同一算款只有一行"真正生效，
--     必须保证 AnalysisCode 非空（5.6 收紧）。
SET @idx_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_check_result' AND INDEX_NAME = 'uk_analysis_clause'
);
SET @ddl := IF(@idx_exists = 0,
  'ALTER TABLE `cert_check_result` ADD UNIQUE KEY `uk_analysis_clause` (`AnalysisCode`, `ClauseCode`)',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 5.5 存量回填：为每条孤儿结果行补一条"迁移分析"（只在有存量时执行）
--     迁移分析：StageKind 取 TaskKind 无从得知 ⇒ 用 onsite（历史数据都是检查方向写的）
SET @orphan := (SELECT COUNT(*) FROM `cert_check_result` WHERE `AnalysisCode` IS NULL);
SET @ddl := IF(@orphan > 0,
  CONCAT(
    'INSERT INTO `cert_check_analysis` ',
    '(`Code`,`CheckItemCode`,`StageKind`,`IsCurrent`,`RunNo`,`Status`,`PromptType`,`Remark`,`IsValid`,`IsDeleted`,`CreateTime`) ',
    'SELECT CONCAT(''MIGR_'', LEFT(MD5(r.`CheckItemCode`), 24)), r.`CheckItemCode`, ''onsite'', 1, 1, ''done'', ''audit_check'', ',
    '''迁移脚本 20261101 回填（原行无分析归属）'', 1, 0, NOW() ',
    'FROM (SELECT DISTINCT `CheckItemCode` FROM `cert_check_result` WHERE `AnalysisCode` IS NULL) r'
  ),
  'SELECT ''无孤儿结果行，跳过回填'' AS `skip`');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl := IF(@orphan > 0,
  CONCAT(
    'UPDATE `cert_check_result` cr ',
    'JOIN `cert_check_analysis` a ON a.`CheckItemCode` = cr.`CheckItemCode` AND a.`Remark` = ''迁移脚本 20261101 回填（原行无分析归属）'' ',
    'SET cr.`AnalysisCode` = a.`Code`, cr.`StageKind` = a.`StageKind` ',
    'WHERE cr.`AnalysisCode` IS NULL'
  ),
  'SELECT ''无孤儿结果行，跳过回填'' AS `skip`');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 5.6 收紧 AnalysisCode 为 NOT NULL（仅在无 NULL 时执行，否则留告警）
SET @null_left := (SELECT COUNT(*) FROM `cert_check_result` WHERE `AnalysisCode` IS NULL);
SET @ddl := IF(@null_left = 0,
  'ALTER TABLE `cert_check_result` MODIFY COLUMN `AnalysisCode` VARCHAR(50) NOT NULL COMMENT ''所属分析（关联 cert_check_analysis.Code）''',
  'SELECT ''⚠️ 仍有 AnalysisCode 为空的行，暂不收紧 NOT NULL'' AS `warn`');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 5.7 移出锁 4 列（锁迁到 cert_check_lock，结果行不再承载锁定状态）
--     ⛔ 先确认没有未迁移的锁定数据，否则拒绝删除（防静默丢锁）
SET @locked_rows := (SELECT COUNT(*) FROM `cert_check_result` WHERE `IsLocked` = 1);
SET @ddl := IF(@locked_rows > 0,
  CONCAT(
    'INSERT INTO `cert_check_lock` (`Code`,`CheckItemCode`,`StageKind`,`LockedBy`,`LockedAt`,`LockReason`,`LockSource`,`IsActive`,`IsValid`,`IsDeleted`,`CreateTime`) ',
    'SELECT CONCAT(''LOCK_'', LEFT(MD5(CONCAT(`CheckItemCode`,'':onsite'')), 24)), `CheckItemCode`, ''onsite'', MAX(`LockedBy`), MAX(`LockedAt`), MAX(`LockReason`), ''manual'', 1, 1, 0, NOW() ',
    'FROM `cert_check_result` WHERE `IsLocked` = 1 GROUP BY `CheckItemCode` ',
    'ON DUPLICATE KEY UPDATE `IsActive` = 1'
  ),
  'SELECT ''无锁定存量，跳过锁迁移'' AS `skip`');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_check_result' AND COLUMN_NAME = 'IsLocked'
);
SET @ddl := IF(@col_exists > 0,
  'ALTER TABLE `cert_check_result`
     DROP INDEX `idx_islocked`,
     DROP COLUMN `IsLocked`,
     DROP COLUMN `LockedBy`,
     DROP COLUMN `LockedAt`,
     DROP COLUMN `LockReason`',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 5.8 补索引：按分支查当前结论（界面"准备分支 / 检查分支"两条查询都走这个）
SET @idx_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_check_result' AND INDEX_NAME = 'idx_stage'
);
SET @ddl := IF(@idx_exists = 0,
  'ALTER TABLE `cert_check_result` ADD KEY `idx_stage` (`StageKind`, `CheckItemCode`)',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 5.9 更新表注释（说明新语义）
ALTER TABLE `cert_check_result`
  COMMENT = '结论（分析 × 条款 · 一行只填一段：prep 段或 onsite 段 · 锁在 cert_check_lock）';


-- ─────────────────────────────────────────────────────────────────────────────
-- ⑥ cert_point 加 AnalysisCode — 疑点归属到"哪一次分析"
--    "同一检查项准备 2 个疑点、检查 3 个疑点，分不清哪批是哪次"
--    ⇒ 挂分析后，按 AnalysisCode 一查就分清；Origin 保留（prep/onsite/pq/manual）
-- ─────────────────────────────────────────────────────────────────────────────

SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_point' AND COLUMN_NAME = 'AnalysisCode'
);
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `cert_point` ADD COLUMN `AnalysisCode` VARCHAR(50) NULL DEFAULT NULL COMMENT ''所属分析（关联 cert_check_analysis.Code；NULL=手工录入）'' AFTER `ClauseCode`',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @idx_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_point' AND INDEX_NAME = 'idx_analysis'
);
SET @ddl := IF(@idx_exists = 0,
  'ALTER TABLE `cert_point` ADD KEY `idx_analysis` (`AnalysisCode`, `Status`)',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;


-- ─────────────────────────────────────────────────────────────────────────────
-- ⑦ cert_task 加 ItemSkipped — 全锁场景的进度口径（§10.3）
--    勾了 8 项、8 项全锁 ⇒ ItemTotal=8, ItemDone=0, ItemSkipped=8
--    Message = "选中的 8 个检查项均已锁定，本次未执行任何分析"（绿色提示，不是错误）
-- ─────────────────────────────────────────────────────────────────────────────

SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_task' AND COLUMN_NAME = 'ItemSkipped'
);
SET @ddl := IF(@col_exists = 0,
  'ALTER TABLE `cert_task` ADD COLUMN `ItemSkipped` INT NOT NULL DEFAULT 0 COMMENT ''因锁定而跳过的项数（全锁时不空转，§10.3）'' AFTER `ItemFailed`',
  'SELECT 1');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;


-- ─────────────────────────────────────────────────────────────────────────────
-- ⑧ 验收断言（达不到就不许放行）
-- ─────────────────────────────────────────────────────────────────────────────

SELECT '---------- 验收 ----------' AS `step`;

-- 8.1 三张新表必须存在
SELECT '8.1 新表' AS `check`,
       GROUP_CONCAT(TABLE_NAME ORDER BY TABLE_NAME) AS `tables`,
       IF(COUNT(*) = 3, 'OK', '❌ 缺表') AS `result`
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_check_analysis', 'cert_check_analysis_evidence', 'cert_check_lock');

-- 8.2 IsCurrent 必须是 DEFAULT NULL（⛔ 用 0 表历史会导致第二次重跑唯一键冲突）
SELECT '8.2 IsCurrent 默认值' AS `check`,
       COLUMN_DEFAULT AS `default`,
       IF(COLUMN_DEFAULT IS NULL AND IS_NULLABLE = 'YES', 'OK', '❌ 必须 DEFAULT NULL') AS `result`
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_check_analysis' AND COLUMN_NAME = 'IsCurrent';

-- 8.3 uk_current 唯一键必须存在
SELECT '8.3 uk_current' AS `check`,
       IF(COUNT(*) > 0, 'OK', '❌ 缺唯一键') AS `result`
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_check_analysis' AND INDEX_NAME = 'uk_current';

-- 8.4 cert_check_result 旧键必须已删、新键必须已建
SELECT '8.4 结果表唯一键' AS `check`,
       GROUP_CONCAT(DISTINCT INDEX_NAME ORDER BY INDEX_NAME) AS `indexes`,
       IF(SUM(INDEX_NAME = 'uk_checkitem_clause') = 0 AND SUM(INDEX_NAME = 'uk_analysis_clause') > 0,
          'OK', '❌ 旧键未删或新键未建') AS `result`
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_check_result'
  AND INDEX_NAME IN ('uk_checkitem_clause', 'uk_analysis_clause');

-- 8.5 锁 4 列必须已移出结果表
SELECT '8.5 结果表锁列' AS `check`,
       IFNULL(GROUP_CONCAT(COLUMN_NAME ORDER BY COLUMN_NAME), '(已全部移除)') AS `left_columns`,
       IF(COUNT(*) = 0, 'OK', '❌ 仍有锁列') AS `result`
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_check_result'
  AND COLUMN_NAME IN ('IsLocked', 'LockedBy', 'LockedAt', 'LockReason');

-- 8.6 cert_point.AnalysisCode / cert_task.ItemSkipped 必须已加
SELECT '8.6 补列' AS `check`,
       GROUP_CONCAT(CONCAT(TABLE_NAME, '.', COLUMN_NAME) ORDER BY TABLE_NAME) AS `columns`,
       IF(COUNT(*) = 2, 'OK', '❌ 缺列') AS `result`
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND ((TABLE_NAME = 'cert_point' AND COLUMN_NAME = 'AnalysisCode')
    OR (TABLE_NAME = 'cert_task'  AND COLUMN_NAME = 'ItemSkipped'));

SELECT '========== 完成：分析层已落地 ==========' AS `step`;
SELECT '下一步：编译后端 → 重启 9992 → 跑一次准备分析验证 4 条出口判据' AS `next`;
