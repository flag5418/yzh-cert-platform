-- ============================================================
-- 体系认证平台 - Phase 2: 测试数据初始化脚本
-- 说明: 创建第一个测试机构、标准、示例企业及完整业务流程数据
-- 案例来源：河北雄安尚龙医疗科技有限公司 ISO 13485 认证
-- 执行顺序: 在 Phase 2 数据字典脚本执行后运行本脚本
-- ============================================================

USE `yzh_cert_platform`;

-- ============================================================
-- Step 1: 创建测试认证机构（CB001）
-- 基于案例：河北雄安尚龙认证有限公司
-- ============================================================

INSERT INTO `cert_certification_body` (`code`, `name`, `short_name`, `cb_code`, `status`, `contact_name`, `contact_phone`, `notes`, `create_by`, `create_time`)
VALUES (
    UUID(),
    '河北雄安尚龙认证有限公司',
    '尚龙认证',
    'CB001',
    'active',
    '张主任',
    '0312-12345678',
    'ISO 13485 医疗器械质量管理体系认证机构（测试用）',
    1,
    NOW()
);

SET @cb_code = (SELECT code FROM cert_certification_body WHERE cb_code = 'CB001' LIMIT 1);

SELECT CONCAT('✅ 测试认证机构创建成功，Code=', @cb_code) AS status;

-- ============================================================
-- Step 2: 创建 ISO 13485:2016 标准
-- 基于案例：医疗器械质量管理体系
-- ============================================================

INSERT INTO `cert_iso_standard` (`code`, `cb_code`, `standard_code`, `standard_name`, `version_year`, `status`, `notes`, `create_by`, `create_time`)
VALUES (
    UUID(),
    @cb_code,
    'ISO 13485:2016',
    '医疗器械 质量管理体系 用于法规的要求',
    2016,
    'implemented',
    '基于 GB/T 42061-2022（等同采用 ISO 13485:2016）',
    1,
    NOW()
);

SET @standard_code = (SELECT code FROM cert_iso_standard WHERE standard_code = 'ISO 13485:2016' LIMIT 1);

SELECT CONCAT('✅ ISO 13485:2016 标准创建成功，Code=', @standard_code) AS status;

-- ============================================================
-- Step 3: 创建 ISO 13485 核心条款（简化版，用于演示）
-- 只创建一级和二级条款，不展开到三级
-- ============================================================

-- 清空可能存在的旧数据
DELETE FROM cert_iso_clause WHERE standard_code = @standard_code;

-- 第4章 组织环境
INSERT INTO `cert_iso_clause` (`code`, `standard_code`, `parent_code`, `clause_number`, `title`, `description`, `sort_order`) VALUES
(UUID(), @standard_code, NULL, '4', '组织环境', '理解组织及其环境', 10),
(UUID(), @standard_code, NULL, '4.1', '理解组织及其环境', '组织应确定与其宗旨和战略方向相关并影响其实现质量管理体系预期结果的能力的各种外部和内部问题', 11),
(UUID(), @standard_code, NULL, '4.2', '理解相关方的需求和期望', '组织应确定与质量管理体系有关的相关方及相关方的要求', 12),
(UUID(), @standard_code, NULL, '4.3', '确定质量管理体系的范围', '组织应确定质量管理体系的边界和适用性', 13),
(UUID(), @standard_code, NULL, '4.4', '质量管理体系及其过程', '组织应按照本标准的要求建立、实施、保持和持续改进质量管理体系', 14);

-- 第5章 领导作用
INSERT INTO `cert_iso_clause` (`code`, `standard_code`, `parent_code`, `clause_number`, `title`, `description`, `sort_order`) VALUES
(UUID(), @standard_code, NULL, '5', '领导作用', '', 20),
(UUID(), @standard_code, NULL, '5.1', '领导作用和承诺', '最高管理者应通过以下活动证实其对质量管理体系的领导作用和承诺', 21),
(UUID(), @standard_code, NULL, '5.2', '质量方针', '最高管理者应制定质量方针', 22),
(UUID(), @standard_code, NULL, '5.3', '组织的岗位、职责和权限', '最高管理者应确保组织内相关岗位的职责、权限得到规定和沟通', 23);

