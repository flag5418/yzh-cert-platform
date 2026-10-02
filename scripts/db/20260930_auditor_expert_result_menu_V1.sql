-- =============================================================================
-- 专家系统菜单增量：NC 检查结果 + 报告结论（专家平台第 3、4 个业务功能入口）
-- 日期：2026-09-30
-- 目的：在「专家系统（MENU_AUD_00）」下新增两个子菜单
--         MENU_AUD_08 → /nc-results      （NC 检查结果）
--         MENU_AUD_09 → /report-results  （报告结论）
--
-- ★ 位置决策：OrderNo=350 / 360，插在「任务中心(300)」与「资料库(400)」之间 ——
--   与专家平台实际操作顺序一致：
--     ① 建企业 → ② 关联阶段/标准 → ③ 开任务（建立→提交→跑队列）→ ④ 看结果并审批/导出。
--   结果两个菜单**紧跟任务中心**，因为它们就是任务的产物视图。
--
-- ★ 为什么拆成两个菜单而不是「一个结果页 + Tab」：
--   用户 2026-09-30 裁决 —— 两个菜单的**业务动作不同**（NC 是「判定 + 严重度」，
--   报告是「章节正文」），列头、修改表单、导出列头都不同；
--   合成一页会让每处都长出 if/else。**实现共用**（`ExpertResultPanel.vue`），
--   **入口分开**（两个菜单）—— 与「NC 与报告规则共用 WorkflowDesigner」同一决策。
--
-- ★ 列名注意：`Sys_Menu` 的启禁列是 `IsValid`，**不存在 `Enable` 列**（铁律九）。
--
-- ★ 幂等：可重复执行（依赖 Sys_Menu.Code 唯一键 + Sys_RoleMenu NOT EXISTS）
-- ★ 归属：scripts/README.md §1 —— 「db/（根）迁移 SQL、一次性 DDL/DML」
--
-- ⚠️ 执行后必须重跑菜单快照，否则前端守卫 R12 基于过期数据：
--      ./scripts/db/verify/sync_menu_urls.sh
-- =============================================================================

-- -----------------------------------------------------------------------------
-- ① 菜单主体：2 个子菜单
-- -----------------------------------------------------------------------------
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Icon`, `OrderNo`, `Url`, `Tag`, `Description`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('MENU_AUD_08', 'MENU_AUD_00', 'NC 检查结果', 'DocumentChecked', 350, '/nc-results', 'auditor',
   '按任务查看 NC 检查结论：勾选批量认可 / 单行直接修改 / 整体列表导出', 1, 0, NOW(), 'seed_auditor'),
  ('MENU_AUD_09', 'MENU_AUD_00', '报告结论', 'Memo', 360, '/report-results', 'auditor',
   '按任务查看报告章节结论：修改章节正文 / 批量认可 / 整体列表导出', 1, 0, NOW(), 'seed_auditor')
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
WHERE m.`Code` IN ('MENU_AUD_08', 'MENU_AUD_09')
  AND m.`IsDeleted` = 0
  AND NOT EXISTS (
    SELECT 1 FROM `Sys_RoleMenu` rm
    WHERE rm.`RoleCode` = 'ROLE_AUDIT_CLIENT_ADMIN'
      AND rm.`MenuCode` = m.`Code`
  );

-- -----------------------------------------------------------------------------
-- ③ 自检输出
-- -----------------------------------------------------------------------------
SELECT '--- 专家系统菜单（应含 MENU_AUD_08 /nc-results、MENU_AUD_09 /report-results）---' AS `check`;
SELECT `Code`, `ParentCode`, `MenuName`, `Url`, `Icon`, `OrderNo`, `IsValid`, `Tag`
FROM `Sys_Menu`
WHERE `Code` LIKE 'MENU\_AUD\_%' AND `IsDeleted` = 0
ORDER BY `OrderNo`;

SELECT '--- MENU_AUD_08/09 已授权角色数（各应 >= 1）---' AS `check`;
SELECT `MenuCode`, COUNT(*) AS `grant_cnt`
FROM `Sys_RoleMenu`
WHERE `MenuCode` IN ('MENU_AUD_08', 'MENU_AUD_09')
GROUP BY `MenuCode`;
