-- ============================================================================
-- count_baseline.sql — 重建后基础计数（★ 只读）
-- ============================================================================
-- 由 rebuild_db.sh 第 7 步调用；也可单独跑：
--   docker exec -i yzh-mysql mysql -uroot -p*** yzh_cert_platform -N -B \
--     < scripts/db/verify/count_baseline.sql
--
-- 用途：一眼确认「重建是否真的产出了结构」+ 抓「Code 为空的脏数据」
--       （准则 A：Code 是业务主键，为空即不可定位）。
--
-- 输出：每行两项 —— Item <TAB> Cnt
-- 说明：库名用 DATABASE() 取当前库，故本文件无占位符、可跨库复用。
-- ============================================================================

SELECT 'tables' AS Item, COUNT(*) AS Cnt
  FROM information_schema.tables
 WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE'
UNION ALL SELECT 'views', COUNT(*)
  FROM information_schema.tables
 WHERE table_schema = DATABASE() AND table_type = 'VIEW'
UNION ALL SELECT 'config_null_code', COUNT(*) FROM cert_sys_config WHERE Code IS NULL OR Code = ''
UNION ALL SELECT 'user_null_code',   COUNT(*) FROM Sys_User        WHERE Code IS NULL OR Code = ''
UNION ALL SELECT 'role_null_code',   COUNT(*) FROM Sys_Role        WHERE Code IS NULL OR Code = ''
UNION ALL SELECT 'menu_null_code',   COUNT(*) FROM Sys_Menu        WHERE Code IS NULL OR Code = ''
UNION ALL SELECT 'config_total',     COUNT(*) FROM cert_sys_config
UNION ALL SELECT 'users',            COUNT(*) FROM Sys_User
UNION ALL SELECT 'roles',            COUNT(*) FROM Sys_Role
UNION ALL SELECT 'menus',            COUNT(*) FROM Sys_Menu
UNION ALL SELECT 'apis',             COUNT(*) FROM sys_api;
