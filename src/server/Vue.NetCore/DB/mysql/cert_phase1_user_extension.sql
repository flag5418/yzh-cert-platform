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

SELECT '✅ Sys_User 表扩展完成' AS status;

-- ============================================================
-- Step 2: 创建新表
-- ============================================================

CREATE TABLE IF NOT EXISTS `cert_org_config` (
    `id` BIGINT(20) NOT NULL AUTO_INCREMENT,
    `code` CHAR(36) NOT NULL,
    `org_code` VARCHAR(50) NOT NULL,
    `org_name` VARCHAR(200) NOT NULL,
    `org_short_name` VARCHAR(100) DEFAULT NULL,
    `org_type` TINYINT NOT NULL DEFAULT 1,
    `registration_no` VARCHAR(100) DEFAULT NULL,
    `legal_person` VARCHAR(100) DEFAULT NULL,
    `contact_phone` VARCHAR(20) DEFAULT NULL,
    `contact_email` VARCHAR(200) DEFAULT NULL,
    `address` VARCHAR(500) DEFAULT NULL,
    `logo_url` VARCHAR(500) DEFAULT NULL,
    `status` TINYINT NOT NULL DEFAULT 0,
    `scope_text` TEXT,
    `cert_scope_json` JSON,
    `theme_config` JSON,
    `login_config` JSON,
    `max_users` INT(11) DEFAULT 100,
    `max_enterprises` INT(11) DEFAULT 1000,
    `expire_date` DATE DEFAULT NULL,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_org_code` (`org_code`),
    UNIQUE KEY `uk_code` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='认证机构配置表';

CREATE TABLE IF NOT EXISTS `cert_registration` (
    `id` BIGINT(20) NOT NULL AUTO_INCREMENT,
    `code` CHAR(36) NOT NULL,
    `registration_no` VARCHAR(50) NOT NULL,
    `org_name` VARCHAR(200) NOT NULL,
    `registration_type` TINYINT NOT NULL,
    `contact_person` VARCHAR(100) NOT NULL,
    `contact_phone` VARCHAR(20) NOT NULL,
    `contact_email` VARCHAR(200) DEFAULT NULL,
    `status` TINYINT NOT NULL DEFAULT 0,
    `create_time` DATETIME DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_registration_no` (`registration_no`),
    UNIQUE KEY `uk_code` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='注册申请表';

SELECT '✅ 新表创建完成' AS status;

-- ============================================================
-- Step 3: 为已存在的业务表添加 OrgCode（手动列出）
-- 只处理确认存在的表
-- ============================================================

-- cert_certification_body
SET @sql = (SELECT IF(
    (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_certification_body' AND column_name = 'OrgCode') = 0,
    'ALTER TABLE `cert_certification_body` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT ''机构编码（多租户）'' AFTER `notes`',
    'SELECT "Skip: cert_certification_body.OrgCode exists"'
));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- cert_iso_standard
SET @sql = (SELECT IF(
    (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'yzh_cert_platform' AND table_name = 'cert_iso_standard' AND column_name = 'OrgCode') = 0,
    'ALTER TABLE `cert_iso_standard` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT ''机构编码（多租户）'' AFTER `notes`',
    'SELECT "Skip: cert_iso_standard.OrgCode exists"'
));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT '✅ 业务表多租户字段扩展完成' AS status;
SELECT '🎉 Phase 1 用户权限体系扩展完成！' AS summary;
