-- ══════════════════════════════════════════════════════════════════════════
-- 20261006 · 专家端菜单 MENU_AUD_12「企业资料规范化」+ 角色授权
-- ══════════════════════════════════════════════════════════════════════════
-- 依据：docs/.../05-企业资料规范化/54-专家端资料规范化开发计划-V1.md §6.1
--   「路由 /enterprise-normalize（MENU_AUD_12，OrderNo=470，排在 MENU_AUD_11=450 之后）」
--
-- ⚠️ 菜单与路由必须【同批落地】：
--   本端路由是**静态**的（cert-auditor/src/router/index.ts），菜单是**动态**的（DB）。
--   菜单 Url 无对应子路由 ⇒ 点击**白屏** + 守卫 R12 报「菜单 Url 无对应路由」。
--   ⇒ 本脚本执行后必须立刻跑：./scripts/db/verify/sync_menu_urls.sh
--
-- ⚠️ Sys_RoleMenu 【没有 Code 列】（Id varchar(64) 主键 + RoleCode/MenuCode/OrderNo/…）
--    ⇒ 只能写 Id。
-- ⚠️ OrderNo=470 插在 原始资料管理(450) 与 组织与成员(500) 之间，零改动既有行。
-- 幂等：ON DUPLICATE KEY UPDATE + INSERT IGNORE
-- ══════════════════════════════════════════════════════════════════════════

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Auth`, `Icon`, `Description`, `OrderNo`, `Url`, `CreateTime`, `CreateBy`, `Tag`, `IsDeleted`, `IsValid`)
VALUES
  ('MENU_AUD_12', 'MENU_AUD_00', '企业资料规范化', NULL, 'MagicStick',
   '企业原始资料 → 按空白模板规则生成规范化标准文档（干跑预览 / 一键规范化 / 锚点账本 / 锁定审计）',
   470, '/enterprise-normalize', NOW(), 'seed_doc_normalize', 'auditor', 0, 1)
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

INSERT IGNORE INTO `Sys_RoleMenu` (`Id`, `RoleCode`, `MenuCode`, `OrderNo`, `CreateTime`, `CreateBy`)
VALUES
  (CONCAT('RM_', REPLACE(UUID(), '-', '')), 'ROLE_AUDIT_CLIENT_ADMIN', 'MENU_AUD_12', 470, NOW(), 'seed_doc_normalize');

-- ── 验证 ──
SELECT Code, ParentCode, MenuName, Url, Tag, OrderNo, IsValid, IsDeleted
  FROM Sys_Menu WHERE Code = 'MENU_AUD_12';

SELECT RoleCode, MenuCode, OrderNo FROM Sys_RoleMenu WHERE MenuCode = 'MENU_AUD_12';
