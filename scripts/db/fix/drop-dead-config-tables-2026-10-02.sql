-- =============================================================================
-- 删除 3 张无用表（2026-10-02）
-- -----------------------------------------------------------------------------
-- 【背景】用户裁决：以下 3 张表在新架构中无任何用途，予以删除。
--
-- 【判据（三条全中才删）】
--   ① 运行代码 0 引用
--      - yzh_field_config / yzh_page_config：死实体类已同步删除
--        （src/certplatform-api/CertPlatform.Shared/Entities/Sys/{FieldConfig,PageConfig}.cs）
--        全仓 *.cs 中 `FieldConfig` / `PageConfig` 命中数归零。
--        新架构「配置驱动 UI」已改为反射驱动：
--        `YzhControllerBase.GetConfigCore()`（src/yzh-core/YZH.Core.Api/Controllers/YzhControllerBase.cs:201-226）
--        由实体类型 V 反射生成 NewEntity / Schema / SearchFields，**从不读 DB 配置表**。
--        前端 src/certplatform-web/ 全仓 0 命中（无对应 api 文件、无消费方）。
--        ⚠️ 全仓剩余的 `SearchFieldConfig` 是**完全不同的类型**——
--           src/yzh-core/YZH.Core.Stand/Models/Config/SearchFieldConfig.cs:4
--           （反射生成的 DTO，非 DB 表映射），与本表无关，勿误删。
--      - wf_skill_category_bak_20260926：纯备份快照表，唯一引用是
--        drop_wf_skill_category_V1.sql:15 的快照创建语句；父表 wf_skill_category
--        已于 2026-09-26 删除，业务已迁至字典表 Sys_DictionaryList
--        （见 CertPlatform.Shared/Entities/Wf/SkillCategoryDict.cs:27）。
--
--   ② 数据量：实测 yzh_field_config = 0 行、yzh_page_config = 0 行（空表）；
--      wf_skill_category_bak_20260926 = 5 行（技能分类历史快照，原始分类字典仍在位）。
--
--   ③ 属旧 Vol 框架产物：这三张表仅被冻结目录 `src/old/**` 引用
--      （vol.api/YZH.Entity/Admin/Platform/Sys/FieldConfig.cs:16、
--       vol.api/Cert.Platform/Services/PageConfigService.cs:78、
--       vol.web/src/yzh/store/yzhConfig.js:5）。
--      `src/old/**` 已冻结、禁改、不参与运行 → 删表不影响新架构。
--      ⚠️ 副作用：删除后**历史 Vol 项目不再可运行**（其前端启动会因配置表缺失而失败）。
--
-- 【备份】执行前已导出数据 → scripts/db/backup/dead_config_tables_before_drop_20261002_094715.sql
--
-- 幂等：DROP TABLE IF EXISTS，可重复执行。
-- =============================================================================

USE `yzh_cert_platform`;

-- 1. 旧 Vol 框架「页面级 / 字段级 UI 配置表」（空表 + 死实体）
DROP TABLE IF EXISTS `yzh_field_config`;
DROP TABLE IF EXISTS `yzh_page_config`;

-- 2. 技能分类备份快照表（父表 wf_skill_category 已于 2026-09-26 删除）
DROP TABLE IF EXISTS `wf_skill_category_bak_20260926`;

-- ============================ 验证 ============================
-- ① 三张表均已不存在（期望 0 行）
SELECT 'VERIFY_tables_dropped' AS chk, COUNT(*) AS cnt
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('yzh_field_config', 'yzh_page_config', 'wf_skill_category_bak_20260926');

-- ② 技能分类字典仍在位（替代 wf_skill_category，期望 5 行）
SELECT 'VERIFY_dict_skill_category' AS chk, COUNT(*) AS cnt
FROM Sys_DictionaryList l
JOIN Sys_Dictionary d ON l.DicCode = d.Code
WHERE d.DicNo = 'skill_category';

-- ③ 无残留视图依赖（期望 0 行）
SELECT 'VERIFY_view_deps' AS chk, COUNT(*) AS cnt
FROM information_schema.VIEW_TABLE_USAGE
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('yzh_field_config', 'yzh_page_config', 'wf_skill_category_bak_20260926');