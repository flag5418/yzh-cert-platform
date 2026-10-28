-- ============================================================================
-- 20261028_clause_attr_menu_V1.sql — V2 条款属性两页菜单（★ 幂等，可重复执行）
-- ============================================================================
-- 背景：`docs/70-V2设计/24-条款标准化-属性池与条款配置-V2.md`
--       属性池（L0）`cert_clause_attr_def` → 条款配置（L1）`cert_clause_attr`
--
-- 挂载位置：MENU_00302「规则定义」（体系认证 MENU_00002 下）
--   MENU_00220 条款属性池    OrderNo 400  /cert/clause-attr-def
--   MENU_00221 条款属性配置  OrderNo 410  /cert/clause-attr
--
-- ⚠️ Url 必须与 `cert-admin/src/router/index.ts` 的 path **逐字一致**，
--    否则前端守卫 R12 报「菜单有路由无 ⇒ 点击白屏」。
-- ⚠️ 执行后必须重跑 `bash scripts/db/verify/sync_menu_urls.sh` 刷新快照，
--    否则守卫基于过期数据（快照文件须随代码提交）。
-- ============================================================================

INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Auth`, `Icon`, `Description`, `OrderNo`, `Url`, `Tag`, `CreateBy`, `CreateTime`, `IsDeleted`, `IsValid`)
VALUES
  ('MENU_00220', 'MENU_00302', '条款属性池', NULL, 'Files',
   '属性全集（L0）：定义"世界上有哪些属性"（条款意图/应宜可/适用性/涉及过程/企业应提供资料/取证链/严重度），跨标准跨条款复用',
   400, '/cert/clause-attr-def', 'admin', 'clause-attr-20261028', NOW(), 0, 1),
  ('MENU_00221', 'MENU_00302', '条款属性配置', NULL, 'Document',
   '条款属性配置（L1）：左树 标准→叶子条款（208 条），右表该条款已配属性；一个条款 = 两份要求（企业侧资料 + 审核员侧取证链）',
   410, '/cert/clause-attr', 'admin', 'clause-attr-20261028', NOW(), 0, 1)
ON DUPLICATE KEY UPDATE
  `ParentCode`  = VALUES(`ParentCode`),
  `Icon`        = VALUES(`Icon`),
  `Description` = VALUES(`Description`),
  `OrderNo`     = VALUES(`OrderNo`),
  `Url`         = VALUES(`Url`),
  `Tag`         = VALUES(`Tag`),
  `IsDeleted`   = 0,
  `UpdateTime`  = NOW();
-- ⚠️ 守卫 B-R6：`MenuName` / `IsValid` 是**人工可改列**（库里改名 / 手动停用），
--    ⛔ 绝不能出现在覆盖子句里 —— 否则每次重跑都把人改的名字和停用状态冲掉。
--    而 `Url` / `ParentCode` 必须由脚本保证（守卫 R12 要校验「路由 ↔ 菜单」一致）。

-- 验证：
--   SELECT Code, MenuName, OrderNo, Url FROM Sys_Menu WHERE ParentCode='MENU_00302' ORDER BY OrderNo;
