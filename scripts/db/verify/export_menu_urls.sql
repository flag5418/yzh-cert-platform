-- ============================================================================
-- export_menu_urls.sql — 菜单「路由契约」快照导出（★ 只读，不改任何数据）
-- ============================================================================
-- 用途：为前端架构守卫 **R12**（路由 ↔ 菜单一致性）提供「菜单事实源」。
--       guards.mjs 读取本脚本的输出（scripts/db/verify/menu-urls.tsv），
--       与前端路由表的 path 做**双向差集**：
--         ① 菜单有、路由无 → ⛔ 报错（点击菜单白屏 / 404）
--         ② 路由有、菜单无 → ⚠️ 警告（孤儿路由：只能手输 URL 到达）
--
-- 为什么必须落成文件、而不是守卫直连数据库：
--   守卫要能在无数据库的环境（CI / 干净检出）下运行 → 快照随代码提交。
--
-- ⚠️ 只读脚本（scripts/README.md 铁律 B4）：仅 SELECT，无任何写操作。
-- ⚠️ 菜单数据变更后必须重新生成快照，否则守卫基于过期数据：
--       ./scripts/db/verify/sync_menu_urls.sh
--
-- 输出：TSV（制表符分隔）
--   第 1 行 注释头：# tag <TAB> code <TAB> url
--   其后   每行一条菜单：Tag <TAB> Code <TAB> Url
--
-- 说明：`Url = '/'` 的行是「分类节点」（侧边栏分组容器，不落地页面），
--       守卫会自动跳过。此处**照实导出**，保持快照忠实于 DB 原貌。
-- ============================================================================

SELECT CONCAT('# tag', CHAR(9), 'code', CHAR(9), 'url') AS Line;

SELECT CONCAT(
         IFNULL(Tag, ''),  CHAR(9),
         IFNULL(Code, ''), CHAR(9),
         IFNULL(Url, '')
       ) AS Line
  FROM Sys_Menu
 WHERE IsValid = 1
   AND IsDeleted = 0
 ORDER BY IFNULL(Tag, ''), IFNULL(OrderNo, 0), Code;
