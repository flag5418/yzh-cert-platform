-- ============================================================
-- 20260917_file_requirement_template_columns.sql
-- cert_file_requirement 补齐模板文件列（迁移至新架构时丢失）
--
-- 背景：
--   旧架构 cert_file_requirement 含 TemplateStoragePath / TemplateFileName / StandardCode，
--   新库建表时未携带 → 文档提取链路的「文件要求模板（FR-xxx）优先」分支必然抛
--   Unknown column 'template_storage_path'，日志被刷爆（实测 246 次/小时），并降级到
--   「标准目录实际文件」分支。
--   C# 实体 CertPlatform.Shared/Entities/Cert/FileRequirement.cs 已同步为 PascalCase 映射。
--
-- 幂等：information_schema 守卫（MySQL 8.0 不支持 ADD COLUMN IF NOT EXISTS）
-- 执行：docker exec -i yzh-mysql mysql -uroot -p*** yzh_cert_platform < 本文件
-- ============================================================

SET @db := DATABASE();

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_file_requirement' AND COLUMN_NAME = 'TemplateStoragePath') = 0,
  'ALTER TABLE cert_file_requirement ADD COLUMN TemplateStoragePath VARCHAR(500) NULL COMMENT ''模板文件 OSS 存储路径'' AFTER SortOrder',
  'SELECT ''TemplateStoragePath 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_file_requirement' AND COLUMN_NAME = 'TemplateFileName') = 0,
  'ALTER TABLE cert_file_requirement ADD COLUMN TemplateFileName VARCHAR(500) NULL COMMENT ''模板文件原始名'' AFTER TemplateStoragePath',
  'SELECT ''TemplateFileName 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_file_requirement' AND COLUMN_NAME = 'StandardCode') = 0,
  'ALTER TABLE cert_file_requirement ADD COLUMN StandardCode VARCHAR(36) NULL COMMENT ''标准编码（cert_iso_standard.Code）'' AFTER TemplateFileName',
  'SELECT ''StandardCode 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- 校验
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_file_requirement'
ORDER BY ORDINAL_POSITION;
