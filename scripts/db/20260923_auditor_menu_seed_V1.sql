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
-- =============================================================================

-- -----------------------------------------------------------------------------
-- ① 菜单主体：1 个顶层分类 + 6 个子菜单
-- -----------------------------------------------------------------------------
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Icon`, `OrderNo`, `Url`, `Tag`, `Description`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('MENU_AUD_00', '0',           '专家系统',   'Avatar',         300, '/',             'auditor', '专家端功能总入口（分类节点，不落地页）', 1, 0, NOW(), 'seed_auditor'),
  ('MENU_AUD_01', 'MENU_AUD_00', '系统一览',   'Odometer',       100, '/overview',     'auditor', '驾驶舱：全局状态 / 能力就绪度 / 待办', 1, 0, NOW(), 'seed_auditor'),
  ('MENU_AUD_02', 'MENU_AUD_00', '企业管理',   'OfficeBuilding', 200, '/enterprises',  'auditor', '企业档案列表 + 新建企业向导', 1, 0, NOW(), 'seed_auditor'),
  ('MENU_AUD_03', 'MENU_AUD_00', '任务中心',   'List',           300, '/tasks',        'auditor', '跨企业任务视角：NC 检查 / 报告生成 / 我的关注', 1, 0, NOW(), 'seed_auditor'),
  ('MENU_AUD_04', 'MENU_AUD_00', '资料库',     'FolderOpened',   400, '/resources',    'auditor', '跨企业资料检索 + 目录模板', 1, 0, NOW(), 'seed_auditor'),
  ('MENU_AUD_05', 'MENU_AUD_00', '组织与成员', 'UserFilled',     500, '/organization', 'auditor', '成员管理 + 角色分组', 1, 0, NOW(), 'seed_auditor'),
  ('MENU_AUD_06', 'MENU_AUD_00', '系统设置',   'Setting',        600, '/settings',     'auditor', '个人信息 / 账户与用量 / 消息设置 / 操作日志', 1, 0, NOW(), 'seed_auditor')
ON DUPLICATE KEY UPDATE
  `ParentCode`  = VALUES(`ParentCode`),
  `MenuName`    = VALUES(`MenuName`),
  `Icon`        = VALUES(`Icon`),
  `OrderNo`     = VALUES(`OrderNo`),
  `Url`         = VALUES(`Url`),
  `Tag`         = VALUES(`Tag`),
  `Description` = VALUES(`Description`),
  `IsValid`     = VALUES(`IsValid`),
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
