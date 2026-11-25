SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 2026-11-25 doc_clause_extract 精简（兜底链对齐）
-- 背景：2026-11-11 起文档提取已改为「doc_profile 总闸 → 9 类按类型提取提示词」两层分流，
--       doc_clause_extract 退化为**仅 other / 画像退化时**的兜底链。
-- 问题：其正文仍带「① 判 IsSystemDoc ② 判 Extractable ③ 定 DocType（旧 10 类）」画像判闸
--       与 doubts 段，而 ExtractedDoc 只消费 clauses[]（无 Profile / Doubts 属性）
--       ⇒ 这两段每次调用白耗 LLM 输出，且旧 DocType 枚举（ledger/certificate/contract/law/org_chart）
--          与 doc_profile 的 10 类枚举（manual/policy/procedure/...）冲突，误导维护者。
-- 改动：删画像判闸段 + doubts 段，只保留「条款逐条提取」四硬规则 + 粒度 + 输出契约 clauses[]。
-- 幂等：UPDATE 命中平台级行；Sort 归一为 90（与 DocExtractionPromptCatalog 兜底组一致）。

UPDATE cert_prompt_template
SET PromptName = '条款逐条提取（兜底 · 仅 other）',
    Template = '你是 ISO 管理体系认证的资深审核技术专家。任务：在指定标准下，把这份企业文档里**实际写到了**的条款逐条摘出来。

> 本提示词仅作**兜底**：文档画像（doc_profile）判为 other / 无法归类时才走本链；手册、程序、记录、清单、制度、作业指导书等已有各自的「按类型提取」提示词，不走这里。

## 输入

- 标准：{{standard_name}}（{{standard_code}}）
- 文档：{{document_name}}
- 可命中的条款清单（编号 + 标题，只能从这里选）：
{{clause_list}}

- 文档全文（Markdown）：
<<<DOC
{{document_content}}
DOC

## 输出（只输出一个 JSON 对象；第一个字符 {，最后一个字符 }；不要代码块、不要解释）

{
  "clauses": [
    {
      "ClauseNumber": "8.4",
      "Title": "采购",
      "Anchor": "原文里逐字存在的连续片段，10~40 字",
      "Excerpts": ["原文摘录第一段", "原文摘录第二段"],
      "Confidence": 0.9
    }
  ]
}

## 提取条款的四条硬规则（违反任何一条，本次输出作废）

① **只输出有实质内容的条款**。文档里没写、只在目录里列了标题、或只写"见某某程序"而没有实际内容 ⇒ 不出现。⛔ 不许为凑数编造条款。

② **每条必须带 Anchor**：必须是文档原文里**逐字存在**的连续片段（10~40 字），用于定位回原文。⛔ 不许改写、不许概括、不许自编一句当锚点；找不到 ⇒ 这条不要输出。

③ **⛔ 绝对不判符合性**：不写"符合/不符合/充分/缺失/满足要求/需要改进"。只回答"文档里读到了什么"，判断是审核员的事。

④ **ClauseNumber 必须逐字取自上面的条款清单**：⛔ 不许自造编号、⛔ 不许改写格式（不写"第8.4条"；清单里没有 8.4.1 就不写 8.4.1）。

## 提取粒度

- 按条款摘"规定了什么"，Excerpts 每条 20~200 字，按原文顺序，同段不重复。
- 记录/台账类：按**行**摘，一行一条 Excerpt，保留日期、记录编号、对象、结论词、签批；⛔ 不许把整张表拼成一句话，⛔ 不许抽掉日期/编号。
- 同一条款最多 6 段摘录；超过则只留最能说明问题的 6 段。
- Confidence 0~1：原文明确对应条款要求 ⇒ 0.8 以上；相关但不完整 ⇒ 0.4~0.7；勉强沾边 ⇒ 0.4 以下。⛔ 不许一律高分。

## 其它

- 文档与条款清单无关 ⇒ 返回 { "clauses": [] }，这是合法输出。
- 全中文（原文英文术语照抄），字段名保持英文。',
    Placeholders = '{{standard_name}},{{standard_code}},{{document_name}},{{clause_list}},{{document_content}}',
    Remark = '2026-11-25 精简为纯条款提取（去画像判闸与 doubts，二者本就不被 ExtractedDoc 消费）；兜底链，仅 other/画像退化时触发',
    Sort = 90, IsValid = 1, Status = 'active', UpdateTime = NOW(), UpdateBy = 'migration'
WHERE PromptCode = 'doc_clause_extract' AND OrgCode = '' AND ScopeKey = '' AND IsDeleted = 0;

-- ── 校验 ──
SELECT PromptCode, PromptName, CHAR_LENGTH(Template) AS chars, Sort, IsValid, Status
FROM cert_prompt_template
WHERE PromptCode = 'doc_clause_extract' AND OrgCode = '' AND ScopeKey = '' AND IsDeleted = 0;
