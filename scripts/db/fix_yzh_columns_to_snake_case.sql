-- =============================================================================
-- YZH Framework Column Migration: PascalCase → snake_case
-- Date: 2026-09-04
-- Purpose: Align database column names with YZH entity [Column("snake_case")] mappings
-- =============================================================================

-- ===== 通用 YZHBaseEntity 列重命名（所有包含 cert_/yzh_/wf_/audit_/ent_/rpt_ 前缀的表） =====

-- cert_certification_body 表
ALTER TABLE cert_certification_body 
  CHANGE COLUMN CreateID create_id INT DEFAULT NULL,
  CHANGE COLUMN Creator creator VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN CreateDate create_date DATETIME DEFAULT NULL,
  CHANGE COLUMN ModifyID modify_id INT DEFAULT NULL,
  CHANGE COLUMN Modifier modifier VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN ModifyDate modify_date DATETIME DEFAULT NULL,
  CHANGE COLUMN DeleteID delete_id INT DEFAULT NULL,
  CHANGE COLUMN Deleter deleter VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN DeleteTime delete_time DATETIME DEFAULT NULL,
  CHANGE COLUMN Status status VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN Enable enable TINYINT(1) DEFAULT 1;

-- cert_iso_standard 表  
ALTER TABLE cert_iso_standard
  CHANGE COLUMN CreateID create_id INT DEFAULT NULL,
  CHANGE COLUMN Creator creator VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN CreateDate create_date DATETIME DEFAULT NULL,
  CHANGE COLUMN ModifyID modify_id INT DEFAULT NULL,
  CHANGE COLUMN Modifier modifier VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN ModifyDate modify_date DATETIME DEFAULT NULL,
  CHANGE COLUMN DeleteID delete_id INT DEFAULT NULL,
  CHANGE COLUMN Deleter deleter VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN DeleteTime delete_time DATETIME DEFAULT NULL,
  CHANGE COLUMN Status status VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN Enable enable TINYINT(1) DEFAULT 1;

-- cert_iso_clause 表
ALTER TABLE cert_iso_clause
  CHANGE COLUMN CreateID create_id INT DEFAULT NULL,
  CHANGE COLUMN Creator creator VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN CreateDate create_date DATETIME DEFAULT NULL,
  CHANGE COLUMN ModifyID modify_id INT DEFAULT NULL,
  CHANGE COLUMN Modifier modifier VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN ModifyDate modify_date DATETIME DEFAULT NULL,
  CHANGE COLUMN DeleteID delete_id INT DEFAULT NULL,
  CHANGE COLUMN Deleter deleter VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN DeleteTime delete_time DATETIME DEFAULT NULL,
  CHANGE COLUMN Status status VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN Enable enable TINYINT(1) DEFAULT 1;

-- cert_cert_stage 表
ALTER TABLE cert_cert_stage
  CHANGE COLUMN CreateID create_id INT DEFAULT NULL,
  CHANGE COLUMN Creator creator VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN CreateDate create_date DATETIME DEFAULT NULL,
  CHANGE COLUMN ModifyID modify_id INT DEFAULT NULL,
  CHANGE COLUMN Modifier modifier VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN ModifyDate modify_date DATETIME DEFAULT NULL,
  CHANGE COLUMN DeleteID delete_id INT DEFAULT NULL,
  CHANGE COLUMN Deleter deleter VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN DeleteTime delete_time DATETIME DEFAULT NULL,
  CHANGE COLUMN Status status VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN Enable enable TINYINT(1) DEFAULT 1;

-- cert_application 表
ALTER TABLE cert_application
  CHANGE COLUMN CreateID create_id INT DEFAULT NULL,
  CHANGE COLUMN Creator creator VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN CreateDate create_date DATETIME DEFAULT NULL,
  CHANGE COLUMN ModifyID modify_id INT DEFAULT NULL,
  CHANGE COLUMN Modifier modifier VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN ModifyDate modify_date DATETIME DEFAULT NULL,
  CHANGE COLUMN DeleteID delete_id INT DEFAULT NULL,
  CHANGE COLUMN Deleter deleter VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN DeleteTime delete_time DATETIME DEFAULT NULL,
  CHANGE COLUMN Status status VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN Enable enable TINYINT(1) DEFAULT 1;

