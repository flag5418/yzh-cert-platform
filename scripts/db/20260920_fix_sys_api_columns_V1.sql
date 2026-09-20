-- 修复 sys_api / sys_role_api / sys_user_permission 列名（snake_case → PascalCase）
-- Date: 2026-09-20
-- 规则：DB / C# / TS 字段必须全部 PascalCase，禁止任何 snake_case

ALTER TABLE sys_api
  CHANGE COLUMN update_date UpdateTime datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP;

ALTER TABLE sys_role_api
  CHANGE COLUMN role_code RoleCode varchar(50) NOT NULL,
  CHANGE COLUMN api_code   ApiCode   varchar(64) NOT NULL;

ALTER TABLE sys_user_permission
  CHANGE COLUMN user_code  UserCode  varchar(36) NOT NULL,
  CHANGE COLUMN api_code   ApiCode   varchar(64) NOT NULL,
  CHANGE COLUMN update_date UpdateTime datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP;
