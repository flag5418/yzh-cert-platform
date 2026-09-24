-- ===================================================================
-- 统一启用/禁用唯一字段：IsValid（消除 6 表 Enable 冗余）
-- 日期：2026-09-24
-- 背景：
--   scripts/db/add_isvalid_to_org_and_user.sql 的同步 UPDATE 曾被注释未执行，
--   导致 Enable/IsValid 分叉：部分行 Enable=0 而 IsValid=1，
--   框架按 IsValid 过滤后看不到禁用行，业务又绕回读 Enable。
--
-- 执行（确认无误后分步执行）：
--   docker exec -i yzh-mysql mysql -uroot -p'Yzh123456.' --default-character-set=utf8mb4 yzh_cert_platform \
--     < scripts/db/unify_enable_to_isvalid_V1.sql
--
-- 步骤：
--   0) 备份 6 表
--   1) 存量同步：Enable → IsValid（以 Enable 为历史真源）
--   2) 校验 mismatch = 0（脚本 SELECT 报告）
--   3) 重建依赖视图（去 Enable 列）
--   4) DROP COLUMN Enable（校验通过后手工去掉注释执行）
-- ===================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
USE yzh_cert_platform;

-- ─── 0) 备份（已存在则跳过） ───
CREATE TABLE IF NOT EXISTS _bak_20260924_sys_organization AS SELECT * FROM Sys_Organization;
CREATE TABLE IF NOT EXISTS _bak_20260924_sys_user AS SELECT * FROM Sys_User;
CREATE TABLE IF NOT EXISTS _bak_20260924_sys_role AS SELECT * FROM Sys_Role;
CREATE TABLE IF NOT EXISTS _bak_20260924_sys_menu AS SELECT * FROM Sys_Menu;
CREATE TABLE IF NOT EXISTS _bak_20260924_sys_dictionary AS SELECT * FROM Sys_Dictionary;
CREATE TABLE IF NOT EXISTS _bak_20260924_sys_dictionarylist AS SELECT * FROM Sys_DictionaryList;

-- ─── 1) 存量同步：Enable → IsValid（历史 Enable=0 表示禁用） ───
-- Enable 可空：NULL 视为启用（与 IsValid 默认 1 对齐）
UPDATE Sys_Organization     SET IsValid = IFNULL(Enable, 1) WHERE IFNULL(Enable, 1) <> IsValid;
UPDATE Sys_User             SET IsValid = IFNULL(Enable, 1) WHERE IFNULL(Enable, 1) <> IsValid;
UPDATE Sys_Role             SET IsValid = IFNULL(Enable, 1) WHERE IFNULL(Enable, 1) <> IsValid;
UPDATE Sys_Menu             SET IsValid = IFNULL(Enable, 1) WHERE IFNULL(Enable, 1) <> IsValid;
UPDATE Sys_Dictionary       SET IsValid = IFNULL(Enable, 1) WHERE IFNULL(Enable, 1) <> IsValid;
UPDATE Sys_DictionaryList   SET IsValid = IFNULL(Enable, 1) WHERE IFNULL(Enable, 1) <> IsValid;

-- ─── 2) 校验：mismatch 必须全 0 ───
SELECT 'Sys_Organization' AS tbl, SUM(IFNULL(Enable,1) <> IsValid) AS mismatch, COUNT(*) AS n FROM Sys_Organization
UNION ALL SELECT 'Sys_User', SUM(IFNULL(Enable,1) <> IsValid), COUNT(*) FROM Sys_User
UNION ALL SELECT 'Sys_Role', SUM(IFNULL(Enable,1) <> IsValid), COUNT(*) FROM Sys_Role
UNION ALL SELECT 'Sys_Menu', SUM(IFNULL(Enable,1) <> IsValid), COUNT(*) FROM Sys_Menu
UNION ALL SELECT 'Sys_Dictionary', SUM(IFNULL(Enable,1) <> IsValid), COUNT(*) FROM Sys_Dictionary
UNION ALL SELECT 'Sys_DictionaryList', SUM(IFNULL(Enable,1) <> IsValid), COUNT(*) FROM Sys_DictionaryList;

-- ─── 3) 重建 v_sys_user（去掉 u.Enable；对齐 20260921 V4 视图定义） ───
DROP VIEW IF EXISTS v_sys_user;
CREATE VIEW v_sys_user AS
SELECT
    u.Id,
    u.Code,
    u.UserName,
    u.UserTrueName,
    u.UserPwd,
    u.RoleId,
    u.IsValid,
    u.IsDeleted,
    u.DeleteTime,
    u.DeleteBy,
    u.OrgCode,
    u.Gender,
    u.PhoneNo,
    u.Email,
    u.HeadImageUrl,
    u.Address,
    u.Remark,
    u.LastLoginDate,
    u.LastModifyPwdDate,
    u.OrderNo,
    u.Token,
    u.CreateTime,
    u.CreateBy,
    u.UpdateTime,
    u.UpdateBy,
    u.DeptId,
    u.UserType,
    r.RoleName        AS RoleName,
    o.OrgName         AS OrgName,
    o.OrgType         AS OrgType,
    o.OrgLevel        AS OrgLevel,
    o.OrgPath         AS OrgPath,
    o.LeaderName      AS OrgLeaderName,
    o.LeaderPhone     AS OrgLeaderPhone
FROM Sys_User u
LEFT JOIN Sys_Role r ON u.RoleId = r.Id
LEFT JOIN Sys_Organization o ON u.OrgCode = o.Code COLLATE utf8mb4_general_ci;

-- ─── 4) DROP COLUMN（校验 mismatch=0 后再执行；默认注释掉） ───
-- 注意：业务表 wf_/cert_standard_directory_ 等的 Enable 属阶段3，不在本脚本范围。
-- ALTER TABLE Sys_Organization   DROP COLUMN Enable;
-- ALTER TABLE Sys_User           DROP COLUMN Enable;
-- ALTER TABLE Sys_Role           DROP COLUMN Enable;
-- ALTER TABLE Sys_Menu           DROP COLUMN Enable;
-- ALTER TABLE Sys_Dictionary     DROP COLUMN Enable;
-- ALTER TABLE Sys_DictionaryList DROP COLUMN Enable;

SELECT '✅ 同步 + v_sys_user 重建完成（DROP COLUMN 须校验后手工执行）' AS Result;
