-- ============================================================================
-- 20261110_doc_clause_extract_v3_V1.sql
-- ① 把 V3 单条文档提示词写到企业文档上传链路真正在用的 PromptCode
--    （doc_clause_extract —— ClauseExtractExecutor / DocExtractPromptService 消费）
-- ② 给 /business/doc-extract-rule 页面补 4 个标准节点（左树数据源）
-- ③ V3 试验期的 doc_analyze 置为停用（保留正文作参考，⛔ 不删）
--
-- 契约：保持 clauses[{ClauseNumber,Title,Anchor,Excerpts,Confidence}] 不变
--       （ExtractedDoc 反序列化忽略未知字段 ⇒ profile/doubts 现在不会破坏链路）
-- 幂等：doc_clause_extract 用 WHERE NOT EXISTS；规则行按 StandardCode 判存在
-- ============================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ── ① 平台级提示词（ScopeKey='' ⇒ 四个标准共用一条）──────────────��────────
INSERT INTO cert_prompt_template
    (PromptCode, PromptName, OrgCode, ScopeKey, Template, Placeholders, Status, Code, IsValid, IsDeleted, CreateTime, Remark)
SELECT
 'doc_clause_extract',
 '文档分析提示词（通用属性 + 按标准提取条款）',
 '',
 '',
'你是 ISO 管理体系认证的资深审核技术专家。任务：读一份企业文档，在指定标准下完成两件事 —— ① 判这份文档能不能用（通用属性）；② 把文档里**实际写到了**的条款逐条摘出来。

## 输入

- 标准：{{standard_name}}（{{standard_code}}）
- 文档：{{document_name}}
- 可命中的条款清单（编号 + 标题，只能从这里选）：
{{clause_list}}

- 文档全文（Markdown）：
<<<DOC
{{document_content}}
DOC

## 执行顺序（先判闸，闸不过就不提取）

① 判 IsSystemDoc（是不是本企业管理体系文件）
② 判 Extractable（正文有没有可用内容：只有封面/目录/修订记录/发放签收页/空白模板 ⇒ false）
③ 定 DocType（10 类）
④ 只有 ①② 都通过才填 clauses；否则 clauses 必须为 []

## 输出（只输出一个 JSON 对象；第一个字符 {，最后一个字符 }；不要代码块、不要解释）

{
  "profile": {
    "IsSystemDoc": true,
    "Extractable": true,
    "DocType": "procedure",
    "DocTitle": "采购控制程序",
    "DocCode": "SN/QES-CX-08-2026",
    "DocVersion": "A/0",
    "OwnerDept": "采购部",
    "ProcessDomains": ["purchase"],
    "Period": "",
    "Confidence": 0.88
  },
  "clauses": [
    {
      "ClauseNumber": "8.4",
      "Title": "采购",
      "Anchor": "原文里逐字存在的连续片段，10~40 字",
      "Excerpts": ["原文摘录第一段", "原文摘录第二段"],
      "Confidence": 0.9
    }
  ],
  "doubts": [
    { "Type": "empty_field", "Loc": "P21 表2 第5行", "Message": "结论列空白" }
  ]
}

## ① ② 两闸判据

- IsSystemDoc：属于本企业管理体系（手册/程序/指导书/记录/台账/证书/合同/法规/组织架构/其他体系文件）⇒ true；与体系无关（简历、宣传册、购物小票、空白草稿、明显不属于本企业）⇒ false。⛔ 不要求它只服务本标准：QES 同一套文件在三个标准下都可以 true。
- Extractable：能读到对本标准有用的规定或事实 ⇒ true；只有封面/目录/修订记录页/发放签收页/空白表格 ⇒ false。IsSystemDoc=false ⇒ Extractable 必须 false、clauses 必须 []。

## ③ DocType（10 类，按内容判，不看扩展名）

