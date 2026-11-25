-- =============================================================================
-- V4 · 文档分类「结构指纹 + 角色定位」提示词改造（幂等，一次性）
-- 依据：docs/80-v4/17-文档分类：结构指纹与角色定位-V1.md §六（改造项 2 / 5）
-- 目标表：cert_prompt_template（平台级行 ScopeKey 为空）
--
--   改造项 2：doc_profile 提示词输入改为「指纹结果 + Toc」，判定改为「先指纹、后角色」，
--             分类结论必须回引命中的指纹 Key；判不出 ⇒ unclassified（⛔ 不猜）。
--   改造项 5：9 条 doc_extract_* 按类型提取提示词追加「章节概要 SectionSummaries」输出约定。
--             （章节骨架与原文偏移由代码按画像 Toc 确定性生成，模型只给概要）
--
-- 幂等：SectionSummaries 块仅在缺失时追加；doc_profile 定点 REPLACE，重复执行无副作用。
-- 回滚：改动前建议 `CREATE TABLE cert_prompt_template_bak_20261125 AS SELECT * FROM cert_prompt_template;`
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- 改造项 5：9 条按类型提取提示词追加「章节概要」输出约定
-- -----------------------------------------------------------------------------
SET @sec_block = '\n\n## 章节概要（V4 必填，供审核员证据发现用）\n\n画像 {{doc_profile}} 里的 Toc 是本文档的客观章节表（系统已从原文切好，含每节的起止位置）。\n请在输出 JSON 顶层额外给出 SectionSummaries，为 Toc 中每个 Level ≤ 2 的标题各写一句概要（≤40 个汉字），说明该节实际写了什么。\n\n- Label 必须逐字抄 Toc 的 Title（不许改写、不许增删字、不许换标点）；对不上就丢弃该条。\n- Summary 只概括本节实际写到的内容；该节为空或只有标题 ⇒ Summary 给空串。\n- 只写概要：不要自己编章节号、页码、偏移或 CharStart/CharEnd —— 章节骨架由系统按 Toc 生成。\n- 示例："SectionSummaries": [ { "Label": "3 职责", "Summary": "规定采购、品质、仓储三部门在采购过程中的职责分工" } ]\n- Toc 缺失或为空 ⇒ SectionSummaries 给 []。\n';

UPDATE cert_prompt_template
SET Template = CONCAT(Template, @sec_block)
WHERE PromptCode IN (
        'doc_extract_manual_chapter',
        'doc_extract_process_profile',
        'doc_extract_record_profile',
        'doc_extract_index',
        'doc_extract_document_control',
        'doc_extract_work_instruction_citation',
        'doc_extract_policy_profile',
        'doc_extract_context_list',
        'doc_extract_audit'
      )
  AND (ScopeKey IS NULL OR ScopeKey = '')
  AND IsValid = 1
  AND IsDeleted = 0
  AND Template NOT LIKE '%SectionSummaries%';

-- -----------------------------------------------------------------------------
-- 改造项 2：doc_profile 提示词改「指纹 + 角色定位」（6 处定点替换）
-- -----------------------------------------------------------------------------
UPDATE cert_prompt_template
SET Template = REPLACE(
  REPLACE(
    REPLACE(
      REPLACE(
        REPLACE(
          REPLACE(Template,
            -- ① 输入：{{headings}} 之后补「结构指纹 + Toc」
            '{{headings}}',
            '{{headings}}\n\n结构指纹（代码从原文提取的客观证据，唯一分类判据）：\n{{fingerprints}}\n\n文档目录索引（Toc；Title 即章节名，含起止位置）：\n{{toc}}'),
          -- ② 判定顺序：去掉「先看文件名」，改为「先指纹、后角色」
          '先看文件名和目录，再看标题和开头正文。文件名与正文冲突时以正文为准，但 Confidence 不得高于 0.6。',
          '★ V4：先看上面的「结构指纹」定 DocType 与 ExtractionMode，命中的指纹 Key 就是证据。\n1. 文件名、目录、标题仅作辅助，不得单独作为分类依据；正文只在指纹缺失时用于复核。\n2. 任何分类结论都必须在本字段 Reason 里回引命中的指纹 Key（如「命中 manual.clause_mapping 与 manual.policy_objective」）。\n3. 指纹无高权重命中、也无法复核 ⇒ DocType="unclassified"、ExtractionMode="unclassified"、Confidence=0，不得猜、不得硬塞 other。'),
        -- ③ DocType 枚举：补 unclassified
        'review_record 管理评审记录；other 其他。\n输出同时给中文名 DocTypeName。',
        'review_record 管理评审记录；other 其他；unclassified 无法判定。\n判不出类型时必须写 unclassified，不许硬塞 other。\n输出同时给中文名 DocTypeName。'),
      -- ④ ExtractionMode 硬路由：补 unclassified
      '- other / 与标准无关 → none',
      '- other / 与标准无关 → none\n- unclassified（判不出）→ unclassified（待人工确认）'),
    -- ⑤ 输出 JSON：新增 Fingerprints / ClassSource，Reason 改为回引指纹
    '  "Confidence": 0.9,\n  "Reason": "文件名和正文均表明为质量手册"\n}',
    '  "Fingerprints": ["manual.issue_order", "manual.policy_objective", "manual.clause_mapping"],\n  "ClassSource": "fingerprint",\n  "Confidence": 0.9,\n  "Reason": "命中 manual.clause_mapping 与 manual.policy_objective，判定为质量手册"\n}'),
  -- ⑥ 硬规则 2：改为「指纹唯一判据」
  '2. 不确定就降低 Confidence，不强行分类。',
  '2. ★ V4：DocType 与 ExtractionMode 只能由「结构指纹」推出，Reason 必须回引命中的指纹 Key；禁止按文件名/目录/标题猜。判不出 ⇒ DocType="unclassified"、ExtractionMode="unclassified"、Confidence=0。')
WHERE PromptCode = 'doc_profile'
  AND (ScopeKey IS NULL OR ScopeKey = '')
  AND IsValid = 1
  AND IsDeleted = 0;

-- -----------------------------------------------------------------------------
-- 校验（期望：doc_profile 含 {{fingerprints}}；9 条均含 SectionSummaries）
-- -----------------------------------------------------------------------------
SELECT PromptCode,
       (Template LIKE '%{{fingerprints}}%') AS HasFingerprintPlaceholder,
       (Template LIKE '%SectionSummaries%')  AS HasSectionSummaries,
       CHAR_LENGTH(Template)                 AS Len
FROM cert_prompt_template
WHERE PromptCode LIKE 'doc_%' AND IsValid = 1 AND IsDeleted = 0
ORDER BY PromptCode;
