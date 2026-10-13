-- ============================================================
-- 20261013_std_dir_config_owner_org_V1.sql
-- 归属判据收敛 · 步骤 2：cert_standard_directory_config 新增「机构 Code」专用列
--
-- 依据：`docs/40-实施/迁移计划/归属判据收敛与机构专用列-设计-V1.md`（用户 2026-10-13 已批）
--       用户口径：「yzh-std-env 是所有机构的统称……如果表中需要特定的机构，
--                 则可以增加新的字段，专门存储机构的 code」
--
-- 【为什么要加这一列】（实测证据链）
--   1) 现状：23 个标准目录文件 的 ConfigCode 全 = b27945be5e1b44f28bcf8fc19852d92d，
--      而该 config 行 OrgCode = 'yzh-std-env'（后台域标记）；
--      但 23 行的 StoragePath 机构段 23/23 = '906e8b2a962c4062b21144af4cc4abc0'（河北雄安尚龙）
--      ⇒ 同一逻辑目录，逻辑归属与物理归属分叉。
--   2) 代码 PathBuilder.StandardFile(config.OrgCode, …) 的身份段取 OrgCode
--      ⇒ 迁移后新产物会写成 /standard-directory/yzh-std-env/…，与存量 906e8b2a… 两套前缀。
--   3) 类注释「★ 按机构隔离（决策⑳修订）」的本意 = 不同机构可有不同的标准落地目录，
--      但 10-12 把 OrgCode 统一为 yzh-std-env 后，该隔离能力丢失。
--   ⇒ 正解：新增 OwnerOrgCode 承载「具体机构」维度，路径与唯一键改用它；
--      OrgCode 保持「后台域标记」语义不变。
--
-- 【本脚本改动】
--   §1 快照（回滚依据）
--   §2 加列 OwnerOrgCode varchar(50) NOT NULL DEFAULT ''
--   §3 回填：b27945be… 的 OwnerOrgCode = 906e8b2a…（覆盖全部 23 个文件，存量路径不变）
--   §4 唯一键：uk_enterprise_std_stage(OrgCode,…) → uk_owner_std_stage(OwnerOrgCode,…)
--   §5 DB 列注释统一（清废弃字面量 YZH-STD-ENT + 修正三方分叉）
--   §6 验证
--   §7 反向脚本（注释态，需要时手工放开）
--
-- 【存量 23 行路径不受影响】回填后 OwnerOrgCode = 906e8b2a…，
--   新口径 PathBuilder 身份段 = OwnerSeg(config) = '906e8b2a…' ⇒ 与存量完全一致。
--
-- 幂等：information_schema 守卫（MySQL 8.0 不支持 ADD COLUMN IF NOT EXISTS）
-- 执行：docker exec -i yzh-mysql mysql -uroot -p*** --default-character-set=utf8mb4 yzh_cert_platform < 本文件
-- 同步：C# 实体 StandardDirectoryConfig.cs（新增 OwnerOrgCode 属性）
--       ⚠️ all_tables_ddl.sql 为冻结时点参考件（文件头禁手工编辑），不在其上同步。
-- ⚠️ 本脚本**只动结构 + 注释 + 1 行回填**，不含任何业务数据改写。
-- ============================================================

SET @db := DATABASE();

-- ─────────────────────────────────────────────
-- 1) 快照（回滚依据；改前 2 行全量留档）
-- ─────────────────────────────────────────────

DROP TABLE IF EXISTS _bak_20261013_std_dir_config_owner;
CREATE TABLE _bak_20261013_std_dir_config_owner AS
  SELECT * FROM cert_standard_directory_config;

SELECT '§1 快照' AS stage, COUNT(*) AS rows_snapshotted
  FROM _bak_20261013_std_dir_config_owner;

