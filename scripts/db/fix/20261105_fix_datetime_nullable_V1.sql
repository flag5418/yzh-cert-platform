-- =================================================================--
-- 20261105_fix_datetime_nullable_V1.sql — CodeFirst DateTime? 列 NOT NULL 修正
--
-- 问题：Sys_User 等表的 LastLoginDate/LastModifyPwdDate/DeleteTime 等
--       C# DateTime? 列被 CodeFirst 错误生成为 `datetime NOT NULL`，
--       导致 .NET 读取时报 "LastLoginDate bind error"。
--       另外 Token 列 CodeFirst 生成 varchar(255)，实际 JWT 密文 > 300 字符
--       → 登录写 Token 时报 "Data too long for column 'Token'"。
-- 修正：DateTime? 列改为允许 NULL；Token 列改宽到 varchar(1000)。幂等。
-- ===================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 1) Sys_User：4 个 DateTime? 列放宽为 NULL（先放宽才能把非法值写为 NULL）
ALTER TABLE Sys_User
  MODIFY LastLoginDate datetime NULL,
  MODIFY LastModifyPwdDate datetime NULL,
  MODIFY UpdateTime datetime NULL,
  MODIFY DeleteTime datetime NULL;

-- 2) 清理 0000-00-00（SQL 严格模式禁止该值用于条件，改用 UpdateTime IS NULL 兜底）
--    这些列刚改为 NULL，已有的非法值需用特殊方式清除
SET sql_mode = '';
UPDATE Sys_User SET LastLoginDate = NULL WHERE LastLoginDate < '1000-01-01';
UPDATE Sys_User SET LastModifyPwdDate = NULL WHERE LastModifyPwdDate < '1000-01-01';
UPDATE Sys_User SET UpdateTime = NULL WHERE UpdateTime < '1000-01-01';
UPDATE Sys_User SET DeleteTime = NULL WHERE DeleteTime < '1000-01-01';

-- 3) Token 列改宽（CodeFirst 默认 varchar(255)，JWT 实际 ~400 字符）
ALTER TABLE Sys_User MODIFY Token varchar(1000) NULL;

SELECT '✅ Sys_User datetime + Token 列修正完成' AS result;