-- cert_enterprise 表
ALTER TABLE cert_enterprise
  CHANGE COLUMN CreateID create_id INT DEFAULT NULL,
  CHANGE COLUMN Creator creator VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN CreateDate create_date DATETIME DEFAULT NULL,
  CHANGE COLUMN ModifyID modify_id INT DEFAULT NULL,
  CHANGE COLUMN Modifier modifier VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN ModifyDate modify_date DATETIME DEFAULT NULL,
  CHANGE COLUMN DeleteID delete_id INT DEFAULT NULL,
  CHANGE COLUMN Deleter deleter VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN DeleteTime delete_time DATETIME DEFAULT NULL,
  CHANGE COLUMN Status status VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN Enable enable TINYINT(1) DEFAULT 1;

-- wf_workflow_definition 表
ALTER TABLE wf_workflow_definition
  CHANGE COLUMN CreateID create_id INT DEFAULT NULL,
  CHANGE COLUMN Creator creator VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN CreateDate create_date DATETIME DEFAULT NULL,
  CHANGE COLUMN ModifyID modify_id INT DEFAULT NULL,
  CHANGE COLUMN Modifier modifier VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN ModifyDate modify_date DATETIME DEFAULT NULL,
  CHANGE COLUMN DeleteID delete_id INT DEFAULT NULL,
  CHANGE COLUMN Deleter deleter VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN DeleteTime delete_time DATETIME DEFAULT NULL,
  CHANGE COLUMN Status status VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN Enable enable TINYINT(1) DEFAULT 1;

-- wf_skill_category 表
ALTER TABLE wf_skill_category
  CHANGE COLUMN CreateID create_id INT DEFAULT NULL,
  CHANGE COLUMN Creator creator VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN CreateDate create_date DATETIME DEFAULT NULL,
  CHANGE COLUMN ModifyID modify_id INT DEFAULT NULL,
  CHANGE COLUMN Modifier modifier VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN ModifyDate modify_date DATETIME DEFAULT NULL,
  CHANGE COLUMN DeleteID delete_id INT DEFAULT NULL,
  CHANGE COLUMN Deleter deleter VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN DeleteTime delete_time DATETIME DEFAULT NULL,
  CHANGE COLUMN Status status VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN Enable enable TINYINT(1) DEFAULT 1;

-- yzh_page_config 表
ALTER TABLE yzh_page_config
  CHANGE COLUMN CreateID create_id INT DEFAULT NULL,
  CHANGE COLUMN Creator creator VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN CreateDate create_date DATETIME DEFAULT NULL,
  CHANGE COLUMN ModifyID modify_id INT DEFAULT NULL,
  CHANGE COLUMN Modifier modifier VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN ModifyDate modify_date DATETIME DEFAULT NULL,
  CHANGE COLUMN DeleteID delete_id INT DEFAULT NULL,
  CHANGE COLUMN Deleter deleter VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN DeleteTime delete_time DATETIME DEFAULT NULL,
  CHANGE COLUMN Status status VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN Enable enable TINYINT(1) DEFAULT 1;

-- yzh_field_config 表
ALTER TABLE yzh_field_config
  CHANGE COLUMN CreateID create_id INT DEFAULT NULL,
  CHANGE COLUMN Creator creator VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN CreateDate create_date DATETIME DEFAULT NULL,
  CHANGE COLUMN ModifyID modify_id INT DEFAULT NULL,
  CHANGE COLUMN Modifier modifier VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN ModifyDate modify_date DATETIME DEFAULT NULL,
  CHANGE COLUMN DeleteID delete_id INT DEFAULT NULL,
  CHANGE COLUMN Deleter deleter VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN DeleteTime delete_time DATETIME DEFAULT NULL,
  CHANGE COLUMN Status status VARCHAR(50) DEFAULT NULL,
  CHANGE COLUMN Enable enable TINYINT(1) DEFAULT 1;
