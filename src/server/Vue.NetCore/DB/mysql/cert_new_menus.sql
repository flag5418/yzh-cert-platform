USE `yzh_cert_platform`;

-- 新增 4 个页面菜单（ParentId=305 = 基础配置）
INSERT INTO Sys_Menu (MenuName, Icon, Description, Enable, OrderNo, TableName, ParentId, Url, MenuType, CreateDate, Creator) VALUES
('ISO 标准库', 'el-icon-document', '全局 ISO 标准基础资料管理', 1, 100, 'ISOStandard', 305, '/CertPlatform/Base/ISOStandard', 2, NOW(), '系统'),
('认证阶段管理', 'el-icon-date', '认证流程阶段配置(基于17021-1)', 1, 200, 'CertStage', 305, '/CertPlatform/Base/CertStage', 2, NOW(), '系统'),
('机构-标准关联', 'el-icon-connection', '为机构分配可开展的 ISO 标准', 1, 300, '', 305, '/CertPlatform/Link/OrgStandard', 2, NOW(), '系统'),
('机构-阶段关联', 'el-icon-operation', '为机构配置认证流程阶段', 1, 400, '', 305, '/CertPlatform/Link/OrgStage', 2, NOW(), '系统');

-- 验证
SELECT Menu_Id, ParentId, MenuName, Url FROM Sys_Menu WHERE ParentId = 305 ORDER BY OrderNo;
