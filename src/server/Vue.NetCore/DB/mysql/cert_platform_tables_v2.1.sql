-- ============================================================================
-- 体系认证平台 - 数据库建表脚本 V2.1
-- ============================================================================
-- 版本：V2.1
-- 日期：2026-07-30
-- 说明：
--   1. 所有业务表继承 BaseEntity 基类（id, code, create_by, create_time, update_by, update_time, delete_by, delete_time）
--   2. 表间关联使用 code（GUID）字段，用户关联除外
--   3. 表名按数据域添加前缀：cert_, ent_, audit_, rpt_, sys_, wf_
--   4. 字符集：utf8mb4，排序规则：utf8mb4_general_ci
-- ============================================================================

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ============================================================================
-- 域 A：认证体系配置（13 张表）- 前缀：cert_
-- ============================================================================

-- A-01 cert_certification_body（认证机构）
DROP TABLE IF EXISTS `cert_certification_body`;
CREATE TABLE `cert_certification_body` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `name` varchar(200) NOT NULL COMMENT '机构全称',
    `short_name` varchar(100) DEFAULT NULL COMMENT '简称',
    `cb_code` varchar(50) DEFAULT NULL COMMENT 'CNAS认可编号',
    `status` enum('active','inactive') DEFAULT 'active' COMMENT '是否启用',
    `contact_name` varchar(50) DEFAULT NULL COMMENT '联系人',
    `contact_phone` varchar(20) DEFAULT NULL COMMENT '联系电话',
    `notes` text COMMENT '备注',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_name` (`name`),
    UNIQUE KEY `uk_cb_code` (`cb_code`),
    KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='认证机构';

-- A-02 cert_iso_standard（ISO 标准）
DROP TABLE IF EXISTS `cert_iso_standard`;
CREATE TABLE `cert_iso_standard` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `cb_code` varchar(36) NOT NULL COMMENT '所属认证机构编码',
    `standard_code` varchar(50) NOT NULL COMMENT '标准编号（如 ISO 9001:2015）',
    `standard_name` varchar(200) NOT NULL COMMENT '标准中文名称',
    `version_year` year NOT NULL COMMENT '版本年份',
    `status` enum('implemented','pending','deprecated') DEFAULT 'pending' COMMENT '实施状态',
    `notes` text COMMENT '备注',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_cb_code` (`cb_code`),
    KEY `idx_standard_code` (`standard_code`),
    CONSTRAINT `fk_iso_standard_cb` FOREIGN KEY (`cb_code`) REFERENCES `cert_certification_body` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='ISO标准';

