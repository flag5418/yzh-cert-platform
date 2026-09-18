-- ============================================================
-- 20260917_sys_standard_columns.sql
-- sys_config / sys_role 补齐框架标准列（IsDeleted/IsValid）
-- 背景：与 wf_* 同一批问题——ORM 按 IIsValid 契约自动过滤，表缺标准列导致启动期查询 1054
-- 幂等：information_schema 守卫
-- ============================================================
SET FOREIGN_KEY_CHECKS = 0;

-- ---------- sys_config ----------
SET @s := IF((SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sys_config' AND COLUMN_NAME = 'IsDeleted') = 0,
    'ALTER TABLE `sys_config` ADD COLUMN `IsDeleted` tinyint(1) NOT NULL DEFAULT 0 COMMENT ''软删除标记(框架标准列)''',
    'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @s := IF((SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sys_config' AND COLUMN_NAME = 'IsValid') = 0,
    'ALTER TABLE `sys_config` ADD COLUMN `IsValid` tinyint(1) NOT NULL DEFAULT 1 COMMENT ''有效标志(框架标准列)''',
    'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

-- ---------- sys_role ----------
SET @s := IF((SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sys_role' AND COLUMN_NAME = 'IsDeleted') = 0,
    'ALTER TABLE `sys_role` ADD COLUMN `IsDeleted` tinyint(1) NOT NULL DEFAULT 0 COMMENT ''软删除标记(框架标准列)''',
    'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @s := IF((SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sys_role' AND COLUMN_NAME = 'IsValid') = 0,
    'ALTER TABLE `sys_role` ADD COLUMN `IsValid` tinyint(1) NOT NULL DEFAULT 1 COMMENT ''有效标志(框架标准列)''',
    'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET FOREIGN_KEY_CHECKS = 1;
SELECT 'sys_config/sys_role 标准列就绪' AS done;
