-- =============================================================================
-- 20261127_clause_chapter_absorb_prompts_V1.sql
-- 目标：把「大章节（ChapterDomain）」从「属性池里的一条 hint」升级为
--       条款侧提示词链路的一等公民 —— clause_attr_gen（产出）+ 两段式证据发现（消费）。
--
-- 背景（用户裁定）：
--   · clause_attr_gen 正文里没有任何「大章节」判据（只有 {{attr_pool}} 里那条 hint）⇒ 模型判不准；
--   · 「大章节 ∧ 部门」是两步式召回的第一段初筛键，必须让两段式证据发现都看得见；
--   · 本次修改不能只改一个点（属性池），必须贯通「生成 → 召回初筛 → 证据生成」。
--
-- 覆盖的 3 个提示词（均存于 cert_prompt_template，平台级）：
--   ① clause_attr_gen          —— 新增「ChapterDomain 大章节」判据条目
--   ② clause_evidence_findings —— 第一段：把「章节画像」明确为「大章节 ∧ 部门」初筛键
--   ③ audit_check_evidence     —— 第二段：以「大章节」为跨标准合并骨架
--
-- 幂等：全部 REPLACE + NOT LIKE 标记保护，重复执行不改动。
-- 审计人：UpdateBy='migration'（AGENTS 铁律⑥ 白名单脚本标记）。
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- ① clause_attr_gen：在「分类判据」末尾（证据契约判据之前）插入 ChapterDomain 判据
-- -----------------------------------------------------------------------------
UPDATE cert_prompt_template
SET Template = REPLACE(
        Template,
        '## 证据契约判据（EvidenceContract）',
        CONCAT(
          '- ChapterDomain：条款归属的「大章节」，跨标准通用（只能取清单允许的码）。按条款功能与内容判，⛔ 不按章号判（章号与功能错位）：\n',
          '  组织环境（理解组织/相关方/范围）⇒ CD_ENV；领导作用（方针/职责/承诺）⇒ CD_LEAD；策划（风险机遇/目标/变更）⇒ CD_DECIDE；\n',
          '  支持（资源/人员/能力/知识/沟通/成文信息）⇒ CD_SUPPORT；运行（设计/采购/生产/交付/放行）⇒ CD_RUN；\n',
          '  绩效评价（监视测量/内审/管理评审/合规性评价）⇒ CD_ASSESS；改进（不合格/纠正/持续改进）⇒ CD_IMPROVE；判不出 ⇒ CD_UNKNOWN。\n',
          '  ★ 本属性是「大章节 ∧ 部门」两步式召回的第一段初筛键（下游用它与企业文档大章节取交集）—— 判错 ⇒ 召回整段跑偏。\n',
          '  ⛔ 不许给空串：判不出必须给 CD_UNKNOWN；与 Role 按功能对齐（Role r1–r7 与大章节一一对应：r1→CD_ENV…r7→CD_IMPROVE）。\n\n',
          '## 证据契约判据（EvidenceContract）'
        )),
    UpdateBy = 'migration',
    UpdateTime = NOW()
WHERE PromptCode = 'clause_attr_gen'
  AND Template LIKE '%## 证据契约判据（EvidenceContract）%'
  AND Template NOT LIKE '%ChapterDomain：条款归属的「大章节」%';

-- -----------------------------------------------------------------------------
-- ② clause_evidence_findings（第一段）：章节画像 → 明确为大章节初筛键
-- -----------------------------------------------------------------------------
UPDATE cert_prompt_template
SET Template = REPLACE(
        Template,
        '- 章节画像（去哪找、谁负责，可能为空）：',
        '- 大章节与去向（本条条款所属「大章节」+ 应读章节 + 责任部门；「大章节 ∧ 部门」是本次两步式召回的初筛键，可能为空）：'
      ),
    UpdateBy = 'migration',
    UpdateTime = NOW()
WHERE PromptCode = 'clause_evidence_findings'
  AND Template LIKE '%- 章节画像（去哪找、谁负责，可能为空）：%';

UPDATE cert_prompt_template
SET Template = REPLACE(
        Template,
        '不写符合性结论，不做条款结构/章节/证据契约分析。',
        '不写符合性结论，不重做条款属性/证据契约分析（大章节由系统给定，直接采用，不做重新判定）。'
      ),
    UpdateBy = 'migration',
    UpdateTime = NOW()
WHERE PromptCode = 'clause_evidence_findings'
  AND Template LIKE '%不写符合性结论，不做条款结构/章节/证据契约分析。%';

-- -----------------------------------------------------------------------------
-- ③ audit_check_evidence（第二段）：以「大章节」为跨标准合并骨架
-- -----------------------------------------------------------------------------
UPDATE cert_prompt_template
SET Template = REPLACE(
        Template,
        '- 条款与要求：\n{{clause_blocks}}',
        '- 条款与要求（每条款含正文 + 属性线索，其中「大章节」是跨标准合并的骨架）：\n{{clause_blocks}}'
      ),
    UpdateBy = 'migration',
    UpdateTime = NOW()
WHERE PromptCode = 'audit_check_evidence'
  AND Template LIKE '%- 条款与要求：\n{{clause_blocks}}%';

UPDATE cert_prompt_template
SET Template = REPLACE(
        Template,
        '3. 合并后每个 clause_evidence 对应一个逻辑条款，条数 = 合并后的逻辑条款数（通常少于输入条款数）。',
        '3. 合并后每个 clause_evidence 对应一个逻辑条款，条数 = 合并后的逻辑条款数（通常少于输入条款数）。\n4. 以「大章节」为骨架：只有同一大章节的条款才可合成一段；大章节不同（如 5 领导作用 与 9 绩效评价、7 支持 与 8 运行）不得合并成同一条证据。'
      ),
    UpdateBy = 'migration',
    UpdateTime = NOW()
WHERE PromptCode = 'audit_check_evidence'
  AND Template LIKE '%3. 合并后每个 clause_evidence 对应一个逻辑条款%'
  AND Template NOT LIKE '%以「大章节」为骨架：只有同一大章节的条款才可合成一段%';

-- -----------------------------------------------------------------------------
-- 验证
-- -----------------------------------------------------------------------------
SELECT PromptCode,
       LENGTH(Template) AS Len,
       CASE WHEN Template LIKE '%ChapterDomain%' THEN 'Y' ELSE '-' END AS has_chapterdomain,
       CASE WHEN Template LIKE '%大章节%' THEN 'Y' ELSE '-' END AS has_dachapter,
       UpdateBy, UpdateTime
FROM cert_prompt_template
WHERE PromptCode IN ('clause_attr_gen','clause_evidence_findings','audit_check_evidence')
ORDER BY PromptCode;
