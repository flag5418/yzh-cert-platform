-- ============================================================================
-- 20261009_role_standard_directory_grant_V1.sql
-- 给「体系认证客户端管理员」补「标准目录」域的全部已登记端点（幂等）
--
-- 背景（2026-10-09 用户报障「用 wzm 登录时文档提取规则页左树加载不出来」）：
--   实测 `/api/Admin/Workflow/StandardDirectory/organization-tree` 与
--   `/stage-files/{code}` 对 wzm **均返回 403**（不是 401，说明登录态正常，是权限被拒）。
--
-- 根因：**权限数据从未配置**（不是代码缺陷）
--   ① `PermissionFilter.OnAuthorizationAsync` → `PermissionService.HasPermissionAsync`
--      判据两层：`sys_user_permission`（用户直配）优先，回落 `sys_role_api`（角色）。
--   ② `sys_api` 里这 45 个端点在启动时由 `ApiScanner` **已自动登记**（IsValid=1）——
--      所以「接口有登记」与「角色有授权」是两件事，前者自动、后者要人勾。
--   ③ `StandardDirectoryController` 自身注释（2026-09-27 加 `[RequirePermission]` 那次）
--      已写明：「当前该控制器的角色-接口关联数为 0，无可断裂项」—— 即当时就没配，一直没配。
--   ④ 实测 `ROLE_AUDIT_CLIENT_ADMIN` 共 95 条授权，分布在其它 Controller
--      （`tree/add/update/delete/filter/export/toggle-valid/action/config` 等通用端点，
--      甚至含 `CertStage/delete`、`ISOClause/delete`、`EnterpriseOriginal/delete`），
--      **唯独 `StandardDirectory` 域 0 条** ⇒ 属「按功能域批量勾选时漏了一组」。
--
-- 修法：按该角色既有授权粒度**整域补全**（45 条），⛔ 不是扩权，是补回遗漏的一组。
--
-- 影响面：`ROLE_AUDIT_CLIENT_ADMIN` 名下**只有 `wzm` 一个用户**（实测），故实际影响 1 人。
-- 可逆性：回滚见文件末尾。
--
-- ⚠️ 生效性：`ApiRepository.GetApiCodesByRoleCodeAsync` 是**纯 DB 查询、无进程内缓存**
--    ⇒ 本脚本执行后**立即生效，无需重启**。
-- ⚠️ ⛔ **不要手工往 `sys_user_permission`（展开表）插这 45 条**：
--    它是派生缓存，由 `PermissionCacheService.ExpandPermissionsInnerAsync` 从
--    `Sys_RoleUser + sys_role_api` 推导。手工插会让「撤销授权后残留旧权限」
--    （`PermissionCacheService.cs:66-68` 已明确警告过这个坑）。当前无需刷新：
--    `HasPermissionAsync` 在展开表未命中时会回落 `sys_role_api`，本脚本正是改那一层。
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ---------------------------------------------------------------------------
-- 1. 补授权：ROLE_AUDIT_CLIENT_ADMIN × 标准目录域全部有效端点
--    `NOT EXISTS` 保证**幂等**（重复执行不会产生重复行）
-- ---------------------------------------------------------------------------
INSERT INTO `sys_role_api` (`RoleCode`, `ApiCode`, `CreateTime`)
SELECT 'ROLE_AUDIT_CLIENT_ADMIN', a.`Code`, NOW()
  FROM `sys_api` a
 WHERE a.`Path` LIKE 'api/Admin/Workflow/StandardDirectory/%'
   AND a.`IsValid` = 1
   AND NOT EXISTS (
       SELECT 1 FROM `sys_role_api` ra
        WHERE ra.`RoleCode` = 'ROLE_AUDIT_CLIENT_ADMIN'
          AND ra.`ApiCode` = a.`Code`
   );

-- ---------------------------------------------------------------------------
-- 2. 验证（执行后应返回 45 / 45）
-- ---------------------------------------------------------------------------
-- SELECT
--   (SELECT COUNT(*) FROM sys_api
--     WHERE Path LIKE 'api/Admin/Workflow/StandardDirectory/%' AND IsValid = 1) AS apis_total,
--   (SELECT COUNT(*) FROM sys_role_api ra JOIN sys_api a ON a.Code = ra.ApiCode
--     WHERE ra.RoleCode = 'ROLE_AUDIT_CLIENT_ADMIN'
--       AND a.Path LIKE 'api/Admin/Workflow/StandardDirectory/%')             AS granted;

-- ---------------------------------------------------------------------------
-- 3. 回滚（如需撤销本次授权）
-- ---------------------------------------------------------------------------
-- DELETE ra FROM sys_role_api ra
--   JOIN sys_api a ON a.Code = ra.ApiCode
--  WHERE ra.RoleCode = 'ROLE_AUDIT_CLIENT_ADMIN'
--    AND a.Path LIKE 'api/Admin/Workflow/StandardDirectory/%';
