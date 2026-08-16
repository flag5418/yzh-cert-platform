-- ============================================================
-- Phase 7: 工作流引擎 — 审核规则库 + 报告定义
-- 更新日期: 2026-08-16
-- 说明: 幂等设计，重复执行安全
-- ============================================================

-- ===== 1. cert_validation_rule 新增字段 =====
-- 使用 stored procedure 实现 IF NOT EXISTS 语义
DROP PROCEDURE IF EXISTS sp_add_vr_columns;
DELIMITER $$
CREATE PROCEDURE sp_add_vr_columns()
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                 WHERE table_schema=DATABASE() AND table_name='cert_validation_rule' AND column_name='org_code') THEN
    ALTER TABLE cert_validation_rule ADD COLUMN org_code VARCHAR(50) NULL COMMENT '所属机构编码（业务分组用）' AFTER code;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                 WHERE table_schema=DATABASE() AND table_name='cert_validation_rule' AND column_name='rule_name_en') THEN
    ALTER TABLE cert_validation_rule ADD COLUMN rule_name_en VARCHAR(200) NULL COMMENT '检查项英文名称' AFTER rule_name;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                 WHERE table_schema=DATABASE() AND table_name='cert_validation_rule' AND column_name='rule_json') THEN
    ALTER TABLE cert_validation_rule ADD COLUMN rule_json TEXT NULL COMMENT '工作流DAG JSON' AFTER workflow_code;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.statistics
                 WHERE table_schema=DATABASE() AND table_name='cert_validation_rule' AND index_name='idx_vr_org_standard_phase') THEN
    CREATE INDEX idx_vr_org_standard_phase ON cert_validation_rule(org_code, standard_code, phase_code);
  END IF;
END$$
DELIMITER ;
CALL sp_add_vr_columns();
DROP PROCEDURE IF EXISTS sp_add_vr_columns;

-- ===== 2. cert_report_template 新增 org_code =====
DROP PROCEDURE IF EXISTS sp_add_rpt_org;
DELIMITER $$
CREATE PROCEDURE sp_add_rpt_org()
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                 WHERE table_schema=DATABASE() AND table_name='cert_report_template' AND column_name='org_code') THEN
    ALTER TABLE cert_report_template ADD COLUMN org_code VARCHAR(50) NULL COMMENT '所属机构编码' AFTER code;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.statistics
                 WHERE table_schema=DATABASE() AND table_name='cert_report_template' AND index_name='idx_rpt_tmpl_org') THEN
    CREATE INDEX idx_rpt_tmpl_org ON cert_report_template(org_code, standard_code, phase_code);
  END IF;
END$$
DELIMITER ;
CALL sp_add_rpt_org();
DROP PROCEDURE IF EXISTS sp_add_rpt_org;

-- ===== 3. rpt_report_section 新增字段 =====
DROP PROCEDURE IF EXISTS sp_add_rs_columns;
DELIMITER $$
CREATE PROCEDURE sp_add_rs_columns()
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                 WHERE table_schema=DATABASE() AND table_name='rpt_report_section' AND column_name='section_name_en') THEN
    ALTER TABLE rpt_report_section ADD COLUMN section_name_en VARCHAR(200) NULL COMMENT '章节英文名称' AFTER section_name;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                 WHERE table_schema=DATABASE() AND table_name='rpt_report_section' AND column_name='section_json') THEN
    ALTER TABLE rpt_report_section ADD COLUMN section_json TEXT NULL COMMENT '章节工作流DAG JSON' AFTER workflow_code;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                 WHERE table_schema=DATABASE() AND table_name='rpt_report_section' AND column_name='remark') THEN
    ALTER TABLE rpt_report_section ADD COLUMN remark VARCHAR(500) NULL COMMENT '章节备注' AFTER section_json;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                 WHERE table_schema=DATABASE() AND table_name='rpt_report_section' AND column_name='is_active') THEN
    ALTER TABLE rpt_report_section ADD COLUMN is_active TINYINT(1) DEFAULT 1 COMMENT '是否启用' AFTER remark;
  END IF;
END$$
DELIMITER ;
CALL sp_add_rs_columns();
DROP PROCEDURE IF EXISTS sp_add_rs_columns;

