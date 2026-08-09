-- ============================================================
-- 添加标准目录管理菜单
-- ============================================================

-- 获取当前最大ID和体系认证菜单ID
SET @max_id = (SELECT MAX(Menu_Id) FROM Sys_Menu);
SET @cert_parent_id = 304;  -- 体系认证平台菜单ID

-- 插入菜单
INSERT INTO `Sys_Menu` (`MenuName`, `ParentId`, `Url`, `OrderNo`, `Icon`, `MenuType`, `Enable`, `Creator`, `CreateDate`) VALUES 
('标准目录管理', @cert_parent_id, '/CertPlatform/Standard', 90, 'el-icon-setting', 0, 1, '超级管理员', NOW()),
('目录配置', @max_id + 1, '/CertPlatform/Standard/DirectoryConfig', 1, '', 1, 1, '超级管理员', NOW()),
('文件夹结构', @max_id + 2, '/CertPlatform/Standard/DirectoryTree', 2, '', 1, 1, '超级管理员', NOW());

-- 分配权限（角色ID: 10=审核员, 20=管理员）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`) VALUES
(10, @max_id + 1, '["Search"]', '超级管理员', NOW()),
(10, @max_id + 2, '["Search"]', '超级管理员', NOW()),
(20, @max_id + 1, '["Search","Add","Delete","Update"]', '超级管理员', NOW()),
(20, @max_id + 2, '["Search","Add","Delete","Update"]', '超级管理员', NOW());

-- 验证结果
SELECT 
    '✅ 菜单创建完成' AS message,
    @max_id + 1 AS standard_dir_menu_id,
    @max_id + 2 AS config_menu_id,
    @max_id + 3 AS tree_menu_id;
