-- ============================================================
-- Prompt 模板表创建
-- 关联：YZH-AI引擎详细设计-V1.md F-03 WorkflowDefinition 扩展
-- 日期：2026-08-11
-- 幂等：是
-- ============================================================

CREATE TABLE IF NOT EXISTS wf_prompt_template (
  id              BIGINT       NOT NULL AUTO_INCREMENT COMMENT '主键',
  code            VARCHAR(100) NOT NULL                COMMENT '全局唯一编码',
  org_code        VARCHAR(50)  DEFAULT NULL            COMMENT '多租户组织编码',
  create_id       INT          DEFAULT NULL            COMMENT '创建人ID',
  creator         VARCHAR(50)  DEFAULT NULL            COMMENT '创建人姓名',
  create_date     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  modify_id       INT          DEFAULT NULL            COMMENT '修改人ID',
  modifier        VARCHAR(50)  DEFAULT NULL            COMMENT '修改人姓名',
  modify_date     DATETIME     DEFAULT NULL            COMMENT '修改时间',
  delete_id       INT          DEFAULT NULL            COMMENT '删除人ID',
  deleter         VARCHAR(50)  DEFAULT NULL            COMMENT '删除人姓名',
  delete_time     DATETIME     DEFAULT NULL            COMMENT '删除时间',
  status          VARCHAR(50)  DEFAULT 'active'        COMMENT '实体启用状态',
  enable          TINYINT(1)   DEFAULT 1               COMMENT '实体启用标记',
  sort            INT          DEFAULT 0               COMMENT '排序',
  remark          VARCHAR(500) DEFAULT NULL            COMMENT '备注',
  prompt_code     VARCHAR(100) NOT NULL                COMMENT '提示词编码',
  prompt_name     VARCHAR(200) NOT NULL                COMMENT '提示词名称',
  prompt_type     VARCHAR(50)  NOT NULL                COMMENT '类型：analyze/extract/verify/validate/report',
  skill_target    VARCHAR(50)  DEFAULT NULL            COMMENT '适用技能：word/excel/pdf/all',
  template        MEDIUMTEXT   DEFAULT NULL            COMMENT '提示词模板',
  description     TEXT         DEFAULT NULL            COMMENT '说明',
  version         INT          NOT NULL DEFAULT 1      COMMENT '版本号',
  is_active       TINYINT(1)   NOT NULL DEFAULT 1      COMMENT '是否当前生效',
  last_test_result TEXT        DEFAULT NULL            COMMENT '最后测试结果（JSON）',
  PRIMARY KEY (id),
  UNIQUE KEY uk_prompt_code (prompt_code),
  KEY idx_prompt_type (prompt_type),
  KEY idx_prompt_active (is_active, prompt_type)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Prompt模板表';

INSERT IGNORE INTO wf_prompt_template (
  code, prompt_code, prompt_name, prompt_type, skill_target,
  template, description, version, is_active
) VALUES
(UUID(), 'analyze_all_v1', 'AI 文档分析（通用）', 'analyze', 'all',
 '你是专业的文档分析助手。请分析以下文档的内容结构，推荐需要提取的字段和表格。\n\n输出要求：\n1. fields: 数组，每项含 field_code（英文驼峰）、field_name（中文名称）、field_type（string/number/date）、description\n2. tables: 数组，每项含 table_code（英文驼峰）、table_name（中文名称）、description、columns（列定义数组）\n\n只输出 JSON，不要任何解释文字。\n\n文档内容：\n---\n{{document_content}}\n---',
 '用于 AI 自动分析文档，推荐字段和表格定义。', 1, 1),
(UUID(), 'extract_all_v1', 'AI 文档提取（通用）', 'extract', 'all',
 '你是专业的文档信息提取助手。请从以下文档中提取指定字段和表格的值。\n\n【需要提取的字段】\n{{fields_json}}\n\n【需要提取的表格】\n{{tables_json}}\n\n输出要求（JSON格式）。\n\n文档内容：\n---\n{{document_content}}\n---',
 '用于 AI 从文档中提取字段值和表格数据。', 1, 1),
(UUID(), 'verify_all_v1', 'AI 提取验证（通用）', 'verify', 'all',
 '你是专业的文档信息提取助手。请严格按照以下要求从文档中提取信息。\n\n【提取要求】\n{{prompt}}\n\n【文档内容】\n---\n{{document_content}}\n---\n\n输出要求：直接输出 JSON，包含 fields[] 和 tables[]。',
 '用于验证 prompt 的提取效果。', 1, 1);

SELECT 'wf_prompt_template 创建完成' AS result;
