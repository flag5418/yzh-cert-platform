-- Phase 3: 系统参数配置 + 转换队列监控 菜单
-- sys_menu 表使用 PascalCase 列名

-- 查找或创建"系统设置"父菜单
SET @parent_id = (SELECT Menu_Id FROM sys_menu WHERE MenuName = '系统设置' AND ParentId = 0 LIMIT 1);

INSERT INTO sys_menu (MenuName, ParentId, Url, Enable, MenuType, OrderNo, CreateDate, Creator, Modifier, ModifyDate)
SELECT '系统设置', 0, '', 1, 0, 90, NOW(), 'admin', 'admin', NOW()
FROM DUAL WHERE NOT EXISTS (
  SELECT 1 FROM sys_menu WHERE MenuName = '系统设置' AND ParentId = 0
);

SET @parent_id = (SELECT Menu_Id FROM sys_menu WHERE MenuName = '系统设置' AND ParentId = 0 LIMIT 1);

-- 系统参数配置
INSERT INTO sys_menu (MenuName, ParentId, Url, Enable, MenuType, OrderNo, CreateDate, Creator, Modifier, ModifyDate)
SELECT '系统参数配置', @parent_id, '/CertPlatform/SysConfig', 1, 1, 1, NOW(), 'admin', 'admin', NOW()
FROM DUAL WHERE NOT EXISTS (
  SELECT 1 FROM sys_menu WHERE MenuName = '系统参数配置' AND Url = '/CertPlatform/SysConfig'
);

-- 转换队列监控
INSERT INTO sys_menu (MenuName, ParentId, Url, Enable, MenuType, OrderNo, CreateDate, Creator, Modifier, ModifyDate)
SELECT '转换队列监控', @parent_id, '/CertPlatform/ConvertQueueMonitor', 1, 1, 2, NOW(), 'admin', 'admin', NOW()
FROM DUAL WHERE NOT EXISTS (
  SELECT 1 FROM sys_menu WHERE MenuName = '转换队列监控' AND Url = '/CertPlatform/ConvertQueueMonitor'
);

-- 验证
SELECT Menu_Id, MenuName, ParentId, Url, Enable FROM sys_menu WHERE ParentId = @parent_id;
