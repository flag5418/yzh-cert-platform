-- ============================================================================
-- 条款属性池：新增 DocCheckMode（资料核对方式，三态）
-- 日期：2026-10-29 ｜ 幂等：可重复执行
-- 依据：docs/70-V2设计/35-文档提取卡与条款检索-V1.md §二
--
-- 作用：决定「认证准备阶段要不要拿企业资料核对这条款」
--   required = 合规证据主要是成文信息，不看资料无法判断
--   optional = 现场观察/访谈才是主证据，资料只辅助（默认）
--   none     = 纯现场/纯访谈，资料核对无意义 ⇒ 直接生成现场任务
--
-- 连锁收益：① L1 提取只把这些条款给模型（提示词更短）② L2 跳过 none
-- ============================================================================

-- ── 1. 字典主表 ─────────────────────────────────────────────────────────────
INSERT INTO `Sys_Dictionary`
  (`Code`, `DicNo`, `DicName`, `OrderNo`, `IsValid`, `IsDeleted`, `Remark`)
VALUES
  ('CLAUSE_DOC_CHECK_MODE', 'clause_doc_check_mode', '条款资料核对方式', 935, 1, 0,
   'required=必须靠文档证据 / optional=现场为主、文档辅助 / none=纯现场，不核对资料')
ON DUPLICATE KEY UPDATE
  `DicName` = VALUES(`DicName`),
  `OrderNo` = VALUES(`OrderNo`),
  `Remark`  = VALUES(`Remark`),
  `IsValid` = 1,
  `IsDeleted` = 0;

-- ── 2. 字典明细（三条） ─────────────────────────────────────────────────────
INSERT INTO `Sys_DictionaryList`
  (`Code`, `DicCode`, `DicName`, `DicValue`, `OrderNo`, `IsValid`, `IsDeleted`, `Remark`)
SELECT 'CD_DCM_required', 'CLAUSE_DOC_CHECK_MODE', '必须查资料', 'required', 10, 1, 0,
       '文件控制/记录控制/内审/管理评审/确认记录/投诉记录等，不看资料无法判断'
WHERE NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList`
                  WHERE `DicCode` = 'CLAUSE_DOC_CHECK_MODE' AND `DicValue` = 'required');

INSERT INTO `Sys_DictionaryList`
  (`Code`, `DicCode`, `DicName`, `DicValue`, `OrderNo`, `IsValid`, `IsDeleted`, `Remark`)
SELECT 'CD_DCM_optional', 'CLAUSE_DOC_CHECK_MODE', '资料辅助（默认）', 'optional', 20, 1, 0,
       '现场观察/访谈才是主证据，资料只能辅助（职责权限/人力资源/工作环境/生产控制等）'
WHERE NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList`
                  WHERE `DicCode` = 'CLAUSE_DOC_CHECK_MODE' AND `DicValue` = 'optional');

INSERT INTO `Sys_DictionaryList`
  (`Code`, `DicCode`, `DicName`, `DicValue`, `OrderNo`, `IsValid`, `IsDeleted`, `Remark`)
SELECT 'CD_DCM_none', 'CLAUSE_DOC_CHECK_MODE', '不查资料', 'none', 30, 1, 0,
       '纯现场或纯访谈（承诺类/方针类表述），资料核对无意义 ⇒ 直接生成现场任务'
WHERE NOT EXISTS (SELECT 1 FROM `Sys_DictionaryList`
                  WHERE `DicCode` = 'CLAUSE_DOC_CHECK_MODE' AND `DicValue` = 'none');

-- ── 3. 属性池新增第 9 条 ────────────────────────────────────────────────────
INSERT INTO `cert_clause_attr_def`
  (`Code`, `AttrName`, `ValueType`, `DictCode`, `Remark`, `PromptHint`, `Sort`, `IsValid`, `IsDeleted`)
SELECT
  'DocCheckMode',
  '资料核对方式',
  'dict',
  'clause_doc_check_mode',
  '决定认证准备阶段要不要拿企业资料核对这条款；none 的条款直接生成现场任务，不进资料核对',
  '判断这条款要不要靠企业资料来查，三选一：\n① required = 该条款的合规证据主要是成文信息（文件控制、记录控制、内审、管理评审、过程确认记录、投诉记录等），不看资料就无法判断；\n② optional = 现场观察/访谈才是主证据，资料只能辅助（职责权限、人力资源、工作环境、生产和服务提供等）；\n③ none = 纯现场或纯访谈，资料核对无意义（承诺类、方针类、资源提供类表述）。\n⚠️ 拿不准时给 optional，⛔ 不要一律给 required —— 一律 required 会把「我们没查」当成「企业没有」。',
  85,
  1,
  0
WHERE NOT EXISTS (SELECT 1 FROM `cert_clause_attr_def` WHERE `Code` = 'DocCheckMode');

-- ── 4. 验证 ─────────────────────────────────────────────────────────────────
SELECT '字典' AS Kind, `DicNo` AS Code, `DicName` AS Name FROM `Sys_Dictionary`
 WHERE `DicNo` = 'clause_doc_check_mode'
UNION ALL
SELECT '明细', `DicValue`, `DicName` FROM `Sys_DictionaryList`
 WHERE `DicCode` = 'CLAUSE_DOC_CHECK_MODE' AND `IsDeleted` = 0
UNION ALL
SELECT '属性', `Code`, `AttrName` FROM `cert_clause_attr_def`
 WHERE `Code` = 'DocCheckMode' AND `IsDeleted` = 0;
