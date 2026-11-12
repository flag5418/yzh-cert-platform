-- ============================================================================
-- 20261110_clause_attr_v3_pool_V1.sql
-- V3 条款属性池扩容（A2）：列宽修正 + 三条新属性定义 + 三个新字典
--
-- 变更日志
--   2026-11-10 新建
--     ① cert_clause_attr.AttrValue     varchar(255) → TEXT   （Artifacts/EvidenceChain 是 500+ 字提示词，255 存不下）
--     ② cert_prompt_template.Template  varchar(255) → TEXT   （提示词正文 3000+ 字，255 存不下 ⇒ 一直存不进去）
--     ③ 新增字典 clause_evidence_form / clause_evidence_family / clause_role
--     ④ cert_clause_attr_def 新增 EvidenceForm / EvidenceFamily / Role（8 → 11 条）
--
-- 幂等：全部为 ALTER / INSERT ... WHERE NOT EXISTS，可重复执行
-- 执行：docker exec -i yzh-mysql mysql -uroot -p*** --default-character-set=utf8mb4 yzh_cert_platform < 本文件
-- ============================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ── ① 值列扩容（STRICT_TRANS_TABLES 下超长会报 1406，不是静默截断）────────────
ALTER TABLE cert_clause_attr
    MODIFY COLUMN AttrValue TEXT NULL COMMENT '值；json 存 JSON 原文，prompt 存提示词正文';

-- ── ② 提示词正文本体 ────────────────────────────────────────────────────────
ALTER TABLE cert_prompt_template
    MODIFY COLUMN Template TEXT NOT NULL COMMENT '提示词正文（含 {{占位符}}）';

-- ── ③ 字典：取证形态 ────────────────────────────────────────────────────────
INSERT INTO Sys_Dictionary (Code, DicName, DicNo, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CLAUSE_EVIDENCE_FORM', '条款取证形态', 'clause_evidence_form', 10, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_Dictionary WHERE Code = 'CLAUSE_EVIDENCE_FORM');

INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CEF_cite', 'CLAUSE_EVIDENCE_FORM', '引用式（引文件/章节/版本，做概括）', 'cite', 10, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'CLAUSE_EVIDENCE_FORM' AND DicValue = 'cite');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CEF_sample', 'CLAUSE_EVIDENCE_FORM', '抽样式（抽若干份记录逐份列要素）', 'sample', 20, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'CLAUSE_EVIDENCE_FORM' AND DicValue = 'sample');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CEF_site', 'CLAUSE_EVIDENCE_FORM', '现场式（看环境/设施/标识/实物）', 'site', 30, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'CLAUSE_EVIDENCE_FORM' AND DicValue = 'site');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CEF_interview', 'CLAUSE_EVIDENCE_FORM', '访谈式（问人，记岗位与回答要点）', 'interview', 40, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'CLAUSE_EVIDENCE_FORM' AND DicValue = 'interview');

-- ── ③ 字典：条款族 ──────────────────────────────────────────────────────────
INSERT INTO Sys_Dictionary (Code, DicName, DicNo, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CLAUSE_EVIDENCE_FAMILY', '条款族（制度/执行）', 'clause_evidence_family', 20, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_Dictionary WHERE Code = 'CLAUSE_EVIDENCE_FAMILY');

INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CFF_f1', 'CLAUSE_EVIDENCE_FAMILY', '制度族（企业规定了什么）', 'f1', 10, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'CLAUSE_EVIDENCE_FAMILY' AND DicValue = 'f1');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CFF_f2', 'CLAUSE_EVIDENCE_FAMILY', '执行族（实际做了什么）', 'f2', 20, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'CLAUSE_EVIDENCE_FAMILY' AND DicValue = 'f2');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CFF_f1f2', 'CLAUSE_EVIDENCE_FAMILY', '混合族（建立并实施）', 'f1f2', 30, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'CLAUSE_EVIDENCE_FAMILY' AND DicValue = 'f1f2');

