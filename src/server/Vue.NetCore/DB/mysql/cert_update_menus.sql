USE `yzh_cert_platform`;

-- 更新菜单名称（按用户要求）
UPDATE Sys_Menu SET MenuName = 'ISO 标准注册' WHERE Menu_Id = 322;
UPDATE Sys_Menu SET MenuName = '认证阶段关联' WHERE Menu_Id = 323;
UPDATE Sys_Menu SET MenuName = '机构-标准关联' WHERE Menu_Id = 324;
UPDATE Sys_Menu SET MenuName = '机构-阶段关联' WHERE Menu_Id = 325;

SELECT Menu_Id, ParentId, MenuName, Url FROM Sys_Menu WHERE Menu_Id IN (322,323,324,325) ORDER BY Menu_Id;
