-- ============================================================
-- 体系认证平台 - Phase 1: 角色权限矩阵配置
-- 版本: V1.0
-- 日期: 2026-07-30
-- 说明: 为9个角色配置菜单和按钮权限
-- ============================================================

USE `yzh_cert_platform`;

-- ============================================================
-- 权限说明：
-- AuthValue 字段格式：View,Add,Edit,Delete,Export,Upload,Search,Audit
--   - View: 查看权限（必须）
--   - Add: 新增权限
--   - Edit: 编辑权限
--   - Delete: 删除权限
--   - Export: 导出权限
--   - Upload: 上传权限
--   - Search: 查询权限
--   - Audit: 审核权限
-- ============================================================

-- 菜单结构回顾：
-- 304 体系认证平台 (父菜单)
-- ├── 305 基础配置
-- │   ├── 306 认证机构管理
-- │   ├── 307 ISO标准管理
-- │   └── 308 工作流配置
-- ├── 309 用户权限
-- │   └── 310 审核员管理
-- ├── 311 数据监控
-- │   └── 312 任务状态监控
-- ├── 313 我的工作台
-- │   └── 314 待办任务
-- ├── 315 企业档案
-- │   └── 316 企业列表
-- ├── 317 审核执行
-- │   ├── 318 审核任务
-- │   └── 319 不符合项管理
-- └── 320 报告生成
--     └── 321 报告列表

-- ============================================================
-- 1. 超级管理员(100): 所有权限（包括系统设置、MES等）
-- ============================================================
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
SELECT 100, Menu_Id, 'View,Add,Edit,Delete,Export,Upload,Search,Audit', 'system', NOW()
FROM `Sys_Menu` WHERE 1=1;

-- ============================================================
-- 2. 总管理员(101): 体系认证全部功能 + 基础页面 + 部分系统设置
-- 排除：MES业务、系统设置中的敏感操作
-- ============================================================
-- 体系认证模块全部权限
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
SELECT 101, Menu_Id, 'View,Add,Edit,Delete,Export,Upload,Search,Audit', 'system', NOW()
FROM `Sys_Menu` WHERE Menu_Id = 304 OR ParentId = 304;

-- 基础页面权限
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
SELECT 101, Menu_Id, 'View', 'system', NOW()
FROM `Sys_Menu` WHERE ParentId = 113 OR Menu_Id = 113;

-- 系统设置部分权限（用户管理、角色管理等，排除系统配置）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
SELECT 101, Menu_Id, 'View,Add,Edit,Delete,Search', 'system', NOW()
FROM `Sys_Menu` WHERE (ParentId = 61 AND MenuName NOT IN ('系统配置', 'SQL监控', 'Redis缓存'))
   OR (Menu_Id = 61);

-- ============================================================
-- 3. 运维人员(102): 系统设置 + 数据监控 + 日志查看
-- ============================================================
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
SELECT 102, Menu_Id, 'View,Add,Edit,Delete,Search', 'system', NOW()
FROM `Sys_Menu` 
WHERE ParentId = 61 OR Menu_Id = 61;  -- 系统设置全部

-- 数据监控
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
SELECT 102, Menu_Id, 'View,Search', 'system', NOW()
FROM `Sys_Menu` WHERE Menu_Id IN (311, 312);

-- ============================================================
-- 4. 配置人员(103): 基础配置 + 认证机构管理 + ISO标准 + 工作流
-- ============================================================
-- 体系认证父菜单（只读）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (103, 304, 'View', 'system', NOW());

