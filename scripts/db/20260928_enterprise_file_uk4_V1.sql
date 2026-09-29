-- ============================================================
-- G-0b：企业资料管理底座迁移① —— EnterpriseCode 禁 NULL + 四列 uk（P13/P16/Q1）
-- 日期：2026-09-28
-- 权威依据：06 册 03 号计划 G-0b、01 号 §八 A-2、13 号 V3.1 §1.1/§1.2
--
-- 内容：
--   ① config/file 两表 EnterpriseCode 存量清洗：NULL/'' → 'YZH-STD-ENT'（模板行标记常量）
--   ② 列改 varchar(36) NOT NULL DEFAULT ''（⛔ 禁 NULL 判定口径；'' 仅防默认值，业务上不允许残留）
--   ③ config uk 重建为四列 (OrgCode, EnterpriseCode, StandardCode, StageCode)（P16）：
--      - 先 DROP 旧 uk_enterprise_std_stage(三列) 与 uk_org_std_stage(三列)
--        （后者必须一并删除：企业行与模板行同 (Org,Std,Stage)，三列键会挡住多企业共存）
--      - ADD UNIQUE KEY uk_enterprise_std_stage(四列)（键名沿用 13 号 V3.1 §1.2）
--   ④ file 表无四列 uk（键仍 uk_cfg_fullpath(ConfigCode, FullPath)，企业行挂企业自有 ConfigCode，不冲突）
--
-- 幂等保护：DROP/ADD 前查 information_schema，重复执行不报错
-- 执行：docker exec -i yzh-mysql mysql -uroot -p'***' yzh_cert_platform < 本文件
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ------------------------------------------------------------
-- 0. 前置检查：新四列键的存量重复（含软删行，决策⑬口径）。
--    必须返回 0 行；返回任何行 = 存量重复，人工裁决保留行后再重跑本脚本。
-- ------------------------------------------------------------
SELECT OrgCode, EnterpriseCode, StandardCode, StageCode, COUNT(*) AS dup_cnt
FROM cert_standard_directory_config
GROUP BY OrgCode, COALESCE(NULLIF(EnterpriseCode, ''), 'YZH-STD-ENT'), StandardCode, StageCode
HAVING dup_cnt > 1;

-- ------------------------------------------------------------
-- 1. config：先 DROP 旧三列 uk（幂等）。
--    ⚠️ 必须在回填之前：实测两机构对同 (Std,Stage) 各有一行模板，
--    旧三列键 (EnterpriseCode,Std,Stage) 下回填常量会互撞；四列键（含 OrgCode）才不撞。
-- ------------------------------------------------------------
SET @s = (SELECT IF(EXISTS(
    SELECT 1 FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_standard_directory_config'
      AND INDEX_NAME = 'uk_enterprise_std_stage'),
    'ALTER TABLE cert_standard_directory_config DROP INDEX uk_enterprise_std_stage',
    'SELECT ''旧 uk_enterprise_std_stage 不存在，跳过'''));
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @s = (SELECT IF(EXISTS(
    SELECT 1 FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_standard_directory_config'
      AND INDEX_NAME = 'uk_org_std_stage'),
    'ALTER TABLE cert_standard_directory_config DROP INDEX uk_org_std_stage',
    'SELECT ''旧 uk_org_std_stage 不存在，跳过'''));
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 2. 存量清洗：config 模板行回填常量（实测 2026-09-28：5 行全 NULL）
UPDATE cert_standard_directory_config
SET EnterpriseCode = 'YZH-STD-ENT'
WHERE EnterpriseCode IS NULL OR EnterpriseCode = '';

-- 3. 存量清洗：file 模板行回填常量（实测 2026-09-28：168 行全 NULL）
UPDATE cert_standard_directory_file
SET EnterpriseCode = 'YZH-STD-ENT'
WHERE EnterpriseCode IS NULL OR EnterpriseCode = '';

