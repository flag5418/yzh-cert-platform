-- ============================================================
-- 20261016_sys_log_apipath_V1.sql
-- sys_log 新增 ApiPath 列（记录真实接口路径）+ ApiPath 索引
--
-- 背景（2026-10-16 用户报障）：操作日志页「模块 / 操作 / 目标类型」三列
--   全无接口信息 —— Module=命名空间（Workflow）、Action=HTTP 动词（查询/新增）、
--   TargetType=命名空间（与 Module 重复）；真实接口路径仅埋在 Detail（列表隐藏）。
--   根因：SysLogDbWriter 落库时丢弃了「控制器.动作」与请求路径。
--
-- 代码同步：SysLog.ApiPath 属性 + SysLogDbWriter 落库 + EntityConfig 列表列。
-- 幂等：information_schema 守卫，可重复执行。
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ---------- ① 新增 ApiPath 列（DB 列名 = C# 属性名 = TS 字段名，PascalCase） ----------
SET @s := IF((SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sys_log' AND COLUMN_NAME = 'ApiPath') = 0,
    'ALTER TABLE `sys_log` ADD COLUMN `ApiPath` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL COMMENT ''接口路径（HTTP 请求 Path）'' AFTER `Action`',
    'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

-- ---------- ② 新增 ApiPath 索引（便于按接口排查） ----------
SET @s := IF((SELECT COUNT(*) FROM information_schema.STATISTICS
              WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sys_log' AND INDEX_NAME = 'idx_api_path') = 0,
    'ALTER TABLE `sys_log` ADD INDEX `idx_api_path` (`ApiPath`)',
    'SELECT 1');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

-- ---------- ③ 验证 ----------
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_COMMENT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'sys_log' AND COLUMN_NAME = 'ApiPath';
