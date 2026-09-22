-- ===================================================================
-- 全量 snake_case → PascalCase 最终清理（V3：Vol 框架遗留表）
-- 日期：2026-09-21
-- 原则：数据库/C#/TypeScript 三端统一，全部使用 PascalCase
--
-- 本次修复（Vol 框架遗留表，新代码基本不使用）：
--   - sys_api (4列：id/code/group_path/enable)
--   - Sys_WorkFlow (1列)
--   - Sys_WorkFlowStep (2列)
--   - Sys_WorkFlowTable (2列)
--   - Sys_WorkFlowTableAuditLog (2列)
--   - Sys_WorkFlowTableStep (3列)
--   - Sys_TableInfo (1列)
--   - Sys_TableColumn (1列)
--
-- ⚠️ 注意：sys_api.group_path 被 PermissionService.cs 引用，改名后需同步修改 C# 代码
-- ===================================================================

USE yzh_cert_platform;

-- sys_api: API 权限表（PermissionService 引用 group_path）
ALTER TABLE sys_api
  CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT,
  CHANGE COLUMN code Code varchar(64) NOT NULL,
  CHANGE COLUMN group_path GroupPath varchar(200) NOT NULL,
  CHANGE COLUMN enable Enable tinyint(1) NULL DEFAULT 1;

-- Sys_WorkFlow: Vol 工作流主表（已废弃，新项目用 wf_workflow_definition）
ALTER TABLE Sys_WorkFlow
  CHANGE COLUMN WorkFlow_Id Id varchar(36) NOT NULL;

-- Sys_WorkFlowStep: Vol 工作流步骤表
ALTER TABLE Sys_WorkFlowStep
  CHANGE COLUMN WorkStepFlow_Id Id varchar(36) NOT NULL,
  CHANGE COLUMN WorkFlow_Id WorkFlowId varchar(36) NULL;

-- Sys_WorkFlowTable: Vol 工作流业务表
ALTER TABLE Sys_WorkFlowTable
  CHANGE COLUMN WorkFlowTable_Id Id varchar(36) NOT NULL,
  CHANGE COLUMN WorkFlow_Id WorkFlowId varchar(36) NULL;

-- Sys_WorkFlowTableAuditLog: Vol 工作流审核日志
ALTER TABLE Sys_WorkFlowTableAuditLog
  CHANGE COLUMN WorkFlowTable_Id WorkFlowTableId varchar(36) NULL,
  CHANGE COLUMN WorkFlowTableStep_Id WorkFlowTableStepId varchar(36) NULL;

-- Sys_WorkFlowTableStep: Vol 工作流业务步骤
ALTER TABLE Sys_WorkFlowTableStep
  CHANGE COLUMN Sys_WorkFlowTableStep_Id Id varchar(36) NOT NULL,
  CHANGE COLUMN WorkFlowTable_Id WorkFlowTableId varchar(36) NOT NULL,
  CHANGE COLUMN WorkFlow_Id WorkFlowId varchar(36) NULL;

-- Sys_TableInfo: Vol 表元数据
ALTER TABLE Sys_TableInfo
  CHANGE COLUMN Table_Id Id int NOT NULL AUTO_INCREMENT;

-- Sys_TableColumn: Vol 列元数据
ALTER TABLE Sys_TableColumn
  CHANGE COLUMN Table_Id TableId int NULL;