-- ===== 4. 插入默认 Skill 数据（wf_skill）=====
INSERT IGNORE INTO wf_skill (code, SkillCode, SkillName, SkillType, InputSchema, OutputSchema, EndpointConfig, Description, IsActive, CreateDate, Creator, Enable, Status, Remark)
VALUES
  (UUID(), 'get_field', '获取字段值', 'data_access',
   '{"type":"object","required":["label_tag"],"properties":{"label_tag":{"type":"string","description":"F-02标签，如[ISO9001_一监_管理评审记录_评审日期]"}}}',
   '{"type":"object","required":["value"],"properties":{"value":{"type":"string"},"confidence":{"type":"number"}}}',
   '{}', '从B-08提取结果中按label_tag读取字段值', 1, NOW(), 'system', 1, 'active', NULL),

  (UUID(), 'get_table', '获取表格数据', 'data_access',
   '{"type":"object","required":["table_code"],"properties":{"table_code":{"type":"string"},"table_index":{"type":"integer","description":"表格序号，默认最新一条"}}}',
   '{"type":"object","required":["rows","table_code"],"properties":{"rows":{"type":"array"},"table_code":{"type":"string"},"confidence":{"type":"number"}}}',
   '{}', '从B-09提取结果中按table_code读取表格数据', 1, NOW(), 'system', 1, 'active', NULL),

  (UUID(), 'compare', '值比较', 'data_process',
   '{"type":"object","required":["value","operator","threshold"],"properties":{"value":{"type":["string","number"]},"operator":{"type":"string","enum":["equals","not_equals","gt","gte","lt","lte","truthy"]},"threshold":{"type":["number","string"]}}}',
   '{"type":"object","required":["result"],"properties":{"result":{"type":"boolean"}}}',
   '{}', '比较两个值，返回布尔结果', 1, NOW(), 'system', 1, 'active', NULL),

  (UUID(), 'date_diff', '日期差', 'data_process',
   '{"type":"object","required":["date_a","date_b","unit"],"properties":{"date_a":{"type":"string"},"date_b":{"type":"string"},"unit":{"type":"string","enum":["day","month","year"]}}}',
   '{"type":"object","required":["diff"],"properties":{"diff":{"type":"number"},"unit":{"type":"string"}}}',
   '{}', '计算两个日期的差值', 1, NOW(), 'system', 1, 'active', NULL),

  (UUID(), 'text_merge', '文本合并', 'data_process',
   '{"type":"object","required":["parts","joiner"],"properties":{"parts":{"type":"array","items":{"type":"string"}},"joiner":{"type":"string"}}}',
   '{"type":"object","required":["result"],"properties":{"result":{"type":"string"}}}',
   '{}', '将多个文本片段按连接符合并', 1, NOW(), 'system', 1, 'active', NULL),

  (UUID(), 'llm_judge', 'AI语义判断', 'ai_judge',
   '{"type":"object","required":["prompt","context"],"properties":{"prompt":{"type":"string"},"context":{"type":"string","description":"上下文数据JSON字符串"}}}',
   '{"type":"object","required":["decision","confidence"],"properties":{"decision":{"type":"string"},"confidence":{"type":"number","minimum":0,"maximum":1},"reasoning":{"type":"string"}}}',
   '{"llm":{"provider":"qwen","model":"qwen-turbo","temperature":0.1,"max_tokens":1024}}', '调用LLM进行语义判断，返回决策+置信度', 1, NOW(), 'system', 1, 'active', NULL),

  (UUID(), 'llm_generate', 'AI内容生成', 'ai_generate',
   '{"type":"object","required":["prompt","context"],"properties":{"prompt":{"type":"string"},"context":{"type":"string"}}}',
   '{"type":"object","required":["content"],"properties":{"content":{"type":"string"},"confidence":{"type":"number"}}}',
   '{"llm":{"provider":"qwen","model":"qwen-plus","temperature":0.3,"max_tokens":2048}}', '调用LLM生成文本内容（报告章节等）', 1, NOW(), 'system', 1, 'active', NULL),

  (UUID(), 'create_nc', '创建不符合项', 'output',
   '{"type":"object","required":["severity","description"],"properties":{"severity":{"type":"string","enum":["minor","major","observation"]},"description":{"type":"string"},"clause_code":{"type":"string"}}}',
   '{"type":"object","required":["nc_severity","nc_description"],"properties":{"nc_severity":{"type":"string"},"nc_description":{"type":"string"},"evidence_refs":{"type":"array"}}}',
   '{}', '创建工作流判定结果：NC不符合项（自动写入audit_nonconformity）', 1, NOW(), 'system', 1, 'active', NULL),

  (UUID(), 'save_result', '保存审核结果', 'output',
   '{"type":"object","required":["result"],"properties":{"result":{"type":"object","description":"审核结论JSON"}}}',
   '{"type":"object","required":["saved"],"properties":{"saved":{"type":"boolean"},"finding_id":{"type":"string"}}}',
   '{}', '保存审核发现结果到audit_finding', 1, NOW(), 'system', 1, 'active', NULL),

  (UUID(), 'assemble_text', '组装报告文本', 'output',
   '{"type":"object","required":["template","data"],"properties":{"template":{"type":"string"},"data":{"type":"object"}}}',
   '{"type":"object","required":["content"],"properties":{"content":{"type":"string"}}}',
   '{}', '按模板组装报告章节文本内容', 1, NOW(), 'system', 1, 'active', NULL);

-- ===== 5. NC判定等级字典（sys_dictionary 表结构不同，手动维护或执行补充SQL）=====
-- DicNo = dict_code, DicName = dict_name, ParentId = parent_code, OrderNo = sort_order
-- 如需插入：
-- INSERT INTO sys_dictionary (DicNo, DicName, ParentId, OrderNo, Enable, CreateDate) VALUES
--   ('NC_SEVERITY', 'NC判定等级', 0, 1, 1, NOW()),
--   ('NC_SEVERITY_minor', '轻微不符合', (SELECT Dic_ID FROM sys_dictionary WHERE DicNo='NC_SEVERITY'), 1, 1, NOW()),
--   ('NC_SEVERITY_major', '严重不符合', (SELECT Dic_ID FROM sys_dictionary WHERE DicNo='NC_SEVERITY'), 2, 1, NOW()),
--   ('NC_SEVERITY_observation', '观察项', (SELECT Dic_ID FROM sys_dictionary WHERE DicNo='NC_SEVERITY'), 3, 1, NOW()),
--   ('NC_SEVERITY_conformant', '符合', (SELECT Dic_ID FROM sys_dictionary WHERE DicNo='NC_SEVERITY'), 4, 1, NOW());