-- 4. config：列改 NOT NULL DEFAULT ''（P13 禁 NULL）
ALTER TABLE cert_standard_directory_config
  MODIFY COLUMN EnterpriseCode varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
    NOT NULL DEFAULT '' COMMENT '企业Code：YZH-STD-ENT=机构模板行；真实值=该企业目录（⛔禁NULL，P13）';

-- 5. config：ADD 四列 uk（P16，幂等）
SET @s = (SELECT IF(NOT EXISTS(
    SELECT 1 FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_standard_directory_config'
      AND INDEX_NAME = 'uk_enterprise_std_stage'),
    'ALTER TABLE cert_standard_directory_config ADD UNIQUE KEY uk_enterprise_std_stage (OrgCode, EnterpriseCode, StandardCode, StageCode)',
    'SELECT ''四列 uk_enterprise_std_stage 已存在，跳过'''));
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 6. file：列改 NOT NULL DEFAULT ''（P13 禁 NULL；无 uk 变更）
ALTER TABLE cert_standard_directory_file
  MODIFY COLUMN EnterpriseCode varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
    NOT NULL DEFAULT '' COMMENT '企业Code：YZH-STD-ENT=模板文件定义行；真实值=该企业实际上传（⛔禁NULL，P13）';

-- ------------------------------------------------------------
-- 7. 验证 SQL（必须全 ok；任何 FAIL = 迁移未完成，禁止继续）
-- ------------------------------------------------------------
SELECT '1. config EnterpriseCode 非空且无 NULL/空串残留行' AS chk,
       IF(COUNT(*) = 0, 'ok', 'FAIL') AS result
FROM cert_standard_directory_config
WHERE EnterpriseCode IS NULL OR EnterpriseCode = '';

SELECT '2. file EnterpriseCode 非空且无 NULL/空串残留行' AS chk,
       IF(COUNT(*) = 0, 'ok', 'FAIL') AS result
FROM cert_standard_directory_file
WHERE EnterpriseCode IS NULL OR EnterpriseCode = '';

SELECT '3. 两表列定义 NOT NULL' AS chk,
       IF(SUM(IS_NULLABLE = 'NO') = 2, 'ok', 'FAIL') AS result
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND ((TABLE_NAME = 'cert_standard_directory_config' OR TABLE_NAME = 'cert_standard_directory_file')
    AND COLUMN_NAME = 'EnterpriseCode');

SELECT '4. config 四列 uk 列序 = OrgCode,EnterpriseCode,StandardCode,StageCode' AS chk,
       IF(GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) = 'OrgCode,EnterpriseCode,StandardCode,StageCode', 'ok', 'FAIL') AS result
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cert_standard_directory_config'
  AND INDEX_NAME = 'uk_enterprise_std_stage';

SELECT '5. 三列旧 uk_org_std_stage 已移除' AS chk,
       IF(COUNT(*) = 0, 'ok', 'FAIL') AS result
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cert_standard_directory_config'
  AND INDEX_NAME = 'uk_org_std_stage';

SELECT '6. 列排序规则 = utf8mb4_general_ci' AS chk,
       IF(SUM(COLLATION_NAME = 'utf8mb4_general_ci') = 2, 'ok', 'FAIL') AS result
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_standard_directory_config', 'cert_standard_directory_file')
  AND COLUMN_NAME = 'EnterpriseCode';

-- 铁律九 DB 验证口径：CONVERT+COLLATE 判大小写（information_schema 默认排序规则大小写不敏感，直接查会永远 0 行）
SELECT '7. 两表无 Enable/enable 列' AS chk,
       IF(COUNT(*) = 0, 'ok', 'FAIL') AS result
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_standard_directory_config', 'cert_standard_directory_file')
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin IN ('Enable', 'enable');

SELECT '8. 模板行数（回填后应 config=5 / file=168，与清洗前实测一致）' AS chk,
       CONCAT('config=', (SELECT COUNT(*) FROM cert_standard_directory_config WHERE EnterpriseCode = 'YZH-STD-ENT'),
              ' file=', (SELECT COUNT(*) FROM cert_standard_directory_file WHERE EnterpriseCode = 'YZH-STD-ENT')) AS result;
