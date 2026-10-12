-- ============================================================================
-- 单一归属字段统一 V1（2026-10-12）
-- 依据：用户裁决 P1 = A（留 OrgCode / yzh-std-env）；P2 = 保留 EnterpriseCode，语义收窄
-- 目标：一切「后台标准级 / 模板行」判别统一走 OrgCode = 'yzh-std-env'
--       ⛔ 退役 YZH-STD-ENT（YzhVirtualEnterprise.Code）
-- 前置备份：scripts/db/backup/backup_20261012_full_pre_orphan_clean.sql.gz
--           + 本脚本内 §0 两表快照
-- 幂等：可重复执行
-- ============================================================================

SELECT '§0 前置快照（回滚依据）' AS step;
DROP TABLE IF EXISTS _bak_20261012_std_dir_file;
CREATE TABLE _bak_20261012_std_dir_file AS SELECT * FROM cert_standard_directory_file;
DROP TABLE IF EXISTS _bak_20261012_std_dir_config;
CREATE TABLE _bak_20261012_std_dir_config AS SELECT * FROM cert_standard_directory_config;
SELECT
  (SELECT COUNT(*) FROM _bak_20261012_std_dir_file)   AS file_rows,
  (SELECT COUNT(*) FROM _bak_20261012_std_dir_config) AS config_rows;

-- ---------------------------------------------------------------------------
SELECT '§1 cert_standard_directory_file 加 OrgCode 列（幂等）' AS step;
SET @has_col := (SELECT COUNT(*) FROM information_schema.COLUMNS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'cert_standard_directory_file'
                   AND COLUMN_NAME = 'OrgCode');
SET @ddl := IF(@has_col = 0,
  "ALTER TABLE cert_standard_directory_file
     ADD COLUMN OrgCode varchar(50) NOT NULL DEFAULT ''
     COMMENT '租户归属（唯一判据）：yzh-std-env=后台标准域；真实值=机构/企业 Code'
     AFTER ConfigCode",
  'SELECT ''already exists'' AS note');
PREPARE s FROM @ddl; EXECUTE s; DEALLOCATE PREPARE s;

-- ---------------------------------------------------------------------------
SELECT '§2 后台标准域行回填 OrgCode = yzh-std-env' AS step;
-- 判据：EnterpriseCode 为空/YZH-STD-ENT 且尚未回填的行
-- 实测（2026-10-12）：23 行全部 StoragePath 非空 + UploadStatus='uploaded'
--   ⇒ 是「后台标准域的实际文件行」，不是企业上传行 ⇒ 归 yzh-std-env 正确
UPDATE cert_standard_directory_file
   SET OrgCode = 'yzh-std-env'
 WHERE OrgCode = ''
   AND (EnterpriseCode IS NULL OR EnterpriseCode = '' OR EnterpriseCode = 'YZH-STD-ENT');

SELECT CONCAT('回填行数 = ', ROW_COUNT()) AS note;

-- ---------------------------------------------------------------------------
SELECT '§3 EnterpriseCode 列注释收窄（语义 = 哪家企业，不再是模板行标记）' AS step;
ALTER TABLE cert_standard_directory_file
  MODIFY COLUMN EnterpriseCode varchar(36) NOT NULL DEFAULT ''
  COMMENT '企业 Code（语义收窄 2026-10-12）：真实值=该企业；空=不属于任何企业（后台/机构行）。⛔ 不再用 YZH-STD-ENT 标记模板行';

-- ---------------------------------------------------------------------------
SELECT '§4 补索引（新判据 OrgCode 为前缀）' AS step;
SET @has_idx := (SELECT COUNT(*) FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'cert_standard_directory_file'
                   AND INDEX_NAME = 'idx_org_std_stage');
SET @ddl2 := IF(@has_idx = 0,
  'ALTER TABLE cert_standard_directory_file ADD INDEX idx_org_std_stage (OrgCode, StandardCode, StageCode)',
  'SELECT ''already exists'' AS note');
PREPARE s2 FROM @ddl2; EXECUTE s2; DEALLOCATE PREPARE s2;

-- ---------------------------------------------------------------------------
SELECT '§5 核对：OrgCode 取值分布' AS step;
SELECT OrgCode, COUNT(*) AS n FROM cert_standard_directory_file GROUP BY OrgCode;

SELECT '§6 核对：config 表（本表 OrgCode 被「机构」占用，判别走 EnterpriseCode 是否为空）' AS step;
SELECT OrgCode, EnterpriseCode, COUNT(*) AS n FROM cert_standard_directory_config GROUP BY OrgCode, EnterpriseCode;

SELECT '§7 核对：全库 YZH-STD-ENT 残留（应为 0）' AS step;
SELECT
  (SELECT COUNT(*) FROM cert_standard_directory_file   WHERE EnterpriseCode = 'YZH-STD-ENT') AS file_ent,
  (SELECT COUNT(*) FROM cert_standard_directory_config WHERE EnterpriseCode = 'YZH-STD-ENT') AS config_ent;
