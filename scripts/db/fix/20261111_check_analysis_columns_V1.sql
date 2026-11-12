-- =============================================================================
-- 认证检查分析落库 · 列类型/可空性对齐（2026-11-11）
--
-- 现象：analyze-item 返回成功，但页面已完成项的「判断依据 / 疑点 / 检查内容」全空，
--       库里 cert_check_analysis / cert_check_result 都是 0 行。
-- 根因（日志实证，同一次事务里连报三条）：
--   ① cert_check_analysis.InputSnapshotJson 是 varchar(255)，代码写最多 60000 字符
--      ⇒ Data too long for column 'InputSnapshotJson'
--   ② cert_check_result.PrepState (及其它条件写入列) 是 NOT NULL，
--      但 onsite 分支按设计只写 Check 组、Prep 组必须为 NULL
--      ⇒ Column 'PrepState' cannot be null
--   ③ cert_analysis.TaskKind / ErrorMessage 等实体可空、表 NOT NULL
-- 本脚本把实体声明为可空 / 大文本的列全部对齐，幂等。
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ── ① 分析行：快照与汇总 JSON 必须是大文本 ──
ALTER TABLE `cert_check_analysis`
    MODIFY COLUMN `EvidenceFingerprint` text NULL COMMENT '证据指纹（多条 MD5 串）',
    MODIFY COLUMN `InputSnapshotJson` mediumtext NULL COMMENT '装配包快照（回放用，最长 60000 字符）',
    MODIFY COLUMN `ResultSummaryJson` mediumtext NULL COMMENT '结果汇总 JSON（装配指标 + 结论映射）',
    MODIFY COLUMN `ErrorMessage` text NULL COMMENT '失败原因';

-- ── ② 逐条款结果：prep / onsite 各自只写一组，另一组必须允许 NULL ──
ALTER TABLE `cert_check_result`
    MODIFY COLUMN `StandardCode` varchar(255) NULL DEFAULT NULL,
    MODIFY COLUMN `ClauseNumber` varchar(255) NULL DEFAULT NULL,
    MODIFY COLUMN `PrepState` varchar(255) NULL DEFAULT NULL,
    MODIFY COLUMN `MissingKindsJson` text NULL,
    MODIFY COLUMN `ConflictsJson` text NULL,
    MODIFY COLUMN `IsMandatory` tinyint(1) NULL DEFAULT NULL,
    MODIFY COLUMN `VerifyLevel` varchar(255) NULL DEFAULT NULL,
    MODIFY COLUMN `PrepNcHint` varchar(255) NULL DEFAULT NULL,
    MODIFY COLUMN `Conclusion` varchar(255) NULL DEFAULT NULL,
    MODIFY COLUMN `AiConclusion` varchar(255) NULL DEFAULT NULL,
    MODIFY COLUMN `AiSeverity` varchar(255) NULL DEFAULT NULL,
    MODIFY COLUMN `NcSuggested` tinyint(1) NULL DEFAULT NULL,
    MODIFY COLUMN `Severity` varchar(255) NULL DEFAULT NULL,
    MODIFY COLUMN `SeveritySource` varchar(255) NULL DEFAULT NULL,
    MODIFY COLUMN `SeverityBaseline` varchar(255) NULL DEFAULT NULL,
    MODIFY COLUMN `Reason` text NULL COMMENT '判断依据',
    MODIFY COLUMN `DoubtsJson` text NULL COMMENT '疑点 JSON',
    MODIFY COLUMN `ConclusionAt` datetime NULL DEFAULT NULL,
    MODIFY COLUMN `StaleReason` varchar(255) NULL DEFAULT NULL;

-- ── ③ 失败留痕行（cert_analysis）：实体可空的列不要 NOT NULL ──
ALTER TABLE `cert_analysis`
    MODIFY COLUMN `TaskKind` varchar(255) NULL DEFAULT NULL,
    MODIFY COLUMN `EvidenceFingerprint` text NULL,
    MODIFY COLUMN `PromptCode` varchar(255) NULL DEFAULT NULL,
    MODIFY COLUMN `PromptVersion` varchar(255) NULL DEFAULT NULL,
    MODIFY COLUMN `ModelName` varchar(255) NULL DEFAULT NULL,
    MODIFY COLUMN `ErrorMessage` text NULL;

-- ── 验收 ──
SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND (
    (TABLE_NAME = 'cert_check_analysis' AND COLUMN_NAME IN ('InputSnapshotJson', 'ResultSummaryJson', 'ErrorMessage'))
    OR (TABLE_NAME = 'cert_check_result' AND COLUMN_NAME IN ('PrepState', 'Reason', 'DoubtsJson', 'AiConclusion'))
    OR (TABLE_NAME = 'cert_analysis' AND COLUMN_NAME IN ('TaskKind', 'ErrorMessage'))
  )
ORDER BY TABLE_NAME, COLUMN_NAME;
