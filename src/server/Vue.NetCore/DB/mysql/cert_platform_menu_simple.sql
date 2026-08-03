-- ============================================================
-- 体系认证平台 - 菜单初始化脚本（简化版 - 固定ID）
-- 版本：V1.0 | 日期：2026-07-30
-- ============================================================

-- 1. 一级菜单：体系认证平台 (Menu_Id=304)
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (304, '体系认证平台', '[{"text":"查询","value":"Search"}]', 'el-icon-document-checked', 'ISO 9001/13485 体系认证管理平台', 1, 8000, '.', 0, '', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 2. 二级菜单：基础配置 (305)
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (305, '基础配置', '[{"text":"查询","value":"Search"}]', 'el-icon-setting', '认证机构、ISO标准、工作流等基础配置', 1, 2000, '.', 304, '', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 2.1 认证机构管理 ⭐ 第一个功能菜单 (306)
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (306, '认证机构管理', '[{"text":"查询","value":"Search"},{"text":"新建","value":"Add"},{"text":"删除","value":"Delete"},{"text":"编辑","value":"Update"},{"text":"导出","value":"Export"},{"text":"导入","value":"Import"}]', 'el-icon-office-building', '管理认证机构信息（名称、资质范围、联系方式等）', 1, 1000, 'cert_certification_body', 305, '/CertPlatform/Cert/CertificationBody', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 2.2 ISO标准管理 (307)
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (307, 'ISO标准管理', '[{"text":"查询","value":"Search"},{"text":"新建","value":"Add"},{"text":"删除","value":"Delete"},{"text":"编辑","value":"Update"}]', 'el-icon-notebook-2', '维护 ISO 9001/13485 等认证标准版本和条款结构', 1, 900, 'cert_iso_standard', 305, '/CertPlatform/Cert/IsoStandard', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 2.3 工作流配置 (308)
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (308, '工作流配置', '[{"text":"查询","value":"Search"},{"text":"新建","value":"Add"},{"text":"删除","value":"Delete"},{"text":"编辑","value":"Update"}]', 'el-icon-magic-stick', '配置 Skill、工作流定义、字段标签映射', 1, 800, 'wf_workflow_definition', 305, '/CertPlatform/Wf/WorkflowDefinition', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 3. 二级菜单：用户权限 (309)
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (309, '用户权限', '[{"text":"查询","value":"Search"}]', 'el-icon-user', '审核员账号管理、角色分配、权限控制', 1, 1500, '.', 304, '', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 3.1 审核员管理 (310)
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (310, '审核员管理', '[{"text":"查询","value":"Search"},{"text":"新建","value":"Add"},{"text":"删除","value":"Delete"},{"text":"编辑","value":"Update"}]', 'el-icon-avatar', '管理审核员账号信息、分配审核任务权限', 1, 1000, 'Sys_User', 309, '/CertPlatform/Sys/AuditorManage', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 4. 二级菜单：数据监控 (311)
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (311, '数据监控', '[{"text":"查询","value":"Search"}]', 'el-icon-data-line', '系统运行状态、任务监控、日志查看', 1, 1000, '.', 304, '', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 4.1 任务状态监控 (312)
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (312, '任务状态监控', '[{"text":"查询","value":"Search"},{"text":"导出","value":"Export"}]', 'el-icon-monitor', '查看所有审核任务执行状态、进度、异常情况', 1, 1000, 'audit_task', 311, '/CertPlatform/Audit/TaskMonitor', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 5. 二级菜单：我的工作台 (313) - 审核员专用
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (313, '我的工作台', '[{"text":"查询","value":"Search"}]', 'el-icon-data-board', '审核员个人工作台：待办任务、日程安排、消息通知', 1, 3000, '.', 304, '', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 5.1 待办任务 (314)
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (314, '待办任务', '[{"text":"查询","value":"Search"}]', 'el-icon-time', '查看待处理的审核任务、即将到期的任务提醒', 1, 1000, 'audit_task', 313, '/CertPlatform/Auditor/PendingTasks', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 6. 二级菜单：企业档案 (315) - 审核员专用
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (315, '企业档案', '[{"text":"查询","value":"Search"}]', 'el-icon-school', '受审核企业管理：企业信息、文件档案、提取结果', 1, 2500, '.', 304, '', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 6.1 企业列表 (316)
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (316, '企业列表', '[{"text":"查询","value":"Search"},{"text":"新建","value":"Add"},{"text":"删除","value":"Delete"},{"text":"编辑","value":"Update"}]', 'el-icon-office-building', '管理受审核企业基本信息、联系方式、认证历史', 1, 1000, 'ent_enterprise', 315, '/CertPlatform/Ent/EnterpriseList', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 7. 二级菜单：审核执行 (317) - 审核员专用
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (317, '审核执行', '[{"text":"查询","value":"Search"}]', 'el-icon-edit-outline', '核心审核功能：审核任务、检查清单、不符合项、纠正措施', 1, 2000, '.', 304, '', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 7.1 审核任务 (318)
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (318, '审核任务', '[{"text":"查询","value":"Search"},{"text":"新建","value":"Add"},{"text":"删除","value":"Delete"},{"text":"编辑","value":"Update"}]', 'el-icon-tickets', '创建和管理审核任务，分配审核员，跟踪审核进度', 1, 1000, 'audit_task', 317, '/CertPlatform/Audit/AuditTask', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 7.2 不符合项管理 (319)
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (319, '不符合项管理', '[{"text":"查询","value":"Search"},{"text":"新建","value":"Add"},{"text":"删除","value":"Delete"},{"text":"编辑","value":"Update"}]', 'el-icon-warning', '记录和管理审核中发现的不符合项（NC），跟踪纠正措施', 1, 900, 'audit_nonconformity', 317, '/CertPlatform/Audit/NonConformity', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 8. 二级菜单：报告生成 (320) - 审核员专用
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (320, '报告生成', '[{"text":"查询","value":"Search"}]', 'el-icon-document', '审核报告生成、预览、导出、归档', 1, 1500, '.', 304, '', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 8.1 报告列表 (321)
INSERT INTO `Sys_Menu` (`Menu_Id`, `MenuName`, `Auth`, `Icon`, `Description`, `Enable`, `OrderNo`, `TableName`, `ParentId`, `Url`, `CreateDate`, `Creator`, `ModifyDate`, `Modifier`, `MenuType`)
VALUES (321, '报告列表', '[{"text":"查询","value":"Search"},{"text":"导出","value":"Export"},{"text":"打印","value":"Print"}]', 'el-icon-document-copy', '查看已生成的审核报告，支持预览、下载PDF、打印', 1, 1000, 'rpt_audit_report', 320, '/CertPlatform/Rpt/ReportList', NOW(), '超级管理员', NOW(), '超级管理员', 0);

-- 9. 创建角色
INSERT INTO `Sys_Role` (`Role_Id`, `RoleName`, `ParentId`, `Dept_Id`, `DeptName`, `OrderNo`, `Creator`, `CreateDate`, `Enable`, `Modifier`, `ModifyDate`)
VALUES (10, '体系管理员', 0, NULL, NULL, 100, '超级管理员', NOW(), 1, '超级管理员', NOW());

INSERT INTO `Sys_Role` (`Role_Id`, `RoleName`, `ParentId`, `Dept_Id`, `DeptName`, `OrderNo`, `Creator`, `CreateDate`, `Enable`, `Modifier`, `ModifyDate`)
VALUES (20, '审核员', 0, NULL, NULL, 200, '超级管理员', NOW(), 1, '超级管理员', NOW());

-- 10. 为"体系管理员"(Role_Id=10)分配权限
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`) VALUES
(10, 305, '["Search"]', '超级管理员', NOW()),
(10, 306, '["Search","Add","Delete","Update","Export","Import"]', '超级管理员', NOW()),
(10, 307, '["Search","Add","Delete","Update"]', '超级管理员', NOW()),
(10, 308, '["Search","Add","Delete","Update"]', '超级管理员', NOW()),
(10, 309, '["Search"]', '超级管理员', NOW()),
(10, 310, '["Search","Add","Delete","Update"]', '超级管理员', NOW()),
(10, 311, '["Search"]', '超级管理员', NOW()),
(10, 312, '["Search","Export"]', '超级管理员', NOW());

-- 11. 为"审核员"(Role_Id=20)分配权限
INSERT INTO `Sys_RoleAuth` (`Role_Id`, `Menu_Id`, `AuthValue`, `Creator`, `CreateDate`) VALUES
(20, 313, '["Search"]', '超级管理员', NOW()),
(20, 314, '["Search"]', '超级管理员', NOW()),
(20, 315, '["Search"]', '超级管理员', NOW()),
(20, 316, '["Search","Add","Delete","Update"]', '超级管理员', NOW()),
(20, 317, '["Search"]', '超级管理员', NOW()),
(20, 318, '["Search","Add","Delete","Update"]', '超级管理员', NOW()),
(20, 319, '["Search","Add","Delete","Update"]', '超级管理员', NOW()),
(20, 320, '["Search"]', '超级管理员', NOW()),
(20, 321, '["Search","Export","Print"]', '超级管理员', NOW());

-- 12. 插入第一条认证机构初始数据
INSERT INTO `cert_certification_body` (
    `Code`, `Name`, `ShortName`, `registration_no`, 
    `accreditation_scope`, `ContactName`, `ContactPhone`, 
    `ContactEmail`, `Address`, `website`, 
    `logo_url`, `Status`, `remarks`,
    `CreateID`, `CreateDate`
) VALUES (
    UUID(),
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
    1,
    '体系认证平台的默认认证机构，用于开发和测试',
    1,
    NOW()
);

-- 验证结果
SELECT '✅ 菜单创建完成' AS message;
SELECT Menu_Id, MenuName, ParentId FROM Sys_Menu WHERE Menu_Id >= 304 ORDER BY Menu_Id;
SELECT Role_Id, RoleName FROM Sys_Role WHERE Role_Id IN (10, 20);
SELECT * FROM cert_certification_body LIMIT 1;
