-- ============================================================================
-- 修复 v_sys_user 视图：补上 IsValid 列 + 完整机构字段
-- 日期：2026-09-20
-- 问题：
--   1. 视图缺少 IsValid 列 → GetByCodeAny 原始 SQL 查询 toggle-valid 报"记录不存在"
--   2. 视图缺少机构扩展字段（OrgType/OrgLevel/OrgPath/LeaderName/LeaderPhone）
-- 修复：
--   a. 后端 GetByCodeAny 改用 ORM（GetOneAnyAsync）→ 不再依赖视图名
--   b. 视图补全字段 → filter 查询能返回完整机构信息
-- ============================================================================

SET NAMES utf8mb4;

DROP VIEW IF EXISTS v_sys_user;

CREATE VIEW v_sys_user AS
SELECT
    -- === Sys_User 物理表字段 ===
    u.User_Id,
    u.Code,
    u.UserName,
    u.UserTrueName,
    u.UserPwd,
    u.Role_Id,
    u.Enable,
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
    u.CreateID,
    u.CreateDate,
    u.Creator,
    u.Modifier,
    u.ModifyDate,
    u.ModifyID,

    -- === 视图扩展字段 ===
    r.RoleName        AS RoleName,
    o.OrgName         AS OrgName,
    o.OrgType         AS OrgType,
    o.OrgLevel        AS OrgLevel,
    o.OrgPath         AS OrgPath,
    o.LeaderName      AS OrgLeaderName,
    o.LeaderPhone     AS OrgLeaderPhone

FROM Sys_User u
LEFT JOIN Sys_Role r ON u.Role_Id = r.Role_Id
LEFT JOIN Sys_Organization o ON u.OrgCode = o.Code COLLATE utf8mb4_unicode_ci
;

SELECT '✅ v_sys_user 视图已更新（补全 IsValid + 机构字段）' AS Result;
