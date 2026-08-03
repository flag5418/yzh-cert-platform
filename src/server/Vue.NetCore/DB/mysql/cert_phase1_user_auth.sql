-- ============================================================
-- 体系认证平台 - Phase 1: 用户权限体系完整初始化脚本
-- 版本: V1.0
-- 日期: 2026-07-30
-- 说明: 包含表结构扩展、角色体系、权限矩阵、示例数据
-- ============================================================

-- 使用 yzh_cert_platform 数据库
USE `yzh_cert_platform`;

-- ============================================================
-- 第一部分：Sys_User 表结构扩展
-- ============================================================

-- 添加用户类型字段（区分管理员/审核员/企业）
ALTER TABLE `Sys_User` 
ADD COLUMN `UserType` TINYINT NOT NULL DEFAULT 10 
COMMENT '用户类型：1=超级管理员, 10=总管理员, 13=运维人员, 14=配置人员, 15=质量专员, 20=审核管理员, 21=审核组长, 22=普通审核员, 30=企业账号' 
AFTER `Enable`;

-- 添加机构编码字段（多租户数据隔离核心字段）
ALTER TABLE `Sys_User` 
ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL 
COMMENT '机构编码（多租户隔离），NULL表示平台管理层' 
AFTER `UserType`;

-- 添加机构ID字段（关联 cert_org_config.Id）
ALTER TABLE `Sys_User` 
ADD COLUMN `OrgId` BIGINT(20) DEFAULT NULL 
COMMENT '机构ID，关联cert_org_config.Id' 
AFTER `OrgCode`;

-- 添加上级用户ID字段（用于企业子账号或审核员层级管理）
ALTER TABLE `Sys_User` 
ADD COLUMN `ParentUserId` INT(11) DEFAULT NULL 
COMMENT '上级用户ID，用于企业子账号或审核员层级' 
AFTER `OrgId`;

-- 为 OrgCode 创建索引（提升多租户查询性能）
ALTER TABLE `Sys_User` ADD INDEX `idx_sys_user_org_code` (`OrgCode`);

-- 为 UserType 创建索引（提升按类型查询性能）
ALTER TABLE `Sys_User` ADD INDEX `idx_sys_user_user_type` (`UserType`);

-- ============================================================
-- 第二部分：创建机构配置表 (cert_org_config)
-- ============================================================

