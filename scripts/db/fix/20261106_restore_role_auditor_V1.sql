-- ====================================================================
-- 20261106_restore_role_auditor_V1.sql
--
-- 背景：按 20261106_dedupe_sys_role_V1.sql 清理「重复/测试角色」时，
--       每组 Code 保留最小 Id，但 ROLE_AUDITOR（Id=2）此前已被页面 UI
--       手动软删除（IsDeleted=1, DeleteBy=U_ADMIN, DeleteTime=2026-11-06 09:29:12），
--       清理后就只剩 ROLE_SUPER_ADMIN 可见，与目标「只剩 ROLE_SUPER_ADMIN、
--       ROLE_AUDITOR」不符。
-- 动作：把被保留的种子角色恢复为未删除（IsValid 保持 1）；
--       同步刷新 UpdateTime（业务时间 = 本地时间）。
-- 幂等：仅 IsDeleted=1 时更新。
-- ====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

UPDATE Sys_Role
SET IsDeleted = 0,
    DeleteBy  = NULL,
    DeleteTime = NULL,
    UpdateTime = NOW()
WHERE Code = 'ROLE_AUDITOR'
  AND IsDeleted = 1;

-- 验评：最终角色清单（期望 ROLE_SUPER_ADMIN / ROLE_AUDITOR 各 1 条、IsDeleted=0）
SELECT Id, Code, RoleName, ParentCode, IsValid, IsDeleted, CreateTime, UpdateTime
FROM Sys_Role
ORDER BY Id;
