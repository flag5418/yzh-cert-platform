SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 2026-11-24 手册章节模式提示词（manual_chapter）
-- 背景：手册「逐条款」链（clause_full）慢、漏条款、串条款；改为「整篇一次、按手册自有章节输出」，
--       手册章号 → ISO 条款号由代码（ManualIndexParser 目录表）确定性挂载，模型不报条款号。
-- 影响：① 新增提示词 doc_extract_manual_chapter；② 修订 doc_profile 硬路由 manual/policy → manual_chapter。
-- 幂等：UPDATE 命中已存在行；INSERT ... WHERE NOT EXISTS 兜底新增行。

-- ============ 1) doc_profile 硬路由修订：manual/policy → manual_chapter ============
UPDATE cert_prompt_template
SET Template = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(Template,
        'manual / policy → clause_full（逐条款）', 'manual / policy → manual_chapter（手册章节）'),
        '只有 manual / policy 可以用 clause_full。', '只有 manual / policy 可以用 manual_chapter。'),
        '"ExtractionMode": "clause_full"', '"ExtractionMode": "manual_chapter"'),
        '"ExtractionModeName": "逐条款"', '"ExtractionModeName": "手册章节"'),
        'ExtractionMode=clause_full', 'ExtractionMode=manual_chapter'),
        'clause_full（逐条款）', 'manual_chapter（手册章节）'),
    UpdateTime = NOW(), UpdateBy = 'migration'
WHERE PromptCode = 'doc_profile' AND OrgCode = '' AND ScopeKey = '' AND IsDeleted = 0;

-- ============ 2) 手册章节提取提示词（新）============
UPDATE cert_prompt_template
SET PromptName = '手册/方针章节提取提示词',
    Template = '你是企业质量管理体系《手册》/《方针目标》的章节事实提取器。手册是纲领性文件、覆盖全部条款；你的任务是**按手册自有章节逐章**提取"这一章实际规定/承诺了什么"，整篇一次输出，⛔ 不按 ISO 条款逐条推理。

## 输入
文档画像：
{{doc_profile}}

文档全文（Markdown）：
<<<DOC
{{document_text}}
DOC

## 核心规则
1. **按手册章节输出，一章一条**：章节号用手册自己的章号（如 0.4、4.1、4.4.2、5.2、7.5.1），⛔ 不要输出 ISO 条款号，也不要按条款重新切分内容。
2. **章节必须来自原文标题**：只输出文档里真实存在标题的章节；⛔ 不得杜撰章节号，也不得把没写的章节凑出来。
3. **一章一段摘要 + 若干要点**：摘要 1-3 句，说清这一章规定了什么；要点列可核对的承诺/职责/要求（每条一句，最多 5 条）。
4. **只转述，不判断**：不写"符合/不符合"，不写审核发现，不写整改建议。
5. 附录/流程图/表格：若属于某章节，并入该章要点；独立附录可单列一章（Section 用附录名）。
6. 封面元数据（文件编号/版本/批准人/生效日期）放 Metadata；看不到就留空，⛔ 不要编。

## 输出
只输出一个 JSON 对象，不要 markdown 代码块，不要解释：
{
  "Metadata": {"DocCode": "", "DocVersion": "", "ApprovedBy": "", "EffectiveDate": ""},
  "ReportSummary": "本手册按 XX 章规定了质量管理体系的要求，覆盖范围、领导作用、策划、支持、运行、绩效评价与改进",
  "OwnerDept": "管理层",
  "SupportDepts": [],
  "Chapters": [
    {"Section": "0.4", "Title": "质量方针和目标", "Summary": "规定了质量方针与年度质量目标及其分解。", "Points": ["质量方针为……", "质量目标包括……"]},
    {"Section": "4.1", "Title": "理解组织及其环境", "Summary": "规定了内外部因素的识别与监视要求。", "Points": ["识别……", "每年评审……"]}
  ]
}

## 硬规则
1. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。
2. Chapters 的 Section 必须是手册原文标题里的章号（纯数字与点，如 4.1、7.5.1）；⛔ 不写 ISO 条款号。
3. 每章 Summary 不超过 3 句；Points 每条不超过 1 句、最多 5 条。
4. 确实没有可提取内容时才返回空 Chapters 数组。
5. 所有文本用简体中文，英文枚举值保持英文。',
    Placeholders = '{{doc_profile}},{{document_text}}',
    Remark = '手册/方针章节提取提示词：整篇一次按章节组织，章号由手册目录确定性挂载',
    Sort = 70, IsValid = 1, Status = 'active', UpdateTime = NOW(), UpdateBy = 'migration'
