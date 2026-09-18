-- ============================================================
-- 20260917_wf_standard_columns.sql
-- YZH 新架构统一契约补齐：IsValid（启用/禁用）+ IsDeleted（软删除）
--
-- 背景（2026-09-17 排查结论）：
--   1. 框架 ORM（YZH.Core.DataBase.SqlSugarDbOrm）对实现 IIsValid 的实体
--      自动追加 `IsValid = 1` / `IsDeleted = 0` 过滤（GetPageAsync/GetListAsync），
--      列名取自实体属性（未显式映射时 = PascalCase 属性名）。
--   2. wf_* 五表与 yzh_queue_task 建表时只带了 snake_case 旧列（is_valid/is_active/IsDeleted 缺失），
--      导致 WfSkill/filter、WfSkillCategory/filter 全部 400：
--      "Unknown column 'IsValid' in 'where clause'"。
--   3. 实体侧按 SysConfig 惯例重声明基类属性（无 SugarColumn 特性 → 自动映射 PascalCase 列）。
--
-- 本脚本职责：DB 侧补齐标准列 + 从旧列同步存量数据；旧列保留不删（兼容存量原生 SQL）。
-- 幂等性：information_schema 守卫，可重复执行。
-- 注意：加列触发表重建会校验既有外键（wf_skill.Code 为 varchar 与部分子表类型不兼容），
--       本次仅为加列不破坏数据，临时关闭外键校验。
-- ============================================================

SET FOREIGN_KEY_CHECKS = 0;

-- ---------- wf_skill ----------
SET @s := IF((SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_skill' AND COLUMN_NAME = 'IsValid') = 0,
    'ALTER TABLE `wf_skill` ADD COLUMN `IsValid` tinyint(1) NOT NULL DEFAULT 1 COMMENT ''有效标志(框架标准列,统一IIsValid契约)''',
    'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;
UPDATE `wf_skill` SET `IsValid` = `is_active` WHERE `is_active` IS NOT NULL;

-- ---------- wf_skill_category ----------
SET @s := IF((SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_skill_category' AND COLUMN_NAME = 'IsValid') = 0,
    'ALTER TABLE `wf_skill_category` ADD COLUMN `IsValid` tinyint(1) NOT NULL DEFAULT 1 COMMENT ''有效标志(框架标准列,统一IIsValid契约)''',
    'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;
UPDATE `wf_skill_category` SET `IsValid` = `is_valid` WHERE `is_valid` IS NOT NULL;

-- ---------- wf_skill_input ----------
SET @s := IF((SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_skill_input' AND COLUMN_NAME = 'IsValid') = 0,
    'ALTER TABLE `wf_skill_input` ADD COLUMN `IsValid` tinyint(1) NOT NULL DEFAULT 1 COMMENT ''有效标志(框架标准列,统一IIsValid契约)''',
    'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;
UPDATE `wf_skill_input` SET `IsValid` = `enable`;

-- ---------- wf_skill_output ----------
SET @s := IF((SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_skill_output' AND COLUMN_NAME = 'IsValid') = 0,
    'ALTER TABLE `wf_skill_output` ADD COLUMN `IsValid` tinyint(1) NOT NULL DEFAULT 1 COMMENT ''有效标志(框架标准列,统一IIsValid契约)''',
    'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;
UPDATE `wf_skill_output` SET `IsValid` = `enable`;

-- ---------- wf_skill_reflection ----------
SET @s := IF((SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_skill_reflection' AND COLUMN_NAME = 'IsValid') = 0,
    'ALTER TABLE `wf_skill_reflection` ADD COLUMN `IsValid` tinyint(1) NOT NULL DEFAULT 1 COMMENT ''有效标志(框架标准列,统一IIsValid契约)''',
    'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;
UPDATE `wf_skill_reflection` SET `IsValid` = `enable`;

-- ---------- yzh_queue_task（此前 IsDeleted/IsValid 双缺，GetListAsync 已报 1054） ----------
SET @s := IF((SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'yzh_queue_task' AND COLUMN_NAME = 'IsDeleted') = 0,
    'ALTER TABLE `yzh_queue_task` ADD COLUMN `IsDeleted` tinyint(1) NOT NULL DEFAULT 0 COMMENT ''软删除标记(框架标准列)''',
    'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @s := IF((SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'yzh_queue_task' AND COLUMN_NAME = 'IsValid') = 0,
    'ALTER TABLE `yzh_queue_task` ADD COLUMN `IsValid` tinyint(1) NOT NULL DEFAULT 1 COMMENT ''有效标志(框架标准列,统一IIsValid契约)''',
    'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;
UPDATE `yzh_queue_task` SET `IsValid` = IF(`status` IN ('cancelled'), 0, 1);

-- ---------- 校验 ----------
SELECT 'wf_skill' AS tbl, COUNT(*) AS total, SUM(`IsValid` = 1) AS valid FROM `wf_skill`
UNION ALL SELECT 'wf_skill_category', COUNT(*), SUM(`IsValid` = 1) FROM `wf_skill_category`
UNION ALL SELECT 'wf_skill_input', COUNT(*), SUM(`IsValid` = 1) FROM `wf_skill_input`
UNION ALL SELECT 'wf_skill_output', COUNT(*), SUM(`IsValid` = 1) FROM `wf_skill_output`
UNION ALL SELECT 'wf_skill_reflection', COUNT(*), SUM(`IsValid` = 1) FROM `wf_skill_reflection`
UNION ALL SELECT 'yzh_queue_task', COUNT(*), SUM(`IsValid` = 1) FROM `yzh_queue_task`;

SET FOREIGN_KEY_CHECKS = 1;
