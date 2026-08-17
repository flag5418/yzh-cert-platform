-- ============================================================
-- Phase 10: wf_skill 体系升级（自定义工作流引擎 V1.2 §5.9/§5.10）
-- 日期: 2026-08-17
-- 说明: 幂等设计，可重复执行（ALTER 用存储过程判存在，新表用 IF NOT EXISTS）
-- 关联: docs/80-功能设计/01-系统管理/工作流管理/自定义工作流引擎-功能设计-V1.md §5.9/§5.10
-- ============================================================

-- ============================================================
-- 0. 幂等辅助存储过程
-- ============================================================
DROP PROCEDURE IF EXISTS `p10_add_column`;
DROP PROCEDURE IF EXISTS `p10_add_index`;
DROP PROCEDURE IF EXISTS `p10_drop_index`;

DELIMITER //
CREATE PROCEDURE `p10_add_column`(IN p_table VARCHAR(64), IN p_col VARCHAR(64), IN p_def VARCHAR(500))
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.COLUMNS
                 WHERE table_schema = DATABASE() AND table_name = p_table AND column_name = p_col) THEN
    SET @sql = CONCAT('ALTER TABLE `', p_table, '` ADD COLUMN `', p_col, '` ', p_def);
    PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    SELECT CONCAT('Added ', p_table, '.', p_col) AS result;
  ELSE
    SELECT CONCAT('Skipped: ', p_table, '.', p_col, ' exists') AS result;
  END IF;
END//

CREATE PROCEDURE `p10_add_index`(IN p_table VARCHAR(64), IN p_index VARCHAR(64), IN p_cols VARCHAR(300))
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.STATISTICS
                 WHERE table_schema = DATABASE() AND table_name = p_table AND index_name = p_index) THEN
    SET @sql = CONCAT('ALTER TABLE `', p_table, '` ADD INDEX `', p_index, '` (', p_cols, ')');
    PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
    SELECT CONCAT('Added index ', p_index, ' on ', p_table) AS result;
  ELSE
    SELECT CONCAT('Skipped: index ', p_index, ' exists') AS result;
  END IF;
END//
DELIMITER ;

-- ============================================================
-- 1. B-09 ent_table_extraction_result 新增 table_code（评审 §3.3 / 阶段 0-1）
-- ============================================================
CALL p10_add_column('ent_table_extraction_result', 'table_code', "varchar(200) DEFAULT NULL COMMENT '定义表编码(cert_doc_table_def.code)，工作流 get_table 节点查询键' AFTER rule_code");
CALL p10_add_index('ent_table_extraction_result', 'idx_table_ext_ent_tbl', 'enterprise_code, table_code');

-- ============================================================
-- 2. wf_skill 主表升级（V1.2 §5.2）
-- ============================================================
CALL p10_add_column('wf_skill', 'category', "varchar(50) NOT NULL DEFAULT 'data_process' COMMENT '功能分类: data_access/data_process/ai_judge/ai_generate/output' AFTER skill_type");
CALL p10_add_column('wf_skill', 'side_effect', "tinyint(1) NOT NULL DEFAULT 0 COMMENT '0=逻辑性(纯函数) 1=功能性(读写外部)' AFTER category");
CALL p10_add_column('wf_skill', 'skill_prompt', "text NULL COMMENT 'AI 使用提示词（解释器组装用）' AFTER description");
CALL p10_add_column('wf_skill', 'output_strict', "tinyint(1) NOT NULL DEFAULT 1 COMMENT '1=强约束校验 0=弱约束(ai_node)' AFTER is_active");
CALL p10_add_column('wf_skill', 'return_type', "varchar(20) NOT NULL DEFAULT 'json' COMMENT '主输出类型: string/number/date/boolean/json' AFTER output_strict");
CALL p10_add_column('wf_skill', 'version', "varchar(20) NOT NULL DEFAULT '1.0' COMMENT '实现版本' AFTER return_type");
CALL p10_add_column('wf_skill', 'icon', "varchar(50) NULL COMMENT '面板图标' AFTER version");
CALL p10_add_column('wf_skill', 'color', "varchar(20) NULL COMMENT '面板颜色' AFTER icon");
CALL p10_add_column('wf_skill', 'sort_order', "int NOT NULL DEFAULT 0 COMMENT '面板排序' AFTER color");
CALL p10_add_index('wf_skill', 'uk_wf_skill_code', 'skill_code');
CALL p10_add_index('wf_skill', 'idx_wf_skill_type', 'skill_type');
CALL p10_add_index('wf_skill', 'idx_wf_skill_category', 'category');
CALL p10_add_index('wf_skill', 'idx_wf_skill_active', 'is_active');

