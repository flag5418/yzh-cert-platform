-- ============================================================================
-- 20261110_doc_prompt_v3_V1.sql
-- V3 文档提示词三条（B 阶段第 1 步）：落库到 cert_prompt_template 平台默认行
--   PromptCode: doc_semantic / doc_extract_cite / doc_extract_detail
--   OrgCode = ''，ScopeKey = ''（平台一套，与机构无关）
--
-- 依据：docs/60-V3单机版/26-文档提示词三类改造-语义分析与两类提取-V1.md
-- 幂等：INSERT ... WHERE NOT EXISTS（已存在则不动；要改正文在页面/新的 V2 脚本里改）
-- ============================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────────────────────
-- ① doc_semantic：文档语义分析（两闸 + 类型 + 过程 + 条款提示 + 6 字段 + 文档级疑点）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO cert_prompt_template
    (PromptCode, PromptName, OrgCode, ScopeKey, Template, Placeholders, Status, Code, IsValid, IsDeleted, CreateTime, Remark)
SELECT
 'doc_semantic',
 '文档语义分析提示词',
 '',
 '',
'你是 ISO 管理体系认证的资深审核技术专家。任务：读一份企业文档，判断它在指定标准下的可用性，并抽出文档身份与文档自身缺陷。

## 输入

- 标准：{{standard_name}}
- 文件名：{{doc_name}}
- 允许的过程域码（只能从这里选）：{{process_list}}
- 该标准条款清单（条款号 标题，每行一条）：
{{clause_brief}}
- 文档内容（Markdown）：
{{doc_md}}

## 本提示词只做四件事

① 判定它是不是本企业管理体系文件（IsSystemDoc）
② 判定它能不能对本标准提出审核报告要用的内容（Extractable）
③ 判定文档类型、归口过程、可能覆盖的条款（DocType / ProcessDomains / ClauseHints）
④ 抽文档身份六字段 + 文档自身缺陷（Doubts）

⛔ 不抽具体事实行（那是提取提示词的事）；⛔ 不判符合/不符合；⛔ 不提审核结论、NC、整改建议。

## 输出

只输出一个 JSON 对象。第一个字符必须是 {，最后一个字符必须是 }。不要 markdown 代码块，不要解释文字。字段名照抄下面示例，不许增删改名。

{
  "IsSystemDoc": true,
  "DocType": "record",
  "ProcessDomains": ["inspection"],
  "ClauseHints": ["8.4.2", "8.6"],
  "Extractable": true,
  "ExcerptStyle": "detail",
  "DocTitle": "进货检验记录",
  "DocCode": "SN/QES-JL-08-2026",
  "DocVersion": "A/0",
  "OwnerDept": "品质部",
  "Period": "2025-01~2025-12",
  "Confidence": 0.86,
  "Doubts": [
    { "Type": "empty_field", "Loc": "P3 表1 第5行", "Message": "结论列空白" }
  ]
}

## 判据

- IsSystemDoc：文档属于该企业管理体系（手册/程序/指导书/记录/台账/证书/合同/法规/组织架构/其他体系文件）⇒ true；
  与体系无关（个人简历、产品宣传册、购物小票、空白草稿、明显不属于该企业的文件）⇒ false。
  ⛔ 不要求它只服务本标准：QES 同一套文件在三个标准下都可 true。
- Extractable：能读到对本标准有用的规定或事实 ⇒ true；
  只是封面/目录/修订记录页/发放签收页/空白模板 ⇒ false。
  IsSystemDoc=false 时 Extractable 必须为 false。
- DocType：manual 手册 / procedure 程序文件 / work_instruction 作业指导书 / record 记录 / ledger 台账 /
  certificate 资质证书 / contract 合同协议 / law 法律法规 / org_chart 组织架构 / other 其他体系文件。
- ExcerptStyle：manual、procedure、work_instruction、law、org_chart ⇒ cite；
  record、ledger、certificate、contract ⇒ detail；IsSystemDoc=false 或 Extractable=false ⇒ none。
- ProcessDomains：这份文档实际归属的过程域（可多选，只能用 {{process_list}} 里的码）。记录类必须判：一条记录属于采购还是检验，直接决定它能不能被对应条款调用。判不出⇒空数组。
- ClauseHints：文档名称/编号/正文明确指向的条款号，只能从上面的条款清单里取，可空。
  ⛔ 不许为了覆盖全而把整章条款都列上；⛔ 不许编清单外的条款号。
- DocTitle / DocCode / DocVersion / OwnerDept / Period：原文写法优先；原文没有⇒空串，⛔ 不许猜。
  Period 只对记录/台账有意义（如 2025-01~2025-12），其余给空串。
- Confidence：0–1。低于 0.6 ⇒ 该行会被人工复核，请如实给低分，⛔ 不许一律给高分。

