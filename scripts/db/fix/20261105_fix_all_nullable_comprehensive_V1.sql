-- ====================================================================
-- 20261105_fix_all_nullable_comprehensive_V1.sql
-- 修复：CodeFirst 重建表后所有列 NOT NULL 的普遍问题
--
-- 根因：某次 CodeFirst 初始化时，SqlSugar 的 CodeFirstSyncTable 未识别
--       C# 可空类型（string?/int?/DateTime?），把所有列建为 NOT NULL；
--       前端传 null 时 MySQL 拒绝 → 「新增失败：必填字段为空」。
--
-- 修复策略：动态生成 + 批量执行 ALTER TABLE，覆盖全库。
-- 幂等：IF IS_NULLABLE='NO' 才生成 MODIFY ... NULL。
-- ====================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
SET SESSION group_concat_max_len = 1000000;

-- ── 步骤 1：生成 ALTER 语句到临时表 ──
DROP TEMPORARY TABLE IF EXISTS _nullable_fix;
CREATE TEMPORARY TABLE _nullable_fix (
  id INT AUTO_INCREMENT PRIMARY KEY,
  ddl VARCHAR(5000) NOT NULL
);

INSERT INTO _nullable_fix (ddl)
SELECT CONCAT(
  'ALTER TABLE `', TABLE_NAME, '` ',
  GROUP_CONCAT(
    CONCAT('MODIFY `', COLUMN_NAME, '` ',
      COLUMN_TYPE,
      CASE WHEN DATA_TYPE IN ('varchar','char','varbinary')
           THEN CONCAT('(', CHARACTER_MAXIMUM_LENGTH, ')')
           ELSE '' END,
      CASE WHEN DATA_TYPE IN ('decimal','numeric','float','double')
           THEN CONCAT('(', NUMERIC_PRECISION, ',', NUMERIC_SCALE, ')')
           ELSE '' END,
      ' NULL'
    )
    ORDER BY ORDINAL_POSITION
    SEPARATOR ', '
  ),
  ';'
)
FROM information_schema.COLUMNS c
WHERE c.TABLE_SCHEMA = DATABASE()
  AND c.TABLE_TYPE = 'BASE TABLE'
  AND c.IS_NULLABLE = 'NO'
  AND c.TABLE_NAME NOT LIKE 'v_%'
  AND (
    -- 明确需要改 NULL 的列（名称模式匹配）
    c.COLUMN_NAME LIKE '%By'
    OR c.COLUMN_NAME IN (
      'Code','UpdateTime','DeleteTime','Remark','Sort',
      'ParentCode','OrgLevel','OrgPath','OrgType',
      'CreditCode','LegalPerson','Province','City','Address',
      'IndustryType','ShortName','CertScope','ContactName',
      'ContactPhone','ContactEmail','ArchiveDate',
      'Title','Profession','Phone','Email','QualificationNo',
      'LeaderName','LeaderPhone','EmployeeCount','WorkYears'
    )
  )
  AND c.TABLE_NAME NOT LIKE 'information_schema%'
GROUP BY c.TABLE_NAME;

-- ── 步骤 2：预览（调试时打开，生产执行时跳过） ──
-- SELECT * FROM _nullable_fix ORDER BY id;

-- ── 步骤 3：使用游标逐条执行 ──
-- ⚠️ MySQL 不能在存储过程外执行动态 PREPARE 语句游标；
--     故实际落地时采用客户端脚本拼接批次执行。
--     这里输出拼接后的完整脚本，由 bash 一次性灌入。

SELECT ddl FROM _nullable_fix ORDER BY id;
