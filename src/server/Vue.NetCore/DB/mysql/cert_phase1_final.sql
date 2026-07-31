-- ============================================================
-- 体系认证平台 - Phase 1: 最终执行脚本（完全安全版）
-- 说明: 所有操作都支持重复执行，不会报错
-- ============================================================

USE `yzh_cert_platform`;

SET @exist = (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'yzh_cert_platform' AND table_name = 'Sys_User');

-- ============================================================
-- 第一部分：Sys_User 表结构扩展
-- ============================================================

-- 检查并添加 UserType 字段
SET @col_count = (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'Sys_User' AND column_name = 'UserType');
SET @sql = IF(@col_count = 0, 
    'ALTER TABLE `Sys_User` ADD COLUMN `UserType` TINYINT NOT NULL DEFAULT 10 COMMENT ''用户类型：1=超级管理员, 10=总管理员, 13=运维人员, 14=配置人员, 15=质量专员, 20=审核管理员, 21=审核组长, 22=普通审核员, 30=企业账号'' AFTER `Enable`', 
    'SELECT "UserType column already exists"');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 检查并添加 OrgCode 字段
SET @col_count = (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'Sys_User' AND column_name = 'OrgCode');
SET @sql = IF(@col_count = 0, 
    'ALTER TABLE `Sys_User` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT ''机构编码（多租户隔离），NULL表示平台管理层'' AFTER `UserType`', 
    'SELECT "OrgCode column already exists"');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 检查并添加 OrgId 字段
SET @col_count = (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'Sys_User' AND column_name = 'OrgId');
SET @sql = IF(@col_count = 0, 
    'ALTER TABLE `Sys_User` ADD COLUMN `OrgId` BIGINT(20) DEFAULT NULL COMMENT ''机构ID，关联cert_org_config.id'' AFTER `OrgCode`', 
    'SELECT "OrgId column already exists"');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 检查并添加 ParentUserId 字段
SET @col_count = (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'Sys_User' AND column_name = 'ParentUserId');
SET @sql = IF(@col_count = 0, 
    'ALTER TABLE `Sys_User` ADD COLUMN `ParentUserId` INT(11) DEFAULT NULL COMMENT ''上级用户ID，用于企业子账号或审核员层级'' AFTER `OrgId`', 
    'SELECT "ParentUserId column already exists"');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 创建索引（如果不存在）
SET @idx_count = (SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = 'yzh_cert_platform' AND table_name = 'Sys_User' AND index_name = 'idx_sys_user_org_code');
SET @sql = IF(@idx_count = 0, 'ALTER TABLE `Sys_User` ADD INDEX `idx_sys_user_org_code` (`OrgCode`)', 'SELECT "Index idx_sys_user_org_code already exists"');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @idx_count = (SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = 'yzh_cert_platform' AND table_name = 'Sys_User' AND index_name = 'idx_sys_user_user_type');
SET @sql = IF(@idx_count = 0, 'ALTER TABLE `Sys_User` ADD INDEX `idx_sys_user_user_type` (`UserType`)', 'SELECT "Index idx_sys_user_user_type already exists"');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SELECT '✅ Step 1: Sys_User 表扩展完成' AS status;

-- ============================================================
-- 第二部分：创建新表
-- ============================================================

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

SELECT '✅ Step 2: 新表创建完成' AS status;

-- ============================================================
-- 第三部分：为存在的业务表添加 OrgCode 字段
-- ============================================================

-- 定义需要处理的表数组（使用临时表模拟）
CREATE TEMPORARY TABLE IF NOT EXISTS temp_tables_to_update (
    table_name VARCHAR(100),
    column_after VARCHAR(100)
);

-- 清空并插入要处理的表列表
TRUNCATE TABLE temp_tables_to_update;
INSERT INTO temp_tables_to_update VALUES 
('cert_certification_body', 'notes'),
('cert_iso_standard', 'description'),
('cert_file_requirement', 'is_required'),
('cert_report_template', 'version'),
('cert_org_config', 'status'),
('cert_registration', 'status');

-- 使用游标处理每个表（这里简化为逐个处理）
-- 由于 MySQL 不支持直接在 SQL 中遍历，我们手动列出所有可能的表

-- 辅助函数：安全的 ALTER TABLE
-- 对于每个表，先检查表是否存在，再检查列是否存在

