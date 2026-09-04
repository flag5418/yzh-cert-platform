-- =====================================================================
-- V1: 新增"体系认证客户端管理员"角色 + 确认 Sys_User 表结构
-- 日期: 2026-09-04
-- =====================================================================

-- 1. 新增角色"体系认证客户端管理员"（Role_Id = 200，审核员=100，超级管理员=1）
--    此角色为机构认证人员的管理员，拥有审核端全部功能权限
INSERT INTO Sys_Role (Role_Id, Role_Name, Enable, Creator, CreateDate, Remark)
VALUES (200, '体系认证客户端管理员', 1, 'system', NOW(), '机构端管理员：拥有审核任务、不符合项、报告等全部审核功能权限，供机构管理人员使用')
ON DUPLICATE KEY UPDATE Role_Name = '体系认证客户端管理员', Remark = '机构端管理员：拥有审核任务、不符合项、报告等全部审核功能权限，供机构管理人员使用';

-- 2. 确认 Sys_User 表 OrgCode 字段存在 (sys_user.org_code)
--    注：Sys_User 实体中对应的属性名为 OrgCode (PascalCase)
SELECT 'Check sys_user table structure:' AS info;
SHOW COLUMNS FROM sys_user LIKE '%org%';

-- 3. 清理 Sys_User 如果有重复的 orgcode 字段（小写），统一用 org_code
--    （仅当存在 orgcode 列时才执行）
-- ALTER TABLE sys_user DROP COLUMN orgcode;

-- 4. 查看现有角色列表确认
SELECT Role_Id, Role_Name, Enable FROM Sys_Role ORDER BY Role_Id;
