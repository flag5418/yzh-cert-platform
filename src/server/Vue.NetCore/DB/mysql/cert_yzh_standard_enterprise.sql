-- ============================================================
-- YZH 标准企业 + B-08/B-09 唯一约束（提取结果落库功能前置）
-- 用途：标准目录文件提取结果挂在 YZH-STD-ENT 企业名下，供工作流验证
-- 日期：2026-08-15
-- 关联：docs/80-功能设计/提取结果落库-功能设计-V1.md §5.2 / §5.3
-- 说明：幂等，可重复执行；列名以实际表结构（snake_case）为准
-- ============================================================

-- 1. 创建 YZH 标准企业（虚拟企业，工作流按 enterprise_code 统一过滤）
INSERT INTO ent_enterprise (
  enterprise_no, code, org_code, name, short_name, credit_code,
  legal_person, address, cert_scope, contact_name, contact_phone, contact_email,
  status, archive_date,
  create_id, creator, create_date, enable
) VALUES (
  'YZH-STD-ENT',                          -- enterprise_no: 企业编码（存储路径用）
  'YZH-STD-ENT',                          -- code: 全局唯一编码（工作流过滤键）
  'YZH',                                  -- org_code: YZH 框架保留
  'YZH标准企业（标准目录数据）',            -- name: 明确标识虚拟性质
  'YZH标准',                               -- short_name
  'YZH-STD-000000',                        -- credit_code: 特殊格式，不与真实企业冲突
  '系统',                                  -- legal_person
  '系统内置',                              -- address
  '全部标准',                              -- cert_scope
  '系统',                                  -- contact_name
  '',                                     -- contact_phone
  '',                                     -- contact_email
  'active',                               -- status
  NULL,                                   -- archive_date
  1,                                      -- create_id: 超级管理员
  '系统初始化',                            -- creator
  NOW(),                                  -- create_date
  1                                       -- enable
)
ON DUPLICATE KEY UPDATE name = VALUES(name), short_name = VALUES(short_name);

-- 2. B-08/B-09 补齐审计列（实体 YZHBaseEntity 映射 create_id/creator/modify_id/modifier/modify_date，
--    缺失会导致 EF 查询报 Unknown column）
ALTER TABLE ent_extraction_result
  ADD COLUMN create_id INT NULL COMMENT '创建人ID',
  ADD COLUMN creator VARCHAR(64) NULL COMMENT '创建人',
  ADD COLUMN modify_id INT NULL COMMENT '修改人ID',
  ADD COLUMN modifier VARCHAR(64) NULL COMMENT '修改人',
  ADD COLUMN modify_date DATETIME NULL COMMENT '修改时间';

ALTER TABLE ent_table_extraction_result
  ADD COLUMN create_id INT NULL COMMENT '创建人ID',
  ADD COLUMN creator VARCHAR(64) NULL COMMENT '创建人',
  ADD COLUMN modify_id INT NULL COMMENT '修改人ID',
  ADD COLUMN modifier VARCHAR(64) NULL COMMENT '修改人',
  ADD COLUMN modify_date DATETIME NULL COMMENT '修改时间';

-- 3. 加宽 code 列：实际文件 FileCode 长度 ~110 字符（FL-FD-{目录}|Lxx|Sxxx|文件名），varchar(36) 不够
ALTER TABLE cert_doc_extraction_rule MODIFY COLUMN standard_file_code varchar(200) NULL COMMENT '规则键：实际文件 FileCode 或文件要求模板 Code';
ALTER TABLE ent_extraction_result MODIFY COLUMN file_code varchar(200) NULL, MODIFY COLUMN rule_code varchar(200) NULL, MODIFY COLUMN standard_file_code varchar(200) NULL;
ALTER TABLE ent_table_extraction_result MODIFY COLUMN file_code varchar(200) NULL, MODIFY COLUMN rule_code varchar(200) NULL, MODIFY COLUMN standard_file_code varchar(200) NULL;

-- 4. B-08 唯一约束：同一企业 + 同一文件 + 同一字段 最多一条（硬保障，先删后插的基础）
ALTER TABLE ent_extraction_result
  ADD UNIQUE KEY uk_ent_ext_ent_file_field (enterprise_code, file_code, field_code);

-- 5. B-09 唯一约束：同一企业 + 同一文件 + 同一表格序号 最多一条
ALTER TABLE ent_table_extraction_result
  ADD UNIQUE KEY uk_ent_tbl_ent_file_idx (enterprise_code, file_code, table_index);
