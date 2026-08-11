-- Phase 3: 系统参数配置 + 转换队列监控 菜单
-- sys_menu 表使用 PascalCase 列名
-- MenuType: 0=PC端菜单, 1=移动端菜单（PC端访问只显示 MenuType=0 的菜单）
-- 
-- ⚠️ 重要：这两个菜单属于「体系认证平台」专属功能，不是通用系统设置
-- ParentId = 304 (体系认证平台)，不是 61 (系统设置)

-- 查找"体系认证平台"父菜单 ID
SET @parent_id = (SELECT Menu_Id FROM sys_menu WHERE MenuName = '体系认证平台' AND ParentId = 0 LIMIT 1);

-- 系统参数配置
INSERT INTO sys_menu (MenuName, ParentId, Url, Enable, MenuType, OrderNo, CreateDate, Creator, Modifier, ModifyDate)
SELECT '系统参数配置', @parent_id, '/CertPlatform/SysConfig', 1, 0, 100, NOW(), 'admin', 'admin', NOW()
FROM DUAL WHERE NOT EXISTS (
  SELECT 1 FROM sys_menu WHERE MenuName = '系统参数配置' AND Url = '/CertPlatform/SysConfig'
);

-- 转换队列监控
INSERT INTO sys_menu (MenuName, ParentId, Url, Enable, MenuType, OrderNo, CreateDate, Creator, Modifier, ModifyDate)
SELECT '转换队列监控', @parent_id, '/CertPlatform/ConvertQueueMonitor', 1, 0, 110, NOW(), 'admin', 'admin', NOW()
FROM DUAL WHERE NOT EXISTS (
  SELECT 1 FROM sys_menu WHERE MenuName = '转换队列监控' AND Url = '/CertPlatform/ConvertQueueMonitor'
);

-- 验证
SELECT Menu_Id, MenuName, ParentId, Url, MenuType, Enable FROM sys_menu WHERE Menu_Id IN (331, 332);

-- ============================================================
-- 修复脚本：确保菜单归属和类型正确
-- 执行日期: 2026-08-11
-- ============================================================
-- 1. 将菜单移到「体系认证平台」下（如果之前错误地放在系统设置下）
UPDATE sys_menu SET ParentId = 304 WHERE Menu_Id IN (331, 332) AND ParentId != 304;

-- 2. 修正 MenuType 为 PC 端（如果之前错误设为移动端）
UPDATE sys_menu SET MenuType = 0 WHERE Url IN (
  '/CertPlatform/SysConfig',
  '/CertPlatform/ConvertQueueMonitor',
  '/CertPlatform/Standard/DirectoryConfig',
  '/CertPlatform/Standard/DirectoryTree'
) AND MenuType = 1;
