-- ============================================================
-- 视图：v_cert_configured_rules
-- 用途：已配置提取规则列表（跨表 JOIN cert_doc_extraction_rule + cert_standard_directory_file）
-- 日期：2026-09-20
-- 更新：审计列改为 PascalCase（code→Code, skill→Skill）
-- 更新：2026-09-26 P1 重建 —— ⑩ PhaseCode → StageCode；⑱ JOIN 键改为 f.Code（原 f.FileCode 复合码已删）
-- 更新：2026-10-13 按 26-数据库视图设计规范-V1 §三 改造 —— ★ 去掉 WHERE 内固化的 r.IsValid = 1，
--       改为以列暴露 `r.IsValid AS RuleIsValid`，由消费侧统一判。
--       背景：本视图原先把 IsValid 固化进 WHERE ⇒「有效性判据藏进视图」，消费方既看不见
--       也无法在需要时（如后台管理「显示停用规则」）放开；且 ConfiguredRuleView 把 IsValid
--       声明成 [SugarColumn(IsIgnore = true)]（GetColumnName → null）⇒ ORM 入口的
--       `IsValid = 1` 硬过滤**不会**追加（2026-10-13 实测：视图无 IsValid 列时
--       `WHERE IsValid = 1` 报 1054，而接口 200 无异常 ⇒ 证明 ORM 侧确实未下发）。
--       ⇒ 改造前「有效性」只有视图 WHERE 这一处，属单点且不可见。
-- 更新：2026-10-15 增 `COALESCE(f.FullPath, '') AS FullPath` —— NC 规则设计器
--       文档字段/文档表格下拉由扁平 el-select 改为「文件夹 → 文件」树形选择
--       （buildFolderFileTree），需要文件夹路径；消费侧 DocExtractionRuleService
--       由 FullPath 去 FileName 后缀得出 folderPath（与实体 FolderPath 计算属性同款）。
--
-- ★ 改造后口径（行为不变，已 A/B 核对）：
--   视图层只固化 `r.IsDeleted = 0`（软删 = 逻辑不存在），IsValid 一律暴露。
--   消费方 DocExtractionRuleService.GetConfiguredRulesAsync 显式传谓词 `x.RuleIsValid == 1`
--   ⇒ 线上可见行与改造前逐行一致。
--
-- ★ 消费方：CertPlatform.Admin/Services/DocExtraction/DocExtractionRuleService.GetConfiguredRulesAsync
--           实体 CertPlatform.Admin/Entities/Doc/ConfiguredRuleView（RuleIsValid 为新增列）
--
-- 回滚：DROP VIEW IF EXISTS `v_cert_configured_rules`;
-- ============================================================

CREATE OR REPLACE VIEW `v_cert_configured_rules` AS
SELECT
    r.Code AS RuleCode,
    r.StandardFileCode AS StandardFileCode,
    COALESCE(f.FileName, r.StandardFileCode) AS FileName,
    COALESCE(f.FullPath, '') AS FullPath,
    COALESCE(r.StandardCode, '') AS StandardCode,
    COALESCE(r.StageCode, '') AS StageCode,
    r.Skill AS Skill,
    r.DocIsValid AS DocIsValid,
    r.Status AS Status,
    r.IsValid AS RuleIsValid,
    r.CreateTime AS CreateTime,
    r.UpdateTime AS UpdateTime
FROM cert_doc_extraction_rule r
LEFT JOIN cert_standard_directory_file f
    ON r.StandardFileCode = f.Code AND f.IsDeleted = 0
WHERE r.IsDeleted = 0;
