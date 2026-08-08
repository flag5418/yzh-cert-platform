USE `yzh_cert_platform`;

-- 更新菜单名：认证阶段关联 → 认证阶段定义
UPDATE Sys_Menu SET MenuName = '认证阶段定义' WHERE Menu_Id = 323;

-- 验证
SELECT Menu_Id, MenuName, Url FROM Sys_Menu WHERE ParentId = 305 ORDER BY OrderNo;
