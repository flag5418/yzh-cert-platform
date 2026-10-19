-- ========================================================================
-- 2026-10-17 新增「服务监控」菜单
-- ========================================================================
-- 修订 V1.2（2026-10-17）：父节点 = MENU_00001（平台管理），OrderNo=750
-- ========================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 1. 清理旧位置（如已存在）
DELETE FROM `Sys_Menu` WHERE Url = '/business/server-monitor';

-- 2. 插入到「平台管理」下（数据字典=700 之后、参数配置之前，取半序 750）
INSERT INTO `Sys_Menu` (`Code`, `ParentCode`, `MenuName`, `Url`, `Icon`, `OrderNo`, `Tag`, `IsValid`)
SELECT
    UUID(),
    'MENU_00001',
    '服务监控',
    '/business/server-monitor',
    'Monitor',
    750,
    'admin',
    1
WHERE NOT EXISTS (
    SELECT 1 FROM `Sys_Menu` WHERE Url = '/business/server-monitor'
);

-- 验证
SELECT Code, ParentCode, MenuName, Url, Icon, OrderNo
FROM `Sys_Menu`
WHERE ParentCode = 'MENU_00001'
ORDER BY OrderNo;
