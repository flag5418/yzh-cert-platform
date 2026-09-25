-- 最终修复：清理剩余旧列名 v3
SET FOREIGN_KEY_CHECKS = 0;

-- cert_standard_directory_file（已有 IsValid，需要删除旧的 enable）
ALTER TABLE cert_standard_directory_file DROP COLUMN enable;

-- cert_standard_directory_folder（已有 IsValid，需要删除 creator/modifier/deleter/enable）
ALTER TABLE cert_standard_directory_folder DROP COLUMN creator;
ALTER TABLE cert_standard_directory_folder DROP COLUMN modifier;
ALTER TABLE cert_standard_directory_folder DROP COLUMN deleter;
ALTER TABLE cert_standard_directory_folder DROP COLUMN enable;

-- cert_standard_phase_config（没有 IsValid，需删除重复+改名）
ALTER TABLE cert_standard_phase_config DROP COLUMN creator;
ALTER TABLE cert_standard_phase_config DROP COLUMN modifier;
ALTER TABLE cert_standard_phase_config DROP COLUMN deleter;
ALTER TABLE cert_standard_phase_config RENAME COLUMN enable TO IsValid;

-- cert_validation_rule_source（没有 IsValid，需删除重复+改名）
ALTER TABLE cert_validation_rule_source DROP COLUMN creator;
ALTER TABLE cert_validation_rule_source DROP COLUMN modifier;
ALTER TABLE cert_validation_rule_source DROP COLUMN deleter;
ALTER TABLE cert_validation_rule_source RENAME COLUMN enable TO IsValid;

SET FOREIGN_KEY_CHECKS = 1;
