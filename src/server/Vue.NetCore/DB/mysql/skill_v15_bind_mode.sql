-- ============================================================
-- Skill V15：wf_skill_input 添加 bind_mode + enum_source 字段
-- 用途：支持参数绑定模式（Link/LinkOrConstant/Enum）和字典来源指定
-- ============================================================

-- ① 添加 bind_mode 字段（绑定模式）
SET @col_exists = (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE table_schema = DATABASE() AND table_name = 'wf_skill_input' AND column_name = 'bind_mode');
SET @sql = IF(@col_exists = 0,
  'ALTER TABLE wf_skill_input ADD COLUMN bind_mode VARCHAR(20) NOT NULL DEFAULT \'LinkOrConstant\' COMMENT \'绑定模式：Link/LinkOrConstant/Enum\' AFTER default_value',
  'SELECT \'列 bind_mode 已存在\'');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ② 添加 enum_source 字段（字典编码）
SET @col_exists2 = (SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE table_schema = DATABASE() AND table_name = 'wf_skill_input' AND column_name = 'enum_source');
SET @sql2 = IF(@col_exists2 = 0,
  'ALTER TABLE wf_skill_input ADD COLUMN enum_source VARCHAR(100) NULL COMMENT \'字典编码（BindMode=Enum 时必填），对应 Sys_Dictionary.DicNo\' AFTER bind_mode',
  'SELECT \'列 enum_source 已存在\'');
PREPARE stmt2 FROM @sql2;
EXECUTE stmt2;
DEALLOCATE PREPARE stmt2;

-- ③ 同步更新已有 Skill 的 bind_mode 和 enum_source（从反射重新分析）
-- CompareSkill: operator 参数是 Enum 模式
UPDATE wf_skill_input SET bind_mode = 'Enum', enum_source = 'compare_operator'
WHERE skill_code = 'compare' AND input_name = 'operator';

-- 其他参数默认为 LinkOrConstant（已在 DEFAULT 中设置）

-- ④ 删除旧的 enum_values 列（如果存在且不再需要）
-- 注意：先确认没有其他代码使用此列再删除
-- SET @col_old = (SELECT COUNT(*) FROM information_schema.COLUMNS
--   WHERE table_schema = DATABASE() AND table_name = 'wf_skill_input' AND column_name = 'enum_values');
-- SET @sql_old = IF(@col_old > 0,
--   'ALTER TABLE wf_skill_input DROP COLUMN enum_values',
--   'SELECT \'列 enum_values 不存在\'');
-- PREPARE stmt_old FROM @sql_old;
-- EXECUTE stmt_old;
-- DEALLOCATE PREPARE stmt_old;

SELECT 'Skill V15 迁移完成：wf_skill_input 已添加 bind_mode + enum_source 字段' AS result;
