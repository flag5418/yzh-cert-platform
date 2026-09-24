-- ============================================================
-- 清库重建 V1（准则 A · 2026-09-24）
-- 用法：mysql < 本脚本 所在目录的 schema/seed 导入在 rebuild_db.sh 中完成
-- 本文件仅作为回填 + 约束收紧清单，schema 导入后执行
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 1. cert_sys_config：Code = ConfigKey（唯一；ConfigKey 最长实测 ≤64）
UPDATE cert_sys_config SET Code = ConfigKey WHERE Code IS NULL OR Code = '';
ALTER TABLE cert_sys_config MODIFY COLUMN Code varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '业务唯一编码=ConfigKey';

-- 2. 核心表 Code 非空（准则 A：定位/更新只走 Code）
ALTER TABLE Sys_User MODIFY COLUMN Code varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '业务唯一编码';
ALTER TABLE Sys_Role MODIFY COLUMN Code varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '业务唯一编码';
ALTER TABLE Sys_Menu MODIFY COLUMN Code varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '业务唯一编码';
ALTER TABLE Sys_Organization MODIFY COLUMN Code varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务唯一编码';

-- 3. 存量 Code 回填（种子若已带 Code 则无影响）
UPDATE Sys_User SET Code = CONCAT('USER_', LPAD(Id, 6, '0')) WHERE Code IS NULL OR Code = '';
UPDATE Sys_Role SET Code = CONCAT('ROLE_', LPAD(Id, 6, '0')) WHERE Code IS NULL OR Code = '';
UPDATE Sys_Menu SET Code = CONCAT('MENU_', LPAD(Id, 6, '0')) WHERE Code IS NULL OR Code = '';

-- 注意：上面 Sys_User/Role/Menu 若先 MODIFY NOT NULL 再 UPDATE 会失败。
-- rebuild_db.sh 顺序：先 UPDATE 回填，再 ALTER NOT NULL。
-- 本文件保留供人工核对，实际执行顺序以 rebuild_db.sh 为准。
