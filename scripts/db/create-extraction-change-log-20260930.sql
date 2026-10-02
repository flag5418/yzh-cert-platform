-- ============================================================
-- create-extraction-change-log-20260930.sql
--
-- 背景（05 号 §五 / 25 号 §六 步骤 6）：
--   补录直接改 cert_extraction_result / cert_table_extraction_result（裁决 J2，D06），
--   本表记「谁把哪个值从什么改成了什么」——「一个字段可以反复由人员更改」的完整时间线。
--
-- ⚠️ 2026-09-30 状态：06 号 §2.8 早已写过此表 DDL，但只在 .md 里，
--    scripts/db/ 下【从未落地】（全仓 35 处提及 100% 在文档中）⇒ 补录链路无留痕落点。
--
-- ★ 不可变表：只允许 INSERT / SELECT，⛔ 代码中禁止 UPDATE / DELETE（05 号 §八铁律）。
--    「作废某条日志」用 ChangeAction='revoke' 追加反向记录，不改原记录。
--
-- 幂等：可重复执行。
-- ============================================================

USE `yzh_cert_platform`;
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

SET @exist := (SELECT COUNT(*) FROM information_schema.TABLES
  WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_extraction_change_log');

SET @ddl := IF(@exist = 1,
  'SELECT ''cert_extraction_change_log 已存在，跳过'' AS msg',
  'CREATE TABLE IF NOT EXISTS `cert_extraction_change_log` (
  `Id`                bigint      NOT NULL AUTO_INCREMENT COMMENT ''主键ID'',
  `Code`              varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT ''全局唯一编码（GUID，业务键）'',
  `OrgCode`           varchar(50) COLLATE utf8mb4_general_ci NOT NULL COMMENT ''★专家工作区编码（租户隔离键）'',
  `ResultType`        varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT ''结果类型：field | table'',
  `ResultCode`        varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT ''提取结果行编码（cert_extraction_result.Code / cert_table_extraction_result.Code）'',
  `EnterpriseCode`    varchar(36) COLLATE utf8mb4_general_ci NOT NULL COMMENT ''★企业编码（注意：提取结果表的 OrgCode 列存的也是这个值）'',
  `RuleCode`          varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''★规则编码（裁决 J4：1文件=1规则，取数与补录的主键成分）'',
  `StandardCode`      varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''标准编码（冗余）'',
  `StageCode`         varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''阶段编码（GUID，冗余）'',
  `StandardFileCode`  varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''标准文件行 Code（只读提示，J1 不分文档）'',
  `FileCode`          varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''提取的源文件编码（补录行填规则的 StandardFileCode）'',
  `FieldCode`         varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''字段编码（冗余）'',
  `TableCode`         varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''表格编码（冗余）'',
  `FieldLabel`        varchar(200) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''展示名快照'',
  `ChangeAction`      varchar(20) COLLATE utf8mb4_general_ci NOT NULL COMMENT ''变更动作：manual_edit=人工补录 | archive=新提取/按RuleCode归档覆盖了人工值 | restore_auto=恢复自动值 | revoke=作废前一条'',
  `OldValue`          longtext    COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''旧值'',
  `NewValue`          longtext    COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''新值'',
  `OldValueSource`    varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''旧来源：auto | manual | NULL（原无此行）'',
  `NewValueSource`    varchar(20) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''新来源：manual | auto'',
  `TaskCode`          varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''★哪个任务触发的补录（手动改提取结果时为空）'',
  `GapCode`           varchar(36) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''对应的缺口编码'',
  `OperatorCode`      varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''操作人编码'',
  `OperatorName`      varchar(100) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''操作人姓名（冗余，防改名后追溯断裂）'',
  `OperateTime`       datetime     DEFAULT NULL COMMENT ''操作时间'',
  `Remark`            varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''备注'',
  `CreateBy`          varchar(50) COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''创建人（基类审计字段）'',
  `CreateTime`        datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT ''创建时间'',
  `IsValid`           int          NOT NULL DEFAULT 1 COMMENT ''有效标志（1=有效）；★只 INSERT/SELECT，不做物理删'',

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code`        (`Code`),
  KEY `idx_result`            (`ResultType`, `ResultCode`),
  KEY `idx_field`             (`FieldCode`, `OperateTime`),
  KEY `idx_table`             (`TableCode`, `OperateTime`),
  KEY `idx_enterprise`        (`EnterpriseCode`, `OperateTime`),
  KEY `idx_rule`              (`RuleCode`, `OperateTime`),
  KEY `idx_task`              (`TaskCode`, `CreateTime`),
  KEY `idx_operator`          (`OperatorCode`, `OperateTime`),
  KEY `idx_action`            (`ChangeAction`, `CreateTime`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT=''提取值补录日志（★不可变：只 INSERT/SELECT）''');

PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ── 验证 ──
SELECT CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin AS ColumnName,
       COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE()
   AND CONVERT(TABLE_NAME USING utf8mb4) COLLATE utf8mb4_bin = 'cert_extraction_change_log'
 ORDER BY ORDINAL_POSITION;

SELECT '行数' AS t, COUNT(*) AS n FROM cert_extraction_change_log;
