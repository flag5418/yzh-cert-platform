-- =================================================================--
-- 20261105_create_v_sys_user_view_V1.sql — 灾难恢复 · 创建 v_sys_user 视图
--
-- 问题：Sys_User 实体声明 [ViewName("v_sys_user")]，EntityService 读操作走视图；
--       CodeFirst 只建表不建视图 → getCurrentUserInfo 通过视图查用户返回
--       "用户不存在"（视图不存在时 SqlSugar 物理表 fallback 也读不到，
--       或静默返回空结果集）。
-- 修复：创建 v_sys_user 视图（来源：scripts/db/views/v_sys_user.sql，补录版）。
-- 幂等：CREATE OR REPLACE VIEW。
-- ===================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

CREATE OR REPLACE VIEW `v_sys_user` AS
SELECT
    u.Id,
    u.Code,
    u.UserName,
    u.UserTrueName,
    u.UserPwd,
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
    (SELECT GROUP_CONCAT(r.RoleName SEPARATOR ',')
       FROM Sys_RoleUser ru
       JOIN Sys_Role r ON ru.RoleCode = r.Code
      WHERE ru.UserCode = u.Code) AS RoleName,
    o.OrgName,
    o.OrgType,
    o.OrgLevel,
    o.OrgPath,
    o.LeaderName  AS OrgLeaderName,
    o.LeaderPhone AS OrgLeaderPhone
FROM Sys_User u
LEFT JOIN Sys_Organization o
       ON u.OrgCode = o.Code
      AND o.IsDeleted = 0
WHERE u.IsDeleted = 0;

SELECT '✅ v_sys_user 视图已创建' AS result;
