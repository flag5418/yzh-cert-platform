-- ============================================================================
-- 20261106_restore_missing_dictionaries_V1.sql
-- 补齐「数据库重建后遗漏未补种」的数据字典
--
-- 背景 / 根因：
--   2026-11-05 CodeFirst 重建（DB 清空）后，字典只补种了 org_type / iso_category /
--   stage_category / clause_* 等，遗漏了 6 个业务页面在用的字典。
--   症状①（已复现）：打开「技能管理 / 工作流设计器」→ 前端 expectOk 直接报
--       「字典编码【skill_category】不存在或已禁用」（已于 09:54 由外部脚本补入 5 项）。
--   症状②（静默降级）：EntityConfig 里 DictCode 字段（YzhForm 自动取
--       `/api/System/Dictionary/items/by-no/{dicNo}`）取不到字典 → 下拉空白 / 原值直出；
--       其中 PromptTemplate.SkillTarget 是必填，空白将直接卡住「文档语义规则」保存。
--
-- 本脚本补种（字典头 + 字典项）：
--   prompt_type / skill_target       ← 原种子 20261002_prompt_workbench_V1.sql §三（逐字对齐）
--   fill_value_type / fill_source_kind / fill_maintain_mode
--                                   ← 原种子 20261002_fill_param_seed_V1.sql §一（逐字对齐）
--   gender（0=男 / 1=女）            ← 口径见 docs/10-YZH架构/08-常见错误与修复.md E105
--                                       （cert_audit_member.Gender 为 int，DicValue 保持字符串由前端 coerce）
--
-- 幂等：Sys_Dictionary / Sys_DictionaryList 仅有主键（无唯一约束），
--       重跑靠 `NOT EXISTS + EXISTS` 保护；已存在（同 DicNo / 同 DicValue）则跳过。
--   ParentCode 一律 NULL —— 与当前库现状一致（重建后所有字典均为根级）。
--   CreateBy 统一用白名单标记 'restore'（scripts/db/verify/verify_createby.sql 已登记）。
--   ★ 顺带把外部脚本遗留的 'restore_20261106' 归一为白名单词 'restore'（否则 C_违规 +6）。
--
-- 执行：
--   docker exec -i yzh-mysql sh -c 'mysql -uroot -p"$MYSQL_ROOT_PASSWORD" --default-character-set=utf8mb4 yzh_cert_platform' < 本文件
-- ============================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────────────────────
-- 0. 审计口径归一（'白名单' 只登记整词，'restore' 是已登记标记）
-- ────────────────────────────────────────────────────────────────────────────
UPDATE Sys_Dictionary     SET CreateBy = 'restore' WHERE CreateBy = 'restore_20261106';
UPDATE Sys_DictionaryList SET CreateBy = 'restore' WHERE CreateBy = 'restore_20261106';

-- ────────────────────────────────────────────────────────────────────────────
-- 1. 提示词类型 prompt_type（wf_prompt_template.PromptType 下拉）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO Sys_Dictionary (Code, DicName, DicNo, ParentCode, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy, Remark)
SELECT 'PROMPT_TYPE', '提示词类型', 'prompt_type', NULL, 400, 1, 0, NOW(), 'restore',
       'wf_prompt_template.PromptType 下拉（原 20261002_prompt_workbench_V1.sql §三）'
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'prompt_type' AND IsDeleted = 0);

INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'PT_doc_group', 'PROMPT_TYPE', '文档分类（分组提示词）', 'doc_group', 10, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'prompt_type' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'PROMPT_TYPE' AND DicValue = 'doc_group');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'PT_doc_content', 'PROMPT_TYPE', '文档作用（内容提示词）', 'doc_content', 20, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'prompt_type' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'PROMPT_TYPE' AND DicValue = 'doc_content');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'PT_prompt_generator', 'PROMPT_TYPE', '提示词生成器（元提示词）', 'prompt_generator', 30, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'prompt_type' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'PROMPT_TYPE' AND DicValue = 'prompt_generator');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'PT_document_analysis', 'PROMPT_TYPE', '文档分析（字段/表格提取）', 'document_analysis', 40, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'prompt_type' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'PROMPT_TYPE' AND DicValue = 'document_analysis');

