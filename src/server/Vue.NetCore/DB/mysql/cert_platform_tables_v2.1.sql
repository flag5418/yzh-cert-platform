-- ============================================================================
-- 体系认证平台 - 数据库建表脚本 V2.1
-- ============================================================================
-- 版本：V2.1
-- 日期：2026-07-30
-- 说明：
--   1. 所有业务表继承 BaseEntity 基类（Id, Code, CreateID, CreateDate, ModifyID, ModifyDate, DeleteID, DeleteTime）
--   2. 表间关联使用 Code（GUID）字段，用户关联除外
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
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `Name` varchar(200) NOT NULL COMMENT '机构全称',
    `ShortName` varchar(100) DEFAULT NULL COMMENT '简称',
    `CbCode` varchar(50) DEFAULT NULL COMMENT 'CNAS认可编号',
    `ContactName` varchar(50) DEFAULT NULL COMMENT '联系人',
    `ContactPhone` varchar(20) DEFAULT NULL COMMENT '联系电话',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    UNIQUE KEY `uk_name` (`Name`),
    UNIQUE KEY `uk_cb_code` (`CbCode`),
    KEY `idx_status` (`Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='认证机构';

-- A-02 cert_iso_standard（ISO 标准）
DROP TABLE IF EXISTS `cert_iso_standard`;
CREATE TABLE `cert_iso_standard` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `CbCode` varchar(36) NOT NULL COMMENT '所属认证机构编码',
    `StandardCode` varchar(50) NOT NULL COMMENT '标准编号（如 ISO 9001:2015）',
    `StandardName` varchar(200) NOT NULL COMMENT '标准中文名称',
    `VersionYear` year NOT NULL COMMENT '版本年份',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_cb_code` (`CbCode`),
    KEY `idx_standard_code` (`StandardCode`),
    CONSTRAINT `fk_iso_standard_cb` FOREIGN KEY (`CbCode`) REFERENCES `cert_certification_body` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='ISO标准';

-- A-03 cert_iso_clause（标准条款）
DROP TABLE IF EXISTS `cert_iso_clause`;
CREATE TABLE `cert_iso_clause` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `StandardCode` varchar(36) NOT NULL COMMENT '所属标准编码',
    `ParentCode` varchar(36) DEFAULT NULL COMMENT '父条款编码（树形结构）',
    `ClauseNumber` varchar(20) NOT NULL COMMENT '条款编号（如 7.1、7.1.1）',
    `Title` varchar(200) NOT NULL COMMENT '条款标题',
    `Description` text COMMENT '条款原文或摘要',
    `SortOrder` int DEFAULT 0 COMMENT '排序',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_standard_code` (`StandardCode`),
    KEY `idx_parent_code` (`ParentCode`),
    KEY `idx_clause_number` (`ClauseNumber`),
    CONSTRAINT `fk_iso_clause_standard` FOREIGN KEY (`StandardCode`) REFERENCES `cert_iso_standard` (`Code`),
    CONSTRAINT `fk_iso_clause_parent` FOREIGN KEY (`ParentCode`) REFERENCES `cert_iso_clause` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='标准条款';

-- A-04 cert_phase_definition（阶段定义）
DROP TABLE IF EXISTS `cert_phase_definition`;
CREATE TABLE `cert_phase_definition` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `PhaseCode` varchar(20) NOT NULL COMMENT '阶段编码（S1/S2/Surv1/Surv2/Recert）',
    `PhaseName` varchar(100) NOT NULL COMMENT '中文名称',
    `SequenceOrder` int NOT NULL COMMENT '顺序（1=S1 2=S2 3=一监 4=二监 5=再认证）',
    `Description` text COMMENT '阶段说明',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    UNIQUE KEY `uk_phase_code` (`PhaseCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='阶段定义';

-- A-05 cert_standard_phase_config（标准-阶段配置）
DROP TABLE IF EXISTS `cert_standard_phase_config`;
CREATE TABLE `cert_standard_phase_config` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `StandardCode` varchar(36) NOT NULL COMMENT '标准编码',
    `PhaseCode` varchar(36) NOT NULL COMMENT '阶段编码',
    `RequiredClauses` json COMMENT '此阶段需检查的条款编码列表',
    `RequiredFiles` json COMMENT '此阶段必需的文件清单编码列表',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    UNIQUE KEY `uk_standard_phase` (`StandardCode`, `PhaseCode`),
    KEY `idx_standard_code` (`StandardCode`),
    KEY `idx_phase_code` (`PhaseCode`),
    CONSTRAINT `fk_spconfig_standard` FOREIGN KEY (`StandardCode`) REFERENCES `cert_iso_standard` (`Code`),
    CONSTRAINT `fk_spconfig_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `cert_phase_definition` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='标准-阶段配置';

-- A-06 cert_directory_template（文件目录模板）
DROP TABLE IF EXISTS `cert_directory_template`;
CREATE TABLE `cert_directory_template` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `ConfigCode` varchar(36) NOT NULL COMMENT '所属标准-阶段配置编码',
    `ParentCode` varchar(36) DEFAULT NULL COMMENT '父文件夹编码（树形结构）',
    `FolderName` varchar(200) NOT NULL COMMENT '文件夹名称',
    `SortOrder` int DEFAULT 0 COMMENT '排序',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_config_code` (`ConfigCode`),
    KEY `idx_parent_code` (`ParentCode`),
    CONSTRAINT `fk_dirtemplate_config` FOREIGN KEY (`ConfigCode`) REFERENCES `cert_standard_phase_config` (`Code`),
    CONSTRAINT `fk_dirtemplate_parent` FOREIGN KEY (`ParentCode`) REFERENCES `cert_directory_template` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文件目录模板';

-- A-07 cert_file_requirement（文件要求）
DROP TABLE IF EXISTS `cert_file_requirement`;
CREATE TABLE `cert_file_requirement` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `FolderCode` varchar(36) NOT NULL COMMENT '所属文件夹编码',
    `FileNameTemplate` varchar(200) NOT NULL COMMENT '文件名称模板',
    `FileType` varchar(50) NOT NULL COMMENT '允许的文件类型（pdf/docx/xlsx/png 等）',
    `IsRequired` tinyint(1) DEFAULT 1 COMMENT '是否必须提供',
    `MaxSizeMB` int DEFAULT 10 COMMENT '最大文件大小（MB）',
    `Description` text COMMENT '文件说明/要求描述',
    `SortOrder` int DEFAULT 0 COMMENT '排序',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_folder_code` (`FolderCode`),
    CONSTRAINT `fk_filereq_folder` FOREIGN KEY (`FolderCode`) REFERENCES `cert_directory_template` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文件要求';

-- A-08 cert_extraction_rule（数据提取规则）
DROP TABLE IF EXISTS `cert_extraction_rule`;
CREATE TABLE `cert_extraction_rule` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `FileRequirementCode` varchar(36) NOT NULL COMMENT '适用文件类型编码',
    `SkillCode` varchar(36) NOT NULL COMMENT '使用的Skill编码',
    `RuleType` enum('Title','table','text','form','mixed') NOT NULL COMMENT '提取规则类型',
    `RuleConfig` json NOT NULL COMMENT '规则配置（参数、提取逻辑）',
    `Description` text COMMENT '规则说明',
    `IsActive` tinyint(1) DEFAULT 1 COMMENT '是否启用',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_filereq_code` (`FileRequirementCode`),
    KEY `idx_skill_code` (`SkillCode`),
    CONSTRAINT `fk_extreq_filereq` FOREIGN KEY (`FileRequirementCode`) REFERENCES `cert_file_requirement` (`Code`),
    CONSTRAINT `fk_extreq_skill` FOREIGN KEY (`SkillCode`) REFERENCES `wf_skill` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='数据提取规则';

-- A-09 cert_extraction_field（提取字段定义）
DROP TABLE IF EXISTS `cert_extraction_field`;
CREATE TABLE `cert_extraction_field` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `RuleCode` varchar(36) NOT NULL COMMENT '所属提取规则编码',
    `SkillCode` varchar(36) DEFAULT NULL COMMENT '提取此字段的Skill（可覆盖规则级Skill）',
    `FieldCode` varchar(100) NOT NULL COMMENT '字段编码（如 iso9001.ent_base.biz_lic.Name）',
    `LabelTag` varchar(500) NOT NULL COMMENT '字段标签（如 [ISO9001_企业基础资料_营业执照_企业名称]）',
    `FieldName` varchar(100) NOT NULL COMMENT '字段显示名称',
    `FieldType` enum('string','number','date','boolean','enum','list') DEFAULT 'string' COMMENT '字段数据类型',
    `EnumValues` json COMMENT '枚举值列表（field_type=enum 时）',
    `SortOrder` int DEFAULT 0 COMMENT '排序',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    UNIQUE KEY `uk_label_tag` (`LabelTag`),
    KEY `idx_rule_code` (`RuleCode`),
    KEY `idx_field_code` (`FieldCode`),
    CONSTRAINT `fk_extfield_rule` FOREIGN KEY (`RuleCode`) REFERENCES `cert_extraction_rule` (`Code`),
    CONSTRAINT `fk_extfield_skill` FOREIGN KEY (`SkillCode`) REFERENCES `wf_skill` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='提取字段定义';

-- A-10 cert_validation_rule（校验规则）
DROP TABLE IF EXISTS `cert_validation_rule`;
CREATE TABLE `cert_validation_rule` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `StandardCode` varchar(36) NOT NULL COMMENT '适用标准编码',
    `PhaseCode` varchar(36) NOT NULL COMMENT '适用阶段编码',
    `ClauseCode` varchar(36) NOT NULL COMMENT '对应条款编码',
    `WorkflowCode` varchar(36) NOT NULL COMMENT '关联的工作流定义编码',
    `RuleCode` varchar(50) NOT NULL COMMENT '规则编码',
    `RuleName` varchar(200) NOT NULL COMMENT '规则名称',
    `SeverityIfViolated` enum('major','minor','observation') NOT NULL COMMENT '触发时的NC严重度',
    `NcDescriptionTemplate` text COMMENT 'NC描述模板',
    `IsActive` tinyint(1) DEFAULT 1 COMMENT '是否启用',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    UNIQUE KEY `uk_rule_code` (`RuleCode`),
    KEY `idx_standard_code` (`StandardCode`),
    KEY `idx_phase_code` (`PhaseCode`),
    KEY `idx_clause_code` (`ClauseCode`),
    KEY `idx_workflow_code` (`WorkflowCode`),
    CONSTRAINT `fk_valrule_standard` FOREIGN KEY (`StandardCode`) REFERENCES `cert_iso_standard` (`Code`),
    CONSTRAINT `fk_valrule_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `cert_phase_definition` (`Code`),
    CONSTRAINT `fk_valrule_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`),
    CONSTRAINT `fk_valrule_workflow` FOREIGN KEY (`WorkflowCode`) REFERENCES `wf_workflow_definition` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='校验规则';

-- A-11 cert_validation_rule_source（校验规则溯源）
DROP TABLE IF EXISTS `cert_validation_rule_source`;
CREATE TABLE `cert_validation_rule_source` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `RuleCode` varchar(36) NOT NULL COMMENT '校验规则编码',
    `FileRequirementCode` varchar(36) NOT NULL COMMENT '溯源文件类型编码',
    `SourcePath` varchar(500) DEFAULT NULL COMMENT '溯源路径（文件内位置描述）',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_rule_code` (`RuleCode`),
    KEY `idx_filereq_code` (`FileRequirementCode`),
    CONSTRAINT `fk_valsource_rule` FOREIGN KEY (`RuleCode`) REFERENCES `cert_validation_rule` (`Code`),
    CONSTRAINT `fk_valsource_filereq` FOREIGN KEY (`FileRequirementCode`) REFERENCES `cert_file_requirement` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='校验规则溯源';

-- A-12 cert_report_template（报告模板）
DROP TABLE IF EXISTS `cert_report_template`;
CREATE TABLE `cert_report_template` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `CbCode` varchar(36) NOT NULL COMMENT '认证机构编码',
    `StandardCode` varchar(36) NOT NULL COMMENT '标准编码',
    `PhaseCode` varchar(36) NOT NULL COMMENT '阶段编码',
    `TemplateName` varchar(200) NOT NULL COMMENT '模板名称',
    `TemplateFilePath` varchar(500) DEFAULT NULL COMMENT '空白文档文件路径（MinIO）',
    `SectionConfig` json COMMENT '报告章节配置（含每章节的 workflow_id、clause_id 映射）',
    `IsDefault` tinyint(1) DEFAULT 0 COMMENT '是否默认模板',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_cb_code` (`CbCode`),
    KEY `idx_standard_code` (`StandardCode`),
    KEY `idx_phase_code` (`PhaseCode`),
    CONSTRAINT `fk_rpttmpl_cb` FOREIGN KEY (`CbCode`) REFERENCES `cert_certification_body` (`Code`),
    CONSTRAINT `fk_rpttmpl_standard` FOREIGN KEY (`StandardCode`) REFERENCES `cert_iso_standard` (`Code`),
    CONSTRAINT `fk_rpttmpl_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `cert_phase_definition` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告模板';

-- A-13 cert_clause_extraction_rule（条款提取规则）
DROP TABLE IF EXISTS `cert_clause_extraction_rule`;
CREATE TABLE `cert_clause_extraction_rule` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `ClauseCode` varchar(36) NOT NULL COMMENT '条款编码',
    `WorkflowCode` varchar(36) NOT NULL COMMENT '关联的提取工作流编码',
    `Description` text COMMENT '规则集说明',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_clause_code` (`ClauseCode`),
    KEY `idx_workflow_code` (`WorkflowCode`),
    CONSTRAINT `fk_clauseext_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`),
    CONSTRAINT `fk_clauseext_workflow` FOREIGN KEY (`WorkflowCode`) REFERENCES `wf_workflow_definition` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='条款提取规则';


-- ============================================================================
-- 域 B：企业档案（9 张表）- 前缀：ent_
-- ============================================================================

-- B-01 ent_enterprise（企业）
DROP TABLE IF EXISTS `ent_enterprise`;
CREATE TABLE `ent_enterprise` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `Name` varchar(200) NOT NULL COMMENT '企业全称',
    `ShortName` varchar(100) DEFAULT NULL COMMENT '简称',
    `CreditCode` varchar(50) DEFAULT NULL COMMENT '统一社会信用代码',
    `LegalPerson` varchar(50) DEFAULT NULL COMMENT '法人代表',
    `Address` text COMMENT '企业地址',
    `CertScope` text COMMENT '认证范围描述',
    `ContactName` varchar(50) DEFAULT NULL COMMENT '对接人姓名',
    `ContactPhone` varchar(20) DEFAULT NULL COMMENT '对接人电话',
    `ContactEmail` varchar(200) DEFAULT NULL COMMENT '对接人邮箱',
    `ArchiveDate` date DEFAULT NULL COMMENT '归档日期',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    UNIQUE KEY `uk_credit_code` (`CreditCode`),
    KEY `idx_name` (`Name`),
    KEY `idx_status` (`Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业';

-- B-02 ent_enterprise_phase（企业阶段）
DROP TABLE IF EXISTS `ent_enterprise_phase`;
CREATE TABLE `ent_enterprise_phase` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `EnterpriseCode` varchar(36) NOT NULL COMMENT '所属企业编码',
    `PhaseCode` varchar(36) NOT NULL COMMENT '阶段定义编码',
    `StandardCode` varchar(36) NOT NULL COMMENT '认证标准编码',
    `StartedAt` datetime DEFAULT NULL COMMENT '开始时间',
    `CompletedAt` datetime DEFAULT NULL COMMENT '完成时间',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    UNIQUE KEY `uk_ent_phase_std` (`EnterpriseCode`, `PhaseCode`, `StandardCode`),
    KEY `idx_enterprise_code` (`EnterpriseCode`),
    KEY `idx_phase_code` (`PhaseCode`),
    KEY `idx_standard_code` (`StandardCode`),
    KEY `idx_status` (`Status`),
    CONSTRAINT `fk_ephase_enterprise` FOREIGN KEY (`EnterpriseCode`) REFERENCES `ent_enterprise` (`Code`),
    CONSTRAINT `fk_ephase_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `cert_phase_definition` (`Code`),
    CONSTRAINT `fk_ephase_standard` FOREIGN KEY (`StandardCode`) REFERENCES `cert_iso_standard` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业阶段';

-- B-03 ent_enterprise_document（企业文档目录）
DROP TABLE IF EXISTS `ent_enterprise_document`;
CREATE TABLE `ent_enterprise_document` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `EnterpriseCode` varchar(36) NOT NULL COMMENT '所属企业编码',
    `PhaseCode` varchar(36) DEFAULT NULL COMMENT '所属阶段编码（scope=phase时必填）',
    `Scope` enum('enterprise_base','phase') NOT NULL COMMENT '资料层级：共享层 / 隔离层',
    `TemplateFolderCode` varchar(36) DEFAULT NULL COMMENT '对应的模板文件夹编码',
    `ParentCode` varchar(36) DEFAULT NULL COMMENT '父文件夹编码（树形结构）',
    `FolderName` varchar(200) NOT NULL COMMENT '文件夹名称',
    `SortOrder` int DEFAULT 0 COMMENT '排序',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_enterprise_code` (`EnterpriseCode`),
    KEY `idx_phase_code` (`PhaseCode`),
    KEY `idx_parent_code` (`ParentCode`),
    KEY `idx_scope` (`Scope`),
    CONSTRAINT `fk_edoc_enterprise` FOREIGN KEY (`EnterpriseCode`) REFERENCES `ent_enterprise` (`Code`),
    CONSTRAINT `fk_edoc_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `ent_enterprise_phase` (`Code`),
    CONSTRAINT `fk_edoc_template` FOREIGN KEY (`TemplateFolderCode`) REFERENCES `cert_directory_template` (`Code`),
    CONSTRAINT `fk_edoc_parent` FOREIGN KEY (`ParentCode`) REFERENCES `ent_enterprise_document` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业文档目录';

-- B-04 ent_enterprise_file（企业文件）
DROP TABLE IF EXISTS `ent_enterprise_file`;
CREATE TABLE `ent_enterprise_file` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `FolderCode` varchar(36) NOT NULL COMMENT '所属文件夹编码',
    `FileName` varchar(500) NOT NULL COMMENT '文件名',
    `FileType` varchar(50) NOT NULL COMMENT '文件类型（pdf/docx/xlsx/png/jpg）',
    `FileSize` bigint NOT NULL COMMENT '文件大小（bytes）',
    `StoragePath` varchar(500) NOT NULL COMMENT 'MinIO存储路径',
    `FileHash` varchar(64) DEFAULT NULL COMMENT '文件SHA256哈希（增量审核依据）',
    `CurrentVersion` int DEFAULT 1 COMMENT '当前版本号',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_folder_code` (`FolderCode`),
    KEY `idx_file_hash` (`FileHash`),
    CONSTRAINT `fk_efile_folder` FOREIGN KEY (`FolderCode`) REFERENCES `ent_enterprise_document` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业文件';

-- B-05 ent_file_version（文件版本）
DROP TABLE IF EXISTS `ent_file_version`;
CREATE TABLE `ent_file_version` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `FileCode` varchar(36) NOT NULL COMMENT '源文件编码',
    `VersionNumber` int NOT NULL COMMENT '版本号（从1开始递增）',
    `FileSize` bigint NOT NULL COMMENT '版本文件大小',
    `StoragePath` varchar(500) NOT NULL COMMENT 'MinIO存储路径',
    `FileHash` varchar(64) NOT NULL COMMENT 'SHA256哈希',
    `ChangeNotes` text COMMENT '变更说明',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    UNIQUE KEY `uk_file_version` (`FileCode`, `VersionNumber`),
    KEY `idx_file_code` (`FileCode`),
    CONSTRAINT `fk_fver_file` FOREIGN KEY (`FileCode`) REFERENCES `ent_enterprise_file` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文件版本';

-- B-06 ent_file_pre_check_result（资料质量预审结果）
DROP TABLE IF EXISTS `ent_file_pre_check_result`;
CREATE TABLE `ent_file_pre_check_result` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `FileCode` varchar(36) NOT NULL COMMENT '被检查的文件编码',
    `VersionNumber` int NOT NULL COMMENT '检查的文件版本',
    `CheckType` enum('readability','clarity','format','completeness') NOT NULL COMMENT '检查类型',
    `CheckResult` enum('pass','warning','block') NOT NULL COMMENT '检查结果',
    `Message` text COMMENT '检查信息',
    `Detail` json COMMENT '详细信息（DPI值、倾斜角度、缺页数等）',
    `CheckedAt` datetime NOT NULL COMMENT '检查时间',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_file_code` (`FileCode`),
    KEY `idx_check_type` (`CheckType`),
    KEY `idx_check_result` (`CheckResult`),
    CONSTRAINT `fk_precheck_file` FOREIGN KEY (`FileCode`) REFERENCES `ent_enterprise_file` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='资料质量预审结果';

-- B-07 ent_file_compliance_check（文件合规检查）
DROP TABLE IF EXISTS `ent_file_compliance_check`;
CREATE TABLE `ent_file_compliance_check` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `FileCode` varchar(36) NOT NULL COMMENT '被检查的文件编码',
    `VersionNumber` int NOT NULL COMMENT '检查的文件版本',
    `RuleCode` varchar(36) NOT NULL COMMENT '触发的校验规则编码',
    `WorkflowExecutionCode` varchar(36) DEFAULT NULL COMMENT '工作流执行记录编码',
    `CheckStatus` enum('pass','fail','warning','blocked') NOT NULL COMMENT '检查结果',
    `Message` text COMMENT '检查信息',
    `Detail` json COMMENT '详细信息（含具体位置、偏离描述）',
    `CheckedAt` datetime NOT NULL COMMENT '检查时间',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_file_code` (`FileCode`),
    KEY `idx_rule_code` (`RuleCode`),
    KEY `idx_check_status` (`CheckStatus`),
    CONSTRAINT `fk_compliance_file` FOREIGN KEY (`FileCode`) REFERENCES `ent_enterprise_file` (`Code`),
    CONSTRAINT `fk_compliance_rule` FOREIGN KEY (`RuleCode`) REFERENCES `cert_validation_rule` (`Code`),
    CONSTRAINT `fk_compliance_wexec` FOREIGN KEY (`WorkflowExecutionCode`) REFERENCES `wf_workflow_execution_log` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文件合规检查';

-- B-08 ent_extraction_result（文档提取结果）
DROP TABLE IF EXISTS `ent_extraction_result`;
CREATE TABLE `ent_extraction_result` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `FileCode` varchar(36) NOT NULL COMMENT '提取的源文件编码',
    `VersionNumber` int NOT NULL COMMENT '提取的文件版本',
    `RuleCode` varchar(36) NOT NULL COMMENT '使用的提取规则编码',
    `FieldCode` varchar(36) NOT NULL COMMENT '对应的提取字段编码',
    `LabelTag` varchar(500) DEFAULT NULL COMMENT '字段标签冗余（便于查询）',
    `ExtractedValue` text COMMENT '提取的值',
    `Confidence` decimal(3,2) DEFAULT NULL COMMENT 'AI提取可信度 (0.00-1.00)',
    `PositionInfo` json DEFAULT NULL COMMENT '位置信息（页码/行号/列号/单元格）',
    `IsManualEdited` tinyint(1) DEFAULT 0 COMMENT '是否被人工修改',
    `ExtractedAt` datetime NOT NULL COMMENT '提取时间',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_file_code` (`FileCode`),
    KEY `idx_rule_code` (`RuleCode`),
    KEY `idx_field_code` (`FieldCode`),
    KEY `idx_label_tag` (`LabelTag`),
    CONSTRAINT `k_extres_file` FOREIGN KEY (`FileCode`) REFERENCES `ent_enterprise_file` (`Code`),
    CONSTRAINT `fk_extres_rule` FOREIGN KEY (`RuleCode`) REFERENCES `cert_extraction_rule` (`Code`),
    CONSTRAINT `fk_extres_field` FOREIGN KEY (`FieldCode`) REFERENCES `cert_extraction_field` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文档提取结果';

-- B-09 ent_table_extraction_result（表格提取结果）
DROP TABLE IF EXISTS `ent_table_extraction_result`;
CREATE TABLE `ent_table_extraction_result` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `FileCode` varchar(36) NOT NULL COMMENT '提取的源文件编码',
    `VersionNumber` int NOT NULL COMMENT '提取的文件版本',
    `RuleCode` varchar(36) NOT NULL COMMENT '使用的提取规则编码',
    `TableIndex` int DEFAULT 1 COMMENT '文档中第几个表格',
    `ExtractedJson` json NOT NULL COMMENT '表格内容（JSON）',
    `Confidence` decimal(3,2) DEFAULT NULL COMMENT 'AI提取可信度',
    `PositionInfo` json DEFAULT NULL COMMENT '表格在文档中的位置信息',
    `ExtractedAt` datetime NOT NULL COMMENT '提取时间',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_file_code` (`FileCode`),
    KEY `idx_rule_code` (`RuleCode`),
    CONSTRAINT `fk_tableext_file` FOREIGN KEY (`FileCode`) REFERENCES `ent_enterprise_file` (`Code`),
    CONSTRAINT `fk_tableext_rule` FOREIGN KEY (`RuleCode`) REFERENCES `cert_extraction_rule` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='表格提取结果';


-- ============================================================================
-- 域 C：审核执行（6 张表）- 前缀：audit_
-- ============================================================================

-- C-01 audit_task（审核任务）
DROP TABLE IF EXISTS `audit_task`;
CREATE TABLE `audit_task` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `PhaseCode` varchar(36) NOT NULL COMMENT '所属企业阶段编码',
    `TaskNumber` varchar(50) NOT NULL COMMENT '任务编号',
    `AuditorId` bigint NOT NULL COMMENT '审核员ID（关联Sys_User.Id）',
    `PlannedDate` date DEFAULT NULL COMMENT '计划审核日期',
    `ActualStartDate` date DEFAULT NULL COMMENT '实际开始日期',
    `ActualCompleteDate` date DEFAULT NULL COMMENT '实际完成日期',
    `AuditScope` text COMMENT '审核范围描述',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    UNIQUE KEY `uk_task_number` (`TaskNumber`),
    KEY `idx_phase_code` (`PhaseCode`),
    KEY `idx_auditor_id` (`AuditorId`),
    KEY `idx_status` (`Status`),
    CONSTRAINT `fk_task_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `ent_enterprise_phase` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='审核任务';

-- C-02 audit_checklist_item（检查表条目）
DROP TABLE IF EXISTS `audit_checklist_item`;
CREATE TABLE `audit_checklist_item` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `TaskCode` varchar(36) NOT NULL COMMENT '所属审核任务编码',
    `ClauseCode` varchar(36) NOT NULL COMMENT '对应条款编码',
    `AuditCriteria` text COMMENT '审核准则（标准条款原文）',
    `FindingDescription` text COMMENT '审核发现描述',
    `Conformity` enum('pending','conform','nonconform','observation','na') DEFAULT 'pending' COMMENT '判定结果',
    `NcsFound` int DEFAULT 0 COMMENT '发现NC数量',
    `CheckedBy` bigint DEFAULT NULL COMMENT '检查人ID',
    `CheckedAt` datetime DEFAULT NULL COMMENT '检查时间',
    `SortOrder` int DEFAULT 0 COMMENT '排序',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_task_code` (`TaskCode`),
    KEY `idx_clause_code` (`ClauseCode`),
    KEY `idx_conformity` (`Conformity`),
    CONSTRAINT `fk_checklist_task` FOREIGN KEY (`TaskCode`) REFERENCES `audit_task` (`Code`),
    CONSTRAINT `fk_checklist_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='检查表条目';

-- C-03 audit_nonconformity（不符合项 / NC）
DROP TABLE IF EXISTS `audit_nonconformity`;
CREATE TABLE `audit_nonconformity` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `TaskCode` varchar(36) NOT NULL COMMENT '所属审核任务编码',
    `ClauseCode` varchar(36) NOT NULL COMMENT '对应条款编码',
    `NcNumber` varchar(50) NOT NULL COMMENT 'NC编号',
    `Severity` enum('major','minor','observation') NOT NULL COMMENT '严重度',
    `Description` text NOT NULL COMMENT 'NC描述（不符合事实）',
    `RequirementRef` text COMMENT '违反的标准要求原文',
    `EvidenceRef` text COMMENT '客观证据引用',
    `SourceType` enum('auto_rule','manual') DEFAULT 'manual' COMMENT 'NC来源：规则自动触发 / 手动创建',
    `SourceCheckCode` varchar(36) DEFAULT NULL COMMENT '触发的合规检查记录编码',
    `RuleCode` varchar(36) DEFAULT NULL COMMENT '触发的校验规则编码',
    `DueDate` date DEFAULT NULL COMMENT '整改截止日期',
    `OpenedBy` bigint NOT NULL COMMENT '开具人ID',
    `OpenedAt` datetime NOT NULL COMMENT '开具时间',
    `ClosedAt` datetime DEFAULT NULL COMMENT '关闭时间',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    UNIQUE KEY `uk_nc_number` (`NcNumber`),
    KEY `idx_task_code` (`TaskCode`),
    KEY `idx_clause_code` (`ClauseCode`),
    KEY `idx_severity` (`Severity`),
    KEY `idx_status` (`Status`),
    KEY `idx_source_type` (`SourceType`),
    CONSTRAINT `fk_nc_task` FOREIGN KEY (`TaskCode`) REFERENCES `audit_task` (`Code`),
    CONSTRAINT `fk_nc_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`),
    CONSTRAINT `fk_nc_sourcecheck` FOREIGN KEY (`SourceCheckCode`) REFERENCES `ent_file_compliance_check` (`Code`),
    CONSTRAINT `fk_nc_rule` FOREIGN KEY (`RuleCode`) REFERENCES `cert_validation_rule` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='不符合项(NC)';

-- C-04 audit_finding（审核发现明细）
DROP TABLE IF EXISTS `audit_finding`;
CREATE TABLE `audit_finding` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `ChecklistItemCode` varchar(36) NOT NULL COMMENT '检查表条目编码',
    `NcCode` varchar(36) DEFAULT NULL COMMENT '关联NC编码',
    `SourceFileCode` varchar(36) DEFAULT NULL COMMENT '来源文件编码',
    `SourcePosition` varchar(200) DEFAULT NULL COMMENT '来源位置（页码/行号/列号）',
    `SourceContent` text COMMENT '来源内容摘录',
    `FindingType` enum('conform','discrepancy','comment') NOT NULL COMMENT '发现类型',
    `Description` text NOT NULL COMMENT '描述',
    `Confidence` decimal(3,2) DEFAULT NULL COMMENT 'AI提取可信度 (0.00-1.00)',
    `IsManual` tinyint(1) DEFAULT 0 COMMENT '是否人工添加',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_checklist_item_code` (`ChecklistItemCode`),
    KEY `idx_nc_code` (`NcCode`),
    KEY `idx_source_file_code` (`SourceFileCode`),
    KEY `idx_finding_type` (`FindingType`),
    CONSTRAINT `fk_finding_checklist` FOREIGN KEY (`ChecklistItemCode`) REFERENCES `audit_checklist_item` (`Code`),
    CONSTRAINT `fk_finding_nc` FOREIGN KEY (`NcCode`) REFERENCES `audit_nonconformity` (`Code`),
    CONSTRAINT `fk_finding_file` FOREIGN KEY (`SourceFileCode`) REFERENCES `ent_enterprise_file` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='审核发现明细';

-- C-05 audit_evidence（审核证据）
DROP TABLE IF EXISTS `audit_evidence`;
CREATE TABLE `audit_evidence` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `TaskCode` varchar(36) NOT NULL COMMENT '所属审核任务编码',
    `ClauseCode` varchar(36) DEFAULT NULL COMMENT '关联条款编码',
    `EvidenceType` enum('photo','audio','screenshot','video','document','other') NOT NULL COMMENT '证据类型',
    `StoragePath` varchar(500) NOT NULL COMMENT 'MinIO存储路径',
    `FileHash` varchar(64) NOT NULL COMMENT 'SHA256哈希',
    `IsVoided` tinyint(1) DEFAULT 0 COMMENT '是否废弃',
    `VoidedAt` datetime DEFAULT NULL COMMENT '废弃时间',
    `VoidedBy` bigint DEFAULT NULL COMMENT '废弃操作人ID',
    `CapturedAt` datetime DEFAULT NULL COMMENT '采集时间',
    `CapturedBy` bigint NOT NULL COMMENT '采集人ID',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_task_code` (`TaskCode`),
    KEY `idx_clause_code` (`ClauseCode`),
    KEY `idx_evidence_type` (`EvidenceType`),
    KEY `idx_is_voided` (`IsVoided`),
    CONSTRAINT `fk_evidence_task` FOREIGN KEY (`TaskCode`) REFERENCES `audit_task` (`Code`),
    CONSTRAINT `fk_evidence_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='审核证据';

-- C-06 audit_rectification（整改记录）
DROP TABLE IF EXISTS `audit_rectification`;
CREATE TABLE `audit_rectification` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `NcCode` varchar(36) NOT NULL COMMENT '关联NC编码',
    `Correction` text NOT NULL COMMENT '纠正措施描述',
    `CorrectiveAction` text COMMENT '纠正措施（根因分析+防再发生）',
    `EvidenceFiles` json COMMENT '整改证据文件路径列表',
    `SubmittedBy` bigint NOT NULL COMMENT '提交人ID',
    `SubmittedAt` datetime NOT NULL COMMENT '提交时间',
    `VerifiedBy` bigint DEFAULT NULL COMMENT '复核人ID',
    `VerifiedAt` datetime DEFAULT NULL COMMENT '复核时间',
    `VerifyResult` enum('approved','rejected') DEFAULT NULL COMMENT '复核结果',
    `VerifyNotes` text DEFAULT NULL COMMENT '复核意见',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_nc_code` (`NcCode`),
    KEY `idx_verify_result` (`VerifyResult`),
    CONSTRAINT `fk_rect_nc` FOREIGN KEY (`NcCode`) REFERENCES `audit_nonconformity` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='整改记录';


-- ============================================================================
-- 域 D：报告生成（4 张表）- 前缀：rpt_
-- ============================================================================

-- D-01 rpt_report_task（报告任务）
DROP TABLE IF EXISTS `rpt_report_task`;
CREATE TABLE `rpt_report_task` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `PhaseCode` varchar(36) NOT NULL COMMENT '所属企业阶段编码',
    `BasedOnAuditTaskCode` varchar(36) DEFAULT NULL COMMENT '基于的审核任务编码',
    `TemplateCode` varchar(36) NOT NULL COMMENT '使用的报告模板编码',
    `TaskNumber` varchar(50) NOT NULL COMMENT '任务编号',
    `GeneratedAt` datetime DEFAULT NULL COMMENT '生成时间',
    `LockedAt` datetime DEFAULT NULL COMMENT '锁定时间',
    `LockedBy` bigint DEFAULT NULL COMMENT '锁定人ID',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    UNIQUE KEY `uk_task_number` (`TaskNumber`),
    KEY `idx_phase_code` (`PhaseCode`),
    KEY `idx_audit_task_code` (`BasedOnAuditTaskCode`),
    KEY `idx_template_code` (`TemplateCode`),
    KEY `idx_status` (`Status`),
    CONSTRAINT `fk_rpttask_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `ent_enterprise_phase` (`Code`),
    CONSTRAINT `fk_rpttask_audit` FOREIGN KEY (`BasedOnAuditTaskCode`) REFERENCES `audit_task` (`Code`),
    CONSTRAINT `fk_rpttask_template` FOREIGN KEY (`TemplateCode`) REFERENCES `cert_report_template` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告任务';

-- D-02 rpt_audit_report（报告正文）
DROP TABLE IF EXISTS `rpt_audit_report`;
CREATE TABLE `rpt_audit_report` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `TaskCode` varchar(36) NOT NULL COMMENT '所属报告任务编码',
    `VersionNumber` int DEFAULT 1 COMMENT '报告版本号',
    `ReportTitle` varchar(500) NOT NULL COMMENT '报告标题',
    `FullContent` mediumtext COMMENT '报告完整内容（Markdown/HTML）',
    `ExportPath` varchar(500) DEFAULT NULL COMMENT '导出的PDF/Word文件路径',
    `EditedBy` bigint DEFAULT NULL COMMENT '最后编辑人ID',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_task_code` (`TaskCode`),
    CONSTRAINT `fk_report_task` FOREIGN KEY (`TaskCode`) REFERENCES `rpt_report_task` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告正文';

-- D-03 rpt_report_section（报告章节内容）
DROP TABLE IF EXISTS `rpt_report_section`;
CREATE TABLE `rpt_report_section` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `ReportCode` varchar(36) NOT NULL COMMENT '所属报告编码',
    `ClauseCode` varchar(36) DEFAULT NULL COMMENT '对应条款编码（可空，概述/结论章节不映射条款）',
    `SectionName` varchar(200) NOT NULL COMMENT '章节名称',
    `SectionContent` text COMMENT '章节填充内容',
    `WorkflowCode` varchar(36) DEFAULT NULL COMMENT '生成此章节的工作流编码',
    `SortOrder` int DEFAULT 0 COMMENT '章节排序',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_report_code` (`ReportCode`),
    KEY `idx_clause_code` (`ClauseCode`),
    KEY `idx_workflow_code` (`WorkflowCode`),
    CONSTRAINT `fk_section_report` FOREIGN KEY (`ReportCode`) REFERENCES `rpt_audit_report` (`Code`),
    CONSTRAINT `fk_section_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`),
    CONSTRAINT `fk_section_workflow` FOREIGN KEY (`WorkflowCode`) REFERENCES `wf_workflow_definition` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告章节内容';

