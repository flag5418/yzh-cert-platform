-- ══════════════════════════════════════════════════════════════════════════
-- 20261127 提示词单一化 · wf_prompt_template 四步退役（步骤④：整表 RENAME 退役）
--
-- 依据：docs/80-v4/27-提示词单一化与文档大章节归属-执行计划-V1.md §二 0.3 表退役步骤 ③ / §九 TODO 0-8
-- 前置：步骤①（scripts/db/fix/20261127_prompt_single_source_retire_wf_V1.sql）已执行
--       —— cert_prompt_template 已补 ModelName/MaxTokens/Temperature 三列，audit_prep 已迁入。
--       Entities/Wf/PromptTemplate.cs 实体已删除（防 CodeFirst 重建 wf 表）。
--
-- ⛔ 本脚本只 RENAME，不 DROP（退役表 zz_retired_wf_prompt_template 保留可回滚）。
-- ⛔ 幂等：重复执行不报错；已退役则跳过。
-- 字符集：显式 utf8mb4_general_ci（铁律八）。
-- ══════════════════════════════════════════════════════════════════════════
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ── 0) 备份（若步骤①已建则复用）──────────────────────────────────────────
CREATE TABLE IF NOT EXISTS _bak_wf_prompt_template_20261127 LIKE wf_prompt_template;
INSERT IGNORE INTO _bak_wf_prompt_template_20261127 SELECT * FROM wf_prompt_template;

-- ── 1) 迁移前后审计：逐生效行正文 MD5（RENAME 前快照）────────────────────
SELECT '0_wf_active_md5' AS step, PromptCode, MD5(Template) AS wf_md5
FROM wf_prompt_template
WHERE IsValid = 1 AND IsDeleted = 0
ORDER BY PromptCode;

-- ── 2) 整表退役 RENAME（幂等）────────────────────────────────────────────
SET @exists_old := (SELECT COUNT(*) FROM information_schema.TABLES
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_prompt_template');
SET @exists_new := (SELECT COUNT(*) FROM information_schema.TABLES
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'zz_retired_wf_prompt_template');
SET @sql := IF(@exists_old = 1 AND @exists_new = 0,
  'RENAME TABLE wf_prompt_template TO zz_retired_wf_prompt_template',
  'SELECT ''skip rename (old exists or already retired)'' AS note');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

-- ── 3) 校验 ──────────────────────────────────────────────────────────────
-- 3.1 wf 表已不存在；退役表存在
SELECT '1_table_state' AS step, TABLE_NAME, TABLE_ROWS
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('wf_prompt_template', 'zz_retired_wf_prompt_template', 'cert_prompt_template');

-- 3.2 生效 cert 行覆盖的系统提示词类型（应含 audit_prep / clause_evidence_findings / audit_check_evidence）
SELECT '2_cert_system_prompts' AS step, PromptCode, IsValid, CHAR_LENGTH(Template) AS tlen
FROM cert_prompt_template
WHERE PromptCode IN ('audit_prep', 'clause_evidence_findings', 'audit_check_evidence', 'clause_attr_gen')
  AND IsDeleted = 0
ORDER BY PromptCode;

-- 3.3 audit_prep 迁移 MD5 比对（wf 备份 vs cert 生效）
SELECT '3_audit_prep_md5' AS step,
  (SELECT MD5(Template) FROM _bak_wf_prompt_template_20261127 WHERE PromptCode = 'audit_prep' LIMIT 1) AS wf_bak_md5,
  (SELECT MD5(Template) FROM cert_prompt_template WHERE PromptCode = 'audit_prep' AND IsDeleted = 0 LIMIT 1) AS cert_md5;
