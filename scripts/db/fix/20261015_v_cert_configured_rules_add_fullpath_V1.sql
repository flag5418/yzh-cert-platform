-- ============================================================
-- 20261015_v_cert_configured_rules_add_fullpath_V1.sql
-- v_cert_configured_rules 增列 FullPath（NC 规则设计器文件树形选择）
--
-- 背景：NC 规则设计器「文档字段 / 文档表格」节点的「文档」下拉原为扁平
--       el-select（configured-rules 只有 FileName），标准文件多时极难选。
--       2026-10-15 改为「文件夹 → 文件」el-tree-select（buildFolderFileTree），
--       需要文件的文件夹路径。
-- 修法：视图 SELECT 增 `COALESCE(f.FullPath, '') AS FullPath`
--       （cert_standard_directory_file.FullPath 实体列，如「质量/一级/质量手册.doc」）。
--       消费侧 DocExtractionRuleService 由 FullPath 去 FileName 后缀得 folderPath
--       （与实体 StandardDirectoryFile.FolderPath 计算属性同款逻辑）。
-- 同步：scripts/db/views/v_cert_configured_rules.sql（权威定义已同步）
-- 幂等：CREATE OR REPLACE VIEW，可重复执行
-- 执行：口令从 docker/.env 加载（⛔ 禁止把口令写进本脚本）：
--         set -a; . docker/.env; set +a
--         docker exec -i yzh-mysql mysql -uroot -p"$MYSQL_ROOT_PASSWORD" \
--           --default-character-set=utf8mb4 yzh_cert_platform \
--           < scripts/db/fix/20261015_v_cert_configured_rules_add_fullpath_V1.sql
-- 验证：SELECT COLUMN_NAME FROM information_schema.COLUMNS
--         WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'v_cert_configured_rules'
--         AND COLUMN_NAME = 'FullPath';  -- 期望 1 行
-- ============================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

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
