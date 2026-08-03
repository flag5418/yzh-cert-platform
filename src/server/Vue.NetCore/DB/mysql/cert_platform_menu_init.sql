-- ============================================================
-- 体系认证平台 - 完整菜单初始化脚本
-- 版本：V1.0 | 日期：2026-07-30
-- 架构：单前端(vol.web) + 双角色(管理员/审核员) + UI差异化
--
-- 菜单结构：
--   体系认证平台（一级）
--   ├── 管理员菜单（基础配置/用户权限/数据监控）
--   └── 审核员菜单（工作台/企业档案/审核执行/报告生成）
--
-- 使用方式：
--   1. 在 MySQL 中执行此脚本
--   2. 创建角色并分配菜单权限（见脚本末尾）
--   3. 重启后端服务，刷新前端页面
-- ============================================================

-- 设置变量：当前最大 Menu_Id（避免与现有菜单冲突）
-- 当前最大 Menu_Id 为 303（基于 2026-07-30 数据）
SET @cert_parent_id = 304;  -- 一级菜单ID：体系认证平台

-- ============================================================
-- 1. 一级菜单：体系认证平台
-- ============================================================
INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @cert_parent_id, 
    '体系认证平台', 
    '[{"text":"查询","value":"Search"}]', 
    'el-icon-document-checked', 
    'ISO 9001/13485 体系认证管理平台 - 配置、审核、报告全流程', 
    1, 
    8000,  -- 排序：系统设置(1000) < 体系认证(8000) < MES业务(9000) < 基础组件(1720)
    '.', 
    0,  -- ParentId=0 表示顶级菜单
    '', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- ============================================================
-- 2. 二级菜单：管理员职能模块
-- ============================================================

-- 2.1 基础配置（域 A - 认证体系配置）
SET @admin_config_id = @cert_parent_id + 1;

INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @admin_config_id, 
    '基础配置', 
    '[{"text":"查询","value":"Search"}]', 
    'el-icon-setting', 
    '认证机构、ISO标准、工作流等基础配置（管理员专用）', 
    1, 
    2000,  -- 管理员模块内最高优先级
    '.', 
    @cert_parent_id, 
    '', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- 2.1.1 认证机构管理 ⭐ 第一个功能菜单
INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @admin_config_id + 1, 
    '认证机构管理', 
    '[{"text":"查询","value":"Search"},{"text":"新建","value":"Add"},{"text":"删除","value":"Delete"},{"text":"编辑","value":"Update"},{"text":"导出","value":"Export"},{"text":"导入","value":"Import"}]', 
    'el-icon-office-building', 
    '管理认证机构信息（名称、资质范围、联系方式等）', 
    1, 
    1000,  -- 基础配置内最高优先级
    'cert_certification_body',  -- 对应数据库表名
    @admin_config_id, 
    '/CertPlatform/Cert/CertificationBody',  -- 路由路径（对应前端页面）
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- 2.1.2 ISO 标准管理
INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @admin_config_id + 2, 
    'ISO标准管理', 
    '[{"text":"查询","value":"Search"},{"text":"新建","value":"Add"},{"text":"删除","value":"Delete"},{"text":"编辑","value":"Update"}]', 
    'el-icon-notebook-2', 
    '维护 ISO 9001/13485 等认证标准版本和条款结构', 
    1, 
    900, 
    'cert_iso_standard', 
    @admin_config_id, 
    '/CertPlatform/Cert/IsoStandard', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- 2.1.3 工作流配置
INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @admin_config_id + 3, 
    '工作流配置', 
    '[{"text":"查询","value":"Search"},{"text":"新建","value":"Add"},{"text":"删除","value":"Delete"},{"text":"编辑","value":"Update"}]', 
    'el-icon-magic-stick', 
    '配置 Skill、工作流定义、字段标签映射', 
    1, 
    800, 
    'wf_workflow_definition', 
    @admin_config_id, 
    '/CertPlatform/Wf/WorkflowDefinition', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- 2.2 用户权限管理
SET @admin_user_id = @cert_parent_id + 5;

INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @admin_user_id, 
    '用户权限', 
    '[{"text":"查询","value":"Search"}]', 
    'el-icon-user', 
    '审核员账号管理、角色分配、权限控制（管理员专用）', 
    1, 
    1500, 
    '.', 
    @cert_parent_id, 
    '', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- 2.2.1 审核员管理（复用系统用户表，但限定角色）
INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @admin_user_id + 1, 
    '审核员管理', 
    '[{"text":"查询","value":"Search"},{"text":"新建","value":"Add"},{"text":"删除","value":"Delete"},{"text":"编辑","value":"Update"}]', 
    'el-icon-avatar', 
    '管理审核员账号信息、分配审核任务权限', 
    1, 
    1000, 
    'Sys_User',  -- 复用 Vol 内置用户表
    @admin_user_id, 
    '/CertPlatform/Sys/AuditorManage', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- 2.3 数据监控
SET @admin_monitor_id = @cert_parent_id + 7;

INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @admin_monitor_id, 
    '数据监控', 
    '[{"text":"查询","value":"Search"}]', 
    'el-icon-data-line', 
    '系统运行状态、任务监控、日志查看（管理员专用）', 
    1, 
    1000, 
    '.', 
    @cert_parent_id, 
    '', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- 2.3.1 任务状态监控
INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @admin_monitor_id + 1, 
    '任务状态监控', 
    '[{"text":"查询","value":"Search"},{"text":"导出","value":"Export"}]', 
    'el-icon-monitor', 
    '查看所有审核任务执行状态、进度、异常情况', 
    1, 
    1000, 
    'audit_task', 
    @admin_monitor_id, 
    '/CertPlatform/Audit/TaskMonitor', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- ============================================================
-- 3. 二级菜单：审核员职能模块
-- ============================================================

-- 3.1 我的工作台
SET @auditor_workspace_id = @cert_parent_id + 9;

INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @auditor_workspace_id, 
    '我的工作台', 
    '[{"text":"查询","value":"Search"}]', 
    'el-icon-data-board', 
    '审核员个人工作台：待办任务、日程安排、消息通知（审核员专用）', 
    1, 
    3000,  -- 审核员模块最高优先级
    '.', 
    @cert_parent_id, 
    '', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- 3.1.1 待办任务
INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @auditor_workspace_id + 1, 
    '待办任务', 
    '[{"text":"查询","value":"Search"}]', 
    'el-icon-time', 
    '查看待处理的审核任务、即将到期的任务提醒', 
    1, 
    1000, 
    'audit_task', 
    @auditor_workspace_id, 
    '/CertPlatform/Auditor/PendingTasks', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- 3.2 企业档案
SET @auditor_enterprise_id = @cert_parent_id + 11;

INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @auditor_enterprise_id, 
    '企业档案', 
    '[{"text":"查询","value":"Search"}]', 
    'el-icon-school', 
    '受审核企业管理：企业信息、文件档案、提取结果（审核员专用）', 
    1, 
    2500, 
    '.', 
    @cert_parent_id, 
    '', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- 3.2.1 企业列表
INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @auditor_enterprise_id + 1, 
    '企业列表', 
    '[{"text":"查询","value":"Search"},{"text":"新建","value":"Add"},{"text":"删除","value":"Delete"},{"text":"编辑","value":"Update"}]', 
    'el-icon-office-building', 
    '管理受审核企业基本信息、联系方式、认证历史', 
    1, 
    1000, 
    'ent_enterprise', 
    @auditor_enterprise_id, 
    '/CertPlatform/Ent/EnterpriseList', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- 3.3 审核执行
SET @auditor_audit_id = @cert_parent_id + 13;

INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @auditor_audit_id, 
    '审核执行', 
    '[{"text":"查询","value":"Search"}]', 
    'el-icon-edit-outline', 
    '核心审核功能：审核任务、检查清单、不符合项、纠正措施（审核员专用）', 
    1, 
    2000, 
    '.', 
    @cert_parent_id, 
    '', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- 3.3.1 审核任务
INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @auditor_audit_id + 1, 
    '审核任务', 
    '[{"text":"查询","value":"Search"},{"text":"新建","value":"Add"},{"text":"删除","value":"Delete"},{"text":"编辑","value":"Update"}]', 
    'el-icon-tickets', 
    '创建和管理审核任务，分配审核员，跟踪审核进度', 
    1, 
    1000, 
    'audit_task', 
    @auditor_audit_id, 
    '/CertPlatform/Audit/AuditTask', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- 3.3.2 不符合项管理
INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @auditor_audit_id + 2, 
    '不符合项管理', 
    '[{"text":"查询","value":"Search"},{"text":"新建","value":"Add"},{"text":"删除","value":"Delete"},{"text":"编辑","value":"Update"}]', 
    'el-icon-warning', 
    '记录和管理审核中发现的不符合项（NC），跟踪纠正措施', 
    1, 
    900, 
    'audit_nonconformity', 
    @auditor_audit_id, 
    '/CertPlatform/Audit/NonConformity', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- 3.4 报告生成
SET @auditor_report_id = @cert_parent_id + 15;

INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @auditor_report_id, 
    '报告生成', 
    '[{"text":"查询","value":"Search"}]', 
    'el-icon-document', 
    '审核报告生成、预览、导出、归档（审核员专用）', 
    1, 
    1500, 
    '.', 
    @cert_parent_id, 
    '', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- 3.4.1 报告列表
INSERT INTO `Sys_Menu` (
    `Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, 
    `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, 
    `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`
) VALUES (
    @auditor_report_id + 1, 
    '报告列表', 
    '[{"text":"查询","value":"Search"},{"text":"导出","value":"Export"},{"text":"打印","value":"Print"}]', 
    'el-icon-document-copy', 
    '查看已生成的审核报告，支持预览、下载PDF、打印', 
    1, 
    1000, 
    'rpt_audit_report', 
    @auditor_report_id, 
    '/CertPlatform/Rpt/ReportList', 
    NOW(), 
    '超级管理员', 
    NOW(), 
    '超级管理员', 
    0
);

