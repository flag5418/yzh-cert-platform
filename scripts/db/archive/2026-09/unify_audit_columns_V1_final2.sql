-- 最终修复：清理剩余旧列名 v2
SET FOREIGN_KEY_CHECKS = 0;

-- cert_standard_directory_file
ALTER TABLE cert_standard_directory_file RENAME COLUMN enable TO IsValid;

-- cert_standard_directory_folder
ALTER TABLE cert_standard_directory_folder DROP COLUMN creator;
ALTER TABLE cert_standard_directory_folder DROP COLUMN modifier;
ALTER TABLE cert_standard_directory_folder DROP COLUMN deleter;
ALTER TABLE cert_standard_directory_folder RENAME COLUMN enable TO IsValid;

-- cert_standard_phase_config
ALTER TABLE cert_standard_phase_config DROP COLUMN creator;
ALTER TABLE cert_standard_phase_config DROP COLUMN modifier;
ALTER TABLE cert_standard_phase_config DROP COLUMN deleter;
ALTER TABLE cert_standard_phase_config RENAME COLUMN enable TO IsValid;

-- cert_validation_rule_source
ALTER TABLE cert_validation_rule_source DROP COLUMN creator;
ALTER TABLE cert_validation_rule_source DROP COLUMN modifier;
ALTER TABLE cert_validation_rule_source DROP COLUMN deleter;
ALTER TABLE cert_validation_rule_source RENAME COLUMN enable TO IsValid;

SET FOREIGN_KEY_CHECKS = 1;
