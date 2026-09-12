-- ============================================================
-- 修复菜单图标名称：Element UI → Element Plus 兼容
-- 日期: 2026-09-11
-- 说明: 将旧版 el-icon-xxx 格式转换为 Element Plus 的 PascalCase 名称
-- ============================================================

-- 禁用外键检查
SET FOREIGN_KEY_CHECKS=0;

-- 更新系统管理菜单图标
UPDATE Sys_Menu SET Icon = 'Setting' WHERE Menu_Id = 1 AND MenuName = '系统管理';
UPDATE Sys_Menu SET Icon = 'HomeFilled' WHERE Menu_Id = 101 AND MenuName = '机构-人员管理';
UPDATE Sys_Menu SET Icon = 'UserFilled' WHERE Menu_Id = 102 AND MenuName = '角色-人员管理';
UPDATE Sys_Menu SET Icon = 'Menu' WHERE Menu_Id = 103 AND MenuName = '角色-菜单管理';
UPDATE Sys_Menu SET Icon = 'Connection' WHERE Menu_Id = 104 AND MenuName = '角色-接口管理';
UPDATE Sys_Menu SET Icon = 'Folder' WHERE Menu_Id = 105 AND MenuName = '菜单管理';
UPDATE Sys_Menu SET Icon = 'Link' WHERE Menu_Id = 106 AND MenuName = '接口管理';
UPDATE Sys_Menu SET Icon = 'Collection' WHERE Menu_Id = 107 AND MenuName = '数据字典';
UPDATE Sys_Menu SET Icon = 'Document' WHERE Menu_Id = 108 AND MenuName = '日志管理';
UPDATE Sys_Menu SET Icon = 'Tools' WHERE Menu_Id = 109 AND MenuName = '系统参数配置';

-- 更新业务管理菜单图标
UPDATE Sys_Menu SET Icon = 'DocumentChecked' WHERE Menu_Id = 2 AND MenuName = '业务管理';
UPDATE Sys_Menu SET Icon = 'Document' WHERE Menu_Id = 201 AND MenuName = 'ISO 标准管理';
UPDATE Sys_Menu SET Icon = 'OfficeBuilding' WHERE Menu_Id = 202 AND MenuName = '认证机构管理';
UPDATE Sys_Menu SET Icon = 'Date' WHERE Menu_Id = 203 AND MenuName = '认证阶段定义';
UPDATE Sys_Menu SET Icon = 'DocumentCopy' WHERE Menu_Id = 204 AND MenuName = '标准条款管理';
UPDATE Sys_Menu SET Icon = 'Connection' WHERE Menu_Id = 205 AND MenuName = '机构-标准关联';
UPDATE Sys_Menu SET Icon = 'Operation' WHERE Menu_Id = 206 AND MenuName = '机构-阶段关联';
UPDATE Sys_Menu SET Icon = 'Files' WHERE Menu_Id = 207 AND MenuName = '标准文件管理';
UPDATE Sys_Menu SET Icon = 'Tickets' WHERE Menu_Id = 208 AND MenuName = '文档提取规则';
UPDATE Sys_Menu SET Icon = 'Collection' WHERE Menu_Id = 209 AND MenuName = '报告章节定义';
UPDATE Sys_Menu SET Icon = 'ChatLineRound' WHERE Menu_Id = 210 AND MenuName = 'Prompt 模板';
UPDATE Sys_Menu SET Icon = 'Cpu' WHERE Menu_Id = 211 AND MenuName = '技能管理';
UPDATE Sys_Menu SET Icon = 'Edit' WHERE Menu_Id = 212 AND MenuName = 'NC 规则设计';
UPDATE Sys_Menu SET Icon = 'Warning' WHERE Menu_Id = 213 AND MenuName = 'NC 检查规则';
UPDATE Sys_Menu SET Icon = 'SetUp' WHERE Menu_Id = 214 AND MenuName = '规则与工作流';
UPDATE Sys_Menu SET Icon = 'Money' WHERE Menu_Id = 215 AND MenuName = 'AI 费用监控';
UPDATE Sys_Menu SET Icon = 'DataAnalysis' WHERE Menu_Id = 216 AND MenuName = '队列监控';

-- 启用外键检查
SET FOREIGN_KEY_CHECKS=1;

-- 验证更新结果
SELECT Menu_Id, MenuName, Icon FROM Sys_Menu WHERE ParentId IN (1, 2) ORDER BY Menu_Id;
