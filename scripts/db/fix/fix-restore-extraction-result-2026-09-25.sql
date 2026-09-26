-- =============================================================================
-- 恢复被连带删除的「文档提取结果」两张表（2026-09-25）
-- -----------------------------------------------------------------------------
-- 【背景】
--   fix-enterprise-v6-cleanup-2026-09-25.sql 在清理 ent_* 家族时，连带 DROP 了
--   ent_extraction_result / ent_table_extraction_result。
--   但这两张表并非「未开发阶段」的表，它们被**已开发完成**的管理端文档提取功能读写：
--     · CertPlatform.Admin/Services/DocExtraction/DocExtractionRuleService.cs
--         （SaveExtractionRuleAsync / GetRuleDetailAsync / DeleteRuleAsync）
--     · CertPlatform.Admin/Services/Workflow/NodeExecutor.cs（docField / docTable 节点）
--     · CertPlatform.Admin/Services/Workflow/Skills/GetFieldSkill.cs
--     · CertPlatform.Admin/Services/Workflow/Skills/GetTableSkill.cs
--     · DocExtractionRuleController（15 个端点，路由 /api/Workflow/DocExtractionRule）
--   → 删除后这些端点会在运行时抛 "Table doesn't exist"。
--
-- 【命名】按数据库命名约定：ent_ 前缀作废（含义不明确），统一以 cert 前缀区分体系认证系统。
--   ent_extraction_result       → cert_extraction_result
--   ent_table_extraction_result → cert_table_extraction_result
--
-- 【列漂移修正】以下脚本被 rebuild_db.sh 的旧快照回冲掉，导致 DB 缺列、实体插入必然失败
--   （ERROR 1054 Unknown column），本次一并补齐：
--     · DB/mysql/phase9_label_tag_to_field_name.sql        label_tag → field_name
--     · scripts/db/20260921_snake_to_pascal_final_V2.sql    field_name → FieldName
--     · DB/mysql/phase10_wf_skill_upgrade.sql               + table_code
--   → 铁律七：DB 列名 == C# 属性名（PascalCase），实体有属性 DB 必须有列。
--
-- 【不重建外键】原表的 5 个外键全部指向**已不存在**的表（悬挂外键，0 影响，因为表为 0 行）：
--   fk_extres_field  → cert_extraction_field   （已改名 cert_doc_field_def）
--   fk_extres_rule   → cert_extraction_rule    （已改名 cert_doc_extraction_rule）
--   k_extres_file    → ent_enterprise_file     （整表已删）
--   fk_tableext_file → ent_enterprise_file     （整表已删）
--   fk_tableext_rule → cert_extraction_rule    （已改名 cert_doc_extraction_rule）
--   规则/字段定义表已稳定（cert_doc_*），文件表（上传功能）尚未定型 →
--   暂以普通索引替代外键，待「上传/文件表」设计定稿时再统一补 FK。
--
-- 【铁律】铁律七（列名 PascalCase 三处一致）/ 铁律八（显式 COLLATE utf8mb4_general_ci）
--         铁律九（保留 IsValid，禁止 Enable）/ 铁律十（SQL 外置，不在 .sh 内嵌）
--
-- 幂等：DROP + CREATE，可重复执行。
-- =============================================================================

