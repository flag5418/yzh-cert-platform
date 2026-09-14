-- ====================================================
-- 全面修复 cert_* 表的审计字段（BaseEntity 标准）
-- 日期: 2026-09-13
-- 目标: 补齐剩余6张表的缺失审计列
-- 前提: 已通过前面脚本修复大部分表
-- ====================================================

-- cert_ai_config: 已有 CreateBy/CreateTime/UpdateBy/UpdateTime/DeleteBy/DeleteTime/IsValid，仅缺 IsDeleted
ALTER TABLE cert_ai_config ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0 AFTER DeleteTime;

-- cert_ai_usage_log: 缺全部审计列（目前只有 CreateTime + IsValid）
ALTER TABLE cert_ai_usage_log ADD COLUMN CreateBy varchar(50) NULL AFTER CreateTime;
ALTER TABLE cert_ai_usage_log ADD COLUMN UpdateBy varchar(50) NULL AFTER CreateBy;
ALTER TABLE cert_ai_usage_log ADD COLUMN UpdateTime datetime NULL AFTER UpdateBy;
ALTER TABLE cert_ai_usage_log ADD COLUMN DeleteBy varchar(50) NULL AFTER UpdateTime;
ALTER TABLE cert_ai_usage_log ADD COLUMN DeleteTime datetime NULL AFTER DeleteBy;
ALTER TABLE cert_ai_usage_log ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0 AFTER DeleteTime;

-- cert_application: 已有 CreateBy/CreateTime/UpdateBy/UpdateTime/IsValid，缺 DeleteBy/DeleteTime/IsDeleted
ALTER TABLE cert_application ADD COLUMN DeleteBy varchar(50) NULL AFTER UpdateTime;
ALTER TABLE cert_application ADD COLUMN DeleteTime datetime NULL AFTER DeleteBy;
ALTER TABLE cert_application ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0 AFTER DeleteTime;

-- cert_enterprise: 已有 CreateBy/CreateTime/UpdateBy/UpdateTime/IsValid，缺 DeleteBy/DeleteTime/IsDeleted
ALTER TABLE cert_enterprise ADD COLUMN DeleteBy varchar(50) NULL AFTER UpdateTime;
ALTER TABLE cert_enterprise ADD COLUMN DeleteTime datetime NULL AFTER DeleteBy;
ALTER TABLE cert_enterprise ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0 AFTER DeleteTime;

-- cert_message: 缺大部分审计列（目前只有 CreateTime + IsValid）
ALTER TABLE cert_message ADD COLUMN CreateBy varchar(50) NULL AFTER CreateTime;
ALTER TABLE cert_message ADD COLUMN UpdateBy varchar(50) NULL AFTER CreateBy;
ALTER TABLE cert_message ADD COLUMN UpdateTime datetime NULL AFTER UpdateBy;
ALTER TABLE cert_message ADD COLUMN DeleteBy varchar(50) NULL AFTER UpdateTime;
ALTER TABLE cert_message ADD COLUMN DeleteTime datetime NULL AFTER DeleteBy;
ALTER TABLE cert_message ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0 AFTER DeleteTime;

-- cert_upload_task: 已有 CreateBy/CreateTime/UpdateTime，缺 UpdateBy/DeleteBy/DeleteTime/IsDeleted
ALTER TABLE cert_upload_task ADD COLUMN UpdateBy varchar(50) NULL AFTER UpdateTime;
ALTER TABLE cert_upload_task ADD COLUMN DeleteBy varchar(50) NULL AFTER UpdateBy;
ALTER TABLE cert_upload_task ADD COLUMN DeleteTime datetime NULL AFTER DeleteBy;
ALTER TABLE cert_upload_task ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0 AFTER DeleteTime;
