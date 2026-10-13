-- =====================================================================
-- 20261013_unify_fields_V1.sql
-- 用途：统一字段 + 删除多余字段
-- 依据：docs/40-实施/分析报告/数据库规范符合性与无用表体检报告-V1.md
-- 备份：backup/db/yzh_cert_platform_before_cleanup_20261013_1004.sql
-- 裁决：用户 2026-10-13「2、统一字段，3、删除多余字段」
-- =====================================================================
-- 口径（实测支撑）：
--   ★ 排序字段：`Sort` 是事实标准（60+ 张表用 Sort；仅 13 张用 SortOrder）
--     且「双列并存」的 8 张表中 `Sort` 恒为 0、真值全在 `SortOrder`
--     ⇒ 正解 = 删 `Sort` + 把 `SortOrder` 改名为 `Sort`（数据零丢失）
--   ★ `StatusField` 是 `Status` 的重复列（3 张 dir 表并存，值已分叉）
--     ⇒ 删 `StatusField`，统一用 `Status`
--   ★ `wf_skill` 双业务键：`Code`(uk) + `SkillCode`(uk)
--     `SkillCode` 是引擎反射键（CertSkillRegistry 按它查 wf_skill_reflection）
--     ⇒ 保留 Code 为唯一业务键，把 Code 对齐为 SkillCode 值后删 SkillCode
-- =====================================================================

SET FOREIGN_KEY_CHECKS = 0;

-- ── 1. 统一排序字段 → Sort ────────────────────────────────────────────
-- 1a. 双列并存（8 张）：先删恒为 0 的 `Sort`，再把 `SortOrder` 改名为 `Sort`
ALTER TABLE `cert_directory_template`          DROP COLUMN `Sort`;
ALTER TABLE `cert_directory_template`          RENAME COLUMN `SortOrder` TO `Sort`;

ALTER TABLE `cert_file_requirement`            DROP COLUMN `Sort`;
ALTER TABLE `cert_file_requirement`            RENAME COLUMN `SortOrder` TO `Sort`;

ALTER TABLE `cert_fill_param_def`              DROP COLUMN `Sort`;
ALTER TABLE `cert_fill_param_def`              RENAME COLUMN `SortOrder` TO `Sort`;

ALTER TABLE `cert_fill_param_value`            DROP COLUMN `Sort`;
ALTER TABLE `cert_fill_param_value`            RENAME COLUMN `SortOrder` TO `Sort`;

ALTER TABLE `cert_iso_clause`                  DROP COLUMN `Sort`;
ALTER TABLE `cert_iso_clause`                  RENAME COLUMN `SortOrder` TO `Sort`;

ALTER TABLE `cert_report_section`              DROP COLUMN `Sort`;
ALTER TABLE `cert_report_section`              RENAME COLUMN `SortOrder` TO `Sort`;

ALTER TABLE `cert_standard_directory_file`     DROP COLUMN `Sort`;
ALTER TABLE `cert_standard_directory_file`     RENAME COLUMN `SortOrder` TO `Sort`;

ALTER TABLE `cert_standard_directory_folder`   DROP COLUMN `Sort`;
ALTER TABLE `cert_standard_directory_folder`   RENAME COLUMN `SortOrder` TO `Sort`;

-- 1b. 仅 SortOrder（4 张）：直接改名为 `Sort`
ALTER TABLE `cert_cert_stage`                  RENAME COLUMN `SortOrder` TO `Sort`;
ALTER TABLE `wf_skill`                         RENAME COLUMN `SortOrder` TO `Sort`;
ALTER TABLE `wf_skill_input`                   RENAME COLUMN `SortOrder` TO `Sort`;
ALTER TABLE `wf_skill_output`                  RENAME COLUMN `SortOrder` TO `Sort`;

-- ── 2. 删多余列：StatusField（Status 的重复列，3 张 dir 表） ──────────
ALTER TABLE `cert_standard_directory_config`   DROP COLUMN `StatusField`;
ALTER TABLE `cert_standard_directory_file`     DROP COLUMN `StatusField`;
ALTER TABLE `cert_standard_directory_folder`   DROP COLUMN `StatusField`;

-- ── 3. 删零引用列 ─────────────────────────────────────────────────────
ALTER TABLE `cert_message`                     DROP COLUMN `RelatedCode`;
ALTER TABLE `wf_skill_input`                   DROP COLUMN `EnumValues`;

-- sys_user 的 11 个 Vol 遗留列（全仓 0 引用，含 JSON 配置）
ALTER TABLE `sys_user`
  DROP COLUMN `AppType`,
  DROP COLUMN `AuditDate`,
  DROP COLUMN `AuditStatus`,
  DROP COLUMN `DeptIds`,
  DROP COLUMN `IsRegregisterPhone`,
  DROP COLUMN `Mobile`,
  DROP COLUMN `ParentUserId`,
  DROP COLUMN `Tel`,
  DROP COLUMN `UserType`,
  DROP COLUMN `WechatOpenid`,
  DROP COLUMN `WechatUnionid`;

-- ── 4. wf_skill 双业务键 → 单一业务键 Code ────────────────────────────
-- 实测：全库仅 wf_skill 自身存 `SK_*` 值（7 行，在 Code 列），无外部引用
--       wf_skill_reflection.SkillCode 18/18 命中 wf_skill.SkillCode
-- 步骤：Code 对齐为 SkillCode 值 → 摘 FK → 删唯一键 → 删列 → FK 改指 Code
UPDATE `wf_skill` SET `Code` = `SkillCode` WHERE `Code` <> `SkillCode`;