USE `yzh_cert_platform`;
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- 1. 文档字段提取结果
-- -----------------------------------------------------------------------------
DROP TABLE IF EXISTS `cert_extraction_result`;
CREATE TABLE `cert_extraction_result` (
  `Id`              bigint       NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`            varchar(36)  COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode`         varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码（标准样例企业 YZH-STD-ENT 等）',
  `CreateBy`        varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime`      datetime     DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy`        varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime`      datetime     DEFAULT NULL,
  `DeleteBy`        varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime`      datetime     DEFAULT NULL COMMENT '删除时间',
  `Status`          varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务状态',
  `Sort`            int          DEFAULT '0' COMMENT '排序号',
  `Remark`          varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `FileCode`        varchar(36)  COLLATE utf8mb4_general_ci NOT NULL COMMENT '提取的源文件编码',
  `StandardCode`    varchar(36)  COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '标准编码（冗余，关联 cert_iso_standard.Code）',
  `StandardFileCode` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '标准文件编码（规则键）',
  `PhaseCode`       varchar(36)  COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '阶段编码（冗余，便于过滤）',
  `VersionNumber`   int          NOT NULL COMMENT '提取的文件版本',
  `RuleCode`        varchar(36)  COLLATE utf8mb4_general_ci NOT NULL COMMENT '使用的提取规则编码',
  `FieldCode`       varchar(36)  COLLATE utf8mb4_general_ci NOT NULL COMMENT '对应的提取字段编码',
  `FieldName`       varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '字段名称（中文名，展示用）',
  `LabelTag`        varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '字段标签冗余（与 FieldCode 同值，兼容旧数据）',
  `ExtractedValue`  text         COLLATE utf8mb4_general_ci COMMENT '提取的值',
  `Confidence`      decimal(3,2) DEFAULT NULL COMMENT 'AI提取可信度 (0.00-1.00)',
  `PositionInfo`    json         DEFAULT NULL COMMENT '位置信息（页码/行号/列号/单元格）',
  `IsManualEdited`  tinyint(1)   DEFAULT '0' COMMENT '是否被人工修改',
  `ExtractedAt`     datetime     NOT NULL COMMENT '提取时间',
  `IsDeleted`       tinyint(1)   NOT NULL DEFAULT '0' COMMENT '软删除标记（false=正常，true=已删除）',
  `IsValid`         int          NOT NULL DEFAULT '1' COMMENT '有效标志（1=有效，0=无效）',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`        (`Code`),
  KEY        `idx_file_code`  (`FileCode`),
  KEY        `idx_rule_code`  (`RuleCode`),
  KEY        `idx_field_code` (`FieldCode`),
  KEY        `idx_label_tag`  (`LabelTag`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文档字段提取结果';

-- -----------------------------------------------------------------------------
-- 2. 表格提取结果
-- -----------------------------------------------------------------------------
DROP TABLE IF EXISTS `cert_table_extraction_result`;
CREATE TABLE `cert_table_extraction_result` (
  `Id`              bigint       NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`            varchar(36)  COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode`         varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码（标准样例企业 YZH-STD-ENT 等）',
  `CreateBy`        varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime`      datetime     DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy`        varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime`      datetime     DEFAULT NULL,
  `DeleteBy`        varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime`      datetime     DEFAULT NULL COMMENT '删除时间',
  `Status`          varchar(50)  COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务状态',
  `Sort`            int          DEFAULT '0' COMMENT '排序号',
  `Remark`          varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `FileCode`        varchar(36)  COLLATE utf8mb4_general_ci NOT NULL COMMENT '提取的源文件编码',
  `StandardCode`    varchar(36)  COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '标准编码（冗余，关联 cert_iso_standard.Code）',
  `StandardFileCode` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '标准文件编码（规则键）',
  `PhaseCode`       varchar(36)  COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '阶段编码（冗余，便于过滤）',
  `VersionNumber`   int          NOT NULL COMMENT '提取的文件版本',
  `RuleCode`        varchar(36)  COLLATE utf8mb4_general_ci NOT NULL COMMENT '使用的提取规则编码',
  `TableCode`       varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '定义表编码（cert_doc_table_def.Code，工作流 get_table 节点查询键）',
  `TableIndex`      int          DEFAULT '1' COMMENT '文档中第几个表格',
  `ExtractedJson`   json         NOT NULL COMMENT '表格内容（JSON）',
  `Confidence`      decimal(3,2) DEFAULT NULL COMMENT 'AI提取可信度',
  `PositionInfo`    json         DEFAULT NULL COMMENT '表格在文档中的位置信息',
  `ExtractedAt`     datetime     NOT NULL COMMENT '提取时间',
  `IsDeleted`       tinyint(1)   NOT NULL DEFAULT '0' COMMENT '软删除标记（false=正常，true=已删除）',
  `IsValid`         int          NOT NULL DEFAULT '1' COMMENT '有效标志（1=有效，0=无效）',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`            (`Code`),
  KEY        `idx_file_code`      (`FileCode`),
  KEY        `idx_rule_code`      (`RuleCode`),
  KEY        `idx_table_code`     (`TableCode`),
  KEY        `idx_tableext_ent_tbl` (`OrgCode`, `TableCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文档表格提取结果';

-- =============================================================================
-- 3. 验证（期望：两张表存在，列齐全，无外键，无 ent_ 残留）
-- =============================================================================

-- 3.1 表存在 + 列数
SELECT '--- 表存在性与列数（期望各 2 行）---' AS Info;
SELECT TABLE_NAME, COUNT(*) AS ColCount, TABLE_COLLATION
FROM information_schema.COLUMNS c
JOIN information_schema.TABLES t USING (TABLE_SCHEMA, TABLE_NAME)
WHERE c.TABLE_SCHEMA = 'yzh_cert_platform'
  AND c.TABLE_NAME IN ('cert_extraction_result', 'cert_table_extraction_result')
GROUP BY TABLE_NAME, TABLE_COLLATION;

-- 3.2 关键漂移列是否补齐（期望 2 行：FieldName / TableCode）
SELECT '--- 漂移列补齐检查（期望 2 行）---' AS Info;
SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND ((TABLE_NAME = 'cert_extraction_result'       AND COLUMN_NAME = 'FieldName')
    OR (TABLE_NAME = 'cert_table_extraction_result' AND COLUMN_NAME = 'TableCode'));

-- 3.3 外键（期望 0 行 —— 已按设计移除悬挂外键）
SELECT '--- 外键（期望 0 行）---' AS Info;
SELECT TABLE_NAME, CONSTRAINT_NAME, REFERENCED_TABLE_NAME
FROM information_schema.KEY_COLUMN_USAGE
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND REFERENCED_TABLE_NAME IS NOT NULL
  AND TABLE_NAME IN ('cert_extraction_result', 'cert_table_extraction_result');

-- 3.4 ent_ 前缀残留（期望 0 行）
SELECT '--- ent_ 前缀残留（期望 0 行）---' AS Info;
SELECT TABLE_NAME FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME LIKE 'ent\_%';

-- 3.5 命名规范复检（必须 COLLATE utf8mb4_bin，否则大小写不敏感恒 0 行）
SELECT '--- 非 PascalCase 列（期望 0 行）---' AS Info;
SELECT TABLE_NAME, COLUMN_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND TABLE_NAME IN ('cert_extraction_result', 'cert_table_extraction_result')
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin NOT REGEXP '^[A-Z][A-Za-z0-9]*$';

-- 3.6 Enable 列零容忍（期望 0 行）
SELECT '--- Enable 列（期望 0 行）---' AS Info;
SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin IN ('Enable', 'enable');