-- ────────────────────────────────────────────────────────────────────────────
-- 2. 适用技能 skill_target（wf_prompt_template.SkillTarget 下拉，必填项）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO Sys_Dictionary (Code, DicName, DicNo, ParentCode, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy, Remark)
SELECT 'SKILL_TARGET', '适用技能', 'skill_target', NULL, 410, 1, 0, NOW(), 'restore',
       'wf_prompt_template.SkillTarget 下拉（原 20261002_prompt_workbench_V1.sql §三）'
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'skill_target' AND IsDeleted = 0);

INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'ST_all', 'SKILL_TARGET', '通用（不限定技能）', 'all', 10, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'skill_target' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'SKILL_TARGET' AND DicValue = 'all');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'ST_analyze_word', 'SKILL_TARGET', 'Word 文档分析', 'analyze_word', 20, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'skill_target' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'SKILL_TARGET' AND DicValue = 'analyze_word');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'ST_analyze_excel', 'SKILL_TARGET', 'Excel 文档分析', 'analyze_excel', 30, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'skill_target' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'SKILL_TARGET' AND DicValue = 'analyze_excel');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'ST_analyze_pdf', 'SKILL_TARGET', 'PDF 文档分析', 'analyze_pdf', 40, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'skill_target' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'SKILL_TARGET' AND DicValue = 'analyze_pdf');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'ST_nc_judge', 'SKILL_TARGET', 'NC 判定', 'nc_judge', 50, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'skill_target' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'SKILL_TARGET' AND DicValue = 'nc_judge');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'ST_nc_conclude', 'SKILL_TARGET', 'NC 结论', 'nc_conclude', 60, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'skill_target' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'SKILL_TARGET' AND DicValue = 'nc_conclude');

-- ────────────────────────────────────────────────────────────────────────────
-- 3. 全局参数值类型 fill_value_type（cert_fill_param_def.ValueType 下拉）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO Sys_Dictionary (Code, DicName, DicNo, ParentCode, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy, Remark)
SELECT 'FILL_VALUE_TYPE', '全局参数值类型', 'fill_value_type', NULL, 300, 1, 0, NOW(), 'restore',
       '体系认证全局参数 ValueType 下拉（原 20261002_fill_param_seed_V1.sql §一）'
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_value_type' AND IsDeleted = 0);

INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'FVT_text', 'FILL_VALUE_TYPE', '文本', 'text', 10, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_value_type' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'FILL_VALUE_TYPE' AND DicValue = 'text');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'FVT_number', 'FILL_VALUE_TYPE', '数字', 'number', 20, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_value_type' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'FILL_VALUE_TYPE' AND DicValue = 'number');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'FVT_date', 'FILL_VALUE_TYPE', '日期', 'date', 30, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_value_type' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'FILL_VALUE_TYPE' AND DicValue = 'date');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'FVT_enum', 'FILL_VALUE_TYPE', '枚举', 'enum', 40, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_value_type' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'FILL_VALUE_TYPE' AND DicValue = 'enum');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'FVT_bool', 'FILL_VALUE_TYPE', '是否', 'bool', 50, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_value_type' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'FILL_VALUE_TYPE' AND DicValue = 'bool');

-- ────────────────────────────────────────────────────────────────────────────
-- 4. 全局参数取值来源 fill_source_kind（cert_fill_param_def.SourceKind 下拉）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO Sys_Dictionary (Code, DicName, DicNo, ParentCode, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy, Remark)
SELECT 'FILL_SOURCE_KIND', '全局参数取值来源', 'fill_source_kind', NULL, 310, 1, 0, NOW(), 'restore',
       '体系认证全局参数 SourceKind 下拉（原 20261002_fill_param_seed_V1.sql §一）'
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_source_kind' AND IsDeleted = 0);

INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'FSK_global', 'FILL_SOURCE_KIND', '全局参数', 'global', 10, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_source_kind' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'FILL_SOURCE_KIND' AND DicValue = 'global');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'FSK_replace', 'FILL_SOURCE_KIND', '替换', 'replace', 20, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_source_kind' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'FILL_SOURCE_KIND' AND DicValue = 'replace');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'FSK_headerFooter', 'FILL_SOURCE_KIND', '页眉页脚', 'headerFooter', 30, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_source_kind' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'FILL_SOURCE_KIND' AND DicValue = 'headerFooter');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'FSK_ai', 'FILL_SOURCE_KIND', 'AI 生成', 'ai', 40, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_source_kind' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'FILL_SOURCE_KIND' AND DicValue = 'ai');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'FSK_manual', 'FILL_SOURCE_KIND', '人工填写', 'manual', 50, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_source_kind' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'FILL_SOURCE_KIND' AND DicValue = 'manual');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'FSK_compute', 'FILL_SOURCE_KIND', '计算派生', 'compute', 60, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_source_kind' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'FILL_SOURCE_KIND' AND DicValue = 'compute');

-- ────────────────────────────────────────────────────────────────────────────
-- 5. 全局参数维护方式 fill_maintain_mode（cert_fill_param_def.MaintainMode 下拉）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO Sys_Dictionary (Code, DicName, DicNo, ParentCode, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy, Remark)
SELECT 'FILL_MAINTAIN_MODE', '全局参数维护方式', 'fill_maintain_mode', NULL, 320, 1, 0, NOW(), 'restore',
       '体系认证全局参数 MaintainMode 下拉（原 20261002_fill_param_seed_V1.sql §一）'
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_maintain_mode' AND IsDeleted = 0);

INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'FMM_auto', 'FILL_MAINTAIN_MODE', '自动映射（企业只读）', 'auto', 10, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_maintain_mode' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'FILL_MAINTAIN_MODE' AND DicValue = 'auto');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'FMM_manual', 'FILL_MAINTAIN_MODE', '企业手工填写', 'manual', 20, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_maintain_mode' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'FILL_MAINTAIN_MODE' AND DicValue = 'manual');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'FMM_both', 'FILL_MAINTAIN_MODE', '自动带出可覆盖', 'both', 30, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'fill_maintain_mode' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'FILL_MAINTAIN_MODE' AND DicValue = 'both');

-- ────────────────────────────────────────────────────────────────────────────
-- 6. 性别 gender（cert_audit_member.Gender，int ↔ DicValue '0'/'1'，
--    口径见 docs/10-YZH架构/08-常见错误与修复.md E105）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO Sys_Dictionary (Code, DicName, DicNo, ParentCode, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy, Remark)
SELECT 'GENDER', '性别', 'gender', NULL, 980, 1, 0, NOW(), 'restore',
       '审核成员性别（E105：0=男 1=女，int 列由前端 coerceDictValue 对齐）'
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'gender' AND IsDeleted = 0);

INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'GD_male', 'GENDER', '男', '0', 10, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'gender' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'GENDER' AND DicValue = '0');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime, CreateBy)
SELECT 'GD_female', 'GENDER', '女', '1', 20, 1, 0, NOW(), 'restore' FROM DUAL
WHERE EXISTS (SELECT 1 FROM Sys_Dictionary WHERE DicNo = 'gender' AND IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'GENDER' AND DicValue = '1');

-- ────────────────────────────────────────────────────────────────────────────
-- 验证（期望：每个 DicNo 1 头 + 预期项数）
-- ────────────────────────────────────────────────────────────────────────────
SELECT d.DicNo, d.DicName, COUNT(l.Id) AS ItemCount
FROM Sys_Dictionary d
JOIN Sys_DictionaryList l ON l.DicCode = d.Code AND l.IsValid = 1 AND l.IsDeleted = 0
WHERE d.IsDeleted = 0 AND d.DicNo IN
      ('prompt_type', 'skill_target', 'fill_value_type', 'fill_source_kind', 'fill_maintain_mode', 'gender')
GROUP BY d.DicNo, d.DicName
ORDER BY d.DicNo;
-- 期望：prompt_type 4 / skill_target 6 / fill_value_type 5 / fill_source_kind 6 / fill_maintain_mode 3 / gender 2

SELECT DicNo, DicName, IsValid, IsDeleted FROM Sys_Dictionary
WHERE DicNo IN ('prompt_type', 'skill_target', 'fill_value_type', 'fill_source_kind', 'fill_maintain_mode', 'gender')
ORDER BY OrderNo;