manual 手册（含目录 + 条款号）｜procedure 程序文件（含“程序/控制程序”，有目的-范围-职责段）｜work_instruction 作业指导书/操作规程/工艺卡｜record 记录表单（表单编号 + 表头字段 + 行数据 + 签批栏）｜ledger 台账（多行登记：设备、培训、合同）｜certificate 证书（编号 + 有效期 + 发证机构）｜contract 合同（甲乙方 + 标的 + 期限 + 签章）｜law 法规（法规名 + 条款号 + 发布机构）｜org_chart 组织架构（部门框 + 汇报线）｜other 是体系文件但归不进上面 9 类

## ④ 提取条款的四条硬规则（违反任何一条，本次输出作废）

① **只输出有实质内容的条款**。文档里没写、只在目录里列了标题、或只写“见某某程序”而没有实际内容 ⇒ 不出现。⛔ 不许为凑数编造条款。

② **每条必须带 Anchor**：必须是文档原文里**逐字存在**的连续片段（10~40 字），用于定位回原文。⛔ 不许改写、不许概括、不许自编一句当锚点；找不到 ⇒ 这条不要输出。

③ **⛔ 绝对不判符合性**：不写“符合/不符合/充分/缺失/满足要求/需要改进”。只回答“文档里读到了什么”，判断是审核员的事。

④ **ClauseNumber 必须逐字取自上面的条款清单**：⛔ 不许自造编号、⛔ 不许改写格式（不写“第8.4条”；清单里没有 8.4.1 就不写 8.4.1）。

## 提取粒度

- 手册/程序/指导书：按条款摘“规定了什么”，Excerpts 每条 20~200 字，按原文顺序，同段不重复。
- 记录/台账（关键）：按**行**摘，一行一条 Excerpt，保留日期、记录编号、对象、结论词、签批；⛔ 不许把整张表拼成一句话，⛔ 不许抽掉日期/编号。
- 同一条款最多 6 段摘录；超过则只留最能说明问题的 6 段。
- Confidence 0~1：原文明确对应条款要求 ⇒ 0.8 以上；相关但不完整 ⇒ 0.4~0.7；勉强沾边 ⇒ 0.4 以下。⛔ 不许一律高分。
- 另外过程域：文档整体归属（production/sales/design/purchase/inspection/metrology/warehouse/after_sales/hr/management 里选，可多选）写在 profile.ProcessDomains；判不出给 []。

## doubts（只找这份文档自身的缺陷，⛔ 不推论企业缺什么）

- empty_field 表头有字段、行内空白（如检验记录“结论”列空）
- no_sign 有签批栏但无签名或无日期
- expired 有日期且已过合理窗口（记录超 12 个月、证书已过期、培训早于上岗）
- missing_seq 编号跳号（QC-001、002、004）或有起无终
- insufficient_count 台账条数明显不足（月度记录一年只有 2 条）
- other 其他可指认缺陷

每条必须带 Type / Loc / Message，Loc 精确到页 + 表名 + 行号；定位不了的 ⛔ 不许输出。

## 其它

- 文档与条款清单无关 ⇒ 返回 { "profile": {...}, "clauses": [], "doubts": [] }，这是合法输出。
- 缺信息给 "" 或 []；⛔ 不许用套话填满字段。
- 文件名只是线索，正文为准；两者冲突以正文为准，并在 doubts 里说明。
- 全中文（原文英文术语照抄），字段名保持英文。',
 '{{standard_name}},{{standard_code}},{{document_name}},{{clause_list}},{{document_content}}',
 'active',
 CONCAT('PT_', REPLACE(UUID(),'-','')),
 1, 0, NOW(),
 'V3 文档层··企业文档上传链路实际消费的提示词（平台一套，四标准共用）：两闸 + 10 类 + 条款摘录（Anchor 必填）+ 文档级疑点'
WHERE NOT EXISTS (
    SELECT 1 FROM cert_prompt_template
    WHERE PromptCode = 'doc_clause_extract' AND IsDeleted = 0 AND IsValid = 1
      AND (OrgCode = '' OR OrgCode IS NULL) AND (ScopeKey = '' OR ScopeKey IS NULL)
);

