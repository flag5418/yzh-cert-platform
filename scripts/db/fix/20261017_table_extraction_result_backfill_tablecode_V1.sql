SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
-- ============================================================
-- 20261017_table_extraction_result_backfill_tablecode_V1.sql
-- ★ 存量回填：cert_table_extraction_result.TableCode（B-09 表格取数键）
--
-- 背景（2026-10-17 用户报障：工作流 docTable 节点读不到平台样例，AI 节点提示词
--   {{文档表格.result}} 恒判 UnresolvedRefs）：
--   · 写侧 DocExtractionRuleService.SyncExtractionResultToB08B09Async 的**平台标准级**
--     分支（旧 :417-431）漏写 TableCode ⇒ 行落库 TableCode=NULL；
--   · 而 docTable 节点按 TableCode='table1' 查（NodeExecutor.ExecuteDocTableAsync）
--     ⇒ 恒 state=not_found ⇒ AI 节点引用解析不到。
--   · 企业分支（SaveEnterpriseExtractionResultsAsync）一直有写 TableCode —— 只有平台样例漏。
--
-- 代码已同步修复（写侧补 TableCode = kv.Key）；本脚本只回填**历史行**。
--
-- 幂等：仅更新 TableCode IS NULL 的行；仅当 RuleCode 在 cert_doc_table_def 中
--       **恰好映射到 1 个有效表格定义**时才回填（多义则跳过，交人工）。
-- 命名：一律 PascalCase（项目铁律七）。
-- ============================================================

-- ── ① 备份（幂等：已存在则不覆盖，保留首次备份） ──────────────
CREATE TABLE IF NOT EXISTS cert_table_extraction_result_bak_tablecode_20261017 AS
  SELECT * FROM cert_table_extraction_result;

-- ── ② 回填：规则 → 唯一表格定义时才写 ────────────────────────
UPDATE cert_table_extraction_result t
JOIN (
  SELECT d.RuleCode, MIN(d.TableCode) AS TableCode
  FROM cert_doc_table_def d
  WHERE d.IsDeleted = 0
  GROUP BY d.RuleCode
  HAVING COUNT(DISTINCT d.TableCode) = 1
) m ON m.RuleCode = t.RuleCode
SET t.TableCode = m.TableCode
WHERE (t.TableCode IS NULL OR t.TableCode = '')
  AND t.RuleCode IS NOT NULL AND t.RuleCode <> '';

-- ── ③ 校验 ───────────────────────────────────────────────────
-- ③-1 仍为空的行（期望：仅「规则无表格定义或多义」的历史行；正常业务应为 0）
SELECT 'C_tablecode_仍空_期望0' AS 指标, COUNT(*) AS 值
FROM cert_table_extraction_result
WHERE (TableCode IS NULL OR TableCode = '') AND IsDeleted = 0;

-- ③-2 回填后按 (RuleCode, TableCode, TableIndex) 去重检查（期望 0）
SELECT 'C_回填后重复_期望0' AS 指标, COUNT(*) AS 值 FROM (
  SELECT RuleCode, TableCode, TableIndex, COUNT(*) c
  FROM cert_table_extraction_result
  WHERE TableCode IS NOT NULL AND TableCode <> '' AND IsDeleted = 0
  GROUP BY RuleCode, TableCode, TableIndex
  HAVING COUNT(*) > 1
) x;

-- ── ④ 回滚 ───────────────────────────────────────────────────
-- UPDATE cert_table_extraction_result t
-- JOIN cert_table_extraction_result_bak_tablecode_20261017 b ON b.Code = t.Code
-- SET t.TableCode = b.TableCode;
-- （回滚后建议 DROP TABLE cert_table_extraction_result_bak_tablecode_20261017;）
