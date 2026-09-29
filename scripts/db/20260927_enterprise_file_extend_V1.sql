-- 企业资料管理 — DB 变更脚本 V1
-- 日期：2026-09-27
-- 说明：在现有标准目录表上加 EnterpriseCode 列，区分模板行与企业上传行；新建企业文件历史版本表
-- 铁律：utf8mb4 + 显式 COLLATE=utf8mb4_general_ci；无 Enable 列；PascalCase 列名

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ============================================================
-- 脚本1：cert_standard_directory_config 加 EnterpriseCode 列
-- ============================================================
ALTER TABLE cert_standard_directory_config
  ADD COLUMN EnterpriseCode varchar(36) DEFAULT NULL COMMENT '企业Code：NULL或YZH-STD-ENT=机构模板；真实值=该企业上传目录'
    AFTER OrgCode,
  ADD UNIQUE KEY uk_enterprise_std_stage (EnterpriseCode, StandardCode, StageCode);

-- ============================================================
-- 脚本2：cert_standard_directory_file 加企业侧字段
-- ============================================================
ALTER TABLE cert_standard_directory_file
  ADD COLUMN EnterpriseCode varchar(36) DEFAULT NULL COMMENT '企业Code：NULL或YZH-STD-ENT=模板定义；真实值=该企业实际上传'
    AFTER ConfigCode,
  ADD COLUMN ExtractStatus varchar(20) NOT NULL DEFAULT 'none' COMMENT '提取状态：none/pending/processing/completed/failed'
    AFTER ConvertStatus,
  ADD COLUMN ExtractMessage varchar(1024) DEFAULT NULL COMMENT '提取失败原因'
    AFTER ExtractStatus,
  ADD COLUMN MaxConfidence decimal(3,2) DEFAULT NULL COMMENT '最高提取置信度'
    AFTER ExtractMessage,
  ADD KEY idx_enterprise_file (EnterpriseCode, StandardCode, StageCode),
  ADD KEY idx_extract_status (ExtractStatus);

-- ============================================================
-- 脚本3：新建 cert_enterprise_file_version 历史版本表
-- ============================================================
CREATE TABLE IF NOT EXISTS `cert_enterprise_file_version` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) NOT NULL COMMENT '业务键GUID',
  `CreateTime` datetime NOT NULL COMMENT '创建时间',
  `CreateBy` varchar(64) DEFAULT NULL COMMENT '创建人Code',
  `FileCode` varchar(36) NOT NULL COMMENT '父文件Code → cert_standard_directory_file.Code（企业上传行）',
  `EnterpriseCode` varchar(36) NOT NULL COMMENT '企业Code（冗余，方便查询）',
  `VersionNumber` int NOT NULL DEFAULT '1' COMMENT '版本号（从1开始递增）',
  `FileName` varchar(500) NOT NULL COMMENT '该版本的文件名',
  `StoragePath` varchar(512) NOT NULL COMMENT '归档后的MinIO路径（History/xxx.doc.vN）',
  `FileSize` bigint NOT NULL DEFAULT '0' COMMENT '该版本文件大小',
  `Reason` varchar(500) DEFAULT NULL COMMENT '替换原因',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_file` (`FileCode`),
  KEY `idx_version` (`FileCode`,`VersionNumber`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业文件历史版本';

-- ============================================================
-- 脚本4：验证 SQL（防假阴性口径）
-- ============================================================
-- ① 新列存在
SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('cert_standard_directory_config', 'cert_standard_directory_file')
  AND COLUMN_NAME IN ('EnterpriseCode', 'ExtractStatus', 'ExtractMessage', 'MaxConfidence')
ORDER BY TABLE_NAME, COLUMN_NAME;

-- ② 新表存在
SELECT TABLE_NAME FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_enterprise_file_version';

-- ③ 新表无 Enable 列
SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'cert_enterprise_file_version'
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin RLIKE '^(Enable|enable)$';
-- 期望：0 行

-- ④ 新表字符集正确
SELECT TABLE_NAME, TABLE_COLLATION FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_enterprise_file_version';
-- 期望：utf8mb4_general_ci
