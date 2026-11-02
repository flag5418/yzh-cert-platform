-- =============================================================================
-- 认证准备 / 认证检查 · 两套提示词分流（V2 专项 — 46 号 §6）
-- 日期：2026-11-01
--
-- 背景（为什么必须分开）：
--   认证准备问「资料齐不齐」（输出 found/partial/missing/unknown/na），
--   认证检查问「符不符合」（输出 pass/ofi/nc/na）。
--   共用一个 audit_check 提示词会让模型在两个值域之间摇摆 —— 这正是
--   「把 pass 写进 PrepState」那类语义污染的温床（46 号 F3）。
--
-- 本脚本做三件事：
--   ① Sys_DictionaryList 补 audit_prep / audit_check 两条
--      （否则「提示词工作台」的类型下拉里选不到这两个类型）
--   ② wf_prompt_template 补 audit_prep 一行（平台级 · 生效）
--      ⚠️ audit_check 已存在（2026-10-30 建的），不动它
--   ③ 验收：类型字典 2 条 + 模板 2 行 + PromptType 精确匹配
--
-- ★ 幂等：可重复执行
-- ★ 关键坑：PromptWorkbenchService.ListAsync 按 PromptType 精确过滤（不是 PromptCode），
--   所以 PromptType 必须逐字等于常量值 'audit_prep' / 'audit_check'
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

SELECT '========== 开始：认证准备/检查 提示词分流 V1 ==========' AS `step`;


-- ─────────────────────────────────────────────────────────────────────────────
-- ① 类型字典（PROMPT_TYPE）补两条
-- ─────────────────────────────────────────────────────────────────────────────

-- audit_prep
SET @exists := (
  SELECT COUNT(*) FROM `Sys_DictionaryList`
  WHERE `DicCode` = 'PROMPT_TYPE' AND `DicValue` = 'audit_prep' AND `IsDeleted` = 0
);
INSERT INTO `Sys_DictionaryList`
  (`Code`, `DicCode`, `DicValue`, `DicName`, `OrderNo`, `IsValid`, `IsDeleted`, `CreateTime`, `Remark`)
SELECT 'PT_audit_prep', 'PROMPT_TYPE', 'audit_prep',
       '认证准备（资料完备性）', 50, 1, 0, NOW(),
       '输入「条款+属性+文档摘录」→ 输出 found/partial/missing/unknown/na + 缺料 + 矛盾'
WHERE @exists = 0;

-- audit_check
SET @exists := (
  SELECT COUNT(*) FROM `Sys_DictionaryList`
  WHERE `DicCode` = 'PROMPT_TYPE' AND `DicValue` = 'audit_check' AND `IsDeleted` = 0
);
INSERT INTO `Sys_DictionaryList`
  (`Code`, `DicCode`, `DicValue`, `DicName`, `OrderNo`, `IsValid`, `IsDeleted`, `CreateTime`, `Remark`)
SELECT 'PT_audit_check', 'PROMPT_TYPE', 'audit_check',
       '认证检查（符合性判定）', 60, 1, 0, NOW(),
       '输入「条款+证据+现场证据」→ 输出 pass/ofi/nc/na + NC 建议 + 严重程度'
WHERE @exists = 0;


-- ─────────────────────────────────────────────────────────────────────────────
-- ② wf_prompt_template 补 audit_prep（平台级 · 生效 · Version=1）
--
--    正文用 §6.3 的占位符：{{task_kind}} / {{check_item_name}} / {{dept_names}}
--    / {{clause_blocks}} / {{previous_results}}
--    ⛔ 不含 {{onsite_evidence}}（那是检查分支才有的现场证据）
-- ─────────────────────────────────────────────────────────────────────────────

SET @exists := (
  SELECT COUNT(*) FROM `wf_prompt_template`
  WHERE `PromptType` = 'audit_prep' AND `StandardCode` IS NULL AND `IsDeleted` = 0
);

INSERT INTO `wf_prompt_template`
  (`Code`, `PromptCode`, `PromptName`, `PromptType`, `SkillTarget`, `StandardCode`,
   `Template`, `Description`, `Version`, `IsActive`, `Status`, `Sort`,
   `CreateBy`, `CreateTime`, `IsValid`, `IsDeleted`)
