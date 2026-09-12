-- =====================================================
-- 菜单重构 + 删除 Sys_Department 表
-- 日期: 2026-09-11
-- 说明: 1. 重建系统管理菜单结构
--       2. 删除 Sys_Department 表
--       3. 删除部门管理相关前端文件引用
-- =====================================================

USE yzh_cert_platform;

-- =====================================================
-- 第一步：备份原菜单数据（安全起见）
-- =====================================================
CREATE TABLE IF NOT EXISTS _backup_sys_menu AS SELECT * FROM Sys_Menu;
CREATE TABLE IF NOT EXISTS _backup_sys_department AS SELECT * FROM Sys_Department;

-- =====================================================
-- 第二步：清空并重建菜单
-- =====================================================
DELETE FROM Sys_Menu;

-- 系统管理根菜单
INSERT INTO Sys_Menu (Menu_Id, ParentId, MenuName, Url, Icon, Enable, OrderNo, MenuType) VALUES
(1, 0, '系统管理', '/', 'el-icon-setting', 1, 100, 0);

-- 系统管理子菜单（底层架构 + 基础功能）
INSERT INTO Sys_Menu (Menu_Id, ParentId, MenuName, Url, Icon, Enable, OrderNo, MenuType) VALUES
(101, 1, '机构-人员管理', '/system/organization', 'el-icon-s-home', 1, 100, 0),
(102, 1, '角色-人员管理', '/system/role-user', 'el-icon-user-solid', 1, 200, 0),
(103, 1, '角色-菜单管理', '/system/role-menu', 'el-icon-menu', 1, 300, 0),
(104, 1, '角色-接口管理', '/system/role-api', 'el-icon-connection', 1, 400, 0),
(105, 1, '菜单管理', '/system/menu', 'el-icon-folder', 1, 500, 0),
(106, 1, '接口管理', '/system/api', 'el-icon-link', 1, 600, 0),
(107, 1, '数据字典', '/system/dict', 'el-icon-receiving', 1, 700, 0),
(108, 1, '日志管理', '/system/log', 'el-icon-document', 1, 800, 0),
(109, 1, '系统参数配置', '/system/config', 'el-icon-s-tools', 1, 900, 0);

-- 业务管理根菜单
INSERT INTO Sys_Menu (Menu_Id, ParentId, MenuName, Url, Icon, Enable, OrderNo, MenuType) VALUES
(2, 0, '业务管理', '/', 'el-icon-document-checked', 1, 200, 0);

-- 业务管理子菜单
INSERT INTO Sys_Menu (Menu_Id, ParentId, MenuName, Url, Icon, Enable, OrderNo, MenuType) VALUES
(201, 2, 'ISO 标准管理', '/cert/iso-standard', 'el-icon-document', 1, 100, 0),
(202, 2, '认证机构管理', '/cert/cert-body', 'el-icon-office-building', 1, 200, 0),
(203, 2, '认证阶段定义', '/cert/cert-stage', 'el-icon-date', 1, 300, 0),
(204, 2, '标准条款管理', '/cert/iso-clause', 'el-icon-document-copy', 1, 400, 0),
(205, 2, '机构-标准关联', '/cert/link-org-standard', 'el-icon-connection', 1, 500, 0),
(206, 2, '机构-阶段关联', '/cert/link-org-stage', 'el-icon-operation', 1, 600, 0),
(207, 2, '标准文件管理', '/business/directory-manager', 'el-icon-files', 1, 700, 0),
(208, 2, '文档提取规则', '/business/doc-extraction-rule', 'el-icon-tickets', 1, 800, 0),
(209, 2, '报告章节定义', '/business/report-def', 'el-icon-collection', 1, 900, 0),
(210, 2, 'Prompt 模板', '/business/prompt-template', 'el-icon-chat-line-round', 1, 1000, 0),
(211, 2, '技能管理', '/business/skill-manage', 'el-icon-cpu', 1, 1100, 0),
(212, 2, 'NC 规则设计', '/business/nc-config', 'el-icon-edit', 1, 1200, 0),
(213, 2, 'NC 检查规则', '/business/workflow-rules', 'el-icon-warning', 1, 1300, 0),
(214, 2, '规则与工作流', '/business/report-rule-config', 'el-icon-set-up', 1, 1400, 0),
(215, 2, 'AI 费用监控', '/business/ai-usage', 'el-icon-money', 1, 1500, 0),
(216, 2, '队列监控', '/business/queue-monitor', 'el-icon-s-data', 1, 1600, 0);

-- =====================================================
-- 第三步：删除 Sys_Department 表
-- =====================================================
DROP TABLE IF EXISTS Sys_Department;

-- =====================================================
-- 第四步：验证
-- =====================================================
SELECT '菜单总数' as info, COUNT(*) as cnt FROM Sys_Menu;
SELECT '系统管理子菜单' as info, COUNT(*) as cnt FROM Sys_Menu WHERE ParentId = 1;
SELECT '业务管理子菜单' as info, COUNT(*) as cnt FROM Sys_Menu WHERE ParentId = 2;