-- ── ③ 字典：功能角色 ────────────────────────────────────────────────────────
INSERT INTO Sys_Dictionary (Code, DicName, DicNo, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CLAUSE_ROLE', '条款功能角色 R1–R7', 'clause_role', 30, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_Dictionary WHERE Code = 'CLAUSE_ROLE');

INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CR_r1', 'CLAUSE_ROLE', 'R1 情境（组织环境/相关方/范围）', 'r1', 10, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'CLAUSE_ROLE' AND DicValue = 'r1');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CR_r2', 'CLAUSE_ROLE', 'R2 领导（方针/职责/承诺）', 'r2', 20, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'CLAUSE_ROLE' AND DicValue = 'r2');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CR_r3', 'CLAUSE_ROLE', 'R3 策划（风险/目标/变更）', 'r3', 30, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'CLAUSE_ROLE' AND DicValue = 'r3');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CR_r4', 'CLAUSE_ROLE', 'R4 支持（资源/人员/知识/文件）', 'r4', 40, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'CLAUSE_ROLE' AND DicValue = 'r4');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CR_r5', 'CLAUSE_ROLE', 'R5 运行（设计/采购/生产/交付/放行）', 'r5', 50, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'CLAUSE_ROLE' AND DicValue = 'r5');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CR_r6', 'CLAUSE_ROLE', 'R6 绩效（监视/内审/管评/合规评价）', 'r6', 60, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'CLAUSE_ROLE' AND DicValue = 'r6');
INSERT INTO Sys_DictionaryList (Code, DicCode, DicName, DicValue, OrderNo, IsValid, IsDeleted, CreateTime)
SELECT 'CR_r7', 'CLAUSE_ROLE', 'R7 改进（不合格/纠正/持续改进）', 'r7', 70, 1, 0, NOW()
WHERE NOT EXISTS (SELECT 1 FROM Sys_DictionaryList WHERE DicCode = 'CLAUSE_ROLE' AND DicValue = 'r7');

-- ── ④ 属性定义：8 → 11 ──────────────────────────────────────────────────────
INSERT INTO cert_clause_attr_def (Code, AttrName, ValueType, DictCode, Remark, PromptHint, Sort, IsValid, IsDeleted, Status, CreateTime)
SELECT 'EvidenceForm', '取证形态', 'dict', 'clause_evidence_form',
       '这一段证据该以什么形态呈现：引文件、抽记录、看现场、还是访谈',
       '按条款动作词判：规定/建立/明确职责 ⇒ cite；实施/检验/处置/改进 ⇒ sample；环境设施标识 ⇒ site；方针承诺与职责访谈 ⇒ interview。拿不准给 cite',
       90, 1, 0, 'active', NOW()
WHERE NOT EXISTS (SELECT 1 FROM cert_clause_attr_def WHERE Code = 'EvidenceForm');

INSERT INTO cert_clause_attr_def (Code, AttrName, ValueType, DictCode, Remark, PromptHint, Sort, IsValid, IsDeleted, Status, CreateTime)
SELECT 'EvidenceFamily', '条款族', 'dict', 'clause_evidence_family',
       '制度族 F1（企业规定了什么）/执行族 F2（实际做了什么），决定引用还是抽样',
       '按条款原文的动作词判，⛔ 不按章号判：建立/规定/确保/明确/策划 ⇒ f1；实施/执行/检验/处置/改进/评价 ⇒ f2；两者都有（建立并实施）⇒ f1f2',
       100, 1, 0, 'active', NOW()
WHERE NOT EXISTS (SELECT 1 FROM cert_clause_attr_def WHERE Code = 'EvidenceFamily');

INSERT INTO cert_clause_attr_def (Code, AttrName, ValueType, DictCode, Remark, PromptHint, Sort, IsValid, IsDeleted, Status, CreateTime)
SELECT 'Role', '功能角色', 'dict', 'clause_role',
       '条款在体系中的功能角色 R1–R7，决定证据骨架（章节只用于排版顺序，不作判据）',
       '按条款功能判：情境/领导/策划/支持/运行/绩效/改进 七选一；跨标准时同功能条款给同一角色（如 9001 8.2 与 13485 7.2 都是 r5）',
       110, 1, 0, 'active', NOW()
WHERE NOT EXISTS (SELECT 1 FROM cert_clause_attr_def WHERE Code = 'Role');

-- ── 验证 ────────────────────────────────────────────────────────────────────
SELECT '属性定义条数' AS 检查项, COUNT(*) AS 值 FROM cert_clause_attr_def WHERE IsValid = 1 AND IsDeleted = 0
UNION ALL SELECT '新字典条数', COUNT(*) FROM Sys_Dictionary WHERE DicNo IN ('clause_evidence_form','clause_evidence_family','clause_role')
UNION ALL SELECT '字典项条数', COUNT(*) FROM Sys_DictionaryList WHERE DicCode IN ('CLAUSE_EVIDENCE_FORM','CLAUSE_EVIDENCE_FAMILY','CLAUSE_ROLE')
UNION ALL SELECT 'AttrValue 类型', 0 FROM DUAL WHERE (SELECT DATA_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='cert_clause_attr' AND COLUMN_NAME='AttrValue')='text'
UNION ALL SELECT 'Template 类型', 0 FROM DUAL WHERE (SELECT DATA_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='cert_prompt_template' AND COLUMN_NAME='Template')='text';
