-- =====================================================
-- Phase 7: NC检查项配置 + 报告内容配置 DDL
-- 日期: 2026-08-16
-- 说明: 1. clause_code 改为 NOT NULL
--       2. 批量导入 ISO 9001:2015 标准条款数据
-- =====================================================

-- 1. clause_code 改为 NOT NULL（先处理空值）
UPDATE cert_validation_rule SET clause_code = 'unknown' WHERE clause_code IS NULL OR clause_code = '';
ALTER TABLE cert_validation_rule MODIFY COLUMN clause_code VARCHAR(36) NOT NULL COMMENT '关联条款编码(cert_iso_clause.code)';

-- 2. 检查 cert_iso_clause 表是否已有数据
SELECT COUNT(*) AS clause_count FROM cert_iso_clause;

-- 3. 批量导入 ISO 9001:2015 标准条款（如果表中无数据）
-- 注意：standard_code 需要与 cert_iso_standard 表中的 code 一致
-- 假设 ISO 9001 的 standard_code = 'ISO9001'

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT * FROM (
  SELECT UUID() AS code, 'ISO9001' AS standard_code, NULL AS parent_code, '4' AS clause_number, '组织环境' AS title, '理解组织及其环境、相关方需求、管理体系范围和过程' AS description, 1 AS sort_order, 1 AS enable, 'active' AS status, NOW() AS create_date
  UNION ALL SELECT UUID(), 'ISO9001', NULL, '5', '领导作用', '领导作用和承诺、方针、组织的角色职责和权限', 2, 1, 'active', NOW()
  UNION ALL SELECT UUID(), 'ISO9001', NULL, '6', '策划', '应对风险和机遇、质量目标、变更策划', 3, 1, 'active', NOW()
  UNION ALL SELECT UUID(), 'ISO9001', NULL, '7', '支持', '资源、能力、意识、沟通、成文信息', 4, 1, 'active', NOW()
  UNION ALL SELECT UUID(), 'ISO9001', NULL, '8', '运行', '运行策划和控制、产品和服务要求、设计开发、外部提供、生产服务提供、放行、不合格输出', 5, 1, 'active', NOW()
  UNION ALL SELECT UUID(), 'ISO9001', NULL, '9', '绩效评价', '监视测量分析评价、内部审核、管理评审', 6, 1, 'active', NOW()
  UNION ALL SELECT UUID(), 'ISO9001', NULL, '10', '改进', '不合格与纠正措施、持续改进', 7, 1, 'active', NOW()
) AS t
WHERE NOT EXISTS (SELECT 1 FROM cert_iso_clause WHERE standard_code = 'ISO9001' AND parent_code IS NULL);

-- 4. 导入二级条款（需要先获取一级条款的 code）
-- 4.1 组织环境（4.x）
INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '4.1', '理解组织及其环境', '组织应确定与其目标相关并影响其实现管理体系预期结果的能力的内外部问题', 1, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='4'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='4.1');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '4.2', '理解相关方的需求和期望', '组织应确定与管理体系有关的相关方及其要求', 2, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='4'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='4.2');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '4.3', '确定管理体系的范围', '组织应明确管理体系的边界和适用性', 3, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='4'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='4.3');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '4.4', '管理体系及其过程', '组织应按照标准要求建立、实施、保持和持续改进管理体系', 4, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='4'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='4.4');

-- 4.2 领导作用（5.x）
INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '5.1', '领导作用和承诺', '最高管理者应通过确保质量方针和目标制定、体系融入、资源提供等证实领导作用', 1, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='5'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='5.1');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '5.2', '方针', '组织应制定和实施质量方针', 2, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='5'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='5.2');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '5.3', '组织的角色、职责和权限', '组织应分配相关角色的职责和权限', 3, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='5'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='5.3');

-- 4.3 策划（6.x）
INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '6.1', '应对风险和机遇的措施', '组织应策划应对风险和机遇的措施', 1, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='6'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='6.1');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '6.2', '质量目标及其实现的策划', '组织应在相关职能和层次制定质量目标', 2, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='6'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='6.2');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '6.3', '变更的策划', '组织应控制有计划的变更', 3, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='6'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='6.3');

-- 4.4 支持（7.x）
INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '7.1', '资源', '组织应确定并提供建立、实施、保持和改进所需的资源', 1, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='7'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='7.1');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '7.2', '能力', '组织应确定所需的人员能力', 2, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='7'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='7.2');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '7.3', '意识', '组织应确保在其控制下工作的人员意识到质量方针和质量目标', 3, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='7'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='7.3');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '7.4', '沟通', '组织应确定与管理体系相关的内部和外部沟通', 4, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='7'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='7.4');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '7.5', '成文信息', '组织的管理体系应包括标准要求的成文信息', 5, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='7'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='7.5');

-- 4.5 运行（8.x）
INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '8.1', '运行的策划和控制', '组织应通过策划、实施和控制满足产品和服务要求所需的过程', 1, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='8'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='8.1');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '8.2', '产品和服务的要求', '组织应与顾客沟通并确定产品和服务要求', 2, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='8'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='8.2');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '8.3', '产品和服务的设计和开发', '组织应建立、实施和保持设计和开发过程', 3, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='8'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='8.3');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '8.4', '外部提供过程、产品和服务的控制', '组织应确保外部提供的过程、产品和服务符合要求', 4, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='8'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='8.4');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '8.5', '生产和服务的提供', '组织应在受控条件下进行生产和服务提供', 5, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='8'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='8.5');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '8.6', '产品和服务的放行', '组织应在适当阶段进行验证和放行', 6, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='8'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='8.6');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '8.7', '不合格输出的控制', '组织应确保对不符合要求的输出进行识别和控制', 7, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='8'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='8.7');

-- 4.6 绩效评价（9.x）
INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '9.1', '监视、测量、分析和评价', '组织应确定需要监视和测量什么', 1, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='9'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='9.1');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '9.2', '内部审核', '组织应按策划的时间间隔进行内部审核', 2, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='9'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='9.2');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '9.3', '管理评审', '最高管理者应按策划的时间间隔评审管理体系', 3, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='9'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='9.3');

-- 4.7 改进（10.x）
INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '10.1', '总则', '组织应确定和选择改进机会', 1, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='10'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='10.1');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '10.2', '不合格和纠正措施', '组织应识别和控制不合格并采取纠正措施', 2, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='10'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='10.2');

INSERT INTO cert_iso_clause (code, standard_code, parent_code, clause_number, title, description, sort_order, enable, status, create_date)
SELECT UUID(), 'ISO9001', p.code, '10.3', '持续改进', '组织应持续改进管理体系的适宜性、充分性和有效性', 3, 1, 'active', NOW()
FROM cert_iso_clause p WHERE p.standard_code='ISO9001' AND p.clause_number='10'
AND NOT EXISTS (SELECT 1 FROM cert_iso_clause c WHERE c.standard_code='ISO9001' AND c.clause_number='10.3');

-- 5. 验证数据
SELECT clause_number, title FROM cert_iso_clause WHERE standard_code = 'ISO9001' ORDER BY sort_order;
