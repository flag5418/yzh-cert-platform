-- =============================================================================
-- 专家系统菜单种子 V1
-- 日期：2026-09-23
-- 目的：为专家端（cert-auditor）建立**独立的「专家系统」顶层分类菜单**及其子菜单，
--       使「角色-菜单分配」可按一个大分类整体授予，便于维护。
--
-- ★ 结构决策（重要）：
--   `AdminLayout.vue` / `AuditorLayout.vue` 的侧边栏**只渲染 2 层**
--   （`v-for menu` → `el-sub-menu` → `v-for child` → `el-menu-item`，无更深嵌套）。
--   故菜单层级**必须 ≤ 2 层**。原设计文档 §3.2 的 6 个顶层菜单被收敛为
--   「专家系统（顶层）→ 6 个功能菜单（子）」，更深层级改由**页面内 Tab / 子路由**承载。
--
-- ⚠️ 2026-09-25 修正：`Sys_Menu` 的启禁列已统一为 `IsValid`，**不存在 `Enable` 列**
--   （铁律九：`Enable` 零容忍）。本脚本原写 `Enable`，重跑会报
--   `ERROR 1054 Unknown column 'Enable'` —— 已就地改为 `IsValid`。
--
-- ★ 设计文档：docs/20-体系认证/03-详细设计/04-专家端/专家端菜单与系统一览设计-V1.md
-- ★ 幂等：可重复执行（依赖 Sys_Menu.Code 唯一键 + Sys_RoleMenu NOT EXISTS）
--
-- ⛔⛔ 【人工可改列】`MenuName` / `IsValid` 是本脚本**刻意不覆盖**的两列
--   （理由见下方 `ON DUPLICATE KEY UPDATE` 之前的注释块）。
--   ★ 本文件里 `IsValid = 0` 的三行 —— MENU_AUD_01 系统一览 / MENU_AUD_05 组织与成员 /
--     MENU_AUD_06 系统设置 —— 是**刻意停用**：对应页面尚未实现，用户裁决 2026-10-11
--     隐藏入口（⛔ 不是「漏写」也不是「坏数据」）。
--     要启用：在「角色-菜单」界面直接启用即可，⛔ 不必改本脚本（重跑也不会再冲掉）。
--   ★ `MENU_AUD_13` / `MENU_AUD_04` 的名称已按用户实际改法固化 ——
--     「队列监控 → 企业队列监控」「资料库 → 认证资料管理」，
--     使「新建库」与「既有库」得到**同一状态**。
-- =============================================================================

