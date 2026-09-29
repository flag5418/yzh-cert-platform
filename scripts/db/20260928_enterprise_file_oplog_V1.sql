-- ============================================================
-- G-0c：企业资料管理底座迁移② —— 操作留痕表 cert_enterprise_file_op_log（V-P4）
-- 日期：2026-09-28
-- 权威依据：06 册 02 号 §六 DDL（只追加审计表，模式续 07 号册）
-- 说明：Q1 裁决后无虚拟企业种子行 INSERT（03 号计划 G-0c）
-- 铁律：utf8mb4 + 显式 COLLATE=utf8mb4_general_ci；无 Enable 列；PascalCase 列名
-- ★ 对 02 号 §六 DDL 的两处底座修正（文档已同步）：
--   ① BaseEntity 强制列 UpdateTime/UpdateBy 必须存在（框架 INSERT 映射列，缺列 = 运行时报 Unknown column）；
--   ② 同期补漏：cert_enterprise_file_version（20260927 建）同样缺这两列，本脚本一并 ALTER。
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `cert_enterprise_file_op_log` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) NOT NULL COMMENT '业务键GUID',
  `CreateTime` datetime NOT NULL,
  `CreateBy` varchar(64) DEFAULT NULL COMMENT '操作人Code',
  `UpdateTime` datetime DEFAULT NULL COMMENT '更新时间（BaseEntity 强制列）',
  `UpdateBy` varchar(64) DEFAULT NULL COMMENT '更新人Code（BaseEntity 强制列）',
  `EnterpriseCode` varchar(36) NOT NULL COMMENT '企业Code → cert_enterprise.Code（真实企业）',
  `StageCode` varchar(36) DEFAULT NULL COMMENT '阶段 Code → cert_cert_stage.Code',
  `FileCode` varchar(36) NOT NULL COMMENT '→ cert_standard_directory_file.Code（企业行）',
  `OpType` varchar(20) NOT NULL COMMENT 'upload/replace/delete/restore/extract_trigger/extract_done',
  `VersionNumber` int DEFAULT NULL COMMENT '本次操作产生的版本号（无关操作为NULL）',
  `Detail` varchar(1000) DEFAULT NULL COMMENT 'JSON：文件名/大小/映射来源/失败原因/Reason双写等',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_enterprise_time` (`EnterpriseCode`,`CreateTime`),
  KEY `idx_file` (`FileCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业文件操作日志（只追加）';

-- ------------------------------------------------------------
-- 补漏：cert_enterprise_file_version 缺 BaseEntity 强制列（幂等 ALTER）
-- ------------------------------------------------------------
SET @s = (SELECT IF(NOT EXISTS(
    SELECT 1 FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_enterprise_file_version'
      AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'UpdateTime'),
    'ALTER TABLE cert_enterprise_file_version ADD COLUMN UpdateTime datetime DEFAULT NULL COMMENT ''更新时间（BaseEntity 强制列）'' AFTER CreateBy,
     ADD COLUMN UpdateBy varchar(64) DEFAULT NULL COMMENT ''更新人Code（BaseEntity 强制列）'' AFTER UpdateTime',
    'SELECT ''version 表 UpdateTime/UpdateBy 已存在，跳过'''));
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- （幂等：若 op_log 表由旧版 DDL 先建出，同样补 BaseEntity 强制列）
SET @s = (SELECT IF(NOT EXISTS(
    SELECT 1 FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_enterprise_file_op_log'
      AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'UpdateTime'),
    'ALTER TABLE cert_enterprise_file_op_log ADD COLUMN UpdateTime datetime DEFAULT NULL COMMENT ''更新时间（BaseEntity 强制列）'' AFTER CreateBy,
     ADD COLUMN UpdateBy varchar(64) DEFAULT NULL COMMENT ''更新人Code（BaseEntity 强制列）'' AFTER UpdateTime',
    'SELECT ''op_log 表 UpdateTime/UpdateBy 已存在，跳过'''));
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ------------------------------------------------------------
-- 验证 SQL
-- ------------------------------------------------------------
SELECT '1. op_log 表存在' AS chk,
       IF(COUNT(*) = 1, 'ok', 'FAIL') AS result
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_enterprise_file_op_log';

SELECT '2. 表 collation = utf8mb4_general_ci' AS chk,
       IF(TABLE_COLLATION = 'utf8mb4_general_ci', 'ok', 'FAIL') AS result
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_enterprise_file_op_log';

-- 铁律九口径：CONVERT+COLLATE 判大小写
SELECT '3. 无 Enable/enable 列' AS chk,
       IF(COUNT(*) = 0, 'ok', 'FAIL') AS result
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cert_enterprise_file_op_log'
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin IN ('Enable', 'enable');

SELECT '4. op_log/version 两表 BaseEntity 强制列齐备' AS chk,
       IF(COUNT(DISTINCT CONCAT(TABLE_NAME, '.', COLUMN_NAME)) = 4, 'ok', 'FAIL') AS result
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_enterprise_file_op_log', 'cert_enterprise_file_version')
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin IN ('UpdateTime', 'UpdateBy');