-- ============================================================
-- 4. 创建角色并分配菜单权限
-- ============================================================

-- 4.1 创建"体系管理员"角色
INSERT INTO `Sys_Role` (
    `Role_Id`, `RoleName`, `ParentId`, `Dept_Id`, `DeptName`, 
    `OrderNo`, `Creator`, `CreateDate`, `Enable`, `Modifier`, `ModifyDate`
) VALUES (
    10,  -- 角色ID（避免与现有角色冲突）
    '体系管理员',
    0,
    NULL,
    NULL,
    100,
    '超级管理员',
    NOW(),
    1,
    '超级管理员',
    NOW()
);

-- 4.2 创建"审核员"角色
INSERT INTO `Sys_Role` (
    `Role_Id`, `RoleName`, `ParentId`, `Dept_Id`, `DeptName`, 
    `OrderNo`, `Creator`, `CreateDate`, `Enable`, `Modifier`, `ModifyDate`
) VALUES (
    20,  -- 角色ID
    '审核员',
    0,
    NULL,
    NULL,
    200,
    '超级管理员',
    NOW(),
    1,
    '超级管理员',
    NOW()
);

-- 4.3 为"体系管理员"分配菜单权限（管理员看到的所有菜单）
-- 基础配置下的所有菜单
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`) VALUES
(10, @admin_config_id,     '["Search"]',         '超级管理员', NOW()),
(10, @admin_config_id + 1, '["Search","Add","Delete","Update","Export","Import"]', '超级管理员', NOW()),
(10, @admin_config_id + 2, '["Search","Add","Delete","Update"]', '超级管理员', NOW()),
(10, @admin_config_id + 3, '["Search","Add","Delete","Update"]', '超级管理员', NOW()),
-- 用户权限下的所有菜单
(10, @admin_user_id,       '["Search"]',         '超级管理员', NOW()),
(10, @admin_user_id + 1,   '["Search","Add","Delete","Update"]', '超级管理员', NOW()),
-- 数据监控下的所有菜单
(10, @admin_monitor_id,    '["Search"]',         '超级管理员', NOW()),
(10, @admin_monitor_id + 1,'["Search","Export"]', '超级管理员', NOW());

-- 4.4 为"审核员"分配菜单权限（审核员看到的菜单）
-- 我的工作台
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`) VALUES
(20, @auditor_workspace_id,     '["Search]",          '超级管理员', NOW()),
(20, @auditor_workspace_id + 1, '["Search"]',         '超级管理员', NOW()),
-- 企业档案
(20, @auditor_enterprise_id,    '["Search"]',         '超级管理员', NOW()),
(20, @auditor_enterprise_id + 1,'["Search","Add","Delete","Update"]', '超级管理员', NOW()),
-- 审核执行
(20, @auditor_audit_id,        '["Search"]',         '超级管理员', NOW()),
(20, @auditor_audit_id + 1,    '["Search","Add","Delete","Update"]', '超级管理员', NOW()),
(20, @auditor_audit_id + 2,    '["Search","Add","Delete","Update"]', '超级管理员', NOW()),
-- 报告生成
(20, @auditor_report_id,       '["Search"]',         '超级管理员', NOW()),
(20, @auditor_report_id + 1,   '["Search","Export","Print"]', '超级管理员', NOW());

-- ============================================================
-- 5. 插入第一条认证机构初始数据（示例数据）
-- ============================================================
INSERT INTO `cert_certification_body` (
    `Code`, `Name`, `ShortName`, `registration_no`, 
    `accreditation_scope`, `ContactName`, `ContactPhone`, 
    `ContactEmail`, `Address`, `website`, 
    `logo_url`, `Status`, `remarks`,
    `CreateID`, `CreateDate`
) VALUES (
    UUID(),  -- Code 字段使用 GUID
    '映智汇认证有限公司',
    '映智汇认证',
    'YZH-CERT-2026-001',
    'ISO 9001质量管理体系认证、ISO 13485医疗器械质量管理体系认证',
    '张三',
    '13800138000',
    'admin@yingzhihui.com',
    '北京市海淀区中关村科技园区',
    'https://www.yingzhihui.com',
    '',
    1,  -- Status: 1=启用
    '体系认证平台的默认认证机构，用于开发和测试',
    1,  -- CreateID: 默认管理员 ID
    NOW()
);

-- ============================================================
-- 完成！
-- ============================================================
SELECT 
    '✅ 菜单创建完成' AS message,
    CONCAT('一级菜单ID: ', @cert_parent_id) AS parent_menu_id,
    (SELECT COUNT(*) FROM Sys_Menu WHERE ParentId = @cert_parent_id) AS sub_menu_count,
    (SELECT COUNT(*) FROM Sys_Role WHERE Role_Id IN (10, 20)) AS role_count,
    (SELECT COUNT(*) FROM cert_certification_body) AS certification_body_count;