-- ============================================================
-- 3. 输入表单模板表（新建，V1.2 §5.3）
-- ============================================================
CREATE TABLE IF NOT EXISTS wf_skill_input (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  code VARCHAR(100) NOT NULL,
  skill_code VARCHAR(100) COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '所属 Skill（与 wf_skill.skill_code 同 collation）',
  input_name VARCHAR(100) NOT NULL, input_label VARCHAR(200) NULL,
  input_type VARCHAR(20) NOT NULL DEFAULT 'text' COMMENT 'text/number/date/boolean/enum/field_ref/table_ref/json',
  enum_values TEXT NULL,
  is_required TINYINT(1) NOT NULL DEFAULT 0, default_value VARCHAR(500) NULL,
  sort_order INT NOT NULL DEFAULT 0,
  enable TINYINT(1) NOT NULL DEFAULT 1, create_id INT NULL, creator VARCHAR(50) NULL, create_date DATETIME NULL,
  modify_id INT NULL, modifier VARCHAR(50) NULL, modify_date DATETIME NULL,
  delete_id INT NULL, deleter VARCHAR(50) NULL, delete_time DATETIME NULL,
  status VARCHAR(50) NULL DEFAULT 'active', remark VARCHAR(500) NULL,
  UNIQUE KEY uk_skill_input (skill_code, input_name), KEY idx_skill_input_skill (skill_code)
) COMMENT='Skill 输入表单模板（画布生成输入表单用，非硬校验）';

-- ============================================================
-- 4. 强约束输出契约表（新建，V1.2 §5.4）
-- ============================================================
CREATE TABLE IF NOT EXISTS wf_skill_output (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  code VARCHAR(100) NOT NULL,
  skill_code VARCHAR(100) COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '所属 Skill（与 wf_skill.skill_code 同 collation）',
  output_name VARCHAR(100) NOT NULL,
  output_type VARCHAR(20) NOT NULL DEFAULT 'json' COMMENT 'string/number/date/boolean/json',
  output_prompt TEXT NULL COMMENT '输出解读提示词（解释器组装用）',
  description VARCHAR(500) NULL, sort_order INT NOT NULL DEFAULT 0,
  enable TINYINT(1) NOT NULL DEFAULT 1, create_id INT NULL, creator VARCHAR(50) NULL, create_date DATETIME NULL,
  modify_id INT NULL, modifier VARCHAR(50) NULL, modify_date DATETIME NULL,
  delete_id INT NULL, deleter VARCHAR(50) NULL, delete_time DATETIME NULL,
  status VARCHAR(50) NULL DEFAULT 'active', remark VARCHAR(500) NULL,
  UNIQUE KEY uk_skill_output (skill_code, output_name), KEY idx_skill_output_skill (skill_code)
) COMMENT='强约束 Skill 输出契约（output_strict=1 时解释器强校验）';

-- ============================================================
-- 5. 反射信息表（新建，method 型 1:1，V1.2 §5.5）
-- ============================================================
CREATE TABLE IF NOT EXISTS wf_skill_reflection (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  code VARCHAR(100) NOT NULL,
  skill_code VARCHAR(100) COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '所属 Skill（与 wf_skill.skill_code 同 collation）',
  class_path VARCHAR(500) NOT NULL COMMENT '反射的地址（类型全名）',
  method_name VARCHAR(200) NOT NULL DEFAULT 'ExecuteAsync' COMMENT '反射的方法',
  param_binding TEXT NULL COMMENT '参数绑定 JSON: {"输入项名":"方法参数名或顺序"}',
  enable TINYINT(1) NOT NULL DEFAULT 1, create_id INT NULL, creator VARCHAR(50) NULL, create_date DATETIME NULL,
  modify_id INT NULL, modifier VARCHAR(50) NULL, modify_date DATETIME NULL,
  delete_id INT NULL, deleter VARCHAR(50) NULL, delete_time DATETIME NULL,
  status VARCHAR(50) NULL DEFAULT 'active', remark VARCHAR(500) NULL,
  UNIQUE KEY uk_skill_reflection (skill_code),
  CONSTRAINT fk_reflection_skill FOREIGN KEY (skill_code) REFERENCES wf_skill(skill_code)
) COMMENT='method 型 Skill 反射信息（1:1）';

