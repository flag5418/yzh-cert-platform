-- ============================================================
-- 体系认证平台 - Phase 1: 用户权限体系扩展
-- 说明: Sys_User 表扩展 + 新表创建 + 业务表多租户字段
-- 执行方式: 在 yzh_cert_platform 数据库中执行
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
    `org_name` VARCHAR(200) NOT NULL,
    `org_short_name` VARCHAR(100) DEFAULT NULL,
    `org_type` TINYINT NOT NULL DEFAULT 1,
    `registration_no` VARCHAR(100) DEFAULT NULL,
    `LegalPerson` VARCHAR(100) DEFAULT NULL,
    `ContactPhone` VARCHAR(20) DEFAULT NULL,
    `ContactEmail` VARCHAR(200) DEFAULT NULL,
    `Address` VARCHAR(500) DEFAULT NULL,
    `logo_url` VARCHAR(500) DEFAULT NULL,
    `Status` TINYINT NOT NULL DEFAULT 0,
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
    `registration_no` VARCHAR(50) NOT NULL,
    `org_name` VARCHAR(200) NOT NULL,
    `registration_type` TINYINT NOT NULL,
    `ContactName` VARCHAR(100) NOT NULL,
    `ContactPhone` VARCHAR(20) NOT NULL,
    `ContactEmail` VARCHAR(200) DEFAULT NULL,
    `Status` TINYINT NOT NULL DEFAULT 0,
    `CreateDate` DATETIME DEFAULT CURRENT_TIMESTAMP,
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
    (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_certification_body' AND column_name = 'OrgCode') = 0,
    'ALTER TABLE `cert_certification_body` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT ''机构编码（多租户）'' AFTER `Remark`',
    'SELECT "Skip: cert_certification_body.OrgCode exists"'
));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- cert_iso_standard
SET @sql = (SELECT IF(
    (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_iso_standard' AND column_name = 'OrgCode') = 0,
    'ALTER TABLE `cert_iso_standard` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT ''机构编码（多租户）'' AFTER `Remark`',
    'SELECT "Skip: cert_iso_standard.OrgCode exists"'
));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT '✅ 业务表多租户字段扩展完成' AS Status;
SELECT '🎉 Phase 1 用户权限体系扩展完成！' AS summary;
