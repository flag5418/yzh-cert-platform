-- ============================================================================
-- SQL 脚本：为所有认证平台业务表添加 status 字段到基类
-- 说明：YZHBaseEntity 新增 Status 字段，需要在所有继承该基类的表中添加
-- 执行时间：2026-07-31
-- ============================================================================

-- 1. 认证机构表
ALTER TABLE cert_certification_body 
ADD COLUMN status VARCHAR(50) DEFAULT 'active' COMMENT '业务状态' AFTER cb_code;

-- 2. ISO 标准表
ALTER TABLE cert_iso_standard 
ADD COLUMN status VARCHAR(50) DEFAULT 'active' COMMENT '业务状态' AFTER standard_code;

-- 3. 认证申请表
ALTER TABLE cert_application 
ADD COLUMN status VARCHAR(50) DEFAULT 'draft' COMMENT '业务状态' AFTER application_no;

-- 4. 审核任务表
ALTER TABLE cert_audit_task 
ADD COLUMN status VARCHAR(50) DEFAULT 'pending' COMMENT '业务状态' AFTER task_number;

-- 5. 企业信息表
ALTER TABLE cert_enterprise 
ADD COLUMN status VARCHAR(50) DEFAULT 'active' COMMENT '业务状态' AFTER ent_code;

-- 6. 审核员资质表
ALTER TABLE cert_auditor_qualification 
ADD COLUMN status VARCHAR(50) DEFAULT 'active' COMMENT '业务状态' AFTER qualification_code;

-- 7. 不符合项表
ALTER TABLE cert_non_conformity 
ADD COLUMN status VARCHAR(50) DEFAULT 'open' COMMENT '业务状态' AFTER nc_code;

-- 8. 审核报告表
ALTER TABLE cert_audit_report 
ADD COLUMN status VARCHAR(50) DEFAULT 'draft' COMMENT '业务状态' AFTER report_code;

-- 9. 文件要求表
ALTER TABLE cert_file_requirement 
ADD COLUMN status VARCHAR(50) DEFAULT 'active' COMMENT '业务状态' AFTER requirement_code;

-- 10. 报告任务表
ALTER TABLE cert_report_task 
ADD COLUMN status VARCHAR(50) DEFAULT 'pending' COMMENT '业务状态' AFTER task_code;

-- ============================================================================
-- 验证：检查所有表是否已添加 status 字段
-- ============================================================================
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    COLUMN_DEFAULT,
    COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
    AND TABLE_NAME LIKE 'cert_%'
    AND COLUMN_NAME = 'status';
