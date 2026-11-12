-- 2026-11-12 计划检查项条款 Code 重映射
-- 背景：ISO9001 等标准条款在 /cert/iso-clause 覆盖重建后，旧条款行被软删，
--       但 cert_audit_plan_detail.Clauses 仍保存旧条款 Code → 认证检查装配时
--       GetClausesByCodesAsync 查不到条款正文，表现为「无条款清单 / 无企业文档摘录 / 证据 0」。
-- 口径：用旧条款 Code 反查 ClauseNumber，再按计划标准找到当前有效条款 Code，按原顺序重写 Clauses。
-- 幂等：只更新「所有旧 Code 都能映射到当前条款、且新串与旧串不同」的行。

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 备份（只建一次；重复执行不覆盖历史备份）
CREATE TABLE IF NOT EXISTS _bak_20261112_plan_detail_clauses AS
SELECT Code, PlanCode, Clauses, ClauseNumbers, NOW() AS BackupTime
FROM cert_audit_plan_detail
WHERE IsDeleted = 0;

DROP TEMPORARY TABLE IF EXISTS tmp_plan_clause_remap;
CREATE TEMPORARY TABLE tmp_plan_clause_remap AS
SELECT
    d.Code AS DetailCode,
    d.Clauses AS OldClauses,
    CAST(GROUP_CONCAT(cur.Code ORDER BY jt.ord SEPARATOR ',') AS CHAR(500)) AS NewClauses,
    COUNT(*) AS CodeCount,
    SUM(CASE WHEN cur.Code IS NULL THEN 1 ELSE 0 END) AS MissingCount
FROM cert_audit_plan_detail d
JOIN cert_audit_plan p ON p.Code = d.PlanCode AND p.IsDeleted = 0
JOIN JSON_TABLE(
    CONCAT('["', REPLACE(COALESCE(d.Clauses, ''), ',', '","'), '"]'),
    '$[*]' COLUMNS (
        ord FOR ORDINALITY,
        old_code VARCHAR(100) PATH '$'
    )
) jt
LEFT JOIN cert_iso_clause c ON c.Code = jt.old_code
LEFT JOIN cert_iso_clause cur
    ON FIND_IN_SET(cur.StandardCode, p.Standards) > 0
   AND cur.ClauseNumber = c.ClauseNumber
   AND cur.IsDeleted = 0
   AND cur.IsValid = 1
WHERE d.IsDeleted = 0
  AND COALESCE(d.Clauses, '') <> ''
GROUP BY d.Code, d.Clauses
HAVING MissingCount = 0
   AND NewClauses <> OldClauses;

UPDATE cert_audit_plan_detail d
JOIN tmp_plan_clause_remap m ON m.DetailCode = d.Code
SET d.Clauses = m.NewClauses,
    d.UpdateTime = NOW()
WHERE d.IsDeleted = 0;

DROP TEMPORARY TABLE IF EXISTS tmp_plan_clause_remap;

-- 验证：应返回 0 行（所有条款 Code 都能查到当前有效条款）
SELECT d.Code, d.ClauseNumbers, d.Clauses
FROM cert_audit_plan_detail d
LEFT JOIN cert_iso_clause c
    ON c.Code = SUBSTRING_INDEX(d.Clauses, ',', 1)
   AND c.IsDeleted = 0
   AND c.IsValid = 1
WHERE d.PlanCode = '35658996e78c4528aeec0321b1d9a162'
  AND d.IsDeleted = 0
  AND c.Code IS NULL;
