-- MySQL dump 10.13  Distrib 8.0.46, for Linux (x86_64)
--
-- Host: localhost    Database: yzh_cert_platform
-- ------------------------------------------------------
-- Server version	8.0.46

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

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
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2026-09-24 10:45:57
