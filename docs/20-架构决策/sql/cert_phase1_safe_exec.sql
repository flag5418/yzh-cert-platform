-- ============================================================
-- 体系认证平台 - Phase 1: 安全执行脚本
-- 说明: 使用存储过程实现幂等操作，避免重复执行错误
-- ============================================================

USE `yzh_cert_platform`;

DELIMITER //

-- 创建存储过程：安全添加列（如果列不存在）
CREATE PROCEDURE IF NOT EXISTS safe_add_column(
    IN p_table_name VARCHAR(100),
    IN p_column_name VARCHAR(100),
    IN p_column_def TEXT,
    IN p_comment VARCHAR(500)
)
BEGIN
    DECLARE col_count INT;
    SELECT COUNT(*) INTO col_count 
    FROM information_schema.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() 
      AND TABLE_NAME = p_table_name 
      AND COLUMN_NAME = p_column_name;
    
    IF col_count = 0 THEN
        SET @sql = CONCAT('ALTER TABLE `', p_table_name, '` ADD COLUMN `', p_column_name, '` ', p_column_def, " COMMENT '", p_comment, "'");
        PREPARE stmt FROM @sql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END //

-- 创建存储过程：安全添加索引（如果索引不存在）
CREATE PROCEDURE IF NOT EXISTS safe_add_index(
    IN p_table_name VARCHAR(100),
    IN p_index_name VARCHAR(100),
    IN p_index_columns TEXT
)
BEGIN
    DECLARE idx_count INT;
    SELECT COUNT(*) INTO idx_count 
    FROM information_schema.STATISTICS 
    WHERE TABLE_SCHEMA = DATABASE() 
      AND TABLE_NAME = p_table_name 
      AND INDEX_NAME = p_index_name;
    
    IF idx_count = 0 THEN
        SET @sql = CONCAT('ALTER TABLE `', p_table_name, '` ADD INDEX `', p_index_name, '` (', p_index_columns, ')');
        PREPARE stmt FROM @sql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END //

DELIMITER ;

-- ============================================================
-- 第一部分：Sys_User 表结构扩展（使用安全方法）
-- ============================================================
CALL safe_add_column('Sys_User', 'UserType', 'TINYINT NOT NULL DEFAULT 10', '用户类型：1=超级管理员, 10=总管理员, 13=运维人员, 14=配置人员, 15=质量专员, 20=审核管理员, 21=审核组长, 22=普通审核员, 30=企业账号');
CALL safe_add_column('Sys_User', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '机构编码（多租户隔离），NULL表示平台管理层');
CALL safe_add_column('Sys_User', 'OrgId', 'BIGINT(20) DEFAULT NULL', '机构ID，关联cert_org_config.id');
CALL safe_add_column('Sys_User', 'ParentUserId', 'INT(11) DEFAULT NULL', '上级用户ID，用于企业子账号或审核员层级');

CALL safe_add_index('Sys_User', 'idx_sys_user_org_code', 'OrgCode');
CALL safe_add_index('Sys_User', 'idx_sys_user_user_type', 'UserType');

SELECT '✅ Sys_User 表扩展完成' AS status;

-- ============================================================
-- 第二部分：创建新表（IF NOT EXISTS）
-- ============================================================

