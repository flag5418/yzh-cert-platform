-- ════════════════════════════════════════════════════════════════════════
-- 2026-11-24 条款属性池清理 V1
--
-- 目的（用户裁定：无效属性池 + 已挂接的无效属性，全部删除）：
--   ① 删除 cert_clause_attr_def 中 IsValid=0 的无效属性池定义（Artifacts / EvidenceChain）
--   ② 删除 cert_clause_attr 中「属性码已不在属性池」的悬空挂接行（即上条属性的挂接数据）
--   ③ 删除 cert_clause_attr 中 IsValid=0 的历史软删行（悬空/过期数据）
--
-- 幂等：可重复执行。执行前自动备份（*_bak_20261124，仅建一次）。
-- 不改动任何表结构；ClauseText（系统派生属性，RM7 语料）⛔ 不动。
-- ════════════════════════════════════════════════════════════════════════
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ── 备份（幂等） ──────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS cert_clause_attr_def_bak_20261124 LIKE cert_clause_attr_def;
INSERT IGNORE INTO cert_clause_attr_def_bak_20261124 SELECT * FROM cert_clause_attr_def WHERE IsValid = 0;

CREATE TABLE IF NOT EXISTS cert_clause_attr_bak_20261124 LIKE cert_clause_attr;
INSERT IGNORE INTO cert_clause_attr_bak_20261124 SELECT * FROM cert_clause_attr WHERE IsValid = 0 OR IsDeleted = 1;

-- ── ① 无效属性池定义 ──────────────────────────────────────────────────
DELETE FROM cert_clause_attr_def WHERE IsValid = 0;

-- ── ② 悬空挂接行（属性码已不在属性池） ────────────────────────────────
DELETE FROM cert_clause_attr WHERE AttrCode NOT IN (SELECT Code FROM cert_clause_attr_def);

-- ── ③ 历史软删行 ──────────────────────────────────────────────────────
DELETE FROM cert_clause_attr WHERE IsDeleted = 1;

-- ── 验证 ──────────────────────────────────────────────────────────────
SELECT 'def_remaining' AS k, COUNT(*) AS v FROM cert_clause_attr_def
UNION ALL SELECT 'attr_remaining', COUNT(*) FROM cert_clause_attr
UNION ALL SELECT 'attr_invalid_rows', COUNT(*) FROM cert_clause_attr WHERE IsValid = 0
UNION ALL SELECT 'attr_deleted_rows', COUNT(*) FROM cert_clause_attr WHERE IsDeleted = 1;

SELECT AttrCode, COUNT(*) AS cnt FROM cert_clause_attr GROUP BY AttrCode ORDER BY AttrCode;
