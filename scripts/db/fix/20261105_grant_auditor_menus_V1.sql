-- ====================================================================
-- 20261105_grant_auditor_menus_V1.sql
-- 修复：专家端（auditor）登录后无任何菜单
-- 根因：Sys_RoleMenu 表为空，ROLE_AUDITOR 角色未关联任何菜单
--       超级管理员走 IsSuperAdmin 绕过路径（不受影响），
--       审核员角色走权限过滤路径 → 返回 0 条 → 侧栏空白
-- 幂等：先删后插，重复运行不产生重复数据
-- ====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ① 清理该角色的旧关联（幂等前提）
DELETE FROM Sys_RoleMenu WHERE RoleCode = 'ROLE_AUDITOR';

-- ② 审核员角色（ROLE_AUDITOR）关联全部 auditor 标签菜单
INSERT INTO Sys_RoleMenu (Id, RoleCode, MenuCode, OrderNo, CreateTime, CreateBy)
SELECT
    UUID(),
    'ROLE_AUDITOR',
    Code,
    OrderNo,
    NOW(),
    'fix_20261105'
FROM Sys_Menu
WHERE Tag = 'auditor'
  AND IsDeleted = 0;

-- ③ 验评：审核员角色应已关联 11 条菜单
SELECT 'ROLE_AUDITOR menu count' AS CheckPoint, COUNT(*) AS Cnt
FROM Sys_RoleMenu WHERE RoleCode = 'ROLE_AUDITOR';