-- 基础配置（完全权限）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (103, 305, 'View,Add,Edit,Delete,Export,Upload,Search', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (103, 306, 'View,Add,Edit,Delete,Export,Upload,Search', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (103, 307, 'View,Add,Edit,Delete,Export,Search', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (103, 308, 'View,Add,Edit,Delete,Search', 'system', NOW());

-- 用户权限-审核员管理
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (103, 309, 'View', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (103, 310, 'View,Add,Edit,Delete,Search', 'system', NOW());

-- ============================================================
-- 5. 质量专员(104): 审核执行 + 企业档案 + 报告生成 + 统计分析
-- ============================================================
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (104, 304, 'View', 'system', NOW());

-- 审核执行（完全权限）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (104, 317, 'View,Add,Edit,Delete,Export,Upload,Search,Audit', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (104, 318, 'View,Add,Edit,Delete,Export,Upload,Search,Audit', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (104, 319, 'View,Add,Edit,Delete,Export,Search,Audit', 'system', NOW());

-- 企业档案（查看+编辑）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (104, 315, 'View,Add,Edit,Export,Search', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (104, 316, 'View,Edit,Export,Search', 'system', NOW());

-- 报告生成
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (104, 320, 'View,Add,Edit,Delete,Export,Upload,Search', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (104, 321, 'View,Add,Edit,Delete,Export,Search', 'system', NOW());

-- 数据监控（只读）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (104, 311, 'View', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (104, 312, 'View,Search', 'system', NOW());

-- ============================================================
-- 6. 审核管理员(200): 本机构的完整管理权限
-- 包括：基础配置(本机构)、审核员管理、数据监控、企业档案、审核执行、报告
-- ============================================================
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 304, 'View', 'system', NOW());

-- 基础配置（仅查看认证机构信息，不可新增/删除机构）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 305, 'View', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 306, 'View,Edit,Export,Search', 'system', NOW());  -- 只能编辑自己的机构
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 307, 'View,Search', 'system', NOW());  -- ISO标准只读
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 308, 'View,Search', 'system', NOW());  -- 工作流只读

-- 用户权限-审核员管理（本机构）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 309, 'View,Add,Edit,Delete,Search', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 310, 'View,Add,Edit,Delete,Search', 'system', NOW());

-- 数据监控（本机构）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 311, 'View', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 312, 'View,Search', 'system', NOW());

-- 我的工作台
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 313, 'View', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 314, 'View,Search', 'system', NOW());

-- 企业档案（本机构）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 315, 'View,Add,Edit,Export,Search', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 316, 'View,Add,Edit,Export,Search', 'system', NOW());

-- 审核执行（本机构，完全权限）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 317, 'View,Add,Edit,Delete,Export,Upload,Search,Audit', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 318, 'View,Add,Edit,Delete,Export,Upload,Search,Audit', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 319, 'View,Add,Edit,Delete,Export,Search,Audit', 'system', NOW());

-- 报告生成
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 320, 'View,Add,Edit,Delete,Export,Upload,Search', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (200, 321, 'View,Add,Edit,Delete,Export,Search', 'system', NOW());

-- ============================================================
-- 7. 审核组长(201): 审核 + 团队管理 + 报告
-- 重点：执行审核、分配任务、管理团队、生成报告
-- ============================================================
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (201, 304, 'View', 'system', NOW());

-- 我的工作台
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (201, 313, 'View', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (201, 314, 'View,Search', 'system', NOW());

-- 审核执行（完全权限，可分配任务给组员）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (201, 317, 'View,Add,Edit,Export,Upload,Search,Audit', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (201, 318, 'View,Add,Edit,Export,Upload,Search,Audit', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (201, 319, 'View,Add,Edit,Export,Search,Audit', 'system', NOW());

-- 企业档案（查看）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (201, 315, 'View', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (201, 316, 'View,Search', 'system', NOW());

-- 报告生成
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (201, 320, 'View,Add,Edit,Export,Upload,Search', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (201, 321, 'View,Add,Edit,Export,Search', 'system', NOW());

-- ============================================================
-- 8. 普通审核员(202): 仅处理分配给自己的任务
-- 最小权限：我的工作台、审核任务（自己）、文件上传、不符合项（自己）
-- ============================================================
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (202, 304, 'View', 'system', NOW());

-- 我的工作台（核心界面）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (202, 313, 'View', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (202, 314, 'View,Search', 'system', NOW());

-- 审核执行（只能看到分配给自己的任务，后端过滤）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (202, 317, 'View,Edit,Upload,Search,Audit', 'system', NOW());  -- 无Add/Delete权限
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (202, 318, 'View,Edit,Upload,Search,Audit', 'system', NOW());  -- 只能编辑分配的任务
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (202, 319, 'View,Add,Edit,Search', 'system', NOW());  -- 可录入不符合项

-- 企业档案（只读，查看被审核企业的信息）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (202, 315, 'View', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (202, 316, 'View,Search', 'system', NOW());

-- ============================================================
-- 9. 企业账号(300): 企业自助服务
-- 功能：企业信息维护、文件上传、审核进度查询、报告下载
-- ============================================================
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (300, 304, 'View', 'system', NOW());

-- 我的工作台（查看待办、审核进度）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (300, 313, 'View', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (300, 314, 'View,Search', 'system', NOW());

-- 企业档案（只能编辑自己的企业信息）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (300, 315, 'View', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (300, 316, 'View,Edit,Upload,Search', 'system', NOW());  -- 可上传文件

-- 报告生成（只读，可下载自己的报告）
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (300, 320, 'View', 'system', NOW());
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`)
VALUES (300, 321, 'View,Export,Search', 'system', NOW());  -- 可下载报告

-- ============================================================
-- 验证权限配置结果
-- ============================================================
SELECT '✅ 权限配置完成！' AS message;
SELECT '' AS separator;
SELECT '📊 各角色权限统计：' AS info;
SELECT 
    r.Role_Id,
    r.RoleName,
    COUNT(ra.Menu_Id) AS menu_count
FROM Sys_Role r
LEFT JOIN Sys_RoleAuth ra ON r.Role_Id = ra.Role_Id
WHERE r.Role_Id IN (100,101,102,103,104,200,201,202,300)
GROUP BY r.Role_Id, r.RoleName
ORDER BY r.Role_Id;
