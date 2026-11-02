-- =============================================================================
-- V2 独立认证检查闭环 — 专家端菜单注册
-- 日期：2026-10-30
-- 目的：为 V2 检查任务、证据挂接页面注册 auditor 端菜单项
--
-- ★ MENU_AUD_19  认证检查任务    /audit-task       任务列表（计划→检查任务→逐项分析）
-- ★ MENU_AUD_20  证据挂接        /audit-evidence   证据挂接 + MD5 变更监控
--
-- ⚠️ 侧边栏只渲染 2 层 → 挂在 MENU_AUD_00（专家系统）下做二级叶子
-- ★ 字符集 utf8mb4 + utf8mb4_general_ci
-- ★ 列名 PascalCase
-- ★ 启用 = IsValid
-- ★ 幂等：ON DUPLICATE KEY UPDATE + 授权带 NOT EXISTS
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- ① 菜单登记：MENU_AUD_19 / MENU_AUD_20
-- -----------------------------------------------------------------------------

INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Icon`, `OrderNo`, `Url`, `Tag`, `Description`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('MENU_AUD_19', 'MENU_AUD_00', '认证检查任务', 'List', 320, '/audit-task', 'auditor',
   'V2 独立认证检查闭环：计划生成的检查任务列表，启动/暂停 → 逐项分析面板（LLM 符合性判定 + 锁定节流）',
   1, 0, NOW(), 'seed_auditor'),
  ('MENU_AUD_20', 'MENU_AUD_00', '证据挂接', 'Paperclip', 330, '/audit-evidence', 'auditor',
   'V2 独立认证检查闭环：证据挂接至检查项 × 条款，MD5 变更监控触发影响分析（已锁定项不刷 LLM）',
   1, 0, NOW(), 'seed_auditor')
ON DUPLICATE KEY UPDATE
  `ParentCode`  = VALUES(`ParentCode`),
  `Icon`        = VALUES(`Icon`),
  `OrderNo`     = VALUES(`OrderNo`),
  `Url`         = VALUES(`Url`),
  `Tag`         = VALUES(`Tag`),
  `Description` = VALUES(`Description`),
  `IsDeleted`   = 0;


-- -----------------------------------------------------------------------------
-- ② 授权给专家角色（审核管理员 ROLE_AUDIT_CLIENT_ADMIN）
-- -----------------------------------------------------------------------------

INSERT INTO `Sys_RoleMenu` (`Id`, `RoleCode`, `MenuCode`, `OrderNo`, `CreateTime`, `CreateBy`)
SELECT REPLACE(UUID(), '-', ''), 'ROLE_AUDIT_CLIENT_ADMIN', m.`Code`, m.`OrderNo`, NOW(), 'seed_auditor'
FROM `Sys_Menu` m
WHERE m.`Code` IN ('MENU_AUD_19', 'MENU_AUD_20')
  AND m.`IsDeleted` = 0
  AND NOT EXISTS (
    SELECT 1 FROM `Sys_RoleMenu` rm
    WHERE rm.`RoleCode` = 'ROLE_AUDIT_CLIENT_ADMIN'
      AND rm.`MenuCode` = m.`Code`
  );


-- -----------------------------------------------------------------------------
-- ③ 自检输出
-- -----------------------------------------------------------------------------

SELECT '--- 新增菜单确认（应含 MENU_AUD_19 / MENU_AUD_20）---' AS `check`;
SELECT `Code`, `ParentCode`, `MenuName`, `Url`, `IsValid`, `Tag`
FROM `Sys_Menu`
WHERE `Code` IN ('MENU_AUD_19', 'MENU_AUD_20') AND `IsDeleted` = 0;

SELECT '--- 角色授权确认（应含 2 行）---' AS `check`;
SELECT rm.`RoleCode`, rm.`MenuCode`, m.`MenuName`
FROM `Sys_RoleMenu` rm
JOIN `Sys_Menu` m ON m.`Code` = rm.`MenuCode`
WHERE rm.`MenuCode` IN ('MENU_AUD_19', 'MENU_AUD_20')
  AND m.`IsDeleted` = 0;
