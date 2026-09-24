-- MySQL dump 10.13  Distrib 8.0.46, for Linux (x86_64)
--
-- Host: localhost    Database: yzh_cert_platform
-- ------------------------------------------------------
-- Server version	8.0.46
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `_bak_20260924_cert_standard_directory_config`
--

DROP TABLE IF EXISTS `_bak_20260924_cert_standard_directory_config`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `_bak_20260924_cert_standard_directory_config` (
  `Id` bigint NOT NULL DEFAULT '0',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `DirectoryCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `StandardCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `PhaseCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `RootFolderName` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Status` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'none',
  `IsValid` tinyint(1) DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `StatusField` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `Enable` tinyint(1) NOT NULL DEFAULT '1',
  `EnableField` tinyint(1) DEFAULT '1',
  `Sort` int DEFAULT '0',
  `Remark` text COLLATE utf8mb4_general_ci
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `_bak_20260924_cert_standard_directory_file`
--

DROP TABLE IF EXISTS `_bak_20260924_cert_standard_directory_file`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `_bak_20260924_cert_standard_directory_file` (
  `Id` bigint NOT NULL DEFAULT '0',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `FileCode` varchar(150) COLLATE utf8mb4_general_ci NOT NULL,
  `FolderCode` varchar(150) COLLATE utf8mb4_general_ci NOT NULL,
  `DirectoryCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `FileName` varchar(500) COLLATE utf8mb4_general_ci NOT NULL,
  `FileType` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `FileSize` bigint DEFAULT NULL COMMENT '文件大小(字节)',
  `FilePattern` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsRequired` tinyint(1) DEFAULT '1',
  `MaxFileSizeMB` int DEFAULT '10',
  `Description` text COLLATE utf8mb4_general_ci,
  `SortOrder` int DEFAULT '0',
  `ExtractionEnabled` tinyint(1) DEFAULT '0',
  `ExtractionRules` json DEFAULT NULL,
  `PreCheckRequired` tinyint(1) DEFAULT '1',
  `ComplianceRequired` tinyint(1) DEFAULT '0',
  `Status` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `Enable` tinyint(1) NOT NULL DEFAULT '1',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `StatusField` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `EnableField` tinyint(1) DEFAULT '1',
  `Sort` int DEFAULT '0',
  `Remark` text COLLATE utf8mb4_general_ci,
  `TaskId` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint(1) DEFAULT '1',
  `UploadStatus` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `StoragePath` varchar(512) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `FullPath` varchar(1024) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ConvertedStoragePath` varchar(512) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ConvertStatus` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ConvertMessage` varchar(1024) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ConvertDate` datetime DEFAULT NULL,
  `PreviewPdfPath` varchar(512) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '预览PDF产物路径（LibreOffice转换）',
  `MarkdownPath` varchar(512) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '提取Markdown产物路径（anydoc转换）',
  `MarkdownStatus` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'none' COMMENT 'Markdown转换状态：none/pending/converting/completed/failed',
  `MarkdownMessage` varchar(1024) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT 'Markdown转换失败原因',
  `MarkdownDate` datetime DEFAULT NULL COMMENT 'Markdown转换完成时间'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `_bak_20260924_cert_standard_directory_folder`
--

DROP TABLE IF EXISTS `_bak_20260924_cert_standard_directory_folder`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `_bak_20260924_cert_standard_directory_folder` (
  `Id` bigint NOT NULL DEFAULT '0',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `FolderCode` varchar(150) COLLATE utf8mb4_general_ci NOT NULL,
  `DirectoryCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `ParentCode` varchar(150) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `FolderName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL,
  `Depth` int DEFAULT '1',
  `SortOrder` int DEFAULT '0',
  `Status` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `Enable` tinyint(1) NOT NULL DEFAULT '1',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `StatusField` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `EnableField` tinyint(1) DEFAULT '1',
  `Sort` int DEFAULT '0',
  `Remark` text COLLATE utf8mb4_general_ci,
  `TaskId` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint(1) DEFAULT '0',
  `FullPath` varchar(1024) COLLATE utf8mb4_general_ci DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `_bak_20260924_rpt_report_section`
--

DROP TABLE IF EXISTS `_bak_20260924_rpt_report_section`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `_bak_20260924_rpt_report_section` (
  `Id` bigint NOT NULL DEFAULT '0' COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `ReportCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属报告编码',
  `ClauseCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '对应条款编码（可空，概述/结论章节不映射条款）',
  `SectionName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '章节名称',
  `SectionNameEn` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '英文名称',
  `SectionContent` text COLLATE utf8mb4_general_ci COMMENT '章节填充内容',
  `WorkflowCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '生成此章节的工作流编码',
  `WorkflowConfig` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '工作流DAG JSON',
  `LayoutJson` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '工作流布局JSON',
  `SectionJson` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '章节配置JSON',
  `SortOrder` int DEFAULT '0' COMMENT '章节排序',
  `IsActive` tinyint(1) NOT NULL DEFAULT '1' COMMENT '是否启用',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `_bak_20260924_sys_dictionary`
--

DROP TABLE IF EXISTS `_bak_20260924_sys_dictionary`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `_bak_20260924_sys_dictionary` (
  `Id` int NOT NULL DEFAULT '0',
  `Code` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '稳定标识（随机唯一，关联键）',
  `Config` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DBServer` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `DbSql` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `DicName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `DicNo` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '字典编码（可选，仅用于显示）',
  `Enable` tinyint NOT NULL,
  `IsValid` tinyint NOT NULL DEFAULT '1' COMMENT '有效标志（1=有效，0=无效）',
  `IsDeleted` tinyint NOT NULL DEFAULT '0' COMMENT '删除标志（1=已删除）',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人',
  `UpdateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `ParentCode` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '父节点 Code（根节点为 NULL）',
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `_bak_20260924_sys_dictionarylist`
--

DROP TABLE IF EXISTS `_bak_20260924_sys_dictionarylist`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `_bak_20260924_sys_dictionarylist` (
  `Id` int NOT NULL DEFAULT '0',
  `Code` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '稳定标识（随机唯一，定位键）',
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DicName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DicValue` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DicCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属字典 Code（= Sys_Dictionary.Code）',
  `Enable` tinyint DEFAULT NULL,
  `IsValid` tinyint NOT NULL DEFAULT '1' COMMENT '有效标志（1=有效，0=无效）',
  `IsDeleted` tinyint NOT NULL DEFAULT '0' COMMENT '删除标志（1=已删除）',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人',
  `UpdateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `Color` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `_bak_20260924_sys_menu`
--

DROP TABLE IF EXISTS `_bak_20260924_sys_menu`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `_bak_20260924_sys_menu` (
  `Id` int NOT NULL DEFAULT '0',
  `Code` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务唯一编码',
  `ParentCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL DEFAULT '0',
  `MenuName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `Auth` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `Icon` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Description` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` tinyint DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `Url` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateTime` datetime DEFAULT NULL COMMENT '创建时间',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '创建人',
  `UpdateTime` datetime DEFAULT NULL COMMENT '修改时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '修改人',
  `Tag` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '菜单分类标签：admin/auditor/enterprise/common',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0' COMMENT '软删除标记',
  `IsValid` int NOT NULL DEFAULT '1' COMMENT '启用/禁用'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `_bak_20260924_sys_organization`
--

DROP TABLE IF EXISTS `_bak_20260924_sys_organization`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `_bak_20260924_sys_organization` (
  `Id` int NOT NULL DEFAULT '0',
  `Code` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `OrgName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL,
  `OrgCode` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ParentCode` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `OrgType` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'Dept',
  `OrgLevel` int DEFAULT NULL,
  `OrgPath` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `LeaderName` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `LeaderPhone` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Sort` int DEFAULT '0',
  `Enable` tinyint DEFAULT '1',
  `IsValid` tinyint NOT NULL DEFAULT '1' COMMENT '有效标志: 1=有效, 0=无效',
  `Remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint DEFAULT '0'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `_bak_20260924_sys_role`
--

DROP TABLE IF EXISTS `_bak_20260924_sys_role`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `_bak_20260924_sys_role` (
  `Id` int NOT NULL DEFAULT '0',
  `Code` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务唯一编码',
  `ParentCode` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '父节点编码（树结构，根节点为 NULL）',
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `DeptName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeptId` int DEFAULT NULL,
  `Enable` tinyint DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `ParentId` int NOT NULL,
  `RoleName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0' COMMENT '软删除标记(框架标准列)',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '有效标志(框架标准列)'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `_bak_20260924_sys_user`
--

DROP TABLE IF EXISTS `_bak_20260924_sys_user`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `_bak_20260924_sys_user` (
  `Id` int NOT NULL DEFAULT '0',
  `Code` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务唯一编码',
  `RoleId` int NOT NULL DEFAULT '0',
  `RoleName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `PhoneNo` varchar(11) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Remark` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Tel` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UserName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `UserPwd` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UserTrueName` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `DeptName` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeptId` int DEFAULT NULL,
  `Email` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` tinyint NOT NULL,
  `IsValid` tinyint NOT NULL DEFAULT '1' COMMENT '有效标志: 1=有效, 0=无效',
  `IsDeleted` tinyint NOT NULL DEFAULT '0' COMMENT '软删除标记',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `DeleteBy` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人Code',
  `UserType` tinyint NOT NULL DEFAULT '10' COMMENT '用户类型：1=超级管理员, 10=总管理员, 13=运维人员, 14=配置人员, 15=质量专员, 20=审核管理员, 21=审核组长, 22=普通审核员, 30=企业账号',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '机构编码（多租户隔离），NULL表示平台管理层',
  `OrgId` bigint DEFAULT NULL COMMENT '机构ID，关联cert_org_config.id',
  `ParentUserId` int DEFAULT NULL COMMENT '上级用户ID，用于企业子账号或审核员层级',
  `Gender` int DEFAULT NULL,
  `HeadImageUrl` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `IsRegregisterPhone` int DEFAULT NULL,
  `LastLoginDate` datetime DEFAULT NULL,
  `LastModifyPwdDate` datetime DEFAULT NULL,
  `Address` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `AppType` int DEFAULT NULL,
  `AuditDate` datetime DEFAULT NULL,
  `AuditStatus` int DEFAULT NULL,
  `Auditor` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `Token` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Mobile` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeptIds` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `WechatOpenid` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `WechatUnionid` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `_bak_20260924_wf_prompt_template`
--

DROP TABLE IF EXISTS `_bak_20260924_wf_prompt_template`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `_bak_20260924_wf_prompt_template` (
  `id` bigint NOT NULL DEFAULT '0' COMMENT '主键',
  `code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '多租户组织编码',
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active' COMMENT '实体启用状态',
  `enable` tinyint(1) DEFAULT '1' COMMENT '实体启用标记',
  `sort` int DEFAULT '0' COMMENT '排序',
  `remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `PromptCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '提示词编码（如 analyze_word_v1）',
  `PromptName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '提示词名称',
  `PromptType` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '类型：analyze/extract/verify/validate/report',
  `SkillTarget` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '适用技能：word/excel/pdf/all',
  `template` mediumtext COLLATE utf8mb4_general_ci COMMENT '提示词模板（支持占位符）',
  `description` text COLLATE utf8mb4_general_ci COMMENT '说明',
  `version` int NOT NULL DEFAULT '1' COMMENT '版本号',
  `IsActive` tinyint(1) NOT NULL DEFAULT '1' COMMENT '是否当前生效',
  `LastTestResult` text COLLATE utf8mb4_general_ci COMMENT '最后测试结果（JSON）',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `_bak_20260924_wf_workflow_definition`
--

DROP TABLE IF EXISTS `_bak_20260924_wf_workflow_definition`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `_bak_20260924_wf_workflow_definition` (
  `Id` bigint NOT NULL DEFAULT '0' COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateTime` datetime DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `Status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `WorkflowCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '工作流编码',
  `WorkflowName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '工作流名称',
  `WorkflowType` enum('extraction','validation','report') COLLATE utf8mb4_general_ci NOT NULL COMMENT '工作流类型',
  `WorkflowConfig` json NOT NULL COMMENT '工作流DAG配置（节点+边+参数）',
  `Version` int DEFAULT '1' COMMENT '版本号',
  `IsActive` tinyint(1) DEFAULT '1' COMMENT '是否启用',
  `Description` text COLLATE utf8mb4_general_ci COMMENT '说明',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `audit_checklist_item`
--

DROP TABLE IF EXISTS `audit_checklist_item`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `audit_checklist_item` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `TaskCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属审核任务编码',
  `ClauseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '对应条款编码',
  `AuditCriteria` text COLLATE utf8mb4_general_ci COMMENT '审核准则（标准条款原文）',
  `FindingDescription` text COLLATE utf8mb4_general_ci COMMENT '审核发现描述',
  `Conformity` enum('pending','conform','nonconform','observation','na') COLLATE utf8mb4_general_ci DEFAULT 'pending' COMMENT '判定结果',
  `NcsFound` int DEFAULT '0' COMMENT '发现NC数量',
  `CheckedBy` bigint DEFAULT NULL COMMENT '检查人ID',
  `CheckedAt` datetime DEFAULT NULL COMMENT '检查时间',
  `SortOrder` int DEFAULT '0' COMMENT '排序',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_task_code` (`TaskCode`),
  KEY `idx_clause_code` (`ClauseCode`),
  KEY `idx_conformity` (`Conformity`),
  CONSTRAINT `fk_checklist_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`),
  CONSTRAINT `fk_checklist_task` FOREIGN KEY (`TaskCode`) REFERENCES `audit_task` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='检查表条目';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `audit_evidence`
--

DROP TABLE IF EXISTS `audit_evidence`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `audit_evidence` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `TaskCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属审核任务编码',
  `ClauseCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '关联条款编码',
  `EvidenceType` enum('photo','audio','screenshot','video','document','other') COLLATE utf8mb4_general_ci NOT NULL COMMENT '证据类型',
  `StoragePath` varchar(500) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'MinIO存储路径',
  `FileHash` varchar(64) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'SHA256哈希',
  `IsVoided` tinyint(1) DEFAULT '0' COMMENT '是否废弃',
  `VoidedAt` datetime DEFAULT NULL COMMENT '废弃时间',
  `VoidedBy` bigint DEFAULT NULL COMMENT '废弃操作人ID',
  `CapturedAt` datetime DEFAULT NULL COMMENT '采集时间',
  `CapturedBy` bigint NOT NULL COMMENT '采集人ID',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_task_code` (`TaskCode`),
  KEY `idx_clause_code` (`ClauseCode`),
  KEY `idx_evidence_type` (`EvidenceType`),
  KEY `idx_is_voided` (`IsVoided`),
  CONSTRAINT `fk_evidence_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`),
  CONSTRAINT `fk_evidence_task` FOREIGN KEY (`TaskCode`) REFERENCES `audit_task` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='审核证据';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `audit_finding`
--

DROP TABLE IF EXISTS `audit_finding`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `audit_finding` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `ChecklistItemCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '检查表条目编码',
  `NcCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '关联NC编码',
  `SourceFileCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '来源文件编码',
  `SourcePosition` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '来源位置（页码/行号/列号）',
  `SourceContent` text COLLATE utf8mb4_general_ci COMMENT '来源内容摘录',
  `FindingType` enum('conform','discrepancy','comment') COLLATE utf8mb4_general_ci NOT NULL COMMENT '发现类型',
  `Description` text COLLATE utf8mb4_general_ci NOT NULL COMMENT '描述',
  `Confidence` decimal(3,2) DEFAULT NULL COMMENT 'AI提取可信度 (0.00-1.00)',
  `IsManual` tinyint(1) DEFAULT '0' COMMENT '是否人工添加',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_checklist_item_code` (`ChecklistItemCode`),
  KEY `idx_nc_code` (`NcCode`),
  KEY `idx_source_file_code` (`SourceFileCode`),
  KEY `idx_finding_type` (`FindingType`),
  CONSTRAINT `fk_finding_checklist` FOREIGN KEY (`ChecklistItemCode`) REFERENCES `audit_checklist_item` (`Code`),
  CONSTRAINT `fk_finding_file` FOREIGN KEY (`SourceFileCode`) REFERENCES `ent_enterprise_file` (`Code`),
  CONSTRAINT `fk_finding_nc` FOREIGN KEY (`NcCode`) REFERENCES `audit_nonconformity` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='审核发现明细';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `audit_nonconformity`
--

DROP TABLE IF EXISTS `audit_nonconformity`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `audit_nonconformity` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `TaskCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属审核任务编码',
  `ClauseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '对应条款编码',
  `NcNumber` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'NC编号',
  `Severity` enum('major','minor','observation') COLLATE utf8mb4_general_ci NOT NULL COMMENT '严重度',
  `Description` text COLLATE utf8mb4_general_ci NOT NULL COMMENT 'NC描述（不符合事实）',
  `RequirementRef` text COLLATE utf8mb4_general_ci COMMENT '违反的标准要求原文',
  `EvidenceRef` text COLLATE utf8mb4_general_ci COMMENT '客观证据引用',
  `SourceType` enum('auto_rule','manual') COLLATE utf8mb4_general_ci DEFAULT 'manual' COMMENT 'NC来源：规则自动触发 / 手动创建',
  `SourceCheckCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '触发的合规检查记录编码',
  `RuleCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '触发的校验规则编码',
  `DueDate` date DEFAULT NULL COMMENT '整改截止日期',
  `OpenedBy` bigint NOT NULL COMMENT '开具人ID',
  `OpenedAt` datetime NOT NULL COMMENT '开具时间',
  `ClosedAt` datetime DEFAULT NULL COMMENT '关闭时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_nc_number` (`NcNumber`),
  KEY `idx_task_code` (`TaskCode`),
  KEY `idx_clause_code` (`ClauseCode`),
  KEY `idx_severity` (`Severity`),
  KEY `idx_status` (`status`),
  KEY `idx_source_type` (`SourceType`),
  KEY `fk_nc_sourcecheck` (`SourceCheckCode`),
  KEY `fk_nc_rule` (`RuleCode`),
  CONSTRAINT `fk_nc_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`),
  CONSTRAINT `fk_nc_rule` FOREIGN KEY (`RuleCode`) REFERENCES `cert_validation_rule` (`Code`),
  CONSTRAINT `fk_nc_sourcecheck` FOREIGN KEY (`SourceCheckCode`) REFERENCES `ent_file_compliance_check` (`Code`),
  CONSTRAINT `fk_nc_task` FOREIGN KEY (`TaskCode`) REFERENCES `audit_task` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='不符合项(NC)';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `audit_project`
--

DROP TABLE IF EXISTS `audit_project`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `audit_project` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` char(36) COLLATE utf8mb4_general_ci NOT NULL,
  `ProjectNo` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '项目编号',
  `ApplicationCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '关联申请编码',
  `CurrentPhase` varchar(30) COLLATE utf8mb4_general_ci DEFAULT 'application_review' COMMENT '当前阶段',
  `ProjectManagerCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `PlannedStartDate` date DEFAULT NULL COMMENT '计划开始日期',
  `PlannedEndDate` date DEFAULT NULL COMMENT '计划结束日期',
  `ActualEndDate` date DEFAULT NULL COMMENT '实际结束日期',
  `status` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Remark` text COLLATE utf8mb4_general_ci,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_project_no` (`ProjectNo`),
  KEY `idx_application_code` (`ApplicationCode`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='审核项目表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `audit_rectification`
--

DROP TABLE IF EXISTS `audit_rectification`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `audit_rectification` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `NcCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '关联NC编码',
  `Correction` text COLLATE utf8mb4_general_ci NOT NULL COMMENT '纠正措施描述',
  `CorrectiveAction` text COLLATE utf8mb4_general_ci COMMENT '纠正措施（根因分析+防再发生）',
  `EvidenceFiles` json DEFAULT NULL COMMENT '整改证据文件路径列表',
  `SubmittedBy` bigint NOT NULL COMMENT '提交人ID',
  `SubmittedAt` datetime NOT NULL COMMENT '提交时间',
  `VerifiedBy` bigint DEFAULT NULL COMMENT '复核人ID',
  `VerifiedAt` datetime DEFAULT NULL COMMENT '复核时间',
  `VerifyResult` enum('approved','rejected') COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '复核结果',
  `VerifyNotes` text COLLATE utf8mb4_general_ci COMMENT '复核意见',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_nc_code` (`NcCode`),
  KEY `idx_verify_result` (`VerifyResult`),
  CONSTRAINT `fk_rect_nc` FOREIGN KEY (`NcCode`) REFERENCES `audit_nonconformity` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='整改记录';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `audit_task`
--

DROP TABLE IF EXISTS `audit_task`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `audit_task` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateTime` datetime DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `Status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '状态',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1',
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `PhaseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属企业阶段编码',
  `TaskNumber` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '任务编号',
  `AuditorId` bigint DEFAULT NULL,
  `PlannedDate` date DEFAULT NULL COMMENT '计划审核日期',
  `ActualStartDate` date DEFAULT NULL COMMENT '实际开始日期',
  `ActualCompleteDate` date DEFAULT NULL COMMENT '实际完成日期',
  `AuditScope` text COLLATE utf8mb4_general_ci COMMENT '审核范围描述',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '创建人',
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '更新人',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_task_number` (`TaskNumber`),
  KEY `idx_phase_code` (`PhaseCode`),
  KEY `idx_status` (`Status`),
  CONSTRAINT `fk_task_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `ent_enterprise_phase` (`Code`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='审核任务';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_ai_config`
--

DROP TABLE IF EXISTS `cert_ai_config`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_ai_config` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Provider` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `ApiKey` varchar(500) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'API Key（加密存储）',
  `Model` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `Temperature` float NOT NULL DEFAULT '0.7',
  `MaxTokens` int NOT NULL DEFAULT '4096' COMMENT '最大Token数',
  `IsEnabled` tinyint(1) NOT NULL DEFAULT '1' COMMENT '是否启用：0-否 1-是',
  `Remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) DEFAULT '1',
  `Status` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'none',
  `Sort` int DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_provider_model` (`Provider`,`Model`)
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='AI配置表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_ai_usage_log`
--

DROP TABLE IF EXISTS `cert_ai_usage_log`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_ai_usage_log` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `CallId` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `BusinessType` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `BusinessRef` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Skill` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `Provider` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `Model` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `PromptTokens` int DEFAULT '0',
  `CompletionTokens` int DEFAULT '0',
  `TotalTokens` int DEFAULT '0',
  `CostUsd` decimal(10,6) DEFAULT '0.000000',
  `DurationMs` bigint DEFAULT '0',
  `success` tinyint(1) DEFAULT '1' COMMENT '是否成功',
  `ErrorMessage` text COLLATE utf8mb4_general_ci,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP COMMENT '调用时间',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除',
  `Status` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'none',
  `Remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `code` (`Code`),
  UNIQUE KEY `uk_call_id` (`CallId`),
  KEY `idx_create_date` (`CreateTime`),
  KEY `idx_model` (`Model`),
  KEY `idx_success` (`success`)
) ENGINE=InnoDB AUTO_INCREMENT=41 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='AI 调用日志（用于费用统计）';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_application`
--

DROP TABLE IF EXISTS `cert_application`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_application` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` char(36) COLLATE utf8mb4_general_ci NOT NULL,
  `ApplicationNo` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '申请编号',
  `CbCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '认证机构编码',
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '标准编码',
  `EnterpriseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '企业编码',
  `CertType` varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT '认证类型(QMS/EMS等)',
  `ScopeText` text COLLATE utf8mb4_general_ci COMMENT '认证范围描述',
  `Status` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'none',
  `SubmitTime` datetime DEFAULT NULL COMMENT '提交时间',
  `AcceptTime` datetime DEFAULT NULL COMMENT '受理时间',
  `CompleteTime` datetime DEFAULT NULL COMMENT '完成时间',
  `Remark` text COLLATE utf8mb4_general_ci,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_application_no` (`ApplicationNo`),
  KEY `idx_cb_code` (`CbCode`),
  KEY `idx_status` (`Status`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='认证申请表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_auditor_profile`
--

DROP TABLE IF EXISTS `cert_auditor_profile`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_auditor_profile` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '业务编码',
  `UserCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '用户编码',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属认证机构编码',
  `AuditorNo` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '审核员编号',
  `AuditorName` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '审核员姓名',
  `Phone` varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT '电话',
  `Email` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '邮箱',
  `Qualification` json DEFAULT NULL COMMENT '资质',
  `ExpertiseAreas` json DEFAULT NULL COMMENT '专业领域',
  `Status` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'active' COMMENT '状态',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL COMMENT '修改时间',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除',
  `Remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `code` (`Code`),
  UNIQUE KEY `auditor_no` (`AuditorNo`),
  KEY `idx_org_code` (`OrgCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='审核员资质档案';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_cert_stage`
--

DROP TABLE IF EXISTS `cert_cert_stage`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_cert_stage` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `StageCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '阶段编码（如 AP/CR/SP/S1/S2/CD/CE/SV/RC）',
  `StageName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '阶段名称',
  `Category` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'process' COMMENT '分类（process/audit/post-cert）',
  `SortOrder` int DEFAULT '0' COMMENT '排序号（1~9）',
  `Description` text COLLATE utf8mb4_general_ci COMMENT '阶段说明',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1',
  `Status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `Remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_stage_code` (`StageCode`),
  KEY `idx_category` (`Category`),
  KEY `idx_sort_order` (`SortOrder`)
) ENGINE=InnoDB AUTO_INCREMENT=11 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='认证阶段（全局基础资料）';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_certification_body`
--

DROP TABLE IF EXISTS `cert_certification_body`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_certification_body` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '状态',
  `IsValid` tinyint(1) DEFAULT '1',
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `Name` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '机构名称',
  `ShortName` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '简称',
  `CbCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT 'CNAS编号',
  `ContactName` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '联系人',
  `ContactPhone` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '联系电话',
  `LegalPerson` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '法人',
  `ContactEmail` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '邮箱',
  `Address` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '地址',
  `LogoUrl` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT 'Logo',
  `ScopeText` text COLLATE utf8mb4_general_ci COMMENT '业务范围',
  `ThemeConfig` text COLLATE utf8mb4_general_ci COMMENT '主题配置',
  `LoginConfig` text COLLATE utf8mb4_general_ci COMMENT '登录配置',
  `MaxUsers` int DEFAULT '100' COMMENT '最大用户数',
  `MaxEnterprises` int DEFAULT '1000' COMMENT '最大企业数',
  `ExpireDate` datetime DEFAULT NULL COMMENT '到期日期',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_name` (`Name`),
  UNIQUE KEY `uk_cb_code` (`CbCode`),
  KEY `idx_status` (`Status`),
  KEY `idx_org_code` (`OrgCode`)
) ENGINE=InnoDB AUTO_INCREMENT=9 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='认证机构';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_clause_extraction_rule`
--

DROP TABLE IF EXISTS `cert_clause_extraction_rule`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_clause_extraction_rule` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Status` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'none',
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `ClauseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '条款编码',
  `WorkflowCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '关联的提取工作流编码',
  `Description` text COLLATE utf8mb4_general_ci COMMENT '规则集说明',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_clause_code` (`ClauseCode`),
  KEY `idx_workflow_code` (`WorkflowCode`),
  CONSTRAINT `fk_clauseext_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`),
  CONSTRAINT `fk_clauseext_workflow` FOREIGN KEY (`WorkflowCode`) REFERENCES `wf_workflow_definition` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='条款提取规则';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_directory_template`
--

DROP TABLE IF EXISTS `cert_directory_template`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_directory_template` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Status` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'none',
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `ConfigCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属标准-阶段配置编码',
  `ParentCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '父文件夹编码（树形结构）',
  `FolderName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '文件夹名称',
  `SortOrder` int DEFAULT '0' COMMENT '排序',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_config_code` (`ConfigCode`),
  KEY `idx_parent_code` (`ParentCode`),
  CONSTRAINT `fk_dirtemplate_config` FOREIGN KEY (`ConfigCode`) REFERENCES `cert_standard_phase_config` (`Code`),
  CONSTRAINT `fk_dirtemplate_parent` FOREIGN KEY (`ParentCode`) REFERENCES `cert_directory_template` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文件目录模板';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_doc_extraction_rule`
--

DROP TABLE IF EXISTS `cert_doc_extraction_rule`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_doc_extraction_rule` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `FileCode` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StandardFileCode` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '规则键：实际文件 FileCode 或文件要求模板 Code',
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '标准编码(冗余)',
  `PhaseCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '阶段编码(冗余)',
  `Skill` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `Prompt` text COLLATE utf8mb4_general_ci,
  `DocIsValid` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否验证通过：0-否 1-是',
  `VerifyMessage` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '验证结果信息',
  `SampleData` json DEFAULT NULL COMMENT '验证时提取的样本数据（JSON格式）',
  `DocContent` longtext COLLATE utf8mb4_general_ci COMMENT '提取的文档内容缓存',
  `Status` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'none',
  `Remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '启用状态',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_standard_file_code` (`StandardFileCode`),
  KEY `idx_status` (`Status`)
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文档提取规则主表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_doc_field_def`
--

DROP TABLE IF EXISTS `cert_doc_field_def`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_doc_field_def` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `RuleCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '规则编码（关联cert_doc_extraction_rule.code）',
  `FieldName` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '字段名称',
  `FieldCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '字段编码（用于工作流引用）',
  `DataType` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'string' COMMENT '数据类型：string/number/date/boolean',
  `Description` text COLLATE utf8mb4_general_ci,
  `IsManual` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否需手动补充：0-否 1-是',
  `Sort` int NOT NULL DEFAULT '0' COMMENT '显示顺序',
  `Remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `IsAiRecommended` tinyint(1) DEFAULT '1' COMMENT '是否AI推荐字段(1=是,0=手动添加)',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '启用状态',
  `Status` varchar(50) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'active',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_rule_field` (`RuleCode`,`FieldCode`),
  KEY `idx_rule_code` (`RuleCode`),
  KEY `idx_field_code` (`FieldCode`)
) ENGINE=InnoDB AUTO_INCREMENT=13 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文档字段定义表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_doc_table_def`
--

DROP TABLE IF EXISTS `cert_doc_table_def`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_doc_table_def` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `RuleCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '规则编码（关联cert_doc_extraction_rule.code）',
  `TableName` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '表格名称',
  `TableCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '表格编码（用于工作流引用）',
  `Description` text COLLATE utf8mb4_general_ci,
  `Sort` int NOT NULL DEFAULT '0' COMMENT '显示顺序',
  `Remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '启用状态',
  `Status` varchar(50) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'active',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_rule_table` (`RuleCode`,`TableCode`),
  KEY `idx_rule_code` (`RuleCode`),
  KEY `idx_table_code` (`TableCode`)
) ENGINE=InnoDB AUTO_INCREMENT=9 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文档表格定义表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_doc_table_field_def`
--

DROP TABLE IF EXISTS `cert_doc_table_field_def`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_doc_table_field_def` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `TableCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '表格编码（关联cert_doc_table_def.code）',
  `ColumnName` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '列名称',
  `ColumnCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '列编码',
  `DataType` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'string' COMMENT '数据类型：string/number/date',
  `Sort` int NOT NULL DEFAULT '0' COMMENT '显示顺序',
  `Remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '启用状态',
  `Status` varchar(50) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'active',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_table_column` (`TableCode`,`ColumnCode`),
  KEY `idx_table_code` (`TableCode`),
  KEY `idx_column_code` (`ColumnCode`)
) ENGINE=InnoDB AUTO_INCREMENT=25 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文档表格字段定义表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_enterprise`
--

DROP TABLE IF EXISTS `cert_enterprise`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_enterprise` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` char(36) COLLATE utf8mb4_general_ci NOT NULL,
  `Name` varchar(200) COLLATE utf8mb4_general_ci NOT NULL,
  `ShortName` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreditCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '统一社会信用代码',
  `LegalPerson` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ContactName` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ContactPhone` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ContactEmail` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Province` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '省份',
  `City` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '城市',
  `Address` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IndustryType` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '行业类型',
  `EmployeeCount` int DEFAULT NULL COMMENT '员工人数',
  `Status` tinyint DEFAULT NULL COMMENT '状态',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '所属机构编码',
  `Remark` text COLLATE utf8mb4_general_ci,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_credit_code` (`CreditCode`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业信息表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_file_requirement`
--

DROP TABLE IF EXISTS `cert_file_requirement`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_file_requirement` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Status` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'none',
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `FolderCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属文件夹编码',
  `FileNameTemplate` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '文件名称模板',
  `FileType` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '允许的文件类型（pdf/docx/xlsx/png 等）',
  `IsRequired` tinyint(1) DEFAULT '1' COMMENT '是否必须提供',
  `MaxSizeMB` int DEFAULT '10' COMMENT '最大文件大小（MB）',
  `Description` text COLLATE utf8mb4_general_ci COMMENT '文件说明/要求描述',
  `SortOrder` int DEFAULT '0' COMMENT '排序',
  `TemplateStoragePath` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '模板文件 OSS 存储路径',
  `TemplateFileName` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '模板文件原始名',
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '标准编码（cert_iso_standard.Code）',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_folder_code` (`FolderCode`),
  CONSTRAINT `fk_filereq_folder` FOREIGN KEY (`FolderCode`) REFERENCES `cert_directory_template` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文件要求';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_iso_clause`
--

DROP TABLE IF EXISTS `cert_iso_clause`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_iso_clause` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属标准编码',
  `ParentCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '父条款编码（树形结构）',
  `ClauseNumber` varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT '条款编号（如 7.1、7.1.1）',
  `Title` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '条款标题',
  `Description` text COLLATE utf8mb4_general_ci COMMENT '条款原文或摘要',
  `SortOrder` int DEFAULT '0' COMMENT '排序',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_parent_code` (`ParentCode`),
  KEY `idx_clause_number` (`ClauseNumber`)
) ENGINE=InnoDB AUTO_INCREMENT=71 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='标准条款';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_iso_standard`
--

DROP TABLE IF EXISTS `cert_iso_standard`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_iso_standard` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '状态',
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `CbCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '机构编号',
  `StandardCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '标准编码',
  `StandardName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '标准名称',
  `VersionYear` int DEFAULT NULL COMMENT '版本年份',
  `Category` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'quality' COMMENT '分类',
  `Description` text COLLATE utf8mb4_general_ci COMMENT '说明',
  `ParentCode` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '父级编码',
  `IsLeaf` tinyint(1) NOT NULL DEFAULT '1' COMMENT '是否叶子节点',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_cb_code` (`CbCode`),
  KEY `idx_standard_code` (`StandardCode`)
) ENGINE=InnoDB AUTO_INCREMENT=15 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='ISO标准';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_message`
--

DROP TABLE IF EXISTS `cert_message`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_message` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `UserCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UserName` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Title` varchar(200) COLLATE utf8mb4_general_ci NOT NULL,
  `Content` text COLLATE utf8mb4_general_ci,
  `MessageType` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'system',
  `IsRead` tinyint(1) DEFAULT '0',
  `ExtraData` json DEFAULT NULL,
  `RelatedCode` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `ReadDate` datetime DEFAULT NULL,
  `IsValid` tinyint(1) DEFAULT NULL,
  `Status` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'none',
  `Remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  KEY `idx_is_read` (`IsRead`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_org_stage`
--

DROP TABLE IF EXISTS `cert_org_stage`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_org_stage` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `StandardCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StageCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `IsValid` tinyint(1) DEFAULT '1',
  `Status` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'none',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_org_std_stage` (`OrgCode`,`StandardCode`,`StageCode`),
  KEY `idx_org_code` (`OrgCode`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_phase_code` (`StageCode`)
) ENGINE=InnoDB AUTO_INCREMENT=48 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_org_standard`
--

DROP TABLE IF EXISTS `cert_org_standard`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_org_standard` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `StandardCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `IsValid` tinyint(1) DEFAULT '1',
  `Status` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'none',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_org_std` (`OrgCode`,`StandardCode`),
  KEY `idx_org_code` (`OrgCode`),
  KEY `idx_standard_code` (`StandardCode`)
) ENGINE=InnoDB AUTO_INCREMENT=14 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_phase_definition`
--

DROP TABLE IF EXISTS `cert_phase_definition`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_phase_definition` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '业务编码',
  `PhaseCode` varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT '阶段编码',
  `PhaseName` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '阶段名称',
  `SequenceOrder` int NOT NULL DEFAULT '0' COMMENT '顺序',
  `Description` text COLLATE utf8mb4_general_ci COMMENT '说明',
  `IsValid` int NOT NULL DEFAULT '1' COMMENT '有效标志(1=启用, 0=停用)',
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '创建人编码',
  `UpdateTime` datetime DEFAULT NULL COMMENT '更新时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '更新人编码',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人编码',
  `IsDeleted` tinyint NOT NULL DEFAULT '0' COMMENT '软删除标志',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `code` (`Code`),
  UNIQUE KEY `phase_code` (`PhaseCode`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_phase_code` (`PhaseCode`),
  KEY `idx_is_valid` (`IsValid`),
  KEY `idx_is_deleted` (`IsDeleted`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='认证阶段定义（通用五阶段，ISO 17021）';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_report_template`
--

DROP TABLE IF EXISTS `cert_report_template`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_report_template` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Status` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'none',
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `CbCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '认证机构编码',
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '标准编码',
  `PhaseCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `TemplateName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '模板名称',
  `TemplateFilePath` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '空白文档文件路径（MinIO）',
  `SectionConfig` json DEFAULT NULL COMMENT '报告章节配置（含每章节的 workflow_id、clause_id 映射）',
  `IsDefault` tinyint(1) DEFAULT '0' COMMENT '是否默认模板',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_cb_code` (`CbCode`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_phase_code` (`PhaseCode`),
  CONSTRAINT `fk_rpttmpl_cb` FOREIGN KEY (`CbCode`) REFERENCES `cert_certification_body` (`Code`)
) ENGINE=InnoDB AUTO_INCREMENT=13 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告模板';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_standard_directory_config`
--

DROP TABLE IF EXISTS `cert_standard_directory_config`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_standard_directory_config` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `DirectoryCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `StandardCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `PhaseCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `RootFolderName` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Status` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'none',
  `IsValid` tinyint(1) DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `StatusField` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `Sort` int DEFAULT '0',
  `Remark` text COLLATE utf8mb4_general_ci,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_directory_code` (`DirectoryCode`),
  UNIQUE KEY `uk_standard_phase` (`StandardCode`,`PhaseCode`)
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_standard_directory_file`
--

DROP TABLE IF EXISTS `cert_standard_directory_file`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_standard_directory_file` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `FileCode` varchar(150) COLLATE utf8mb4_general_ci NOT NULL,
  `FolderCode` varchar(150) COLLATE utf8mb4_general_ci NOT NULL,
  `DirectoryCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `FileName` varchar(500) COLLATE utf8mb4_general_ci NOT NULL,
  `FileType` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `FileSize` bigint DEFAULT NULL COMMENT '文件大小(字节)',
  `FilePattern` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsRequired` tinyint(1) DEFAULT '1',
  `MaxFileSizeMB` int DEFAULT '10',
  `Description` text COLLATE utf8mb4_general_ci,
  `SortOrder` int DEFAULT '0',
  `ExtractionEnabled` tinyint(1) DEFAULT '0',
  `ExtractionRules` json DEFAULT NULL,
  `PreCheckRequired` tinyint(1) DEFAULT '1',
  `ComplianceRequired` tinyint(1) DEFAULT '0',
  `Status` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `StatusField` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `Sort` int DEFAULT '0',
  `Remark` text COLLATE utf8mb4_general_ci,
  `TaskId` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint(1) DEFAULT '1',
  `UploadStatus` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `StoragePath` varchar(512) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `FullPath` varchar(1024) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ConvertedStoragePath` varchar(512) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ConvertStatus` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ConvertMessage` varchar(1024) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ConvertDate` datetime DEFAULT NULL,
  `PreviewPdfPath` varchar(512) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '预览PDF产物路径（LibreOffice转换）',
  `MarkdownPath` varchar(512) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '提取Markdown产物路径（anydoc转换）',
  `MarkdownStatus` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'none' COMMENT 'Markdown转换状态：none/pending/converting/completed/failed',
  `MarkdownMessage` varchar(1024) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT 'Markdown转换失败原因',
  `MarkdownDate` datetime DEFAULT NULL COMMENT 'Markdown转换完成时间',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_file_code` (`FileCode`),
  KEY `idx_folder_code` (`FolderCode`),
  KEY `idx_directory_code` (`DirectoryCode`)
) ENGINE=InnoDB AUTO_INCREMENT=502 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_standard_directory_folder`
--

DROP TABLE IF EXISTS `cert_standard_directory_folder`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_standard_directory_folder` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `FolderCode` varchar(150) COLLATE utf8mb4_general_ci NOT NULL,
  `DirectoryCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `ParentCode` varchar(150) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `FolderName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL,
  `Depth` int DEFAULT '1',
  `SortOrder` int DEFAULT '0',
  `Status` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `StatusField` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `Sort` int DEFAULT '0',
  `Remark` text COLLATE utf8mb4_general_ci,
  `TaskId` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint(1) DEFAULT '0',
  `FullPath` varchar(1024) COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_folder_code` (`FolderCode`),
  KEY `idx_directory_code` (`DirectoryCode`),
  KEY `idx_parent_code` (`ParentCode`)
) ENGINE=InnoDB AUTO_INCREMENT=34 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_standard_phase_config`
--

DROP TABLE IF EXISTS `cert_standard_phase_config`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_standard_phase_config` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Status` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'none',
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '标准编码',
  `PhaseCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `RequiredClauses` json DEFAULT NULL COMMENT '此阶段需检查的条款编码列表',
  `RequiredFiles` json DEFAULT NULL COMMENT '此阶段必需的文件清单编码列表',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_standard_phase` (`StandardCode`,`PhaseCode`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_phase_code` (`PhaseCode`),
  CONSTRAINT `fk_spconfig_standard` FOREIGN KEY (`StandardCode`) REFERENCES `cert_iso_standard` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='标准-阶段配置';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_sys_config`
--

DROP TABLE IF EXISTS `cert_sys_config`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_sys_config` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `ConfigKey` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `ConfigValue` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ConfigType` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Category` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `DisplayName` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Description` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序',
  `IsReadonly` tinyint DEFAULT '0',
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `DeleteBy` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '启用状态: 1=启用, 0=禁用/逻辑删除',
  `Status` varchar(50) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'active',
  `Remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Code` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_config_key` (`ConfigKey`),
  UNIQUE KEY `code` (`Code`),
  KEY `idx_category` (`Category`)
) ENGINE=InnoDB AUTO_INCREMENT=29 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='全局系统参数配置';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_upload_task`
--

DROP TABLE IF EXISTS `cert_upload_task`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_upload_task` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `TaskId` varchar(64) COLLATE utf8mb4_general_ci NOT NULL,
  `DirectoryCode` varchar(128) COLLATE utf8mb4_general_ci NOT NULL,
  `TotalFiles` int NOT NULL DEFAULT '0',
  `TotalSize` bigint NOT NULL DEFAULT '0',
  `SuccessCount` int NOT NULL DEFAULT '0',
  `Status` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'initialized',
  `CreateBy` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  `ExpireTime` datetime DEFAULT NULL,
  `Code` varchar(64) COLLATE utf8mb4_general_ci NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UK_TaskId` (`TaskId`),
  UNIQUE KEY `code` (`Code`)
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='上传任务追踪表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_validation_rule`
--

DROP TABLE IF EXISTS `cert_validation_rule`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_validation_rule` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Status` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'none',
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '适用标准编码',
  `PhaseCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `ClauseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '对应条款编码',
  `WorkflowCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `RuleCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '规则编码',
  `RuleName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '规则名称',
  `RuleNameEn` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `SeverityIfViolated` enum('major','minor','observation') COLLATE utf8mb4_general_ci DEFAULT NULL,
  `RuleJson` longtext COLLATE utf8mb4_general_ci,
  `LayoutJson` longtext COLLATE utf8mb4_general_ci,
  `NcDescriptionTemplate` text COLLATE utf8mb4_general_ci COMMENT 'NC描述模板',
  `IsActive` tinyint(1) DEFAULT '1' COMMENT '是否启用',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_rule_code` (`RuleCode`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_phase_code` (`PhaseCode`),
  KEY `idx_clause_code` (`ClauseCode`),
  KEY `idx_workflow_code` (`WorkflowCode`),
  CONSTRAINT `fk_valrule_clause` FOREIGN KEY (`ClauseCode`) REFERENCES `cert_iso_clause` (`Code`),
  CONSTRAINT `fk_valrule_standard` FOREIGN KEY (`StandardCode`) REFERENCES `cert_iso_standard` (`Code`),
  CONSTRAINT `fk_valrule_workflow` FOREIGN KEY (`WorkflowCode`) REFERENCES `wf_workflow_definition` (`Code`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='校验规则';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cert_validation_rule_source`
--

DROP TABLE IF EXISTS `cert_validation_rule_source`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `cert_validation_rule_source` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `Status` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'none',
  `IsValid` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `RuleCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '校验规则编码',
  `FileRequirementCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '溯源文件类型编码',
  `SourcePath` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '溯源路径（文件内位置描述）',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_rule_code` (`RuleCode`),
  KEY `idx_filereq_code` (`FileRequirementCode`),
  CONSTRAINT `fk_valsource_filereq` FOREIGN KEY (`FileRequirementCode`) REFERENCES `cert_file_requirement` (`Code`),
  CONSTRAINT `fk_valsource_rule` FOREIGN KEY (`RuleCode`) REFERENCES `cert_validation_rule` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='校验规则溯源';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ent_enterprise`
--

DROP TABLE IF EXISTS `ent_enterprise`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `ent_enterprise` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `EnterpriseNo` varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT '企业编号',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `Status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint(1) NOT NULL DEFAULT '1',
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `Name` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '企业全称',
  `ShortName` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '简称',
  `CreditCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '统一社会信用代码',
  `LegalPerson` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '法人代表',
  `Address` text COLLATE utf8mb4_general_ci COMMENT '企业地址',
  `Province` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '省份',
  `City` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '城市',
  `IndustryType` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '行业类型',
  `EmployeeCount` int DEFAULT NULL COMMENT '员工人数',
  `CertScope` text COLLATE utf8mb4_general_ci COMMENT '认证范围描述',
  `ContactName` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '对接人姓名',
  `ContactPhone` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '对接人电话',
  `ContactEmail` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '对接人邮箱',
  `ArchiveDate` date DEFAULT NULL COMMENT '归档日期',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_enterprise_no` (`EnterpriseNo`),
  UNIQUE KEY `uk_credit_code` (`CreditCode`),
  KEY `idx_name` (`Name`),
  KEY `idx_status` (`Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ent_enterprise_document`
--

DROP TABLE IF EXISTS `ent_enterprise_document`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `ent_enterprise_document` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `EnterpriseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属企业编码',
  `PhaseCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '所属阶段编码（scope=phase时必填）',
  `Scope` enum('enterprise_base','phase') COLLATE utf8mb4_general_ci NOT NULL COMMENT '资料层级：共享层 / 隔离层',
  `TemplateFolderCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '对应的模板文件夹编码',
  `ParentCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '父文件夹编码（树形结构）',
  `FolderName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '文件夹名称',
  `SortOrder` int DEFAULT '0' COMMENT '排序',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_enterprise_code` (`EnterpriseCode`),
  KEY `idx_phase_code` (`PhaseCode`),
  KEY `idx_parent_code` (`ParentCode`),
  KEY `idx_scope` (`Scope`),
  KEY `fk_edoc_template` (`TemplateFolderCode`),
  CONSTRAINT `fk_edoc_enterprise` FOREIGN KEY (`EnterpriseCode`) REFERENCES `ent_enterprise` (`Code`),
  CONSTRAINT `fk_edoc_parent` FOREIGN KEY (`ParentCode`) REFERENCES `ent_enterprise_document` (`Code`),
  CONSTRAINT `fk_edoc_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `ent_enterprise_phase` (`Code`),
  CONSTRAINT `fk_edoc_template` FOREIGN KEY (`TemplateFolderCode`) REFERENCES `cert_directory_template` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业文档目录';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ent_enterprise_file`
--

DROP TABLE IF EXISTS `ent_enterprise_file`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `ent_enterprise_file` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `FolderCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属文件夹编码',
  `FileName` varchar(500) COLLATE utf8mb4_general_ci NOT NULL COMMENT '文件名',
  `FileType` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '文件类型（pdf/docx/xlsx/png/jpg）',
  `FileSize` bigint NOT NULL COMMENT '文件大小（bytes）',
  `StoragePath` varchar(500) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'MinIO存储路径',
  `FileHash` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '文件SHA256哈希（增量审核依据）',
  `CurrentVersion` int DEFAULT '1' COMMENT '当前版本号',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_folder_code` (`FolderCode`),
  KEY `idx_file_hash` (`FileHash`),
  CONSTRAINT `fk_efile_folder` FOREIGN KEY (`FolderCode`) REFERENCES `ent_enterprise_document` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业文件';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ent_enterprise_phase`
--

DROP TABLE IF EXISTS `ent_enterprise_phase`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `ent_enterprise_phase` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `EnterpriseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属企业编码',
  `PhaseCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '认证标准编码',
  `StartedAt` datetime DEFAULT NULL COMMENT '开始时间',
  `CompletedAt` datetime DEFAULT NULL COMMENT '完成时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_ent_phase_std` (`EnterpriseCode`,`PhaseCode`,`StandardCode`),
  KEY `idx_enterprise_code` (`EnterpriseCode`),
  KEY `idx_phase_code` (`PhaseCode`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_status` (`status`),
  CONSTRAINT `fk_ephase_enterprise` FOREIGN KEY (`EnterpriseCode`) REFERENCES `ent_enterprise` (`Code`),
  CONSTRAINT `fk_ephase_standard` FOREIGN KEY (`StandardCode`) REFERENCES `cert_iso_standard` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='企业阶段';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ent_extraction_result`
--

DROP TABLE IF EXISTS `ent_extraction_result`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `ent_extraction_result` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `FileCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '提取的源文件编码',
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StandardFileCode` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `PhaseCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `VersionNumber` int NOT NULL COMMENT '提取的文件版本',
  `RuleCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '使用的提取规则编码',
  `FieldCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '对应的提取字段编码',
  `LabelTag` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '字段标签冗余（便于查询）',
  `ExtractedValue` text COLLATE utf8mb4_general_ci COMMENT '提取的值',
  `Confidence` decimal(3,2) DEFAULT NULL COMMENT 'AI提取可信度 (0.00-1.00)',
  `PositionInfo` json DEFAULT NULL COMMENT '位置信息（页码/行号/列号/单元格）',
  `IsManualEdited` tinyint(1) DEFAULT '0' COMMENT '是否被人工修改',
  `ExtractedAt` datetime NOT NULL COMMENT '提取时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_file_code` (`FileCode`),
  KEY `idx_rule_code` (`RuleCode`),
  KEY `idx_field_code` (`FieldCode`),
  KEY `idx_label_tag` (`LabelTag`),
  CONSTRAINT `fk_extres_field` FOREIGN KEY (`FieldCode`) REFERENCES `cert_extraction_field` (`Code`),
  CONSTRAINT `fk_extres_rule` FOREIGN KEY (`RuleCode`) REFERENCES `cert_extraction_rule` (`Code`),
  CONSTRAINT `k_extres_file` FOREIGN KEY (`FileCode`) REFERENCES `ent_enterprise_file` (`Code`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文档提取结果';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ent_file_compliance_check`
--

DROP TABLE IF EXISTS `ent_file_compliance_check`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `ent_file_compliance_check` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `FileCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '被检查的文件编码',
  `VersionNumber` int NOT NULL COMMENT '检查的文件版本',
  `RuleCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '触发的校验规则编码',
  `WorkflowExecutionCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '工作流执行记录编码',
  `CheckStatus` enum('pass','fail','warning','blocked') COLLATE utf8mb4_general_ci NOT NULL COMMENT '检查结果',
  `Message` text COLLATE utf8mb4_general_ci COMMENT '检查信息',
  `Detail` json DEFAULT NULL COMMENT '详细信息（含具体位置、偏离描述）',
  `CheckedAt` datetime NOT NULL COMMENT '检查时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_file_code` (`FileCode`),
  KEY `idx_rule_code` (`RuleCode`),
  KEY `idx_check_status` (`CheckStatus`),
  KEY `fk_compliance_wexec` (`WorkflowExecutionCode`),
  CONSTRAINT `fk_compliance_file` FOREIGN KEY (`FileCode`) REFERENCES `ent_enterprise_file` (`Code`),
  CONSTRAINT `fk_compliance_rule` FOREIGN KEY (`RuleCode`) REFERENCES `cert_validation_rule` (`Code`),
  CONSTRAINT `fk_compliance_wexec` FOREIGN KEY (`WorkflowExecutionCode`) REFERENCES `wf_workflow_execution_log` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文件合规检查';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ent_file_pre_check_result`
--

DROP TABLE IF EXISTS `ent_file_pre_check_result`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `ent_file_pre_check_result` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `FileCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '被检查的文件编码',
  `VersionNumber` int NOT NULL COMMENT '检查的文件版本',
  `CheckType` enum('readability','clarity','format','completeness') COLLATE utf8mb4_general_ci NOT NULL COMMENT '检查类型',
  `CheckResult` enum('pass','warning','block') COLLATE utf8mb4_general_ci NOT NULL COMMENT '检查结果',
  `Message` text COLLATE utf8mb4_general_ci COMMENT '检查信息',
  `Detail` json DEFAULT NULL COMMENT '详细信息（DPI值、倾斜角度、缺页数等）',
  `CheckedAt` datetime NOT NULL COMMENT '检查时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_file_code` (`FileCode`),
  KEY `idx_check_type` (`CheckType`),
  KEY `idx_check_result` (`CheckResult`),
  CONSTRAINT `fk_precheck_file` FOREIGN KEY (`FileCode`) REFERENCES `ent_enterprise_file` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='资料质量预审结果';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ent_file_version`
--

DROP TABLE IF EXISTS `ent_file_version`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `ent_file_version` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `FileCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '源文件编码',
  `VersionNumber` int NOT NULL COMMENT '版本号（从1开始递增）',
  `FileSize` bigint NOT NULL COMMENT '版本文件大小',
  `StoragePath` varchar(500) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'MinIO存储路径',
  `FileHash` varchar(64) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'SHA256哈希',
  `ChangeNotes` text COLLATE utf8mb4_general_ci COMMENT '变更说明',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_file_version` (`FileCode`,`VersionNumber`),
  KEY `idx_file_code` (`FileCode`),
  CONSTRAINT `fk_fver_file` FOREIGN KEY (`FileCode`) REFERENCES `ent_enterprise_file` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='文件版本';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ent_table_extraction_result`
--

DROP TABLE IF EXISTS `ent_table_extraction_result`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `ent_table_extraction_result` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `FileCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '提取的源文件编码',
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StandardFileCode` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `PhaseCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `VersionNumber` int NOT NULL COMMENT '提取的文件版本',
  `RuleCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '使用的提取规则编码',
  `TableIndex` int DEFAULT '1' COMMENT '文档中第几个表格',
  `ExtractedJson` json NOT NULL COMMENT '表格内容（JSON）',
  `Confidence` decimal(3,2) DEFAULT NULL COMMENT 'AI提取可信度',
  `PositionInfo` json DEFAULT NULL COMMENT '表格在文档中的位置信息',
  `ExtractedAt` datetime NOT NULL COMMENT '提取时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_file_code` (`FileCode`),
  KEY `idx_rule_code` (`RuleCode`),
  CONSTRAINT `fk_tableext_file` FOREIGN KEY (`FileCode`) REFERENCES `ent_enterprise_file` (`Code`),
  CONSTRAINT `fk_tableext_rule` FOREIGN KEY (`RuleCode`) REFERENCES `cert_extraction_rule` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='表格提取结果';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `FormCollectionObject`
--

DROP TABLE IF EXISTS `FormCollectionObject`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `FormCollectionObject` (
  `FormCollectionId` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `FormId` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Title` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `FormData` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  PRIMARY KEY (`FormCollectionId`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `FormDesignOptions`
--

DROP TABLE IF EXISTS `FormDesignOptions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `FormDesignOptions` (
  `FormId` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `Title` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `DaraggeOptions` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `FormOptions` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `FormConfig` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `FormFields` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `TableConfig` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  PRIMARY KEY (`FormId`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `rpt_audit_report`
--

DROP TABLE IF EXISTS `rpt_audit_report`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `rpt_audit_report` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `TaskCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属报告任务编码',
  `VersionNumber` int DEFAULT '1' COMMENT '报告版本号',
  `ReportTitle` varchar(500) COLLATE utf8mb4_general_ci NOT NULL COMMENT '报告标题',
  `FullContent` mediumtext COLLATE utf8mb4_general_ci COMMENT '报告完整内容（Markdown/HTML）',
  `ExportPath` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '导出的PDF/Word文件路径',
  `EditedBy` bigint DEFAULT NULL COMMENT '最后编辑人ID',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_task_code` (`TaskCode`),
  CONSTRAINT `fk_report_task` FOREIGN KEY (`TaskCode`) REFERENCES `rpt_report_task` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告正文';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `rpt_report_section`
--

DROP TABLE IF EXISTS `rpt_report_section`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `rpt_report_section` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `ReportCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属报告编码',
  `ClauseCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '对应条款编码（可空，概述/结论章节不映射条款）',
  `SectionName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '章节名称',
  `SectionNameEn` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '英文名称',
  `SectionContent` text COLLATE utf8mb4_general_ci COMMENT '章节填充内容',
  `WorkflowCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '生成此章节的工作流编码',
  `WorkflowConfig` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '工作流DAG JSON',
  `LayoutJson` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '工作流布局JSON',
  `SectionJson` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '章节配置JSON',
  `SortOrder` int DEFAULT '0' COMMENT '章节排序',
  `IsActive` tinyint(1) NOT NULL DEFAULT '1' COMMENT '是否启用',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_report_code` (`ReportCode`),
  KEY `idx_clause_code` (`ClauseCode`),
  KEY `idx_workflow_code` (`WorkflowCode`)
) ENGINE=InnoDB AUTO_INCREMENT=10 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告章节内容';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `rpt_report_section_source`
--

DROP TABLE IF EXISTS `rpt_report_section_source`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `rpt_report_section_source` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `SectionCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属报告章节编码',
  `SourceType` enum('extraction','finding','nc','manual','template','compliance') COLLATE utf8mb4_general_ci NOT NULL COMMENT '来源类型',
  `SourceCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '来源记录的编码（根据source_type指向不同表）',
  `SourceDescription` text COLLATE utf8mb4_general_ci COMMENT '来源描述',
  `Confidence` decimal(3,2) DEFAULT NULL COMMENT '可信度',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_section_code` (`SectionCode`),
  KEY `idx_source_type` (`SourceType`),
  CONSTRAINT `fk_src_section` FOREIGN KEY (`SectionCode`) REFERENCES `rpt_report_section` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告内容溯源';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `rpt_report_task`
--

DROP TABLE IF EXISTS `rpt_report_task`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `rpt_report_task` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `PhaseCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属企业阶段编码',
  `BasedOnAuditTaskCode` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '基于的审核任务编码',
  `TemplateCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '使用的报告模板编码',
  `TaskNumber` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '任务编号',
  `GeneratedAt` datetime DEFAULT NULL COMMENT '生成时间',
  `LockedAt` datetime DEFAULT NULL COMMENT '锁定时间',
  `LockedBy` bigint DEFAULT NULL COMMENT '锁定人ID',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_task_number` (`TaskNumber`),
  KEY `idx_phase_code` (`PhaseCode`),
  KEY `idx_audit_task_code` (`BasedOnAuditTaskCode`),
  KEY `idx_template_code` (`TemplateCode`),
  KEY `idx_status` (`status`),
  CONSTRAINT `fk_rpttask_audit` FOREIGN KEY (`BasedOnAuditTaskCode`) REFERENCES `audit_task` (`Code`),
  CONSTRAINT `fk_rpttask_phase` FOREIGN KEY (`PhaseCode`) REFERENCES `ent_enterprise_phase` (`Code`),
  CONSTRAINT `fk_rpttask_template` FOREIGN KEY (`TemplateCode`) REFERENCES `cert_report_template` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='报告任务';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `SellOrder`
--

DROP TABLE IF EXISTS `SellOrder`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `SellOrder` (
  `Id` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `OrderType` int NOT NULL,
  `TranNo` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `SellNo` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `Qty` int NOT NULL,
  `AuditDate` datetime DEFAULT NULL,
  `AuditStatus` int NOT NULL,
  `AuditId` int DEFAULT NULL,
  `Auditor` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateBy` varchar(255) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(255) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `SellOrderList`
--

DROP TABLE IF EXISTS `SellOrderList`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `SellOrderList` (
  `Id` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `OrderId` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `ProductName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `MO` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Qty` int NOT NULL,
  `Weight` decimal(18,2) DEFAULT NULL,
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateBy` varchar(255) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(255) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `sys_api`
--

DROP TABLE IF EXISTS `sys_api`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `sys_api` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(64) COLLATE utf8mb4_general_ci NOT NULL,
  `Method` varchar(10) COLLATE utf8mb4_general_ci NOT NULL,
  `Path` varchar(200) COLLATE utf8mb4_general_ci NOT NULL,
  `GroupPath` varchar(200) COLLATE utf8mb4_general_ci NOT NULL,
  `Name` varchar(200) COLLATE utf8mb4_general_ci NOT NULL,
  `Author` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` tinyint(1) DEFAULT '1',
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateTime` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_api_code` (`Code`),
  KEY `idx_api_group` (`GroupPath`),
  KEY `idx_api_path` (`Path`)
) ENGINE=InnoDB AUTO_INCREMENT=442 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='接口表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_City`
--

DROP TABLE IF EXISTS `Sys_City`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_City` (
  `CityId` int NOT NULL AUTO_INCREMENT,
  `CityCode` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CityName` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ProvinceCode` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`CityId`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=346 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `sys_config`
--

DROP TABLE IF EXISTS `sys_config`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `sys_config` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `Status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active' COMMENT '业务状态',
  `Enable` tinyint DEFAULT '1' COMMENT '启用状态',
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `ConfigKey` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '参数键',
  `ConfigValue` text COLLATE utf8mb4_general_ci NOT NULL COMMENT '参数值',
  `ValueType` enum('string','number','boolean','json') COLLATE utf8mb4_general_ci DEFAULT 'string' COMMENT '值类型',
  `Description` text COLLATE utf8mb4_general_ci COMMENT '参数说明',
  `IsSystem` tinyint(1) DEFAULT '0' COMMENT '是否系统级（不可删除）',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0' COMMENT '软删除标记(框架标准列)',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '有效标志(框架标准列)',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_config_key` (`ConfigKey`),
  KEY `idx_value_type` (`ValueType`),
  KEY `idx_is_system` (`IsSystem`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='系统参数';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_Dictionary`
--

DROP TABLE IF EXISTS `Sys_Dictionary`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_Dictionary` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '稳定标识（随机唯一，关联键）',
  `Config` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DBServer` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `DbSql` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `DicName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `DicNo` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '字典编码（可选，仅用于显示）',
  `IsValid` tinyint NOT NULL DEFAULT '1' COMMENT '有效标志（1=有效，0=无效）',
  `IsDeleted` tinyint NOT NULL DEFAULT '0' COMMENT '删除标志（1=已删除）',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人',
  `UpdateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `ParentCode` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '父节点 Code（根节点为 NULL）',
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  PRIMARY KEY (`Id`) USING BTREE,
  UNIQUE KEY `uk_sys_dictionary_code` (`Code`),
  UNIQUE KEY `uk_sys_dictionary_dicno` (`DicNo`),
  KEY `idx_sys_dictionary_pcode` (`ParentCode`)
) ENGINE=InnoDB AUTO_INCREMENT=153 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_DictionaryList`
--

DROP TABLE IF EXISTS `Sys_DictionaryList`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_DictionaryList` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '稳定标识（随机唯一，定位键）',
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DicName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DicValue` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DicCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属字典 Code（= Sys_Dictionary.Code）',
  `IsValid` tinyint NOT NULL DEFAULT '1' COMMENT '有效标志（1=有效，0=无效）',
  `IsDeleted` tinyint NOT NULL DEFAULT '0' COMMENT '删除标志（1=已删除）',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人',
  `UpdateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `Color` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE,
  UNIQUE KEY `uk_sys_dictionarylist_code` (`Code`),
  KEY `idx_sys_dictionarylist_dicode` (`DicCode`)
) ENGINE=InnoDB AUTO_INCREMENT=692 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `sys_log`
--

DROP TABLE IF EXISTS `sys_log`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `sys_log` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `Status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active' COMMENT '业务状态',
  `Enable` tinyint DEFAULT '1' COMMENT '启用状态',
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `UserId` bigint DEFAULT NULL COMMENT '操作用户ID',
  `Module` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '操作模块',
  `Action` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '操作动作',
  `TargetType` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '操作对象类型（表名）',
  `TargetId` bigint DEFAULT NULL COMMENT '操作对象ID',
  `Detail` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '操作详情',
  `IpAddress` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '操作IP',
  `UserAgent` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '用户代理',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_user_id` (`UserId`),
  KEY `idx_module` (`Module`),
  KEY `idx_action` (`Action`),
  KEY `idx_create_time` (`CreateTime`)
) ENGINE=InnoDB AUTO_INCREMENT=5285 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='系统日志';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_Menu`
--

DROP TABLE IF EXISTS `Sys_Menu`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_Menu` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务唯一编码',
  `ParentCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL DEFAULT '0',
  `MenuName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `Auth` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `Icon` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Description` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `Url` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateTime` datetime DEFAULT NULL COMMENT '创建时间',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '创建人',
  `UpdateTime` datetime DEFAULT NULL COMMENT '修改时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '修改人',
  `Tag` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '菜单分类标签：admin/auditor/enterprise/common',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0' COMMENT '软删除标记',
  `IsValid` int NOT NULL DEFAULT '1' COMMENT '启用/禁用',
  PRIMARY KEY (`Id`) USING BTREE,
  UNIQUE KEY `uk_menu_code` (`Code`)
) ENGINE=InnoDB AUTO_INCREMENT=227 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_Organization`
--

DROP TABLE IF EXISTS `Sys_Organization`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_Organization` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `OrgName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL,
  `OrgCode` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ParentCode` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `OrgType` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'Dept',
  `OrgLevel` int DEFAULT NULL,
  `OrgPath` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `LeaderName` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `LeaderPhone` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Sort` int DEFAULT '0',
  `IsValid` tinyint NOT NULL DEFAULT '1' COMMENT '有效标志: 1=有效, 0=无效',
  `Remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsDeleted` tinyint DEFAULT '0',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `Code` (`Code`),
  KEY `idx_parent_code` (`ParentCode`),
  KEY `idx_org_code` (`OrgCode`)
) ENGINE=InnoDB AUTO_INCREMENT=73 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_Province`
--

DROP TABLE IF EXISTS `Sys_Province`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_Province` (
  `ProvinceId` int NOT NULL AUTO_INCREMENT,
  `ProvinceCode` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `ProvinceName` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `RegionCode` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`ProvinceId`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=44 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_QuartzLog`
--

DROP TABLE IF EXISTS `Sys_QuartzLog`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_QuartzLog` (
  `LogId` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `TaskName` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '任务名称',
  `ElapsedTime` int DEFAULT NULL COMMENT '耗时(秒)',
  `StratDate` datetime DEFAULT NULL COMMENT '开始时间',
  `EndDate` datetime DEFAULT NULL COMMENT '结束时间',
  `Result` int DEFAULT NULL COMMENT '执行结果',
  `ResponseContent` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '返回内容',
  `ErrorMsg` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  PRIMARY KEY (`LogId`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_QuartzOptions`
--

DROP TABLE IF EXISTS `Sys_QuartzOptions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_QuartzOptions` (
  `Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `TaskName` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '任务名称',
  `GroupName` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '任务分组',
  `CronExpression` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT 'Corn表达式',
  `Method` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '请求方式',
  `ApiUrl` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT 'Url地址',
  `AuthKey` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `AuthValue` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Describe` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '描述',
  `LastRunTime` datetime DEFAULT NULL COMMENT '最后执行执行',
  `Status` int DEFAULT NULL COMMENT '运行状态',
  `PostData` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT 'post参数',
  `TimeOut` int DEFAULT NULL COMMENT '超时时间(秒)',
  `CreateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_Role`
--

DROP TABLE IF EXISTS `Sys_Role`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_Role` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务唯一编码',
  `ParentCode` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '父节点编码（树结构，根节点为 NULL）',
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `DeptName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeptId` int DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `ParentId` int NOT NULL,
  `RoleName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0' COMMENT '软删除标记(框架标准列)',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '有效标志(框架标准列)',
  PRIMARY KEY (`Id`) USING BTREE,
  UNIQUE KEY `uk_role_code` (`Code`),
  KEY `idx_role_parent_code` (`ParentCode`)
) ENGINE=InnoDB AUTO_INCREMENT=301 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `sys_role_api`
--

DROP TABLE IF EXISTS `sys_role_api`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `sys_role_api` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `RoleCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `ApiCode` varchar(64) COLLATE utf8mb4_general_ci NOT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_role_api` (`RoleCode`,`ApiCode`),
  KEY `idx_api_code` (`ApiCode`)
) ENGINE=InnoDB AUTO_INCREMENT=122 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='角色-接口关联表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_RoleAuth`
--

DROP TABLE IF EXISTS `Sys_RoleAuth`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_RoleAuth` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `AuthValue` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` text COLLATE utf8mb4_general_ci,
  `MenuId` int NOT NULL,
  `MenuCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '菜单编码',
  `UpdateBy` text COLLATE utf8mb4_general_ci,
  `UpdateTime` datetime DEFAULT NULL,
  `RoleId` int DEFAULT NULL,
  `RoleCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '角色编码',
  `UserId` int DEFAULT NULL,
  `UserCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '用户编码',
  PRIMARY KEY (`Id`) USING BTREE,
  KEY `idx_roleauth_menu_code` (`MenuCode`),
  KEY `idx_roleauth_role_code` (`RoleCode`),
  KEY `idx_roleauth_user_code` (`UserCode`)
) ENGINE=InnoDB AUTO_INCREMENT=368 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_RoleMenu`
--

DROP TABLE IF EXISTS `Sys_RoleMenu`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_RoleMenu` (
  `Id` varchar(64) COLLATE utf8mb4_general_ci NOT NULL COMMENT '主键',
  `RoleCode` varchar(64) COLLATE utf8mb4_general_ci NOT NULL COMMENT '角色编码（Sys_Role.Code）',
  `MenuCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '菜单编码（Sys_Menu.Code）',
  `OrderNo` int DEFAULT NULL COMMENT '排序号',
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `CreateBy` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '创建人',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_role_menu` (`RoleCode`,`MenuCode`),
  KEY `idx_rm_role` (`RoleCode`),
  KEY `idx_rm_menu` (`MenuCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='角色-菜单关联表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_RoleUser`
--

DROP TABLE IF EXISTS `Sys_RoleUser`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_RoleUser` (
  `Id` varchar(64) COLLATE utf8mb4_general_ci NOT NULL,
  `RoleCode` varchar(64) COLLATE utf8mb4_general_ci NOT NULL COMMENT '角色编码',
  `UserCode` varchar(64) COLLATE utf8mb4_general_ci NOT NULL COMMENT '用户编码',
  `OrderNo` int DEFAULT NULL COMMENT '排序号',
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `CreateBy` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_role_user` (`RoleCode`,`UserCode`),
  KEY `idx_role_code` (`RoleCode`),
  KEY `idx_user_code` (`UserCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='角色-用户关联表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_TableColumn`
--

DROP TABLE IF EXISTS `Sys_TableColumn`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_TableColumn` (
  `ColumnId` int NOT NULL AUTO_INCREMENT,
  `ApiInPut` int DEFAULT NULL,
  `ApiIsNull` int DEFAULT NULL,
  `ApiOutPut` int DEFAULT NULL,
  `ColSize` int DEFAULT NULL,
  `ColumnCNName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ColumnName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ColumnType` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `ColumnWidth` int DEFAULT NULL,
  `Columnformat` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DropNo` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `EditColNo` int DEFAULT NULL,
  `EditRowNo` int DEFAULT NULL,
  `EditType` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` int DEFAULT NULL,
  `IsColumnData` int DEFAULT NULL,
  `IsDisplay` int DEFAULT NULL,
  `IsImage` int DEFAULT NULL,
  `IsKey` int DEFAULT NULL,
  `IsNull` int DEFAULT NULL,
  `IsReadDataset` int DEFAULT NULL,
  `Maxlength` int DEFAULT NULL,
  `UpdateBy` longtext COLLATE utf8mb4_general_ci,
  `UpdateTime` datetime DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `Script` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `SearchColNo` int DEFAULT NULL,
  `SearchRowNo` int DEFAULT NULL,
  `SearchType` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Sortable` int DEFAULT NULL,
  `TableName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `TableId` int DEFAULT NULL,
  `Placeholder` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `AddDefaultValue` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UploadOption` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `SearchDateRange` int DEFAULT NULL,
  `SearchDefaultValue` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CustomValidate` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsUnique` int DEFAULT NULL,
  `SummaryType` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `HeaderFilter` int DEFAULT NULL,
  `TextAlign` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ShowOverflowTooltip` int DEFAULT NULL,
  `FixedColumn` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CalcColumn` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Text1` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Text2` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`ColumnId`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=1828 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_TableInfo`
--

DROP TABLE IF EXISTS `Sys_TableInfo`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_TableInfo` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `CnName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ColumnCNName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DBServer` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `DataTableType` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DetailCnName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DetailName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `EditorType` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` int DEFAULT NULL,
  `ExpressField` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `FolderName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Namespace` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `ParentId` int DEFAULT NULL,
  `RichText` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `SortName` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `TableName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `TableTrueName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UploadField` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UploadMaxCount` int DEFAULT NULL,
  `AsyncApi` int DEFAULT NULL,
  `Text1` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Text2` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `QuickQueryFields` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ShowDetail` int DEFAULT NULL,
  `FixedSearch` int DEFAULT NULL,
  `MainKeyField` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=89 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_User`
--

DROP TABLE IF EXISTS `Sys_User`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_User` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '业务唯一编码',
  `RoleId` int NOT NULL DEFAULT '0',
  `RoleName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `PhoneNo` varchar(11) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Remark` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Tel` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UserName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `UserPwd` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UserTrueName` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `DeptName` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeptId` int DEFAULT NULL,
  `Email` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsValid` tinyint NOT NULL DEFAULT '1' COMMENT '有效标志: 1=有效, 0=无效',
  `IsDeleted` tinyint NOT NULL DEFAULT '0' COMMENT '软删除标记',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `DeleteBy` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人Code',
  `UserType` tinyint NOT NULL DEFAULT '10' COMMENT '用户类型：1=超级管理员, 10=总管理员, 13=运维人员, 14=配置人员, 15=质量专员, 20=审核管理员, 21=审核组长, 22=普通审核员, 30=企业账号',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '机构编码（多租户隔离），NULL表示平台管理层',
  `OrgId` bigint DEFAULT NULL COMMENT '机构ID，关联cert_org_config.id',
  `ParentUserId` int DEFAULT NULL COMMENT '上级用户ID，用于企业子账号或审核员层级',
  `Gender` int DEFAULT NULL,
  `HeadImageUrl` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `IsRegregisterPhone` int DEFAULT NULL,
  `LastLoginDate` datetime DEFAULT NULL,
  `LastModifyPwdDate` datetime DEFAULT NULL,
  `Address` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `AppType` int DEFAULT NULL,
  `AuditDate` datetime DEFAULT NULL,
  `AuditStatus` int DEFAULT NULL,
  `Auditor` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `OrderNo` int DEFAULT NULL,
  `Token` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Mobile` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeptIds` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `WechatOpenid` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `WechatUnionid` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE,
  UNIQUE KEY `uk_wechat_openid` (`WechatOpenid`),
  UNIQUE KEY `uk_wechat_unionid` (`WechatUnionid`),
  UNIQUE KEY `uk_user_code` (`Code`),
  KEY `idx_sys_user_org_code` (`OrgCode`),
  KEY `idx_sys_user_user_type` (`UserType`),
  KEY `IX_Sys_User_IsDeleted` (`IsDeleted`)
) ENGINE=InnoDB AUTO_INCREMENT=3399 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `sys_user_permission`
--

DROP TABLE IF EXISTS `sys_user_permission`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `sys_user_permission` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `UserCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `ApiCode` varchar(64) COLLATE utf8mb4_general_ci NOT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateTime` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_user_api` (`UserCode`,`ApiCode`),
  KEY `idx_user_code` (`UserCode`)
) ENGINE=InnoDB AUTO_INCREMENT=1047 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='用户权限缓存表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_UserDepartment`
--

DROP TABLE IF EXISTS `Sys_UserDepartment`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_UserDepartment` (
  `Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `UserId` int NOT NULL,
  `DepartmentId` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `Enable` int NOT NULL,
  `CreateBy` varchar(255) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(255) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_WorkFlow`
--

DROP TABLE IF EXISTS `Sys_WorkFlow`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_WorkFlow` (
  `Id` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `WorkName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '流程名称',
  `WorkTable` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '表名',
  `WorkTableName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '功能菜单',
  `NodeConfig` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '节点信息',
  `LineConfig` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '连接配置',
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '备注',
  `Weight` int DEFAULT NULL COMMENT '权重',
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` tinyint DEFAULT NULL,
  `UpdateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `AuditingEdit` int DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_WorkFlowStep`
--

DROP TABLE IF EXISTS `Sys_WorkFlowStep`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_WorkFlowStep` (
  `Id` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `WorkFlowId` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StepId` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '流程节点Id',
  `StepName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '节点名称',
  `StepType` int DEFAULT NULL COMMENT '节点类型(1=按用户审批,2=按角色审批)',
  `StepValue` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '审批用户id或角色id',
  `OrderId` int DEFAULT NULL,
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '备注',
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` tinyint DEFAULT NULL,
  `UpdateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `NextStepIds` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `ParentId` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `AuditRefuse` int DEFAULT NULL,
  `AuditBack` int DEFAULT NULL,
  `AuditMethod` int DEFAULT NULL,
  `SendMail` int DEFAULT NULL,
  `Filters` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `StepAttrType` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Weight` int DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_WorkFlowTable`
--

DROP TABLE IF EXISTS `Sys_WorkFlowTable`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_WorkFlowTable` (
  `Id` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `WorkFlowId` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `WorkName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `WorkTableKey` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '表主键id',
  `WorkTable` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '表名',
  `WorkTableName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '表中文名',
  `CurrentOrderId` int DEFAULT NULL,
  `AuditStatus` int DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` tinyint DEFAULT NULL,
  `UpdateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `CurrentStepId` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StepName` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_WorkFlowTableAuditLog`
--

DROP TABLE IF EXISTS `Sys_WorkFlowTableAuditLog`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_WorkFlowTableAuditLog` (
  `Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `WorkFlowTableId` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `WorkFlowTableStepId` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StepId` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StepName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `AuditId` int DEFAULT NULL,
  `Auditor` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `AuditStatus` int DEFAULT NULL,
  `AuditResult` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `AuditDate` datetime DEFAULT NULL,
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateTime` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `Sys_WorkFlowTableStep`
--

DROP TABLE IF EXISTS `Sys_WorkFlowTableStep`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `Sys_WorkFlowTableStep` (
  `Id` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `WorkFlowTableId` varchar(36) COLLATE utf8mb4_general_ci NOT NULL,
  `WorkFlowId` varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StepId` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StepName` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `StepType` int DEFAULT NULL,
  `StepValue` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `OrderId` int DEFAULT NULL,
  `AuditId` int DEFAULT NULL COMMENT '审核人id',
  `Auditor` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '审核人',
  `AuditStatus` int DEFAULT NULL COMMENT '审核状态',
  `AuditDate` datetime DEFAULT NULL,
  `Remark` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Enable` tinyint DEFAULT NULL,
  `UpdateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `StepAttrType` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ParentId` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci,
  `NextStepId` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Weight` int DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `TestDb`
--

DROP TABLE IF EXISTS `TestDb`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `TestDb` (
  `Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `TestDbName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `TestDbContent` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `TestService`
--

DROP TABLE IF EXISTS `TestService`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `TestService` (
  `Id` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `DbName` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `DbContent` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT NULL,
  `CreateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci ROW_FORMAT=DYNAMIC;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Temporary view structure for view `v_cert_configured_rules`
--

DROP TABLE IF EXISTS `v_cert_configured_rules`;
/*!50001 DROP VIEW IF EXISTS `v_cert_configured_rules`*/;
SET @saved_cs_client     = @@character_set_client;
/*!50503 SET character_set_client = utf8mb4 */;
/*!50001 CREATE VIEW `v_cert_configured_rules` AS SELECT 
 1 AS `RuleCode`,
 1 AS `StandardFileCode`,
 1 AS `FileName`,
 1 AS `StandardCode`,
 1 AS `PhaseCode`,
 1 AS `Skill`,
 1 AS `DocIsValid`,
 1 AS `Status`,
 1 AS `CreateTime`,
 1 AS `UpdateTime`*/;
SET character_set_client = @saved_cs_client;

--
-- Temporary view structure for view `v_cert_phase_definition`
--

DROP TABLE IF EXISTS `v_cert_phase_definition`;
/*!50001 DROP VIEW IF EXISTS `v_cert_phase_definition`*/;
SET @saved_cs_client     = @@character_set_client;
/*!50503 SET character_set_client = utf8mb4 */;
/*!50001 CREATE VIEW `v_cert_phase_definition` AS SELECT 
 1 AS `Id`,
 1 AS `Code`,
 1 AS `PhaseCode`,
 1 AS `PhaseName`,
 1 AS `SequenceOrder`,
 1 AS `Description`,
 1 AS `IsValid`,
 1 AS `StatusName`,
 1 AS `CreateTime`,
 1 AS `CreateBy`,
 1 AS `UpdateTime`,
 1 AS `UpdateBy`,
 1 AS `DeleteTime`,
 1 AS `DeleteBy`,
 1 AS `IsDeleted`*/;
SET character_set_client = @saved_cs_client;

--
-- Temporary view structure for view `v_cert_stage`
--

DROP TABLE IF EXISTS `v_cert_stage`;
/*!50001 DROP VIEW IF EXISTS `v_cert_stage`*/;
SET @saved_cs_client     = @@character_set_client;
/*!50503 SET character_set_client = utf8mb4 */;
/*!50001 CREATE VIEW `v_cert_stage` AS SELECT 
 1 AS `Id`,
 1 AS `Code`,
 1 AS `StageCode`,
 1 AS `StageName`,
 1 AS `Description`,
 1 AS `SortOrder`,
 1 AS `Category`,
 1 AS `CategoryName`,
 1 AS `Status`,
 1 AS `StatusName`,
 1 AS `Remark`,
 1 AS `IsValid`,
 1 AS `CreateBy`,
 1 AS `CreateTime`,
 1 AS `UpdateBy`,
 1 AS `UpdateTime`,
 1 AS `DeleteBy`,
 1 AS `DeleteTime`,
 1 AS `IsDeleted`*/;
SET character_set_client = @saved_cs_client;

--
-- Temporary view structure for view `v_certification_body`
--

DROP TABLE IF EXISTS `v_certification_body`;
/*!50001 DROP VIEW IF EXISTS `v_certification_body`*/;
SET @saved_cs_client     = @@character_set_client;
/*!50503 SET character_set_client = utf8mb4 */;
/*!50001 CREATE VIEW `v_certification_body` AS SELECT 
 1 AS `Id`,
 1 AS `Code`,
 1 AS `OrgCode`,
 1 AS `Status`,
 1 AS `StatusName`,
 1 AS `IsValid`,
 1 AS `Sort`,
 1 AS `Remark`,
 1 AS `CreateBy`,
 1 AS `CreateTime`,
 1 AS `UpdateBy`,
 1 AS `UpdateTime`,
 1 AS `DeleteBy`,
 1 AS `DeleteTime`,
 1 AS `Name`,
 1 AS `ShortName`,
 1 AS `CbCode`,
 1 AS `ContactName`,
 1 AS `ContactPhone`,
 1 AS `LegalPerson`,
 1 AS `ContactEmail`,
 1 AS `Address`,
 1 AS `LogoUrl`,
 1 AS `ScopeText`,
 1 AS `ThemeConfig`,
 1 AS `LoginConfig`,
 1 AS `MaxUsers`,
 1 AS `MaxEnterprises`,
 1 AS `ExpireDate`*/;
SET character_set_client = @saved_cs_client;

--
-- Temporary view structure for view `v_iso_standard`
--

DROP TABLE IF EXISTS `v_iso_standard`;
/*!50001 DROP VIEW IF EXISTS `v_iso_standard`*/;
SET @saved_cs_client     = @@character_set_client;
/*!50503 SET character_set_client = utf8mb4 */;
/*!50001 CREATE VIEW `v_iso_standard` AS SELECT 
 1 AS `Id`,
 1 AS `Code`,
 1 AS `OrgCode`,
 1 AS `CbCode`,
 1 AS `CbName`,
 1 AS `StandardCode`,
 1 AS `StandardName`,
 1 AS `VersionYear`,
 1 AS `Category`,
 1 AS `CategoryName`,
 1 AS `Description`,
 1 AS `Status`,
 1 AS `StatusName`,
 1 AS `CreateBy`,
 1 AS `CreateTime`,
 1 AS `UpdateBy`,
 1 AS `UpdateTime`,
 1 AS `DeleteBy`,
 1 AS `DeleteTime`,
 1 AS `IsValid`,
 1 AS `Sort`,
 1 AS `Remark`,
 1 AS `ParentCode`,
 1 AS `IsLeaf`*/;
SET character_set_client = @saved_cs_client;

--
-- Temporary view structure for view `v_standard_directory_root_files`
--

DROP TABLE IF EXISTS `v_standard_directory_root_files`;
/*!50001 DROP VIEW IF EXISTS `v_standard_directory_root_files`*/;
SET @saved_cs_client     = @@character_set_client;
/*!50503 SET character_set_client = utf8mb4 */;
/*!50001 CREATE VIEW `v_standard_directory_root_files` AS SELECT 
 1 AS `Id`,
 1 AS `Code`,
 1 AS `FileCode`,
 1 AS `FileName`,
 1 AS `FileType`,
 1 AS `StoragePath`,
 1 AS `ConvertedStoragePath`,
 1 AS `ConvertStatus`,
 1 AS `ConvertMessage`,
 1 AS `UploadStatus`,
 1 AS `TaskId`,
 1 AS `DirectoryCode`,
 1 AS `FolderCode`,
 1 AS `IsValid`,
 1 AS `IsDeleted`,
 1 AS `FileSize`*/;
SET character_set_client = @saved_cs_client;

--
-- Temporary view structure for view `v_sys_user`
--

DROP TABLE IF EXISTS `v_sys_user`;
/*!50001 DROP VIEW IF EXISTS `v_sys_user`*/;
SET @saved_cs_client     = @@character_set_client;
/*!50503 SET character_set_client = utf8mb4 */;
/*!50001 CREATE VIEW `v_sys_user` AS SELECT 
 1 AS `Id`,
 1 AS `Code`,
 1 AS `UserName`,
 1 AS `UserTrueName`,
 1 AS `UserPwd`,
 1 AS `RoleId`,
 1 AS `IsValid`,
 1 AS `IsDeleted`,
 1 AS `DeleteTime`,
 1 AS `DeleteBy`,
 1 AS `OrgCode`,
 1 AS `Gender`,
 1 AS `PhoneNo`,
 1 AS `Email`,
 1 AS `HeadImageUrl`,
 1 AS `Address`,
 1 AS `Remark`,
 1 AS `LastLoginDate`,
 1 AS `LastModifyPwdDate`,
 1 AS `OrderNo`,
 1 AS `Token`,
 1 AS `CreateTime`,
 1 AS `CreateBy`,
 1 AS `UpdateTime`,
 1 AS `UpdateBy`,
 1 AS `DeptId`,
 1 AS `UserType`,
 1 AS `RoleName`,
 1 AS `OrgName`,
 1 AS `OrgType`,
 1 AS `OrgLevel`,
 1 AS `OrgPath`,
 1 AS `OrgLeaderName`,
 1 AS `OrgLeaderPhone`*/;
SET character_set_client = @saved_cs_client;

--
-- Temporary view structure for view `v_upload_task_detail`
--

DROP TABLE IF EXISTS `v_upload_task_detail`;
/*!50001 DROP VIEW IF EXISTS `v_upload_task_detail`*/;
SET @saved_cs_client     = @@character_set_client;
/*!50503 SET character_set_client = utf8mb4 */;
/*!50001 CREATE VIEW `v_upload_task_detail` AS SELECT 
 1 AS `TaskId`,
 1 AS `DirectoryCode`,
 1 AS `TotalFiles`,
 1 AS `SuccessCount`,
 1 AS `Status`,
 1 AS `ExpireTime`,
 1 AS `FileCode`,
 1 AS `FileName`,
 1 AS `UploadStatus`,
 1 AS `StoragePath`,
 1 AS `FileIsValid`,
 1 AS `FileIsDeleted`*/;
SET character_set_client = @saved_cs_client;

--
-- Temporary view structure for view `v_workflow`
--

DROP TABLE IF EXISTS `v_workflow`;
/*!50001 DROP VIEW IF EXISTS `v_workflow`*/;
SET @saved_cs_client     = @@character_set_client;
/*!50503 SET character_set_client = utf8mb4 */;
/*!50001 CREATE VIEW `v_workflow` AS SELECT 
 1 AS `Id`,
 1 AS `Code`,
 1 AS `OrgCode`,
 1 AS `WorkflowCode`,
 1 AS `WorkflowName`,
 1 AS `WorkflowType`,
 1 AS `WorkflowTypeName`,
 1 AS `WorkflowConfig`,
 1 AS `Version`,
 1 AS `IsActive`,
 1 AS `IsActiveName`,
 1 AS `Description`,
 1 AS `Status`,
 1 AS `StatusName`,
 1 AS `Sort`,
 1 AS `Remark`,
 1 AS `CreateTime`,
 1 AS `CreateBy`,
 1 AS `UpdateTime`,
 1 AS `UpdateBy`,
 1 AS `DeleteTime`,
 1 AS `DeleteBy`,
 1 AS `IsDeleted`,
 1 AS `IsValid`*/;
SET character_set_client = @saved_cs_client;

--
-- Table structure for table `wf_execution_task`
--

DROP TABLE IF EXISTS `wf_execution_task`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `wf_execution_task` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active' COMMENT '业务状态',
  `enable` tinyint DEFAULT '1' COMMENT '启用状态',
  `sort` int DEFAULT '0' COMMENT '排序号',
  `remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `TaskType` varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT '任务类型：TEST | NC_CHECK | REPORT_GENERATE',
  `TaskStatus` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'queued' COMMENT '执行状态：queued|executing|completed|failed|cancelled',
  `TestScope` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'FULL' COMMENT '测试范围：FULL（整流）| NODE（单节点）| AI_NODE（AI 节点），仅 TaskType=TEST 时有效',
  `ConfigSnapshot` json NOT NULL COMMENT '执行时的工作流配置快照（从cert_validation_rule.rule_json锁定）',
  `RuleCode` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT 'cert_validation_rule.rule_code（配置来源）',
  `EnterpriseCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '企业编码（运行时绑定）',
  `PhaseCode` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '审核阶段',
  `QueueCode` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT 'yzh_queue.queue_code（关联队列层）',
  `CacheKeys` text COLLATE utf8mb4_general_ci COMMENT '预热的缓存键列表（JSON数组，任务级缓存方案）',
  `ResultSummary` json DEFAULT NULL COMMENT '执行结果摘要（end节点输出）',
  `ErrorMessage` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '失败原因',
  `StartedAt` datetime DEFAULT NULL COMMENT '开始执行时间',
  `CompletedAt` datetime DEFAULT NULL COMMENT '完成时间',
  `DurationMs` int DEFAULT NULL COMMENT '执行耗时(ms)',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  KEY `idx_type_status` (`TaskType`,`TaskStatus`),
  KEY `idx_rule` (`RuleCode`),
  KEY `idx_enterprise` (`EnterpriseCode`),
  KEY `idx_phase` (`PhaseCode`),
  KEY `idx_queue` (`QueueCode`),
  KEY `idx_status` (`TaskStatus`),
  KEY `idx_test_scope` (`TaskType`,`TestScope`,`TaskStatus`)
) ENGINE=InnoDB AUTO_INCREMENT=38 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='工作流执行任务（TEST/NC_CHECK/REPORT_GENERATE）';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `wf_execution_task_item`
--

DROP TABLE IF EXISTS `wf_execution_task_item`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `wf_execution_task_item` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active' COMMENT '业务状态',
  `enable` tinyint DEFAULT '1' COMMENT '启用状态',
  `sort` int DEFAULT '0' COMMENT '排序号',
  `remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `TaskCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'wf_execution_task.code（所属任务）',
  `RuleCode` varchar(64) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'cert_validation_rule.rule_code（关联配置定义）',
  `ItemType` varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'NC_CHECK | REPORT_GENERATE',
  `ItemStatus` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'queued' COMMENT 'queued|executing|completed|failed|cancelled',
  `IsSuccess` tinyint DEFAULT NULL COMMENT '业务成功标志：1=成功 0=失败 NULL=未完成',
  `CacheKeys` text COLLATE utf8mb4_general_ci COMMENT '预热的缓存键列表（JSON数组，任务级缓存方案）',
  `ResultSummary` json DEFAULT NULL COMMENT '本项执行结果（end节点输出）',
  `ErrorMessage` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '失败原因',
  `StartedAt` datetime DEFAULT NULL COMMENT '开始执行时间',
  `CompletedAt` datetime DEFAULT NULL COMMENT '完成时间',
  `DurationMs` int DEFAULT NULL COMMENT '执行耗时(ms)',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  UNIQUE KEY `uk_task_rule` (`TaskCode`,`RuleCode`),
  KEY `idx_task` (`TaskCode`),
  KEY `idx_task_status` (`TaskCode`,`ItemStatus`),
  KEY `idx_rule` (`RuleCode`)
) ENGINE=InnoDB AUTO_INCREMENT=38 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='执行项（一个NC检查项 / 一个报告章节）';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `wf_field_label_mapping`
--

DROP TABLE IF EXISTS `wf_field_label_mapping`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `wf_field_label_mapping` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `LabelTag` varchar(500) COLLATE utf8mb4_general_ci NOT NULL COMMENT '字段标签，如 [ISO9001_企业基础资料_营业执照_企业名称]',
  `FieldCode` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '字段编码，如 iso9001.ent_base.biz_lic.Name',
  `StandardCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属标准编码',
  `ScopeLevel` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '层级路径，如 企业基础资料/营业执照',
  `DocumentName` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '所属文档名称',
  `FieldName` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '字段名称',
  `DataType` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '数据类型',
  `SkillCode` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Description` text COLLATE utf8mb4_general_ci COMMENT '说明',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_label_tag` (`LabelTag`),
  KEY `idx_field_code` (`FieldCode`),
  KEY `idx_standard_code` (`StandardCode`),
  KEY `idx_skill_code` (`SkillCode`),
  CONSTRAINT `fk_flm_skill` FOREIGN KEY (`SkillCode`) REFERENCES `wf_skill` (`Code`),
  CONSTRAINT `fk_flm_standard` FOREIGN KEY (`StandardCode`) REFERENCES `cert_iso_standard` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='字段标签映射';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `wf_node_execution`
--

DROP TABLE IF EXISTS `wf_node_execution`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `wf_node_execution` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active' COMMENT '业务状态',
  `enable` tinyint DEFAULT '1' COMMENT '启用状态',
  `sort` int DEFAULT '0' COMMENT '排序号',
  `remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `TaskCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'wf_execution_task.code',
  `ItemCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'wf_execution_task_item.code',
  `NodeId` varchar(64) COLLATE utf8mb4_general_ci NOT NULL COMMENT '节点ID（前端生成的 classCode_n序号）',
  `NodeType` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT 'start|end|skill|ai_node|logic|branch|docField|docTable',
  `NodeTitle` varchar(128) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '节点名称快照',
  `SkillCode` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT 'Skill编码（功能节点）',
  `ExecStatus` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'pending' COMMENT 'pending|executing|completed|failed|skipped',
  `OutputJson` json DEFAULT NULL COMMENT '节点输出（所有端口的JSON）',
  `ErrorMessage` varchar(1000) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '执行错误信息',
  `StartedAt` datetime DEFAULT NULL COMMENT '开始执行时间',
  `CompletedAt` datetime DEFAULT NULL COMMENT '完成时间',
  `ExecutionTimeMs` int DEFAULT NULL COMMENT '执行耗时(ms)',
  `IsReused` tinyint DEFAULT '0' COMMENT '0=新执行 1=复用了历史结果',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_code` (`code`),
  UNIQUE KEY `uk_task_item_node` (`TaskCode`,`ItemCode`,`NodeId`),
  KEY `idx_task_item` (`TaskCode`,`ItemCode`),
  KEY `idx_task` (`TaskCode`),
  KEY `idx_node_id` (`NodeId`),
  KEY `idx_exec_status` (`ExecStatus`)
) ENGINE=InnoDB AUTO_INCREMENT=117 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='节点执行状态（跨路径复用的核心载体）';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `wf_path_execution`
--

DROP TABLE IF EXISTS `wf_path_execution`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `wf_path_execution` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `TaskCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'wf_execution_task.Code',
  `ItemCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'wf_execution_task_item.Code',
  `PathIndex` int NOT NULL COMMENT '路径索引（从0开始）',
  `Status` varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'pending|executing|completed|failed',
  `NodeIds` json DEFAULT NULL COMMENT '路径节点ID列表（按执行顺序）',
  `ReusedCount` int NOT NULL DEFAULT '0' COMMENT '本路径复用的节点数（未真跑，取自跨路径结果池）',
  `FailedAtNodeId` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '失败节点ID',
  `ErrorMessage` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '失败原因',
  `OutputJson` json DEFAULT NULL COMMENT '路径最终输出（end 节点输出）',
  `DurationMs` int DEFAULT NULL COMMENT '路径耗时(ms)',
  `StartedAt` datetime DEFAULT NULL COMMENT '路径开始时间',
  `CompletedAt` datetime DEFAULT NULL COMMENT '路径完成时间',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '创建人Code',
  `CreateTime` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '更新人Code',
  `UpdateTime` datetime DEFAULT NULL COMMENT '更新时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0' COMMENT '软删除标记',
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人Code',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsValid` int NOT NULL DEFAULT '1' COMMENT '有效性',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_task_item_path` (`TaskCode`,`ItemCode`,`PathIndex`),
  KEY `idx_task_code` (`TaskCode`),
  KEY `idx_status` (`Status`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='工作流路径执行记录';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `wf_prompt_template`
--

DROP TABLE IF EXISTS `wf_prompt_template`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `wf_prompt_template` (
  `id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键',
  `code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '多租户组织编码',
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active' COMMENT '实体启用状态',
  `sort` int DEFAULT '0' COMMENT '排序',
  `remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `PromptCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '提示词编码（如 analyze_word_v1）',
  `PromptName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '提示词名称',
  `PromptType` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '类型：analyze/extract/verify/validate/report',
  `SkillTarget` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '适用技能：word/excel/pdf/all',
  `template` mediumtext COLLATE utf8mb4_general_ci COMMENT '提示词模板（支持占位符）',
  `description` text COLLATE utf8mb4_general_ci COMMENT '说明',
  `version` int NOT NULL DEFAULT '1' COMMENT '版本号',
  `IsActive` tinyint(1) NOT NULL DEFAULT '1' COMMENT '是否当前生效',
  `LastTestResult` text COLLATE utf8mb4_general_ci COMMENT '最后测试结果（JSON）',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_prompt_code` (`PromptCode`),
  KEY `idx_prompt_type` (`PromptType`),
  KEY `idx_prompt_active` (`IsActive`,`PromptType`)
) ENGINE=InnoDB AUTO_INCREMENT=20 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='Prompt模板表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `wf_skill`
--

DROP TABLE IF EXISTS `wf_skill`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `wf_skill` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '唯一编码',
  `SkillCode` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT 'Skill编码',
  `name` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT 'Skill名称',
  `SkillType` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'method' COMMENT '类型：method/api',
  `CategoryCode` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '分类编码',
  `SideEffect` tinyint(1) NOT NULL DEFAULT '0' COMMENT '是否有副作用',
  `description` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci COMMENT '说明',
  `PromptTemplate` text COLLATE utf8mb4_general_ci COMMENT 'Prompt模板',
  `IsActive` tinyint(1) NOT NULL DEFAULT '1' COMMENT '启用状态',
  `OutputStrict` tinyint(1) NOT NULL DEFAULT '1' COMMENT '输出严格模式',
  `ReturnType` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT 'json' COMMENT '返回类型',
  `version` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT '1.0' COMMENT '版本',
  `icon` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '图标',
  `color` varchar(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '颜色',
  `SortOrder` int NOT NULL DEFAULT '0' COMMENT '排序',
  `remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '有效标志(框架标准列,统一IIsValid契约)',
  `CreateTime` datetime DEFAULT NULL COMMENT '创建时间',
  `CreateBy` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '创建人Code',
  `UpdateTime` datetime DEFAULT NULL COMMENT '更新时间',
  `UpdateBy` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '更新人Code',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间（软删除）',
  `DeleteBy` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人Code',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_skill_code` (`SkillCode`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_skill_category` (`CategoryCode`)
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='Skill主表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `wf_skill_api`
--

DROP TABLE IF EXISTS `wf_skill_api`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `wf_skill_api` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `SkillCode` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `url` varchar(500) COLLATE utf8mb4_general_ci NOT NULL,
  `HttpMethod` varchar(10) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'POST',
  `headers` text COLLATE utf8mb4_general_ci COMMENT '请求头 JSON（值可含 $sys. 引用）',
  `AuthConfig` text COLLATE utf8mb4_general_ci,
  `ParamMapping` text COLLATE utf8mb4_general_ci,
  `ResponseMapping` text COLLATE utf8mb4_general_ci,
  `TimeoutSeconds` int NOT NULL DEFAULT '30',
  `enable` tinyint(1) NOT NULL DEFAULT '1',
  `CreateTime` datetime DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_skill_api` (`SkillCode`),
  CONSTRAINT `fk_api_skill` FOREIGN KEY (`SkillCode`) REFERENCES `wf_skill` (`SkillCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='api 型 Skill 信息（1:1，预留）';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `wf_skill_category`
--

DROP TABLE IF EXISTS `wf_skill_category`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `wf_skill_category` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `ParentCode` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '父节点编码（树结构）',
  `CategoryCode` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '分类编码（与 wf_skill.category 对应）',
  `Name` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '分类名称',
  `icon` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '图标',
  `color` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '颜色',
  `SortOrder` int NOT NULL DEFAULT '0' COMMENT '排序',
  `remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '有效标志(框架标准列,统一IIsValid契约)',
  `CreateTime` datetime DEFAULT NULL COMMENT '创建时间',
  `CreateBy` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '创建人Code',
  `UpdateTime` datetime DEFAULT NULL COMMENT '更新时间',
  `UpdateBy` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '更新人Code',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间（软删除）',
  `DeleteBy` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '删除人Code',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_skill_category_code` (`CategoryCode`)
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='Skill 分类（基础资料维护：面板分组 + 页面左侧导航）';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `wf_skill_input`
--

DROP TABLE IF EXISTS `wf_skill_input`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `wf_skill_input` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `SkillCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `InputName` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `InputLabel` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `InputType` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'text' COMMENT 'text/number/date/boolean/enum/field_ref/table_ref/json',
  `EnumValues` text COLLATE utf8mb4_general_ci,
  `IsRequired` tinyint(1) NOT NULL DEFAULT '0',
  `DefaultValue` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `BindMode` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'LinkOrConstant' COMMENT '绑定模式：Link/LinkOrConstant/Enum',
  `EnumSource` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '字典编码（BindMode=Enum 时必填），对应 Sys_Dictionary.DicNo',
  `SortOrder` int NOT NULL DEFAULT '0',
  `enable` tinyint(1) NOT NULL DEFAULT '1',
  `CreateTime` datetime DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '有效标志(框架标准列,统一IIsValid契约)',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_skill_input` (`SkillCode`,`InputName`),
  KEY `idx_skill_input_skill` (`SkillCode`)
) ENGINE=InnoDB AUTO_INCREMENT=40 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='Skill 输入表单模板（画布生成输入表单用，非硬校验）';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `wf_skill_output`
--

DROP TABLE IF EXISTS `wf_skill_output`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `wf_skill_output` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `SkillCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `OutputName` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `OutputType` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'json' COMMENT 'string/number/date/boolean/json',
  `OutputPrompt` text COLLATE utf8mb4_general_ci COMMENT '输出解读提示词（解释器组装用）',
  `description` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `SortOrder` int NOT NULL DEFAULT '0',
  `enable` tinyint(1) NOT NULL DEFAULT '1',
  `CreateTime` datetime DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '有效标志(框架标准列,统一IIsValid契约)',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_skill_output` (`SkillCode`,`OutputName`),
  KEY `idx_skill_output_skill` (`SkillCode`)
) ENGINE=InnoDB AUTO_INCREMENT=36 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='强约束 Skill 输出契约（output_strict=1 时解释器强校验）';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `wf_skill_reflection`
--

DROP TABLE IF EXISTS `wf_skill_reflection`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `wf_skill_reflection` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `SkillCode` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属 Skill（与 wf_skill.skill_code 同 collation）',
  `ClassPath` varchar(500) COLLATE utf8mb4_general_ci NOT NULL COMMENT '反射的地址（类型全名）',
  `MethodName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'ExecuteAsync' COMMENT '反射的方法',
  `ParamBinding` text COLLATE utf8mb4_general_ci COMMENT '参数绑定 JSON: {"输入项名":"方法参数名或顺序"}',
  `enable` tinyint(1) NOT NULL DEFAULT '1',
  `CreateTime` datetime DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'active',
  `remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '有效标志(框架标准列,统一IIsValid契约)',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_skill_reflection` (`SkillCode`),
  UNIQUE KEY `uk_class_method` (`ClassPath`,`MethodName`),
  CONSTRAINT `fk_reflection_skill` FOREIGN KEY (`SkillCode`) REFERENCES `wf_skill` (`SkillCode`)
) ENGINE=InnoDB AUTO_INCREMENT=21 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='method 型 Skill 反射信息（1:1）';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `wf_workflow_definition`
--

DROP TABLE IF EXISTS `wf_workflow_definition`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `wf_workflow_definition` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateTime` datetime DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `Status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `WorkflowCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '工作流编码',
  `WorkflowName` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '工作流名称',
  `WorkflowType` enum('extraction','validation','report') COLLATE utf8mb4_general_ci NOT NULL COMMENT '工作流类型',
  `WorkflowConfig` json NOT NULL COMMENT '工作流DAG配置（节点+边+参数）',
  `Version` int DEFAULT '1' COMMENT '版本号',
  `IsActive` tinyint(1) DEFAULT '1' COMMENT '是否启用',
  `Description` text COLLATE utf8mb4_general_ci COMMENT '说明',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_workflow_code` (`WorkflowCode`),
  KEY `idx_workflow_type` (`WorkflowType`),
  KEY `idx_is_active` (`IsActive`),
  KEY `idx_version` (`Version`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='工作流定义';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `wf_workflow_execution_log`
--

DROP TABLE IF EXISTS `wf_workflow_execution_log`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `wf_workflow_execution_log` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码（GUID）',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '组织编码',
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `status` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `enable` tinyint DEFAULT NULL,
  `Sort` int DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '备注',
  `WorkflowCode` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '工作流定义编码',
  `Workflowversion` int NOT NULL COMMENT '执行时的工作流版本',
  `BusinessType` enum('audit_task','report_task','file_upload') COLLATE utf8mb4_general_ci NOT NULL COMMENT '业务场景类型',
  `BusinessId` bigint NOT NULL COMMENT '关联的业务ID（审核任务ID/报告任务ID/文件ID）',
  `NodeId` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '节点ID',
  `SkillCode` varchar(100) COLLATE utf8mb4_general_ci NOT NULL COMMENT '执行的Skill',
  `InputData` json DEFAULT NULL COMMENT '实际输入数据',
  `OutputData` json DEFAULT NULL COMMENT '实际输出数据',
  `ErrorMsg` text COLLATE utf8mb4_general_ci COMMENT '错误信息',
  `DurationMs` int DEFAULT NULL COMMENT '耗时（毫秒）',
  `StartedAt` datetime NOT NULL COMMENT '开始时间',
  `CompletedAt` datetime DEFAULT NULL COMMENT '完成时间',
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_workflow_code` (`WorkflowCode`),
  KEY `idx_business_type` (`BusinessType`),
  KEY `idx_business_id` (`BusinessId`),
  KEY `idx_node_id` (`NodeId`),
  KEY `idx_status` (`status`),
  KEY `idx_started_at` (`StartedAt`),
  CONSTRAINT `fk_wlog_workflow` FOREIGN KEY (`WorkflowCode`) REFERENCES `wf_workflow_definition` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='工作流执行日志';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `yzh_field_config`
--

DROP TABLE IF EXISTS `yzh_field_config`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `yzh_field_config` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `PageKey` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `FieldName` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `FieldAlias` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `XsFlag` tinyint DEFAULT '1',
  `ColumnSxh` int DEFAULT '0',
  `ColumnTitle` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ColumnWidth` int DEFAULT '120',
  `ColumnFixed` varchar(10) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `sortable` tinyint DEFAULT '1',
  `ColumnFormatter` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ShowOverflow` tinyint DEFAULT '1',
  `align` varchar(10) COLLATE utf8mb4_general_ci DEFAULT 'left',
  `BcFlag` tinyint DEFAULT '1',
  `FormTitle` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `ControlType` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'input',
  `GridRow` int DEFAULT '0',
  `GridCol` int DEFAULT '0',
  `GridRowSpan` int DEFAULT '1',
  `GridColSpan` int DEFAULT '1',
  `required` tinyint DEFAULT '0',
  `maxlength` int DEFAULT '0',
  `placeholder` varchar(200) COLLATE utf8mb4_general_ci DEFAULT '',
  `DefaultValue` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `readonly` tinyint DEFAULT '0',
  `disabled` tinyint DEFAULT '0',
  `precision` int DEFAULT NULL,
  `MinVal` decimal(18,6) DEFAULT NULL,
  `MaxVal` decimal(18,6) DEFAULT NULL,
  `TextareaRows` int DEFAULT '3',
  `DataKey` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `RemoteUrl` varchar(255) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `GroupIndex` int DEFAULT '0',
  `SearchFlag` tinyint DEFAULT '0',
  `SearchTitle` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `SearchPlaceholder` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `SearchControlType` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `SearchWidth` int DEFAULT '180',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreatedAt` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT '',
  `Code` varchar(64) COLLATE utf8mb4_general_ci NOT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `code` (`Code`),
  UNIQUE KEY `uk_page_field` (`PageKey`,`FieldName`,`OrgCode`),
  KEY `idx_page_key` (`PageKey`),
  KEY `idx_field_name` (`FieldName`)
) ENGINE=InnoDB AUTO_INCREMENT=49 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `yzh_page_config`
--

DROP TABLE IF EXISTS `yzh_page_config`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `yzh_page_config` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `PageKey` varchar(50) COLLATE utf8mb4_general_ci NOT NULL,
  `PageTitle` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `EntityName` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `TableName` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `ControllerName` varchar(100) COLLATE utf8mb4_general_ci NOT NULL,
  `KeyField` varchar(50) COLLATE utf8mb4_general_ci DEFAULT 'Id',
  `KeyFieldType` varchar(10) COLLATE utf8mb4_general_ci DEFAULT 'number',
  `SortField` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `SortOrder` varchar(5) COLLATE utf8mb4_general_ci DEFAULT 'desc',
  `DialogWidth` int DEFAULT '960',
  `DialogMaxHeight` varchar(20) COLLATE utf8mb4_general_ci DEFAULT '85vh',
  `DialogLabelWidth` int DEFAULT '120',
  `RowHeight` varchar(10) COLLATE utf8mb4_general_ci DEFAULT 'default',
  `stripe` tinyint DEFAULT '1',
  `ShowRowNumber` tinyint DEFAULT '1',
  `SearchMode` varchar(10) COLLATE utf8mb4_general_ci DEFAULT 'fixed',
  `VisibleButtons` text COLLATE utf8mb4_general_ci,
  `ShowActionColumn` tinyint DEFAULT '1',
  `CheckboxSelection` tinyint DEFAULT '1',
  `IncrementalUpdate` tinyint DEFAULT '1',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsActive` tinyint DEFAULT '1',
  `CreatedAt` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdatedAt` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT '',
  `Code` varchar(64) COLLATE utf8mb4_general_ci NOT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `code` (`Code`),
  UNIQUE KEY `uk_page_org` (`PageKey`,`OrgCode`),
  KEY `idx_page_key` (`PageKey`)
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `yzh_queue`
--

DROP TABLE IF EXISTS `yzh_queue`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `yzh_queue` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码(GUID)，表间关联用',
  `QueueCode` varchar(64) COLLATE utf8mb4_general_ci NOT NULL COMMENT '队列业务编码：Q-{yyyyMMdd}-{6位随机}',
  `QueueType` varchar(30) COLLATE utf8mb4_general_ci NOT NULL COMMENT '队列类型：file_convert/auto_verify/report_generate',
  `QueueName` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '队列名称（人话）',
  `ScopeKey` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '范围键（按类型约定格式，如 file_convert=机构|标准|阶段）',
  `ScopeInfo` json DEFAULT NULL COMMENT '冗余展示数据 JSON',
  `SourceType` varchar(30) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '来源类型：upload_task/verify_req/report_req',
  `SourceId` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '来源ID：上传任务taskId等',
  `Status` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'pending' COMMENT 'pending/running/completed/failed/cancelled',
  `TotalCount` int DEFAULT '0' COMMENT '子任务总数',
  `PendingCount` int DEFAULT '0',
  `ProcessingCount` int DEFAULT '0',
  `CompletedCount` int DEFAULT '0',
  `FailedCount` int DEFAULT '0',
  `CancelledCount` int DEFAULT '0',
  `Progress` int DEFAULT '0' COMMENT '0-100',
  `StartTime` datetime DEFAULT NULL,
  `EndTime` datetime DEFAULT NULL,
  `Remark` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '机构编码（多租户）',
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_queue_code` (`QueueCode`),
  UNIQUE KEY `uk_source` (`SourceType`,`SourceId`),
  KEY `idx_scope_status` (`QueueType`,`ScopeKey`,`Status`),
  KEY `idx_status` (`Status`),
  KEY `idx_create_date` (`CreateTime`)
) ENGINE=InnoDB AUTO_INCREMENT=45 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='yzh队列主表（通用队列中心）';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `yzh_queue_resource_lock`
--

DROP TABLE IF EXISTS `yzh_queue_resource_lock`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `yzh_queue_resource_lock` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码(GUID)',
  `QueueCode` varchar(64) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属队列编码',
  `ResourceTable` varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT '资源表名（如 cert_standard_directory_file / 任意业务表）',
  `ResourceCode` varchar(200) COLLATE utf8mb4_general_ci NOT NULL COMMENT '资源唯一编码',
  `ResourceName` varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '资源名称快照',
  `TaskNo` int DEFAULT NULL COMMENT '占用该资源的子任务序号（NULL=队列级锁）',
  `Status` varchar(20) COLLATE utf8mb4_general_ci DEFAULT 'locked' COMMENT 'locked/released',
  `ActiveKey` varchar(260) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '活跃锁键:{resource_table}|{resource_code}，释放时置NULL；uk_active唯一索引实现同一资源同时仅一个活跃锁',
  `ReleaseTime` datetime DEFAULT NULL,
  `ExpireAt` datetime DEFAULT NULL COMMENT '锁租约安全网',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0',
  `IsValid` int NOT NULL DEFAULT '1',
  `LockTime` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_active` (`ActiveKey`),
  KEY `idx_queue` (`QueueCode`,`Status`),
  KEY `idx_locked` (`ResourceTable`,`ResourceCode`,`Status`),
  KEY `idx_expire` (`Status`,`ExpireAt`)
) ENGINE=InnoDB AUTO_INCREMENT=4369 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='yzh队列资源锁定表';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `yzh_queue_task`
--

DROP TABLE IF EXISTS `yzh_queue_task`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `yzh_queue_task` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT '全局唯一编码(GUID)',
  `QueueCode` varchar(64) COLLATE utf8mb4_general_ci NOT NULL COMMENT '所属队列编码(yzh_queue.queue_code)',
  `TaskType` varchar(30) COLLATE utf8mb4_general_ci NOT NULL COMMENT '任务类型：file_convert/auto_verify/report_generate',
  `Payload` text COLLATE utf8mb4_general_ci COMMENT '业务数据 JSON（file_convert={fileCode,fileName,sourcePath,targetPath,convertType}）',
  `Status` varchar(20) COLLATE utf8mb4_general_ci NOT NULL DEFAULT 'pending' COMMENT 'pending/processing/completed/failed/cancelled',
  `ErrorType` varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '错误分类: retryable(可重试)/permanent(永久)',
  `ErrorMessage` varchar(2000) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '错误信息',
  `RetryCount` int DEFAULT '0',
  `MaxRetryCount` int DEFAULT '3',
  `NextRetryAt` datetime DEFAULT NULL COMMENT '下次重试时间(指数退避+抖动)',
  `LockedUntil` datetime DEFAULT NULL COMMENT '领取租约到期时间(worker续期,到期可被重新领取)',
  `LockedAt` datetime DEFAULT NULL COMMENT '领取时间',
  `LockedBy` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '领取 Worker 标识',
  `ProcessTime` datetime DEFAULT NULL COMMENT '开始处理时间',
  `CompleteTime` datetime DEFAULT NULL COMMENT '完成/失败/取消时间',
  `TaskId` varchar(64) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '来源批次ID（如上传任务taskId）',
  `UserId` int DEFAULT NULL,
  `UserName` varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '发起用户名',
  `OrgCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '机构编码',
  `Priority` int DEFAULT '0' COMMENT '优先级（0=普通，10=高优先）',
  `LockCodes` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT '本任务持有的资源锁编码(逗号分隔，对应 yzh_queue_resource_lock.code)',
  `CreateTime` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdateTime` datetime DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `CreateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `UpdateBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `DeleteBy` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0' COMMENT '软删除标记(框架标准列)',
  `IsValid` tinyint(1) NOT NULL DEFAULT '1' COMMENT '有效标志(框架标准列,统一IIsValid契约)',
  `UserCode` varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_queue` (`QueueCode`),
  KEY `idx_claim` (`Status`,`NextRetryAt`),
  KEY `idx_lease` (`Status`,`LockedUntil`),
  KEY `idx_task_id` (`TaskId`),
  KEY `idx_type` (`TaskType`)
) ENGINE=InnoDB AUTO_INCREMENT=5667 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='yzh队列子任务表（通用任务）';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping routines for database 'yzh_cert_platform'
--
/*!50003 DROP PROCEDURE IF EXISTS `p_safe_change_column` */;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8mb4 */ ;
/*!50003 SET character_set_results = utf8mb4 */ ;
/*!50003 SET collation_connection  = utf8mb4_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
CREATE DEFINER=`root`@`localhost` PROCEDURE `p_safe_change_column`(
  IN p_table   VARCHAR(64),
  IN p_old_col VARCHAR(64),
  IN p_new_col VARCHAR(64),
  IN p_def     VARCHAR(500)
)
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE table_schema = DATABASE() AND table_name = p_table AND column_name = p_old_col) THEN
    SET @sql = CONCAT('ALTER TABLE `', p_table, '` CHANGE COLUMN `', p_old_col, '` `', p_new_col, '` ', p_def);
    PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    SELECT CONCAT('Renamed ', p_table, '.', p_old_col, ' → ', p_new_col) AS result;
  ELSEIF EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE table_schema = DATABASE() AND table_name = p_table AND column_name = p_new_col) THEN
    SELECT CONCAT('Skipped: ', p_table, ' already has ', p_new_col) AS result;
  ELSE
    SELECT CONCAT('Error: ', p_table, ' has neither ', p_old_col, ' nor ', p_new_col) AS result;
  END IF;
END ;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;
/*!50003 DROP PROCEDURE IF EXISTS `p_safe_drop_column` */;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8mb4 */ ;
/*!50003 SET character_set_results = utf8mb4 */ ;
/*!50003 SET collation_connection  = utf8mb4_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
CREATE DEFINER=`root`@`localhost` PROCEDURE `p_safe_drop_column`(
  IN p_table VARCHAR(64),
  IN p_col   VARCHAR(64)
)
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.COLUMNS WHERE table_schema = DATABASE() AND table_name = p_table AND column_name = p_col) THEN
    SET @sql = CONCAT('ALTER TABLE `', p_table, '` DROP COLUMN `', p_col, '`');
    PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    SELECT CONCAT('Dropped ', p_table, '.', p_col) AS result;
  ELSE
    SELECT CONCAT('Skipped: ', p_table, '.', p_col, ' does not exist') AS result;
  END IF;
END ;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;
/*!50003 DROP PROCEDURE IF EXISTS `p_safe_drop_index` */;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8mb4 */ ;
/*!50003 SET character_set_results = utf8mb4 */ ;
/*!50003 SET collation_connection  = utf8mb4_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
CREATE DEFINER=`root`@`localhost` PROCEDURE `p_safe_drop_index`(
  IN p_table VARCHAR(64),
  IN p_index VARCHAR(64)
)
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.STATISTICS WHERE table_schema = DATABASE() AND table_name = p_table AND index_name = p_index) THEN
    SET @sql = CONCAT('ALTER TABLE `', p_table, '` DROP INDEX `', p_index, '`');
    PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    SELECT CONCAT('Dropped index ', p_index, ' from ', p_table) AS result;
  ELSE
    SELECT CONCAT('Skipped: index ', p_index, ' not found on ', p_table) AS result;
  END IF;
END ;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;
/*!50003 DROP PROCEDURE IF EXISTS `safe_add_column` */;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8mb4 */ ;
/*!50003 SET character_set_results = utf8mb4 */ ;
/*!50003 SET collation_connection  = utf8mb4_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
CREATE DEFINER=`root`@`localhost` PROCEDURE `safe_add_column`(
  IN p_table_name  VARCHAR(100),
  IN p_column_name VARCHAR(100),
  IN p_column_def  TEXT,
  IN p_comment     VARCHAR(500)
)
BEGIN
  DECLARE col_count INT;
  SELECT COUNT(*) INTO col_count
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = p_table_name
    AND COLUMN_NAME  = p_column_name;

  IF col_count = 0 THEN
    SET @sql = CONCAT('ALTER TABLE `', p_table_name, '` ADD COLUMN `', p_column_name, '` ', p_column_def, " COMMENT '", p_comment, "'");
    PREPARE stmt FROM @sql;
    EXECUTE stmt;
    DEALLOCATE PREPARE stmt;
  END IF;
END ;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;
/*!50003 DROP PROCEDURE IF EXISTS `safe_add_index` */;
/*!50003 SET @saved_cs_client      = @@character_set_client */ ;
/*!50003 SET @saved_cs_results     = @@character_set_results */ ;
/*!50003 SET @saved_col_connection = @@collation_connection */ ;
/*!50003 SET character_set_client  = utf8mb4 */ ;
/*!50003 SET character_set_results = utf8mb4 */ ;
/*!50003 SET collation_connection  = utf8mb4_general_ci */ ;
/*!50003 SET @saved_sql_mode       = @@sql_mode */ ;
/*!50003 SET sql_mode              = 'ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION' */ ;
DELIMITER ;;
CREATE DEFINER=`root`@`localhost` PROCEDURE `safe_add_index`(
  IN p_table_name    VARCHAR(100),
  IN p_index_name    VARCHAR(100),
  IN p_index_columns TEXT
)
BEGIN
  DECLARE idx_count INT;
  SELECT COUNT(*) INTO idx_count
  FROM information_schema.STATISTICS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = p_table_name
    AND INDEX_NAME   = p_index_name;

  IF idx_count = 0 THEN
    SET @sql = CONCAT('ALTER TABLE `', p_table_name, '` ADD INDEX `', p_index_name, '` (', p_index_columns, ')');
    PREPARE stmt FROM @sql;
    EXECUTE stmt;
    DEALLOCATE PREPARE stmt;
  END IF;
END ;;
DELIMITER ;
/*!50003 SET sql_mode              = @saved_sql_mode */ ;
/*!50003 SET character_set_client  = @saved_cs_client */ ;
/*!50003 SET character_set_results = @saved_cs_results */ ;
/*!50003 SET collation_connection  = @saved_col_connection */ ;

--
-- Final view structure for view `v_cert_configured_rules`
--

/*!50001 DROP VIEW IF EXISTS `v_cert_configured_rules`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_0900_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `v_cert_configured_rules` AS select `r`.`Code` AS `RuleCode`,`r`.`StandardFileCode` AS `StandardFileCode`,coalesce(`f`.`FileName`,`r`.`StandardFileCode`) AS `FileName`,coalesce(`r`.`StandardCode`,'') AS `StandardCode`,coalesce(`r`.`PhaseCode`,'') AS `PhaseCode`,`r`.`Skill` AS `Skill`,`r`.`DocIsValid` AS `DocIsValid`,`r`.`Status` AS `Status`,`r`.`CreateTime` AS `CreateTime`,`r`.`UpdateTime` AS `UpdateTime` from (`cert_doc_extraction_rule` `r` left join `cert_standard_directory_file` `f` on(((`r`.`StandardFileCode` = `f`.`FileCode`) and (`f`.`IsDeleted` = 0)))) where ((`r`.`IsDeleted` = 0) and (`r`.`IsValid` = 1)) */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `v_cert_phase_definition`
--

/*!50001 DROP VIEW IF EXISTS `v_cert_phase_definition`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_0900_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `v_cert_phase_definition` AS select `p`.`Id` AS `Id`,`p`.`Code` AS `Code`,`p`.`PhaseCode` AS `PhaseCode`,`p`.`PhaseName` AS `PhaseName`,`p`.`SequenceOrder` AS `SequenceOrder`,`p`.`Description` AS `Description`,`p`.`IsValid` AS `IsValid`,((case `p`.`IsValid` when 1 then '启用' else '停用' end) collate utf8mb4_general_ci) AS `StatusName`,`p`.`CreateTime` AS `CreateTime`,`p`.`CreateBy` AS `CreateBy`,`p`.`UpdateTime` AS `UpdateTime`,`p`.`UpdateBy` AS `UpdateBy`,`p`.`DeleteTime` AS `DeleteTime`,`p`.`DeleteBy` AS `DeleteBy`,`p`.`IsDeleted` AS `IsDeleted` from `cert_phase_definition` `p` where (`p`.`IsDeleted` = 0) */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `v_cert_stage`
--

/*!50001 DROP VIEW IF EXISTS `v_cert_stage`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_0900_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `v_cert_stage` AS select `s`.`Id` AS `Id`,`s`.`Code` AS `Code`,`s`.`StageCode` AS `StageCode`,`s`.`StageName` AS `StageName`,`s`.`Description` AS `Description`,`s`.`SortOrder` AS `SortOrder`,`s`.`Category` AS `Category`,`cat`.`DicName` AS `CategoryName`,`s`.`Status` AS `Status`,(case `s`.`Status` when 'active' then '启用' when 'inactive' then '停用' else `s`.`Status` end) AS `StatusName`,`s`.`Remark` AS `Remark`,`s`.`IsValid` AS `IsValid`,`s`.`CreateBy` AS `CreateBy`,`s`.`CreateTime` AS `CreateTime`,`s`.`UpdateBy` AS `UpdateBy`,`s`.`UpdateTime` AS `UpdateTime`,`s`.`DeleteBy` AS `DeleteBy`,`s`.`DeleteTime` AS `DeleteTime`,`s`.`IsDeleted` AS `IsDeleted` from (`cert_cert_stage` `s` left join `sys_dictionarylist` `cat` on(((`cat`.`DicCode` = 'stage_category') and (`cat`.`DicValue` = (`s`.`Category` collate utf8mb4_general_ci))))) */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `v_certification_body`
--

/*!50001 DROP VIEW IF EXISTS `v_certification_body`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_0900_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `v_certification_body` AS select `cb`.`Id` AS `Id`,`cb`.`Code` AS `Code`,`cb`.`OrgCode` AS `OrgCode`,`cb`.`Status` AS `Status`,(case `cb`.`Status` when 'active' then '启用' else `cb`.`Status` end) AS `StatusName`,`cb`.`IsValid` AS `IsValid`,`cb`.`Sort` AS `Sort`,`cb`.`Remark` AS `Remark`,`cb`.`CreateBy` AS `CreateBy`,`cb`.`CreateTime` AS `CreateTime`,`cb`.`UpdateBy` AS `UpdateBy`,`cb`.`UpdateTime` AS `UpdateTime`,`cb`.`DeleteBy` AS `DeleteBy`,`cb`.`DeleteTime` AS `DeleteTime`,`cb`.`Name` AS `Name`,`cb`.`ShortName` AS `ShortName`,`cb`.`CbCode` AS `CbCode`,`cb`.`ContactName` AS `ContactName`,`cb`.`ContactPhone` AS `ContactPhone`,`cb`.`LegalPerson` AS `LegalPerson`,`cb`.`ContactEmail` AS `ContactEmail`,`cb`.`Address` AS `Address`,`cb`.`LogoUrl` AS `LogoUrl`,`cb`.`ScopeText` AS `ScopeText`,`cb`.`ThemeConfig` AS `ThemeConfig`,`cb`.`LoginConfig` AS `LoginConfig`,`cb`.`MaxUsers` AS `MaxUsers`,`cb`.`MaxEnterprises` AS `MaxEnterprises`,`cb`.`ExpireDate` AS `ExpireDate` from `cert_certification_body` `cb` */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `v_iso_standard`
--

/*!50001 DROP VIEW IF EXISTS `v_iso_standard`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_0900_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `v_iso_standard` AS select `s`.`Id` AS `Id`,`s`.`Code` AS `Code`,`s`.`OrgCode` AS `OrgCode`,`s`.`CbCode` AS `CbCode`,`cb`.`ShortName` AS `CbName`,`s`.`StandardCode` AS `StandardCode`,`s`.`StandardName` AS `StandardName`,`s`.`VersionYear` AS `VersionYear`,`s`.`Category` AS `Category`,`cat`.`DicName` AS `CategoryName`,`s`.`Description` AS `Description`,`s`.`Status` AS `Status`,(case `s`.`Status` when 'active' then '启用' when 'inactive' then '停用' else `s`.`Status` end) AS `StatusName`,`s`.`CreateBy` AS `CreateBy`,`s`.`CreateTime` AS `CreateTime`,`s`.`UpdateBy` AS `UpdateBy`,`s`.`UpdateTime` AS `UpdateTime`,`s`.`DeleteBy` AS `DeleteBy`,`s`.`DeleteTime` AS `DeleteTime`,`s`.`IsValid` AS `IsValid`,`s`.`Sort` AS `Sort`,`s`.`Remark` AS `Remark`,`s`.`ParentCode` AS `ParentCode`,`s`.`IsLeaf` AS `IsLeaf` from ((`cert_iso_standard` `s` left join `cert_certification_body` `cb` on((`s`.`CbCode` = `cb`.`Code`))) left join `sys_dictionarylist` `cat` on(((`cat`.`DicCode` = 'iso_category') and (`cat`.`DicValue` = `s`.`Category`)))) */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `v_standard_directory_root_files`
--

/*!50001 DROP VIEW IF EXISTS `v_standard_directory_root_files`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_general_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `v_standard_directory_root_files` AS select `f`.`Id` AS `Id`,`f`.`Code` AS `Code`,`f`.`FileCode` AS `FileCode`,`f`.`FileName` AS `FileName`,`f`.`FileType` AS `FileType`,`f`.`StoragePath` AS `StoragePath`,`f`.`ConvertedStoragePath` AS `ConvertedStoragePath`,`f`.`ConvertStatus` AS `ConvertStatus`,`f`.`ConvertMessage` AS `ConvertMessage`,`f`.`UploadStatus` AS `UploadStatus`,`f`.`TaskId` AS `TaskId`,`f`.`DirectoryCode` AS `DirectoryCode`,`f`.`FolderCode` AS `FolderCode`,`f`.`IsValid` AS `IsValid`,`f`.`IsDeleted` AS `IsDeleted`,`f`.`FileSize` AS `FileSize` from `cert_standard_directory_file` `f` where ((`f`.`FolderCode` is null) or (`f`.`FolderCode` = '') or exists(select 1 from `cert_standard_directory_folder` `sf` where ((`sf`.`FolderCode` = `f`.`FolderCode`) and (`sf`.`IsDeleted` = 0))) is false) */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `v_sys_user`
--

/*!50001 DROP VIEW IF EXISTS `v_sys_user`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_general_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `v_sys_user` AS select `u`.`Id` AS `Id`,`u`.`Code` AS `Code`,`u`.`UserName` AS `UserName`,`u`.`UserTrueName` AS `UserTrueName`,`u`.`UserPwd` AS `UserPwd`,`u`.`RoleId` AS `RoleId`,`u`.`IsValid` AS `IsValid`,`u`.`IsDeleted` AS `IsDeleted`,`u`.`DeleteTime` AS `DeleteTime`,`u`.`DeleteBy` AS `DeleteBy`,`u`.`OrgCode` AS `OrgCode`,`u`.`Gender` AS `Gender`,`u`.`PhoneNo` AS `PhoneNo`,`u`.`Email` AS `Email`,`u`.`HeadImageUrl` AS `HeadImageUrl`,`u`.`Address` AS `Address`,`u`.`Remark` AS `Remark`,`u`.`LastLoginDate` AS `LastLoginDate`,`u`.`LastModifyPwdDate` AS `LastModifyPwdDate`,`u`.`OrderNo` AS `OrderNo`,`u`.`Token` AS `Token`,`u`.`CreateTime` AS `CreateTime`,`u`.`CreateBy` AS `CreateBy`,`u`.`UpdateTime` AS `UpdateTime`,`u`.`UpdateBy` AS `UpdateBy`,`u`.`DeptId` AS `DeptId`,`u`.`UserType` AS `UserType`,`r`.`RoleName` AS `RoleName`,`o`.`OrgName` AS `OrgName`,`o`.`OrgType` AS `OrgType`,`o`.`OrgLevel` AS `OrgLevel`,`o`.`OrgPath` AS `OrgPath`,`o`.`LeaderName` AS `OrgLeaderName`,`o`.`LeaderPhone` AS `OrgLeaderPhone` from ((`sys_user` `u` left join `sys_role` `r` on((`u`.`RoleId` = `r`.`Id`))) left join `sys_organization` `o` on((`u`.`OrgCode` = (`o`.`Code` collate utf8mb4_general_ci)))) */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `v_upload_task_detail`
--

/*!50001 DROP VIEW IF EXISTS `v_upload_task_detail`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_0900_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `v_upload_task_detail` AS select `t`.`TaskId` AS `TaskId`,`t`.`DirectoryCode` AS `DirectoryCode`,`t`.`TotalFiles` AS `TotalFiles`,`t`.`SuccessCount` AS `SuccessCount`,`t`.`Status` AS `Status`,`t`.`ExpireTime` AS `ExpireTime`,`f`.`FileCode` AS `FileCode`,`f`.`FileName` AS `FileName`,`f`.`UploadStatus` AS `UploadStatus`,`f`.`StoragePath` AS `StoragePath`,`f`.`IsValid` AS `FileIsValid`,`f`.`IsDeleted` AS `FileIsDeleted` from (`cert_upload_task` `t` left join `cert_standard_directory_file` `f` on(((`t`.`TaskId` = (`f`.`TaskId` collate utf8mb4_unicode_ci)) and (`f`.`IsDeleted` = 0)))) */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `v_workflow`
--

/*!50001 DROP VIEW IF EXISTS `v_workflow`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_general_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `v_workflow` AS select `w`.`Id` AS `Id`,`w`.`Code` AS `Code`,`w`.`OrgCode` AS `OrgCode`,`w`.`WorkflowCode` AS `WorkflowCode`,`w`.`WorkflowName` AS `WorkflowName`,`w`.`WorkflowType` AS `WorkflowType`,(case `w`.`WorkflowType` when 'extraction' then '提取' when 'validation' then '审核' when 'report' then '报告' else `w`.`WorkflowType` end) AS `WorkflowTypeName`,`w`.`WorkflowConfig` AS `WorkflowConfig`,`w`.`Version` AS `Version`,`w`.`IsActive` AS `IsActive`,(case `w`.`IsActive` when 1 then '启用' when 0 then '停用' else cast(`w`.`IsActive` as char charset utf8mb4) end) AS `IsActiveName`,`w`.`Description` AS `Description`,`w`.`Status` AS `Status`,(case `w`.`Status` when 'active' then '启用' when 'inactive' then '停用' else `w`.`Status` end) AS `StatusName`,`w`.`Sort` AS `Sort`,`w`.`Remark` AS `Remark`,`w`.`CreateTime` AS `CreateTime`,`w`.`CreateBy` AS `CreateBy`,`w`.`UpdateTime` AS `UpdateTime`,`w`.`UpdateBy` AS `UpdateBy`,`w`.`DeleteTime` AS `DeleteTime`,`w`.`DeleteBy` AS `DeleteBy`,`w`.`IsDeleted` AS `IsDeleted`,`w`.`IsValid` AS `IsValid` from `wf_workflow_definition` `w` */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2026-09-24 10:45:26
