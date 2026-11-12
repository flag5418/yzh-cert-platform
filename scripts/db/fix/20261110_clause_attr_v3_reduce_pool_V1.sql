-- ============================================================================
-- 20261110_clause_attr_v3_reduce_pool_V1.sql
-- 属性池收敛（用户 2026-11-10 裁决）：Artifacts / EvidenceChain 不属于属性池
--
-- 理由：属性池只放「可量化、可参数化、为提示词服务」的信息；
--   「企业该提供什么资料」「审核员怎么取证」属于第三类提示词（审核发现/疑点），
--   在生成时结合条款属性 + 企业事实产出，⛔ 不在 L1 属性里预先定义。
--
-- 变更：
--   ① cert_clause_attr_def：Artifacts / EvidenceChain 置 IsValid=0（保留历史，不物理删）
--   ② cert_clause_attr：这两条属性的已有行软删除（示例条款 8.4.1 / 6.1 / 9.2）
--   ③ cert_prompt_template：正文改为 9 属性版本（删两段产出契约，加边界说明）
--
-- 幂等：可重复执行
-- ============================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ── ① 两条属性退出池子 ──────────────────────────────────────────────────────
UPDATE cert_clause_attr_def
SET IsValid = 0, UpdateTime = NOW(), Remark = CONCAT(IFNULL(Remark, ''), '（2026-11-10 移出属性池：属第三类提示词）')
WHERE Code IN ('Artifacts', 'EvidenceChain') AND IsValid = 1;

-- ── ② 已有配置行软删除 ──────────────────────────────────────────────────────
UPDATE cert_clause_attr
SET IsDeleted = 1, DeleteTime = NOW(), DeleteBy = 'SYSTEM'
WHERE AttrCode IN ('Artifacts', 'EvidenceChain') AND IsDeleted = 0;

-- ── ③ 提示词：9 属性版本 ────────────────────────────────────────────────────
UPDATE cert_prompt_template
SET Template = '你是 ISO 管理体系认证的资深审核技术专家。你的任务：读一条标准条款，为这条条款产出「属性配置」JSON。

## 输入

- 标准：{{standard_name}}
- 条款号：{{clause_number}}
- 条款标题：{{clause_title}}
- 条款正文：
{{clause_content}}

## 输出

只输出一个 JSON 对象，不要 markdown 代码块，不要任何解释文字。
字段名 = 文末「属性清单」里的字段名，逐个都要输出；不许增、删、改名，不许输出清单外的字段。
某属性对这条款确实不适用（挂不上）⇒ 给空串（长度为 0）或空数组 []，这是合法输出；⛔ 不许为了填满而硬编。

## 边界（本提示词只做一件事）

本提示词只产出「可量化、可参数化」的条款属性，供后续提示词消费。
⛔ 不产出「企业应提供哪些资料」、⛔ 不产出「审核员怎么取证」—— 那两件事属于审核发现/审核证据提示词，在生成时结合条款属性与企业事实现场产出。
⛔ 不产出任何结论（符合/不符合）、⛔ 不产出任何提取步骤。

## 通用判据

① 先读条款正文的主句（第一句带义务动词的话），再看细节。正文为空或纯标题性说明 ⇒ 全部字段给空值。
② 出现「适用时 / 如有时 / 当…时」这类限定语 ⇒ 该条款是条件适用，必须把触发条件写出来，⛔ 不许笼统写「视情况而定」。
③ 判不了就留空串或空数组，⛔ 不许编造、⛔ 不许用通用套话把字段填满。空值 = 待人工确认。
④ 所有文本用简体中文；字段名保持英文原样。
⑤ 不许把条款原文整段抄进任何字段（ClauseIntent 只允许一句话概括，不是复述）。

## 分类判据（只对属性清单里存在的字段适用）

- ClauseIntent：一句话说清「这条款在管什么」，主语动词取自原文主句；⛔ 不许出现「确保/保证/有效落实」这类不落地的空话。
- Obligation：按正文的动词判 —— 应/shall ⇒ shall；宜/should ⇒ should；可/may ⇒ may；一个义务动词都没有 ⇒ note。
  ⛔ 不许按「标准惯例」默认填 shall。
- Applicability：没有「适用时/如有时/当…时」⇒ general；有 ⇒ conditional，并把触发条件原文要点写进 ApplicabilityCondition。
  ⛔ 「适用」与「不适用」是审核时对企业事实的判定结果，不是条款本身的属性 —— 这里只写 general/conditional。
