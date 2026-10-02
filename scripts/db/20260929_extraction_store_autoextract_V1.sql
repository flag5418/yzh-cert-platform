-- ============================================================
-- S2 · 自动提取开关 + 提取结果活跃唯一键 + 提取状态 3 态
-- 对应：docs/40-实施/企业资料管理/10-执行计划-V1.md §7 ②③④
-- 日期：2026-09-29
-- ============================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ③ 上传任务的自动提取开关（S2：图 3 主路径的分流依据）
ALTER TABLE cert_upload_task
  ADD COLUMN AutoExtract TINYINT(1) NOT NULL DEFAULT 1
    COMMENT '上传完成后是否自动触发字段/表格提取（1=自动 0=仅转换不提取）';

-- ②-a 提取结果（B-08 字段）：活跃行唯一键 = 「同一版本最多 1 行 IsValid=1」
--     ActiveFlag 为生成列：IsValid=1 ⇒ 1，否则 NULL（NULL 不参与唯一约束 ⇒ 历史归档行不冲突）
ALTER TABLE cert_extraction_result
  ADD COLUMN ActiveFlag TINYINT GENERATED ALWAYS AS (IF(IsValid = 1, 1, NULL)) STORED,
  ADD UNIQUE KEY uk_extract_active (OrgCode, FileCode, FieldCode, VersionNumber, ActiveFlag),
  ADD KEY idx_ent_file_active (OrgCode, FileCode, IsValid, VersionNumber);

-- ②-b 表格提取结果（B-09 表格）：同口径活跃唯一键（该表已有 IsValid 列）
ALTER TABLE cert_table_extraction_result
  ADD COLUMN ActiveFlag TINYINT GENERATED ALWAYS AS (IF(IsValid = 1, 1, NULL)) STORED,
  ADD UNIQUE KEY uk_table_extract_active (OrgCode, FileCode, TableCode, VersionNumber, ActiveFlag),
  ADD KEY idx_ent_file_active (OrgCode, FileCode, IsValid, VersionNumber);

-- ④ 提取状态取值域收敛为 3 态（不新增 ExtractSkipReason 列）
--    none=未提取（含转换中/失败/未要求自动提取） / completed=已提取 / skipped=无规则
ALTER TABLE cert_standard_directory_file
  MODIFY COLUMN ExtractStatus varchar(20) NOT NULL DEFAULT 'none'
    COMMENT 'none=未提取 / completed=已提取 / skipped=无规则（失败原因落 ExtractMessage）';

-- ============================================================
-- 验证（应各返回 1）
-- ============================================================
-- SELECT COUNT(*) FROM information_schema.COLUMNS
--  WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='cert_upload_task' AND COLUMN_NAME='AutoExtract';
-- SELECT COUNT(*) FROM information_schema.STATISTICS
--  WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='cert_extraction_result' AND INDEX_NAME='uk_extract_active';
-- SELECT COUNT(*) FROM information_schema.COLUMNS
--  WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME='cert_table_extraction_result' AND COLUMN_NAME='ActiveFlag';