-- ── ② 页面左树：每标准一行（现有页面按 cert_doc_extract_rules 出树）──────────
INSERT INTO cert_doc_extract_rules
    (StandardCode, StandardName, MarkdownContent, PromptContent, PromptVariables, Version, IsPublished, Sort, Status, Code, IsValid, IsDeleted, CreateTime, Remark)
SELECT 'iso9001', 'GB/T 19001-2016 质量管理体系', '（标准原文见 cert_iso_clause，本页仅作提示词维护入口）', '', '', 1, 1, 10, 'active', CONCAT('DR_', REPLACE(UUID(),'-','')), 1, 0, NOW(), 'V3 文档提示词维护节点'
WHERE NOT EXISTS (SELECT 1 FROM cert_doc_extract_rules WHERE StandardCode='iso9001' AND IsDeleted=0);

INSERT INTO cert_doc_extract_rules
    (StandardCode, StandardName, MarkdownContent, PromptContent, PromptVariables, Version, IsPublished, Sort, Status, Code, IsValid, IsDeleted, CreateTime, Remark)
SELECT 'iso14001', 'GB/T 24001-2016 环境管理体系', '（标准原文见 cert_iso_clause，本页仅作提示词维护入口）', '', '', 1, 1, 20, 'active', CONCAT('DR_', REPLACE(UUID(),'-','')), 1, 0, NOW(), 'V3 文档提示词维护节点'
WHERE NOT EXISTS (SELECT 1 FROM cert_doc_extract_rules WHERE StandardCode='iso14001' AND IsDeleted=0);

INSERT INTO cert_doc_extract_rules
    (StandardCode, StandardName, MarkdownContent, PromptContent, PromptVariables, Version, IsPublished, Sort, Status, Code, IsValid, IsDeleted, CreateTime, Remark)
SELECT 'iso45001', 'GB/T 45001-2020 职业健康安全管理体系', '（标准原文见 cert_iso_clause，本页仅作提示词维护入口）', '', '', 1, 1, 30, 'active', CONCAT('DR_', REPLACE(UUID(),'-','')), 1, 0, NOW(), 'V3 文档提示词维护节点'
WHERE NOT EXISTS (SELECT 1 FROM cert_doc_extract_rules WHERE StandardCode='iso45001' AND IsDeleted=0);

INSERT INTO cert_doc_extract_rules
    (StandardCode, StandardName, MarkdownContent, PromptContent, PromptVariables, Version, IsPublished, Sort, Status, Code, IsValid, IsDeleted, CreateTime, Remark)
SELECT 'iso13485', 'GB/T 42061-2022 医疗器械质量管理体系', '（标准原文见 cert_iso_clause，本页仅作提示词维护入口）', '', '', 1, 1, 40, 'active', CONCAT('DR_', REPLACE(UUID(),'-','')), 1, 0, NOW(), 'V3 文档提示词维护节点'
WHERE NOT EXISTS (SELECT 1 FROM cert_doc_extract_rules WHERE StandardCode='iso13485' AND IsDeleted=0);

-- ── ③ doc_analyze 停用（正文保留，便于对照）────────────────────────────────
UPDATE cert_prompt_template
SET IsValid = 0,
    Remark = CONCAT(IFNULL(Remark, ''), ' ｜ 2026-11-10 合并进 doc_clause_extract（企业文档链路实际消费），保留参考'),
    UpdateTime = NOW()
WHERE PromptCode = 'doc_analyze' AND IsDeleted = 0 AND IsValid = 1;

-- ── 验证 ──────────────────────────────────────────────────────────────────
SELECT PromptCode, CHAR_LENGTH(Template) AS chars, IsValid, Placeholders
FROM cert_prompt_template
WHERE IsDeleted = 0 AND PromptCode IN ('doc_clause_extract','doc_analyze')
ORDER BY IsValid DESC, PromptCode;

SELECT StandardCode, StandardName, Version, IsPublished, Code
FROM cert_doc_extract_rules WHERE IsDeleted = 0 ORDER BY Sort;
