-- ====================================================================
-- 20261105_fix_sys_organization_nullable_V1.sql
-- 修复：创建「企业信息」文件夹报「必填字段为空」
-- 根因：CodeFirst 建 Sys_Organization 时将所有列建为 NOT NULL；
--       EnsureEnterpriseGroupAsync 新建文件夹时 LeaderName/LeaderPhone 未赋值（C# null），
--       写入 NOT NULL 列时 MySQL 拒绝 → 归一化成「新增失败：必填字段为空」。
-- 前置：fix_organization_zerodate_V1.sql 已改 UpdateTime/DeleteTime。
-- ====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

ALTER TABLE Sys_Organization
  MODIFY OrgCode    varchar(255) NULL,   -- string?
  MODIFY ParentCode varchar(255) NULL,   -- string?
  MODIFY OrgType    varchar(255) NULL,   -- string?
  MODIFY OrgLevel   int          NULL,   -- int?
  MODIFY OrgPath    varchar(255) NULL,   -- string?
  MODIFY LeaderName varchar(255) NULL,   -- string?
  MODIFY LeaderPhone varchar(255) NULL,  -- string?
  MODIFY Sort       int          NULL,   -- int?
  MODIFY Remark     varchar(500) NULL,   -- string?
  MODIFY CreateBy   varchar(255) NULL,   -- string?
  MODIFY UpdateBy   varchar(255) NULL,   -- string?
  MODIFY DeleteBy   varchar(255) NULL;   -- string?

/*
  保持 NOT NULL 的列（Entity 声明为必须 / 框架约束）：
    Id, Code, OrgName, IsValid, IsDeleted, CreateTime, DeleteTime(?)
    注：DeleteTime 由先行脚本 fix_organization_zerodate_V1.sql 改为 NULL。
*/

-- 验评
SELECT COLUMN_NAME, IS_NULLABLE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND TABLE_NAME = 'Sys_Organization'
ORDER BY ORDINAL_POSITION;
