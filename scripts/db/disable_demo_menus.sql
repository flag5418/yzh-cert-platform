-- 清理 Vol 框架示例菜单（基础组件 + 基础页面 demos）
-- 这些是 Vol 自带的示例/教学页面，认证平台不需要

-- 1. 禁用 "基础组件" (id=32) 的所有子菜单
UPDATE sys_menu SET Enable = 0, Modifier = 'system_cleanup', ModifyDate = NOW()
WHERE ParentId = 32 AND Enable = 1;

-- 2. 禁用 "基础组件" 自身
UPDATE sys_menu SET Enable = 0, Modifier = 'system_cleanup', ModifyDate = NOW()
WHERE Menu_Id = 32;

-- 3. 禁用 "基础页面" (MenuType=1, id=113) 及其子菜单
UPDATE sys_menu SET Enable = 0, Modifier = 'system_cleanup', ModifyDate = NOW()
WHERE (Menu_Id = 113 OR ParentId = 113) AND MenuType = 1;

-- 验证结果（所有被禁用的菜单不应再显示）
SELECT Menu_Id, MenuName, Url, Enable FROM sys_menu WHERE Enable = 0 ORDER BY Menu_Id;
