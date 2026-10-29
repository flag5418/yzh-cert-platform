-- ============================================================================
-- 20261029_clause_attr_prompt_V3.sql — 「条款属性预生成」提示词 **V3**（★ 幂等）
-- ============================================================================
-- 为什么出 V3（2026-10-29 用户裁定「先把 13485 的条款属性跑完」时发现的三个硬问题）：
--
--   ① **属性清单被写死在正文里**：v1/v2 的正文含 `{"ClauseIntent": ...}` 骨架，只列 8 条属性。
--      属性池新增第 9 条 `DocCheckMode` 后，模型**不会输出这个字段** ⇒ 落库静默缺失、
--      全程不报错，只表现为「这条属性怎么一直是空的」。
--      V3 改：正文只写**判据**，字段清单唯一来源 = `{{attr_pool}}` 占位符，
--      由 `ClauseAttrPromptService.BuildPoolSectionAsync` 按 `cert_clause_attr_def` 实时渲染
--      ⇒ 池子增删改，预生成自动跟上。
--
--   ② **两处提示词表并存、各读一份**：页面「提示词」抽屉编辑 `cert_prompt_template.clause_attr_gen`，
--      而 `ClauseAttrGenerateService` 读的是 `wf_prompt_template.clause_attr_v1/v2`
--      ⇒ 用户改了正文一点变化都没有（典型「后台看 A、实际跑 B」）。
--      V3 起唯一来源 = `cert_prompt_template`；`wf_prompt_template` 的两条 clause_attr 行**停用并标注弃用**。
--
--   ③ **两条 clause_attr 都是 IsActive=0**：`ResolveActiveAsync` 命中 0 条直接返回 null，
--      预生成会报「未找到生效的提示词」—— 即使正文没问题也跑不动。
--
-- ★ 幂等口径：
--   `cert_prompt_template` 的 `uk_prompt_scope` 是 (PromptCode, OrgCode, ScopeKey) 且后两列可空
--   ⇒ MySQL 唯一索引**允许多条 NULL**，`ON DUPLICATE KEY` 挡不住重复插入。
--   故这里用 `UPDATE` + `INSERT ... WHERE NOT EXISTS` 两段式。
--
-- ⚠️ 正文与 `ClauseAttrPromptService.FallbackTemplate` **逐字一致**；
--    改一处必须同步改另一处，否则「库里跑一套、兜底跑另一套」。
-- ============================================================================

-- ── 1. 提示词正文（唯一来源）────────────────────────────────────────────────
UPDATE `cert_prompt_template`
   SET `Template` = '你是 ISO 管理体系认证的资深审核技术专家。你的任务：读一条标准条款，为这条条款产出「属性配置」JSON。

## 输入

- 标准：{{standard_name}}
- 条款号：{{clause_number}}
- 条款标题：{{clause_title}}
- 条款正文：
{{clause_content}}

## 输出

只输出一个 JSON 对象，不要 markdown 代码块，不要任何解释文字。
字段名 = 文末「属性清单」里的字段名，**逐个都要输出**；⛔ 不许增、删、改名，⛔ 不许输出清单外的字段。

## 通用判据

① 先读条款正文的**主句**（第一句带义务动词的话），再看细节；正文为空或纯说明性 ⇒ 按「不产生检查要求」处理，各字段给空值。
② 出现「适用时 / 如有时 / 当…时」这类限定语 ⇒ 该条款是**条件适用**，必须把触发条件写出来，⛔ 不许笼统写「视情况而定」。
③ 判不了就留空串（或空数组），⛔ 不许编造、⛔ 不许用通用套话把字段填满。空值 = 待人工确认，是合法输出。
④ 所有文本用简体中文；字段名保持英文原样。

## 分类判据（只对属性清单里**存在**的字段适用）

- `Obligation`：按正文的**动词**判 —— 应/shall ⇒ `shall`；宜/should ⇒ `should`；可/may ⇒ `may`；
  **一个义务动词都没有 ⇒ `note`**。⛔ 不许按「标准惯例」默认填 `shall`。
- `Applicability`：没有「适用时/如有时/当…时」⇒ `general`；有 ⇒ `conditional`，并把触发条件原文要点写进
  `ApplicabilityCondition`（通用条款给空串）。
- `Process`：这条款落在哪些**过程域**（只能取清单允许的码），下游与企业部门取交集用。⛔ 不许把部门名当过程填。
- `Severity`：这条款一旦缺失或失效，对**产品安全与体系有效性**的影响程度（只能取清单允许的码）。
- `DocCheckMode`：这条款要不要靠企业资料来查（只能取清单允许的码）。拿不准给 `optional`，
  ⛔ 不要一律给 `required` —— 一律 required 会把「我们没查」当成「企业没有」。

## Artifacts 要求（企业侧 · 可公开给企业）

产出的是**提示词正文**（不是结论、不是清单结果）。将来这条提示词给模型用，让它从企业资料里找出「这条款要求企业提供什么」。

① **第一句必须是输出契约**，逐字照这个句式写：
   「只输出 JSON 数组，不要任何解释文字。数组每个元素包含：Kind（取字典 artifact_type 的码）、Name、Purpose（一句话说明能证明什么）。」