-- 创建机构配置表
CREATE TABLE IF NOT EXISTS `cert_org_config` (
    `id` BIGINT(20) NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` CHAR(36) NOT NULL COMMENT 'GUID编码，用于关联',
    `org_code` VARCHAR(50) NOT NULL COMMENT '机构唯一编码',
    `org_name` VARCHAR(200) NOT NULL COMMENT '机构全称',
    `org_short_name` VARCHAR(100) DEFAULT NULL COMMENT '机构简称',
    `org_type` TINYINT NOT NULL DEFAULT 1 COMMENT '机构类型：1=认证机构, 2=咨询公司, 3=检测机构',
    `registration_no` VARCHAR(100) DEFAULT NULL COMMENT '认证机构批准号',
    `legal_person` VARCHAR(100) DEFAULT NULL COMMENT '法定代表人',
    `contact_phone` VARCHAR(20) DEFAULT NULL COMMENT '联系电话',
    `contact_email` VARCHAR(200) DEFAULT NULL COMMENT '联系邮箱',
    `address` VARCHAR(500) DEFAULT NULL COMMENT '详细地址',
    `logo_url` VARCHAR(500) DEFAULT NULL COMMENT '机构Logo URL',
    `status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态：0=停用, 1=正常, 2=待审核',
    `scope_text` TEXT COMMENT '认证范围说明',
    `cert_scope_json` JSON COMMENT '认证范围详细数据(JSON)',
    `theme_config` JSON COMMENT '前端主题配置(JSON)',
    `login_config` JSON COMMENT '登录页定制配置(JSON)',
    `max_users` INT(11) DEFAULT 100 COMMENT '最大用户数限制',
    `max_enterprises` INT(11) DEFAULT 1000 COMMENT '最大企业数限制',
    `expire_date` DATE DEFAULT NULL COMMENT '服务到期日期',
    `create_by` INT(11) DEFAULT NULL COMMENT '创建人',
    `create_time` DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` INT(11) DEFAULT NULL COMMENT '更新人',
    `update_time` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    `delete_by` INT(11) DEFAULT NULL COMMENT '删除人',
    `delete_time` DATETIME DEFAULT NULL COMMENT '删除时间',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_org_code` (`org_code`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_org_status` (`status`),
    KEY `idx_org_type` (`org_type`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='认证机构配置表';

-- 创建注册申请表
CREATE TABLE IF NOT EXISTS `cert_registration` (
    `id` BIGINT(20) NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `code` CHAR(36) NOT NULL COMMENT 'GUID编码',
    `registration_no` VARCHAR(50) NOT NULL COMMENT '申请编号',
    `org_name` VARCHAR(200) NOT NULL COMMENT '机构/企业名称',
    `registration_type` TINYINT NOT NULL COMMENT '注册类型：1=认证机构, 2=企业用户',
    `contact_person` VARCHAR(100) NOT NULL COMMENT '联系人',
    `contact_phone` VARCHAR(20) NOT NULL COMMENT '联系电话',
    `contact_email` VARCHAR(200) DEFAULT NULL COMMENT '联系邮箱',
    `org_type` TINYINT DEFAULT NULL COMMENT '机构类型（认证机构时必填）',
    `business_license` VARCHAR(500) DEFAULT NULL COMMENT '营业执照URL',
    `qualification_files` JSON COMMENT '资质文件列表(JSON)',
    `status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态：0=待审核, 1=已通过, 2=已拒绝, 3=已撤销',
    `audit_by` INT(11) DEFAULT NULL COMMENT '审核人',
    `audit_time` DATETIME DEFAULT NULL COMMENT '审核时间',
    `audit_remark` VARCHAR(1000) DEFAULT NULL COMMENT '审核备注',
    `create_by` INT(11) DEFAULT NULL COMMENT '创建人',
    `create_time` DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `update_by` INT(11) DEFAULT NULL COMMENT '更新人',
    `update_time` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    `delete_by` INT(11) DEFAULT NULL COMMENT '删除人',
    `delete_time` DATETIME DEFAULT NULL COMMENT '删除时间',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_registration_no` (`registration_no`),
    UNIQUE KEY `uk_code` (`code`),
    KEY `idx_reg_type` (`registration_type`),
    KEY `idx_reg_status` (`status`),
    KEY `idx_reg_create_time` (`create_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='注册申请表';

SELECT '✅ 新表创建完成' AS status;

-- ============================================================
-- 第三部分：为现有业务表添加 Org_Code 字段（仅对存在的表）
-- ============================================================

-- 定义需要处理的表列表
-- 认证体系配置相关表
CALL safe_add_column('cert_certification_body', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('cert_certification_body', 'idx_cb_org_code', 'OrgCode');

CALL safe_add_column('cert_certification_system', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('cert_certification_system', 'idx_cs_org_code', 'OrgCode');

CALL safe_add_column('cert_certification_scope', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('cert_certification_scope', 'idx_cscope_org_code', 'OrgCode');

-- 审核流程相关表
CALL safe_add_column('cert_audit_task', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('cert_audit_task', 'idx_at_org_code', 'OrgCode');

CALL safe_add_column('cert_audit_team', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('cert_audit_team', 'idx_ateam_org_code', 'OrgCode');

CALL safe_add_column('cert_audit_schedule', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('cert_audit_schedule', 'idx_aschedule_org_code', 'OrgCode');

CALL safe_add_column('cert_audit_finding', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('cert_audit_finding', 'idx_afinding_org_code', 'OrgCode');

CALL safe_add_column('cert_nc_record', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('cert_nc_record', 'idx_nc_org_code', 'OrgCode');

-- 企业与项目相关表
CALL safe_add_column('cert_enterprise_info', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('cert_enterprise_info', 'idx_ei_org_code', 'OrgCode');

CALL safe_add_column('cert_contract', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('cert_contract', 'idx_contract_org_code', 'OrgCode');

CALL safe_add_column('cert_project', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('cert_project', 'idx_project_org_code', 'OrgCode');

-- 文件与报告相关表
CALL safe_add_column('cert_document_template', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('cert_document_template', 'idx_dt_org_code', 'OrgCode');

CALL safe_add_column('cert_file_requirement', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('cert_file_requirement', 'idx_fr_org_code', 'OrgCode');

CALL safe_add_column('cert_uploaded_file', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('cert_uploaded_file', 'idx_uf_org_code', 'OrgCode');

CALL safe_add_column('cert_audit_report', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('cert_audit_report', 'idx_ar_org_code', 'OrgCode');

CALL safe_add_column('cert_report_task', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('cert_report_task', 'idx_rt_org_code', 'OrgCode');

-- 工作流相关表
CALL safe_add_column('wf_process_instance', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('wf_process_instance', 'wf_pi_org_code', 'OrgCode');

CALL safe_add_column('wf_task', 'OrgCode', 'VARCHAR(50) DEFAULT NULL', '所属机构编码');
CALL safe_add_index('wf_task', 'wf_task_org_code', 'OrgCode');

SELECT '✅ 业务表 OrgCode 字段添加完成' AS status;

-- ============================================================
-- 第四部分：插入角色数据（忽略重复）
-- ============================================================

-- Layer 1: 平台管理层（5个角色）
INSERT IGNORE INTO `Sys_Role` (`RoleId`, `RoleName`, `ParentId`, `Enable`, `CreateID`, `CreateDate`, `ModifyID`, `ModifyDate`, `OrderNo`) VALUES
(100, '超级管理员', 0, 1, 0, NOW(), 0, NOW(), 1),
(101, '总管理员', 0, 1, 0, NOW(), 0, NOW(), 2),
(102, '运维人员', 0, 1, 0, NOW(), 0, NOW(), 3),
(103, '配置人员', 0, 1, 0, NOW(), 0, NOW(), 4),
(104, '质量专员', 0, 1, 0, NOW(), 0, NOW(), 5);

-- Layer 2: 机构管理层（3个角色）
INSERT IGNORE INTO `Sys_Role` (`RoleId`, `RoleName`, `ParentId`, `Enable`, `CreateID`, `CreateDate`, `ModifyID`, `ModifyDate`, `OrderNo`) VALUES
(200, '审核管理员', 0, 1, 0, NOW(), 0, NOW(), 10),
(201, '审核组长', 0, 1, 0, NOW(), 0, NOW(), 11),
(202, '普通审核员', 0, 1, 0, NOW(), 0, NOW(), 12);

-- Layer 3: 企业层（1个角色）
INSERT IGNORE INTO `Sys_Role` (`RoleId`, `RoleName`, `ParentId`, `Enable`, `CreateID`, `CreateDate`, `ModifyID`, `ModifyDate`, `OrderNo`) VALUES
(300, '企业账号', 0, 1, 0, NOW(), 0, NOW(), 20);

SELECT '✅ 角色数据插入完成' AS status;

-- ============================================================
-- 第五部分：创建部门数据
-- ============================================================
INSERT IGNORE INTO `sys_department` (`id`, `department_name`, `department_code`, `parent_id`, `enable`, `create_id`, `create_time`, `modify_id`, `modify_time`) VALUES
(100, '体系认证平台总部', 'PLATFORM_HQ', 0, 1, 0, NOW(), 0, NOW()),
(101, '运维部', 'OPS_DEPT', 100, 1, 0, NOW(), 0, NOW()),
(102, '配置管理部', 'CONFIG_DEPT', 100, 1, 0, NOW(), 0, NOW()),
(103, '质量管理部', 'QA_DEPT', 100, 1, 0, NOW(), 0, NOW());

SELECT '✅ 部门数据插入完成' AS status;

-- ============================================================
-- 第六部分：更新超级管理员账号
-- ============================================================
UPDATE `Sys_User` SET `UserType` = 1 WHERE `User_Id` = 1;

SELECT '✅ 超级管理员账号更新完成' AS status;

-- ============================================================
-- 第七部分：创建测试用户（忽略重复）
-- ============================================================

-- 总管理员
INSERT IGNORE INTO `Sys_User` (
    `UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`,
    `Token`, `AppType`, `AuditStatus`, `Enable`,
    `UserType`, `OrgCode`, `Dept_Id`, `Email`, `Mobile`,
    `UserTrueName`, `CreateID`, `CreateDate`
) VALUES (
    'admin_manager', 
    'fAiqPZF6bVj4G7+qJcVaLQ==',
    '总管理员',
    101,
    '总管理员',
    UUID(),
    1,
    2,
    1,
    10,
    NULL,
    100,
    'admin@certplatform.com',
    '13800000001',
    '平台总管理员',
    1,
    NOW()
);

-- 运维人员
INSERT IGNORE INTO `Sys_User` (
    `UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`,
    `Token`, `AppType`, `AuditStatus`, `Enable`,
    `UserType`, `OrgCode`, `Dept_Id`, `Email`, `Mobile`,
    `UserTrueName`, `CreateID`, `CreateDate`
) VALUES (
    'ops_user', 
    'fAiqPZF6bVj4G7+qJcVaLQ==',
    '运维人员',
    102,
    '运维人员',
    UUID(),
    1,
    2,
    1,
    13,
    NULL,
    101,
    'ops@certplatform.com',
    '13800000002',
    '运维专员',
    1,
    NOW()
);

-- 配置人员
INSERT IGNORE INTO `Sys_User` (
    `UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`,
    `Token`, `AppType`, `AuditStatus`, `Enable`,
    `UserType`, `OrgCode`, `Dept_Id`, `Email`, `Mobile`,
    `UserTrueName`, `CreateID`, `CreateDate`
) VALUES (
    'config_user', 
    'fAiqPZF6bVj4G7+qJcVaLQ==',
    '配置人员',
    103,
    '配置人员',
    UUID(),
    1,
    2,
    1,
    14,
    NULL,
    102,
    'config@certplatform.com',
    '13800000003',
    '配置专员',
    1,
    NOW()
);

-- 质量专员
INSERT IGNORE INTO `Sys_User` (
    `UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`,
    `Token`, `AppType`, `AuditStatus`, `Enable`,
    `UserType`, `OrgCode`, `Dept_Id`, `Email`, `Mobile`,
    `UserTrueName`, `CreateID`, `CreateDate`
) VALUES (
    'qa_user', 
    'fAiqPZF6bVj4G7+qJcVaLQ==',
    '质量专员',
    104,
    '质量专员',
    UUID(),
    1,
    2,
    1,
    15,
    NULL,
    103,
    'qa@certplatform.com',
    '13800000004',
    '质量专员',
    1,
    NOW()
);

SELECT '✅ 平台管理层测试用户创建完成' AS status;

-- ============================================================
-- 第八部分：创建示例机构和审核员用户
-- ============================================================

-- 插入示例认证机构（如果不存在）
INSERT IGNORE INTO `cert_org_config` (
    `code`, `org_code`, `org_name`, `org_short_name`, `org_type`,
    `registration_no`, `legal_person`, `contact_phone`, `contact_email`,
    `address`, `status`, `max_users`, `max_enterprises`
) VALUES (
    UUID(),
    'CB001',
    '河北雄安尚龙认证有限公司',
    '尚龙认证',
    1,
    'CNAS-C131-M',
    '张三',
    '0312-12345678',
    'admin@shanglong.cn',
    '河北省雄安新区容城县',
    1,
    50,
    500
);

-- 审核管理员（CB001）
INSERT IGNORE INTO `Sys_User` (
    `UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`,
    `Token`, `AppType`, `AuditStatus`, `Enable`,
    `UserType`, `OrgCode`, `OrgId`, `Email`, `Mobile`,
    `UserTrueName`, `CreateID`, `CreateDate`
) SELECT
    'cb001_admin',
    'fAiqPZF6bVj4G7+qJcVaLQ==',
    '审核管理员',
    200,
    '审核管理员',
    UUID(),
    1,
    2,
    1,
    20,
    'CB001',
    id,
    'admin@shanglong.cn',
    '13900000001',
    '李四（尚龙认证管理员）',
    1,
    NOW()
FROM `cert_org_config` WHERE `org_code` = 'CB001' LIMIT 1;

-- 审核组长（CB001）
INSERT IGNORE INTO `Sys_User` (
    `UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`,
    `Token`, `AppType`, `AuditStatus`, `Enable`,
    `UserType`, `OrgCode`, `OrgId`, `Email`, `Mobile`,
    `UserTrueName`, `CreateID`, `CreateDate`
) SELECT
    'cb001_leader',
    'fAiqPZF6bVj4G7+qJcVaLQ==',
    '审核组长',
    201,
    '审核组长',
    UUID(),
    1,
    2,
    1,
    21,
    'CB001',
    id,
    'leader@shanglong.cn',
    '13900000002',
    '王五（尚龙审核组长）',
    1,
    NOW()
FROM `cert_org_config` WHERE `org_code` = 'CB001' LIMIT 1;

-- 普通审核员（CB001）
INSERT IGNORE INTO `Sys_User` (
    `UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`,
    `Token`, `AppType`, `AuditStatus`, `Enable`,
    `UserType`, `OrgCode`, `OrgId`, `Email`, `Mobile`,
    `UserTrueName`, `CreateID`, `CreateDate`
) SELECT
    'cb001_auditor',
    'fAiqPZF6bVj4G7+qJcVaLQ==',
    '普通审核员',
    202,
    '普通审核员',
    UUID(),
    1,
    2,
    1,
    22,
    'CB001',
    id,
    'auditor@shanglong.cn',
    '13900000003',
    '赵六（尚龙审核员）',
    1,
    NOW()
FROM `cert_org_config` WHERE `org_code` = 'CB001' LIMIT 1;

SELECT '✅ 示例机构和审核员用户创建完成' AS status;

-- ============================================================
-- 第九部分：创建企业用户测试账号
-- ============================================================
INSERT IGNORE INTO `Sys_User` (
    `UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`,
    `Token`, `AppType`, `AuditStatus`, `Enable`,
    `UserType`, `OrgCode`, `OrgId`, `Email`, `Mobile`,
    `UserTrueName`, `CreateID`, `CreateDate`
) SELECT
    'ent001_user',
    'fAiqPZF6bVj4G7+qJcVaLQ==',
    '企业账号',
    300,
    '企业账号',
    UUID(),
    1,
    2,
    1,
    30,
    'CB001',
    id,
    'ent@testcompany.com',
    '13700000001',
    '孙七（测试企业管理员）',
    1,
    NOW()
FROM `cert_org_config` WHERE `org_code` = 'CB001' LIMIT 1;

SELECT '✅ 企业用户测试账号创建完成' AS status;

-- ============================================================
-- 清理临时存储过程（可选）
-- ============================================================
-- DROP PROCEDURE IF EXISTS safe_add_column;
-- DROP PROCEDURE IF EXISTS safe_add_index;

-- ============================================================
-- 最终验证
-- ============================================================
SELECT '' AS separator;
SELECT '🎉 Phase 1 数据库初始化全部完成！' AS message;
SELECT '' AS separator;
SELECT '📋 测试账号清单（密码均为 123456）：' AS info;
SELECT CONCAT('  - ', UserName, ' (', UserTypeName, ')') AS account 
FROM Sys_User WHERE UserType IS NOT NULL ORDER BY UserType;
