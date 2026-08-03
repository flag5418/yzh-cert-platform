-- ============================================================
-- 体系认证平台 - Phase 1: 简化执行脚本
-- 说明: 分步执行，每步独立，支持重复执行
-- 执行方式：分多次执行或使用 source 命令
-- ============================================================

USE `yzh_cert_platform`;

-- ============================================================
-- Step 1: Sys_User 表扩展（幂等操作）
-- ============================================================

-- 添加 UserType 字段（如果不存在）
SET @sql = (SELECT IF(
    (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'Sys_User' AND column_name = 'UserType') = 0,
    'ALTER TABLE `Sys_User` ADD COLUMN `UserType` TINYINT NOT NULL DEFAULT 10 COMMENT ''用户类型'' AFTER `Enable`',
    'SELECT "Skip: UserType exists"'
));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 添加 OrgCode 字段（如果不存在）
SET @sql = (SELECT IF(
    (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'Sys_User' AND column_name = 'OrgCode') = 0,
    'ALTER TABLE `Sys_User` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT ''机构编码'' AFTER `UserType`',
    'SELECT "Skip: OrgCode exists"'
));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 添加 OrgId 字段（如果不存在）
SET @sql = (SELECT IF(
    (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'Sys_User' AND column_name = 'OrgId') = 0,
    'ALTER TABLE `Sys_User` ADD COLUMN `OrgId` BIGINT(20) DEFAULT NULL COMMENT ''机构ID'' AFTER `OrgCode`',
    'SELECT "Skip: OrgId exists"'
));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 添加 ParentUserId 字段（如果不存在）
SET @sql = (SELECT IF(
    (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'Sys_User' AND column_name = 'ParentUserId') = 0,
    'ALTER TABLE `Sys_User` ADD COLUMN `ParentUserId` INT(11) DEFAULT NULL COMMENT ''上级用户ID'' AFTER `OrgId`',
    'SELECT "Skip: ParentUserId exists"'
));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT '✅ Sys_User 表扩展完成' AS Status;