② 之后用 1~3 句写「去企业资料的哪类文件里找、认哪些关键字段」。
③ ⛔ 不许预先写结论，⛔ 不许写「符合/不符合」，⛔ 不许把审核员取证步骤写进来。

## EvidenceChain 要求（审核员侧 · 取证剧本，⛔ 不提前给企业）

产出的是**提示词正文**（不是结论）。将来给审核员用，让模型生成「到这家企业该怎么取证」的步骤。

① **第一句必须是输出契约**，逐字照这个句式写：
   「只输出 JSON 数组，不要任何解释文字。数组每个元素包含：Kind（site|record|interview|doc）、Action、Target、Sample（无则省略）、Finding（留空）。」
② 之后用 1~3 句写「去哪里、看什么、抽多少、问谁」。
③ ⛔ 不许预先写结论；⛔ 找不到证据就留空，不许推测。
④ 至少覆盖 2 种 `Kind`（纯查文件不足以证明体系在运行）。

## 硬约束

- ⛔ 「企业该提供什么」与「审核员怎么取证」必须分开产出（各归各的属性），合并会导致企业照着取证剧本准备、审核失效。
- ⛔ 不许把条款原文抄一遍充当答案。
- 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。

{{attr_pool}}',
       `PromptName` = '条款属性预生成提示词',
       `Remark`     = 'V3：属性清单走 {{attr_pool}} 实时渲染（⛔ 正文不写字段清单）；Artifacts/EvidenceChain 第一句必须是输出契约',
       `UpdateBy`   = 'clause-attr-20261029',
       `UpdateTime` = NOW()
 WHERE `PromptCode` = 'clause_attr_gen'
   AND `OrgCode` IS NULL
   AND `ScopeKey` IS NULL
   AND `IsDeleted` = 0;

INSERT INTO `cert_prompt_template`
  (`Code`, `PromptCode`, `PromptName`, `OrgCode`, `ScopeKey`, `Template`, `Remark`,
   `Sort`, `IsValid`, `Status`, `IsDeleted`, `CreateBy`, `CreateTime`)
SELECT REPLACE(UUID(), '-', ''), 'clause_attr_gen', '条款属性预生成提示词', NULL, NULL,
       (SELECT `Template` FROM (SELECT `Template` FROM `cert_prompt_template`
                                 WHERE `PromptCode` = 'clause_attr_gen' AND `IsDeleted` = 0
                                 ORDER BY `Id` LIMIT 1) t),
       'V3：属性清单走 {{attr_pool}} 实时渲染',
       0, 1, 'active', 0, 'clause-attr-20261029', NOW()
 WHERE NOT EXISTS (SELECT 1 FROM (SELECT `Id` FROM `cert_prompt_template`
                                   WHERE `PromptCode` = 'clause_attr_gen' AND `IsDeleted` = 0) x);

-- ── 2. 弃用 wf_prompt_template 的两条 clause_attr（读它 = 属性清单被写死）──────
UPDATE `wf_prompt_template`
   SET `IsActive` = 0,
       `Remark`   = CONCAT(COALESCE(`Remark`, ''), ' 【已弃用 2026-10-29：条款属性预生成改读 cert_prompt_template.clause_attr_gen，本行正文把 8 条属性写死在 JSON 骨架里，新增属性不会产出】'),
       `UpdateTime` = NOW()
 WHERE `PromptType` = 'clause_attr'
   AND `Remark` NOT LIKE '%已弃用 2026-10-29%';

-- ── 3. 清掉属性池里的临时测试行（TempAttr）──────────────────────────────────
-- ⚠️ 只软删这一条按 Code 点名的行；⛔ 不做范围删除
UPDATE `cert_clause_attr_def`
   SET `IsDeleted` = 1, `IsValid` = 0, `UpdateTime` = NOW()
 WHERE `Code` = 'TempAttr'
   AND `IsDeleted` = 0;

-- ── 4. 验证 ─────────────────────────────────────────────────────────────────
-- 4.1 提示词：必须恰好 1 行，且正文含 {{attr_pool}}
SELECT '提示词' AS Kind, `PromptCode` AS Code,
       CHAR_LENGTH(`Template`) AS Len,
       (`Template` LIKE '%{{attr_pool}}%') AS HasPool,
       (`Template` LIKE '%ClauseIntent%')  AS HasHardcodedField
  FROM `cert_prompt_template`
 WHERE `PromptCode` = 'clause_attr_gen' AND `IsDeleted` = 0;

-- 4.2 属性池：启用条数应为 9，且不再有 TempAttr
SELECT '属性池' AS Kind, COUNT(*) AS EnabledCount
  FROM `cert_clause_attr_def` WHERE `IsDeleted` = 0 AND `IsValid` = 1;

-- 4.3 wf_prompt_template：clause_attr 应全部 IsActive = 0
SELECT '旧提示词' AS Kind, `PromptCode`, `Version`, `IsActive`
  FROM `wf_prompt_template` WHERE `PromptType` = 'clause_attr' ORDER BY `Version`;