CREATE TABLE IF NOT EXISTS `cert_org_config` (
    `Id` BIGINT(20) NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` CHAR(36) NOT NULL COMMENT 'GUID编码，用于关联',
    `OrgCode` VARCHAR(50) NOT NULL COMMENT '机构唯一编码',
    `org_name` VARCHAR(200) NOT NULL COMMENT '机构全称',
    `org_short_name` VARCHAR(100) DEFAULT NULL COMMENT '机构简称',
    `org_type` TINYINT NOT NULL DEFAULT 1 COMMENT '机构类型：1=认证机构, 2=咨询公司, 3=检测机构',
    `registration_no` VARCHAR(100) DEFAULT NULL COMMENT '认证机构批准号',
    `LegalPerson` VARCHAR(100) DEFAULT NULL COMMENT '法定代表人',
    `ContactPhone` VARCHAR(20) DEFAULT NULL COMMENT '联系电话',
    `ContactEmail` VARCHAR(200) DEFAULT NULL COMMENT '联系邮箱',
    `Address` VARCHAR(500) DEFAULT NULL COMMENT '详细地址',
    `logo_url` VARCHAR(500) DEFAULT NULL COMMENT '机构Logo URL',
    `Status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态：0=停用, 1=正常, 2=待审核',
    `ScopeText` TEXT COMMENT '认证范围说明',
    `cert_scope_json` JSON COMMENT '认证范围详细数据(JSON)',
    `theme_config` JSON COMMENT '前端主题配置(JSON)',
    `login_config` JSON COMMENT '登录页定制配置(JSON)',
    `max_users` INT(11) DEFAULT 100 COMMENT '最大用户数限制',
    `max_enterprises` INT(11) DEFAULT 1000 COMMENT '最大企业数限制',
    `expire_date` DATE DEFAULT NULL COMMENT '服务到期日期',
    `CreateID` INT(11) DEFAULT NULL COMMENT '创建人',
    `CreateDate` DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` INT(11) DEFAULT NULL COMMENT '更新人',
    `ModifyDate` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    `DeleteID` INT(11) DEFAULT NULL COMMENT '删除人',
    `DeleteTime` DATETIME DEFAULT NULL COMMENT '删除时间',
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_org_code` (`OrgCode`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_org_status` (`Status`),
    KEY `idx_org_type` (`org_type`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='认证机构配置表';

-- ============================================================
-- 第三部分：创建注册申请表 (cert_registration)
-- ============================================================

CREATE TABLE IF NOT EXISTS `cert_registration` (
    `Id` BIGINT(20) NOT NULL AUTO_INCREMENT COMMENT '主键ID',
    `Code` CHAR(36) NOT NULL COMMENT 'GUID编码',
    `registration_no` VARCHAR(50) NOT NULL COMMENT '申请编号',
    `org_name` VARCHAR(200) NOT NULL COMMENT '机构/企业名称',
    `registration_type` TINYINT NOT NULL COMMENT '注册类型：1=认证机构, 2=企业用户',
    `ContactName` VARCHAR(100) NOT NULL COMMENT '联系人',
    `ContactPhone` VARCHAR(20) NOT NULL COMMENT '联系电话',
    `ContactEmail` VARCHAR(200) DEFAULT NULL COMMENT '联系邮箱',
    `org_type` TINYINT DEFAULT NULL COMMENT '机构类型（认证机构时必填）',
    `business_license` VARCHAR(500) DEFAULT NULL COMMENT '营业执照URL',
    `qualification_files` JSON COMMENT '资质文件列表(JSON)',
    `Status` TINYINT NOT NULL DEFAULT 0 COMMENT '状态：0=待审核, 1=已通过, 2=已拒绝, 3=已撤销',
    `audit_by` INT(11) DEFAULT NULL COMMENT '审核人',
    `audit_time` DATETIME DEFAULT NULL COMMENT '审核时间',
    `audit_remark` VARCHAR(1000) DEFAULT NULL COMMENT '审核备注',
    `CreateID` INT(11) DEFAULT NULL COMMENT '创建人',
    `CreateDate` DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` INT(11) DEFAULT NULL COMMENT '更新人',
    `ModifyDate` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    `DeleteID` INT(11) DEFAULT NULL COMMENT '删除人',
    `DeleteTime` DATETIME DEFAULT NULL COMMENT '删除时间',
    PRIMARY KEY (`Id`),
    UNIQUE KEY `uk_registration_no` (`registration_no`),
    UNIQUE KEY `uk_code` (`Code`),
    KEY `idx_reg_type` (`registration_type`),
    KEY `idx_reg_status` (`Status`),
    KEY `idx_reg_create_time` (`CreateDate`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='注册申请表';

-- ============================================================
-- 第四部分：为现有业务表添加 Org_Code 字段（多租户支持）
-- ============================================================

-- 认证体系配置相关表
ALTER TABLE `cert_certification_body` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `Remark`;
ALTER TABLE `cert_certification_body` ADD INDEX `idx_cb_org_code` (`OrgCode`);

ALTER TABLE `cert_certification_system` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `Description`;
ALTER TABLE `cert_certification_system` ADD INDEX `idx_cs_org_code` (`OrgCode`);

ALTER TABLE `cert_certification_scope` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `risk_level`;
ALTER TABLE `cert_certification_scope` ADD INDEX `idx_cscope_org_code` (`OrgCode`);

-- 审核流程相关表
ALTER TABLE `cert_audit_task` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `ActualEndDate`;
ALTER TABLE `cert_audit_task` ADD INDEX `idx_at_org_code` (`OrgCode`);

ALTER TABLE `cert_audit_team` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `Status`;
ALTER TABLE `cert_audit_team` ADD INDEX `idx_ateam_org_code` (`OrgCode`);

ALTER TABLE `cert_audit_schedule` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `location`;
ALTER TABLE `cert_audit_schedule` ADD INDEX `idx_aschedule_org_code` (`OrgCode`);

ALTER TABLE `cert_audit_finding` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `verify_date`;
ALTER TABLE `cert_audit_finding` ADD INDEX `idx_afinding_org_code` (`OrgCode`);

ALTER TABLE `cert_nc_record` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `close_date`;
ALTER TABLE `cert_nc_record` ADD INDEX `idx_nc_org_code` (`OrgCode`);

-- 企业与项目相关表
ALTER TABLE `cert_enterprise_info` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `export_status`;
ALTER TABLE `cert_enterprise_info` ADD INDEX `idx_ei_org_code` (`OrgCode`);

ALTER TABLE `cert_contract` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `total_amount`;
ALTER TABLE `cert_contract` ADD INDEX `idx_contract_org_code` (`OrgCode`);

ALTER TABLE `cert_project` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `stage`;
ALTER TABLE `cert_project` ADD INDEX `idx_project_org_code` (`OrgCode`);

-- 文件与报告相关表
ALTER TABLE `cert_document_template` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `Version`;
ALTER TABLE `cert_document_template` ADD INDEX `idx_dt_org_code` (`OrgCode`);

ALTER TABLE `cert_file_requirement` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `IsRequired`;
ALTER TABLE `cert_file_requirement` ADD INDEX `idx_fr_org_code` (`OrgCode`);

ALTER TABLE `cert_uploaded_file` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `FileSize`;
ALTER TABLE `cert_uploaded_file` ADD INDEX `idx_uf_org_code` (`OrgCode`);

ALTER TABLE `cert_audit_report` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `approval_date`;
ALTER TABLE `cert_audit_report` ADD INDEX `idx_ar_org_code` (`OrgCode`);

ALTER TABLE `cert_report_task` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `DueDate`;
ALTER TABLE `cert_report_task` ADD INDEX `idx_rt_org_code` (`OrgCode`);

-- 工作流相关表
ALTER TABLE `wf_process_instance` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `form_data`;
ALTER TABLE `wf_process_instance` ADD INDEX `wf_pi_org_code` (`OrgCode`);

ALTER TABLE `wf_task` ADD COLUMN `OrgCode` VARCHAR(50) DEFAULT NULL COMMENT '所属机构编码' AFTER `form_url`;
ALTER TABLE `wf_task` ADD INDEX `wf_task_org_code` (`OrgCode`);

-- ============================================================
-- 第五部分：创建完整的角色体系（9个角色）
-- ============================================================

-- 清理可能存在的旧角色数据（可选，谨慎使用）
-- DELETE FROM sys_role WHERE role_id IN (100,101,102,103,104,200,201,202,300);

-- Layer 1: 平台管理层（5个角色）
INSERT INTO `sys_role` (`RoleId`, `RoleName`, `parent_id`, `Enable`, `CreateID`, `CreateDate`, `ModifyID`, `modify_time`, `order_no`, `delete_reason`) VALUES
(100, '超级管理员', 0, 1, 0, NOW(), 0, NOW(), 1, NULL),
(101, '总管理员', 0, 1, 0, NOW(), 0, NOW(), 2, NULL),
(102, '运维人员', 0, 1, 0, NOW(), 0, NOW(), 3, NULL),
(103, '配置人员', 0, 1, 0, NOW(), 0, NOW(), 4, NULL),
(104, '质量专员', 0, 1, 0, NOW(), 0, NOW(), 5, NULL);

-- Layer 2: 机构管理层（3个角色）
INSERT INTO `sys_role` (`RoleId`, `RoleName`, `parent_id`, `Enable`, `CreateID`, `CreateDate`, `ModifyID`, `modify_time`, `order_no`, `delete_reason`) VALUES
(200, '审核管理员', 0, 1, 0, NOW(), 0, NOW(), 10, NULL),
(201, '审核组长', 0, 1, 0, NOW(), 0, NOW(), 11, NULL),
(202, '普通审核员', 0, 1, 0, NOW(), 0, NOW(), 12, NULL);

-- Layer 3: 企业层（1个角色）
INSERT INTO `sys_role` (`RoleId`, `RoleName`, `parent_id`, `Enable`, `CreateID`, `CreateDate`, `ModifyID`, `modify_time`, `order_no`, `delete_reason`) VALUES
(300, '企业账号', 0, 1, 0, NOW(), 0, NOW(), 20, NULL);

-- ============================================================
-- 第六部分：创建平台管理部门树（组织架构）
-- ============================================================

-- 清理旧部门数据（可选）
-- DELETE FROM sys_department WHERE Id >= 100;

INSERT INTO `sys_department` (`Id`, `department_name`, `department_code`, `parent_id`, `Enable`, `CreateID`, `CreateDate`, `ModifyID`, `modify_time`) VALUES
(100, '体系认证平台总部', 'PLATFORM_HQ', 0, 1, 0, NOW(), 0, NOW()),
(101, '运维部', 'OPS_DEPT', 100, 1, 0, NOW(), 0, NOW()),
(102, '配置管理部', 'CONFIG_DEPT', 100, 1, 0, NOW(), 0, NOW()),
(103, '质量管理部', 'QA_DEPT', 100, 1, 0, NOW(), 0, NOW());

-- ============================================================
-- 第七部分：创建测试用户（每个角色一个示例用户）
-- ============================================================

-- 更新超级管理员账号（user_type=1, OrgCode=NULL 表示平台最高权限）
UPDATE `sys_user` SET `UserType` = 1 WHERE `User_Id` = 1;

-- 创建总管理员测试账号
INSERT INTO `sys_user` (
    `UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`,
    `token`, `AppType`, `AuditStatus`, `Enable`, `modify_password`,
    `UserType`, `OrgCode`, `Dept_Id`, `email`, `Mobile`,
    `CreateID`, `CreateDate`
) VALUES (
    'admin_manager', 
    'fAiqPZF6bVj4G7+qJcVaLQ==',  -- 密码: 123456
    '总管理员',
    101,
    '总管理员',
    UUID(),
    1,
    2,
    1,
    0,
    10,  -- user_type = 总管理员
    NULL,  -- OrgCode = NULL 表示平台管理层
    100,  -- 部门：体系认证平台总部
    'admin@certplatform.com',
    '13800000001',
    1,
    NOW()
);

-- 创建运维人员测试账号
INSERT INTO `sys_user` (
    `UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`,
    `token`, `AppType`, `AuditStatus`, `Enable`, `modify_password`,
    `UserType`, `OrgCode`, `Dept_Id`, `email`, `Mobile`,
    `CreateID`, `CreateDate`
) VALUES (
    'ops_user', 
    'fAiqPZF6bVj4G7+qJcVaLQ==',  -- 密码: 123456
    '运维人员',
    102,
    '运维人员',
    UUID(),
    1,
    2,
    1,
    0,
    13,  -- user_type = 运维人员
    NULL,
    101,  -- 部门：运维部
    'ops@certplatform.com',
    '13800000002',
    1,
    NOW()
);

-- 创建配置人员测试账号
INSERT INTO `sys_user` (
    `UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`,
    `token`, `AppType`, `AuditStatus`, `Enable`, `modify_password`,
    `UserType`, `OrgCode`, `Dept_Id`, `email`, `Mobile`,
    `CreateID`, `CreateDate`
) VALUES (
    'config_user', 
    'fAiqPZF6bVj4G7+qJcVaLQ==',  -- 密码: 123456
    '配置人员',
    103,
    '配置人员',
    UUID(),
    1,
    2,
    1,
    0,
    14,  -- user_type = 配置人员
    NULL,
    102,  -- 部门：配置管理部
    'config@certplatform.com',
    '13800000003',
    1,
    NOW()
);

-- 创建质量专员测试账号
INSERT INTO `sys_user` (
    `UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`,
    `token`, `AppType`, `AuditStatus`, `Enable`, `modify_password`,
    `UserType`, `OrgCode`, `Dept_Id`, `email`, `Mobile`,
    `CreateID`, `CreateDate`
) VALUES (
    'qa_user', 
    'fAiqPZF6bVj4G7+qJcVaLQ==',  -- 密码: 123456
    '质量专员',
    104,
    '质量专员',
    UUID(),
    1,
    2,
    1,
    0,
    15,  -- user_type = 质量专员
    NULL,
    103,  -- 部门：质量管理部
    'qa@certplatform.com',
    '13800000004',
    1,
    NOW()
);

-- ============================================================
-- 第八部分：创建示例认证机构和审核员账号
-- ============================================================

-- 插入示例认证机构
INSERT INTO `cert_org_config` (
    `Code`, `OrgCode`, `org_name`, `org_short_name`, `org_type`,
    `registration_no`, `LegalPerson`, `ContactPhone`, `ContactEmail`,
    `Address`, `Status`, `max_users`, `max_enterprises`
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

-- 创建审核管理员账号（属于 CB001 机构）
INSERT INTO `sys_user` (
    `UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`,
    `token`, `AppType`, `AuditStatus`, `Enable`, `modify_password`,
    `UserType`, `OrgCode`, `OrgId`, `email`, `Mobile`,
    `UserTrueName`, `CreateID`, `CreateDate`
) SELECT
    'cb001_admin',
    'fAiqPZF6bVj4G7+qJcVaLQ==',  -- 密码: 123456
    '审核管理员',
    200,
    '审核管理员',
    UUID(),
    1,
    2,
    1,
    0,
    20,  -- user_type = 审核管理员
    'CB001',
    Id,  -- org_id 从刚插入的机构获取
    'admin@shanglong.cn',
    '13900000001',
    '李四（尚龙认证管理员）',
    1,
    NOW()
FROM `cert_org_config` WHERE `OrgCode` = 'CB001' LIMIT 1;

-- 创建审核组长账号
INSERT INTO `sys_user` (
    `UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`,
    `token`, `AppType`, `AuditStatus`, `Enable`, `modify_password`,
    `UserType`, `OrgCode`, `OrgId`, `email`, `Mobile`,
    `UserTrueName`, `CreateID`, `CreateDate`
) SELECT
    'cb001_leader',
    'fAiqPZF6bVj4G7+qJcVaLQ==',  -- 密码: 123456
    '审核组长',
    201,
    '审核组长',
    UUID(),
    1,
    2,
    1,
    0,
    21,  -- user_type = 审核组长
    'CB001',
    Id,
    'leader@shanglong.cn',
    '13900000002',
    '王五（尚龙审核组长）',
    1,
    NOW()
FROM `cert_org_config` WHERE `OrgCode` = 'CB001' LIMIT 1;

-- 创建普通审核员账号
INSERT INTO `sys_user` (
    `UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`,
    `token`, `AppType`, `AuditStatus`, `Enable`, `modify_password`,
    `UserType`, `OrgCode`, `OrgId`, `email`, `Mobile`,
    `UserTrueName`, `CreateID`, `CreateDate`
) SELECT
    'cb001_auditor',
    'fAiqPZF6bVj4G7+qJcVaLQ==',  -- 密码: 123456
    '普通审核员',
    202,
    '普通审核员',
    UUID(),
    1,
    2,
    1,
    0,
    22,  -- user_type = 普通审核员
    'CB001',
    Id,
    'auditor@shanglong.cn',
    '13900000003',
    '赵六（尚龙审核员）',
    1,
    NOW()
FROM `cert_org_config` WHERE `OrgCode` = 'CB001' LIMIT 1;

-- ============================================================
-- 第九部分：创建企业用户测试账号
-- ============================================================

INSERT INTO `sys_user` (
    `UserName`, `UserPwd`, `UserTypeName`, `RoleId`, `RoleName`,
    `token`, `AppType`, `AuditStatus`, `Enable`, `modify_password`,
    `UserType`, `OrgCode`, `OrgId`, `email`, `Mobile`,
    `UserTrueName`, `CreateID`, `CreateDate`
) SELECT
    'ent001_user',
    'fAiqPZF6bVj4G7+qJcVaLQ==',  -- 密码: 123456
    '企业账号',
    300,
    '企业账号',
    UUID(),
    1,
    2,
    1,
    0,
    30,  -- user_type = 企业账号
    'CB001',
    Id,
    'ent@testcompany.com',
    '13700000001',
    '孙七（测试企业管理员）',
    1,
    NOW()
FROM `cert_org_config` WHERE `OrgCode` = 'CB001' LIMIT 1;

-- ============================================================
-- 第十部分：配置角色权限矩阵
-- 说明：
--   - 超级管理员(100): 所有菜单权限
--   - 总管理员(101): 除系统监控外的所有菜单
--   - 运维人员(102): 系统设置、日志、监控
--   - 配置人员(103): 基础配置、认证体系、机构管理
--   - 质量专员(104): 审核管理、报告、统计分析
--   - 审核管理员(200): 本机构的全部功能
--   - 审核组长(201): 审核、团队、报告
--   - 普通审核员(202): 分配给自己的任务和文件
--   - 企业账号(300): 仅企业自助服务
-- ============================================================

-- 注意：这里需要根据实际的菜单ID来配置权限
-- 由于菜单ID可能因环境不同而变化，下面提供基于菜单名称的查询方式
-- 实际部署时需要根据 sys_menu 表中的真实 ID 进行调整

-- 示例：为超级管理员分配所有菜单权限（假设体系认证模块的父菜单ID为304）
/*
INSERT INTO sys_roleauth (Id, role_id, module_id, module_type, permission, Enable, CreateID, CreateDate)
SELECT 
    NULL,
    100,
    menu_id,
    1,  -- menu
    'View,Add,Edit,Delete,Export,Upload,Search,Audit',
    1,
    1,
    NOW()
FROM sys_menu 
WHERE parent_id = 304 OR Id = 304;
*/

-- 提示信息
SELECT '✅ 数据库扩展完成！请继续执行权限配置脚本。' AS message;
SELECT '📋 测试账号清单：' AS info;
SELECT '  - 超级管理员: admin / 123456 (已有)' AS account UNION ALL
SELECT '  - 总管理员: admin_manager / 123456' UNION ALL
SELECT '  - 运维人员: ops_user / 123456' UNION ALL
SELECT '  - 配置人员: config_user / 123456' UNION ALL
SELECT '  - 质量专员: qa_user / 123456' UNION ALL
SELECT '  - 审核管理员(CB001): cb001_admin / 123456' UNION ALL
SELECT '  - 审核组长(CB001): cb001_leader / 123456' UNION ALL
SELECT '  - 普通审核员(CB001): cb001_auditor / 123456' UNION ALL
SELECT '  - 企业账号: ent001_user / 123456';
