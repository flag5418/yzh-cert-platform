-- ====================================================================
-- 20261106_dedupe_sys_role_V1.sql
--
-- 清理：Sys_Role 中的重复/测试数据（用户 2026-11-06 确认）
--
-- 背景：
--   种子脚本 20261105_disaster_seed_minimal_V1.sql 重复执行两次
--   （16:29:34 / 16:30:32），因 Sys_Role.Code 无唯一索引，
--   INSERT IGNORE 未拦住 → ROLE_SUPER_ADMIN / ROLE_AUDITOR 各 2 条。
--   另有 3 个通过 /api/Role/tree/add 手工验证创建的「审核员」
--   GUID 角色（5e1c… / b8b1… / 0f80…）。
--
-- 保留策略：每组 Code 仅保留最小 Id（最早）1 条 + 删除全部测试 GUID 角色。
-- 关联不受影响：Sys_RoleUser / Sys_RoleMenu 按 RoleCode 关联，被保留的
--               ROLE_SUPER_ADMIN / ROLE_AUDITOR 即为其引用目标。
--
-- 幂等：重复行删除后再次执行不再命中；测试 Code 删除后再次执行删除 0 行。
-- ====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 0) 清理前快照
DROP TABLE IF EXISTS _bak_dedupe_20261106_sys_role;
CREATE TABLE _bak_dedupe_20261106_sys_role AS SELECT * FROM Sys_Role;
SELECT 'before' AS stage, Id, Code, RoleName, CreateTime FROM Sys_Role ORDER BY Id;

-- 1) 同 Code 重复：每组仅保留最小 Id
DELETE r1 FROM Sys_Role r1
INNER JOIN (
    SELECT Code, MIN(Id) AS MinId
    FROM Sys_Role
    GROUP BY Code
    HAVING COUNT(*) > 1
) r2 ON r1.Code = r2.Code
WHERE r1.Id > r2.MinId;

-- 2) 删除测试创建的 GUID 角色（/api/Role/tree/add 手工验证产生）
DELETE FROM Sys_Role
WHERE Code IN (
    '5e1c3e4ebea54f6e92c1b8a6979b3172',
    'b8b15b00617e4d9eab746c29cfb4cee0',
    '0f80ab6b9dc540d6ba14a9b59ba96432'
);

-- 3) 验评：角色清单 + 重复计数（必须为 0）
SELECT 'after' AS stage, Id, Code, RoleName, ParentCode, IsValid, IsDeleted
FROM Sys_Role
ORDER BY Id;

SELECT COUNT(*) AS DupRows
FROM (SELECT Code FROM Sys_Role GROUP BY Code HAVING COUNT(*) > 1) t;