## Doubts（只找这份文档自身的缺陷，⛔ 不推论企业缺什么）

- empty_field 表头有字段、行内空白（如检验记录“结论”列空）
- expired 有日期且已过合理窗口（记录超 12 个月、证书已过期、培训早于上岗）
- no_sign 有签批栏但无签名或无日期
- missing_seq 编号跳号（QC-001、002、004）或有起无终
- insufficient_count 台账条数明显不足（月度记录一年只有 2 条）
- empty_doc 只有封面/目录（此时 Extractable 必须为 false）
- other 其他可指认缺陷

每条必须带 Type / Loc / Message，Loc 精确到页 + 表名 + 行号；定位不了的疑点 ⛔ 不许输出。

## 硬约束

- 缺信息就给 "" 或 []，这是合法输出；⛔ 不许用套话填满字段。
- 文件名只是线索，正文为准；正文与文件名冲突时以正文为准并在 Message 里说明。
- 全中文；字段名保持英文；只输出 JSON。',
 '{{standard_name}},{{doc_name}},{{process_list}},{{clause_brief}},{{doc_md}}',
 'active',
 CONCAT('PT_', REPLACE(UUID(),'-','')),
 1, 0, NOW(),
 'V3 B 阶段·文档语义分析（平台一套）：两闸 + 10 类 + 过程域 + 条款提示 + 6 字段 + 文档级疑点'
WHERE NOT EXISTS (
    SELECT 1 FROM cert_prompt_template
    WHERE PromptCode = 'doc_semantic' AND IsDeleted = 0
      AND (OrgCode = '' OR OrgCode IS NULL) AND (ScopeKey = '' OR ScopeKey IS NULL)
);

-- ────────────────────────────────────────────────────────────────────────────
-- ② doc_extract_cite：引用式提取（手册 / 程序文件 / 指导书 / 法规 / 组织架构）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO cert_prompt_template
    (PromptCode, PromptName, OrgCode, ScopeKey, Template, Placeholders, Status, Code, IsValid, IsDeleted, CreateTime, Remark)
SELECT
 'doc_extract_cite',
 '文档引用式提取提示词',
 '',
 '',
'你是 ISO 管理体系认证的资深审核技术专家。任务：从一份体系文件里，按条款整理出「可被审核报告引用」的条目。

## 输入

- 标准：{{standard_name}}
- 文件身份：{{doc_profile}}
- 该标准条款清单（条款号 标题）：
{{clause_brief}}
- 文档内容（Markdown）：
{{doc_md}}

## 只做一件事

把这份文件里「规定了什么」按条款做成索引条目：条款号 + 位置 + 标题 + 不超过 60 字的要点 + 文件名引用。

⛔ 不抄正文段落（要点是概括，不是摘抄）；⛔ 不抽记录行；⛔ 不判符合不符合；⛔ 不写整改建议。

## 输出

只输出一个 JSON 对象，第一个字符 {，最后一个字符 }。不要代码块，不要解释。

{
  "Entries": [
    {
      "ClauseHint": "8.4",
      "Loc": "P12 §8.4",
      "Heading": "采购",
      "Point": "规定供方评价、选择、绩效监视与再评价的职责与准则",
      "FileRef": "SN/QES-CX-08-2026 采购控制程序"
    }
  ],
  "CoveredProcesses": ["purchase"],
  "Doubts": []
}

## 判据

- ClauseHint：这条内容对应的条款号，优先取正文自带编号；正文无编号时按内容判，只能从条款清单里取。
- Loc：页码 + 章节号/小节号，必须能在原件定位。
- Heading：原文小节标题（无标题时用条款标题）。
- Point：≤60 字，写清「这条款在这份文件里规定了什么」，⛔ 不许出现“确保/有效落实”这类空话。
- 一份文件产出 1–8 条即可，不追求覆盖全部条款；文件里确实没写的条款 ⛔ 不许硬造条目。
- FileRef：文件编号 + 文件名（身份里没有编号就用文件名）。
- CoveredProcesses：本文件整体覆盖的过程域码，可空。
- Doubts：只列这份文件自身的缺陷（缺编号、缺版本、缺签批、内容与标题不符），格式同 { "Type": "...", "Loc": "...", "Message": "..." }。

## 硬约束

- 全中文，字段名英文；只输出 JSON；缺信息给 "" 或 []。
- Entries 里不许出现审核结论、证据编号、NC 编号。',
 '{{standard_name}},{{doc_profile}},{{clause_brief}},{{doc_md}}',
 'active',
 CONCAT('PT_', REPLACE(UUID(),'-','')),
 1, 0, NOW(),
 'V3 B 阶段·引用式提取（平台一套）：手册/程序/指导书/法规 ⇒ 条款号 + ≤60 字要点 + 文件引用'
