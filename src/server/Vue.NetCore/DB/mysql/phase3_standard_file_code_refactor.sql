-- =====================================================================
-- Phase 3: 标准文件 code 枢纽改造
-- 日期: 2026-08-14
-- 设计文档: docs/70-当前执行/Phase3-标准文件code枢纽改造设计-V1.md
--
-- 核心设计:
--   standard_file_code 是核心枢纽字段，关联 cert_file_requirement.code
--   企业文件通过它关联到提取规则、字段定义、表格定义
--   org_code/standard_code/phase_code 作为冗余字段方便过滤和后期数据提取
--
-- 重要说明:
--   ⚠️ 不删除 phase_code！而是在保留 phase_code 的基础上新增 standard_file_code
--   ⚠️ 不删除 file_code！旧字段保留向后兼容，新代码不再使用
--   ⚠️ org_code 继承自 YZHBaseEntity，已存在于所有表中
--
-- 变更清单:
--   ent_extraction_result       +standard_file_code +standard_code（phase_code/org_code 已存在）
--   ent_table_extraction_result 同上
--   cert_doc_extraction_rule    +standard_file_code(UNIQUE) +standard_code +phase_code（org_code 已存在）
--   cert_file_requirement       +template_storage_path +template_file_name +standard_code
--   ent_enterprise_file         +standard_file_code
--
-- 规范: 所有表关联通过 code(GUID)，不通过 id(自增主键)
-- =====================================================================

-- ============================================================
-- 1. ent_extraction_result 改造
-- ============================================================
-- 1.1 新增 standard_file_code (关联 cert_file_requirement.code)
ALTER TABLE ent_extraction_result
  ADD COLUMN standard_file_code varchar(36) DEFAULT NULL COMMENT '标准文件编码(关联cert_file_requirement.code)' AFTER rule_code;

-- 1.2 新增 standard_code (冗余，关联 cert_iso_standard.code)
ALTER TABLE ent_extraction_result
  ADD COLUMN standard_code varchar(36) DEFAULT NULL COMMENT '标准编码(冗余)' AFTER standard_file_code;

-- 1.3 phase_code 已存在，保留为冗余字段（含义不变：认证阶段编码）
-- 1.4 org_code 已存在（从 YZHBaseEntity 继承）

-- 1.5 为 standard_file_code 添加索引
ALTER TABLE ent_extraction_result
  ADD INDEX idx_standard_file_code (standard_file_code);

-- ============================================================
-- 2. ent_table_extraction_result 改造
-- ============================================================
-- 2.1 新增 standard_file_code
ALTER TABLE ent_table_extraction_result
  ADD COLUMN standard_file_code varchar(36) DEFAULT NULL COMMENT '标准文件编码(关联cert_file_requirement.code)' AFTER rule_code;

-- 2.2 新增 standard_code (冗余)
ALTER TABLE ent_table_extraction_result
  ADD COLUMN standard_code varchar(36) DEFAULT NULL COMMENT '标准编码(冗余)' AFTER standard_file_code;

-- 2.3 phase_code 已存在，保留为冗余字段
-- 2.4 org_code 已存在

-- 2.5 为 standard_file_code 添加索引
ALTER TABLE ent_table_extraction_result
  ADD INDEX idx_standard_file_code (standard_file_code);

-- ============================================================
-- 3. cert_doc_extraction_rule 改造
-- 从 file_code(关联企业文件) 改为 standard_file_code(关联标准文件)
-- ============================================================
-- 3.1 新增 standard_file_code
ALTER TABLE cert_doc_extraction_rule
  ADD COLUMN standard_file_code varchar(36) DEFAULT NULL COMMENT '标准文件编码(关联cert_file_requirement.code)' AFTER file_code;

-- 3.2 新增 org_code (冗余)
ALTER TABLE cert_doc_extraction_rule
  ADD COLUMN standard_code varchar(36) DEFAULT NULL COMMENT '标准编码(冗余)' AFTER standard_file_code;

-- 3.3 新增 phase_code (冗余)
ALTER TABLE cert_doc_extraction_rule
  ADD COLUMN phase_code varchar(36) DEFAULT NULL COMMENT '阶段编码(冗余)' AFTER standard_code;

-- 3.4 去掉 file_code 的 UNIQUE 约束（旧约束名需要先查找）
-- 先尝试删除可能的 UNIQUE 约束
SET @constraint_name = (
  SELECT CONSTRAINT_NAME
  FROM information_schema.KEY_COLUMN_USAGE
  WHERE TABLE_SCHEMA = 'yzh_cert_platform'
    AND TABLE_NAME = 'cert_doc_extraction_rule'
    AND COLUMN_NAME = 'file_code'
    AND CONSTRAINT_NAME != 'PRIMARY'
);
SET @sql = IF(@constraint_name IS NOT NULL,
  CONCAT('ALTER TABLE cert_doc_extraction_rule DROP INDEX ', @constraint_name),
  'SELECT "file_code has no unique constraint to drop"');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 3.5 为 standard_file_code 添加 UNIQUE 约束（一个标准文件对应一个规则）
ALTER TABLE cert_doc_extraction_rule
  ADD UNIQUE INDEX uk_standard_file_code (standard_file_code);

-- 3.6 file_code 保留但不再作为关联键（向后兼容，后续可清理）
-- 注：file_code 字段保留，新代码不再使用它做查询

-- ============================================================
-- 4. cert_file_requirement 改造
-- 新增模板文件存储路径和标准编码
-- ============================================================
-- 4.1 新增 template_storage_path (模板文件在 OSS 中的路径)
ALTER TABLE cert_file_requirement
  ADD COLUMN template_storage_path varchar(500) DEFAULT NULL COMMENT '模板文件OSS存储路径(/standard-directory/...)' AFTER sort_order;

-- 4.2 新增 template_file_name (模板文件原始名)
ALTER TABLE cert_file_requirement
  ADD COLUMN template_file_name varchar(500) DEFAULT NULL COMMENT '模板文件原始名' AFTER template_storage_path;

-- 4.3 新增 standard_code (关联标准)
ALTER TABLE cert_file_requirement
  ADD COLUMN standard_code varchar(36) DEFAULT NULL COMMENT '标准编码(关联cert_iso_standard.code)' AFTER template_file_name;

-- 4.4 为 standard_code 添加索引
ALTER TABLE cert_file_requirement
  ADD INDEX idx_standard_code (standard_code);

-- ============================================================
-- 5. ent_enterprise_file 改造
-- 新增 standard_file_code 关联标准文件
-- ============================================================
-- 5.1 新增 standard_file_code
ALTER TABLE ent_enterprise_file
  ADD COLUMN standard_file_code varchar(36) DEFAULT NULL COMMENT '标准文件编码(关联cert_file_requirement.code)' AFTER upload_status;

-- 5.2 为 standard_file_code 添加索引
ALTER TABLE ent_enterprise_file
  ADD INDEX idx_standard_file_code (standard_file_code);

-- ============================================================
-- 验证
-- ============================================================
SELECT '=== ent_extraction_result ===' AS info;
DESCRIBE ent_extraction_result;

SELECT '=== ent_table_extraction_result ===' AS info;
DESCRIBE ent_table_extraction_result;

SELECT '=== cert_doc_extraction_rule ===' AS info;
DESCRIBE cert_doc_extraction_rule;

SELECT '=== cert_file_requirement ===' AS info;
DESCRIBE cert_file_requirement;

SELECT '=== ent_enterprise_file ===' AS info;
DESCRIBE ent_enterprise_file;