-- 第6章 策划
INSERT INTO `cert_iso_clause` (`code`, `standard_code`, `parent_code`, `clause_number`, `title`, `description`, `sort_order`) VALUES
(UUID(), @standard_code, NULL, '6', '策划', '', 30),
(UUID(), @standard_code, NULL, '6.1', '应对风险和机遇的措施', '组织应策划和实施应对风险和机遇的措施', 31),
(UUID(), @standard_code, NULL, '6.2', '质量目标及其实现的策划', '组织应在相关职能、层次和质量管理体系所需的过程建立质量目标', 32),
(UUID(), @standard_code, NULL, '6.3', '变更的策划', '当组织确定需要对质量管理体系进行变更时', 33);

-- 第7章 支持
INSERT INTO `cert_iso_clause` (`code`, `standard_code`, `parent_code`, `clause_number`, `title`, `description`, `sort_order`) VALUES
(UUID(), @standard_code, NULL, '7', '支持', '', 40),
(UUID(), @standard_code, NULL, '7.1', '资源', '组织应确定并提供所需的资源', 41),
(UUID(), @standard_code, NULL, '7.1.2', '人员（人力资源）', '组织应确定并配备所需的人员', 42),
(UUID(), @standard_code, NULL, '7.1.3', '基础设施', '组织应确定、提供和维护所需的基础设施', 43),
(UUID(), @standard_code, NULL, '7.1.6', '知识管理', '组织应确定质量管理体系运行所需的知识', 44),
(UUID(), @standard_code, NULL, '7.2', '能力', '组织应：a) 确定在其控制下工作的人员所需具备的能力', 45),
(UUID(), @standard_code, NULL, '7.3', '意识', '组织应确保在其控制下工作人员意识到：a) 质量方针', 46),
(UUID(), @standard_code, NULL, '7.4', '沟通', '组织应确定与质量管理体系有关的内部和外部沟通', 47),
(UUID(), @standard_code, NULL, '7.5', '文件化信息', '组织的质量管理体系应包括：a) 本标准要求的文件化信息', 48);

-- 第8章 运行
INSERT INTO `cert_iso_clause` (`code`, `standard_code`, `parent_code`, `clause_number`, `title`, `description`, `sort_order`) VALUES
(UUID(), @standard_code, NULL, '8', '运行', '', 50),
(UUID(), @standard_code, NULL, '8.1', '运行的策划和控制', '组织应通过实施以下过程对产品和服务的要求进行策划和控制', 51),
(UUID(), @standard_code, NULL, '8.2', '产品和服务的要求', '与顾客有关的过程', 52),
(UUID(), @standard_code, NULL, '8.2.2', '与产品和服务有关的要求的确定', '在确定向顾客提供的产品和服务的要求时', 53),
(UUID(), @standard_code, NULL, '8.2.3', '与产品和服务有关的要求的评审', '组织应确保有能力满足向顾客提供的产品和服务的要求', 54),
(UUID(), @standard_code, NULL, '8.3', '设计和开发', '医疗器械设计和开发的特殊要求', 55),
(UUID(), @standard_code, NULL, '8.4', '外部提供的过程、产品和服务的控制', '组织应确保外部提供的过程、产品和服务符合要求', 56),
(UUID(), @standard_code, NULL, '8.5', '生产和服务提供', '生产和服务提供的控制', 57),
(UUID(), @standard_code, NULL, '8.5.1', '生产和服务提供的控制', '组织应在受控条件下实施生产和服务的提供', 58),
(UUID(), @standard_code, NULL, '8.5.6', '更改控制', '组织应对生产或服务提供的更改进行评审和控制', 59),
(UUID(), @standard_code, NULL, '8.6', '产品和服务的放行', '组织应在适当阶段实施策划的安排，以验证产品和服务的要求已得到满足', 60),
(UUID(), @standard_code, NULL, '8.7', '不合格输出的控制', '组织应确保对不符合要求的输出进行识别和控制', 61);

-- 第9章 绩效评价
INSERT INTO `cert_iso_clause` (`code`, `standard_code`, `parent_code`, `clause_number`, `title`, `description`, `sort_order`) VALUES
(UUID(), @standard_code, NULL, '9', '绩效评价', '', 70),
(UUID(), @standard_code, NULL, '9.1', '监视、测量、分析和评价', '组织应确定：a) 需要监视和测量的对象', 71),
(UUID(), @standard_code, NULL, '9.2', '内部审核', '组织应按照策划的时间间隔进行内部审核', 72),
(UUID(), @standard_code, NULL, '9.3', '管理评审', '最高管理者应按照策划的时间间隔对组织的质量管理体系进行评审', 73);

