-- ============================================================================
-- 20261002_fill_param_menu_V1.sql
-- 体系认证全局参数 —— 菜单入库 + 角色授权
--
-- 后台管理：MENU_00217「体系认证全局参数定义」→ /business/fill-param-def
-- 专家端  ：MENU_AUD_10「企业全局参数定义」  → /enterprise-fill-params
--
-- ⛔ 跑完必须执行 ./scripts/db/verify/sync_menu_urls.sh 刷新菜单快照，
--    否则守卫 R12（路由↔菜单双向一致性）基于过期数据会误报。
-- ============================================================================

SET NAMES utf8mb4;

-- ── ① 后台管理菜单 ──
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Auth`, `Icon`, `Description`, `OrderNo`, `Url`, `CreateTime`, `CreateBy`, `Tag`, `IsDeleted`, `IsValid`)
VALUES
  ('MENU_00217', 'MENU_00002', '体系认证全局参数定义', NULL, 'Setting',
   '按「机构 × 标准 × 阶段」定义体系认证全局参数，供标准文档填充时取值',
   1700, '/business/fill-param-def', NOW(), 'seed_fill_param', 'admin', 0, 1)
ON DUPLICATE KEY UPDATE
  `ParentCode` = VALUES(`ParentCode`),
  `MenuName`   = VALUES(`MenuName`),
  `Icon`       = VALUES(`Icon`),
  `Description`= VALUES(`Description`),
  `OrderNo`    = VALUES(`OrderNo`),
  `Url`        = VALUES(`Url`),
  `Tag`        = VALUES(`Tag`),
  `IsDeleted`  = 0,
  `IsValid`    = 1;

-- ── ② 专家端（企业维度）菜单 ──
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Auth`, `Icon`, `Description`, `OrderNo`, `Url`, `CreateTime`, `CreateBy`, `Tag`, `IsDeleted`, `IsValid`)
VALUES
  ('MENU_AUD_10', 'MENU_AUD_00', '企业全局参数定义', NULL, 'Document',
   '企业基本信息 + 后台定义的全局参数，合并成待完善清单',
   260, '/enterprise-fill-params', NOW(), 'seed_fill_param', 'auditor', 0, 1)
ON DUPLICATE KEY UPDATE
  `ParentCode` = VALUES(`ParentCode`),
  `MenuName`   = VALUES(`MenuName`),
  `Icon`       = VALUES(`Icon`),
  `Description`= VALUES(`Description`),
  `OrderNo`    = VALUES(`OrderNo`),
  `Url`        = VALUES(`Url`),
  `Tag`        = VALUES(`Tag`),
  `IsDeleted`  = 0,
  `IsValid`    = 1;

-- ── ③ 角色授权（后台侧：超管 + 体系管理员）──
INSERT IGNORE INTO `Sys_RoleMenu` (`Id`, `RoleCode`, `MenuCode`, `OrderNo`, `CreateTime`, `CreateBy`)
VALUES
  (CONCAT('RM_', REPLACE(UUID(), '-', '')), 'ROLE_SUPER_ADMIN', 'MENU_00217', 1700, NOW(), 'seed_fill_param'),
  (CONCAT('RM_', REPLACE(UUID(), '-', '')), 'ROLE_ADMIN',       'MENU_00217', 1700, NOW(), 'seed_fill_param');

-- ── ④ 角色授权（专家端侧：体系认证客户端管理员）──
INSERT IGNORE INTO `Sys_RoleMenu` (`Id`, `RoleCode`, `MenuCode`, `OrderNo`, `CreateTime`, `CreateBy`)
VALUES
  (CONCAT('RM_', REPLACE(UUID(), '-', '')), 'ROLE_AUDIT_CLIENT_ADMIN', 'MENU_AUD_10', 260, NOW(), 'seed_fill_param');

-- ── 验证 ──
-- SELECT Code, ParentCode, MenuName, Url, Tag, OrderNo FROM Sys_Menu WHERE Code IN ('MENU_00217','MENU_AUD_10');
-- SELECT RoleCode, MenuCode FROM Sys_RoleMenu WHERE MenuCode IN ('MENU_00217','MENU_AUD_10');