ALTER TABLE `wf_skill_reflection` DROP FOREIGN KEY `fk_reflection_skill`;
ALTER TABLE `wf_skill`            DROP INDEX `uk_skill_code`;
ALTER TABLE `wf_skill`            DROP COLUMN `SkillCode`;
-- SkillCode → wf_skill.Code，符合「XxxCode → Xxx.Code」项目惯例
ALTER TABLE `wf_skill_reflection`
  ADD CONSTRAINT `fk_reflection_skill` FOREIGN KEY (`SkillCode`) REFERENCES `wf_skill` (`Code`);

-- ── 5. 补齐 Code 唯一约束（5 张表） ──────────────────────────────────
ALTER TABLE `cert_message`          ADD UNIQUE KEY `uk_code` (`Code`);
ALTER TABLE `wf_prompt_template`    ADD UNIQUE KEY `uk_code` (`Code`);
ALTER TABLE `wf_skill_input`        ADD UNIQUE KEY `uk_code` (`Code`);
ALTER TABLE `wf_skill_output`       ADD UNIQUE KEY `uk_code` (`Code`);
ALTER TABLE `wf_skill_reflection`   ADD UNIQUE KEY `uk_code` (`Code`);

SET FOREIGN_KEY_CHECKS = 1;

-- ── 6. 重建受影响视图 ────────────────────────────────────────────────
DROP VIEW IF EXISTS `v_cert_stage`;
CREATE ALGORITHM=UNDEFINED SQL SECURITY DEFINER VIEW `v_cert_stage` AS
select `s`.`Id` AS `Id`,`s`.`Code` AS `Code`,`s`.`StageCode` AS `StageCode`,`s`.`StageName` AS `StageName`,
`s`.`Description` AS `Description`,`s`.`Sort` AS `Sort`,`s`.`Category` AS `Category`,`cat`.`DicName` AS `CategoryName`,
`s`.`Status` AS `Status`,(case `s`.`Status` when 'active' then '启用' when 'inactive' then '停用' else `s`.`Status` end) AS `StatusName`,
`s`.`Remark` AS `Remark`,`s`.`IsValid` AS `IsValid`,`s`.`CreateBy` AS `CreateBy`,`s`.`CreateTime` AS `CreateTime`,
`s`.`UpdateBy` AS `UpdateBy`,`s`.`UpdateTime` AS `UpdateTime`,`s`.`DeleteBy` AS `DeleteBy`,`s`.`DeleteTime` AS `DeleteTime`,
`s`.`IsDeleted` AS `IsDeleted`
from ((`cert_cert_stage` `s`
  left join `sys_dictionary` `d` on(((`d`.`DicNo` = 'stage_category') and (`d`.`IsDeleted` = 0))))
  left join `sys_dictionarylist` `cat` on(((`cat`.`DicCode` = `d`.`Code`) and (`cat`.`DicValue` = (`s`.`Category` collate utf8mb4_general_ci)) and (`cat`.`IsDeleted` = 0))));

DROP VIEW IF EXISTS `v_sys_user`;
CREATE ALGORITHM=UNDEFINED SQL SECURITY DEFINER VIEW `v_sys_user` AS
select `u`.`Id` AS `Id`,`u`.`Code` AS `Code`,`u`.`UserName` AS `UserName`,`u`.`UserTrueName` AS `UserTrueName`,
`u`.`UserPwd` AS `UserPwd`,`u`.`IsValid` AS `IsValid`,`u`.`IsDeleted` AS `IsDeleted`,`u`.`DeleteTime` AS `DeleteTime`,
`u`.`DeleteBy` AS `DeleteBy`,`u`.`OrgCode` AS `OrgCode`,`u`.`Gender` AS `Gender`,`u`.`PhoneNo` AS `PhoneNo`,
`u`.`Email` AS `Email`,`u`.`HeadImageUrl` AS `HeadImageUrl`,`u`.`Address` AS `Address`,`u`.`Remark` AS `Remark`,
`u`.`LastLoginDate` AS `LastLoginDate`,`u`.`LastModifyPwdDate` AS `LastModifyPwdDate`,`u`.`OrderNo` AS `OrderNo`,
`u`.`Token` AS `Token`,`u`.`CreateTime` AS `CreateTime`,`u`.`CreateBy` AS `CreateBy`,`u`.`UpdateTime` AS `UpdateTime`,
`u`.`UpdateBy` AS `UpdateBy`,
(select group_concat(`r`.`RoleName` separator ',') from (`sys_roleuser` `ru` join `sys_role` `r` on((`ru`.`RoleCode` = `r`.`Code`))) where (`ru`.`UserCode` = `u`.`Code`)) AS `RoleName`,
`o`.`OrgName` AS `OrgName`,`o`.`OrgType` AS `OrgType`,`o`.`OrgLevel` AS `OrgLevel`,`o`.`OrgPath` AS `OrgPath`,
`o`.`LeaderName` AS `OrgLeaderName`,`o`.`LeaderPhone` AS `OrgLeaderPhone`
from (`sys_user` `u` left join `sys_organization` `o` on((`u`.`OrgCode` = (`o`.`Code` collate utf8mb4_general_ci))));

-- =====================================================================
-- 验证
-- =====================================================================
SELECT 'remaining_SortOrder_columns' AS chk, COUNT(*) AS cnt FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA='yzh_cert_platform' AND COLUMN_NAME='SortOrder'
UNION ALL
SELECT 'remaining_StatusField_columns', COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA='yzh_cert_platform' AND COLUMN_NAME='StatusField'
UNION ALL
SELECT 'remaining_Enable_columns', COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA='yzh_cert_platform' AND LOWER(COLUMN_NAME)='enable'
UNION ALL
SELECT 'base_tables', COUNT(*) FROM information_schema.TABLES
  WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_TYPE='BASE TABLE';