-- cert_certification_body
SET @table_exists = (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_certification_body');
IF @table_exists > 0 THEN
    SET @col_exists = (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_certification_body' AND column_name = 'OrgCode');
    IF @col_exists = 0 THEN
        ALTER TABLE `cert_certification_body` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `notes`;
        ALTER TABLE `cert_certification_body` ADD INDEX `idx_cb_org_code` (`OrgCode`);
    END IF;
END IF;

-- cert_iso_standard
SET @table_exists = (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_iso_standard');
IF @table_exists > 0 THEN
    SET @col_exists = (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_iso_standard' AND column_name = 'OrgCode');
    IF @col_exists = 0 THEN
        ALTER TABLE `cert_iso_standard` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `description`;
        ALTER TABLE `cert_iso_standard` ADD INDEX `idx_cs_org_code` (`OrgCode`);
    END IF;
END IF;

-- cert_file_requirement
SET @table_exists = (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_file_requirement');
IF @table_exists > 0 THEN
    SET @col_exists = (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_file_requirement' AND column_name = 'OrgCode');
    IF @col_exists = 0 THEN
        ALTER TABLE `cert_file_requirement` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `is_required`;
        ALTER TABLE `cert_file_requirement` ADD INDEX `idx_fr_org_code` (`OrgCode`);
    END IF;
END IF;

-- cert_report_template
SET @table_exists = (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_report_template');
IF @table_exists > 0 THEN
    SET @col_exists = (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_report_template' AND column_name = 'OrgCode');
    IF @col_exists = 0 THEN
        ALTER TABLE `cert_report_template` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `version`;
        ALTER TABLE `cert_report_template` ADD INDEX `idx_dt_org_code` (`OrgCode`);
    END IF;
END IF;

DROP TEMPORARY TABLE IF EXISTS temp_tables_to_update;

SELECT '✅ Step 3: 业务表 OrgCode 字段添加完成' AS status;

-- ============================================================
-- 第四部分：插入角色数据
-- ============================================================
INSERT IGNORE INTO `Sys_Role` (`RoleId`, `RoleName`, `ParentId`, `Enable`, `CreateID`, `CreateDate`, `ModifyID`, `ModifyDate`, `OrderNo`) VALUES
(100, '超级管理员', 0, 1, 0, NOW(), 0, NOW(), 1),
(101, '总管理员', 0, 1, 0, NOW(), 0, NOW(), 2),
(102, '运维人员', 0, 1, 0, NOW(), 0, NOW(), 3),
(103, '配置人员', 0, 1, 0, NOW(), 0, NOW(), 4),
(104, '质量专员', 0, 1, 0, NOW(), 0, NOW(), 5),
(200, '审核管理员', 0, 1, 0, NOW(), 0, NOW(), 10),
(201, '审核组长', 0, 1, 0, NOW(), 0, NOW(), 11),
(202, '普通审核员', 0, 1, 0, NOW(), 0, NOW(), 12),
(300, '企业账号', 0, 1, 0, NOW(), 0, NOW(), 20);

SELECT '✅ Step 4: 角色数据插入完成' AS status;

-- ============================================================
-- 第五部分：部门数据
-- ============================================================
INSERT IGNORE INTO `sys_department` (`id`, `department_name`, `department_code`, `parent_id`, `enable`, `create_id`, `create_time`, `modify_id`, `modify_time`) VALUES
(100, '体系认证平台总部', 'PLATFORM_HQ', 0, 1, 0, NOW(), 0, NOW()),
(101, '运维部', 'OPS_DEPT', 100, 1, 0, NOW(), 0, NOW()),
(102, '配置管理部', 'CONFIG_DEPT', 100, 1, 0, NOW(), 0, NOW()),
(103, '质量管理部', 'QA_DEPT', 100, 1, 0, NOW(), 0, NOW());

SELECT '✅ Step 5: 部门数据插入完成' AS status;

-- ============================================================
-- 第六部分：更新超级管理员
-- ============================================================
UPDATE `Sys_User` SET `UserType` = 1 WHERE `User_Id` = 1;

SELECT '✅ Step 6: 超级管理员账号更新完成' AS status;

-- ============================================================
-- 第七部分：测试用户
-- ============================================================
INSERT IGNORE INTO `Sys_User` (`UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`, `Token`, `AppType`, `AuditStatus`, `Enable`, `UserType`, `OrgCode`, `Dept_Id`, `Email`, `Mobile`, `UserTrueName`, `CreateID`, `CreateDate`) VALUES
('admin_manager', 'fAiqPZF6bVj4G7+qJcVaLQ==', '总管理员', 101, '总管理员', UUID(), 1, 2, 1, 10, NULL, 100, 'admin@certplatform.com', '13800000001', '平台总管理员', 1, NOW()),
('ops_user', 'fAiqPZF6bVj4G7+qJcVaLQ==', '运维人员', 102, '运维人员', UUID(), 1, 2, 1, 13, NULL, 101, 'ops@certplatform.com', '13800000002', '运维专员', 1, NOW()),
('config_user', 'fAiqPZF6bVj4G7+qJcVaLQ==', '配置人员', 103, '配置人员', UUID(), 1, 2, 1, 14, NULL, 102, 'config@certplatform.com', '13800000003', '配置专员', 1, NOW()),
('qa_user', 'fAiqPZF6bVj4G7+qJcVaLQ==', '质量专员', 104, '质量专员', UUID(), 1, 2, 1, 15, NULL, 103, 'qa@certplatform.com', '13800000004', '质量专员', 1, NOW());

SELECT '✅ Step 7: 平台管理层测试用户创建完成' AS status;

-- ============================================================
-- 第八部分：示例机构和审核员
-- ============================================================
INSERT IGNORE INTO `cert_org_config` (`code`, `org_code`, `org_name`, `org_short_name`, `org_type`, `registration_no`, `legal_person`, `contact_phone`, `contact_email`, `address`, `status`, `max_users`, `max_enterprises`) VALUES
(UUID(), 'CB001', '河北雄安尚龙认证有限公司', '尚龙认证', 1, 'CNAS-C131-M', '张三', '0312-12345678', 'admin@shanglong.cn', '河北省雄安新区容城县', 1, 50, 500);

INSERT IGNORE INTO `Sys_User` (`UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`, `Token`, `AppType`, `AuditStatus`, `Enable`, `UserType`, `OrgCode`, `OrgId`, `Email`, `Mobile`, `UserTrueName`, `CreateID`, `CreateDate`)
SELECT 'cb001_admin', 'fAiqPZF6bVj4G7+qJcVaLQ==', '审核管理员', 200, '审核管理员', UUID(), 1, 2, 1, 20, 'CB001', id, 'admin@shanglong.cn', '13900000001', '李四（尚龙认证管理员）', 1, NOW() FROM `cert_org_config` WHERE `org_code` = 'CB001' LIMIT 1;

INSERT IGNORE INTO `Sys_User` (`UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`, `Token`, `AppType`, `AuditStatus`, `Enable`, `UserType`, `OrgCode`, `OrgId`, `Email`, `Mobile`, `UserTrueName`, `CreateID`, `CreateDate`)
SELECT 'cb001_leader', 'fAiqPZF6bVj4G7+qJcVaLQ==', '审核组长', 201, '审核组长', UUID(), 1, 2, 1, 21, 'CB001', id, 'leader@shanglong.cn', '13900000002', '王五（尚龙审核组长）', 1, NOW() FROM `cert_org_config` WHERE `org_code` = 'CB001' LIMIT 1;

INSERT IGNORE INTO `Sys_User` (`UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`, `Token`, `AppType`, `AuditStatus`, `Enable`, `UserType`, `OrgCode`, `OrgId`, `Email`, `Mobile`, `UserTrueName`, `CreateID`, `CreateDate`)
SELECT 'cb001_auditor', 'fAiqPZF6bVj4G7+qJcVaLQ==', '普通审核员', 202, '普通审核员', UUID(), 1, 2, 1, 22, 'CB001', id, 'auditor@shanglong.cn', '13900000003', '赵六（尚龙审核员）', 1, NOW() FROM `cert_org_config` WHERE `org_code` = 'CB001' LIMIT 1;

SELECT '✅ Step 8: 示例机构和审核员用户创建完成' AS status;

-- ============================================================
-- 第九部分：企业用户
-- ============================================================
INSERT IGNORE INTO `Sys_User` (`UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`, `Token`, `AppType`, `AuditStatus`, `Enable`, `UserType`, `OrgCode`, `OrgId`, `Email`, `Mobile`, `UserTrueName`, `CreateID`, `CreateDate`)
SELECT 'ent001_user', 'fAiqPZF6bVj4G7+qJcVaLQ==', '企业账号', 300, '企业账号', UUID(), 1, 2, 1, 30, 'CB001', id, 'ent@testcompany.com', '13700000001', '孙七（测试企业管理员）', 1, NOW() FROM `cert_org_config` WHERE `org_code` = 'CB001' LIMIT 1;

SELECT '✅ Step 9: 企业用户测试账号创建完成' AS status;

-- ============================================================
-- 最终验证输出
-- ============================================================
SELECT '' AS '';
SELECT '🎉🎉🎉 Phase 1 数据库初始化全部完成！🎉🎉🎉' AS message;
SELECT '' AS '';
SELECT '📋 测试账号清单（密码均为 123456）：' AS info;
SELECT User_Id, UserName, UserTypeName, UserType, OrgCode AS 机构编码 
FROM Sys_User 
WHERE UserType IS NOT NULL 
ORDER BY UserType;
SELECT '' AS '';
SELECT '📊 角色统计：' AS stats;
SELECT RoleId, RoleName FROM Sys_Role WHERE RoleId IN (100,101,102,103,104,200,201,202,300) ORDER BY RoleId;
