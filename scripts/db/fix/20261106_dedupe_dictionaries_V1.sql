-- ============================================================================
-- 20261106_dedupe_dictionaries_V1.sql
--
-- 清理：Sys_Dictionary / Sys_DictionaryList 的重复数据（用户 2026-11-06 确认）
--
-- 背景：
--   2026-11-06 上午多次执行 / re-执行条款属性相关种子脚本
--   （scripts/db/20261028_clause_attr_V1.sql 等），而两表**只有主键、无唯一索引**，
--   INSERT ... ON DUPLICATE KEY UPDATE 实际从未拦住重复：
--     · 表头 CLAUSE_* / ARTIFACT_TYPE / PROCESS_DOMAIN 各 2 份（两份 Code 完全相同）
--     · 字典项按 (DicCode, DicValue) 重复 4–6 份
--   症状：字典管理页树出现重复表头 / 下拉开出重复选项 / 逐条删改时可能命中错行。
--
-- 保留策略（与 20261106_dedupe_sys_role_V1.sql 一致）：各组仅保留最小 Id（最早）1 份。
--   ★ 无需「项迁移到保留表头」：重复表头的 Code 完全相同，字典项按 Code 字符串关联，
--     保留任一表头其项都挂在同一 Code 下。
--   ☆ 仅对 IsDeleted=0 的行去重（清理前已确认两表无 IsDeleted=1 行）。
--
-- 幂等：重复行删除后再次执行不再命中。
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 0) 清理前快照
DROP TABLE IF EXISTS _bak_dedupe_20261106_sys_dictionary;
CREATE TABLE _bak_dedupe_20261106_sys_dictionary AS SELECT * FROM Sys_Dictionary;
DROP TABLE IF EXISTS _bak_dedupe_20261106_sys_dictionarylist;
CREATE TABLE _bak_dedupe_20261106_sys_dictionarylist AS SELECT * FROM Sys_DictionaryList;
SELECT 'before-headers' AS stage, COUNT(*) AS total FROM Sys_Dictionary WHERE IsDeleted=0;
SELECT 'before-items' AS stage, COUNT(*) AS total FROM Sys_DictionaryList WHERE IsDeleted=0;

-- 1) 表头去重：同 Code 仅保留最小 Id
DELETE d1 FROM Sys_Dictionary d1
INNER JOIN (
    SELECT Code, MIN(Id) AS MinId
    FROM Sys_Dictionary
    WHERE IsDeleted = 0
    GROUP BY Code
    HAVING COUNT(*) > 1
) d2 ON d1.Code = d2.Code
WHERE d1.IsDeleted = 0 AND d1.Id > d2.MinId;

-- 2) 字典项去重：同 (DicCode, DicValue) 仅保留最小 Id
DELETE l1 FROM Sys_DictionaryList l1
INNER JOIN (
    SELECT DicCode, DicValue, MIN(Id) AS MinId
    FROM Sys_DictionaryList
    WHERE IsDeleted = 0 AND DicValue IS NOT NULL
    GROUP BY DicCode, DicValue
    HAVING COUNT(*) > 1
) l2 ON l1.DicCode = l2.DicCode AND l1.DicValue = l2.DicValue
WHERE l1.IsDeleted = 0 AND l1.Id > l2.MinId;

-- 3) 验评
SELECT 'after-headers' AS stage, COUNT(*) AS total, COUNT(DISTINCT Code) AS distinct_code,
       COUNT(DISTINCT DicNo) AS distinct_dicno FROM Sys_Dictionary WHERE IsDeleted=0;
SELECT 'after-items' AS stage, COUNT(*) AS total FROM Sys_DictionaryList WHERE IsDeleted=0;
SELECT 'hdr-dup-check' AS chk, COUNT(*) AS dup_groups FROM (
    SELECT Code FROM Sys_Dictionary WHERE IsDeleted=0 GROUP BY Code HAVING COUNT(*) > 1
) t;
SELECT 'item-dup-check' AS chk, COUNT(*) AS dup_groups FROM (
    SELECT DicCode, DicValue FROM Sys_DictionaryList WHERE IsDeleted=0 AND DicValue IS NOT NULL
    GROUP BY DicCode, DicValue HAVING COUNT(*) > 1
) t;
SELECT d.DicNo, d.DicName, COUNT(l.Id) AS ItemCount
FROM Sys_Dictionary d
JOIN Sys_DictionaryList l ON l.DicCode = d.Code AND l.IsValid = 1 AND l.IsDeleted = 0
WHERE d.IsDeleted = 0
GROUP BY d.DicNo, d.DicName
ORDER BY d.OrderNo;
