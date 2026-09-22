-- ===================================================================
-- 全量 snake_case → PascalCase 最终清理（V2：增量补漏）
-- 日期：2026-09-21
-- 原则：数据库/C#/TypeScript 三端统一，全部使用 PascalCase
--
-- 已完成（无需再改）：
--   ✅ cert_ai_usage_log (5列)
--   ✅ yzh_queue (id/code/status)
--   ✅ yzh_queue_task (id/code/status)
--
-- 本次修复：
--   - wf_skill_api (6列 + 外键处理)
--   - wf_skill_input (1列)
--   - yzh_queue_resource_lock (1列：DROP 冗余 create_time)
--   - yzh_field_config (33列)
--   - yzh_page_config (20列)
--   - Vol 框架内置表 (8张)
--
-- 执行后需同步修改：C# 实体属性名 + TypeScript 接口字段名
-- ===================================================================

USE yzh_cert_platform;

-- ===================================================================
-- 第一部分：YZH/Core 业务表（剩余）
-- ===================================================================

-- wf_skill_api: Skill API 配置
-- 先 DROP 外键，改名后重建
ALTER TABLE wf_skill_api DROP FOREIGN KEY fk_api_skill;
ALTER TABLE wf_skill_api
  CHANGE COLUMN skill_code SkillCode varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  CHANGE COLUMN http_method HttpMethod varchar(10) NOT NULL DEFAULT 'POST',
  CHANGE COLUMN auth_config AuthConfig text NULL,
  CHANGE COLUMN param_mapping ParamMapping text NULL,
  CHANGE COLUMN response_mapping ResponseMapping text NULL,
  CHANGE COLUMN timeout_seconds TimeoutSeconds int NOT NULL DEFAULT 30;
-- 重建外键
ALTER TABLE wf_skill_api ADD CONSTRAINT fk_api_skill FOREIGN KEY (SkillCode) REFERENCES wf_skill (SkillCode);

-- wf_skill_input: Skill 输入定义
ALTER TABLE wf_skill_input
  CHANGE COLUMN enum_values EnumValues text NULL;

-- yzh_queue_resource_lock: 队列资源锁（create_time 与 CreateTime 重复，DROP）
ALTER TABLE yzh_queue_resource_lock
  DROP COLUMN create_time;

-- yzh_field_config: 字段配置表（完整 PascalCase 化）
ALTER TABLE yzh_field_config
  CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT,
  CHANGE COLUMN page_key PageKey varchar(50) NOT NULL,
  CHANGE COLUMN field_name FieldName varchar(50) NOT NULL,
  CHANGE COLUMN field_alias FieldAlias varchar(100) NULL,
  CHANGE COLUMN xs_flag XsFlag tinyint NULL DEFAULT 1,
  CHANGE COLUMN column_sxh ColumnSxh int NULL DEFAULT 0,
  CHANGE COLUMN column_title ColumnTitle varchar(100) NULL,
  CHANGE COLUMN column_width ColumnWidth int NULL DEFAULT 120,
  CHANGE COLUMN column_fixed ColumnFixed varchar(10) NULL,
  CHANGE COLUMN column_formatter ColumnFormatter varchar(50) NULL,
  CHANGE COLUMN show_overflow ShowOverflow tinyint NULL DEFAULT 1,
  CHANGE COLUMN bc_flag BcFlag tinyint NULL DEFAULT 1,
  CHANGE COLUMN form_title FormTitle varchar(100) NULL,
  CHANGE COLUMN control_type ControlType varchar(20) NULL DEFAULT 'input',
  CHANGE COLUMN grid_row GridRow int NULL DEFAULT 0,
  CHANGE COLUMN grid_col GridCol int NULL DEFAULT 0,
  CHANGE COLUMN grid_row_span GridRowSpan int NULL DEFAULT 1,
  CHANGE COLUMN grid_col_span GridColSpan int NULL DEFAULT 1,
  CHANGE COLUMN default_value DefaultValue varchar(500) NULL,
  CHANGE COLUMN min_val MinVal decimal(18,6) NULL,
  CHANGE COLUMN max_val MaxVal decimal(18,6) NULL,
  CHANGE COLUMN textarea_rows TextareaRows int NULL DEFAULT 3,
  CHANGE COLUMN data_key DataKey varchar(50) NULL,
  CHANGE COLUMN remote_url RemoteUrl varchar(255) NULL,
  CHANGE COLUMN group_index GroupIndex int NULL DEFAULT 0,
  CHANGE COLUMN search_flag SearchFlag tinyint NULL DEFAULT 0,
  CHANGE COLUMN search_title SearchTitle varchar(100) NULL,
  CHANGE COLUMN search_placeholder SearchPlaceholder varchar(100) NULL,
  CHANGE COLUMN search_control_type SearchControlType varchar(20) NULL,
  CHANGE COLUMN search_width SearchWidth int NULL DEFAULT 180,
  CHANGE COLUMN org_code OrgCode varchar(50) NULL,
  CHANGE COLUMN code Code varchar(64) NOT NULL;