WHERE PromptCode = 'doc_extract_manual_chapter' AND OrgCode = '' AND ScopeKey = '' AND IsDeleted = 0;

INSERT INTO cert_prompt_template (PromptCode, PromptName, OrgCode, ScopeKey, Template, Placeholders, Remark, Sort, IsValid, Status, IsDeleted, Code, CreateTime, CreateBy)
SELECT 'doc_extract_manual_chapter', '手册/方针章节提取提示词', '', '', '你是企业质量管理体系《手册》/《方针目标》的章节事实提取器。手册是纲领性文件、覆盖全部条款；你的任务是**按手册自有章节逐章**提取"这一章实际规定/承诺了什么"，整篇一次输出，⛔ 不按 ISO 条款逐条推理。

## 输入
文档画像：
{{doc_profile}}

文档全文（Markdown）：
<<<DOC
{{document_text}}
DOC

## 核心规则
1. **按手册章节输出，一章一条**：章节号用手册自己的章号（如 0.4、4.1、4.4.2、5.2、7.5.1），⛔ 不要输出 ISO 条款号，也不要按条款重新切分内容。
2. **章节必须来自原文标题**：只输出文档里真实存在标题的章节；⛔ 不得杜撰章节号，也不得把没写的章节凑出来。
3. **一章一段摘要 + 若干要点**：摘要 1-3 句，说清这一章规定了什么；要点列可核对的承诺/职责/要求（每条一句，最多 5 条）。
4. **只转述，不判断**：不写"符合/不符合"，不写审核发现，不写整改建议。
5. 附录/流程图/表格：若属于某章节，并入该章要点；独立附录可单列一章（Section 用附录名）。
6. 封面元数据（文件编号/版本/批准人/生效日期）放 Metadata；看不到就留空，⛔ 不要编。

## 输出
只输出一个 JSON 对象，不要 markdown 代码块，不要解释：
{
  "Metadata": {"DocCode": "", "DocVersion": "", "ApprovedBy": "", "EffectiveDate": ""},
  "ReportSummary": "本手册按 XX 章规定了质量管理体系的要求，覆盖范围、领导作用、策划、支持、运行、绩效评价与改进",
  "OwnerDept": "管理层",
  "SupportDepts": [],
  "Chapters": [
    {"Section": "0.4", "Title": "质量方针和目标", "Summary": "规定了质量方针与年度质量目标及其分解。", "Points": ["质量方针为……", "质量目标包括……"]},
    {"Section": "4.1", "Title": "理解组织及其环境", "Summary": "规定了内外部因素的识别与监视要求。", "Points": ["识别……", "每年评审……"]}
  ]
}

## 硬规则
1. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。
2. Chapters 的 Section 必须是手册原文标题里的章号（纯数字与点，如 4.1、7.5.1）；⛔ 不写 ISO 条款号。
3. 每章 Summary 不超过 3 句；Points 每条不超过 1 句、最多 5 条。
4. 确实没有可提取内容时才返回空 Chapters 数组。
5. 所有文本用简体中文，英文枚举值保持英文。',
    '{{doc_profile}},{{document_text}}',
    '手册/方针章节提取提示词：整篇一次按章节组织，章号由手册目录确定性挂载',
    70, 1, 'active', 0, 'PT_DOC_EXTRACT_MANUAL_CHAPTER_V1', NOW(), 'migration'
WHERE NOT EXISTS (SELECT 1 FROM cert_prompt_template WHERE PromptCode = 'doc_extract_manual_chapter' AND OrgCode = '' AND ScopeKey = '' AND IsDeleted = 0);

-- ============ 3) 校验 ============
SELECT PromptCode, PromptName, LENGTH(Template) AS TemplateLength, IsValid, Status
FROM cert_prompt_template
WHERE PromptCode IN ('doc_profile', 'doc_extract_manual_chapter')
  AND OrgCode = '' AND ScopeKey = '' AND IsDeleted = 0;
