-- ============================================================
-- T0 前置对齐：wf_workflow_execution_log 执行状态字段补充
-- 关联：YZH-AI引擎详细设计-V1.md §8.2 F-04
-- 日期：2026-08-11
-- 幂等：是（先检查列是否存在）
-- 执行：docker exec -i yzh-mysql mysql -uroot -pYzh123456. yzh_cert_platform < cert_phase_ai_engine_t0_alignment.sql
-- ============================================================

-- 1. 新增执行状态列（区别于基类 Status 实体启用标记）
-- 取值：pending / running / success / failed / skipped
SET @dbname = DATABASE();
SET @tablename = 'wf_workflow_execution_log';
SET @columnname = 'execution_status';
SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE
      (TABLE_SCHEMA = @dbname)
      AND (TABLE_NAME = @tablename)
      AND (COLUMN_NAME = @columnname)
  ) > 0,
  'SELECT 1',
  CONCAT('ALTER TABLE ', @tablename, ' ADD COLUMN ', @columnname, ' VARCHAR(20) DEFAULT ''pending'' COMMENT ''执行状态：pending=待执行 running=执行中 success=成功 failed=失败 skipped=分支跳过''')
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

-- 2. 为执行状态列创建索引（按实例查询场景高频）
SET @columnname2 = 'execution_status';
SET @preparedStatement2 = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
    WHERE
      (TABLE_SCHEMA = @dbname)
      AND (TABLE_NAME = @tablename)
      AND (INDEX_NAME = 'idx_execution_status')
  ) > 0,
  'SELECT 1',
  'ALTER TABLE wf_workflow_execution_log ADD INDEX idx_execution_status (execution_status)'
));
PREPARE indexIfNotExists FROM @preparedStatement2;
EXECUTE indexIfNotExists;
DEALLOCATE PREPARE indexIfNotExists;

-- 3. 验证新增字段
SELECT COLUMN_NAME, COLUMN_TYPE, COLUMN_DEFAULT, COLUMN_COMMENT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND TABLE_NAME = 'wf_workflow_execution_log'
  AND COLUMN_NAME IN ('WorkflowCode', 'BusinessId', 'execution_status', 'InputData', 'OutputData', 'StartedAt')
ORDER BY ORDINAL_POSITION;
