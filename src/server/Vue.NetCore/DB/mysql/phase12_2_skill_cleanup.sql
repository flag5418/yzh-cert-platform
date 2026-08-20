-- ============================================================
-- Phase 12.2: Skill 体系精简
-- 1. 移除 document_extract（文档内容提取走独立子系统，不属于 Skill 体系）
-- 2. 更新 compare Skill 反射信息（参数类型 object? → string?）
-- 3. 更新 assemble Skill 反射信息（参数重构为 prefix_text + suffix_text + joiner）
-- ============================================================

-- 1. 清理 document_extract 相关记录
DELETE FROM wf_skill_input WHERE skill_code = 'document_extract';
DELETE FROM wf_skill_output WHERE skill_code = 'document_extract';
DELETE FROM wf_skill_reflection WHERE skill_code = 'document_extract';
DELETE FROM wf_skill WHERE skill_code = 'document_extract';

-- 2. 更新 compare Skill 说明（反射信息不变，classPath 不变）
-- compare 参数类型从 object? 改为 string?，反射自动分析
-- 无需改 class_path / method_name

-- 3. 更新 assemble Skill 说明
UPDATE wf_skill
SET skill_name = '文本拼接',
    description = '将前半部分文本和后半部分文本按连接符拼接为一个字符串',
    modify_date = NOW()
WHERE skill_code = 'assemble';

-- 4. 清理 assemble 旧端口镜像（下次保存时反射自动重建）
DELETE FROM wf_skill_input WHERE skill_code = 'assemble';
DELETE FROM wf_skill_output WHERE skill_code = 'assemble';

-- 5. 清理 compare 旧端口镜像（下次保存时反射自动重建）
DELETE FROM wf_skill_input WHERE skill_code = 'compare';
DELETE FROM wf_skill_output WHERE skill_code = 'compare';

SELECT 'Phase 12.2 Skill 精简完成' AS result;
