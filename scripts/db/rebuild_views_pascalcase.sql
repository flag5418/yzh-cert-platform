-- ============================================================
-- 重建受列名变更影响的视图
-- 创建时间：2026-09-13
-- 原因：DB 列名从 snake_case 重命名为 PascalCase 后，视图引用旧列名会报错
--
-- ★ 2026-09-23 补充（勿删）：必须固定连接排序规则。
--   视图里 `CASE ... THEN '启用' ELSE '停用'` 这类**字面量派生列**，其 collation 取自
--   建视图时的 `collation_connection` 并**固化**在视图定义中。
--   本脚本原先无此设置 → 视图列被固化成当时连接的 `utf8mb4_0900_ai_ci`，
--   与全库 `utf8mb4_general_ci` 不一致，跨表比较即报 ERROR 1267。
--   （实际残留：`v_cert_phase_definition.StatusName`）
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────
-- 1. v_certification_body（认证机构视图）
-- ────────────────────────────────────────────────────────────
DROP VIEW IF EXISTS `v_certification_body`;
CREATE VIEW `v_certification_body` AS
SELECT 
  `cb`.`Id` AS `Id`,
  `cb`.`Code` AS `Code`,
  `cb`.`OrgCode` AS `OrgCode`,
  `cb`.`Status` AS `Status`,
  (CASE `cb`.`Status` WHEN 'active' THEN '启用' ELSE `cb`.`Status` END) AS `StatusName`,
  `cb`.`IsValid` AS `IsValid`,
  `cb`.`Sort` AS `Sort`,
  `cb`.`Remark` AS `Remark`,
  `cb`.`CreateBy` AS `CreateBy`,
  `cb`.`CreateTime` AS `CreateTime`,
  `cb`.`UpdateBy` AS `UpdateBy`,
  `cb`.`UpdateTime` AS `UpdateTime`,
  `cb`.`DeleteBy` AS `DeleteBy`,
  `cb`.`DeleteTime` AS `DeleteTime`,
  `cb`.`Name` AS `Name`,
  `cb`.`ShortName` AS `ShortName`,
  `cb`.`CbCode` AS `CbCode`,
  `cb`.`ContactName` AS `ContactName`,
  `cb`.`ContactPhone` AS `ContactPhone`,
  `cb`.`LegalPerson` AS `LegalPerson`,
  `cb`.`ContactEmail` AS `ContactEmail`,
  `cb`.`Address` AS `Address`,
  `cb`.`LogoUrl` AS `LogoUrl`,
  `cb`.`ScopeText` AS `ScopeText`,
  `cb`.`ThemeConfig` AS `ThemeConfig`,
  `cb`.`LoginConfig` AS `LoginConfig`,
  `cb`.`MaxUsers` AS `MaxUsers`,
  `cb`.`MaxEnterprises` AS `MaxEnterprises`,
  `cb`.`ExpireDate` AS `ExpireDate`
FROM `cert_certification_body` `cb`;

-- ────────────────────────────────────────────────────────────
-- 2. v_cert_phase_definition（阶段定义视图）
-- ────────────────────────────────────────────────────────────
DROP VIEW IF EXISTS `v_cert_phase_definition`;
CREATE VIEW `v_cert_phase_definition` AS
SELECT 
  `p`.`Id` AS `Id`,
  `p`.`Code` AS `Code`,
  `p`.`PhaseCode` AS `PhaseCode`,
  `p`.`PhaseName` AS `PhaseName`,
  `p`.`SequenceOrder` AS `SequenceOrder`,
  `p`.`Description` AS `Description`,
  `p`.`IsValid` AS `IsValid`,
  (CASE `p`.`IsValid` WHEN 1 THEN '启用' ELSE '停用' END) AS `StatusName`,
  `p`.`CreateTime` AS `CreateTime`,
  `p`.`CreateBy` AS `CreateBy`,
  `p`.`UpdateTime` AS `UpdateTime`,
  `p`.`UpdateBy` AS `UpdateBy`,
  `p`.`DeleteTime` AS `DeleteTime`,
  `p`.`DeleteBy` AS `DeleteBy`,
  `p`.`IsDeleted` AS `IsDeleted`
