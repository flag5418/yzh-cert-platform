-- ============================================================
-- Skill V2 静态方法版：数据库清理 + 同步
-- 1. 清理 llm_extract 相关记录
-- 2. 更新 5 个 Skill 的反射信息（classPath + methodName）
-- 3. 清理冗余字段（version/icon/color）
-- ============================================================

-- ① 删除 llm_extract
DELETE FROM wf_skill_reflection WHERE skill_code = 'llm_extract';
DELETE FROM wf_skill_input WHERE skill_code = 'llm_extract';
DELETE FROM wf_skill_output WHERE skill_code = 'llm_extract';
DELETE FROM wf_skill WHERE skill_code = 'llm_extract';

-- ② 更新 5 个 Skill 的 SkillName/Description（从反射同步）
-- get_field
UPDATE wf_skill SET 
  skill_name = '获取字段值',
  description = '按字段编码和企业编码查询已提取的文档字段值',
  skill_type = 'method'
WHERE skill_code = 'get_field';

-- get_table
UPDATE wf_skill SET 
  skill_name = '获取表格数据',
  description = '按表格编码和企业编码查询已提取的表格数据',
  skill_type = 'method'
WHERE skill_code = 'get_table';

-- compare
UPDATE wf_skill SET 
  skill_name = '值比较',
  description = '确定性比较：数值比较（value+operator+threshold）、日期差（date_a+date_b）、非空判断（operator=not_empty）',
  skill_type = 'method'
WHERE skill_code = 'compare';

-- assemble
UPDATE wf_skill SET 
  skill_name = '文本拼接',
  description = '将任意数量片段（常量/变量按序混合）拼接成一个字符串',
  skill_type = 'method'
WHERE skill_code = 'assemble';

-- document_extract
UPDATE wf_skill SET 
  skill_name = '文档内容提取',
  description = '本地解析 Word/Excel/PDF/Text 文件，输出结构化段落、表格和全文文本',
  skill_type = 'method'
WHERE skill_code = 'document_extract';

-- ③ 同步反射信息（classPath + methodName）
-- 先清理旧反射信息
DELETE FROM wf_skill_reflection WHERE skill_code IN ('get_field','get_table','compare','assemble','document_extract');

-- 插入新反射信息
INSERT INTO wf_skill_reflection (code, skill_code, class_path, method_name, param_binding, enable, status, create_date, creator)
VALUES
  (UUID(), 'get_field',       'YZH.Core.Skills.GetFieldSkill',       'ExecuteAsync', NULL, 1, 'active', NOW(), 'system'),
  (UUID(), 'get_table',       'YZH.Core.Skills.GetTableSkill',       'ExecuteAsync', NULL, 1, 'active', NOW(), 'system'),
  (UUID(), 'compare',        'YZH.Core.Skills.CompareSkill',         'ExecuteAsync', NULL, 1, 'active', NOW(), 'system'),
  (UUID(), 'assemble',        'YZH.Core.Skills.AssembleSkill',        'ExecuteAsync', NULL, 1, 'active', NOW(), 'system'),
  (UUID(), 'document_extract','YZH.Core.Skills.DocumentExtractSkill', 'ExecuteAsync', NULL, 1, 'active', NOW(), 'system');

-- ④ 添加唯一索引：class_path + method_name（防重复注册）
-- 先检查是否已有索引
SET @idx_exists = (SELECT COUNT(*) FROM information_schema.STATISTICS 
  WHERE table_schema = DATABASE() AND table_name = 'wf_skill_reflection' AND index_name = 'uk_class_method');
SET @sql = IF(@idx_exists = 0,
  'ALTER TABLE wf_skill_reflection ADD UNIQUE INDEX uk_class_method (class_path, method_name)',
  'SELECT ''索引 uk_class_method 已存在''');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
