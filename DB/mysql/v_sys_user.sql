-- ============================================================================
-- v_sys_user：用户管理视图
-- 日期：2026-09-10
-- 说明：关联 Sys_Organization（机构名称）、Sys_Role（角色名称）
--       视图用于查询（含 OrgName、RoleName 等关联信息）
--       增删改走 Sys_User 物理表（[NotMapped] 字段自动忽略）
-- ============================================================================

SET NAMES utf8mb4;

DROP VIEW IF EXISTS v_sys_user;

CREATE VIEW v_sys_user AS
SELECT
    -- === Sys_User 物理表字段（与实体属性一一对应） ===
    u.User_Id,
    u.Code,
    u.UserName,
    u.UserTrueName,
    u.UserPwd,
    u.Role_Id,
    u.Enable,
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

    -- === 视图扩展字段（[NotMapped]，仅查询时填充） ===
    r.RoleName      AS RoleName,    -- 角色名称（来自 Sys_Role）
    o.OrgName       AS OrgName      -- 机构名称（来自 Sys_Organization）

FROM Sys_User u
LEFT JOIN Sys_Role r ON u.Role_Id = r.Role_Id
LEFT JOIN Sys_Organization o ON u.OrgCode = o.Code COLLATE utf8mb4_unicode_ci
;

-- ============================================================================
-- 视图创建验证
-- ============================================================================
SELECT '✅ v_sys_user 创建完成' AS Result;
