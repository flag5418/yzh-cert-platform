-- ============================================================================
-- 20261110_status_column_default_V1.sql
-- 修：三张 V3 表的 Status 列 NOT NULL 无默认值 ⇒ 直插报 1364「必填字段为空」
--
-- 症状：ClauseAttrGenerateService 逐属性直插 cert_clause_attr 时，
--       实体没有给 Status（框架 AddCore 也不填）⇒ sql_mode=STRICT_TRANS_TABLES 下
--       报 `Field 'Status' doesn't have a default value`，接口只能回「新增失败：必填字段为空」。
-- 口径：Status 是历史遗留列（业务语义已由 IsValid / IsDeleted 承载），
--       统一给 DEFAULT 'active'（与既有 27 行取值一致），代码侧也显式赋值（双保险）。
-- 幂等：可重复执行
-- ============================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

ALTER TABLE cert_clause_attr      MODIFY COLUMN Status varchar(255) NOT NULL DEFAULT 'active' COLLATE utf8mb4_general_ci;
ALTER TABLE cert_clause_attr_def  MODIFY COLUMN Status varchar(255) NOT NULL DEFAULT 'active' COLLATE utf8mb4_general_ci;
ALTER TABLE cert_prompt_template  MODIFY COLUMN Status varchar(255) NOT NULL DEFAULT 'active' COLLATE utf8mb4_general_ci;

SELECT TABLE_NAME, COLUMN_DEFAULT, IS_NULLABLE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND COLUMN_NAME = 'Status'
  AND TABLE_NAME IN ('cert_clause_attr','cert_clause_attr_def','cert_prompt_template');
