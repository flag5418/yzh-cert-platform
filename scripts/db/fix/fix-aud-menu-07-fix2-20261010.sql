SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
-- 把当前 MENU_AUD_07（队列监控）改成 MENU_AUD_13
UPDATE Sys_Menu SET Code = 'MENU_AUD_13', ParentCode = 'MENU_AUD_00', MenuName = '队列监控', Icon = 'Cpu', Url = '/queue', OrderNo = 310, Tag = 'auditor', IsValid = 1, UpdateTime = NOW(), UpdateBy = 'fix_20261010' WHERE Code = 'MENU_AUD_07' AND MenuName = '队列监控';
-- 更新队列监控的 Sys_RoleMenu 引用
UPDATE Sys_RoleMenu SET MenuCode = 'MENU_AUD_13' WHERE MenuCode = 'MENU_AUD_07';
-- 插入阶段标准关联为 MENU_AUD_07
INSERT INTO Sys_Menu (Code, ParentCode, MenuName, Icon, OrderNo, Url, Tag, Description, IsValid, IsDeleted, CreateTime, CreateBy) VALUES ('MENU_AUD_07', 'MENU_AUD_00', '阶段标准关联', 'Connection', 250, '/enterprise-stages', 'auditor', '企业 × 阶段 × 标准 三元关联', 1, 0, NOW(), 'fix_20261010');
-- 授权阶段标准关联
INSERT IGNORE INTO Sys_RoleMenu (Id, RoleCode, MenuCode, OrderNo, CreateTime, CreateBy) VALUES (CONCAT('RM_', REPLACE(UUID(), '-', '')), 'ROLE_AUDIT_CLIENT_ADMIN', 'MENU_AUD_07', 250, NOW(), 'fix_20261010');