- ApplicabilityCondition：通用条款给空串；条件条款写清「什么条件下适用」，用原文的关键限定语，⛔ 不许自己造条件。
- Process：这条款落在哪些过程域（只能取清单允许的码，可多选数组）。下游与企业部门职责取交集用。
  ⛔ 不许把部门名（如「品质部」）当过程填；⛔ 不许因为「这章像管理」就全给 management。
- Severity：这条款一旦缺失或失效，对产品安全与体系有效性的影响程度（只能取清单允许的码）。按后果判，不按条款地位判。
- EvidenceForm：这条款的审核证据该以什么形态呈现（只能取清单允许的码）。
  判据：规定/建立/明确职责/策划 ⇒ cite；实施/执行/检验/处置/改进/评价 ⇒ sample；
  环境、设施、标识、现场实物状态 ⇒ site；方针承诺、职责履行、人员意识 ⇒ interview。
- EvidenceFamily：制度族 f1（企业规定了什么）/执行族 f2（实际做了什么）/混合 f1f2（建立并实施）。
  判据：只按条款原文的动作词判，⛔ 不按章号判（章号与功能错位，实测章号判法会把方向判反）。
  只有 f1 语义词（建立/规定/确保/明确/策划/形成文件）⇒ f1；只有 f2 语义词（实施/执行/检验/处置/改进/评价/保留记录）⇒ f2；两类都有 ⇒ f1f2。
- Role：条款在体系中的功能角色（只能取清单允许的码）。按功能判，不按章号判：
  组织环境/相关方/范围 ⇒ r1；方针/职责/承诺/组织架构 ⇒ r2；风险机遇/目标/变更策划 ⇒ r3；
  资源/人员/能力/知识/沟通/成文信息 ⇒ r4；设计/采购/生产/交付/放行/不合格输出控制 ⇒ r5；
  监视测量/内审/管理评审/合规性评价 ⇒ r6；不合格处置/纠正措施/持续改进 ⇒ r7。
  跨标准时同功能条款给同一角色（如「采购控制」在 9001 8.4 与 13485 7.4 都是 r5）。

## 三属性一致约束（必须同时成立）

- f1 ⇒ EvidenceForm 一般是 cite 或 interview；f2 ⇒ 一般是 sample 或 site；f1f2 ⇒ 可以是 cite+sample 的组合，但只填一个主形态（防一段里塞两种写法）。
- r1/r3 多为 f1（制度与策划）；r5 多为 f1f2 或 f2；r6/r7 多为 f2。若你的判断与这条不一致，必须在 ClauseIntent 里说清理由，⛔ 不许两头都填满凑一致。
- Role 与 Process 不是一回事：Role 说「这一段怎么写」，Process 说「这属于哪个过程」。

## 硬约束

- ⛔ 不许把条款原文抄一遍充当答案。
- ⛔ 不许输出清单外的字段（尤其不要再输出 Artifacts / EvidenceChain 这类已移出属性池的字段）。
- 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。

{{attr_pool}}',
    UpdateTime = NOW(), UpdateBy = 'SYSTEM',
    Remark = 'V3 属性池 9 条 · 平台一套（2026-11-10 移出 Artifacts/EvidenceChain）'
WHERE PromptCode = 'clause_attr_gen' AND IsDeleted = 0
  AND (OrgCode = '' OR OrgCode IS NULL) AND (ScopeKey = '' OR ScopeKey IS NULL);

-- ── 验证 ────────────────────────────────────────────────────────────────────
SELECT '启用属性数' AS 检查项, COUNT(*) AS 值 FROM cert_clause_attr_def WHERE IsValid = 1 AND IsDeleted = 0
UNION ALL SELECT '停用属性数', COUNT(*) FROM cert_clause_attr_def WHERE IsValid = 0 AND IsDeleted = 0
UNION ALL SELECT '有效配置行', COUNT(*) FROM cert_clause_attr WHERE IsDeleted = 0
UNION ALL SELECT '提示词字符数', CHAR_LENGTH(Template) FROM cert_prompt_template WHERE PromptCode = 'clause_attr_gen'
UNION ALL SELECT '提示词含Artifacts段', CASE WHEN Template LIKE '%Artifacts 要求%' THEN 1 ELSE 0 END FROM cert_prompt_template WHERE PromptCode = 'clause_attr_gen';