SELECT
  REPLACE(UUID(), '-', ''),
  'audit_prep',
  '认证准备 · 资料完备性分析',
  'audit_prep',
  'all',
  NULL,
  CONCAT(
'作为认证审核专家，你正在做**入场前的资料完备性检查**（分支：{{task_kind}}）。

检查项：{{check_item_name}}
涉及部门：{{dept_names}}

{{clause_blocks}}

上次结论（仅增量模式有值）：
{{previous_results}}

## 你的任务
逐条款判断「**企业提交的资料是否足以支撑现场审核**」，只回答资料完备性，**不回答符合性**。

## 判定口径（严格按此执行）
- found   ：所需资料齐全，且互相不矛盾
- partial ：有资料但缺关键要素，或不同文档之间互相矛盾
- missing ：完全找不到所需资料
- unknown ：无法判断（资料不足以下判断）—— 不要猜，不要当成不符合
- na      ：该条款不适用于本企业 / 本阶段

## 硬约束
- 你不能输出 pass / ofi / nc —— 那是现场检查阶段的事，现在还没到现场
- unknown 绝不等于不符合：不知道就是不知道
- missing_kinds 只列**资料名称**（如「管理评审记录」），不要写整句
- conflicts 只列**互相矛盾的具体事实**（「A 文件说 3 人，B 文件说 5 人」）

## 输出
严格输出 JSON 对象（不要任何解释文字、不要 markdown 代码块）：
{"prep_state":"found|partial|missing|unknown|na","summary":"一句话总结","missing_kinds":["缺的资料名称"],"conflicts":["矛盾描述"],"verify_level":"doc|remote|onsite|test","prep_nc_hint":"给现场的建议（可为空）","confidence":0-100}'
  ),
  '认证准备分支：判断资料完备性（found/partial/missing/unknown/na），不下符合性结论',
  1, 1, 'active', 50,
  'system', NOW(), 1, 0
WHERE @exists = 0;


-- ─────────────────────────────────────────────────────────────────────────────
-- ③ 验收
-- ─────────────────────────────────────────────────────────────────────────────

SELECT '---------- 验收 ----------' AS `step`;

-- 3.1 类型字典两条都在
SELECT '3.1 PROMPT_TYPE 字典' AS `check`,
       GROUP_CONCAT(`DicValue` ORDER BY `OrderNo`) AS `values`,
       IF(COUNT(*) = 2, 'OK', '❌ 缺项') AS `result`
FROM `Sys_DictionaryList`
WHERE `DicCode` = 'PROMPT_TYPE' AND `DicValue` IN ('audit_prep', 'audit_check') AND `IsDeleted` = 0;

-- 3.2 模板两行都在，且 PromptType 精确等于常量值
SELECT '3.2 模板行' AS `check`,
       GROUP_CONCAT(CONCAT(`PromptCode`, '(', `PromptType`, ',v', `Version`, ',active=', `IsActive`, ')')
                    ORDER BY `PromptCode`) AS `rows`,
       IF(SUM(`PromptType` = 'audit_prep') > 0 AND SUM(`PromptType` = 'audit_check') > 0,
          'OK', '❌ 缺模板') AS `result`
FROM `wf_prompt_template`
WHERE `PromptType` IN ('audit_prep', 'audit_check')
  AND `StandardCode` IS NULL AND `IsActive` = 1 AND `IsDeleted` = 0;

-- 3.3 audit_prep 正文必须含关键占位符，且不能含现场证据占位符
SELECT '3.3 audit_prep 正文' AS `check`,
       CONCAT('clause_blocks=', IF(`Template` LIKE '%{{clause_blocks}}%', 'Y', 'N'),
              ' onsite_evidence=', IF(`Template` LIKE '%{{onsite_evidence}}%', 'Y(❌应无)', 'N')) AS `detail`,
       IF(`Template` LIKE '%{{clause_blocks}}%' AND `Template` NOT LIKE '%{{onsite_evidence}}%',
          'OK', '❌ 占位符不对') AS `result`
FROM `wf_prompt_template`
WHERE `PromptType` = 'audit_prep' AND `StandardCode` IS NULL AND `IsDeleted` = 0;

SELECT '========== 完成：两套提示词已就绪 ==========' AS `step`;
