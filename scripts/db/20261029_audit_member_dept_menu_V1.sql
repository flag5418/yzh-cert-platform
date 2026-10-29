-- =============================================================================
-- 专家系统菜单增量：专家组设置 + 企业机构管理
-- 日期：2026-10-29
-- 目的：在「专家系统（MENU_AUD_00）」下新增两个子菜单
--
-- ① MENU_AUD_15  专家组设置    /audit-member      OrderNo=150（企业管理 100 之后）
-- ② MENU_AUD_16  企业机构管理  /enterprise-dept    OrderNo=280（阶段标准关联 250 之后）
--
-- ★ 铁律九：Sys_Menu 启禁列 = IsValid（不存在 Enable 列）
-- ★ 幂等：可重复执行
-- ★ MenuName / IsValid 不在 ON DUPLICATE KEY UPDATE 中（避免冲掉人工改动）
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- ① 菜单主体：2 个子菜单
-- -----------------------------------------------------------------------------
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Icon`, `OrderNo`, `Url`, `Tag`, `Description`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('MENU_AUD_15', 'MENU_AUD_00', '专家组设置', 'UserFilled', 150, '/audit-member', 'auditor',
   '审核人员基本档案管理（姓名/职称/专业/资质/联系方式），入场计划指派审核员时从池子挑选', 1, 0, NOW(), 'seed_auditor'),
  ('MENU_AUD_16', 'MENU_AUD_00', '企业机构管理', 'OfficeBuilding', 280, '/enterprise-dept', 'auditor',
   '按企业建立部门树（不限层级）及负责人信息，入场计划选择部门、定访谈对象时引用', 1, 0, NOW(), 'seed_auditor')
ON DUPLICATE KEY UPDATE
  `ParentCode`  = VALUES(`ParentCode`),
  `Icon`        = VALUES(`Icon`),
  `OrderNo`     = VALUES(`OrderNo`),
  `Url`         = VALUES(`Url`),
  `Tag`         = VALUES(`Tag`),
  `Description` = VALUES(`Description`),
  `IsDeleted`   = 0;

-- -----------------------------------------------------------------------------
-- ② 授权给专家角色
-- -----------------------------------------------------------------------------
INSERT INTO `Sys_RoleMenu` (`Id`, `RoleCode`, `MenuCode`, `OrderNo`, `CreateTime`, `CreateBy`)
SELECT REPLACE(UUID(), '-', ''), 'ROLE_AUDIT_CLIENT_ADMIN', m.`Code`, m.`OrderNo`, NOW(), 'seed_auditor'
FROM `Sys_Menu` m
WHERE m.`Code` IN ('MENU_AUD_15', 'MENU_AUD_16')
  AND m.`IsDeleted` = 0
  AND NOT EXISTS (
    SELECT 1 FROM `Sys_RoleMenu` rm
    WHERE rm.`RoleCode` = 'ROLE_AUDIT_CLIENT_ADMIN'
      AND rm.`MenuCode` = m.`Code`
  );

-- -----------------------------------------------------------------------------
-- ③ 自检输出
-- -----------------------------------------------------------------------------
SELECT '--- 新增菜单确认（应含 MENU_AUD_15/16）---' AS `check`;
SELECT `Code`, `ParentCode`, `MenuName`, `Url`, `Icon`, `OrderNo`, `IsValid`, `Tag`
FROM `Sys_Menu`
WHERE `Code` IN ('MENU_AUD_15', 'MENU_AUD_16')
  AND `IsDeleted` = 0;

SELECT '--- 已授权角色数（应 >= 1）---' AS `check`;
SELECT `MenuCode`, COUNT(*) AS `grant_cnt`
FROM `Sys_RoleMenu`
WHERE `MenuCode` IN ('MENU_AUD_15', 'MENU_AUD_16')
GROUP BY `MenuCode`;