-- ============================================================
-- 6. API 信息表（新建，api 型 1:1，V1.2 §5.6；本期预留，后续实现）
-- ============================================================
CREATE TABLE IF NOT EXISTS wf_skill_api (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  code VARCHAR(100) NOT NULL,
  skill_code VARCHAR(100) COLLATE utf8mb4_0900_ai_ci NOT NULL COMMENT '所属 Skill（与 wf_skill.skill_code 同 collation）',
  url VARCHAR(500) NOT NULL, http_method VARCHAR(10) NOT NULL DEFAULT 'POST',
  headers TEXT NULL COMMENT '请求头 JSON（值可含 $sys. 引用）',
  auth_config TEXT NULL COMMENT '鉴权 JSON: {"type":"bearer","tokenSource":"$sys.XXX"}——密钥不落库',
  param_mapping TEXT NULL COMMENT '参数映射: {"输入项名":"请求参数名"}',
  response_mapping TEXT NULL COMMENT '响应解析: {"输出项名":"$.data.xxx"}',
  timeout_seconds INT NOT NULL DEFAULT 30,
  enable TINYINT(1) NOT NULL DEFAULT 1, create_id INT NULL, creator VARCHAR(50) NULL, create_date DATETIME NULL,
  modify_id INT NULL, modifier VARCHAR(50) NULL, modify_date DATETIME NULL,
  delete_id INT NULL, deleter VARCHAR(50) NULL, delete_time DATETIME NULL,
  status VARCHAR(50) NULL DEFAULT 'active', remark VARCHAR(500) NULL,
  UNIQUE KEY uk_skill_api (skill_code),
  CONSTRAINT fk_api_skill FOREIGN KEY (skill_code) REFERENCES wf_skill(skill_code)
) COMMENT='api 型 Skill 信息（1:1，预留）';

-- ============================================================
-- 7. 业务行新增布局列（V1.2 §5.8）
-- ============================================================
CALL p10_add_column('cert_validation_rule', 'layout_json', "text NULL COMMENT '画布布局JSON(节点坐标/缩放/平移,UI恢复用,解释器不读)' AFTER rule_json");
CALL p10_add_column('rpt_report_section', 'layout_json', "text NULL COMMENT '画布布局JSON(节点坐标/缩放/平移,UI恢复用,解释器不读)' AFTER workflow_config");

-- ============================================================
-- 8. 种子整改（V1.2 §5.10：7 行登记即实现）
-- ============================================================

-- 8.1 停用无实现的不匹配行（date_diff/text_merge/llm_judge/llm_generate/create_nc/save_result/assemble_text）
UPDATE wf_skill
SET is_active = 0, status = 'disabled',
    remark = CONCAT(IFNULL(remark, ''), '; phase10: 无对应实现，停用（自定义工作流引擎 V1.2 §5.10）')
WHERE skill_code IN ('date_diff', 'text_merge', 'llm_judge', 'llm_generate', 'create_nc', 'save_result', 'assemble_text');

-- 8.2 6 个已实现 Skill 元数据落位（UPSERT：旧表缺行则 INSERT，已存在则 UPDATE，与 YZH.Core/Skills SkillBase 声明对齐）
INSERT INTO wf_skill
  (code, skill_code, skill_name, skill_type, category, side_effect, output_strict, return_type,
   description, is_active, create_date, creator, enable, status)
VALUES
  (UUID(), 'get_field', '获取字段值', 'method', 'data_access', 1, 1, 'json', '按 field_code + enterprise_code + file_code 从 B-08 提取字段值', 1, NOW(), 'system', 1, 'active'),
  (UUID(), 'get_table', '获取表格数据', 'method', 'data_access', 1, 1, 'json', '按 table_code + enterprise_code + file_code 从 B-09 提取表格行', 1, NOW(), 'system', 1, 'active'),
  (UUID(), 'compare', '值比较', 'method', 'data_process', 0, 1, 'boolean', '多条件逻辑判断（gt/gte/lt/lte/eq/neq/contains/not_contains），输出 result 布尔', 1, NOW(), 'system', 1, 'active'),
  (UUID(), 'assemble', '字符串拼接', 'method', 'data_process', 0, 1, 'string', 'N 段文本按序拼接（常量 + {{引用}} 混合），输出 content', 1, NOW(), 'system', 1, 'active'),
  (UUID(), 'document_extract', '文档提取', 'method', 'data_access', 1, 1, 'json', '文档结构化提取（word/excel/pdf）', 1, NOW(), 'system', 1, 'active'),
  (UUID(), 'llm_extract', 'AI 提取', 'method', 'ai_judge', 1, 1, 'json', 'LLM 从文本提取结构化结果', 1, NOW(), 'system', 1, 'active')
ON DUPLICATE KEY UPDATE
  skill_name = VALUES(skill_name),
  skill_type = VALUES(skill_type),
  category = VALUES(category),
  side_effect = VALUES(side_effect),
  output_strict = VALUES(output_strict),
  return_type = VALUES(return_type),
  description = VALUES(description),
  is_active = 1,
  status = 'active';

-- 8.3 ai_node 登记（弱约束，待实现，先停用）
INSERT IGNORE INTO wf_skill
  (code, skill_code, skill_name, skill_type, category, side_effect, output_strict, return_type,
   description, is_active, create_date, creator, enable, status, remark)