FROM `cert_phase_definition` `p`
WHERE `p`.`IsDeleted` = 0;

-- ────────────────────────────────────────────────────────────
-- 3. v_iso_standard（ISO标准视图）
-- ────────────────────────────────────────────────────────────
DROP VIEW IF EXISTS `v_iso_standard`;
CREATE VIEW `v_iso_standard` AS
SELECT 
  `s`.`Id` AS `Id`,
  `s`.`Code` AS `Code`,
  `s`.`OrgCode` AS `OrgCode`,
  `s`.`CbCode` AS `CbCode`,
  `cb`.`ShortName` AS `CbName`,
  `s`.`StandardCode` AS `StandardCode`,
  `s`.`StandardName` AS `StandardName`,
  `s`.`VersionYear` AS `VersionYear`,
  `s`.`Category` AS `Category`,
  `cat`.`DicName` AS `CategoryName`,
  `s`.`Description` AS `Description`,
  `s`.`Status` AS `Status`,
  (CASE `s`.`Status` WHEN 'active' THEN '启用' WHEN 'inactive' THEN '停用' ELSE `s`.`Status` END) AS `StatusName`,
  `s`.`CreateBy` AS `CreateBy`,
  `s`.`CreateTime` AS `CreateTime`,
  `s`.`UpdateBy` AS `UpdateBy`,
  `s`.`UpdateTime` AS `UpdateTime`,
  `s`.`DeleteBy` AS `DeleteBy`,
  `s`.`DeleteTime` AS `DeleteTime`,
  `s`.`IsValid` AS `IsValid`,
  `s`.`Sort` AS `Sort`,
  `s`.`Remark` AS `Remark`,
  `s`.`ParentCode` AS `ParentCode`,
  `s`.`IsLeaf` AS `IsLeaf`
FROM `cert_iso_standard` `s`
LEFT JOIN `cert_certification_body` `cb` ON `s`.`CbCode` = `cb`.`Code`
LEFT JOIN `sys_dictionarylist` `cat` ON `cat`.`DicCode` = 'iso_category' AND `cat`.`DicValue` = `s`.`Category`;

-- ────────────────────────────────────────────────────────────
-- 4. v_cert_stage（认证阶段视图）— 重建
-- ────────────────────────────────────────────────────────────
DROP VIEW IF EXISTS `v_cert_stage`;
CREATE VIEW `v_cert_stage` AS
SELECT 
  `s`.`Id` AS `Id`,
  `s`.`Code` AS `Code`,
  `s`.`StageCode` AS `StageCode`,
  `s`.`StageName` AS `StageName`,
  `s`.`Description` AS `Description`,
  `s`.`SortOrder` AS `SortOrder`,
  `s`.`Category` AS `Category`,
  `cat`.`DicName` AS `CategoryName`,
  `s`.`Status` AS `Status`,
  (CASE `s`.`Status` WHEN 'active' THEN '启用' WHEN 'inactive' THEN '停用' ELSE `s`.`Status` END) AS `StatusName`,
  `s`.`Remark` AS `Remark`,
  `s`.`IsValid` AS `IsValid`,
  `s`.`CreateBy` AS `CreateBy`,
  `s`.`CreateTime` AS `CreateTime`,
  `s`.`UpdateBy` AS `UpdateBy`,
  `s`.`UpdateTime` AS `UpdateTime`,
  `s`.`DeleteBy` AS `DeleteBy`,
  `s`.`DeleteTime` AS `DeleteTime`,
  `s`.`IsDeleted` AS `IsDeleted`
FROM `cert_cert_stage` `s`
LEFT JOIN `sys_dictionarylist` `cat` ON `cat`.`DicCode` = 'stage_category' AND `cat`.`DicValue` = `s`.`Category` COLLATE utf8mb4_general_ci;