WHERE NOT EXISTS (
    SELECT 1 FROM cert_prompt_template
    WHERE PromptCode = 'doc_extract_cite' AND IsDeleted = 0
      AND (OrgCode = '' OR OrgCode IS NULL) AND (ScopeKey = '' OR ScopeKey IS NULL)
);

-- ────────────────────────────────────────────────────────────────────────────
-- ③ doc_extract_detail：明细式提取（记录 / 台账 / 证书 / 合同）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO cert_prompt_template
    (PromptCode, PromptName, OrgCode, ScopeKey, Template, Placeholders, Status, Code, IsValid, IsDeleted, CreateTime, Remark)
SELECT
 'doc_extract_detail',
 '文档明细式提取提示词',
 '',
 '',
'你是 ISO 管理体系认证的资深审核技术专家。任务：从一份记录/台账/证书/合同里，抽出可被条款调用的行级事实。

## 输入

- 标准：{{standard_name}}
- 文件身份：{{doc_profile}}
- 该标准条款清单（条款号 标题）：
{{clause_brief}}
- 文档内容（Markdown）：
{{doc_md}}

## 只做一件事

把文中的每一张表/每一条登记，抽成行级事实：日期、编号、对象、事项、结论、签批、所属过程、可能对应的条款、原文关键行。

⛔ 不写审核结论（Result 只抄原文结论词）；⛔ 不写段落（那是审核证据提示词的事）；⛔ 不改写原文事实。

## 输出

只输出一个 JSON 对象，第一个字符 {，最后一个字符 }。不要代码块，不要解释。

{
  "Sheets": [
    { "Name": "进货检验记录", "Loc": "P21 表2", "Columns": ["日期","物料名称","批号","检验项目","结论","检验员"], "RowsTotal": 12 }
  ],
  "Facts": [
    {
      "Loc": "P21 表2 第3行",
      "Date": "2025-03-14",
      "RecordNo": "IQC-2025-031",
      "Object": "6061 铝棒",
      "Item": "进货检验",
      "Result": "合格",
      "Signature": "李×",
      "ProcessDomain": "inspection",
      "ClauseHints": ["8.4.2", "8.6"],
      "Quote": "2025-03-14 6061 铝棒 批号 2503-11 外观/尺寸检验 合格 李×"
    }
  ],
  "Doubts": [],
  "Truncated": false
}

## 判据

- Loc：页码 + 表名 + 行号，必须能定位；定位不了的行 ⛔ 不许抽。
- Date：原文日期，规范成 YYYY-MM-DD；只有年月就写 YYYY-MM；无日期留空串。
- RecordNo / Object / Item / Result / Signature：照原文；没有就留空串，⛔ 不许猜、不许补。
- Result 只写原文结论词（合格/不合格/通过/让步接收/拒收）；原文没有结论列 ⇒ 空串。
- ProcessDomain：这一行事实真正归属的过程域（只能用 {{process_list}} 里的码）。同一本记录里不同行可以不同。
- ClauseHints：这一行能被哪些条款调用，只能从条款清单里取，可空。
- Quote：≤100 字原文关键行，必须与 Loc 对得上；⛔ 不许拼接多行、不许改数字。
- Dates/Facts 上限 200 行；超过 ⇒ Truncated=true，取前 200 行，⛔ 不许抽样丢行。
- 空行、合计行、页脚、装订线说明 ⛔ 不计入 Facts。
- Doubts：只列这份文件自身缺陷（字段空、日期过期、缺签批、编号跳号、条数不足），带 Type / Loc / Message。

## 硬约束

- 全中文（原文英文术语照原文），字段名英文；只输出 JSON；缺信息给 "" 或 []。
- Facts 里不许出现“符合”“不符合”“建议开 NC”这类判断词。',
 '{{standard_name}},{{doc_profile}},{{process_list}},{{clause_brief}},{{doc_md}}',
 'active',
 CONCAT('PT_', REPLACE(UUID(),'-','')),
 1, 0, NOW(),
 'V3 B 阶段·明细式提取（平台一套）：记录/台账/证书/合同 ⇒ 行级事实（时间/编号/对象/结论/过程/条款）'
WHERE NOT EXISTS (
    SELECT 1 FROM cert_prompt_template
    WHERE PromptCode = 'doc_extract_detail' AND IsDeleted = 0
      AND (OrgCode = '' OR OrgCode IS NULL) AND (ScopeKey = '' OR ScopeKey IS NULL)
);

-- ────────────────────────────────────────────────────────────────────────────
-- 验证
-- ────────────────────────────────────────────────────────────────────────────
SELECT PromptCode, PromptName, CHAR_LENGTH(Template) AS chars, Placeholders, IsValid
FROM cert_prompt_template
WHERE PromptCode IN ('clause_attr_gen', 'doc_semantic', 'doc_extract_cite', 'doc_extract_detail')
  AND IsDeleted = 0
ORDER BY PromptCode;
