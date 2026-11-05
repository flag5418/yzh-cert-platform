-- ====================================================================
-- 20261105_fix_organization_zerodate_V1.sql
-- 修复：专家端企业管理过滤器报 "Sys_Organization UpdateTime bind error"
-- 根因：Program.cs CodeFirst AutoCreate=true 重建全库后，
--       ① Sys_Organization 表的 UpdateTime（C# DateTime?）被错误建为 datetime NOT NULL；
--       ② INSERT 时未显式写 UpdateTime → MySQL 写入零日期 0000-00-00 00:00:00；
--       ③ WorkspaceContextService.Resolve() 查 Sys_Organization 时 SqlSugar
--          尝试映射 UpdateTime = 0000-00-00 → .NET 无法表示 → "bind error"。
-- 幂等：先改列属性（允许 NULL）再清零日期，重复运行安全。
-- ====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ★ 步骤顺序：先改列属性，再清零日期（若在 NOT NULL 列上 SET NULL，非严格模式会静默转为 0000-00-00）

-- ① 修正列的 NULL 属性：DateTime? 对应的列必须允许 NULL
ALTER TABLE Sys_Organization
  MODIFY UpdateTime datetime NULL,
  MODIFY DeleteTime datetime NULL;

-- ② 临时清空 sql_mode，以便按值条件定位零日期行
SET sql_mode = '';

UPDATE Sys_Organization SET UpdateTime = NULL WHERE UpdateTime < '1000-01-01';
UPDATE Sys_Organization SET DeleteTime = NULL WHERE DeleteTime < '1000-01-01';

-- ③ 恢复严格模式（后续 INSERT 若仍不显式写时间，框架会 DateTime.Now 填补，不会再生零日期）
SET sql_mode = 'STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';

-- ④ 验评：确认零日期已清零
SELECT 'Sys_Organization zero-date check' AS CheckPoint,
       CONVERT(COUNT(*) USING utf8mb4) COLLATE utf8mb4_bin AS ZeroDateRows
FROM Sys_Organization
WHERE (UpdateTime IS NOT NULL AND UpdateTime < '1000-01-01')
   OR (DeleteTime IS NOT NULL AND DeleteTime < '1000-01-01');

-- ⑤ 验评：列属性确认
SELECT COLUMN_NAME, IS_NULLABLE, DATA_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND TABLE_NAME = 'Sys_Organization'
  AND COLUMN_NAME IN ('UpdateTime', 'CreateTime', 'DeleteTime');
