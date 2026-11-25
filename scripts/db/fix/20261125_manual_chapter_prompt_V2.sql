SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 2026-11-25 手册章节模式提示词 V2（manual_chapter）
-- 背景（对照 docs/80-v4 05/16 号）：
--   ① 手册「不按条款提取」——已由 V1（20261124）实现：按手册自有章节输出，条款号由手册目录
--      （ManualIndexParser）确定性挂载，模型不自报条款号。
--   ② 本轮补「标准无关 + 可原样引用」：
--      - 新增 Topic（标准无关主题标签，供后续按主题/释义反查，不依赖条款号）；
--      - 新增 Verbatim（逐字原样段，供审核报告原样输出，全文硬上限 10 段防超输出额度）；
--      - 新增 DocFunction=basis（文档职能=规定类）；
--      - Metadata 补全（编制/审核/批准/发布/生效日期）。
--   ③ 输出额度：ManualChapter 走 8000 token（见 ClauseExtractExecutor），Verbatim 硬上限 10 段以保不截断。
-- 幂等：先 INSERT 兜底建行（缺行时），再 UPDATE 灌正文 —— 正文只写一份，避免占位串。

-- ============ 1) 兜底建行（仅当平台级行不存在时）============
INSERT INTO cert_prompt_template (PromptCode, PromptName, OrgCode, ScopeKey, Template, Placeholders, Remark, Sort, IsValid, Status, IsDeleted, Code, CreateTime, CreateBy)
SELECT 'doc_extract_manual_chapter', '手册/方针章节提取提示词', '', '', '', '{{doc_profile}},{{document_text}}',
    '手册/方针章节提取提示词 V2', 70, 1, 'active', 0, 'PT_DOC_EXTRACT_MANUAL_CHAPTER_V2', NOW(), 'migration'
WHERE NOT EXISTS (SELECT 1 FROM cert_prompt_template WHERE PromptCode = 'doc_extract_manual_chapter' AND OrgCode = '' AND ScopeKey = '' AND IsDeleted = 0);

-- ============ 2) 灌正文（存在即更新；刚兜底建的行也会被本次更新覆盖）============
UPDATE cert_prompt_template
SET PromptName = '手册/方针章节提取提示词',
    Template = '你是企业《管理手册》/《方针目标》的章节事实提取器。手册是纲领性文件、覆盖全部条款；你的任务是按手册**自有章节**逐章提取"这一章实际规定/承诺了什么 + 可直接引用的原文段落"，整篇一次输出。
⛔ 不按 ISO 条款逐条推理；⛔ 不输出任何 ISO 条款号。

## 输入
文档画像：
{{doc_profile}}

文档全文（Markdown）：
<<<DOC
{{document_text}}
DOC

## 核心规则
1. **按手册章节输出，一章一条**：章节号用手册自己的章号（如 0.4、4.1、4.4、5.2、7.5.1）。⛔ 不要输出 ISO 条款号，也不要按条款重新切分内容。
2. **章节必须来自原文标题**：只输出文档里真实存在标题的章节；⛔ 不得杜撰章节号，也不得把没写的章节凑出来。
3. **一章四件事**：
   - Topic：主题标签，只能从下列固定集合里选一个 —— 组织环境 / 相关方 / 范围 / 方针目标 / 职责权限 / 风险机遇 / 资源 / 文件化信息 / 运行策划 / 绩效评价 / 改进 / 其他；
   - Summary：1–3 句，说清这一章规定了什么；
   - Points：可核对的承诺 / 职责 / 要求，最多 5 条，每条一句；
   - Verbatim：**逐字**取自原文、可直接在审核报告里原样引用的关键段落，每段 20–150 字。⛔ 不许改写、不许概括、不许拼接；**全文最多 10 段，只给最关键的章节（方针 / 目标 / 范围 / 职责 / 关键承诺），其余章节留空数组**。
4. **只转述，不判断**：不写"符合 / 不符合"，不写审核发现，不写整改建议。
5. 附录 / 流程图 / 表格：若属于某章节，并入该章；独立附录可单列一章（Section 用附录名）。
6. 封面元数据（文件编号 / 版本 / 编制 / 审核 / 批准 / 发布日期 / 生效日期）放 Metadata；看不到就留空，⛔ 不要编。

## 输出
只输出一个 JSON 对象，不要 markdown 代码块，不要解释：
{
  "DocFunction": "basis",
  "Metadata": {"DocCode": "", "DocVersion": "", "PreparedBy": "", "ReviewedBy": "", "ApprovedBy": "", "IssuedDate": "", "EffectiveDate": ""},
  "ReportSummary": "本手册按 XX 章规定了管理体系的要求，覆盖范围、领导作用、策划、支持、运行、绩效评价与改进",
  "OwnerDept": "管理层",
  "SupportDepts": [],
  "Chapters": [
    {"Section": "0.4", "Title": "方针和目标", "Topic": "方针目标", "Summary": "规定了方针与年度目标及其分解。", "Points": ["方针为……"], "Verbatim": ["本公司质量方针是……"]}
  ]
}

## 硬规则
1. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。
2. Chapters 的 Section 必须是手册原文标题里的章号（纯数字与点，如 4.1、7.5.1）；⛔ 不写 ISO 条款号。
3. Verbatim 必须逐字取自原文；⛔ 不许改写 / 概括 / 拼接；全文最多 10 段；找不到就 []。
4. Topic 只能取集合内的枚举值。
5. 每章 Summary ≤ 3 句；Points ≤ 5 条；Verbatim 每段 20–150 字。
6. 确实没有可提取内容时才返回空 Chapters 数组。
7. 所有文本用简体中文，英文枚举值保持英文。',
    Placeholders = '{{doc_profile}},{{document_text}}',
    Remark = '手册/方针章节提取提示词 V2：按章节 + Topic 主题标签 + Verbatim 原样段（报告原样引用）；条款号由代码确定性挂载',
    Sort = 70, IsValid = 1, Status = 'active', UpdateTime = NOW(), UpdateBy = 'migration'
WHERE PromptCode = 'doc_extract_manual_chapter' AND OrgCode = '' AND ScopeKey = '' AND IsDeleted = 0;

-- 校验：正文长度与关键字段是否就位
SELECT PromptCode, PromptName, LENGTH(Template) AS TemplateLength,
       (Template LIKE '%"Topic"%') AS HasTopic,
       (Template LIKE '%"Verbatim"%') AS HasVerbatim,
       (Template LIKE '%DocFunction%') AS HasDocFunction,
       IsValid, Status
FROM cert_prompt_template
WHERE PromptCode = 'doc_extract_manual_chapter' AND OrgCode = '' AND ScopeKey = '' AND IsDeleted = 0;
