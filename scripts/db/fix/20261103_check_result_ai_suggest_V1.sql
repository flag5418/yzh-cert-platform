-- =============================================================================
-- 2026-11-03 · cert_check_result 区分「系统建议」与「审核员判定」
--
-- 背景（用户 2026-11-03）：
--   系统是「认证辅助系统」，不是认证决策系统 —— 分析只给建议（建议结论 /
--   建议不符合程度 / 多条疑点），最终结论必须由审核员判定。
--   原实现把 AI 结论直接写进 Conclusion，前端看起来像「已经定了」。
--
-- 新增列：
--   AiConclusion  系统建议结论 pass / ofi / nc / na（⛔ 仅供参考，不是最终结论）
--   AiSeverity    系统建议不符合程度 major / minor（⛔ 仅供参考）
--   DoubtsJson    疑点分析 JSON 数组 [{point, detail}]，可以是多条、也可以为空
--
-- 口径：
--   Conclusion / Severity 只由审核员写（SaveResultAsync）；
--   ConclusionBy 非空 ⇔ 审核员已判定（前端据此显示「已判定」，否则「待判定」）。
--
-- 存量纠偏：ConclusionBy 为空 = 未经审核员确认 ⇒ 把现有 Conclusion / Severity
--   迁到 Ai* 列，结论列清空（显示「待判定」）。
--
-- 幂等：列已存在则跳过；存量迁移只处理 ConclusionBy 为空的旧 AI 结论。
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ── 1. AiConclusion ──
SET @has_ai_conclusion := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'cert_check_result'
      AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'AiConclusion'
);
SET @ddl := IF(
    @has_ai_conclusion = 0,
    'ALTER TABLE cert_check_result ADD COLUMN AiConclusion varchar(20) NULL COMMENT ''系统建议结论 pass/ofi/nc/na（AI 产出，仅供参考，最终由审核员判定）'' AFTER Conclusion',
    'SELECT ''AiConclusion 列已存在，跳过'' AS Skip'
);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ── 2. AiSeverity ──
SET @has_ai_severity := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'cert_check_result'
      AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'AiSeverity'
);
SET @ddl := IF(
    @has_ai_severity = 0,
    'ALTER TABLE cert_check_result ADD COLUMN AiSeverity varchar(20) NULL COMMENT ''系统建议不符合程度 major/minor（AI 产出，仅供参考，最终由审核员判定）'' AFTER AiConclusion',
    'SELECT ''AiSeverity 列已存在，跳过'' AS Skip'
);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ── 3. DoubtsJson ──
SET @has_doubts_json := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'cert_check_result'
      AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'DoubtsJson'
);
SET @ddl := IF(
    @has_doubts_json = 0,
    'ALTER TABLE cert_check_result ADD COLUMN DoubtsJson longtext NULL COMMENT ''疑点分析 JSON 数组 [{point, detail}]（可多条，空数组=无疑点）'' AFTER Reason',
    'SELECT ''DoubtsJson 列已存在，跳过'' AS Skip'
);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ── 4. 存量纠偏：未人工确认的 AI 结论 → 建议列 ──
UPDATE cert_check_result
SET AiConclusion = Conclusion,
    AiSeverity   = CASE
                       WHEN Conclusion = 'nc' THEN NULLIF(Severity, '')
                       ELSE NULL
                   END,
    Conclusion   = NULL,
    Severity     = NULL,
    SeveritySource = NULL,
    UpdateTime   = NOW()
WHERE ConclusionBy IS NULL
  AND Conclusion IS NOT NULL;

-- 验证
SELECT CONCAT('AiConclusion 列数 = ', COUNT(*)) AS Result
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cert_check_result'
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'AiConclusion';

SELECT CONCAT('未判定(AiConclusion 非空) = ', COUNT(*)) AS Cnt
FROM cert_check_result
WHERE AiConclusion IS NOT NULL AND ConclusionBy IS NULL;

SELECT CONCAT('已判定(ConclusionBy 非空) = ', COUNT(*)) AS Cnt
FROM cert_check_result
WHERE ConclusionBy IS NOT NULL;
