-- =============================================================================
-- 修复 Sys_RoleMenu 排序规则错配 V1
-- 日期：2026-09-23
--
-- ★ 问题：
--   `Sys_RoleMenu` 由 create_sys_rolemenu_V1.sql 建立时只写了
--   `DEFAULT CHARSET=utf8mb4`（未指定 COLLATE）→ 随 MySQL 8.0 服务端默认
--   落到 `utf8mb4_0900_ai_ci`。
--   但它的两个**外键目标**都是历史导入的 `utf8mb4_general_ci`：
--       RoleCode → sys_role.Code        (utf8mb4_general_ci)
--       MenuCode → Sys_Menu.Code        (utf8mb4_general_ci)
--
-- ★ 症状：
--   任何「列 vs 列」比较/关联都抛 1267：
--     ERROR 1267 (HY000): Illegal mix of collations
--       (utf8mb4_0900_ai_ci,IMPLICIT) and (utf8mb4_general_ci,IMPLICIT) for operation '='
--   典型触发点：`WHERE rm.MenuCode = m.Code` 这类 EXISTS / JOIN。
--   （「列 vs 字面量」不受影响 —— 字面量 coercibility=4，列胜出，故单表 UPDATE 不会报错。）
--
-- ★ 修法：
--   对齐到既有约定 `utf8mb4_general_ci`（**不改 Sys_Menu / sys_role** —— 它们是历史
--   导入表，与库内 65 张 general_ci 表互相关联，改它们会引发新的错配）。
--   本表仅 23 行，CONVERT 开销可忽略。
--
-- ★ 防复发：create_sys_rolemenu_V1.sql 的建表语句已同步补 `COLLATE=utf8mb4_general_ci`。
-- ★ 幂等：可重复执行。
-- =============================================================================

ALTER TABLE `Sys_RoleMenu`
  CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- 自检：应输出 utf8mb4_general_ci
-- -----------------------------------------------------------------------------
SELECT '--- Sys_RoleMenu 排序规则 ---' AS `check`;
SELECT TABLE_NAME, TABLE_COLLATION
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Sys_RoleMenu';

SELECT '--- 各字符列排序规则（应全部为 utf8mb4_general_ci）---' AS `check`;
SELECT COLUMN_NAME, COLLATION_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'Sys_RoleMenu'
  AND COLLATION_NAME IS NOT NULL
ORDER BY ORDINAL_POSITION;

-- -----------------------------------------------------------------------------
-- 自检：与父表做一次列-列比较，不再抛 1267 即为修复成功
-- -----------------------------------------------------------------------------
SELECT '--- 跨表 collation 比较探针（期望 0 行、且不报错）---' AS `check`;
SELECT rm.`MenuCode`, m.`Code`
FROM `Sys_RoleMenu` rm
JOIN `Sys_Menu` m ON rm.`MenuCode` = m.`Code`
WHERE 1 = 0;
