-- =====================================================================
-- 恢复旧版标准目录管理+消息通知 相关数据库表
-- 从 git 68119f1^ 版本恢复
-- =====================================================================

-- 1. 标准目录配置表
CREATE TABLE IF NOT EXISTS `cert_standard_directory_config` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) NOT NULL,
  `DirectoryCode` varchar(100) NOT NULL,
  `StandardCode` varchar(50) NOT NULL,
  `PhaseCode` varchar(50) NOT NULL,
  `RootFolderName` varchar(200) DEFAULT NULL,
  `Status` enum('draft','active','archived') DEFAULT 'draft',
  `Enable` tinyint(1) DEFAULT 1,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(50) DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ModifyID` int DEFAULT NULL,
  `Modifier` varchar(50) DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `DeleteID` int DEFAULT NULL,
  `Deleter` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `Status_field` varchar(50) DEFAULT 'active',
  `Enable_field` tinyint(1) DEFAULT 1,
  `Sort` int DEFAULT 0,
  `Remark` text,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_directory_code` (`DirectoryCode`),
  UNIQUE KEY `uk_standard_phase` (`StandardCode`, `PhaseCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. 标准目录文件夹表
CREATE TABLE IF NOT EXISTS `cert_standard_directory_folder` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) NOT NULL,
  `FolderCode` varchar(150) NOT NULL,
  `DirectoryCode` varchar(100) NOT NULL,
  `ParentCode` varchar(150) DEFAULT NULL,
  `FolderName` varchar(200) NOT NULL,
  `Depth` int DEFAULT 1,
  `SortOrder` int DEFAULT 0,
  `Status` enum('draft','active','archived') DEFAULT 'draft',
  `Enable` tinyint(1) DEFAULT 1,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(50) DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ModifyID` int DEFAULT NULL,
  `Modifier` varchar(50) DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `DeleteID` int DEFAULT NULL,
  `Deleter` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `Status_field` varchar(50) DEFAULT 'active',
  `Enable_field` tinyint(1) DEFAULT 1,
  `Sort` int DEFAULT 0,
  `Remark` text,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_folder_code` (`FolderCode`),
  KEY `idx_directory_code` (`DirectoryCode`),
  KEY `idx_parent_code` (`ParentCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3. 标准目录文件表
CREATE TABLE IF NOT EXISTS `cert_standard_directory_file` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) NOT NULL,
  `FileCode` varchar(150) NOT NULL,
  `FolderCode` varchar(150) NOT NULL,
  `DirectoryCode` varchar(100) NOT NULL,
  `FileName` varchar(500) NOT NULL,
  `FileType` varchar(50) DEFAULT NULL,
  `FilePattern` varchar(200) DEFAULT NULL,
  `IsRequired` tinyint(1) DEFAULT 1,
  `MaxFileSizeMB` int DEFAULT 10,
  `Description` text,
  `SortOrder` int DEFAULT 0,
  `ExtractionEnabled` tinyint(1) DEFAULT 0,
  `ExtractionRules` json DEFAULT NULL,
  `PreCheckRequired` tinyint(1) DEFAULT 1,
  `ComplianceRequired` tinyint(1) DEFAULT 0,
  `Status` enum('draft','active','archived') DEFAULT 'draft',
  `Enable` tinyint(1) DEFAULT 1,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(50) DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ModifyID` int DEFAULT NULL,
  `Modifier` varchar(50) DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `DeleteID` int DEFAULT NULL,
  `Deleter` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `Status_field` varchar(50) DEFAULT 'active',
  `Enable_field` tinyint(1) DEFAULT 1,
  `Sort` int DEFAULT 0,
  `Remark` text,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_file_code` (`FileCode`),
  KEY `idx_folder_code` (`FolderCode`),
  KEY `idx_directory_code` (`DirectoryCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 4. 消息表
CREATE TABLE IF NOT EXISTS `cert_message` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Title` varchar(200) NOT NULL,
  `Content` text,
  `MessageType` varchar(50) DEFAULT 'system',
  `IsRead` tinyint(1) DEFAULT 0,
  `UserId` int NOT NULL,
  `RelatedCode` varchar(100) DEFAULT NULL,
  `ExtraData` json DEFAULT NULL,
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(50) DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ModifyID` int DEFAULT NULL,
  `Modifier` varchar(50) DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `DeleteID` int DEFAULT NULL,
  `Deleter` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `Enable` tinyint(1) DEFAULT 1,
  `Status` varchar(50) DEFAULT 'active',
  `Remark` varchar(500) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `idx_user_id` (`UserId`),
  KEY `idx_is_read` (`IsRead`),
  KEY `idx_message_type` (`MessageType`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 5. 认证阶段表
CREATE TABLE IF NOT EXISTS `cert_cert_stage` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) NOT NULL,
  `PhaseCode` varchar(50) NOT NULL,
  `PhaseName` varchar(100) NOT NULL,
  `Description` text,
  `SortOrder` int DEFAULT 0,
  `Enable` tinyint(1) DEFAULT 1,
  `Status` varchar(50) DEFAULT 'active',
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(50) DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ModifyID` int DEFAULT NULL,
  `Modifier` varchar(50) DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `DeleteID` int DEFAULT NULL,
  `Deleter` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `Remark` varchar(500) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_phase_code` (`PhaseCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 6. 机构-标准关联表
CREATE TABLE IF NOT EXISTS `cert_org_standard` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) NOT NULL,
  `OrgCode` varchar(50) NOT NULL,
  `StandardCode` varchar(50) NOT NULL,
  `Enable` tinyint(1) DEFAULT 1,
  `Status` varchar(50) DEFAULT 'active',
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(50) DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ModifyID` int DEFAULT NULL,
  `Modifier` varchar(50) DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `DeleteID` int DEFAULT NULL,
  `Deleter` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `Remark` varchar(500) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_org_code` (`OrgCode`),
  KEY `idx_standard_code` (`StandardCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 7. 机构-阶段关联表
CREATE TABLE IF NOT EXISTS `cert_org_stage` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) NOT NULL,
  `OrgCode` varchar(50) NOT NULL,
  `StandardCode` varchar(50) NOT NULL,
  `PhaseCode` varchar(50) NOT NULL,
  `Enable` tinyint(1) DEFAULT 1,
  `Status` varchar(50) DEFAULT 'active',
  `CreateID` int DEFAULT NULL,
  `Creator` varchar(50) DEFAULT NULL,
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ModifyID` int DEFAULT NULL,
  `Modifier` varchar(50) DEFAULT NULL,
  `ModifyDate` datetime DEFAULT NULL,
  `DeleteID` int DEFAULT NULL,
  `Deleter` varchar(50) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `Remark` varchar(500) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_org_code` (`OrgCode`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_phase_code` (`PhaseCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 验证
SELECT '=== 恢复结果 ===' AS info;
SELECT 'cert_standard_directory_config' AS tbl, COUNT(*) AS cnt FROM cert_standard_directory_config
UNION ALL SELECT 'cert_standard_directory_folder', COUNT(*) FROM cert_standard_directory_folder
UNION ALL SELECT 'cert_standard_directory_file', COUNT(*) FROM cert_standard_directory_file
UNION ALL SELECT 'cert_message', COUNT(*) FROM cert_message
UNION ALL SELECT 'cert_cert_stage', COUNT(*) FROM cert_cert_stage
UNION ALL SELECT 'cert_org_standard', COUNT(*) FROM cert_org_standard
UNION ALL SELECT 'cert_org_stage', COUNT(*) FROM cert_org_stage;
