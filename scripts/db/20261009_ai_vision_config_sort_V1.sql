-- ============================================================================
-- 20261009_ai_vision_config_sort_V1.sql
-- 视觉模型配置的「排序号」归位（幂等）
--
-- 背景（2026-10-09 用户报障「系统参数中视觉模型还未配置」）：
--   `ai_vision_config` 确实已配好且可用（13 键、Enabled=true、Model=qwen3-vl-flash），
--   但它在 `/system/config` 页**第 2 页** ⇒ 打开页面看不到 ⇒ 被误判为「未配置」。
--
-- 根因两层（都已修）：
--   ① 页面没传默认排序 ⇒ 后端 `SqlSugarDbOrm` 判 `SortField` 为空就不加 ORDER BY
--      ⇒ 实际按 **Id 升序** ⇒ 晚插入的 `ai_vision_config`（Id=52）落到第 2 页。
--      → 前端修：`yzh.vue.core/src/pages/system/config/index.vue` 传
--        `:default-sort="{ prop: 'Sort', order: 'asc' }"`，让 `Sort` 列真正生效。
--   ② 本行 `Sort` 播种值是 100（比 `system_version`/`system_name` 还靠后），
--      与同组的 ai_* 参数（10~15）脱节。
--      → 本脚本：100 → 16，紧跟 `ai_temperature`(15)，让视觉配置与其余 AI 参数相邻。
--
-- ⚠️ 只改显示顺序，⛔ 不动 `ConfigValue`（那是视觉模型的实际配置，见 20261004 脚本）。
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

UPDATE `cert_sys_config`
   SET `Sort` = 16,
       `UpdateTime` = NOW()
 WHERE `ConfigKey` = 'ai_vision_config'
   AND `Sort` <> 16;

-- 验证：应返回 Sort=16，且排在 ai_temperature(15) 之后、ocr_provider(20) 之前
-- SELECT Id, ConfigKey, Sort, Category FROM cert_sys_config
--  WHERE Category = 'ai_model' ORDER BY Sort;
