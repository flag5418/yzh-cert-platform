-- ============================================================
-- fix-v-cert-stage-category-diccode-2026-10-08.sql
-- 修复 v_cert_stage 的 CategoryName 恒为 NULL（分类列显示英文的根因之二）
--
-- 问题：视图 join 条件写 `sys_dictionarylist.DicCode = 'stage_category'`（DicNo 老约定），
--       而字典项 Sys_DictionaryList.DicCode 实际存的是 sys_dictionary.Code（GUID，
--       stage_category = 26b0f0baae6c11f1953796fd503fd974），业务值存 DicValue。
--       ⇒ cat 三行全 join 不上 ⇒ CategoryName 全 NULL。
-- 影响：/filter 走视图（CertStageController T+V 读视图），列表分类列无中文。
-- 修法：join 改为「按 DicNo 解析字典 Code，再匹配 DicCode + DicValue」。
--       ⛔ 不改 v_iso_standard（其字典项是 DicNo 字符串老约定，改了反而断）。
-- 同步：scripts/db/rebuild_views_pascalcase.sql §4 已同步同一 join。
-- 执行：docker exec -i yzh-mysql mysql -uroot -pYzh123456. yzh_cert_platform \
--         < scripts/db/fix/fix-v-cert-stage-category-diccode-2026-10-08.sql
-- ============================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

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
LEFT JOIN `sys_dictionary` `d`
       ON `d`.`DicNo` = 'stage_category' AND `d`.`IsDeleted` = 0
LEFT JOIN `sys_dictionarylist` `cat`
       ON `cat`.`DicCode` = `d`.`Code`
      AND `cat`.`DicValue` = `s`.`Category` COLLATE utf8mb4_general_ci
      AND `cat`.`IsDeleted` = 0;

-- ============================================================
-- 验证 SQL（必须 3 行且 CategoryName 全非 NULL；返回 0 行 = 修复失败）
-- ============================================================
SELECT `StageCode`, `StageName`, `Category`, `CategoryName`, `Status`, `StatusName`
FROM `v_cert_stage`
WHERE `IsDeleted` = 0;