-- 第10章 改进
INSERT INTO `cert_iso_clause` (`code`, `standard_code`, `parent_code`, `clause_number`, `title`, `description`, `sort_order`) VALUES
(UUID(), @standard_code, NULL, '10', '改进', '', 80),
(UUID(), @standard_code, NULL, '10.1', '总则', '组织应确定和选择改进机会，并采取必要措施', 81),
(UUID(), @standard_code, NULL, '10.2', '不合格和纠正措施', '当出现不合格时，组织应：a) 对不合格做出响应', 82),
(UUID(), @standard_code, NULL, '10.3', '持续改进', '组织应持续改进质量管理体系的适宜性、充分性和有效性', 83);

SELECT CONCAT('✅ ISO 13485 条款创建完成，共 ', COUNT(*), ' 个条款') AS status 
FROM cert_iso_clause WHERE standard_code = @standard_code;

-- ============================================================
-- Step 4: 创建示例企业（申请方）
-- 基于案例：河北雄安尚龙医疗科技有限公司
-- ============================================================

-- 注意：企业表结构需要根据实际情况调整，这里使用通用字段
-- 如果还没有企业表，先创建一个简化的

CREATE TABLE IF NOT EXISTS `cert_enterprise` (
    `id` BIGINT(20) NOT NULL AUTO_INCREMENT,
    `code` CHAR(36) NOT NULL,
    `enterprise_name` VARCHAR(200) NOT NULL,
    `short_name` VARCHAR(100) DEFAULT NULL,
    `unified_social_credit_code` VARCHAR(50) NOT NULL COMMENT '统一社会信用代码',
    `legal_person` VARCHAR(100) DEFAULT NULL,
    `contact_person` VARCHAR(100) DEFAULT NULL,
    `contact_phone` VARCHAR(20) DEFAULT NULL,
    `contact_email` VARCHAR(200) DEFAULT NULL,
    `province` VARCHAR(50) DEFAULT NULL,
    `city` VARCHAR(50) DEFAULT NULL,
    `address` VARCHAR(500) DEFAULT NULL,
    `industry_type` VARCHAR(100) DEFAULT NULL COMMENT '行业类型',
    `employee_count` INT(11) DEFAULT NULL COMMENT '员工人数',
    `status` TINYINT NOT NULL DEFAULT 0,
    `org_code` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码',
    `notes` TEXT,
    `create_by` BIGINT(20) DEFAULT NULL,
    `create_time` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `update_by` BIGINT(20) DEFAULT NULL,
    `update_time` DATETIME DEFAULT NULL,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_credit_code` (`unified_social_credit_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='企业信息表';

INSERT INTO `cert_enterprise` (`code`, `enterprise_name`, `short_name`, `unified_social_credit_code`, `legal_person`, `contact_person`, `contact_phone`, `province`, `city`, `address`, `industry_type`, `employee_count`, `status`, `org_code`, `notes`, `create_by`, `create_time`)
VALUES (
    UUID(),
    '河北雄安尚龙医疗科技有限公司',
    '尚龙医疗',
    '91133200MA0Axxxxxx',
    '张三',
    '李四',
    '13800138000',
    '河北省',
    '雄安新区',
    '河北省雄安新区容城县科技园区X号',
    '医疗器械制造',
    150,
    1,
    'CB001',
    '主要产品：真空拔罐器等一类医疗器械（案例企业）',
    1,
    NOW()
);

SET @ent_code = (SELECT code FROM cert_enterprise WHERE enterprise_name = '河北雄安尚龙医疗科技有限公司' LIMIT 1);

SELECT CONCAT('✅ 示例企业创建成功，Code=', @ent_code) AS status;

-- ============================================================
-- Step 5: 创建示例认证申请
-- ============================================================

CREATE TABLE IF NOT EXISTS `cert_application` (
    `id` BIGINT(20) NOT NULL AUTO_INCREMENT,
    `code` CHAR(36) NOT NULL,
    `application_no` VARCHAR(50) NOT NULL COMMENT '申请编号',
    `cb_code` VARCHAR(36) NOT NULL COMMENT '认证机构编码',
    `standard_code` VARCHAR(36) NOT NULL COMMENT '标准编码',
    `enterprise_code` VARCHAR(36) NOT NULL COMMENT '企业编码',
    `cert_type` VARCHAR(20) NOT NULL COMMENT '认证类型(QMS/EMS等)',
    `scope_text` TEXT COMMENT '认证范围描述',
    `status` VARCHAR(30) NOT NULL DEFAULT 'draft' COMMENT '申请状态',
    `submit_time` DATETIME DEFAULT NULL COMMENT '提交时间',
    `accept_time` DATETIME DEFAULT NULL COMMENT '受理时间',
    `complete_time` DATETIME DEFAULT NULL COMMENT '完成时间',
    `notes` TEXT,
    `create_by` BIGINT(20) DEFAULT NULL,
    `create_time` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `update_by` BIGINT(20) DEFAULT NULL,
    `update_time` DATETIME DEFAULT NULL,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_application_no` (`application_no`),
    KEY `idx_cb_code` (`cb_code`),
    KEY `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='认证申请表';

-- 生成申请编号：年份 + 机构码 + 流水号
SET @app_no = CONCAT(DATE_FORMAT(NOW(), '%Y'), '-CB001-', LPAD(FLOOR(RAND() * 10000), 4, '0'));

INSERT INTO `cert_application` (`code`, `application_no`, `cb_code`, `standard_code`, `enterprise_code`, `cert_type`, `scope_text`, `status`, `notes`, `create_by`, `create_time`)
VALUES (
    UUID(),
    @app_no,
    @cb_code,
    @standard_code,
    @ent_code,
    'QMS',
    '真空拔罐器的设计开发、生产和服务提供过程的质量管理体系认证（符合GB/T 42061-2022 / ISO 13485:2016标准要求）',
    'submitted',
    '示例申请：用于演示完整的审核流程',
    1,
    NOW()
);

SET @app_code = (SELECT code FROM cert_application WHERE application_no = @app_no LIMIT 1);

SELECT CONCAT('✅ 示例认证申请创建成功，申请编号=', @app_no) AS status;

-- ============================================================
-- Step 6: 创建审核任务（对应5个阶段）
-- ============================================================

CREATE TABLE IF NOT EXISTS `audit_project` (
    `id` BIGINT(20) NOT NULL AUTO_INCREMENT,
    `code` CHAR(36) NOT NULL,
    `project_no` VARCHAR(50) NOT NULL COMMENT '项目编号',
    `application_code` VARCHAR(36) NOT NULL COMMENT '关联申请编码',
    `current_phase` VARCHAR(30) DEFAULT 'application_review' COMMENT '当前阶段',
    `project_manager_id` BIGINT(20) DEFAULT NULL COMMENT '项目经理ID',
    `planned_start_date` DATE DEFAULT NULL COMMENT '计划开始日期',
    `planned_end_date` DATE DEFAULT NULL COMMENT '计划结束日期',
    `actual_end_date` DATE DEFAULT NULL COMMENT '实际结束日期',
    `status` VARCHAR(20) NOT NULL DEFAULT 'active' COMMENT '项目状态',
    `notes` TEXT,
    `create_by` BIGINT(20) DEFAULT NULL,
    `create_time` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `update_by` BIGINT(20) DEFAULT NULL,
    `update_time` DATETIME DEFAULT NULL,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    UNIQUE KEY `uk_project_no` (`project_no`),
    KEY `idx_application_code` (`application_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='审核项目表';

-- 创建审核项目
SET @project_no = CONCAT('PRJ-', DATE_FORMAT(NOW(), '%Y%m'), '-', LPAD(FLOOR(RAND() * 1000), 3, '0'));

INSERT INTO `audit_project` (`code`, `project_no`, `application_code`, `current_phase`, `project_manager_id`, `planned_start_date`, `planned_end_date`, `status`, `notes`, `create_by`, `create_time`)
VALUES (
    UUID(),
    @project_no,
    @app_code,
    'application_review',
    1, -- 使用超级管理员作为默认项目经理
    CURDATE(),
    DATE_ADD(CURDATE(), INTERVAL 90 DAY),
    'active',
    '示例项目：河北雄安尚龙医疗 ISO 13485 初次认证',
    1,
    NOW()
);

SET @project_code = (SELECT code FROM audit_project WHERE project_no = @project_no LIMIT 1);

SELECT CONCAT('✅ 审核项目创建成功，项目编号=', @project_no) AS status;

-- ============================================================
-- Step 7: 创建各阶段的审核任务
-- ============================================================

-- 阶段1：申请受理
INSERT INTO `audit_task` (`code`, `phase_code`, `task_number`, `auditor_id`, `status`, `planned_date`, `audit_scope`, `notes`, `create_by`, `create_time`)
VALUES (
    UUID(),
    @project_code,
    CONCAT(@project_no, '-T01'),
    1,
    'pending_assignment',
    DATE_ADD(CURDATE(), INTERVAL 3 DAY),
    '检查申请材料完整性、确认认证范围、核实企业资质',
    '阶段1：申请受理',
    1,
    NOW()
);

-- 阶段2：文件评审
INSERT INTO `audit_task` (`code`, `phase_code`, `task_number`, `auditor_id`, `status`, `planned_date`, `audit_scope`, `notes`, `create_by`, `create_time`)
VALUES (
    UUID(),
    @project_code,
    CONCAT(@project_no, '-T02'),
    1,
    'pending_assignment',
    DATE_ADD(CURDATE(), INTERVAL 15 DAY),
    '评审质量手册、程序文件、记录表格等体系文件',
    '阶段2：文件评审',
    1,
    NOW()
);

-- 阶段3：一阶段审核
INSERT INTO `audit_task` (`code`, `phase_code`, `task_number`, `auditor_id`, `status`, `planned_date`, `audit_scope`, `notes`, `create_by`, `create_time`)
VALUES (
    UUID(),
    @project_code,
    CONCAT(@project_no, '-T03'),
    1,
    'pending_assignment',
    DATE_ADD(CURDATE(), INTERVAL 35 DAY),
    '现场审核：了解体系运行情况、确认审核计划可行性',
    '阶段3：一阶段审核（现场）',
    1,
    NOW()
);

-- 阶段4：二阶段审核
INSERT INTO `audit_task` (`code`, `phase_code`, `task_number`, `auditor_id`, `status`, `planned_date`, `audit_scope`, `notes`, `create_by`, `create_time`)
VALUES (
    UUID(),
    @project_code,
    CONCAT(@project_no, '-T04'),
    1,
    'pending_assignment',
    DATE_ADD(CURDATE(), INTERVAL 65 DAY),
    '现场审核：全面评价体系符合性、有效性',
    '阶段4：二阶段审核（现场）',
    1,
    NOW()
);

-- 阶段5：认证决定
INSERT INTO `audit_task` (`code`, `phase_code`, `task_number`, `auditor_id`, `status`, `planned_date`, `audit_scope`, `notes`, `create_by`, `create_time`)
VALUES (
    UUID(),
    @project_code,
    CONCAT(@project_no, '-T05'),
    1,
    'pending_assignment',
    DATE_ADD(CURDATE(), INTERVAL 80 DAY),
    '综合评审所有审核资料，做出认证决定',
    '阶段5：认证决定',
    1,
    NOW()
);

SELECT CONCAT('✅ 审核任务创建完成，共 5 个阶段') AS status;

-- ============================================================
-- Step 8: 更新申请状态为已提交
-- ============================================================

UPDATE `cert_application`
SET `status` = 'submitted',
    `submit_time` = NOW()
WHERE `code` = @app_code;

SELECT '✅ 申请状态更新为"已提交"' AS status;

-- ============================================================
-- 总结
-- ============================================================

SELECT 
    '🎉 测试数据初始化完成！' AS summary,
    (SELECT COUNT(*) FROM cert_certification_body WHERE cb_code = 'CB001') AS '认证机构数',
    (SELECT COUNT(*) FROM cert_iso_standard WHERE standard_code = 'ISO 13485:2016') AS '标准数',
    (SELECT COUNT(*) FROM cert_iso_clause WHERE standard_code = @standard_code) AS '条款数',
    (SELECT COUNT(*) FROM cert_enterprise WHERE enterprise_name LIKE '%尚龙%') AS '企业数',
    (SELECT COUNT(*) FROM cert_application WHERE application_no = @app_no) AS '申请数',
    (SELECT COUNT(*) FROM audit_task WHERE phase_code = @project_code) AS '审核任务数';

SELECT '
═══════════════════════════════════════
📋 测试数据清单：
═══════════════════════════════════════
1️⃣  认证机构：河北雄安尚龙认证有限公司 (CB001)
2️⃣  认证标准：ISO 13485:2016（43个核心条款）
3️⃣  示例企业：河北雄安尚龙医疗科技有限公司
4️⃣  认证申请：1份（已提交状态）
5️⃣  审核项目：1个（含5个阶段任务）

💡 下一步建议：
   → 实现 Vol view-grid 页面展示以上数据
   → 先跑通"机构管理→标准管理→申请列表"流程
   → 再逐步完善审核任务的详细功能
═══════════════════════════════════════
' AS next_steps;
