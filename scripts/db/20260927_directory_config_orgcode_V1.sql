-- ============================================================
-- 迁移：cert_standard_directory_config 加机构维度（决策⑳修订）
-- 日期：2026-09-27
--
-- 背景（2026-09-27 用户改判，推翻 2026-09-26 决策⑳）：
--   标准目录不是「平台全局库」，而是「各机构的标准落地目录模板」：
--   ① 主表按机构隔离：uk = (OrgCode, StandardCode, StageCode)
--      —— 不能假设所有机构对同一「标准 × 阶段」的目录结构与文件完全一致；
--   ② MinIO 路径同步加机构段（PathBuilder.StandardFile 改造）：
--        /standard-directory/{OrgCode}/{StandardCode}/{StageCode}/{FolderPath}/{FileName}
--      —— 不加段则两机构同名文件物理 key 相撞，标准库「纯覆盖无归档」语义会互相覆盖丢数据；
--   ③ 子表【不加】列：cert_standard_directory_folder / file 经 ConfigCode
--      → 本表 OrgCode 间接归属，避免三表同步加列与双份真源；
--   ④ 总表无感懒建（决策㉑）：首次进入阶段 / 上传时后端 Ensure 自动建行，
--      不要求用户手工创建（原「新建配置」入口降级为管理：改根名/状态/级联清理）。
--
-- 影响：原重建脚本 20260926_standard_directory_rebuild_V1.sql 第 18 行
--       「⑳ 三张表【不加】OrgCode 列」与第 195 行验证「三表无 OrgCode」作废
--       （历史脚本是当时的快照，不回改；以本脚本为准）。
--
-- 执行方式：mysql -uroot -p yzh_cert_platform < 本文件（DB: yzh_cert_platform，MySQL 8.0 @3307）
-- ============================================================

-- ------------------------------------------------------------
-- 1. 加列（跟随 cert_enterprise.OrgCode 宽度 varchar(50)；先加列 → 再清遗留 → 再换 uk）
-- ------------------------------------------------------------
ALTER TABLE cert_standard_directory_config
  ADD COLUMN OrgCode varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
      NOT NULL DEFAULT '' COMMENT '机构 Code → certification_body.Code（决策⑳修订：按机构隔离的目录归属）'
      AFTER Code;

-- ------------------------------------------------------------
-- 2. 清理遗留行：旧结构（无 OrgCode）的配置行机构无法归属。
--    只删【无子级】的行（folder/file 均无引用）—— 有子级则跳过，
--    由第 3 步前置检查报 ABORT，人工确认机构后再处理，禁止带子级盲删。
--    （执行时点实测：1 行、0 子级、0 文件 —— 均为 2026-09-27 冒烟数据）
-- ------------------------------------------------------------
DELETE c FROM cert_standard_directory_config c
WHERE c.OrgCode = ''
  AND NOT EXISTS (SELECT 1 FROM cert_standard_directory_folder f WHERE f.ConfigCode = c.Code)
  AND NOT EXISTS (SELECT 1 FROM cert_standard_directory_file  i WHERE i.ConfigCode = c.Code);

-- ------------------------------------------------------------
-- 3. 前置检查：必须返回 0 行（ok）。返回 ABORT = 存在带子级的遗留配置，
--    本脚本到此为止 → 人工给该行补 OrgCode（或先处理子级）后再执行第 4 步。
-- ------------------------------------------------------------
SELECT IF(COUNT(*) = 0, 'ok', 'ABORT：存在带子级的遗留配置，需人工归属机构后再换 uk') AS precheck
FROM cert_standard_directory_config c
WHERE c.OrgCode = ''
  AND (EXISTS (SELECT 1 FROM cert_standard_directory_folder f WHERE f.ConfigCode = c.Code)
    OR EXISTS (SELECT 1 FROM cert_standard_directory_file  i WHERE i.ConfigCode = c.Code));

-- ------------------------------------------------------------
-- 4. 换唯一键：uk_std_stage(标准,阶段) → uk_org_std_stage(机构,标准,阶段)
--    （沿用决策⑬：IsDeleted 不入键 → 软删行仍占键位，建前「含已删」查重 + 复活）
-- ------------------------------------------------------------
ALTER TABLE cert_standard_directory_config
  DROP KEY uk_std_stage,
  ADD UNIQUE KEY uk_org_std_stage (OrgCode, StandardCode, StageCode);

-- ------------------------------------------------------------
-- 5. 验证 SQL（必须全 ok；任何 FAIL = 迁移未完成，禁止继续编码）
-- ------------------------------------------------------------
SELECT '1. OrgCode 列存在' AS chk,
       IF(COUNT(*) = 1, 'ok', 'FAIL') AS result
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND TABLE_NAME = 'cert_standard_directory_config'
  AND COLUMN_NAME = 'OrgCode'
  AND COLUMN_TYPE LIKE 'varchar(50)%';

SELECT '2. uk_org_std_stage(机构,标准,阶段)' AS chk,
       IF(GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) = 'OrgCode,StandardCode,StageCode', 'ok', 'FAIL') AS result
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND TABLE_NAME = 'cert_standard_directory_config'
  AND INDEX_NAME = 'uk_org_std_stage';

SELECT '3. 旧键 uk_std_stage 已移除' AS chk,
       IF(COUNT(*) = 0, 'ok', 'FAIL') AS result
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND TABLE_NAME = 'cert_standard_directory_config'
  AND INDEX_NAME = 'uk_std_stage';

SELECT '4. 无空机构遗留行' AS chk,
       IF(COUNT(*) = 0, 'ok', 'FAIL') AS result
FROM cert_standard_directory_config
WHERE OrgCode = '';

-- 排序规则一致性（铁律八：全库 utf8mb4_general_ci，避免跨表关联 1267）
SELECT '5. 列排序规则 = utf8mb4_general_ci' AS chk,
       IF(COLLATION_NAME = 'utf8mb4_general_ci', 'ok', 'FAIL') AS result
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND TABLE_NAME = 'cert_standard_directory_config'
  AND COLUMN_NAME = 'OrgCode';
