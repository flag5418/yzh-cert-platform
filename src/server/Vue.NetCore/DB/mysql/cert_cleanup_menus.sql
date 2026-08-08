USE `yzh_cert_platform`;

-- 1. 删除旧的 ISO 标准管理菜单（已被 ISO 标准注册替代）
DELETE FROM Sys_Menu WHERE Menu_Id = 307;

-- 2. 验证当前基础配置下的菜单
SELECT Menu_Id, MenuName, Url, Enable, MenuType FROM Sys_Menu WHERE ParentId = 305 ORDER BY OrderNo;