-- D-04 rpt_report_section_source（报告内容溯源）
DROP TABLE IF EXISTS `rpt_report_section_source`;
CREATE TABLE `rpt_report_section_source` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `SectionCode` varchar(36) NOT NULL COMMENT '所属报告章节编码',
    `SourceType` enum('extraction','finding','nc','manual','template','compliance') NOT NULL COMMENT '来源类型',
    `SourceCode` varchar(36) DEFAULT NULL COMMENT '来源记录的编码（根据source_type指向不同表）',
    `SourceDescription` text COMMENT '来源描述',
    `Confidence` decimal(3,2) DEFAULT NULL COMMENT '可信度',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_section_code` (`SectionCode`),
    KEY `idx_source_type` (`SourceType`),
    CONSTRAINT `fk_src_section` FOREIGN KEY (`SectionCode`) REFERENCES `rpt_report_section` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告内容溯源';


-- ============================================================================
-- 域 E：系统基础（补充 2 张表，Vol 内置 5 张保持不变）- 前缀：sys_
-- ============================================================================

-- E-06 sys_log（系统日志）
DROP TABLE IF EXISTS `sys_log`;
CREATE TABLE `sys_log` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `UserId` bigint DEFAULT NULL COMMENT '操作用户ID',
    `Module` varchar(50) NOT NULL COMMENT '操作模块',
    `Action` varchar(100) NOT NULL COMMENT '操作动作',
    `TargetType` varchar(50) DEFAULT NULL COMMENT '操作对象类型（表名）',
    `TargetId` bigint DEFAULT NULL COMMENT '操作对象ID',
    `Detail` json COMMENT '操作详情（变更前后值等）',
    `IpAddress` varchar(50) DEFAULT NULL COMMENT '操作IP',
    `UserAgent` varchar(500) DEFAULT NULL COMMENT '用户代理',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_user_id` (`UserId`),
    KEY `idx_module` (`Module`),
    KEY `idx_action` (`Action`),
    KEY `idx_create_time` (`CreateDate`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='系统日志';

-- E-07 sys_config（系统参数）
DROP TABLE IF EXISTS `sys_config`;
CREATE TABLE `sys_config` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `ConfigKey` varchar(100) NOT NULL COMMENT '参数键',
    `ConfigValue` text NOT NULL COMMENT '参数值',
    `ValueType` enum('string','number','boolean','json') DEFAULT 'string' COMMENT '值类型',
    `Description` text COMMENT '参数说明',
    `IsSystem` tinyint(1) DEFAULT 0 COMMENT '是否系统级（不可删除）',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    UNIQUE KEY `uk_config_key` (`ConfigKey`),
    KEY `idx_value_type` (`ValueType`),
    KEY `idx_is_system` (`IsSystem`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='系统参数';


-- ============================================================================
-- 域 F：工作流框架（4 张表）- 前缀：wf_
-- ============================================================================

-- F-01 wf_skill（Skill 定义）
DROP TABLE IF EXISTS `wf_skill`;
CREATE TABLE `wf_skill` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `SkillCode` varchar(100) NOT NULL COMMENT 'Skill编码（如 ocr_biz_license、compare_date_diff）',
    `SkillName` varchar(200) NOT NULL COMMENT 'Skill名称',
    `SkillType` enum('ocr','word_extract','excel_extract','pdf_extract','llm_judge','calculate','compare','assemble','api','llm_generate') NOT NULL COMMENT 'Skill类型',
    `InputSchema` json COMMENT '输入参数定义（JSON Schema）',
    `OutputSchema` json COMMENT '输出结构定义（含字段+位置+可信度）',
    `EndpointConfig` json COMMENT '调用配置（API地址/函数名/参数模板）',
    `Description` text COMMENT 'Skill说明',
    `IsActive` tinyint(1) DEFAULT 1 COMMENT '是否启用',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    UNIQUE KEY `uk_skill_code` (`SkillCode`),
    KEY `idx_skill_type` (`SkillType`),
    KEY `idx_is_active` (`IsActive`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='Skill定义';

-- F-02 wf_field_label_mapping（字段标签映射）
DROP TABLE IF EXISTS `wf_field_label_mapping`;
CREATE TABLE `wf_field_label_mapping` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `LabelTag` varchar(500) NOT NULL COMMENT '字段标签，如 [ISO9001_企业基础资料_营业执照_企业名称]',
    `FieldCode` varchar(200) NOT NULL COMMENT '字段编码，如 iso9001.ent_base.biz_lic.Name',
    `StandardCode` varchar(36) NOT NULL COMMENT '所属标准编码',
    `ScopeLevel` varchar(100) DEFAULT NULL COMMENT '层级路径，如 企业基础资料/营业执照',
    `DocumentName` varchar(200) DEFAULT NULL COMMENT '所属文档名称',
    `FieldName` varchar(100) DEFAULT NULL COMMENT '字段名称',
    `DataType` varchar(50) DEFAULT NULL COMMENT '数据类型',
    `SkillCode` varchar(36) DEFAULT NULL COMMENT '提取此字段的Skill编码',
    `Description` text COMMENT '说明',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    UNIQUE KEY `uk_label_tag` (`LabelTag`),
    KEY `idx_field_code` (`FieldCode`),
    KEY `idx_standard_code` (`StandardCode`),
    KEY `idx_skill_code` (`SkillCode`),
    CONSTRAINT `fk_flm_standard` FOREIGN KEY (`StandardCode`) REFERENCES `cert_iso_standard` (`Code`),
    CONSTRAINT `fk_flm_skill` FOREIGN KEY (`SkillCode`) REFERENCES `wf_skill` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='字段标签映射';

-- F-03 wf_workflow_definition（工作流定义）
DROP TABLE IF EXISTS `wf_workflow_definition`;
CREATE TABLE `wf_workflow_definition` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `WorkflowCode` varchar(100) NOT NULL COMMENT '工作流编码',
    `WorkflowName` varchar(200) NOT NULL COMMENT '工作流名称',
    `WorkflowType` enum('extraction','validation','report') NOT NULL COMMENT '工作流类型',
    `WorkflowConfig` json NOT NULL COMMENT '工作流DAG配置（节点+边+参数）',
    `Version` int DEFAULT 1 COMMENT '版本号',
    `IsActive` tinyint(1) DEFAULT 1 COMMENT '是否启用',
    `Description` text COMMENT '说明',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    UNIQUE KEY `uk_workflow_code` (`WorkflowCode`),
    KEY `idx_workflow_type` (`WorkflowType`),
    KEY `idx_is_active` (`IsActive`),
    KEY `idx_version` (`Version`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='工作流定义';

-- F-04 wf_workflow_execution_log（工作流执行日志）
DROP TABLE IF EXISTS `wf_workflow_execution_log`;
CREATE TABLE `wf_workflow_execution_log` (
    -- 基类字段
    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',

    -- 业务字段
    `WorkflowCode` varchar(36) NOT NULL COMMENT '工作流定义编码',
    `Workflowversion` int NOT NULL COMMENT '执行时的工作流版本',
    `BusinessType` enum('audit_task','report_task','file_upload') NOT NULL COMMENT '业务场景类型',
    `BusinessId` bigint NOT NULL COMMENT '关联的业务ID（审核任务ID/报告任务ID/文件ID）',
    `NodeId` varchar(50) NOT NULL COMMENT '节点ID',
    `SkillCode` varchar(100) NOT NULL COMMENT '执行的Skill',
    `InputData` json COMMENT '实际输入数据',
    `OutputData` json COMMENT '实际输出数据',
    `ErrorMsg` text COMMENT '错误信息',
    `DurationMs` int COMMENT '耗时（毫秒）',
    `StartedAt` datetime NOT NULL COMMENT '开始时间',
    `CompletedAt` datetime DEFAULT NULL COMMENT '完成时间',
    
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_workflow_code` (`WorkflowCode`),
    KEY `idx_business_type` (`BusinessType`),
    KEY `idx_business_id` (`BusinessId`),
    KEY `idx_node_id` (`NodeId`),
    KEY `idx_status` (`Status`),
    KEY `idx_started_at` (`StartedAt`),
    CONSTRAINT `fk_wlog_workflow` FOREIGN KEY (`WorkflowCode`) REFERENCES `wf_workflow_definition` (`Code`)
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
