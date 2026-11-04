-- =============================================================================
-- 2026-11-03 · 更新 audit_check 提示词：分析只给「建议 + 多条疑点」
--
-- 背景（用户 2026-11-03）：
--   系统是认证辅助系统，不是认证决策系统 —— AI 只给建议，最终是否符合、
--   不符合到什么程度，必须由审核员判定。
--   · 分析产出：判定依据（summary）+ 疑点分析（doubts，可多条）；
--   · 没有实质疑点 ⇒ doubts=[] 且 conclusion=conformant（系统建议该检查项合格）；
--   · 有疑点 ⇒ 不给 conformant（最多 observation / minor_nc / major_nc）。
--
-- 幂等：按 PromptCode 定位，重复执行结果一致。
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

UPDATE wf_prompt_template
SET Template = '作为认证审核专家，请根据以下条款要求与已挂接证据，对检查项进行符合性分析。\\n\\n你只提供「审核建议」，不替审核员下结论：最终是否符合、不符合到什么程度，必须由审核员判定。\\n\\n条款信息：{{ClauseInfo}}\\n\\n检查项要求：{{CheckRequirement}}\\n\\n已挂接证据：{{EvidenceList}}\\n\\n【判定依据 summary 的写法】按正式认证报告的口径书写，便于审核员审阅：\\n第一段先用一句话给出总体判断；\\n随后分段列出「证据支持」与「证据缺口 / 风险」，每条以「- 」开头，必要时注明条款号、文件或部门；\\n段落之间用 \\n\\n 分隔，列表项之间用 \\n 分隔，禁止写成一大段。\\n\\n【疑点分析 doubts 的写法】逐条列出可独立审阅的疑点，可以为多条，也可以为空数组：\\n- 每条包含 point（一句话标题）与 detail（为什么可疑：涉及哪份文件 / 哪个条款 / 缺了什么）；\\n- 没有实质疑点时必须输出 []，且 conclusion 必须为 conformant（系统据此建议该检查项合格）；\\n- 有疑点时不要给 conformant，最多给 observation 或 minor_nc / major_nc。\\n\\n只输出 JSON（不要代码块围栏、不要额外文字），字符串内换行必须转义为 \\n：\\n{"conclusion":"conformant|minor_nc|major_nc|observation","summary":"...","recommendation":"整改建议","confidence":0-100,"nc_suggestion":"如需开不符合项，写明不符合事实","severity_suggestion":"minor|major","doubts":[{"point":"疑点标题","detail":"疑点说明"}]}',
    Version = 2,
    UpdateTime = NOW()
WHERE PromptCode = 'audit_check'
  AND IsDeleted = 0;

-- 验证
SELECT PromptCode, Version, IsActive, LENGTH(Template) AS Len,
       CASE WHEN Template LIKE '%doubts%' THEN 'OK 含怀疑点契约' ELSE 'NG 缺少 doubts' END AS Chk
FROM wf_prompt_template
WHERE PromptCode = 'audit_check' AND IsDeleted = 0;
