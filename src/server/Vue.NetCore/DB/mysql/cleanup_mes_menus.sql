-- ============================================================
-- 清除 MES 相关菜单
-- 说明：删除 Sys_Menu 表中所有 MES 业务菜单及其子菜单
-- Menu_Id = 235 是 MES业务 顶级菜单
-- ============================================================

USE `yzh_cert_platform`;

-- 先查看 MES 菜单树（确认后再删除）
SELECT '=== MES 菜单树（待删除） ===' AS '';
SELECT 
    p.`Menu_Id` AS parent_id,
    p.`MenuName` AS parent_name,
    p.`OrderNo` AS parent_order,
    c.`Menu_Id` AS child_id,
    c.`MenuName` AS child_name,
    c.`Url` AS child_url,
    c.`OrderNo` AS child_order
FROM `Sys_Menu` p
LEFT JOIN `Sys_Menu` c ON c.`ParentId` = p.`Menu_Id`
WHERE p.`Menu_Id` = 235
ORDER BY c.`OrderNo` DESC;

-- 查看更深层级的子菜单（MES子菜单的子菜单）
SELECT '=== MES 三级子菜单（待删除） ===' AS '';
SELECT `Menu_Id`, `MenuName`, `ParentId`, `Url`, `OrderNo`
FROM `Sys_Menu`
WHERE `ParentId` IN (
    SELECT `Menu_Id` FROM `Sys_Menu` WHERE `ParentId` = 235
)
ORDER BY `OrderNo`;

-- 删除所有 MES 相关菜单（从最深层开始）
-- Step 1: 删除三级菜单（MES子菜单的子菜单）
DELETE FROM `Sys_Menu`
WHERE `ParentId` IN (
    SELECT child.`Menu_Id` FROM (
        SELECT `Menu_Id` FROM `Sys_Menu` WHERE `ParentId` = 235
    ) child
);

-- Step 2: 删除二级菜单（MES业务的直接子菜单）
DELETE FROM `Sys_Menu` WHERE `ParentId` = 235;

-- Step 3: 删除顶级菜单 MES业务
DELETE FROM `Sys_Menu` WHERE `Menu_Id` = 235;

-- 验证删除结果
SELECT '=== 验证：MES 菜单已删除 ===' AS '';
SELECT `Menu_Id`, `MenuName`
FROM `Sys_Menu`
WHERE `MenuName` LIKE '%MES%' OR `MenuName` LIKE '%生产%' OR `MenuName` LIKE '%仓库%' OR `MenuName` LIKE '%设备%' OR `MenuName` LIKE '%工序%' OR `MenuName` LIKE '%BOM%' OR `MenuName` LIKE '%报工%' OR `MenuName` LIKE '%质检%' OR `MenuName` LIKE '%排班%'
ORDER BY `Menu_Id`;

-- 显示最终顶级菜单
SELECT '=== 最终顶级菜单 ===' AS '';
SELECT `Menu_Id`, `MenuName`, `OrderNo`, `Enable`
FROM `Sys_Menu`
WHERE `ParentId` = 0
ORDER BY `OrderNo` DESC;
