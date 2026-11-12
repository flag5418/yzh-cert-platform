-- =============================================================================
-- 认证准备 / 认证检查 两套提示词 · 恢复种子（2026-11-11）
--
-- 现象：/audit-onsite 点「自动分析」→ analyze-item 返回
--       「未找到符合性分析提示词模板（audit_check）」。
-- 根因：wf_prompt_template 被 CodeFirst 重建后为空，20261030 / 20261101 两次
--       种子数据全部丢失（与 Sys_Menu 丢 10 条业务菜单同一批事故）。
--       引擎 PromptWorkbenchService.ListAsync 按 PromptType 精确取行，缺行即失败。
--
-- 本脚本做两件事（幂等，可重复执行）：
--   ① 对齐表结构：StandardCode / OrgCode 改 NULL DEFAULT NULL
--      （实体是 string?，语义「NULL = 平台级」；NOT NULL 无默认会让页面保存报 1364）
--   ② 重建平台级 audit_prep / audit_check 两行，正文按 46 号 §6.3 占位符与
--      §6.7 / §6.8 输出契约编写（平台级 = StandardCode/OrgCode 为 NULL）
--
-- 验收：两行都在、PromptType 精确、IsActive=1、Template 非空
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

ALTER TABLE `wf_prompt_template`
    MODIFY COLUMN `StandardCode` varchar(255) NULL DEFAULT NULL COMMENT '适用标准 Code；NULL=平台级（跨标准通用）',
    MODIFY COLUMN `OrgCode` varchar(255) NULL DEFAULT NULL COMMENT '机构 Code；NULL=平台级';

-- 平台级旧行先删（避免 Version 膨胀与正文分叉）
DELETE FROM `wf_prompt_template`
WHERE `PromptType` IN ('audit_prep', 'audit_check')
  AND `StandardCode` IS NULL
  AND `OrgCode` IS NULL;

-- ── audit_prep：资料完备性（值域 found/partial/missing/unknown/na）──
INSERT INTO `wf_prompt_template`
  (`Code`, `PromptCode`, `PromptName`, `PromptType`, `SkillTarget`, `StandardCode`, `OrgCode`,
   `Template`, `ModelName`, `MaxTokens`, `Temperature`, `Description`, `Version`, `IsActive`, `Status`,
   `CreateBy`, `CreateTime`, `IsValid`, `IsDeleted`)
VALUES
  (REPLACE(UUID(), '-', ''), 'audit_prep', '认证准备 · 资料完备性分析', 'audit_prep', 'CertCheckEngine', NULL, NULL,
   '作为认证审核专家，你正在做入场前的资料完备性检查（分支：{{task_kind}}）。

检查项：{{check_item_name}}
涉及部门：{{dept_names}}

## 条款与属性
{{clause_blocks}}

## 上次结论（仅增量模式有值）
{{previous_results}}

## 你的任务
逐条款判断「企业提交的资料是否足以支撑现场审核」，只回答资料完备性，不回答符合性。

## 判定口径（严格按此执行）
- found   ：所需资料齐全，且互相不矛盾
- partial ：有资料但缺关键要素，或不同文档之间互相矛盾
- missing ：完全找不到所需资料
- unknown ：无法判断（资料不足以下判断）—— 不要猜，不要当成不符合
- na      ：该条款不适用于本企业 / 本阶段

## 硬约束
- 不得输出 pass / ofi / nc —— 那是现场检查阶段的事
- unknown 绝不等于不符合：不知道就是不知道
- missing_kinds 只列资料名称（如「管理评审记录」），不要写整句
- conflicts 只列互相矛盾的具体事实（如「A 文件说 3 人，B 文件说 5 人」）
- 每条结论必须能指回上面的条款块；没有对应证据就不要编造摘录

## 输出
严格输出 JSON 对象（不要解释文字、不要 markdown 代码块）：
{"prep_state":"found|partial|missing|unknown|na","summary":"一句话总结","missing_kinds":["缺的资料名称"],"conflicts":["矛盾描述"],"verify_level":"doc|remote|onsite|test","prep_nc_hint":"给现场的建议（可为空）","doubts":[{"point":"疑点标题","detail":"为什么可疑"}],"confidence":0-100}',
   NULL, NULL, NULL,
   '认证准备分支：判断资料完备性（found/partial/missing/unknown/na），不下符合性结论', 1, 1, 'active',
   'system', NOW(), 1, 0);