VALUES
  (UUID(), 'ai_node', 'AI 节点', 'method', 'ai_generate', 1, 0, 'json',
   '提示词组织输入→LLM→通用输出 content/json/confidence（弱约束）', 0, NOW(), 'system', 1, 'disabled',
   'phase10 登记，AiNodeSkill 待实现');

-- ============================================================
-- 9. Skill 分类表（基础资料维护，页面左侧导航 + 面板分组，V1.2 §5.2）
-- ============================================================
CREATE TABLE IF NOT EXISTS wf_skill_category (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  code VARCHAR(100) NOT NULL,
  category_code VARCHAR(50) NOT NULL COMMENT '分类编码（与 wf_skill.category 对应）',
  category_name VARCHAR(100) NOT NULL COMMENT '分类名称',
  icon VARCHAR(50) NULL COMMENT '图标',
  color VARCHAR(20) NULL COMMENT '颜色',
  sort_order INT NOT NULL DEFAULT 0 COMMENT '排序',
  enable TINYINT(1) NOT NULL DEFAULT 1,
  create_id INT NULL, creator VARCHAR(50) NULL, create_date DATETIME NULL,
  modify_id INT NULL, modifier VARCHAR(50) NULL, modify_date DATETIME NULL,
  delete_id INT NULL, deleter VARCHAR(50) NULL, delete_time DATETIME NULL,
  status VARCHAR(50) NULL DEFAULT 'active', remark VARCHAR(500) NULL,
  UNIQUE KEY uk_skill_category_code (category_code)
) COMMENT='Skill 分类（基础资料维护：面板分组 + 页面左侧导航）';

-- 9.2 默认 5 分类种子（与 SkillBase.Category 声明对齐，幂等）
INSERT INTO wf_skill_category (code, category_code, category_name, icon, color, sort_order, enable, status, create_date, creator)
SELECT UUID(), 'data_access', '数据获取', 'Folder', '#409EFF', 1, 1, 'active', NOW(), 'system' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM wf_skill_category WHERE category_code = 'data_access');
INSERT INTO wf_skill_category (code, category_code, category_name, icon, color, sort_order, enable, status, create_date, creator)
SELECT UUID(), 'data_process', '数据处理', 'Cpu', '#67C23A', 2, 1, 'active', NOW(), 'system' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM wf_skill_category WHERE category_code = 'data_process');
INSERT INTO wf_skill_category (code, category_code, category_name, icon, color, sort_order, enable, status, create_date, creator)
SELECT UUID(), 'ai_judge', 'AI 判断', 'MagicStick', '#E6A23C', 3, 1, 'active', NOW(), 'system' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM wf_skill_category WHERE category_code = 'ai_judge');
INSERT INTO wf_skill_category (code, category_code, category_name, icon, color, sort_order, enable, status, create_date, creator)
SELECT UUID(), 'ai_generate', 'AI 生成', 'ChatDotRound', '#F56C6C', 4, 1, 'active', NOW(), 'system' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM wf_skill_category WHERE category_code = 'ai_generate');
INSERT INTO wf_skill_category (code, category_code, category_name, icon, color, sort_order, enable, status, create_date, creator)
SELECT UUID(), 'output', '输出', 'Document', '#909399', 5, 1, 'active', NOW(), 'system' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM wf_skill_category WHERE category_code = 'output');

-- ============================================================
-- 10. 菜单入口（Skill 管理，ParentId=304 体系认证平台）
-- ============================================================
INSERT INTO sys_menu (MenuName, ParentId, Url, OrderNo, MenuType, Icon, Description, Enable, CreateDate, Creator)
SELECT 'Skill 管理', 304, '/CertPlatform/SkillManage', 125, 0, 'Cpu', '工作流节点 Skill 配置（输入模板/输出契约/反射/API）', 1, NOW(), 'system'
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE Url = '/CertPlatform/SkillManage');

INSERT INTO sys_menu (MenuName, ParentId, Url, OrderNo, MenuType, Icon, Description, Enable, CreateDate, Creator)
SELECT 'NC 规则配置', 304, '/CertPlatform/NCConfig', 126, 0, 'EditPen', 'NC 规则工作流配置（三栏：机构树+检查项+画布）', 1, NOW(), 'system'
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE Url = '/CertPlatform/NCConfig');

-- ============================================================
-- 11. 清理辅助存储过程 + 验证
-- ============================================================
DROP PROCEDURE IF EXISTS `p10_add_column`;
DROP PROCEDURE IF EXISTS `p10_add_index`;

SELECT '=== wf_skill 生效 Skill ===' AS info;
SELECT skill_code, skill_name, skill_type, category, side_effect, output_strict, return_type, is_active
FROM wf_skill WHERE is_active = 1 ORDER BY skill_code;

SELECT '=== ent_table_extraction_result ===' AS info;
SHOW COLUMNS FROM ent_table_extraction_result LIKE 'table_code';