-- ============================================================
-- Step 2: 创建新表
-- ============================================================
CREATE TABLE IF NOT EXISTS `cert_org_config` (
    `Id` BIGINT(20) NOT NULL AUTO_INCREMENT,
    `Code` CHAR(36) NOT NULL,
    `OrgCode` VARCHAR(50) NOT NULL,
    `CreateID` INT DEFAULT NULL,
    `Creator` NVARCHAR(50) DEFAULT NULL,
    `CreateDate` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `ModifyID` INT DEFAULT NULL,
    `Modifier` NVARCHAR(50) DEFAULT NULL,
    `ModifyDate` DATETIME DEFAULT NULL,
    `DeleteID` INT DEFAULT NULL,
    `Deleter` NVARCHAR(50) DEFAULT NULL,
    `DeleteTime` DATETIME DEFAULT NULL,
    `Status` VARCHAR(50) DEFAULT 'active',
    `Enable` TINYINT DEFAULT 1,
    `Sort` INT DEFAULT 0,
    `Remark` NVARCHAR(500) DEFAULT NULL,
    `org_name` VARCHAR(200) NOT NULL,
    `org_short_name` VARCHAR(100) DEFAULT NULL,
    `org_type` TINYINT NOT NULL DEFAULT 1,
    `registration_no` VARCHAR(100) DEFAULT NULL,
    `LegalPerson` VARCHAR(100) DEFAULT NULL,
    `ContactPhone` VARCHAR(20) DEFAULT NULL,
    `ContactEmail` VARCHAR(200) DEFAULT NULL,
    `Address` VARCHAR(500) DEFAULT NULL,
    `logo_url` VARCHAR(500) DEFAULT NULL,
    `ScopeText` TEXT,
    `cert_scope_json` JSON,
    `theme_config` JSON,
    `login_config` JSON,
    `max_users` INT(11) DEFAULT 100,
    `max_enterprises` INT(11) DEFAULT 1000,
    `expire_date` DATE DEFAULT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_org_code` (`OrgCode`),
    UNIQUE KEY `uk_code` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='认证机构配置表';

CREATE TABLE IF NOT EXISTS `cert_registration` (
    `Id` BIGINT(20) NOT NULL AUTO_INCREMENT,
    `Code` CHAR(36) NOT NULL,
    `CreateID` INT DEFAULT NULL,
    `Creator` NVARCHAR(50) DEFAULT NULL,
    `CreateDate` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `ModifyID` INT DEFAULT NULL,
    `Modifier` NVARCHAR(50) DEFAULT NULL,
    `ModifyDate` DATETIME DEFAULT NULL,
    `DeleteID` INT DEFAULT NULL,
    `Deleter` NVARCHAR(50) DEFAULT NULL,
    `DeleteTime` DATETIME DEFAULT NULL,
    `Status` VARCHAR(50) DEFAULT 'active',
    `Enable` TINYINT DEFAULT 1,
    `Sort` INT DEFAULT 0,
    `Remark` NVARCHAR(500) DEFAULT NULL,
    `registration_no` VARCHAR(50) NOT NULL,
    `org_name` VARCHAR(200) NOT NULL,
    `registration_type` TINYINT NOT NULL,
    `ContactName` VARCHAR(100) NOT NULL,
    `ContactPhone` VARCHAR(20) NOT NULL,
    `ContactEmail` VARCHAR(200) DEFAULT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_registration_no` (`registration_no`),
    UNIQUE KEY `uk_code` (`Code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='注册申请表';

SELECT '✅ 新表创建完成' AS Status;

-- ============================================================
-- Step 3: 为已存在的业务表添加 OrgCode（手动列出）
-- 只处理确认存在的表
-- ============================================================

-- cert_certification_body
SET @sql = (SELECT IF(
    (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_certification_body') > 0
    AND (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_certification_body' AND column_name = 'OrgCode') = 0,
    'ALTER TABLE `cert_certification_body` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL',
    'SELECT "Skip: cert_certification_body"'
));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- cert_iso_standard
SET @sql = (SELECT IF(
    (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_iso_standard') > 0
    AND (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_iso_standard' AND column_name = 'OrgCode') = 0,
    'ALTER TABLE `cert_iso_standard` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL',
    'SELECT "Skip: cert_iso_standard"'
));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- cert_file_requirement
SET @sql = (SELECT IF(
    (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_file_requirement') > 0
    AND (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_file_requirement' AND column_name = 'OrgCode') = 0,
    'ALTER TABLE `cert_file_requirement` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL',
    'SELECT "Skip: cert_file_requirement"'
));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- cert_report_template
SET @sql = (SELECT IF(
    (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_report_template') > 0
    AND (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_report_template' AND column_name = 'OrgCode') = 0,
    'ALTER TABLE `cert_report_template` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL',
    'SELECT "Skip: cert_report_template"'
));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT '✅ 业务表字段添加完成' AS Status;

-- ============================================================
-- Step 4: 角色数据（INSERT IGNORE 避免重复）
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

SELECT '✅ 角色数据插入完成' AS Status;

-- ============================================================
-- Step 5: 部门数据
-- ============================================================
INSERT IGNORE INTO `sys_department` (`Id`, `department_name`, `department_code`, `parent_id`, `Enable`, `CreateID`, `CreateDate`, `ModifyID`, `ModifyDate`) VALUES
(100, '体系认证平台总部', 'PLATFORM_HQ', 0, 1, 0, NOW(), 0, NOW()),
(101, '运维部', 'OPS_DEPT', 100, 1, 0, NOW(), 0, NOW()),
(102, '配置管理部', 'CONFIG_DEPT', 100, 1, 0, NOW(), 0, NOW()),
(103, '质量管理部', 'QA_DEPT', 100, 1, 0, NOW(), 0, NOW());

SELECT '✅ 部门数据插入完成' AS Status;

-- ============================================================
-- Step 6: 更新超级管理员
-- ============================================================
UPDATE `Sys_User` SET `UserType` = 1 WHERE `User_Id` = 1;
SELECT '✅ 超级管理员更新完成' AS Status;

-- ============================================================
-- Step 7: 测试用户
-- ============================================================
INSERT IGNORE INTO `Sys_User` (`UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`, `Token`, `AppType`, `AuditStatus`, `Enable`, `UserType`, `OrgCode`, `Dept_Id`, `Email`, `Mobile`, `UserTrueName`, `CreateID`, `CreateDate`) VALUES
('admin_manager', 'fAiqPZF6bVj4G7+qJcVaLQ==', '总管理员', 101, '总管理员', UUID(), 1, 2, 1, 10, NULL, 100, 'admin@certplatform.com', '13800000001', '平台总管理员', 1, NOW()),
('ops_user', 'fAiqPZF6bVj4G7+qJcVaLQ==', '运维人员', 102, '运维人员', UUID(), 1, 2, 1, 13, NULL, 101, 'ops@certplatform.com', '13800000002', '运维专员', 1, NOW()),
('config_user', 'fAiqPZF6bVj4G7+qJcVaLQ==', '配置人员', 103, '配置人员', UUID(), 1, 2, 1, 14, NULL, 102, 'config@certplatform.com', '13800000003', '配置专员', 1, NOW()),
('qa_user', 'fAiqPZF6bVj4G7+qJcVaLQ==', '质量专员', 104, '质量专员', UUID(), 1, 2, 1, 15, NULL, 103, 'qa@certplatform.com', '13800000004', '质量专员', 1, NOW());

SELECT '✅ 平台层测试用户创建完成' AS Status;

-- ============================================================
-- Step 8: 示例机构和审核员
-- ============================================================
INSERT IGNORE INTO `cert_org_config` (`Code`, `OrgCode`, `org_name`, `org_short_name`, `org_type`, `registration_no`, `LegalPerson`, `ContactPhone`, `ContactEmail`, `Address`, `Status`) VALUES
(UUID(), 'CB001', '河北雄安尚龙认证有限公司', '尚龙认证', 1, 'CNAS-C131-M', '张三', '0312-12345678', 'admin@shanglong.cn', '河北省雄安新区容城县', 1);

INSERT IGNORE INTO `Sys_User` (`UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`, `Token`, `AppType`, `AuditStatus`, `Enable`, `UserType`, `OrgCode`, `OrgId`, `Email`, `Mobile`, `UserTrueName`, `CreateID`, `CreateDate`)
SELECT 'cb001_admin', 'fAiqPZF6bVj4G7+qJcVaLQ==', '审核管理员', 200, '审核管理员', UUID(), 1, 2, 1, 20, 'CB001', Id, 'admin@shanglong.cn', '13900000001', '李四（尚龙）', 1, NOW() FROM `cert_org_config` WHERE `OrgCode` = 'CB001';

INSERT IGNORE INTO `Sys_User` (`UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`, `Token`, `AppType`, `AuditStatus`, `Enable`, `UserType`, `OrgCode`, `OrgId`, `Email`, `Mobile`, `UserTrueName`, `CreateID`, `CreateDate`)
SELECT 'cb001_leader', 'fAiqPZF6bVj4G7+qJcVaLQ==', '审核组长', 201, '审核组长', UUID(), 1, 2, 1, 21, 'CB001', Id, 'leader@shanglong.cn', '13900000002', '王五（尚龙）', 1, NOW() FROM `cert_org_config` WHERE `OrgCode` = 'CB001';

INSERT IGNORE INTO `Sys_User` (`UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`, `Token`, `AppType`, `AuditStatus`, `Enable`, `UserType`, `OrgCode`, `OrgId`, `Email`, `Mobile`, `UserTrueName`, `CreateID`, `CreateDate`)
SELECT 'cb001_auditor', 'fAiqPZF6bVj4G7+qJcVaLQ==', '普通审核员', 202, '普通审核员', UUID(), 1, 2, 1, 22, 'CB001', Id, 'auditor@shanglong.cn', '13900000003', '赵六（尚龙）', 1, NOW() FROM `cert_org_config` WHERE `OrgCode` = 'CB001';

SELECT '✅ 示例机构和审核员创建完成' AS Status;

-- ============================================================
-- Step 9: 企业用户
-- ============================================================
INSERT IGNORE INTO `Sys_User` (`UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`, `Token`, `AppType`, `AuditStatus`, `Enable`, `UserType`, `OrgCode`, `OrgId`, `Email`, `Mobile`, `UserTrueName`, `CreateID`, `CreateDate`)
SELECT 'ent001_user', 'fAiqPZF6bVj4G7+qJcVaLQ==', '企业账号', 300, '企业账号', UUID(), 1, 2, 1, 30, 'CB001', Id, 'ent@testcompany.com', '13700000001', '孙七（企业）', 1, NOW() FROM `cert_org_config` WHERE `OrgCode` = 'CB001';

SELECT '✅ 企业用户创建完成' AS Status;

-- ============================================================
-- 最终验证
-- ============================================================
SELECT '' AS '';
SELECT '========================================' AS '';
SELECT '  🎉 Phase 1 初始化完成！' AS '';
SELECT '========================================' AS '';
SELECT '' AS '';
SELECT '📋 测试账号（密码: 123456）：' AS '';
SELECT User_Id, UserName, UserTypeName, UserType, OrgCode FROM Sys_User WHERE UserType IS NOT NULL ORDER BY UserType;
