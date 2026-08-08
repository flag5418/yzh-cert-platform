USE `yzh_cert_platform`;

-- 修复：新菜单的 MenuType 应该是 0 (PC菜单) 而不是 2 (移动端菜单)
UPDATE Sys_Menu SET MenuType = 0 WHERE Menu_Id IN (322, 323, 324, 325);

-- 验证
SELECT Menu_Id, MenuName, Url, MenuType FROM Sys_Menu WHERE Menu_Id IN (322, 323, 324, 325);
