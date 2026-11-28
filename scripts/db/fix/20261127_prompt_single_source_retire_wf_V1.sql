-- ══════════════════════════════════════════════════════════════════════════
-- 20261127 提示词单一化 · wf_prompt_template 四步退役（步骤①：cert 表补列 + 迁移 audit_prep）
--
-- 依据：docs/80-v4/27-提示词单一化与文档大章节归属-执行计划-V1.md §二 0.3 / §九 TODO 0-5、0-6
-- 决策：D1（cert 表补 ModelName/MaxTokens/Temperature 3 列，逐字迁移）
--
-- 事实（2026-11-27 实测）：
--   · wf_prompt_template 全表仅 2 行：audit_prep(1600B, IsActive=1) / audit_check(3848B, IsActive=1)
--   · cert_prompt_template 21 行（全平台级 OrgCode='' / ScopeKey=''），唯独缺 audit_prep
--   · audit_check 退役（PromptTypes.AuditCheck 常量同时删除），不迁移
--
-- 幂等：重复执行不产生重复行；备份表 _bak_*_20261127 用 INSERT IGNORE 去重。
-- 字符集：显式 utf8mb4_general_ci（铁律八）。
-- ⛔ 本脚本只做「补列 + 迁入 + 备份 + 校验」，不做 RENAME（RENAME 在代码全部改造完成后单独执行）。
-- ══════════════════════════════════════════════════════════════════════════
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ── 0) 备份 ────────────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS _bak_cert_prompt_template_20261127 LIKE cert_prompt_template;
INSERT IGNORE INTO _bak_cert_prompt_template_20261127 SELECT * FROM cert_prompt_template;

CREATE TABLE IF NOT EXISTS _bak_wf_prompt_template_20261127 LIKE wf_prompt_template;
INSERT IGNORE INTO _bak_wf_prompt_template_20261127 SELECT * FROM wf_prompt_template;

-- ── 1) cert_prompt_template 补 3 列（D1，幂等）────────────────────────────
SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_prompt_template' AND COLUMN_NAME = 'ModelName');
SET @s := IF(@c = 0,
  'ALTER TABLE cert_prompt_template ADD COLUMN ModelName varchar(255) NULL COMMENT ''模型名（行级覆盖；空=平台统一AI配置）'' AFTER Status',
  'SELECT ''ModelName already exists'' AS note');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_prompt_template' AND COLUMN_NAME = 'MaxTokens');
SET @s := IF(@c = 0,
  'ALTER TABLE cert_prompt_template ADD COLUMN MaxTokens int NULL COMMENT ''最大token（行级覆盖；null=平台统一AI配置）'' AFTER ModelName',
  'SELECT ''MaxTokens already exists'' AS note');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @c := (SELECT COUNT(*) FROM information_schema.COLUMNS
           WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_prompt_template' AND COLUMN_NAME = 'Temperature');
SET @s := IF(@c = 0,
  'ALTER TABLE cert_prompt_template ADD COLUMN Temperature decimal(3,2) NULL COMMENT ''温度（行级覆盖；null=平台统一AI配置）'' AFTER MaxTokens',
  'SELECT ''Temperature already exists'' AS note');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

-- ── 2) 迁移 wf_prompt_template.audit_prep → cert_prompt_template ─────────
--  字段映射（0.3 映射表）：PromptType→PromptCode（逐字）；
--  StandardCode(GUID)→ScopeKey（本行 StandardCode 为 NULL ⇒ ScopeKey=''，平台级）；
--  Description→Remark（截断 255）；IsActive=1→IsValid=1；逐字迁 3 个模型参数。
--  ⚠️ 若将来 wf 出现 StandardCode 为 GUID 的行，须先 join cert_iso_standard 转短码再迁（本行无需）。
INSERT INTO cert_prompt_template
  (PromptCode, PromptName, OrgCode, ScopeKey, Template, Placeholders, Remark, Sort,
   IsValid, Status, Code, ModelName, MaxTokens, Temperature, IsDeleted, CreateTime, CreateBy)
SELECT
  w.PromptCode,
  IFNULL(NULLIF(w.PromptName, ''), w.PromptCode),
  IFNULL(w.OrgCode, ''),
  IFNULL(NULLIF(w.StandardCode, ''), ''),
  w.Template,
  '',                                                    -- Placeholders NOT NULL
  LEFT(w.Description, 255),                              -- Remark varchar(255)
  NULL,
  1,
  IFNULL(NULLIF(w.Status, ''), 'active'),
  IFNULL(NULLIF(w.Code, ''), REPLACE(UUID(), '-', '')),
  w.ModelName, w.MaxTokens, w.Temperature,
  0,
  IFNULL(w.CreateTime, NOW()),
  w.CreateBy
FROM wf_prompt_template w
WHERE w.PromptCode = 'audit_prep'
  AND w.IsDeleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM cert_prompt_template c
      WHERE c.PromptCode = 'audit_prep' AND IFNULL(c.ScopeKey, '') = '' AND c.IsDeleted = 0
  );

-- ── 3) 软删老表 audit_check（按 0.6；整表 RENAME 后亦无引用）──────────────
UPDATE wf_prompt_template SET IsValid = 0, IsDeleted = 1, DeleteTime = NOW()
WHERE PromptCode = 'audit_check' AND IsDeleted = 0;

-- ── 4) 校验 ───────────────────────────────────────────────────────────────
SELECT '1_md5_compare' AS step,
  (SELECT MD5(Template) FROM wf_prompt_template WHERE PromptCode = 'audit_prep' LIMIT 1) AS wf_md5,
  (SELECT MD5(Template) FROM cert_prompt_template WHERE PromptCode = 'audit_prep' AND IsDeleted = 0 LIMIT 1) AS cert_md5;

SELECT '2_cert_rows' AS step,
  (SELECT COUNT(*) FROM cert_prompt_template WHERE IsDeleted = 0) AS cert_total,
  (SELECT COUNT(*) FROM cert_prompt_template WHERE PromptCode = 'audit_prep' AND IsDeleted = 0) AS audit_prep_rows;

SELECT '3_new_cols' AS step, COLUMN_NAME, COLUMN_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_prompt_template'
  AND COLUMN_NAME IN ('ModelName', 'MaxTokens', 'Temperature')
ORDER BY COLUMN_NAME;
