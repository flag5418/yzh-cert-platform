-- ============================================================
-- 全链路命名一致性统一：DB 列名 snake_case → PascalCase
-- 创建时间：2026-09-13
-- 涉及表：6 张，共 55 列
-- 
-- 执行方式：mysql -uroot -pYzh123456. yzh_cert_platform < rename_columns_pascalcase.sql
-- 回滚方式：见文件末尾 Rollback 部分
-- ============================================================

-- ────────────────────────────────────────────────────────────
-- 1. cert_iso_standard（9 列）
-- ────────────────────────────────────────────────────────────
ALTER TABLE `cert_iso_standard`
  CHANGE COLUMN `cb_code` `CbCode` varchar(50) DEFAULT NULL COMMENT '机构编号',
  CHANGE COLUMN `standard_code` `StandardCode` varchar(50) NOT NULL COMMENT '标准编码',
  CHANGE COLUMN `standard_name` `StandardName` varchar(200) NOT NULL COMMENT '标准名称',
  CHANGE COLUMN `version_year` `VersionYear` int DEFAULT NULL COMMENT '版本年份',
  CHANGE COLUMN `category` `Category` varchar(50) DEFAULT 'quality' COMMENT '分类',
  CHANGE COLUMN `description` `Description` text COMMENT '说明',
  CHANGE COLUMN `parent_code` `ParentCode` varchar(100) DEFAULT NULL COMMENT '父级编码',
  CHANGE COLUMN `is_leaf` `IsLeaf` tinyint(1) NOT NULL DEFAULT 1 COMMENT '是否叶子节点',
  CHANGE COLUMN `status` `Status` varchar(50) DEFAULT NULL COMMENT '状态';

