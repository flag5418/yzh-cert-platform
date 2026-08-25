-- ============================================================
-- 提示词模板版本清理脚本
-- 目标：去掉版本号设计，每个提示词只保留一个稳定版本
-- 
-- 变更内容：
--   1. 逻辑删除旧版本：analyze_excel_v1 (Id=5), analyze_word_v1 (Id=4)
--   2. 重命名保留版本：去掉 _v1/_v2 后缀，统一为无版本号编码
--      analyze_excel_v2 → analyze_excel
--      analyze_word_v2  → analyze_word
--      analyze_pdf_v1   → analyze_pdf
--      extract_all_v1   → extract_all
--      verify_all_v1    → verify_all
--   3. 同步更新 prompt_name 去掉 _v1/_v2 后缀
-- ============================================================

-- Step 1: 逻辑删除旧版本（Enable=0）
UPDATE wf_prompt_template 
SET enable = 0, 
    is_active = 0,
    modify_date = NOW(),
    modifier = 'system'
WHERE prompt_code IN ('analyze_excel_v1', 'analyze_word_v1')
  AND enable = 1;

-- Step 2: 重命名保留版本——去掉 _v2 后缀
UPDATE wf_prompt_template 
SET prompt_code = 'analyze_excel',
    prompt_name = 'Excel表格结构分析',
    modify_date = NOW(),
    modifier = 'system'
WHERE prompt_code = 'analyze_excel_v2' 
  AND enable = 1;

UPDATE wf_prompt_template 
SET prompt_code = 'analyze_word',
    prompt_name = 'Word文档结构分析',
    modify_date = NOW(),
    modifier = 'system'
WHERE prompt_code = 'analyze_word_v2' 
  AND enable = 1;

-- Step 3: 重命名保留版本——去掉 _v1 后缀
UPDATE wf_prompt_template 
SET prompt_code = 'analyze_pdf',
    prompt_name = 'PDF文档结构分析',
    modify_date = NOW(),
    modifier = 'system'
WHERE prompt_code = 'analyze_pdf_v1' 
  AND enable = 1;

UPDATE wf_prompt_template 
SET prompt_code = 'extract_all',
    prompt_name = 'AI 文档提取（通用）',
    modify_date = NOW(),
    modifier = 'system'
WHERE prompt_code = 'extract_all_v1' 
  AND enable = 1;

UPDATE wf_prompt_template 
SET prompt_code = 'verify_all',
    prompt_name = 'AI 提取验证（通用）',
    modify_date = NOW(),
    modifier = 'system'
WHERE prompt_code = 'verify_all_v1' 
  AND enable = 1;

-- 验证结果
SELECT id, prompt_code, prompt_name, enable, is_active, version
FROM wf_prompt_template
WHERE enable = 1
ORDER BY prompt_type, prompt_code;