-- yzh_page_config: 页面配置表（完整 PascalCase 化）
ALTER TABLE yzh_page_config
  CHANGE COLUMN id Id bigint NOT NULL AUTO_INCREMENT,
  CHANGE COLUMN page_key PageKey varchar(50) NOT NULL,
  CHANGE COLUMN page_title PageTitle varchar(100) NOT NULL,
  CHANGE COLUMN entity_name EntityName varchar(100) NOT NULL,
  CHANGE COLUMN table_name TableName varchar(100) NOT NULL,
  CHANGE COLUMN controller_name ControllerName varchar(100) NOT NULL,
  CHANGE COLUMN key_field KeyField varchar(50) NULL DEFAULT 'Id',
  CHANGE COLUMN key_field_type KeyFieldType varchar(10) NULL DEFAULT 'number',
  CHANGE COLUMN sort_field SortField varchar(50) NULL,
  CHANGE COLUMN sort_order SortOrder varchar(5) NULL DEFAULT 'desc',
  CHANGE COLUMN dialog_width DialogWidth int NULL DEFAULT 960,
  CHANGE COLUMN dialog_max_height DialogMaxHeight varchar(20) NULL DEFAULT '85vh',
  CHANGE COLUMN dialog_label_width DialogLabelWidth int NULL DEFAULT 120,
  CHANGE COLUMN row_height RowHeight varchar(10) NULL DEFAULT 'default',
  CHANGE COLUMN show_row_number ShowRowNumber tinyint NULL DEFAULT 1,
  CHANGE COLUMN search_mode SearchMode varchar(10) NULL DEFAULT 'fixed',
  CHANGE COLUMN visible_buttons VisibleButtons text NULL,
  CHANGE COLUMN checkbox_selection CheckboxSelection tinyint NULL DEFAULT 1,
  CHANGE COLUMN incremental_update IncrementalUpdate tinyint NULL DEFAULT 1,
  CHANGE COLUMN org_code OrgCode varchar(50) NULL,
  CHANGE COLUMN code Code varchar(64) NOT NULL;

-- ===================================================================
-- 第二部分：Vol 框架内置表（谨慎执行）
-- ===================================================================

-- Sys_User 表
ALTER TABLE Sys_User
  CHANGE COLUMN User_Id Id int NOT NULL AUTO_INCREMENT,
  CHANGE COLUMN Role_Id RoleId int NOT NULL DEFAULT 0,
  CHANGE COLUMN Dept_Id DeptId int NULL,
  CHANGE COLUMN wechat_openid WechatOpenid varchar(64) NULL,
  CHANGE COLUMN wechat_unionid WechatUnionid varchar(64) NULL;

-- Sys_Role 表
ALTER TABLE Sys_Role
  CHANGE COLUMN Role_Id Id int NOT NULL AUTO_INCREMENT,
  CHANGE COLUMN Dept_Id DeptId int NULL;

-- Sys_RoleAuth 表
ALTER TABLE Sys_RoleAuth
  CHANGE COLUMN Auth_Id Id int NOT NULL AUTO_INCREMENT,
  CHANGE COLUMN Menu_Id MenuId int NOT NULL,
  CHANGE COLUMN Role_Id RoleId int NULL,
  CHANGE COLUMN User_Id UserId int NULL;

-- Sys_Menu 表
ALTER TABLE Sys_Menu
  CHANGE COLUMN Menu_Id Id int NOT NULL AUTO_INCREMENT;

-- Sys_Dictionary 表
ALTER TABLE Sys_Dictionary
  CHANGE COLUMN Dic_ID Id int NOT NULL AUTO_INCREMENT;

-- Sys_DictionaryList 表
ALTER TABLE Sys_DictionaryList
  CHANGE COLUMN DicList_ID Id int NOT NULL AUTO_INCREMENT;

-- SellOrder 表
ALTER TABLE SellOrder
  CHANGE COLUMN Order_Id Id varchar(36) NOT NULL;

-- SellOrderList 表
ALTER TABLE SellOrderList
  CHANGE COLUMN OrderList_Id Id varchar(36) NOT NULL,
  CHANGE COLUMN Order_Id OrderId varchar(36) NOT NULL;
