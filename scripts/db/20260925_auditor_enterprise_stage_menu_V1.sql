-- =============================================================================
-- 专家系统菜单增量：企业-阶段-标准关联（专家平台第 2 个业务功能）
-- 日期：2026-09-25
-- 目的：在「专家系统（MENU_AUD_00）」下新增子菜单「阶段标准关联」→ /enterprise-stages
--
-- ★ 位置决策：OrderNo=250，插在「企业管理(200)」与「任务中心(300)」之间 ——
--   与专家平台实际操作顺序一致（① 建企业 → ② 关联阶段/标准 → ③ 开任务）。
--
-- ★ 列名注意（2026-09-25）：`Sys_Menu` 的启禁列已统一为 `IsValid`，
--   **不存在 `Enable` 列**（铁律九：`Enable` 零容忍）。
--   ⚠️ 因此旧脚本 `20260923_auditor_menu_seed_V1.sql` 已失效（它仍写 `Enable`），
--      见本目录 `fix-auditor-menu-seed-enable-to-isvalid-2026-09-25.sql`。
--
-- ★ 幂等：可重复执行（依赖 Sys_Menu.Code 唯一键 + Sys_RoleMenu NOT EXISTS）
-- ★ 归属：scripts/README.md §1 —— 「db/（根）迁移 SQL、一次性 DDL/DML」
-- =============================================================================

-- -----------------------------------------------------------------------------
-- ① 菜单主体：1 个子菜单
-- -----------------------------------------------------------------------------
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Icon`, `OrderNo`, `Url`, `Tag`, `Description`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('MENU_AUD_07', 'MENU_AUD_00', '阶段标准关联', 'Connection', 250, '/enterprise-stages', 'auditor',
   '企业 × 阶段 × 标准 三元关联：左树选企业，右侧「阶段→标准」勾选即生效', 1, 0, NOW(), 'seed_auditor')
ON DUPLICATE KEY UPDATE
  `ParentCode`  = VALUES(`ParentCode`),
  `MenuName`    = VALUES(`MenuName`),
  `Icon`        = VALUES(`Icon`),
  `OrderNo`     = VALUES(`OrderNo`),
  `Url`         = VALUES(`Url`),
  `Tag`         = VALUES(`Tag`),
  `Description` = VALUES(`Description`),
  `IsValid`     = 1,
  `IsDeleted`   = 0;

-- -----------------------------------------------------------------------------
-- ② 授权给专家角色（★ 不做这一步，专家登录后侧边栏看不到该菜单）
--    过滤链路只认 Sys_RoleMenu（MenuPermissionService），Sys_Menu.Tag 不参与过滤。
-- -----------------------------------------------------------------------------
INSERT INTO `Sys_RoleMenu` (`Id`, `RoleCode`, `MenuCode`, `OrderNo`, `CreateTime`, `CreateBy`)
SELECT REPLACE(UUID(), '-', ''), 'ROLE_AUDIT_CLIENT_ADMIN', m.`Code`, m.`OrderNo`, NOW(), 'seed_auditor'
FROM `Sys_Menu` m
WHERE m.`Code` = 'MENU_AUD_07'
  AND m.`IsDeleted` = 0
  AND NOT EXISTS (
    SELECT 1 FROM `Sys_RoleMenu` rm
    WHERE rm.`RoleCode` = 'ROLE_AUDIT_CLIENT_ADMIN'
      AND rm.`MenuCode` = m.`Code`
  );

-- -----------------------------------------------------------------------------
-- ③ 自检输出
-- -----------------------------------------------------------------------------
SELECT '--- 专家系统菜单（应含 MENU_AUD_07 /enterprise-stages）---' AS `check`;
SELECT `Code`, `ParentCode`, `MenuName`, `Url`, `Icon`, `OrderNo`, `IsValid`, `Tag`
FROM `Sys_Menu`
WHERE `Code` LIKE 'MENU\_AUD\_%' AND `IsDeleted` = 0
ORDER BY `OrderNo`;

SELECT '--- MENU_AUD_07 已授权角色数（应 >= 1）---' AS `check`;
SELECT COUNT(*) AS `grant_cnt`
FROM `Sys_RoleMenu`
WHERE `MenuCode` = 'MENU_AUD_07';
