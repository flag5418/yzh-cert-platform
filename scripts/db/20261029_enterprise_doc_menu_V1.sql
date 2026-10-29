-- =============================================================================
-- 脚本：20261029_enterprise_doc_menu_V1.sql
-- 日期：2026-10-29
-- 目的：为「企业资料（V2 上传重写版）」新增**独立菜单**，与 V1 老菜单并存
--
-- ★ 背景（2026-10-29 用户裁决）：
--   「我们是新的表、新的逻辑、新的菜单，待 V2 稳定后，再开始清理 V1 的残留。」
--   ⇒ 新页面**不复用** `MENU_AUD_04`（/resources，认证资料管理，V1），
--     改用独立菜单 `MENU_AUD_18`（/enterprise-doc）。
--   ⇒ V1 的 `MENU_AUD_04` / `MENU_AUD_11` / `MENU_AUD_12` 与老页面、老控制器
--     **一行不动**（P4「下线与清理」整体暂缓）。
--
-- 对照：
--   MENU_AUD_04  /resources            认证资料管理   V1：企业→阶段→标准→槽位 四层
--   MENU_AUD_11  /enterprise-original  企业原始资料   V1：原始资料 + 语义分析 + 画像
--   MENU_AUD_12  /enterprise-normalize 企业资料规范化 V1：规范化任务
--   MENU_AUD_18  /enterprise-doc       **企业资料**   V2：企业一层 + 计划标准 + L1 条款提取  ★ 本脚本
--
-- ★ 字符集/排序规则全库统一 utf8mb4 + utf8mb4_general_ci（铁律八）
-- ★ 列名 = PascalCase（DB / C# / TS 三处一致）
-- ★ 启用列 = IsValid（铁律九，不存在 Enable）
-- ★ 幂等：可重复执行
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- ① 菜单：企业资料（专家系统 MENU_AUD_00 下，紧跟「认证计划」290）
-- -----------------------------------------------------------------------------
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Icon`, `OrderNo`, `Url`, `Tag`, `Description`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  ('MENU_AUD_18', 'MENU_AUD_00', '企业资料', 'FolderOpened', 295, '/enterprise-doc', 'auditor',
   '企业资料（V2 上传重写版）：只按「企业」存一层（无阶段/无槽位），上传不带标准，标准只从当前有效认证计划来；转换产物 PDF+Markdown，按标准做 L1 条款提取',
   1, 0, NOW(), 'seed_enterprise_doc')
ON DUPLICATE KEY UPDATE
  `ParentCode`  = VALUES(`ParentCode`),
  `MenuName`    = VALUES(`MenuName`),
  `Icon`        = VALUES(`Icon`),
  `OrderNo`     = VALUES(`OrderNo`),
  `Url`         = VALUES(`Url`),
  `Tag`         = VALUES(`Tag`),
  `Description` = VALUES(`Description`),
  `IsDeleted`   = 0;

-- -----------------------------------------------------------------------------
-- ② 授权给专家角色
-- -----------------------------------------------------------------------------
INSERT INTO `Sys_RoleMenu` (`Id`, `RoleCode`, `MenuCode`, `OrderNo`, `CreateTime`, `CreateBy`)
SELECT REPLACE(UUID(), '-', ''), 'ROLE_AUDIT_CLIENT_ADMIN', m.`Code`, m.`OrderNo`, NOW(), 'seed_enterprise_doc'
FROM `Sys_Menu` m
WHERE m.`Code` = 'MENU_AUD_18'
  AND m.`IsDeleted` = 0
  AND NOT EXISTS (
    SELECT 1 FROM `Sys_RoleMenu` rm
    WHERE rm.`RoleCode` = 'ROLE_AUDIT_CLIENT_ADMIN'
      AND rm.`MenuCode` = m.`Code`
  );

-- -----------------------------------------------------------------------------
-- ③ 验证（人工执行）
-- -----------------------------------------------------------------------------
-- SELECT Code, ParentCode, MenuName, Url, OrderNo, IsValid FROM Sys_Menu
--   WHERE Code IN ('MENU_AUD_04','MENU_AUD_11','MENU_AUD_12','MENU_AUD_18') ORDER BY OrderNo;
-- SELECT RoleCode, MenuCode FROM Sys_RoleMenu WHERE MenuCode = 'MENU_AUD_18';