-- ────────────────────────────────────────────────────────────
-- 2. cert_phase_definition（6 列）
-- ────────────────────────────────────────────────────────────
ALTER TABLE `cert_phase_definition`
  CHANGE COLUMN `id` `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键',
  CHANGE COLUMN `code` `Code` varchar(36) NOT NULL COMMENT '业务编码',
  CHANGE COLUMN `phase_code` `PhaseCode` varchar(20) NOT NULL COMMENT '阶段编码',
  CHANGE COLUMN `phase_name` `PhaseName` varchar(100) NOT NULL COMMENT '阶段名称',
  CHANGE COLUMN `sequence_order` `SequenceOrder` int NOT NULL DEFAULT 0 COMMENT '顺序',
  CHANGE COLUMN `description` `Description` text COMMENT '说明';

-- ────────────────────────────────────────────────────────────
-- 3. cert_certification_body（16 列）
-- ────────────────────────────────────────────────────────────
ALTER TABLE `cert_certification_body`
  CHANGE COLUMN `name` `Name` varchar(200) NOT NULL COMMENT '机构名称',
  CHANGE COLUMN `short_name` `ShortName` varchar(100) DEFAULT NULL COMMENT '简称',
  CHANGE COLUMN `cb_code` `CbCode` varchar(50) DEFAULT NULL COMMENT 'CNAS编号',
  CHANGE COLUMN `legal_person` `LegalPerson` varchar(100) DEFAULT NULL COMMENT '法人',
  CHANGE COLUMN `contact_name` `ContactName` varchar(50) DEFAULT NULL COMMENT '联系人',
  CHANGE COLUMN `contact_phone` `ContactPhone` varchar(20) DEFAULT NULL COMMENT '联系电话',
  CHANGE COLUMN `contact_email` `ContactEmail` varchar(200) DEFAULT NULL COMMENT '邮箱',
  CHANGE COLUMN `address` `Address` varchar(500) DEFAULT NULL COMMENT '地址',
  CHANGE COLUMN `logo_url` `LogoUrl` varchar(500) DEFAULT NULL COMMENT 'Logo',
  CHANGE COLUMN `scope_text` `ScopeText` text COMMENT '业务范围',
  CHANGE COLUMN `theme_config` `ThemeConfig` text COMMENT '主题配置',
  CHANGE COLUMN `login_config` `LoginConfig` text COMMENT '登录配置',
  CHANGE COLUMN `max_users` `MaxUsers` int DEFAULT 100 COMMENT '最大用户数',
  CHANGE COLUMN `max_enterprises` `MaxEnterprises` int DEFAULT 1000 COMMENT '最大企业数',
  CHANGE COLUMN `expire_date` `ExpireDate` datetime DEFAULT NULL COMMENT '到期日期',
  CHANGE COLUMN `status` `Status` varchar(50) DEFAULT NULL COMMENT '状态';

-- ────────────────────────────────────────────────────────────
-- 4. audit_task（11 列）
-- ────────────────────────────────────────────────────────────
ALTER TABLE `audit_task`
  CHANGE COLUMN `creator` `Creator` varchar(50) DEFAULT NULL COMMENT '创建人',
  CHANGE COLUMN `create_date` `CreateDate` datetime DEFAULT NULL COMMENT '创建时间',
  CHANGE COLUMN `modifier` `Modifier` varchar(50) DEFAULT NULL COMMENT '修改人',
  CHANGE COLUMN `modify_date` `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  CHANGE COLUMN `deleter` `Deleter` varchar(50) DEFAULT NULL COMMENT '删除人',
  CHANGE COLUMN `delete_time` `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  CHANGE COLUMN `status` `Status` varchar(50) DEFAULT NULL COMMENT '状态',
  CHANGE COLUMN `enable` `Enable` tinyint DEFAULT NULL COMMENT '启用',
  CHANGE COLUMN `create_by` `CreateBy` varchar(50) DEFAULT NULL COMMENT '创建人',
  CHANGE COLUMN `update_by` `UpdateBy` varchar(50) DEFAULT NULL COMMENT '更新人',
  CHANGE COLUMN `delete_by` `DeleteBy` varchar(50) DEFAULT NULL COMMENT '删除人';

-- ────────────────────────────────────────────────────────────
-- 5. cert_enterprise（3 列）
-- ────────────────────────────────────────────────────────────
ALTER TABLE `cert_enterprise`
  CHANGE COLUMN `province` `Province` varchar(50) DEFAULT NULL COMMENT '省份',
  CHANGE COLUMN `city` `City` varchar(50) DEFAULT NULL COMMENT '城市',
  CHANGE COLUMN `status` `Status` tinyint DEFAULT NULL COMMENT '状态';

-- ────────────────────────────────────────────────────────────
-- 6. cert_auditor_profile（10 列）
-- ────────────────────────────────────────────────────────────
ALTER TABLE `cert_auditor_profile`
  CHANGE COLUMN `id` `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键',
  CHANGE COLUMN `code` `Code` varchar(36) NOT NULL COMMENT '业务编码',
  CHANGE COLUMN `user_code` `UserCode` varchar(50) DEFAULT NULL COMMENT '用户编码',
  CHANGE COLUMN `auditor_no` `AuditorNo` varchar(50) NOT NULL COMMENT '审核员编号',
  CHANGE COLUMN `auditor_name` `AuditorName` varchar(100) NOT NULL COMMENT '审核员姓名',
  CHANGE COLUMN `phone` `Phone` varchar(20) NOT NULL COMMENT '电话',
  CHANGE COLUMN `email` `Email` varchar(200) DEFAULT NULL COMMENT '邮箱',
  CHANGE COLUMN `qualification` `Qualification` json DEFAULT NULL COMMENT '资质',
  CHANGE COLUMN `expertise_areas` `ExpertiseAreas` json DEFAULT NULL COMMENT '专业领域',
  CHANGE COLUMN `status` `Status` varchar(20) NOT NULL DEFAULT 'active' COMMENT '状态';

-- ============================================================
-- 验证查询
-- ============================================================
-- SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS 
-- WHERE TABLE_SCHEMA = 'yzh_cert_platform' 
-- AND COLUMN_NAME NOT REGEXP '^[A-Z]' 
-- AND TABLE_NAME IN ('cert_iso_standard','cert_phase_definition','cert_certification_body','audit_task','cert_enterprise','cert_auditor_profile')
-- ORDER BY TABLE_NAME;

