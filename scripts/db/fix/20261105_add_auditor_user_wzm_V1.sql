-- ===================================================================
-- 20261105_add_auditor_user_wzm_V1.sql — 添加专家端审核员账号 wzm
--
-- 背景：专家端登录账号 wzm 缺 → 登录报"用户不存在"。
-- 设计：
--   1. 为专家创建 VirtualOrg 类型的工作区节点（ORG_WZM）
--   2. 创建用户 wzm（密码 123456 → AES 密文同 admin）
--   3. 关联到 ROLE_AUDITOR 角色
-- 幂等：INSERT IGNORE。
-- ===================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 1) 专家工作区（VirtualOrg 叶子）
INSERT IGNORE INTO Sys_Organization (Code, OrgName, OrgType, OrgLevel, OrgPath, Sort, IsValid, IsDeleted, CreateTime)
VALUES ('ORG_WZM', 'wzm专家工作区', 'VirtualOrg', 2, '/Platform/ORG_WZM', 1, 1, 0, NOW());

-- 2) 用户 wzm / 123456（AES 密文同 admin 的 "MtLleSqH5gOzl~tFS_qW8w=="）
INSERT IGNORE INTO Sys_User (Code, UserName, UserPwd, UserTrueName, OrgCode, IsValid, IsDeleted, CreateTime)
VALUES ('U_WZM', 'wzm', 'MtLleSqH5gOzl~tFS_qW8w==', '审核员wzm', 'ORG_WZM', 1, 0, NOW());

-- 3) 角色关联
INSERT IGNORE INTO Sys_RoleUser (Id, RoleCode, UserCode, OrderNo, CreateTime) VALUES
('RU_WZM', 'ROLE_AUDITOR', 'U_WZM', 1, NOW());

SELECT '✅ wzm 审核员账号已创建' AS result;
