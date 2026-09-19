-- ========================================================================
-- 2026-09-19: 统一审计字段列名为 PascalCase（DB列名 = C#属性名 = PascalCase）
-- 
-- 铁律：YZH 架构要求数据库列名与 C# 属性名完全一致（PascalCase）
-- 
-- 状态：sys_organization/sys_user/sys_dictionary/sys_dictionarylist/sys_role
--       的审计列已改为 PascalCase，只需删除旧 ID 列
--       wf_prompt_template 仍为 snake_case，需要修正
-- ========================================================================

-- ========================================================================
-- 一、删除旧 ID 列（YZH 架构关联走 Code，不需要 ID 列）
-- ========================================================================
ALTER TABLE Sys_Organization DROP COLUMN CreateID;
ALTER TABLE Sys_Organization DROP COLUMN ModifyID;
ALTER TABLE Sys_Organization DROP COLUMN DeleteID;

ALTER TABLE Sys_User DROP COLUMN CreateID;
ALTER TABLE Sys_User DROP COLUMN ModifyID;

-- ========================================================================
-- 二、wf_prompt_template（Prompt 模板）- snake_case → PascalCase
-- ========================================================================
-- 同时存在 creator/create_by, modifier/update_by, deleter/delete_by
-- 策略：合并数据到标准列，删除重复列

-- 步骤 1: 合并数据
UPDATE wf_prompt_template SET create_by = COALESCE(create_by, creator) WHERE creator IS NOT NULL;
UPDATE wf_prompt_template SET update_by = COALESCE(update_by, modifier) WHERE modifier IS NOT NULL;
UPDATE wf_prompt_template SET delete_by = COALESCE(delete_by, deleter) WHERE deleter IS NOT NULL;

-- 步骤 2: RENAME 为 PascalCase
ALTER TABLE wf_prompt_template 
    CHANGE COLUMN create_by CreateBy varchar(50) NULL,
    CHANGE COLUMN create_date CreateTime datetime NULL DEFAULT CURRENT_TIMESTAMP,
    CHANGE COLUMN update_by UpdateBy varchar(50) NULL,
    CHANGE COLUMN modify_date UpdateTime datetime NULL,
    CHANGE COLUMN delete_by DeleteBy varchar(50) NULL,
    CHANGE COLUMN delete_time DeleteTime datetime NULL;

-- 步骤 3: 删除旧重复列
ALTER TABLE wf_prompt_template DROP COLUMN creator;
ALTER TABLE wf_prompt_template DROP COLUMN modifier;
ALTER TABLE wf_prompt_template DROP COLUMN deleter;

-- ========================================================================
-- 三、校验（执行后运行）
-- ========================================================================
-- 检查是否还有非 PascalCase 审计列
SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = 'yzh_cert_platform'
  AND (
    column_name IN ('Creator', 'CreateDate', 'Modifier', 'ModifyDate', 'Deleter', 'CreateID', 'ModifyID', 'DeleteID', 'ParentId')
    OR column_name REGEXP '^(creator|create_date|modifier|modify_date|deleter|delete_time|create_by|update_by|delete_by)$'
  )
ORDER BY table_name, column_name;