-- -----------------------------------------------------------------------------
-- ① 菜单主体：1 个顶层分类 + 6 个子菜单
-- -----------------------------------------------------------------------------
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Icon`, `OrderNo`, `Url`, `Tag`, `Description`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('MENU_AUD_00', '0',           '专家系统',   'Avatar',         300, '/',             'auditor', '专家端功能总入口（分类节点，不落地页）', 1, 0, NOW(), 'seed_auditor'),
  ('MENU_AUD_01', 'MENU_AUD_00', '系统一览',   'Odometer',       100, '/overview',     'auditor', '驾驶舱：全局状态 / 能力就绪度 / 待办', 0, 0, NOW(), 'seed_auditor'),
  ('MENU_AUD_02', 'MENU_AUD_00', '企业管理',   'OfficeBuilding', 200, '/enterprises',  'auditor', '企业档案列表 + 新建企业向导', 1, 0, NOW(), 'seed_auditor'),
  ('MENU_AUD_03', 'MENU_AUD_00', '任务中心',   'List',           300, '/tasks',        'auditor', '任务列表：NC 检查 / 报告生成的任务管理', 1, 0, NOW(), 'seed_auditor'),
  ('MENU_AUD_13', 'MENU_AUD_00', '企业队列监控', 'Cpu',          310, '/queue',        'auditor', '查看 / 管理当前工作区 NC 检查与报告生成队列的执行进度', 1, 0, NOW(), 'seed_auditor'),
  ('MENU_AUD_04', 'MENU_AUD_00', '认证资料管理', 'FolderOpened', 400, '/resources',    'auditor', '跨企业资料检索 + 目录模板', 1, 0, NOW(), 'seed_auditor'),
  ('MENU_AUD_05', 'MENU_AUD_00', '组织与成员', 'UserFilled',     500, '/organization', 'auditor', '成员管理 + 角色分组', 0, 0, NOW(), 'seed_auditor'),
  ('MENU_AUD_06', 'MENU_AUD_00', '系统设置',   'Setting',        600, '/settings',     'auditor', '个人信息 / 账户与用量 / 消息设置 / 操作日志', 0, 0, NOW(), 'seed_auditor')
-- ⛔⛔ 【不要往这里加 `MenuName` / `IsValid`】⛔⛔
--    本段是**每次重跑都会执行**的覆盖子句，而这两列是**人工可改列**
--    （菜单名能在界面上改、菜单能手动停用）。一旦写进来，重跑本脚本就会把
--    人工改动**静默冲掉** —— 实测症状：「我改了菜单名 / 禁用了菜单，
--    跑一次 SQL 又被还原了」（2026-10-11 用户报障）。
--    ⇒ 本段只准设置**结构 · 位置类**列（ParentCode / Icon / OrderNo / Url / Tag / Description）。
--    ⚠️ 守卫 `B-R6`（`scripts/backend/guards.mjs`）会拦下回潮。
ON DUPLICATE KEY UPDATE
  `ParentCode`  = VALUES(`ParentCode`),
  `Icon`        = VALUES(`Icon`),
  `OrderNo`     = VALUES(`OrderNo`),
  `Url`         = VALUES(`Url`),
  `Tag`         = VALUES(`Tag`),
  `Description` = VALUES(`Description`),
  `IsDeleted`   = 0;

-- -----------------------------------------------------------------------------
-- ② 授权给专家角色（★ 不做这一步，专家登录后侧边栏就是空的）
--    过滤链路只认 Sys_RoleMenu（MenuPermissionService），Sys_Menu.Tag 不参与过滤。
--    ⚠️ 顶层 MENU_AUD_00 必须一并授权 —— 否则 buildTree() 因找不到父节点，
--       会把 6 个子菜单提升为顶层，侧边栏结构错乱。
-- -----------------------------------------------------------------------------
INSERT INTO `Sys_RoleMenu` (`Id`, `RoleCode`, `MenuCode`, `OrderNo`, `CreateTime`, `CreateBy`)
SELECT REPLACE(UUID(), '-', ''), 'ROLE_AUDIT_CLIENT_ADMIN', m.`Code`, m.`OrderNo`, NOW(), 'seed_auditor'
FROM `Sys_Menu` m
WHERE m.`Code` LIKE 'MENU\_AUD\_%'
  AND m.`IsDeleted` = 0
  AND NOT EXISTS (
    SELECT 1 FROM `Sys_RoleMenu` rm
    WHERE rm.`RoleCode` = 'ROLE_AUDIT_CLIENT_ADMIN'
      AND rm.`MenuCode` = m.`Code`
  );

-- -----------------------------------------------------------------------------
-- ③ 给既有管理端菜单打 Tag（纯分类标签，不参与权限过滤）
--    用途：前端侧边栏据此分流 —— cert-admin 隐藏 tag=auditor，cert-auditor 只显示 tag=auditor。
-- -----------------------------------------------------------------------------
UPDATE `Sys_Menu`
SET `Tag` = 'admin'
WHERE (`Tag` IS NULL OR `Tag` = '')
  AND `Code` LIKE 'MENU\_0%';

-- -----------------------------------------------------------------------------
-- ④ 自检输出
-- -----------------------------------------------------------------------------
SELECT '--- 专家系统菜单 ---' AS `check`;
SELECT `Code`, `ParentCode`, `MenuName`, `Url`, `Icon`, `OrderNo`, `IsValid`, `Tag`
FROM `Sys_Menu`
WHERE `Code` LIKE 'MENU\_AUD\_%' AND `IsDeleted` = 0
ORDER BY `ParentCode`, `OrderNo`;

SELECT '--- ROLE_AUDIT_CLIENT_ADMIN 已授权菜单数 ---' AS `check`;
SELECT COUNT(*) AS `menu_cnt`
FROM `Sys_RoleMenu`
WHERE `RoleCode` = 'ROLE_AUDIT_CLIENT_ADMIN';