-- ─────────────────────────────────────────────
-- 2) 加列 OwnerOrgCode
--    语义：具体机构 Code（= sys_organization.Code = cert_certification_body.Code）
--          '' = 不特指任何机构（平台通用模板）
-- ─────────────────────────────────────────────

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_standard_directory_config'
       AND COLUMN_NAME = 'OwnerOrgCode') = 0,
  'ALTER TABLE cert_standard_directory_config ADD COLUMN OwnerOrgCode varchar(50) NOT NULL DEFAULT '''' COMMENT ''具体机构 Code（= sys_organization.Code / cert_certification_body.Code）；空 = 不特指机构（平台通用模板）'' AFTER OrgCode',
  'SELECT ''cert_standard_directory_config.OwnerOrgCode 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ─────────────────────────────────────────────
-- 3) 回填：该 config 行覆盖全部 23 个文件（机构 = 河北雄安尚龙认证有限公司）
--    机构 Code 来源：23 行 StoragePath 的机构段实测值（23/23 一致）
-- ─────────────────────────────────────────────

UPDATE cert_standard_directory_config
   SET OwnerOrgCode = '906e8b2a962c4062b21144af4cc4abc0'
 WHERE Code = 'b27945be5e1b44f28bcf8fc19852d92d'
   AND OwnerOrgCode = '';

SELECT '§3 回填后' AS stage, Code, OrgCode, EnterpriseCode, OwnerOrgCode
  FROM cert_standard_directory_config;

-- ─────────────────────────────────────────────
-- 4) 唯一键切换：uk_enterprise_std_stage(OrgCode, EnterpriseCode, StandardCode, StageCode)
--                → uk_owner_std_stage(OwnerOrgCode, EnterpriseCode, StandardCode, StageCode)
--    + 补 idx_org_code（OrgCode 退出 uk 首列后不能丢索引）
--    ⚠️ 企业行也必须落 OwnerOrgCode（代码侧已同步）—— 否则企业行去重口径会变。
-- ─────────────────────────────────────────────

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_standard_directory_config'
       AND INDEX_NAME = 'uk_owner_std_stage') = 0,
  'ALTER TABLE cert_standard_directory_config
     ADD UNIQUE KEY uk_owner_std_stage (OwnerOrgCode, EnterpriseCode, StandardCode, StageCode)',
  'SELECT ''uk_owner_std_stage 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_standard_directory_config'
       AND INDEX_NAME = 'uk_enterprise_std_stage') > 0,
  'ALTER TABLE cert_standard_directory_config DROP INDEX uk_enterprise_std_stage',
  'SELECT ''uk_enterprise_std_stage 不存在，跳过'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_standard_directory_config'
       AND INDEX_NAME = 'idx_org_code') = 0,
  'ALTER TABLE cert_standard_directory_config ADD INDEX idx_org_code (OrgCode)',
  'SELECT ''idx_org_code 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ─────────────────────────────────────────────
-- 5) DB 列注释统一（清废弃字面量 YZH-STD-ENT + 修正三方分叉）
--    判据：企业列只回答「有没有企业」（空 = 非企业行）；机构值 ⛔ 不进企业列。
-- ─────────────────────────────────────────────

ALTER TABLE cert_standard_directory_config
  MODIFY COLUMN EnterpriseCode varchar(36) NOT NULL
    COMMENT '企业 Code。真实值 = 该企业该标准该阶段的企业目录；空 = 不属于任何企业（后台/机构模板行）';

ALTER TABLE cert_standard_directory_config
  MODIFY COLUMN OrgCode varchar(50) NOT NULL
    COMMENT '后台域标记（两值模型）：模板行恒 yzh-std-env = 所有机构的统称；具体机构见 OwnerOrgCode';

-- ⚠️ 这两列实测为 varchar(50) NULL（可空），⛔ 不要顺手改成 NOT NULL（会改可空性）
ALTER TABLE cert_extraction_result
  MODIFY COLUMN OrgCode varchar(50) NULL
    COMMENT '企业 Code（本表归属维度 = 企业）';

ALTER TABLE cert_table_extraction_result
  MODIFY COLUMN OrgCode varchar(50) NULL
    COMMENT '企业 Code（本表归属维度 = 企业）';

-- ─────────────────────────────────────────────
-- 6) 验证
-- ─────────────────────────────────────────────

SELECT '§6a 新列存在' AS chk,
       COUNT(*) AS n
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_standard_directory_config'
   AND COLUMN_NAME = 'OwnerOrgCode';

SELECT '§6b 索引' AS chk, INDEX_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) AS cols
  FROM information_schema.STATISTICS
 WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'cert_standard_directory_config'
 GROUP BY INDEX_NAME;

-- §6c 废弃字面量残留（期望 0）
--   ⚠️ 排除两类**合法**出现，否则误报：
--     ① 备份表（_bak_*，拷贝了改前注释，属留档）
--     ② 刻意写的「否定句」（如 _file 注释「⛔ 不再用 YZH-STD-ENT 标记模板行」）
SELECT '§6c 废弃字面量残留（排除备份/否定句）' AS chk,
       TABLE_NAME, COLUMN_NAME
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = @db
   AND COLUMN_COMMENT LIKE '%YZH-STD-ENT%'
   AND TABLE_NAME NOT LIKE '\_bak\_%'
   AND COLUMN_COMMENT NOT LIKE '%不再用 YZH-STD-ENT%';

SELECT '§6d 归属分布' AS chk, OrgCode, EnterpriseCode, OwnerOrgCode, COUNT(*) AS n
  FROM cert_standard_directory_config
 GROUP BY OrgCode, EnterpriseCode, OwnerOrgCode;

-- ─────────────────────────────────────────────
-- 7) 反向脚本（需要时手工放开；⛔ 代码 revert 不会回滚本脚本的数据改动）
-- ─────────────────────────────────────────────

-- ALTER TABLE cert_standard_directory_config DROP INDEX uk_owner_std_stage;
-- ALTER TABLE cert_standard_directory_config
--   ADD UNIQUE KEY uk_enterprise_std_stage (OrgCode, EnterpriseCode, StandardCode, StageCode);
-- ALTER TABLE cert_standard_directory_config DROP INDEX idx_org_code;
-- ALTER TABLE cert_standard_directory_config DROP COLUMN OwnerOrgCode;
-- ALTER TABLE cert_standard_directory_config
--   MODIFY COLUMN EnterpriseCode varchar(36) NOT NULL
--     COMMENT '企业Code：YZH-STD-ENT=机构模板行；真实值=该企业目录（⛔禁NULL，P13）';
-- ALTER TABLE cert_standard_directory_config
--   MODIFY COLUMN OrgCode varchar(50) NOT NULL
--     COMMENT '机构 Code → certification_body.Code（决策⑳修订：按机构隔离的目录归属）';
-- ALTER TABLE cert_extraction_result
--   MODIFY COLUMN OrgCode varchar(50) NULL
--     COMMENT '组织编码（标准样例企业 YZH-STD-ENT 等）';
-- ALTER TABLE cert_table_extraction_result
--   MODIFY COLUMN OrgCode varchar(50) NULL
--     COMMENT '组织编码（标准样例企业 YZH-STD-ENT 等）';
