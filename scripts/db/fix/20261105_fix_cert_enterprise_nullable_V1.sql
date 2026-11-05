-- ====================================================================
-- 20261105_fix_cert_enterprise_nullable_V1.sql
-- 修复：新增企业报「必填字段为空」
-- 根因：CodeFirst 建表时未识别 C# 可空类型（string? / int? / DateTime?），
--       将全部列建为 datetime/varchar/int NOT NULL；前端提交 null 值时
--       MySQL 拒绝写入 → SqlSugarDbOrm.FormatError 将 "Cannot insert NULL"
--       归一化成「新增失败：必填字段为空」。
-- 修复：按 Enterprise.cs 的类型定义，把可空属性对应列改为 NULL。
-- 幂等：ALTER TABLE ... MODIFY ... 重复执行无副作用。
-- ====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ① 审计字段（BaseEntity 声明为可空）
ALTER TABLE cert_enterprise
  MODIFY Code       varchar(255)  NULL,
  MODIFY CreateBy   varchar(255)  NULL,
  MODIFY UpdateTime datetime      NULL,
  MODIFY UpdateBy   varchar(255)  NULL,
  MODIFY DeleteTime datetime      NULL,
  MODIFY DeleteBy   varchar(255)  NULL;

-- ② 业务字段（C# 声明为 string? / int? / DateTime? 的必须允许 NULL）
ALTER TABLE cert_enterprise
  MODIFY ShortName     varchar(255)  NULL,
  MODIFY CreditCode    varchar(255)  NULL,
  MODIFY LegalPerson   varchar(255)  NULL,
  MODIFY Province      varchar(255)  NULL,
  MODIFY City          varchar(255)  NULL,
  MODIFY Address       varchar(255)  NULL,
  MODIFY IndustryType  varchar(255)  NULL,
  MODIFY EmployeeCount int           NULL,
  MODIFY ContactName   varchar(255)  NULL,
  MODIFY ContactPhone  varchar(255)  NULL,
  MODIFY ContactEmail  varchar(255)  NULL,
  MODIFY ArchiveDate   datetime      NULL;

/*
  保持 NOT NULL 的列（Entity 声明为必须 / 框架约束）：
    Id, OrgCode, EnterpriseNo, Name, CertScope, Remark, Sort,
    IsValid, IsDeleted, CreateTime

  注意：OrgCode / EnterpriseNo 由 OnBeforeAdd 服务端填充，前端不传也没问题。
*/

-- ③ 验评：可空列属性确认
SELECT COLUMN_NAME, IS_NULLABLE, DATA_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND TABLE_NAME = 'cert_enterprise'
  AND COLUMN_NAME IN (
    'Code','ShortName','CreditCode','LegalPerson','Province','City','Address',
    'IndustryType','EmployeeCount','ContactName','ContactPhone','ContactEmail',
    'ArchiveDate','CreateBy','UpdateBy','UpdateTime','DeleteBy','DeleteTime'
  )
ORDER BY ORDINAL_POSITION;