-- ── audit_check：符合性判定（值域 pass/ofi/nc/na）──
INSERT INTO `wf_prompt_template`
  (`Code`, `PromptCode`, `PromptName`, `PromptType`, `SkillTarget`, `StandardCode`, `OrgCode`,
   `Template`, `ModelName`, `MaxTokens`, `Temperature`, `Description`, `Version`, `IsActive`, `Status`,
   `CreateBy`, `CreateTime`, `IsValid`, `IsDeleted`)
VALUES
  (REPLACE(UUID(), '-', ''), 'audit_check', '认证检查 · 符合性判定', 'audit_check', 'CertCheckEngine', NULL, NULL,
   '作为认证审核专家，你正在做认证检查（分支：{{task_kind}}）。只依据下面给出的条款、条款属性与企业文档摘录 / 现场证据判定，不得凭空补事实。

检查项：{{check_item_name}}
涉及部门：{{dept_names}}

## 条款与属性
{{clause_blocks}}

## 现场证据
{{onsite_evidence}}

## 上次结论（仅增量模式有值）
{{previous_results}}

## 你的任务
逐条款回答「企业的实际做法是否符合该条款要求」，并给出证据指向。

## 判定口径（严格按此执行）
- pass ：有明确证据表明做法符合条款要求（证据必须能指回上面的条款块或证据块）
- ofi  ：基本符合，但存在改进空间；或证据不足以确认符合，需要现场进一步核实
- nc   ：有明确证据表明未满足条款要求（事实清楚、可复现）
- na   ：该条款不适用于本企业 / 本次审核范围

## 硬约束
1. severity 不得高于条款基线（见条款属性里的 Severity）；Obligation 不是 shall 时最高 observation。
2. conclusion=pass 时 nc_suggestion 必须为 no。
3. nc_draft 只是草稿，系统永不自动签发。
4. 没有证据支撑时不得判 pass，也不得把「文档没写」直接当成 nc；应判 ofi 并在 doubts 里说明需核实什么。
5. 事实描述只写客观事实，不写原因分析。
6. doubts 为空表示无疑点（系统会建议合格）；有疑点必须逐条写清 point 与 detail。

## 输出
严格输出 JSON 对象（不要解释文字、不要 markdown 代码块）：
{"conclusion":"pass|ofi|nc|na","summary":"一句话结论","recommendation":"整改或核实建议（可为空）","confidence":0-100,"nc_suggestion":"yes|no|uncertain","severity_suggestion":"major|minor|observation","evidence_codes":["引用的证据/文档标识"],"doubts":[{"point":"疑点标题","detail":"涉及哪份文件/条款、缺什么或哪里矛盾"}],"nc_draft":{"fact_desc":"不符合事实","requirement_ref":"条款原文","severity":"major|minor","corrective_req":"整改要求","due_days":30}}',
   NULL, NULL, NULL,
   '认证检查分支：判断符合性（pass/ofi/nc/na）+ 疑点 + NC 草稿；系统建议不代替审核员结论', 1, 1, 'active',
   'system', NOW(), 1, 0);

-- ── 验收 ──
SELECT `PromptCode`, `PromptType`, `StandardCode`, `OrgCode`, `Version`, `IsActive`,
       CHAR_LENGTH(`Template`) AS `TemplateChars`, `IsValid`, `IsDeleted`
FROM `wf_prompt_template`
WHERE `PromptType` IN ('audit_prep', 'audit_check')
ORDER BY `PromptType`;
