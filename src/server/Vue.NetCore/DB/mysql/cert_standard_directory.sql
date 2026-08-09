-- ============================================================
-- 标准目录结构管理 - 数据库表创建脚本
-- 创建时间：2026-08-08
-- ============================================================

-- ============================================================
-- 表1：标准目录配置表 (cert_standard_directory_config)
-- 作用：定义标准目录结构（机构无关）
-- ============================================================

DROP TABLE IF EXISTS `cert_standard_directory_file`;
DROP TABLE IF EXISTS `cert_standard_directory_folder`;
DROP TABLE IF EXISTS `cert_standard_directory_config`;

CREATE TABLE `cert_standard_directory_config` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  
  -- 编码字段
  `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
  `DirectoryCode` varchar(100) NOT NULL COMMENT '目录编码（SDC-{标准}|{阶段}）',
  
  -- 关联字段
  `StandardCode` varchar(50) NOT NULL COMMENT '标准编码',
  `PhaseCode` varchar(50) NOT NULL COMMENT '阶段编码',
  
  -- 目录配置
  `RootFolderName` varchar(200) DEFAULT NULL COMMENT '根文件夹名称',
  
  -- 状态
  `Status` enum('draft','active','archived') DEFAULT 'draft' COMMENT '状态',
  `Enable` tinyint(1) DEFAULT 1 COMMENT '是否启用',
  
  -- 审计字段
  `CreateID` int DEFAULT NULL COMMENT '创建人ID',
  `Creator` varchar(50) DEFAULT NULL COMMENT '创建人姓名',
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
  `Modifier` varchar(50) DEFAULT NULL COMMENT '修改人姓名',
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
  `Deleter` varchar(50) DEFAULT NULL COMMENT '删除人姓名',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `Status_field` varchar(50) DEFAULT 'active' COMMENT '业务状态',
  `Enable_field` tinyint(1) DEFAULT 1 COMMENT '启用状态',
  `Sort` int DEFAULT 0 COMMENT '排序',
  `Remark` text COMMENT '备注',
  
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_directory_code` (`DirectoryCode`),
  UNIQUE KEY `uk_standard_phase` (`StandardCode`, `PhaseCode`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_phase_code` (`PhaseCode`),
  KEY `idx_status` (`Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='标准目录配置表';

-- ============================================================
-- 表2：标准目录文件夹表 (cert_standard_directory_folder)
-- 作用：定义标准目录的文件夹结构
-- ============================================================

CREATE TABLE `cert_standard_directory_folder` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  
  -- 编码字段
  `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
  `FolderCode` varchar(150) NOT NULL COMMENT '文件夹编码（FD-{DirectoryCode}|L{Level}|S{Seq}）',
  
  -- 关联字段
  `DirectoryCode` varchar(100) NOT NULL COMMENT '目录编码',
  `ParentCode` varchar(150) DEFAULT NULL COMMENT '父文件夹编码',
  
  -- 文件夹信息
  `FolderName` varchar(200) NOT NULL COMMENT '文件夹名称',
  `Depth` int DEFAULT 1 COMMENT '层级深度',
  `SortOrder` int DEFAULT 0 COMMENT '排序',
  
  -- 状态
  `Status` enum('draft','active','archived') DEFAULT 'draft' COMMENT '状态',
  `Enable` tinyint(1) DEFAULT 1 COMMENT '是否启用',
  
  -- 审计字段
  `CreateID` int DEFAULT NULL COMMENT '创建人ID',
  `Creator` varchar(50) DEFAULT NULL COMMENT '创建人姓名',
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
  `Modifier` varchar(50) DEFAULT NULL COMMENT '修改人姓名',
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
  `Deleter` varchar(50) DEFAULT NULL COMMENT '删除人姓名',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `Status_field` varchar(50) DEFAULT 'active' COMMENT '业务状态',
  `Enable_field` tinyint(1) DEFAULT 1 COMMENT '启用状态',
  `Sort` int DEFAULT 0 COMMENT '排序',
  `Remark` text COMMENT '备注',
  
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_folder_code` (`FolderCode`),
  KEY `idx_directory_code` (`DirectoryCode`),
  KEY `idx_parent_code` (`ParentCode`),
  KEY `idx_status` (`Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='标准目录文件夹表';

-- ============================================================
-- 表3：标准目录文件表 (cert_standard_directory_file)
-- 作用：定义标准目录中每个文件夹要求的文件规格
-- ============================================================

CREATE TABLE `cert_standard_directory_file` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  
  -- 编码字段
  `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',
  `FileCode` varchar(150) NOT NULL COMMENT '文件编码（FL-{FolderCode}|{FileName}|{Type}）',
  
  -- 关联字段
  `FolderCode` varchar(150) NOT NULL COMMENT '所属文件夹编码',
  `DirectoryCode` varchar(100) NOT NULL COMMENT '目录编码',
  
  -- 文件信息
  `FileName` varchar(500) NOT NULL COMMENT '文件名称模板',
  `FileType` varchar(50) DEFAULT NULL COMMENT '文件类型（pdf/docx/xlsx/png等）',
  `FilePattern` varchar(200) DEFAULT NULL COMMENT '文件名正则匹配规则',
  
  -- 文件要求
  `IsRequired` tinyint(1) DEFAULT 1 COMMENT '是否必须提供',
  `MaxFileSizeMB` int DEFAULT 10 COMMENT '最大文件大小（MB）',
  `Description` text COMMENT '文件说明/要求描述',
  `SortOrder` int DEFAULT 0 COMMENT '排序',
  
  -- 提取规则
  `ExtractionEnabled` tinyint(1) DEFAULT 0 COMMENT '是否启用自动提取',
  `ExtractionRules` json DEFAULT NULL COMMENT '提取规则配置',
  
  -- 校验规则
  `PreCheckRequired` tinyint(1) DEFAULT 1 COMMENT '是否要求预审',
  `ComplianceRequired` tinyint(1) DEFAULT 0 COMMENT '是否要求合规检查',
  
  -- 状态
  `Status` enum('draft','active','archived') DEFAULT 'draft' COMMENT '状态',
  `Enable` tinyint(1) DEFAULT 1 COMMENT '是否启用',
  
  -- 审计字段
  `CreateID` int DEFAULT NULL COMMENT '创建人ID',
  `Creator` varchar(50) DEFAULT NULL COMMENT '创建人姓名',
  `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
  `Modifier` varchar(50) DEFAULT NULL COMMENT '修改人姓名',
  `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
  `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
  `Deleter` varchar(50) DEFAULT NULL COMMENT '删除人姓名',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `Status_field` varchar(50) DEFAULT 'active' COMMENT '业务状态',
  `Enable_field` tinyint(1) DEFAULT 1 COMMENT '启用状态',
  `Sort` int DEFAULT 0 COMMENT '排序',
  `Remark` text COMMENT '备注',
  
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_file_code` (`FileCode`),
  KEY `idx_folder_code` (`FolderCode`),
  KEY `idx_directory_code` (`DirectoryCode`),
  KEY `idx_status` (`Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='标准目录文件表';

-- ============================================================
-- 验证查询
-- ============================================================

SELECT '✅ 标准目录配置表创建完成' AS Result;
SELECT '✅ 标准目录文件夹表创建完成' AS Result;
SELECT '✅ 标准目录文件表创建完成' AS Result;

SELECT 
  (SELECT COUNT(*) FROM cert_standard_directory_config) AS config_count,
  (SELECT COUNT(*) FROM cert_standard_directory_folder) AS folder_count,
  (SELECT COUNT(*) FROM cert_standard_directory_file) AS file_count;
