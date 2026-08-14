-- ============================================================
-- cert_doc_* 系列表审计字段统一改造
-- 日期: 2026-08-14
-- 说明: 将 update_id/update_date 改名为 modify_id/modify_date
--       补充 creator/modifier/deleter/delete_id/delete_time/enable 列
--       使审计字段与 YZHBaseEntity 基类 [Column] 映射一致
-- ============================================================

SET FOREIGN_KEY_CHECKS = 0;

-- 1. cert_doc_extraction_rule
ALTER TABLE cert_doc_extraction_rule 
  CHANGE COLUMN update_id modify_id int NULL,
  CHANGE COLUMN update_date modify_date datetime NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  ADD COLUMN creator varchar(50) NULL COMMENT '创建人姓名' AFTER create_id,
  ADD COLUMN modifier varchar(50) NULL COMMENT '修改人姓名' AFTER modify_id,
  ADD COLUMN delete_id int NULL COMMENT '删除人ID',
  ADD COLUMN deleter varchar(50) NULL COMMENT '删除人姓名',
  ADD COLUMN delete_time datetime NULL COMMENT '删除时间',
  ADD COLUMN enable tinyint(1) NOT NULL DEFAULT 1 COMMENT '启用状态';

-- 2. cert_doc_field_def
ALTER TABLE cert_doc_field_def 
  CHANGE COLUMN update_id modify_id int NULL,
  CHANGE COLUMN update_date modify_date datetime NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  ADD COLUMN creator varchar(50) NULL COMMENT '创建人姓名' AFTER create_id,
  ADD COLUMN modifier varchar(50) NULL COMMENT '修改人姓名' AFTER modify_id,
  ADD COLUMN delete_id int NULL COMMENT '删除人ID',
  ADD COLUMN deleter varchar(50) NULL COMMENT '删除人姓名',
  ADD COLUMN delete_time datetime NULL COMMENT '删除时间',
  ADD COLUMN enable tinyint(1) NOT NULL DEFAULT 1 COMMENT '启用状态',
  ADD COLUMN org_code varchar(50) NULL COMMENT '机构编码',
  ADD COLUMN status varchar(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';

-- 3. cert_doc_table_def
ALTER TABLE cert_doc_table_def 
  CHANGE COLUMN update_id modify_id int NULL,
  CHANGE COLUMN update_date modify_date datetime NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  ADD COLUMN creator varchar(50) NULL COMMENT '创建人姓名' AFTER create_id,
  ADD COLUMN modifier varchar(50) NULL COMMENT '修改人姓名' AFTER modify_id,
  ADD COLUMN delete_id int NULL COMMENT '删除人ID',
  ADD COLUMN deleter varchar(50) NULL COMMENT '删除人姓名',
  ADD COLUMN delete_time datetime NULL COMMENT '删除时间',
  ADD COLUMN enable tinyint(1) NOT NULL DEFAULT 1 COMMENT '启用状态',
  ADD COLUMN org_code varchar(50) NULL COMMENT '机构编码',
  ADD COLUMN status varchar(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';

-- 4. cert_doc_table_field_def
ALTER TABLE cert_doc_table_field_def 
  CHANGE COLUMN update_id modify_id int NULL,
  CHANGE COLUMN update_date modify_date datetime NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  ADD COLUMN creator varchar(50) NULL COMMENT '创建人姓名' AFTER create_id,
  ADD COLUMN modifier varchar(50) NULL COMMENT '修改人姓名' AFTER modify_id,
  ADD COLUMN delete_id int NULL COMMENT '删除人ID',
  ADD COLUMN deleter varchar(50) NULL COMMENT '删除人姓名',
  ADD COLUMN delete_time datetime NULL COMMENT '删除时间',
  ADD COLUMN enable tinyint(1) NOT NULL DEFAULT 1 COMMENT '启用状态',
  ADD COLUMN org_code varchar(50) NULL COMMENT '机构编码',
  ADD COLUMN status varchar(50) NOT NULL DEFAULT 'active' COMMENT '业务状态';

SET FOREIGN_KEY_CHECKS = 1;

-- 验证
SELECT 'cert_doc_extraction_rule' AS tbl, GROUP_CONCAT(COLUMN_NAME) AS columns 
FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='cert_doc_extraction_rule';
SELECT 'cert_doc_field_def' AS tbl, GROUP_CONCAT(COLUMN_NAME) AS columns 
FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='cert_doc_field_def';
SELECT 'cert_doc_table_def' AS tbl, GROUP_CONCAT(COLUMN_NAME) AS columns 
FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='cert_doc_table_def';
SELECT 'cert_doc_table_field_def' AS tbl, GROUP_CONCAT(COLUMN_NAME) AS columns 
FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='cert_doc_table_field_def';
