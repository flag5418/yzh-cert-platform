-- ============================================================
-- Skill 体系精简同步 SQL（V1.4）
-- 修复 collation 问题：使用 CONVERT 统一编码
-- ============================================================

-- 1. 确保 compare 节点有 compare_result 输出端口
INSERT INTO wf_skill_output (code, skill_code, output_name, output_type, description, output_prompt, sort_order, enable, status, create_date, creator)
SELECT UUID(), 'compare', 'compare_result', 'boolean', '比较结果', '', 1, 1, 'active', NOW(), 'system'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM wf_skill_output
    WHERE skill_code COLLATE utf8mb4_unicode_ci = 'compare' COLLATE utf8mb4_unicode_ci
      AND output_name COLLATE utf8mb4_unicode_ci = 'compare_result' COLLATE utf8mb4_unicode_ci
);

-- 2. 确保所有功能节点有标准输出端口（success/error/result）
INSERT INTO wf_skill_output (code, skill_code, output_name, output_type, description, output_prompt, sort_order, enable, status, create_date, creator)
SELECT UUID(), s.skill_code, 'success', 'boolean', '是否执行成功', '', 0, 1, 'active', NOW(), 'system'
FROM wf_skill s
WHERE s.is_active = 1 AND s.enable = 1
  AND NOT EXISTS (
    SELECT 1 FROM wf_skill_output o
    WHERE o.skill_code COLLATE utf8mb4_unicode_ci = s.skill_code COLLATE utf8mb4_unicode_ci
      AND o.output_name COLLATE utf8mb4_unicode_ci = 'success' COLLATE utf8mb4_unicode_ci
  );

INSERT INTO wf_skill_output (code, skill_code, output_name, output_type, description, output_prompt, sort_order, enable, status, create_date, creator)
SELECT UUID(), s.skill_code, 'error', 'string', '失败时的错误信息', '', 0, 1, 'active', NOW(), 'system'
FROM wf_skill s
WHERE s.is_active = 1 AND s.enable = 1
  AND NOT EXISTS (
    SELECT 1 FROM wf_skill_output o
    WHERE o.skill_code COLLATE utf8mb4_unicode_ci = s.skill_code COLLATE utf8mb4_unicode_ci
      AND o.output_name COLLATE utf8mb4_unicode_ci = 'error' COLLATE utf8mb4_unicode_ci
  );

INSERT INTO wf_skill_output (code, skill_code, output_name, output_type, description, output_prompt, sort_order, enable, status, create_date, creator)
SELECT UUID(), s.skill_code, 'result', 'json', '执行结果（业务数据）', '', 0, 1, 'active', NOW(), 'system'
FROM wf_skill s
WHERE s.is_active = 1 AND s.enable = 1
  AND NOT EXISTS (
    SELECT 1 FROM wf_skill_output o
    WHERE o.skill_code COLLATE utf8mb4_unicode_ci = s.skill_code COLLATE utf8mb4_unicode_ci
      AND o.output_name COLLATE utf8mb4_unicode_ci = 'result' COLLATE utf8mb4_unicode_ci
  );

-- 3. 确保所有功能节点的 SkillType 为 method
UPDATE wf_skill SET skill_type = 'method' WHERE skill_type IS NULL OR skill_type = '' OR skill_type != 'method';
