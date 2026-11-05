-- ====================================================================
-- 20261105_seed_missing_auditor_menus_V1.sql
-- 修复：专家端登录后多个业务模块菜单缺失
--
-- 根因：
--   1. CodeFirst 重建 Sys_Menu 时只落了最基础 8 条（MENU_AUD_00~06 + 13），
--      后续增量菜单脚本（阶段标准关联/NC 结果/报告结论/审核参数/计划/准备/检查）
--      未在重建后重跑 → 路由有、菜单无 → 白屏 + R12 孤儿路由报警。
--   2. 超级管理员走 IsSuperAdmin 绕过（看到全部菜单），
--      审核员走权限过滤 → 只看到 11 条（仅「专家系统」下那几张基础页）。
--
-- 修复：补充 10 条缺失菜单 + 授权给 ROLE_AUDITOR 角色。
-- 幂等：先删后插（菜单表）+ NOT EXISTS 防重（角色关联表）。
-- ====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- ① 一次性补全缺失菜单（按 OrderNo 排序，300 段已有占用 → 新项用 280/290/295/305 等插空）
-- -----------------------------------------------------------------------------
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Icon`, `OrderNo`, `Url`, `Tag`, `Description`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  -- ★ 用户 2026-09-25 裁决（已有种子脚本但未落地）
  ('MENU_AUD_07', 'MENU_AUD_00', '阶段标准关联', 'Connection', 250,
    '/enterprise-stages', 'auditor', '企业 × 阶段 × 标准 三元关联', 1, 0, NOW(), 'fix_20261105'),
  -- ★ 用户 2026-09-30 裁决（已有种子脚本但未落地）
  ('MENU_AUD_08', 'MENU_AUD_00', 'NC 检查结果', 'DocumentChecked', 350,
   '/nc-results', 'auditor', '按任务查看 NC 检查结论', 1, 0, NOW(), 'fix_20261105'),
  ('MENU_AUD_09', 'MENU_AUD_00', '报告结论', 'Memo', 360,
   '/report-results', 'auditor', '按任务查看报告章节结论', 1, 0, NOW(), 'fix_20261105'),
  -- ★ 企业全局参数定义（企业档案 × 后台参数定义的关联补值清单）
  ('MENU_AUD_10', 'MENU_AUD_00', '企业全局参数登记', 'Edit', 370,
   '/enterprise-fill-params', 'auditor', '企业标准核心字段录入 · 填充预览', 1, 0, NOW(), 'fix_20261105'),
  -- ★ 审核成员（工作区下审核员 / 审核组长的人员管理）
  ('MENU_AUD_15', 'MENU_AUD_00', '审核成员', 'User', 420,
   '/audit-member', 'auditor', '审核员 / 审核组长 · 角色分组与工作区人员管理', 1, 0, NOW(), 'fix_20261105'),
  -- ★ 认证计划（条款为核心的认证入场计划）
  ('MENU_AUD_17', 'MENU_AUD_00', '认证计划', 'Calendar', 430,
   '/audit-plan', 'auditor', '按条款为核心的认证入场计划管理', 1, 0, NOW(), 'fix_20261105'),
  -- ★ 认证准备（计划执行 · 证据挂接 · 文档审核）
  ('MENU_AUD_21', 'MENU_AUD_00', '认证准备', 'Reading', 440,
   '/audit-prep', 'auditor', '计划书执行 · 企业证据与文档 · 条款审核准备', 1, 0, NOW(), 'fix_20261105'),
  -- ★ 认证检查（NC 处置 · 现场检查 · 结论定级）
  ('MENU_AUD_22', 'MENU_AUD_00', '认证检查', 'Search', 460,
   '/audit-onsite', 'auditor', 'NC 不符合项 · 现场审核 · 认证结论检查', 1, 0, NOW(), 'fix_20261105'),
  -- ★ 企业部门管理（工作区下企业节点 / 部门层级维护）
  ('MENU_AUD_23', 'MENU_AUD_00', '企业部门', 'OfficeBuilding', 480,
   '/enterprise-dept', 'auditor', '工作区下企业 · 部门层级维护', 1, 0, NOW(), 'fix_20261105'),
  -- ★ 企业资料审核（规范化任务审核 · 文档确认）
  ('MENU_AUD_14', 'MENU_AUD_00', '企业资料审核', 'Checked', 490,
   '/enterprise-review', 'auditor', '已规范化文档的审核与确认', 1, 0, NOW(), 'fix_20261105')
ON DUPLICATE KEY UPDATE
  `ParentCode`  = VALUES(`ParentCode`),
  `Icon`        = VALUES(`Icon`),
  `OrderNo`     = VALUES(`OrderNo`),
  `Url`         = VALUES(`Url`),
  `Tag`         = VALUES(`Tag`),
  `Description` = VALUES(`Description`),
  `IsDeleted`   = 0,
  `IsValid`     = 1;

-- -----------------------------------------------------------------------------
-- ② 给专家角色授权（走权限过滤的角色必须显式关联）
-- -----------------------------------------------------------------------------
INSERT INTO `Sys_RoleMenu` (`Id`, `RoleCode`, `MenuCode`, `OrderNo`, `CreateTime`, `CreateBy`)
SELECT
    UUID(),
    'ROLE_AUDITOR',
    m.`Code`,
    m.`OrderNo`,
    NOW(),
    'fix_20261105'
FROM `Sys_Menu` m
WHERE m.`Tag` = 'auditor'
  AND m.`IsDeleted` = 0
  AND NOT EXISTS (
    SELECT 1 FROM `Sys_RoleMenu` rm
    WHERE rm.`RoleCode` = 'ROLE_AUDITOR'
      AND rm.`MenuCode` = m.`Code`
  );

-- -----------------------------------------------------------------------------
-- ③ 验评
-- -----------------------------------------------------------------------------
SELECT '专家端全量菜单（IsDeleted=0）' AS check_point;
SELECT `Code`, `MenuName`, `Url`, `OrderNo`, `IsValid`
FROM `Sys_Menu`
WHERE `Tag` = 'auditor' AND `IsDeleted` = 0
ORDER BY `OrderNo`;

SELECT '审核员角色授权菜单数' AS check_point, COUNT(*) AS cnt
FROM `Sys_RoleMenu` WHERE `RoleCode` = 'ROLE_AUDITOR';
