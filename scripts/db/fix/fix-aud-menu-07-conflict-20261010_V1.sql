-- ============================================================================
-- 修复：审核员端 MENU_AUD_07 Code 冲突 + 补回丢失的队列监控
-- 日期：2026-10-10
--
-- 问题：
--   20260923_auditor_menu_seed_V1.sql 中 MENU_AUD_07 = 队列监控（/menu=/queue）
--   20260925_auditor_enterprise_stage_menu_V1.sql 用同一 MENU_AUD_07 Code 插入阶段标准关联
--   ⇒ ON DUPLICATE KEY UPDATE 覆盖后，队列监控丢失
--
-- 修复方案（幂等）：
--   ① 确保 MENU_AUD_07 = 阶段标准关联（最后执行覆盖，保持当前最终意图）
--   ② 补回队列监控为 MENU_AUD_13（避免再用 MENU_AUD_07 产生歧义）
--   ③ 重新授权
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ① 确保 MENU_AUD_07 = 阶段标准关联
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Icon`, `OrderNo`, `Url`, `Tag`, `Description`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('MENU_AUD_07', 'MENU_AUD_00', '阶段标准关联', 'Connection', 250, '/enterprise-stages', 'auditor',
   '企业 × 阶段 × 标准 三元关联：左树选企业，右侧「阶段→标准」勾选即生效', 1, 0, NOW(), 'fix_20261010')
ON DUPLICATE KEY UPDATE
  `ParentCode` = 'MENU_AUD_00',
  `MenuName`   = '阶段标准关联',
  `Icon`       = 'Connection',
  `OrderNo`    = 250,
  `Url`        = '/enterprise-stages',
  `Tag`        = 'auditor',
  `IsValid`    = 1,
  `IsDeleted`  = 0;

-- ② 补回队列监控（新 Code = MENU_AUD_13，避免再冲突）
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Icon`, `OrderNo`, `Url`, `Tag`, `Description`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('MENU_AUD_13', 'MENU_AUD_00', '队列监控', 'Cpu', 310, '/queue', 'auditor',
   '查看 / 管理当前工作区 NC 检查与报告生成队列的执行进度', 1, 0, NOW(), 'fix_20261010')
ON DUPLICATE KEY UPDATE
  `ParentCode` = 'MENU_AUD_00',
  `MenuName`   = '队列监控',
  `Icon`       = 'Cpu',
  `OrderNo`    = 310,
  `Url`        = '/queue',
  `Tag`        = 'auditor',
  `IsValid`    = 1,
  `IsDeleted`  = 0;

-- ③ 授权给专家角色（MENU_AUD_07 阶段标准关联）
INSERT IGNORE INTO `Sys_RoleMenu` (`Id`, `RoleCode`, `MenuCode`, `OrderNo`, `CreateTime`, `CreateBy`)
VALUES
  (CONCAT('RM_', REPLACE(UUID(), '-', '')), 'ROLE_AUDIT_CLIENT_ADMIN', 'MENU_AUD_07', 250, NOW(), 'fix_20261010');

-- ③ 授权给专家角色（MENU_AUD_13 队列监控）
INSERT IGNORE INTO `Sys_RoleMenu` (`Id`, `RoleCode`, `MenuCode`, `OrderNo`, `CreateTime`, `CreateBy`)
VALUES
  (CONCAT('RM_', REPLACE(UUID(), '-', '')), 'ROLE_AUDIT_CLIENT_ADMIN', 'MENU_AUD_13', 310, NOW(), 'fix_20261010');

-- ── 验证 ──
SELECT '--- 审核员端菜单（修复后）---' AS `check`;
SELECT `Code`, `ParentCode`, `MenuName`, `Url`, `Icon`, `OrderNo`, `IsValid`, `Tag`
FROM `Sys_Menu`
WHERE `Code` LIKE 'MENU\_AUD\_%' AND `IsDeleted` = 0
ORDER BY `OrderNo`;

SELECT '--- ROLE_AUDIT_CLIENT_ADMIN 已授权 ---' AS `check`;
SELECT `MenuCode`, COUNT(*) AS `grant_cnt`
FROM `Sys_RoleMenu`
WHERE `RoleCode` = 'ROLE_AUDIT_CLIENT_ADMIN'
  AND `MenuCode` LIKE 'MENU\_AUD\_%'
GROUP BY `MenuCode`;
