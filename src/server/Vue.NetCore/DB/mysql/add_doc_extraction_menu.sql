-- ============================================================
-- 添加文档提取规则管理菜单
-- 执行前请确保 cert_doc_extraction_tables.sql 已执行
-- ============================================================

-- 获取当前最大ID和体系认证菜单ID
SET @max_id = (SELECT MAX(Menu_Id) FROM Sys_Menu);
SET @cert_parent_id = 304;  -- 体系认证平台菜单ID

-- 插入菜单（作为标准目录管理的子菜单）
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `ParentId`, `Url`, `OrderNo`, `Icon`, `MenuType`, `Enable`, `Creator`, `CreateDate`) VALUES 
(@max_id + 1, '文档提取规则', @cert_parent_id, '/CertPlatform/Standard/DocExtractionRule', 95, 'el-icon-document-checked', 1, 1, '超级管理员', NOW());

-- 分配权限（角色ID: 1=超级管理员, 10=审核员, 20=管理员）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`) VALUES
(1, @max_id + 1, '["Search","Add","Delete","Update"]', '超级管理员', NOW()),
(10, @max_id + 1, '["Search"]', '超级管理员', NOW()),
(20, @max_id + 1, '["Search","Add","Delete","Update"]', '超级管理员', NOW());

-- 验证结果
SELECT 
    '✅ 文档提取规则菜单创建完成' AS message,
    @max_id + 1 AS menu_id,
    '文档提取规则' AS menu_name,
    '/CertPlatform/Standard/DocExtractionRule' AS url;