-- ============================================================
-- Rollback（回滚脚本）
-- ============================================================
-- 如需回滚，执行以下脚本将列名恢复为 snake_case：
--
-- ALTER TABLE `cert_iso_standard`
--   CHANGE COLUMN `CbCode` `cb_code` varchar(50),
--   CHANGE COLUMN `StandardCode` `standard_code` varchar(50) NOT NULL,
--   CHANGE COLUMN `StandardName` `standard_name` varchar(200) NOT NULL,
--   CHANGE COLUMN `VersionYear` `version_year` int,
--   CHANGE COLUMN `Category` `category` varchar(50) DEFAULT 'quality',
--   CHANGE COLUMN `Description` `description` text,
--   CHANGE COLUMN `ParentCode` `parent_code` varchar(100),
--   CHANGE COLUMN `IsLeaf` `is_leaf` tinyint(1) NOT NULL DEFAULT 1,
--   CHANGE COLUMN `Status` `status` varchar(50);
--
-- ALTER TABLE `cert_phase_definition`
--   CHANGE COLUMN `Id` `id` bigint NOT NULL AUTO_INCREMENT,
--   CHANGE COLUMN `Code` `code` varchar(36) NOT NULL,
--   CHANGE COLUMN `PhaseCode` `phase_code` varchar(20) NOT NULL,
--   CHANGE COLUMN `PhaseName` `phase_name` varchar(100) NOT NULL,
--   CHANGE COLUMN `SequenceOrder` `sequence_order` int NOT NULL DEFAULT 0,
--   CHANGE COLUMN `Description` `description` text;
--
-- ALTER TABLE `cert_certification_body`
--   CHANGE COLUMN `Name` `name` varchar(200) NOT NULL,
--   CHANGE COLUMN `ShortName` `short_name` varchar(100),
--   CHANGE COLUMN `CbCode` `cb_code` varchar(50),
--   CHANGE COLUMN `LegalPerson` `legal_person` varchar(100),
--   CHANGE COLUMN `ContactName` `contact_name` varchar(50),
--   CHANGE COLUMN `ContactPhone` `contact_phone` varchar(20),
--   CHANGE COLUMN `ContactEmail` `contact_email` varchar(200),
--   CHANGE COLUMN `Address` `address` varchar(500),
--   CHANGE COLUMN `LogoUrl` `logo_url` varchar(500),
--   CHANGE COLUMN `ScopeText` `scope_text` text,
--   CHANGE COLUMN `ThemeConfig` `theme_config` text,
--   CHANGE COLUMN `LoginConfig` `login_config` text,
--   CHANGE COLUMN `MaxUsers` `max_users` int DEFAULT 100,
--   CHANGE COLUMN `MaxEnterprises` `max_enterprises` int DEFAULT 1000,
--   CHANGE COLUMN `ExpireDate` `expire_date` datetime,
--   CHANGE COLUMN `Status` `status` varchar(50);
--
-- ALTER TABLE `audit_task`
--   CHANGE COLUMN `Creator` `creator` varchar(50),
--   CHANGE COLUMN `CreateDate` `create_date` datetime,
--   CHANGE COLUMN `Modifier` `modifier` varchar(50),
--   CHANGE COLUMN `ModifyDate` `modify_date` datetime,
--   CHANGE COLUMN `Deleter` `deleter` varchar(50),
--   CHANGE COLUMN `DeleteTime` `delete_time` datetime,
--   CHANGE COLUMN `Status` `status` varchar(50),
--   CHANGE COLUMN `Enable` `enable` tinyint,
--   CHANGE COLUMN `CreateBy` `create_by` varchar(50),
--   CHANGE COLUMN `UpdateBy` `update_by` varchar(50),
--   CHANGE COLUMN `DeleteBy` `delete_by` varchar(50);
--
-- ALTER TABLE `cert_enterprise`
--   CHANGE COLUMN `Province` `province` varchar(50),
--   CHANGE COLUMN `City` `city` varchar(50),
--   CHANGE COLUMN `Status` `status` tinyint;
--
-- ALTER TABLE `cert_auditor_profile`
--   CHANGE COLUMN `Id` `id` bigint NOT NULL AUTO_INCREMENT,
--   CHANGE COLUMN `Code` `code` varchar(36) NOT NULL,
--   CHANGE COLUMN `UserCode` `user_code` varchar(50),
--   CHANGE COLUMN `AuditorNo` `auditor_no` varchar(50) NOT NULL,
--   CHANGE COLUMN `AuditorName` `auditor_name` varchar(100) NOT NULL,
--   CHANGE COLUMN `Phone` `phone` varchar(20) NOT NULL,
--   CHANGE COLUMN `Email` `email` varchar(200),
--   CHANGE COLUMN `Qualification` `qualification` json,
--   CHANGE COLUMN `ExpertiseAreas` `expertise_areas` json,
--   CHANGE COLUMN `Status` `status` varchar(20) NOT NULL DEFAULT 'active';
