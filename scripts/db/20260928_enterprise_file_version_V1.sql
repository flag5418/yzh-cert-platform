-- ============================================================
-- G-0b 补：cert_standard_directory_file 加当前版本号列 VersionNumber
-- 日期：2026-09-28
-- 依据：06 册 02 号 §二 版本号口径第 3 条——"当前行 VersionNumber 必须显式落库"
--       是 G-1b（confirm 补写 =1）与 B-08 四元组 (FileCode, VersionNumber) 对账的 DB 前提；
--       实测列不存在，计划 G-0/G-1 均未显式安排建列，本脚本收口该缺口。
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 幂等：已存在则跳过
SET @s = (SELECT IF(NOT EXISTS(
    SELECT 1 FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_standard_directory_file'
      AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'VersionNumber'),
    'ALTER TABLE cert_standard_directory_file ADD COLUMN VersionNumber int NOT NULL DEFAULT 1 COMMENT ''当前内容版本号：槽位链从1起（企业行）；模板行恒1；替换在G-3时序中递增'' AFTER FileSize',
    'SELECT ''VersionNumber 列已存在，跳过'''));
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 验证
SELECT '1. VersionNumber 列存在且 NOT NULL' AS chk,
       IF(COUNT(*) = 1, 'ok', 'FAIL') AS result
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cert_standard_directory_file'
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'VersionNumber'
  AND IS_NULLABLE = 'NO';
