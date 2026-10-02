-- ============================================================
-- 20260929_doc_extraction_rule_orgcode_V1.sql
-- S0：规则表加机构维度（docs/40-实施/企业资料管理/10-执行计划-V1.md §七①）
--
-- 背景：cert_doc_extraction_rule 原唯一键 = uk_standard_file_code (StandardFileCode) 单列，
--       「按机构追溯 机构→标准→阶段→文档 四元组」做不到；本脚本改为四元组唯一。
-- 只改 1 张表：cert_doc_field_def / cert_doc_table_def / cert_doc_table_field_def
--             经 RuleCode 回溯规则，不加 OrgCode（10 号 §三）。
-- ⚠️ 重复执行会报 Duplicate column name 'OrgCode' / Duplicate key（本脚本非幂等）。
-- 字符集：utf8mb4 + utf8mb4_general_ci（铁律八）。
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

ALTER TABLE cert_doc_extraction_rule
  ADD COLUMN OrgCode varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL
    COMMENT '认证机构 Code（模板所属机构）；企业侧按 机构+标准+阶段+文档 四元组定位规则'
    AFTER Code,
  DROP INDEX uk_standard_file_code,
  ADD UNIQUE KEY uk_rule_scope (OrgCode, StandardCode, StageCode, StandardFileCode);

-- ── 存量回填：规则的机构 = 规则键（模板文件行）→ 其 ConfigCode → 配置的 OrgCode ──
UPDATE cert_doc_extraction_rule r
  JOIN cert_standard_directory_file f ON f.Code = r.StandardFileCode
  JOIN cert_standard_directory_config c ON c.Code = f.ConfigCode
   SET r.OrgCode = c.OrgCode
 WHERE r.OrgCode IS NULL
   AND c.OrgCode IS NOT NULL AND c.OrgCode <> '';

-- ── 验证（人工执行，期望：1 行 OrgCode 非空 + uk_rule_scope 存在）────────────
-- SELECT Code, OrgCode, StandardCode, StageCode, StandardFileCode, Status, IsValid
--   FROM cert_doc_extraction_rule;
-- SHOW INDEX FROM cert_doc_extraction_rule;
