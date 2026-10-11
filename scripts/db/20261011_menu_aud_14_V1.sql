-- ══════════════════════════════════════════════════════════════════════════
-- 20261011 · 专家端菜单 MENU_AUD_14「企业资料审核」+ 角色授权
--          ＋ 修订 MENU_AUD_12「企业资料规范化」的描述（职责收窄为「任务列表 + 向导」）
-- ══════════════════════════════════════════════════════════════════════════
-- 依据（用户 2026-10-11 裁决 · 原文）：
--   「这个页面，我发现了，应该将该页面做成 2 个，第一个是企业资料规范化，
--     第二个是企业资料审核」
--   「第二个页面是左树右表，树的结构，企业 + 阶段 + 任务，右侧是勾选的所有的
--     标准化文档，可以针对已经完成标准文档规范的进行审核」
--   「可以按企业的阶段，统一导出所有的文档」
--
-- 拆分后的职责：
--   MENU_AUD_12 /enterprise-normalize          → 任务列表（开任务 / 看进度 / 取消 / 重新执行）
--   （无菜单）/enterprise-normalize/create     → 向导（开任务的具体流程，子页面不进侧边栏）
--   MENU_AUD_14 /enterprise-review             → 左树右表审核 + 按阶段统一导出
--
-- ⚠️ 菜单与路由必须【同批落地】：
--   本端路由是**静态**的（cert-auditor/src/router/index.ts），菜单是**动态**的（DB）。
--   菜单 Url 无对应子路由 ⇒ 点击**白屏** + 守卫 R12 报「菜单 Url 无对应路由」。
--   ⇒ 本脚本执行后必须立刻跑：./scripts/db/verify/sync_menu_urls.sh
--
-- ⚠️ 编号说明：MENU_AUD_13 已被 `/queue`（队列监控）占用 ⇒ 本菜单取 **MENU_AUD_14**。
-- ⚠️ OrderNo=480 插在 企业资料规范化(470) 与 组织与成员(500) 之间，零改动既有行。
-- ⚠️ Sys_RoleMenu 【没有 Code 列】（Id varchar(64) 主键 + RoleCode/MenuCode/OrderNo/…）
--    ⇒ 只能写 Id。
-- 幂等：ON DUPLICATE KEY UPDATE + INSERT IGNORE
-- ══════════════════════════════════════════════════════════════════════════

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ── ① 新增：企业资料审核 ──
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Auth`, `Icon`, `Description`, `OrderNo`, `Url`, `CreateTime`, `CreateBy`, `Tag`, `IsDeleted`, `IsValid`)
VALUES
  ('MENU_AUD_14', 'MENU_AUD_00', '企业资料审核', NULL, 'DocumentChecked',
   '按「企业 › 阶段 › 任务」查看已规范化的标准文档，逐锚点审核取值（改值 / 钉住），并按阶段统一导出',
   480, '/enterprise-review', NOW(), 'seed_doc_review', 'auditor', 0, 1)
-- ⛔ 刻意**不**覆盖 `MenuName` / `IsValid` —— 人工可改列，重跑会静默冲掉人工改名与停用
--    （实测症状：「改了菜单、跑一次 SQL 又被还原」2026-10-11）。守卫 `B-R6` 拦回潮。
ON DUPLICATE KEY UPDATE
  `ParentCode` = VALUES(`ParentCode`),
  `Icon`       = VALUES(`Icon`),
  `Description`= VALUES(`Description`),
  `OrderNo`    = VALUES(`OrderNo`),
  `Url`        = VALUES(`Url`),
  `Tag`        = VALUES(`Tag`),
  `IsDeleted`  = 0;

INSERT IGNORE INTO `Sys_RoleMenu` (`Id`, `RoleCode`, `MenuCode`, `OrderNo`, `CreateTime`, `CreateBy`)
VALUES
  (CONCAT('RM_', REPLACE(UUID(), '-', '')), 'ROLE_AUDIT_CLIENT_ADMIN', 'MENU_AUD_14', 480, NOW(), 'seed_doc_review');

-- ── ② 修订：企业资料规范化的描述（页面已收窄为「任务列表 + 向导」，⛔ 不再承载账本/锁定审计） ──
-- ⚠️ `WHERE` 里必须带上**旧描述原文**：本文件会被重复执行，若不加这个条件，
--    每次重跑都会把人工改过的描述**冲回**脚本值（与 `MenuName` / `IsValid` 同一类病）。
--    加了之后语义 = 「只在还停留在种子原文时才修订」= 幂等且不覆盖人工值。
UPDATE `Sys_Menu`
   SET `Description` = '开启与跟踪规范化任务：选企业与阶段 → 选文档（整体 / 部分）→ 干跑预览 → 入队执行；执行结果到「企业资料审核」里审核'
 WHERE `Code` = 'MENU_AUD_12'
   AND `Description` = '企业原始资料 → 按空白模板规则生成规范化标准文档（干跑预览 / 一键规范化 / 锚点账本 / 锁定审计）';

-- ── 验证 ──
SELECT Code, ParentCode, MenuName, Url, Tag, OrderNo, IsValid, IsDeleted
  FROM Sys_Menu WHERE Code IN ('MENU_AUD_12', 'MENU_AUD_14');

SELECT RoleCode, MenuCode, OrderNo FROM Sys_RoleMenu
 WHERE MenuCode IN ('MENU_AUD_12', 'MENU_AUD_14');
