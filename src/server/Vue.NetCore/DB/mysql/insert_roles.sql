-- Insert required roles for the system
-- Run this script to set up the role-based access control

-- 1. Super Admin (主管理员)
INSERT INTO Sys_Role (Role_Id, RoleName, ParentId, Enable, OrderNo, Creator, CreateDate, Remark)
VALUES (1, '主管理员', 0, 1, 1, 'system', NOW(), '超级管理员：拥有系统全部权限')
ON DUPLICATE KEY UPDATE RoleName = '主管理员', Enable = 1;

-- 2. Maintainer (维护人员)
INSERT INTO Sys_Role (Role_Id, RoleName, ParentId, Enable, OrderNo, Creator, CreateDate, Remark)
VALUES (10, '维护人员', 0, 1, 2, 'system', NOW(), '系统维护人员：负责系统配置和维护')
ON DUPLICATE KEY UPDATE RoleName = '维护人员', Enable = 1;

-- 3. Auditor Admin (体系认证客户端管理员)
INSERT INTO Sys_Role (Role_Id, RoleName, ParentId, Enable, OrderNo, Creator, CreateDate, Remark)
VALUES (200, '体系认证客户端管理员', 0, 1, 3, 'system', NOW(), '审核端管理员：拥有审核端全部功能权限，可管理机构和审核员')
ON DUPLICATE KEY UPDATE RoleName = '体系认证客户端管理员', Enable = 1;

-- 4. Auditor (审核员)
INSERT INTO Sys_Role (Role_Id, RoleName, ParentId, Enable, OrderNo, Creator, CreateDate, Remark)
VALUES (20, '审核员', 0, 1, 4, 'system', NOW(), '基础审核员：执行审核任务，查看企业和报告')
ON DUPLICATE KEY UPDATE RoleName = '审核员', Enable = 1;

-- Verify the roles
SELECT Role_Id, RoleName, Enable, Remark FROM Sys_Role ORDER BY Role_Id;
