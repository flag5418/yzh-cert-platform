-- ============================================================================
-- 「按标准提取条款」提示词（P2，2026-10-29）
--
-- 目标：把 doc_clause_extract 的正文落到 cert_prompt_template，让运营可以改提示词而不用发版。
--   ⚠️ 正文与 C# 兜底常量 DocExtractPromptService.FallbackTemplate **逐字一致**
--      （库里有行 ⇒ 以库为准；库里没有 ⇒ 用代码兜底，两者必须同源，否则"改了没生效"查不出来）。
--   ⚠️ 幂等：按 PromptCode + 平台级(OrgCode/ScopeKey 均为空) 判重，重复执行不产生第二行。
--   ⚠️ Status = 'active'：非 active 的行 DocExtractPromptService 不认（会落到兜底）。
-- ============================================================================

INSERT INTO cert_prompt_template
  (Code, PromptCode, PromptName, OrgCode, ScopeKey, Template, Placeholders, Remark, Sort, IsValid, Status, IsDeleted, CreateBy, CreateTime)
SELECT
  REPLACE(UUID(), '-', ''),
  'doc_clause_extract',
  '按标准提取条款提示词',
  NULL,
  NULL,
  '你是 ISO 管理体系认证的资深审核技术专家。你的任务：读一份企业提供的文档，判断这份文档里**实际写到了**下列哪些条款，并逐条摘录原文。

## 输入

- 标准：{{standard_name}}（{{standard_code}}）
- 文档：{{document_name}}
- 可命中的条款清单（编号 + 标题，共若干条）：
{{clause_list}}

- 文档全文（Markdown）：
<<<DOC
{{document_content}}
DOC

## 输出

只输出一个 JSON 对象，不要 markdown 代码块，不要任何解释文字：

{
  "clauses": [
    {
      "ClauseNumber": "4.2.4",
      "Title": "医疗器械文件控制",
      "Anchor": "原文中能定位这段的连续片段，10~40 字，逐字摘录",
      "Excerpts": ["原文摘录第一段", "原文摘录第二段"],
      "Confidence": 0.9
    }
  ]
}

## 硬规则（违反任何一条，本次输出作废）

① **只输出有实质内容的条款**。文档里没写、只是目录里列了个标题、或只有一句「见某某程序」而没有实际内容 ⇒ **不要出现在结果里**。⛔ 不许为了凑数编造条款。

② **每一条都必须带 `Anchor`**。`Anchor` 必须是文档原文里**逐字存在**的连续片段（10~40 字），用于把结果定位回原文。⛔ 不许改写、不许概括、不许自己编一句话当锚点。找不到这样的原文片段 ⇒ 这条不要输出。

③ **⛔ 绝对不许判断符合性**。不写「符合」「不符合」「充分」「缺失」「满足要求」「需要改进」。你只负责回答「这份文档里**读到了什么**」。判断是审核员的事，你写了就是污染结论。

④ **`ClauseNumber` 必须逐字取自上面的条款清单**。⛔ 不许自造编号、⛔ 不许改写格式（不要写「第4.2.4条」、不要写「4.2.4.1」除非清单里就有它）。

## 其它要求

- `Excerpts` 每条 20~200 字，按文档中出现的顺序；同一段不要重复摘录。
- `Confidence` 取 0~1：原文明确对应条款要求 ⇒ 0.8 以上；只是相关但不完整 ⇒ 0.4~0.7；勉强沾边 ⇒ 0.4 以下。
- 文档是别的体系（如财务制度、人事花名册）与条款清单无关 ⇒ 返回 `{ "clauses": [] }`，这是合法输出。
- 所有文本用简体中文（原文是英文就照抄英文，不要翻译）。
- 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。',
  '[{"token":"{{standard_name}}","desc":"标准名称（含短码）"},{"token":"{{standard_code}}","desc":"标准短码"},{"token":"{{clause_list}}","desc":"可命中的条款清单（DocCheckMode≠none 的叶子条款）"},{"token":"{{document_name}}","desc":"文档名"},{"token":"{{document_content}}","desc":"文档 Markdown 全文（长文档分段后逐段送）"}]',
  '企业文档按标准提取条款的通用提示词。四条硬规则：只输出有实质内容的条款 / 必带 Anchor / 不判符合性 / 编号逐字取自清单。',
  10, 1, 'active', 0, 'system', NOW()
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM cert_prompt_template
  WHERE PromptCode = 'doc_clause_extract' AND OrgCode IS NULL AND ScopeKey IS NULL
);

-- 验证
SELECT PromptCode, PromptName, ScopeKey, Status, LENGTH(Template) AS TemplateChars
FROM cert_prompt_template WHERE PromptCode = 'doc_clause_extract';
