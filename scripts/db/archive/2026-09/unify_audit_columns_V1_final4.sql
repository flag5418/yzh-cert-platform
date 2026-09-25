-- 最终修复：remark → Remark（仅 4 张表）
SET FOREIGN_KEY_CHECKS = 0;

ALTER TABLE cert_doc_extraction_rule RENAME COLUMN remark TO Remark;
ALTER TABLE cert_doc_field_def RENAME COLUMN remark TO Remark;
ALTER TABLE cert_doc_table_def RENAME COLUMN remark TO Remark;
ALTER TABLE cert_doc_table_field_def RENAME COLUMN remark TO Remark;

SET FOREIGN_KEY_CHECKS = 1;
