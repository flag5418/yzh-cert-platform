-- =====================================================================
-- 20261123_clause_attr_ch45_corrections_V1.sql
-- 第 4–5 章切片定稿带来的条款属性纠偏（幂等，可重复执行）
--
-- 依据：docs/80-v4/10-第4-5章切片定稿-V1.md §三
--   1) 4.1 / 4.2 的 EvidenceForm 由 cite → sample
--      底稿实况是"查《清单》…抽：影响因素…"，证据是行，不是整本条款。
--   2) 5.2.1 的 EvidenceForm 由 interview → cite
--      方针原文在手册里可逐字引用；5.2.2（沟通）才靠访谈，保持 interview。
--   3) 取证形态判据补一条：「确定…」输出为清单/名录/台账 ⇒ sample
--      原判据只有「规定/建立/明确职责 ⇒ cite；实施/检验/处置/改进 ⇒ sample」，
--      4.1/4.2 的动作词是"确定"，按原规则只能落到"拿不准给 cite"。
--
-- ⛔ 不改：4.4.2 的 Role=r4（成文信息属支持角色，正确）
-- ⛔ 不改：计划明细 4.4.1/4.4.2 两条（走父级回退，不合并）
--
-- 回滚见文末 §5（把三处值改回原值、PromptHint 改回原文）。
-- =====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ---------------------------------------------------------------------
-- §1 改值前快照（执行日志留痕，只查不改）
-- ---------------------------------------------------------------------
SELECT 'BEFORE' AS Phase, StandardCode, ClauseNumber, AttrCode, AttrValue, ValueSource, Status
FROM cert_clause_attr
WHERE IsDeleted = 0
  AND StandardCode = 'iso9001'
  AND ClauseNumber IN ('4.1', '4.2', '5.2.1')
  AND AttrCode = 'EvidenceForm'
ORDER BY ClauseNumber;

-- ---------------------------------------------------------------------
-- §2 4.1 / 4.2：cite → sample
-- ---------------------------------------------------------------------
UPDATE cert_clause_attr
SET AttrValue   = 'sample',
    ValueSource = 'reference',
    Remark      = '第4-5章切片：底稿为"查清单+抽1行"，证据是行（8字段/5字段），非整本条款',
    UpdateTime  = NOW(),
    UpdateBy    = 'fix_20261123_ch45'
WHERE IsDeleted = 0
  AND StandardCode = 'iso9001'
  AND ClauseNumber IN ('4.1', '4.2')
  AND AttrCode = 'EvidenceForm'
  AND AttrValue = 'cite';          -- 幂等：已改过则影响 0 行

-- ---------------------------------------------------------------------
-- §3 5.2.1：interview → cite
-- ---------------------------------------------------------------------
UPDATE cert_clause_attr
SET AttrValue   = 'cite',
    ValueSource = 'reference',
    Remark      = '第4-5章切片：方针原文在手册5.2.1可逐字引用；沟通(5.2.2)才靠访谈',
    UpdateTime  = NOW(),
    UpdateBy    = 'fix_20261123_ch45'
WHERE IsDeleted = 0
  AND StandardCode = 'iso9001'
  AND ClauseNumber = '5.2.1'
  AND AttrCode = 'EvidenceForm'
  AND AttrValue = 'interview';     -- 幂等

-- ---------------------------------------------------------------------
-- §4 取证形态判据补「确定…清单 ⇒ sample」
--    只在原文尚未包含该规则时追加，重复执行不会叠加。
-- ---------------------------------------------------------------------
UPDATE cert_clause_attr_def
SET PromptHint = CONCAT(
      PromptHint,
      '；「确定/识别…」若输出是清单/名录/台账 ⇒ sample（第4/5章定，4.1/4.2 为例）'
    ),
    UpdateTime = NOW(),
    UpdateBy   = 'fix_20261123_ch45'
WHERE IsDeleted = 0
  AND AttrName = '取证形态'
  AND PromptHint NOT LIKE '%若输出是清单/名录/台账%';

-- ---------------------------------------------------------------------
-- §5 改值后验证（期望：4.1=sample, 4.2=sample, 5.2.1=cite；其余不变）
-- ---------------------------------------------------------------------
SELECT 'AFTER' AS Phase, ClauseNumber, AttrValue, ValueSource, LEFT(Remark, 60) AS Remark
FROM cert_clause_attr
WHERE IsDeleted = 0
  AND StandardCode = 'iso9001'
  AND ClauseNumber IN ('4.1', '4.2', '5.2.1')
  AND AttrCode = 'EvidenceForm'
ORDER BY ClauseNumber;

-- 全章 10 条款的取证形态一览（人工核对用）
SELECT ClauseNumber, AttrValue AS EvidenceForm
FROM cert_clause_attr
WHERE IsDeleted = 0
  AND StandardCode = 'iso9001'
  AND ClauseNumber IN ('4.1','4.2','4.3','4.4.1','4.4.2','5.1.1','5.1.2','5.2.1','5.2.2','5.3')
  AND AttrCode = 'EvidenceForm'
ORDER BY ClauseNumber;

SELECT AttrName, LEFT(PromptHint, 200) AS PromptHint
FROM cert_clause_attr_def
WHERE IsDeleted = 0 AND AttrName = '取证形态';

-- ---------------------------------------------------------------------
-- §6 回滚（需要时手工执行，平时不要跑）
-- ---------------------------------------------------------------------
-- UPDATE cert_clause_attr SET AttrValue='cite'
--  WHERE IsDeleted=0 AND StandardCode='iso9001' AND AttrCode='EvidenceForm'
--    AND ClauseNumber IN ('4.1','4.2') AND ValueSource='reference';
-- UPDATE cert_clause_attr SET AttrValue='interview'
--  WHERE IsDeleted=0 AND StandardCode='iso9001' AND AttrCode='EvidenceForm'
--    AND ClauseNumber='5.2.1' AND ValueSource='reference';
-- UPDATE cert_clause_attr_def
--    SET PromptHint = REPLACE(PromptHint,
--        '；「确定/识别…」若输出是清单/名录/台账 ⇒ sample（第4/5章定，4.1/4.2 为例）','')
--  WHERE IsDeleted=0 AND AttrName='取证形态';