-- A-03 cert_iso_clause（标准条款）
DROP TABLE IF EXISTS `cert_iso_clause`;
CREATE TABLE `cert_iso_clause` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `standard_code` varchar(36) NOT NULL COMMENT '所属标准编码',
    `parent_code` varchar(36) DEFAULT NULL COMMENT '父条款编码（树形结构）',
    `clause_number` varchar(20) NOT NULL COMMENT '条款编号（如 7.1、7.1.1）',
    `title` varchar(200) NOT NULL COMMENT '条款标题',
    `description` text COMMENT '条款原文或摘要',
    `sort_order` int DEFAULT 0 COMMENT '排序',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_standard_code` (`standard_code`),
    KEY `idx_parent_code` (`parent_code`),
    KEY `idx_clause_number` (`clause_number`),
    CONSTRAINT `fk_iso_clause_standard` FOREIGN KEY (`standard_code`) REFERENCES `cert_iso_standard` (`code`),
    CONSTRAINT `fk_iso_clause_parent` FOREIGN KEY (`parent_code`) REFERENCES `cert_iso_clause` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='标准条款';

-- A-04 cert_phase_definition（阶段定义）
DROP TABLE IF EXISTS `cert_phase_definition`;
CREATE TABLE `cert_phase_definition` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `phase_code` varchar(20) NOT NULL COMMENT '阶段编码（S1/S2/Surv1/Surv2/Recert）',
    `phase_name` varchar(100) NOT NULL COMMENT '中文名称',
    `sequence_order` int NOT NULL COMMENT '顺序（1=S1 2=S2 3=一监 4=二监 5=再认证）',
    `description` text COMMENT '阶段说明',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_phase_code` (`phase_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='阶段定义';

-- A-05 cert_standard_phase_config（标准-阶段配置）
DROP TABLE IF EXISTS `cert_standard_phase_config`;
CREATE TABLE `cert_standard_phase_config` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `standard_code` varchar(36) NOT NULL COMMENT '标准编码',
    `phase_code` varchar(36) NOT NULL COMMENT '阶段编码',
    `required_clauses` json COMMENT '此阶段需检查的条款编码列表',
    `required_files` json COMMENT '此阶段必需的文件清单编码列表',
    `notes` text COMMENT '备注',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_standard_phase` (`standard_code`, `phase_code`),
    KEY `idx_standard_code` (`standard_code`),
    KEY `idx_phase_code` (`phase_code`),
    CONSTRAINT `fk_spconfig_standard` FOREIGN KEY (`standard_code`) REFERENCES `cert_iso_standard` (`code`),
    CONSTRAINT `fk_spconfig_phase` FOREIGN KEY (`phase_code`) REFERENCES `cert_phase_definition` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='标准-阶段配置';

-- A-06 cert_directory_template（文件目录模板）
DROP TABLE IF EXISTS `cert_directory_template`;
CREATE TABLE `cert_directory_template` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `config_code` varchar(36) NOT NULL COMMENT '所属标准-阶段配置编码',
    `parent_code` varchar(36) DEFAULT NULL COMMENT '父文件夹编码（树形结构）',
    `folder_name` varchar(200) NOT NULL COMMENT '文件夹名称',
    `sort_order` int DEFAULT 0 COMMENT '排序',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_config_code` (`config_code`),
    KEY `idx_parent_code` (`parent_code`),
    CONSTRAINT `fk_dirtemplate_config` FOREIGN KEY (`config_code`) REFERENCES `cert_standard_phase_config` (`code`),
    CONSTRAINT `fk_dirtemplate_parent` FOREIGN KEY (`parent_code`) REFERENCES `cert_directory_template` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文件目录模板';

-- A-07 cert_file_requirement（文件要求）
DROP TABLE IF EXISTS `cert_file_requirement`;
CREATE TABLE `cert_file_requirement` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `folder_code` varchar(36) NOT NULL COMMENT '所属文件夹编码',
    `file_name_template` varchar(200) NOT NULL COMMENT '文件名称模板',
    `file_type` varchar(50) NOT NULL COMMENT '允许的文件类型（pdf/docx/xlsx/png 等）',
    `is_required` tinyint(1) DEFAULT 1 COMMENT '是否必须提供',
    `max_size_mb` int DEFAULT 10 COMMENT '最大文件大小（MB）',
    `description` text COMMENT '文件说明/要求描述',
    `sort_order` int DEFAULT 0 COMMENT '排序',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_folder_code` (`folder_code`),
    CONSTRAINT `fk_filereq_folder` FOREIGN KEY (`folder_code`) REFERENCES `cert_directory_template` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文件要求';

-- A-08 cert_extraction_rule（数据提取规则）
DROP TABLE IF EXISTS `cert_extraction_rule`;
CREATE TABLE `cert_extraction_rule` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `file_requirement_code` varchar(36) NOT NULL COMMENT '适用文件类型编码',
    `skill_code` varchar(36) NOT NULL COMMENT '使用的Skill编码',
    `rule_type` enum('title','table','text','form','mixed') NOT NULL COMMENT '提取规则类型',
    `rule_config` json NOT NULL COMMENT '规则配置（参数、提取逻辑）',
    `description` text COMMENT '规则说明',
    `is_active` tinyint(1) DEFAULT 1 COMMENT '是否启用',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_filereq_code` (`file_requirement_code`),
    KEY `idx_skill_code` (`skill_code`),
    CONSTRAINT `fk_extreq_filereq` FOREIGN KEY (`file_requirement_code`) REFERENCES `cert_file_requirement` (`code`),
    CONSTRAINT `fk_extreq_skill` FOREIGN KEY (`skill_code`) REFERENCES `wf_skill` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='数据提取规则';

-- A-09 cert_extraction_field（提取字段定义）
DROP TABLE IF EXISTS `cert_extraction_field`;
CREATE TABLE `cert_extraction_field` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `rule_code` varchar(36) NOT NULL COMMENT '所属提取规则编码',
    `skill_code` varchar(36) DEFAULT NULL COMMENT '提取此字段的Skill（可覆盖规则级Skill）',
    `field_code` varchar(100) NOT NULL COMMENT '字段编码（如 iso9001.ent_base.biz_lic.name）',
    `label_tag` varchar(500) NOT NULL COMMENT '字段标签（如 [ISO9001_企业基础资料_营业执照_企业名称]）',
    `field_name` varchar(100) NOT NULL COMMENT '字段显示名称',
    `field_type` enum('string','number','date','boolean','enum','list') DEFAULT 'string' COMMENT '字段数据类型',
    `enum_values` json COMMENT '枚举值列表（field_type=enum 时）',
    `sort_order` int DEFAULT 0 COMMENT '排序',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_label_tag` (`label_tag`),
    KEY `idx_rule_code` (`rule_code`),
    KEY `idx_field_code` (`field_code`),
    CONSTRAINT `fk_extfield_rule` FOREIGN KEY (`rule_code`) REFERENCES `cert_extraction_rule` (`code`),
    CONSTRAINT `fk_extfield_skill` FOREIGN KEY (`skill_code`) REFERENCES `wf_skill` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='提取字段定义';

-- A-10 cert_validation_rule（校验规则）
DROP TABLE IF EXISTS `cert_validation_rule`;
CREATE TABLE `cert_validation_rule` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `standard_code` varchar(36) NOT NULL COMMENT '适用标准编码',
    `phase_code` varchar(36) NOT NULL COMMENT '适用阶段编码',
    `clause_code` varchar(36) NOT NULL COMMENT '对应条款编码',
    `workflow_code` varchar(36) NOT NULL COMMENT '关联的工作流定义编码',
    `rule_code` varchar(50) NOT NULL COMMENT '规则编码',
    `rule_name` varchar(200) NOT NULL COMMENT '规则名称',
    `severity_if_violated` enum('major','minor','observation') NOT NULL COMMENT '触发时的NC严重度',
    `nc_description_template` text COMMENT 'NC描述模板',
    `is_active` tinyint(1) DEFAULT 1 COMMENT '是否启用',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_rule_code` (`rule_code`),
    KEY `idx_standard_code` (`standard_code`),
    KEY `idx_phase_code` (`phase_code`),
    KEY `idx_clause_code` (`clause_code`),
    KEY `idx_workflow_code` (`workflow_code`),
    CONSTRAINT `fk_valrule_standard` FOREIGN KEY (`standard_code`) REFERENCES `cert_iso_standard` (`code`),
    CONSTRAINT `fk_valrule_phase` FOREIGN KEY (`phase_code`) REFERENCES `cert_phase_definition` (`code`),
    CONSTRAINT `fk_valrule_clause` FOREIGN KEY (`clause_code`) REFERENCES `cert_iso_clause` (`code`),
    CONSTRAINT `fk_valrule_workflow` FOREIGN KEY (`workflow_code`) REFERENCES `wf_workflow_definition` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='校验规则';

-- A-11 cert_validation_rule_source（校验规则溯源）
DROP TABLE IF EXISTS `cert_validation_rule_source`;
CREATE TABLE `cert_validation_rule_source` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `rule_code` varchar(36) NOT NULL COMMENT '校验规则编码',
    `file_requirement_code` varchar(36) NOT NULL COMMENT '溯源文件类型编码',
    `source_path` varchar(500) DEFAULT NULL COMMENT '溯源路径（文件内位置描述）',
    `notes` text COMMENT '备注',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_rule_code` (`rule_code`),
    KEY `idx_filereq_code` (`file_requirement_code`),
    CONSTRAINT `fk_valsource_rule` FOREIGN KEY (`rule_code`) REFERENCES `cert_validation_rule` (`code`),
    CONSTRAINT `fk_valsource_filereq` FOREIGN KEY (`file_requirement_code`) REFERENCES `cert_file_requirement` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='校验规则溯源';

-- A-12 cert_report_template（报告模板）
DROP TABLE IF EXISTS `cert_report_template`;
CREATE TABLE `cert_report_template` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `cb_code` varchar(36) NOT NULL COMMENT '认证机构编码',
    `standard_code` varchar(36) NOT NULL COMMENT '标准编码',
    `phase_code` varchar(36) NOT NULL COMMENT '阶段编码',
    `template_name` varchar(200) NOT NULL COMMENT '模板名称',
    `template_file_path` varchar(500) DEFAULT NULL COMMENT '空白文档文件路径（MinIO）',
    `section_config` json COMMENT '报告章节配置（含每章节的 workflow_id、clause_id 映射）',
    `is_default` tinyint(1) DEFAULT 0 COMMENT '是否默认模板',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_cb_code` (`cb_code`),
    KEY `idx_standard_code` (`standard_code`),
    KEY `idx_phase_code` (`phase_code`),
    CONSTRAINT `fk_rpttmpl_cb` FOREIGN KEY (`cb_code`) REFERENCES `cert_certification_body` (`code`),
    CONSTRAINT `fk_rpttmpl_standard` FOREIGN KEY (`standard_code`) REFERENCES `cert_iso_standard` (`code`),
    CONSTRAINT `fk_rpttmpl_phase` FOREIGN KEY (`phase_code`) REFERENCES `cert_phase_definition` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告模板';

-- A-13 cert_clause_extraction_rule（条款提取规则）
DROP TABLE IF EXISTS `cert_clause_extraction_rule`;
CREATE TABLE `cert_clause_extraction_rule` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `clause_code` varchar(36) NOT NULL COMMENT '条款编码',
    `workflow_code` varchar(36) NOT NULL COMMENT '关联的提取工作流编码',
    `description` text COMMENT '规则集说明',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_clause_code` (`clause_code`),
    KEY `idx_workflow_code` (`workflow_code`),
    CONSTRAINT `fk_clauseext_clause` FOREIGN KEY (`clause_code`) REFERENCES `cert_iso_clause` (`code`),
    CONSTRAINT `fk_clauseext_workflow` FOREIGN KEY (`workflow_code`) REFERENCES `wf_workflow_definition` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='条款提取规则';


-- ============================================================================
-- 域 B：企业档案（9 张表）- 前缀：ent_
-- ============================================================================

-- B-01 ent_enterprise（企业）
DROP TABLE IF EXISTS `ent_enterprise`;
CREATE TABLE `ent_enterprise` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID（关联Sys_User.id）',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `name` varchar(200) NOT NULL COMMENT '企业全称',
    `short_name` varchar(100) DEFAULT NULL COMMENT '简称',
    `credit_code` varchar(50) DEFAULT NULL COMMENT '统一社会信用代码',
    `legal_person` varchar(50) DEFAULT NULL COMMENT '法人代表',
    `address` text COMMENT '企业地址',
    `cert_scope` text COMMENT '认证范围描述',
    `contact_name` varchar(50) DEFAULT NULL COMMENT '对接人姓名',
    `contact_phone` varchar(20) DEFAULT NULL COMMENT '对接人电话',
    `contact_email` varchar(200) DEFAULT NULL COMMENT '对接人邮箱',
    `status` enum('active','archived') DEFAULT 'active' COMMENT 'active=活跃 / archived=已归档',
    `archive_date` date DEFAULT NULL COMMENT '归档日期',
    `notes` text COMMENT '备注',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_credit_code` (`credit_code`),
    KEY `idx_name` (`name`),
    KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业';

-- B-02 ent_enterprise_phase（企业阶段）
DROP TABLE IF EXISTS `ent_enterprise_phase`;
CREATE TABLE `ent_enterprise_phase` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `enterprise_code` varchar(36) NOT NULL COMMENT '所属企业编码',
    `phase_code` varchar(36) NOT NULL COMMENT '阶段定义编码',
    `standard_code` varchar(36) NOT NULL COMMENT '认证标准编码',
    `status` enum('pending','in_progress','completed','closed') DEFAULT 'pending' COMMENT '状态',
    `started_at` datetime DEFAULT NULL COMMENT '开始时间',
    `completed_at` datetime DEFAULT NULL COMMENT '完成时间',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_ent_phase_std` (`enterprise_code`, `phase_code`, `standard_code`),
    KEY `idx_enterprise_code` (`enterprise_code`),
    KEY `idx_phase_code` (`phase_code`),
    KEY `idx_standard_code` (`standard_code`),
    KEY `idx_status` (`status`),
    CONSTRAINT `fk_ephase_enterprise` FOREIGN KEY (`enterprise_code`) REFERENCES `ent_enterprise` (`code`),
    CONSTRAINT `fk_ephase_phase` FOREIGN KEY (`phase_code`) REFERENCES `cert_phase_definition` (`code`),
    CONSTRAINT `fk_ephase_standard` FOREIGN KEY (`standard_code`) REFERENCES `cert_iso_standard` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业阶段';

-- B-03 ent_enterprise_document（企业文档目录）
DROP TABLE IF EXISTS `ent_enterprise_document`;
CREATE TABLE `ent_enterprise_document` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `enterprise_code` varchar(36) NOT NULL COMMENT '所属企业编码',
    `phase_code` varchar(36) DEFAULT NULL COMMENT '所属阶段编码（scope=phase时必填）',
    `scope` enum('enterprise_base','phase') NOT NULL COMMENT '资料层级：共享层 / 隔离层',
    `template_folder_code` varchar(36) DEFAULT NULL COMMENT '对应的模板文件夹编码',
    `parent_code` varchar(36) DEFAULT NULL COMMENT '父文件夹编码（树形结构）',
    `folder_name` varchar(200) NOT NULL COMMENT '文件夹名称',
    `sort_order` int DEFAULT 0 COMMENT '排序',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_enterprise_code` (`enterprise_code`),
    KEY `idx_phase_code` (`phase_code`),
    KEY `idx_parent_code` (`parent_code`),
    KEY `idx_scope` (`scope`),
    CONSTRAINT `fk_edoc_enterprise` FOREIGN KEY (`enterprise_code`) REFERENCES `ent_enterprise` (`code`),
    CONSTRAINT `fk_edoc_phase` FOREIGN KEY (`phase_code`) REFERENCES `ent_enterprise_phase` (`code`),
    CONSTRAINT `fk_edoc_template` FOREIGN KEY (`template_folder_code`) REFERENCES `cert_directory_template` (`code`),
    CONSTRAINT `fk_edoc_parent` FOREIGN KEY (`parent_code`) REFERENCES `ent_enterprise_document` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业文档目录';

-- B-04 ent_enterprise_file（企业文件）
DROP TABLE IF EXISTS `ent_enterprise_file`;
CREATE TABLE `ent_enterprise_file` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '上传人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `folder_code` varchar(36) NOT NULL COMMENT '所属文件夹编码',
    `file_name` varchar(500) NOT NULL COMMENT '文件名',
    `file_type` varchar(50) NOT NULL COMMENT '文件类型（pdf/docx/xlsx/png/jpg）',
    `file_size` bigint NOT NULL COMMENT '文件大小（bytes）',
    `storage_path` varchar(500) NOT NULL COMMENT 'MinIO存储路径',
    `file_hash` varchar(64) DEFAULT NULL COMMENT '文件SHA256哈希（增量审核依据）',
    `current_version` int DEFAULT 1 COMMENT '当前版本号',
    `notes` text COMMENT '备注',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_folder_code` (`folder_code`),
    KEY `idx_file_hash` (`file_hash`),
    CONSTRAINT `fk_efile_folder` FOREIGN KEY (`folder_code`) REFERENCES `ent_enterprise_document` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业文件';

-- B-05 ent_file_version（文件版本）
DROP TABLE IF EXISTS `ent_file_version`;
CREATE TABLE `ent_file_version` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '上传人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `file_code` varchar(36) NOT NULL COMMENT '源文件编码',
    `version_number` int NOT NULL COMMENT '版本号（从1开始递增）',
    `file_size` bigint NOT NULL COMMENT '版本文件大小',
    `storage_path` varchar(500) NOT NULL COMMENT 'MinIO存储路径',
    `file_hash` varchar(64) NOT NULL COMMENT 'SHA256哈希',
    `change_notes` text COMMENT '变更说明',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_file_version` (`file_code`, `version_number`),
    KEY `idx_file_code` (`file_code`),
    CONSTRAINT `fk_fver_file` FOREIGN KEY (`file_code`) REFERENCES `ent_enterprise_file` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文件版本';

-- B-06 ent_file_pre_check_result（资料质量预审结果）
DROP TABLE IF EXISTS `ent_file_pre_check_result`;
CREATE TABLE `ent_file_pre_check_result` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `file_code` varchar(36) NOT NULL COMMENT '被检查的文件编码',
    `version_number` int NOT NULL COMMENT '检查的文件版本',
    `check_type` enum('readability','clarity','format','completeness') NOT NULL COMMENT '检查类型',
    `check_result` enum('pass','warning','block') NOT NULL COMMENT '检查结果',
    `message` text COMMENT '检查信息',
    `detail` json COMMENT '详细信息（DPI值、倾斜角度、缺页数等）',
    `checked_at` datetime NOT NULL COMMENT '检查时间',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_file_code` (`file_code`),
    KEY `idx_check_type` (`check_type`),
    KEY `idx_check_result` (`check_result`),
    CONSTRAINT `fk_precheck_file` FOREIGN KEY (`file_code`) REFERENCES `ent_enterprise_file` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='资料质量预审结果';

-- B-07 ent_file_compliance_check（文件合规检查）
DROP TABLE IF EXISTS `ent_file_compliance_check`;
CREATE TABLE `ent_file_compliance_check` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `file_code` varchar(36) NOT NULL COMMENT '被检查的文件编码',
    `version_number` int NOT NULL COMMENT '检查的文件版本',
    `rule_code` varchar(36) NOT NULL COMMENT '触发的校验规则编码',
    `workflow_execution_code` varchar(36) DEFAULT NULL COMMENT '工作流执行记录编码',
    `check_status` enum('pass','fail','warning','blocked') NOT NULL COMMENT '检查结果',
    `message` text COMMENT '检查信息',
    `detail` json COMMENT '详细信息（含具体位置、偏离描述）',
    `checked_at` datetime NOT NULL COMMENT '检查时间',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_file_code` (`file_code`),
    KEY `idx_rule_code` (`rule_code`),
    KEY `idx_check_status` (`check_status`),
    CONSTRAINT `fk_compliance_file` FOREIGN KEY (`file_code`) REFERENCES `ent_enterprise_file` (`code`),
    CONSTRAINT `fk_compliance_rule` FOREIGN KEY (`rule_code`) REFERENCES `cert_validation_rule` (`code`),
    CONSTRAINT `fk_compliance_wexec` FOREIGN KEY (`workflow_execution_code`) REFERENCES `wf_workflow_execution_log` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文件合规检查';

-- B-08 ent_extraction_result（文档提取结果）
DROP TABLE IF EXISTS `ent_extraction_result`;
CREATE TABLE `ent_extraction_result` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `file_code` varchar(36) NOT NULL COMMENT '提取的源文件编码',
    `version_number` int NOT NULL COMMENT '提取的文件版本',
    `rule_code` varchar(36) NOT NULL COMMENT '使用的提取规则编码',
    `field_code` varchar(36) NOT NULL COMMENT '对应的提取字段编码',
    `label_tag` varchar(500) DEFAULT NULL COMMENT '字段标签冗余（便于查询）',
    `extracted_value` text COMMENT '提取的值',
    `confidence` decimal(3,2) DEFAULT NULL COMMENT 'AI提取可信度 (0.00-1.00)',
    `position_info` json DEFAULT NULL COMMENT '位置信息（页码/行号/列号/单元格）',
    `is_manual_edited` tinyint(1) DEFAULT 0 COMMENT '是否被人工修改',
    `extracted_at` datetime NOT NULL COMMENT '提取时间',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_file_code` (`file_code`),
    KEY `idx_rule_code` (`rule_code`),
    KEY `idx_field_code` (`field_code`),
    KEY `idx_label_tag` (`label_tag`),
    CONSTRAINT `k_extres_file` FOREIGN KEY (`file_code`) REFERENCES `ent_enterprise_file` (`code`),
    CONSTRAINT `fk_extres_rule` FOREIGN KEY (`rule_code`) REFERENCES `cert_extraction_rule` (`code`),
    CONSTRAINT `fk_extres_field` FOREIGN KEY (`field_code`) REFERENCES `cert_extraction_field` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文档提取结果';

-- B-09 ent_table_extraction_result（表格提取结果）
DROP TABLE IF EXISTS `ent_table_extraction_result`;
CREATE TABLE `ent_table_extraction_result` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `file_code` varchar(36) NOT NULL COMMENT '提取的源文件编码',
    `version_number` int NOT NULL COMMENT '提取的文件版本',
    `rule_code` varchar(36) NOT NULL COMMENT '使用的提取规则编码',
    `table_index` int DEFAULT 1 COMMENT '文档中第几个表格',
    `extracted_json` json NOT NULL COMMENT '表格内容（JSON）',
    `confidence` decimal(3,2) DEFAULT NULL COMMENT 'AI提取可信度',
    `position_info` json DEFAULT NULL COMMENT '表格在文档中的位置信息',
    `extracted_at` datetime NOT NULL COMMENT '提取时间',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_file_code` (`file_code`),
    KEY `idx_rule_code` (`rule_code`),
    CONSTRAINT `fk_tableext_file` FOREIGN KEY (`file_code`) REFERENCES `ent_enterprise_file` (`code`),
    CONSTRAINT `fk_tableext_rule` FOREIGN KEY (`rule_code`) REFERENCES `cert_extraction_rule` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='表格提取结果';


-- ============================================================================
-- 域 C：审核执行（6 张表）- 前缀：audit_
-- ============================================================================

-- C-01 audit_task（审核任务）
DROP TABLE IF EXISTS `audit_task`;
CREATE TABLE `audit_task` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `phase_code` varchar(36) NOT NULL COMMENT '所属企业阶段编码',
    `task_number` varchar(50) NOT NULL COMMENT '任务编号',
    `auditor_id` bigint NOT NULL COMMENT '审核员ID（关联Sys_User.id）',
    `status` enum('pending','in_progress','completed','closed') DEFAULT 'pending' COMMENT '状态',
    `planned_date` date DEFAULT NULL COMMENT '计划审核日期',
    `actual_start_date` date DEFAULT NULL COMMENT '实际开始日期',
    `actual_complete_date` date DEFAULT NULL COMMENT '实际完成日期',
    `audit_scope` text COMMENT '审核范围描述',
    `notes` text COMMENT '备注',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_task_number` (`task_number`),
    KEY `idx_phase_code` (`phase_code`),
    KEY `idx_auditor_id` (`auditor_id`),
    KEY `idx_status` (`status`),
    CONSTRAINT `fk_task_phase` FOREIGN KEY (`phase_code`) REFERENCES `ent_enterprise_phase` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='审核任务';

-- C-02 audit_checklist_item（检查表条目）
DROP TABLE IF EXISTS `audit_checklist_item`;
CREATE TABLE `audit_checklist_item` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `task_code` varchar(36) NOT NULL COMMENT '所属审核任务编码',
    `clause_code` varchar(36) NOT NULL COMMENT '对应条款编码',
    `audit_criteria` text COMMENT '审核准则（标准条款原文）',
    `finding_description` text COMMENT '审核发现描述',
    `conformity` enum('pending','conform','nonconform','observation','na') DEFAULT 'pending' COMMENT '判定结果',
    `ncs_found` int DEFAULT 0 COMMENT '发现NC数量',
    `checked_by` bigint DEFAULT NULL COMMENT '检查人ID',
    `checked_at` datetime DEFAULT NULL COMMENT '检查时间',
    `sort_order` int DEFAULT 0 COMMENT '排序',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_task_code` (`task_code`),
    KEY `idx_clause_code` (`clause_code`),
    KEY `idx_conformity` (`conformity`),
    CONSTRAINT `fk_checklist_task` FOREIGN KEY (`task_code`) REFERENCES `audit_task` (`code`),
    CONSTRAINT `fk_checklist_clause` FOREIGN KEY (`clause_code`) REFERENCES `cert_iso_clause` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='检查表条目';

-- C-03 audit_nonconformity（不符合项 / NC）
DROP TABLE IF EXISTS `audit_nonconformity`;
CREATE TABLE `audit_nonconformity` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `task_code` varchar(36) NOT NULL COMMENT '所属审核任务编码',
    `clause_code` varchar(36) NOT NULL COMMENT '对应条款编码',
    `nc_number` varchar(50) NOT NULL COMMENT 'NC编号',
    `severity` enum('major','minor','observation') NOT NULL COMMENT '严重度',
    `description` text NOT NULL COMMENT 'NC描述（不符合事实）',
    `requirement_ref` text COMMENT '违反的标准要求原文',
    `evidence_ref` text COMMENT '客观证据引用',
    `status` enum('open','rectifying','rectified','pending_verification','closed') DEFAULT 'open' COMMENT '状态',
    `source_type` enum('auto_rule','manual') DEFAULT 'manual' COMMENT 'NC来源：规则自动触发 / 手动创建',
    `source_check_code` varchar(36) DEFAULT NULL COMMENT '触发的合规检查记录编码',
    `rule_code` varchar(36) DEFAULT NULL COMMENT '触发的校验规则编码',
    `due_date` date DEFAULT NULL COMMENT '整改截止日期',
    `opened_by` bigint NOT NULL COMMENT '开具人ID',
    `opened_at` datetime NOT NULL COMMENT '开具时间',
    `closed_at` datetime DEFAULT NULL COMMENT '关闭时间',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_nc_number` (`nc_number`),
    KEY `idx_task_code` (`task_code`),
    KEY `idx_clause_code` (`clause_code`),
    KEY `idx_severity` (`severity`),
    KEY `idx_status` (`status`),
    KEY `idx_source_type` (`source_type`),
    CONSTRAINT `fk_nc_task` FOREIGN KEY (`task_code`) REFERENCES `audit_task` (`code`),
    CONSTRAINT `fk_nc_clause` FOREIGN KEY (`clause_code`) REFERENCES `cert_iso_clause` (`code`),
    CONSTRAINT `fk_nc_sourcecheck` FOREIGN KEY (`source_check_code`) REFERENCES `ent_file_compliance_check` (`code`),
    CONSTRAINT `fk_nc_rule` FOREIGN KEY (`rule_code`) REFERENCES `cert_validation_rule` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='不符合项(NC)';

-- C-04 audit_finding（审核发现明细）
DROP TABLE IF EXISTS `audit_finding`;
CREATE TABLE `audit_finding` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '记录人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `checklist_item_code` varchar(36) NOT NULL COMMENT '检查表条目编码',
    `nc_code` varchar(36) DEFAULT NULL COMMENT '关联NC编码',
    `source_file_code` varchar(36) DEFAULT NULL COMMENT '来源文件编码',
    `source_position` varchar(200) DEFAULT NULL COMMENT '来源位置（页码/行号/列号）',
    `source_content` text COMMENT '来源内容摘录',
    `finding_type` enum('conform','discrepancy','comment') NOT NULL COMMENT '发现类型',
    `description` text NOT NULL COMMENT '描述',
    `confidence` decimal(3,2) DEFAULT NULL COMMENT 'AI提取可信度 (0.00-1.00)',
    `is_manual` tinyint(1) DEFAULT 0 COMMENT '是否人工添加',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_checklist_item_code` (`checklist_item_code`),
    KEY `idx_nc_code` (`nc_code`),
    KEY `idx_source_file_code` (`source_file_code`),
    KEY `idx_finding_type` (`finding_type`),
    CONSTRAINT `fk_finding_checklist` FOREIGN KEY (`checklist_item_code`) REFERENCES `audit_checklist_item` (`code`),
    CONSTRAINT `fk_finding_nc` FOREIGN KEY (`nc_code`) REFERENCES `audit_nonconformity` (`code`),
    CONSTRAINT `fk_finding_file` FOREIGN KEY (`source_file_code`) REFERENCES `ent_enterprise_file` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='审核发现明细';

-- C-05 audit_evidence（审核证据）
DROP TABLE IF EXISTS `audit_evidence`;
CREATE TABLE `audit_evidence` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `task_code` varchar(36) NOT NULL COMMENT '所属审核任务编码',
    `clause_code` varchar(36) DEFAULT NULL COMMENT '关联条款编码',
    `evidence_type` enum('photo','audio','screenshot','video','document','other') NOT NULL COMMENT '证据类型',
    `storage_path` varchar(500) NOT NULL COMMENT 'MinIO存储路径',
    `file_hash` varchar(64) NOT NULL COMMENT 'SHA256哈希',
    `is_voided` tinyint(1) DEFAULT 0 COMMENT '是否废弃',
    `voided_at` datetime DEFAULT NULL COMMENT '废弃时间',
    `voided_by` bigint DEFAULT NULL COMMENT '废弃操作人ID',
    `captured_at` datetime DEFAULT NULL COMMENT '采集时间',
    `captured_by` bigint NOT NULL COMMENT '采集人ID',
    `notes` text COMMENT '备注',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_task_code` (`task_code`),
    KEY `idx_clause_code` (`clause_code`),
    KEY `idx_evidence_type` (`evidence_type`),
    KEY `idx_is_voided` (`is_voided`),
    CONSTRAINT `fk_evidence_task` FOREIGN KEY (`task_code`) REFERENCES `audit_task` (`code`),
    CONSTRAINT `fk_evidence_clause` FOREIGN KEY (`clause_code`) REFERENCES `cert_iso_clause` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='审核证据';

-- C-06 audit_rectification（整改记录）
DROP TABLE IF EXISTS `audit_rectification`;
CREATE TABLE `audit_rectification` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `nc_code` varchar(36) NOT NULL COMMENT '关联NC编码',
    `correction` text NOT NULL COMMENT '纠正措施描述',
    `corrective_action` text COMMENT '纠正措施（根因分析+防再发生）',
    `evidence_files` json COMMENT '整改证据文件路径列表',
    `submitted_by` bigint NOT NULL COMMENT '提交人ID',
    `submitted_at` datetime NOT NULL COMMENT '提交时间',
    `verified_by` bigint DEFAULT NULL COMMENT '复核人ID',
    `verified_at` datetime DEFAULT NULL COMMENT '复核时间',
    `verify_result` enum('approved','rejected') DEFAULT NULL COMMENT '复核结果',
    `verify_notes` text DEFAULT NULL COMMENT '复核意见',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_nc_code` (`nc_code`),
    KEY `idx_verify_result` (`verify_result`),
    CONSTRAINT `fk_rect_nc` FOREIGN KEY (`nc_code`) REFERENCES `audit_nonconformity` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='整改记录';


-- ============================================================================
-- 域 D：报告生成（4 张表）- 前缀：rpt_
-- ============================================================================

-- D-01 rpt_report_task（报告任务）
DROP TABLE IF EXISTS `rpt_report_task`;
CREATE TABLE `rpt_report_task` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `phase_code` varchar(36) NOT NULL COMMENT '所属企业阶段编码',
    `based_on_audit_task_code` varchar(36) DEFAULT NULL COMMENT '基于的审核任务编码',
    `template_code` varchar(36) NOT NULL COMMENT '使用的报告模板编码',
    `task_number` varchar(50) NOT NULL COMMENT '任务编号',
    `status` enum('draft','generated','edited','locked') DEFAULT 'draft' COMMENT '状态',
    `generated_at` datetime DEFAULT NULL COMMENT '生成时间',
    `locked_at` datetime DEFAULT NULL COMMENT '锁定时间',
    `locked_by` bigint DEFAULT NULL COMMENT '锁定人ID',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_task_number` (`task_number`),
    KEY `idx_phase_code` (`phase_code`),
    KEY `idx_audit_task_code` (`based_on_audit_task_code`),
    KEY `idx_template_code` (`template_code`),
    KEY `idx_status` (`status`),
    CONSTRAINT `fk_rpttask_phase` FOREIGN KEY (`phase_code`) REFERENCES `ent_enterprise_phase` (`code`),
    CONSTRAINT `fk_rpttask_audit` FOREIGN KEY (`based_on_audit_task_code`) REFERENCES `audit_task` (`code`),
    CONSTRAINT `fk_rpttask_template` FOREIGN KEY (`template_code`) REFERENCES `cert_report_template` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告任务';

-- D-02 rpt_audit_report（报告正文）
DROP TABLE IF EXISTS `rpt_audit_report`;
CREATE TABLE `rpt_audit_report` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `task_code` varchar(36) NOT NULL COMMENT '所属报告任务编码',
    `version_number` int DEFAULT 1 COMMENT '报告版本号',
    `report_title` varchar(500) NOT NULL COMMENT '报告标题',
    `full_content` mediumtext COMMENT '报告完整内容（Markdown/HTML）',
    `export_path` varchar(500) DEFAULT NULL COMMENT '导出的PDF/Word文件路径',
    `edited_by` bigint DEFAULT NULL COMMENT '最后编辑人ID',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_task_code` (`task_code`),
    CONSTRAINT `fk_report_task` FOREIGN KEY (`task_code`) REFERENCES `rpt_report_task` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告正文';

-- D-03 rpt_report_section（报告章节内容）
DROP TABLE IF EXISTS `rpt_report_section`;
CREATE TABLE `rpt_report_section` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `report_code` varchar(36) NOT NULL COMMENT '所属报告编码',
    `clause_code` varchar(36) DEFAULT NULL COMMENT '对应条款编码（可空，概述/结论章节不映射条款）',
    `section_name` varchar(200) NOT NULL COMMENT '章节名称',
    `section_content` text COMMENT '章节填充内容',
    `workflow_code` varchar(36) DEFAULT NULL COMMENT '生成此章节的工作流编码',
    `sort_order` int DEFAULT 0 COMMENT '章节排序',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_report_code` (`report_code`),
    KEY `idx_clause_code` (`clause_code`),
    KEY `idx_workflow_code` (`workflow_code`),
    CONSTRAINT `fk_section_report` FOREIGN KEY (`report_code`) REFERENCES `rpt_audit_report` (`code`),
    CONSTRAINT `fk_section_clause` FOREIGN KEY (`clause_code`) REFERENCES `cert_iso_clause` (`code`),
    CONSTRAINT `fk_section_workflow` FOREIGN KEY (`workflow_code`) REFERENCES `wf_workflow_definition` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告章节内容';

-- D-04 rpt_report_section_source（报告内容溯源）
DROP TABLE IF EXISTS `rpt_report_section_source`;
CREATE TABLE `rpt_report_section_source` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `section_code` varchar(36) NOT NULL COMMENT '所属报告章节编码',
    `source_type` enum('extraction','finding','nc','manual','template','compliance') NOT NULL COMMENT '来源类型',
    `source_code` varchar(36) DEFAULT NULL COMMENT '来源记录的编码（根据source_type指向不同表）',
    `source_description` text COMMENT '来源描述',
    `confidence` decimal(3,2) DEFAULT NULL COMMENT '可信度',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_section_code` (`section_code`),
    KEY `idx_source_type` (`source_type`),
    CONSTRAINT `fk_src_section` FOREIGN KEY (`section_code`) REFERENCES `rpt_report_section` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告内容溯源';


-- ============================================================================
-- 域 E：系统基础（补充 2 张表，Vol 内置 5 张保持不变）- 前缀：sys_
-- ============================================================================

-- E-06 sys_log（系统日志）
DROP TABLE IF EXISTS `sys_log`;
CREATE TABLE `sys_log` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '操作用户ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '操作时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `user_id` bigint DEFAULT NULL COMMENT '操作用户ID',
    `module` varchar(50) NOT NULL COMMENT '操作模块',
    `action` varchar(100) NOT NULL COMMENT '操作动作',
    `target_type` varchar(50) DEFAULT NULL COMMENT '操作对象类型（表名）',
    `target_id` bigint DEFAULT NULL COMMENT '操作对象ID',
    `detail` json COMMENT '操作详情（变更前后值等）',
    `ip_address` varchar(50) DEFAULT NULL COMMENT '操作IP',
    `user_agent` varchar(500) DEFAULT NULL COMMENT '用户代理',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_user_id` (`user_id`),
    KEY `idx_module` (`module`),
    KEY `idx_action` (`action`),
    KEY `idx_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='系统日志';

-- E-07 sys_config（系统参数）
DROP TABLE IF EXISTS `sys_config`;
CREATE TABLE `sys_config` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '最后修改人ID',
    `update_time` datetime DEFAULT NULL COMMENT '修改时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `config_key` varchar(100) NOT NULL COMMENT '参数键',
    `config_value` text NOT NULL COMMENT '参数值',
    `value_type` enum('string','number','boolean','json') DEFAULT 'string' COMMENT '值类型',
    `description` text COMMENT '参数说明',
    `is_system` tinyint(1) DEFAULT 0 COMMENT '是否系统级（不可删除）',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_config_key` (`config_key`),
    KEY `idx_value_type` (`value_type`),
    KEY `idx_is_system` (`is_system`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='系统参数';


-- ============================================================================
-- 域 F：工作流框架（4 张表）- 前缀：wf_
-- ============================================================================

-- F-01 wf_skill（Skill 定义）
DROP TABLE IF EXISTS `wf_skill`;
CREATE TABLE `wf_skill` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `skill_code` varchar(100) NOT NULL COMMENT 'Skill编码（如 ocr_biz_license、compare_date_diff）',
    `skill_name` varchar(200) NOT NULL COMMENT 'Skill名称',
    `skill_type` enum('ocr','word_extract','excel_extract','pdf_extract','llm_judge','calculate','compare','assemble','api','llm_generate') NOT NULL COMMENT 'Skill类型',
    `input_schema` json COMMENT '输入参数定义（JSON Schema）',
    `output_schema` json COMMENT '输出结构定义（含字段+位置+可信度）',
    `endpoint_config` json COMMENT '调用配置（API地址/函数名/参数模板）',
    `description` text COMMENT 'Skill说明',
    `is_active` tinyint(1) DEFAULT 1 COMMENT '是否启用',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_skill_code` (`skill_code`),
    KEY `idx_skill_type` (`skill_type`),
    KEY `idx_is_active` (`is_active`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='Skill定义';

-- F-02 wf_field_label_mapping（字段标签映射）
DROP TABLE IF EXISTS `wf_field_label_mapping`;
CREATE TABLE `wf_field_label_mapping` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `label_tag` varchar(500) NOT NULL COMMENT '字段标签，如 [ISO9001_企业基础资料_营业执照_企业名称]',
    `field_code` varchar(200) NOT NULL COMMENT '字段编码，如 iso9001.ent_base.biz_lic.name',
    `standard_code` varchar(36) NOT NULL COMMENT '所属标准编码',
    `scope_level` varchar(100) DEFAULT NULL COMMENT '层级路径，如 企业基础资料/营业执照',
    `document_name` varchar(200) DEFAULT NULL COMMENT '所属文档名称',
    `field_name` varchar(100) DEFAULT NULL COMMENT '字段名称',
    `data_type` varchar(50) DEFAULT NULL COMMENT '数据类型',
    `skill_code` varchar(36) DEFAULT NULL COMMENT '提取此字段的Skill编码',
    `description` text COMMENT '说明',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_label_tag` (`label_tag`),
    KEY `idx_field_code` (`field_code`),
    KEY `idx_standard_code` (`standard_code`),
    KEY `idx_skill_code` (`skill_code`),
    CONSTRAINT `fk_flm_standard` FOREIGN KEY (`standard_code`) REFERENCES `cert_iso_standard` (`code`),
    CONSTRAINT `fk_flm_skill` FOREIGN KEY (`skill_code`) REFERENCES `wf_skill` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='字段标签映射';

-- F-03 wf_workflow_definition（工作流定义）
DROP TABLE IF EXISTS `wf_workflow_definition`;
CREATE TABLE `wf_workflow_definition` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `workflow_code` varchar(100) NOT NULL COMMENT '工作流编码',
    `workflow_name` varchar(200) NOT NULL COMMENT '工作流名称',
    `workflow_type` enum('extraction','validation','report') NOT NULL COMMENT '工作流类型',
    `workflow_config` json NOT NULL COMMENT '工作流DAG配置（节点+边+参数）',
    `version` int DEFAULT 1 COMMENT '版本号',
    `is_active` tinyint(1) DEFAULT 1 COMMENT '是否启用',
    `description` text COMMENT '说明',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_workflow_code` (`workflow_code`),
    KEY `idx_workflow_type` (`workflow_type`),
    KEY `idx_is_active` (`is_active`),
    KEY `idx_version` (`version`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='工作流定义';

-- F-04 wf_workflow_execution_log（工作流执行日志）
DROP TABLE IF EXISTS `wf_workflow_execution_log`;
CREATE TABLE `wf_workflow_execution_log` (
    -- 基类字段
    `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `create_by` bigint DEFAULT NULL COMMENT '创建人ID',
    `create_time` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` bigint DEFAULT NULL COMMENT '更新人ID',
    `update_time` datetime DEFAULT NULL COMMENT '更新时间',
    `delete_by` bigint DEFAULT NULL COMMENT '删除人ID',
    `delete_time` datetime DEFAULT NULL COMMENT '删除时间',
    
    -- 业务字段
    `workflow_code` varchar(36) NOT NULL COMMENT '工作流定义编码',
    `workflow_version` int NOT NULL COMMENT '执行时的工作流版本',
    `business_type` enum('audit_task','report_task','file_upload') NOT NULL COMMENT '业务场景类型',
    `business_id` bigint NOT NULL COMMENT '关联的业务ID（审核任务ID/报告任务ID/文件ID）',
    `node_id` varchar(50) NOT NULL COMMENT '节点ID',
    `skill_code` varchar(100) NOT NULL COMMENT '执行的Skill',
    `input_data` json COMMENT '实际输入数据',
    `output_data` json COMMENT '实际输出数据',
    `status` enum('pending','running','success','failed','skipped') NOT NULL COMMENT '执行状态',
    `error_msg` text COMMENT '错误信息',
    `duration_ms` int COMMENT '耗时（毫秒）',
    `started_at` datetime NOT NULL COMMENT '开始时间',
    `completed_at` datetime DEFAULT NULL COMMENT '完成时间',
    
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_workflow_code` (`workflow_code`),
    KEY `idx_business_type` (`business_type`),
    KEY `idx_business_id` (`business_id`),
    KEY `idx_node_id` (`node_id`),
    KEY `idx_status` (`status`),
    KEY `idx_started_at` (`started_at`),
    CONSTRAINT `fk_wlog_workflow` FOREIGN KEY (`workflow_code`) REFERENCES `wf_workflow_definition` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='工作流执行日志';


-- ============================================================================
-- 完成
-- ============================================================================
SET FOREIGN_KEY_CHECKS = 1;

-- 输出统计信息
SELECT '✅ 数据库表创建完成！' AS message;
SELECT COUNT(*) AS table_count FROM information_schema.tables 
WHERE table_schema = DATABASE() AND table_name IN (
    'cert_certification_body', 'cert_iso_standard', 'cert_iso_clause', 'cert_phase_definition',
    'cert_standard_phase_config', 'cert_directory_template', 'cert_file_requirement',
    'cert_extraction_rule', 'cert_extraction_field', 'cert_validation_rule',
    'cert_validation_rule_source', 'cert_report_template', 'cert_clause_extraction_rule',
    'ent_enterprise', 'ent_enterprise_phase', 'ent_enterprise_document', 'ent_enterprise_file',
    'ent_file_version', 'ent_file_pre_check_result', 'ent_file_compliance_check',
    'ent_extraction_result', 'ent_table_extraction_result',
    'audit_task', 'audit_checklist_item', 'audit_nonconformity', 'audit_finding',
    'audit_evidence', 'audit_rectification',
    'rpt_report_task', 'rpt_audit_report', 'rpt_report_section', 'rpt_report_section_source',
    'sys_log', 'sys_config',
    'wf_skill', 'wf_field_label_mapping', 'wf_workflow_definition', 'wf_workflow_execution_log'
);
