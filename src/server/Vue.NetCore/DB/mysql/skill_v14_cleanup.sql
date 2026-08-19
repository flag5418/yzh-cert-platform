-- ============================================================
-- 清理无效 Skill 数据（V1.4.1）
-- 移除没有 C# 实现的占位 Skill 及其子表数据
-- 保留：document_extract, llm_extract, compare, get_field, get_table, assemble
-- 删除：assemble_text, date_diff, text_merge, llm_judge, llm_generate, create_nc, save_result, ai_node
-- ============================================================

-- 1. 先删子表
DELETE FROM wf_skill_input WHERE skill_code IN ('assemble_text', 'date_diff', 'text_merge', 'llm_judge', 'llm_generate', 'create_nc', 'save_result', 'ai_node');
DELETE FROM wf_skill_output WHERE skill_code IN ('assemble_text', 'date_diff', 'text_merge', 'llm_judge', 'llm_generate', 'create_nc', 'save_result', 'ai_node');
DELETE FROM wf_skill_reflection WHERE skill_code IN ('assemble_text', 'date_diff', 'text_merge', 'llm_judge', 'llm_generate', 'create_nc', 'save_result', 'ai_node');

-- 2. 删主表
DELETE FROM wf_skill WHERE skill_code IN ('assemble_text', 'date_diff', 'text_merge', 'llm_judge', 'llm_generate', 'create_nc', 'save_result', 'ai_node');
