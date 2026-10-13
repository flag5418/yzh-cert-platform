-- ============================================================
-- 20261013_queue_reuse_columns_V1.sql
-- 队列复用（方案 C）T1：给 yzh_queue / yzh_queue_task 补 6 类列 + Payload text→json
--
-- 依据：`docs/50-任务/迁移计划/单一归属字段与队列复用-设计-V1.md` §2.2 / §2.5 T1
--       （P3 已裁「先补列」；两表当前均 0 行 ⇒ 无兼容负担）
--
-- 【为什么要补】（§2.2 实测：框架队列 vs 专家专用队列的 6 类结构差异）
--   方案 C = 专家任务并入 yzh_queue。并入前必须把框架队列缺的列补齐，否则
--   专家端语义（跳过计数 / 队列级重试锁 / 最小单元定位 / 版本戳 / 工作流留痕）
--   在 yzh_queue 上无处安放。
--     1) 主表 SkippedCount        —— 「跳过」与「失败」必须分开（用户明确：跳过 ≠ 失败）
--     2) 主表 Priority/RetryCount/MaxRetryCount/LockCode/LockedUntil/LastError
--                                  —— 队列级抢占与整体重试（专家表同名列对齐）
--     3) 明细 ItemCode           —— 最小队列单元 = NC 检查项 / 章节，需指向具体业务对象
--     4) 明细 Seq                —— 明细顺序稳定（章节天然有序）
--     5) 明细 ExtractVersionStamp —— 提取结果版本戳（结果变了要重跑）
--     6) 明细 WorkflowExecTaskCode —— 与工作流执行留痕对齐
--   Payload text→json：与专家明细表 Payload(json) 对齐；两表 0 行 ⇒ 直接改型，无转换。
--
-- 【列类型对齐来源】cert_expert_task_queue 实测（2026-10-12 information_schema）：
--   SkippedCount int(0) · Priority int(0) · RetryCount int(0) · MaxRetryCount int(3)
--   · LockCode varchar(50) · LockedUntil datetime · LastError varchar(1000)
--
-- 幂等：information_schema 守卫（MySQL 8.0 不支持 ADD COLUMN IF NOT EXISTS）
-- 执行：docker exec -i yzh-mysql mysql -uroot -p*** --default-character-set=utf8mb4 yzh_cert_platform < 本文件
-- 同步：C# 实体 YzhQueue.cs / YzhQueueTask.cs（src/yzh-core/YZH.Core.Stand/Models/Queue/）
--       ⚠️ all_tables_ddl.sql 为冻结时点参考件（文件头禁手工编辑），不在其上同步。
-- ============================================================

SET @db := DATABASE();

-- ─────────────────────────────────────────────
-- 1) yzh_queue 主表：补 7 列（跳过计数 + 队列级重试/锁/错误）
-- ─────────────────────────────────────────────

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'yzh_queue' AND COLUMN_NAME = 'SkippedCount') = 0,
  'ALTER TABLE yzh_queue ADD COLUMN SkippedCount int NOT NULL DEFAULT 0 COMMENT ''跳过数（跳过≠失败，与 FailedCount 分列统计）'' AFTER CancelledCount',
  'SELECT ''yzh_queue.SkippedCount 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'yzh_queue' AND COLUMN_NAME = 'Priority') = 0,
  'ALTER TABLE yzh_queue ADD COLUMN Priority int NOT NULL DEFAULT 0 COMMENT ''队列级优先级（越大越先，对齐专家表）'' AFTER Progress',
  'SELECT ''yzh_queue.Priority 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'yzh_queue' AND COLUMN_NAME = 'RetryCount') = 0,
  'ALTER TABLE yzh_queue ADD COLUMN RetryCount int NOT NULL DEFAULT 0 COMMENT ''队列级重试次数'' AFTER Priority',
  'SELECT ''yzh_queue.RetryCount 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'yzh_queue' AND COLUMN_NAME = 'MaxRetryCount') = 0,
  'ALTER TABLE yzh_queue ADD COLUMN MaxRetryCount int NOT NULL DEFAULT 3 COMMENT ''队列级最大重试'' AFTER RetryCount',
  'SELECT ''yzh_queue.MaxRetryCount 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'yzh_queue' AND COLUMN_NAME = 'LockCode') = 0,
  'ALTER TABLE yzh_queue ADD COLUMN LockCode varchar(50) DEFAULT NULL COMMENT ''队列级当前持有者（worker 标识，对齐专家表）'' AFTER MaxRetryCount',
  'SELECT ''yzh_queue.LockCode 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'yzh_queue' AND COLUMN_NAME = 'LockedUntil') = 0,
  'ALTER TABLE yzh_queue ADD COLUMN LockedUntil datetime DEFAULT NULL COMMENT ''队列级锁租约到期'' AFTER LockCode',
  'SELECT ''yzh_queue.LockedUntil 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'yzh_queue' AND COLUMN_NAME = 'LastError') = 0,
  'ALTER TABLE yzh_queue ADD COLUMN LastError varchar(1000) DEFAULT NULL COMMENT ''队列级最后错误（整队失败原因快照）'' AFTER LockedUntil',
  'SELECT ''yzh_queue.LastError 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ─────────────────────────────────────────────
-- 2) yzh_queue_task 明细表：补 4 列（最小单元定位 / 顺序 / 版本戳 / 工作流留痕）
-- ─────────────────────────────────────────────

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'yzh_queue_task' AND COLUMN_NAME = 'ItemCode') = 0,
  'ALTER TABLE yzh_queue_task ADD COLUMN ItemCode varchar(64) DEFAULT NULL COMMENT ''业务实体 Code（最小队列单元：NC 检查项 Code / 报告章节 Code）'' AFTER TaskType',
  'SELECT ''yzh_queue_task.ItemCode 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'yzh_queue_task' AND COLUMN_NAME = 'Seq') = 0,
  'ALTER TABLE yzh_queue_task ADD COLUMN Seq int NOT NULL DEFAULT 0 COMMENT ''明细顺序（章节天然有序，保证展示与执行顺序稳定）'' AFTER ItemCode',
  'SELECT ''yzh_queue_task.Seq 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'yzh_queue_task' AND COLUMN_NAME = 'ExtractVersionStamp') = 0,
  'ALTER TABLE yzh_queue_task ADD COLUMN ExtractVersionStamp varchar(100) DEFAULT NULL COMMENT ''提取结果版本戳（结果变更后据此判定需重跑）'' AFTER Seq',
  'SELECT ''yzh_queue_task.ExtractVersionStamp 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'yzh_queue_task' AND COLUMN_NAME = 'WorkflowExecTaskCode') = 0,
  'ALTER TABLE yzh_queue_task ADD COLUMN WorkflowExecTaskCode varchar(64) DEFAULT NULL COMMENT ''工作流执行任务编码（与工作流执行留痕对齐）'' AFTER ExtractVersionStamp',
  'SELECT ''yzh_queue_task.WorkflowExecTaskCode 已存在'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ─────────────────────────────────────────────
-- 3) Payload text → json（两表 0 行 ⇒ 直接改型，无转换负担）
-- ─────────────────────────────────────────────

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'yzh_queue_task' AND COLUMN_NAME = 'Payload'
       AND COLUMN_TYPE = 'json') = 0,
  'ALTER TABLE yzh_queue_task MODIFY COLUMN Payload json NULL COMMENT ''业务数据 JSON（file_convert={fileCode,fileName,sourcePath,targetPath,convertType}；专家任务按 TaskType 承载 ItemCode/Seq 等）''',
  'SELECT ''yzh_queue_task.Payload 已是 json'' AS msg'
);
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ─────────────────────────────────────────────
-- 验证（应输出：yzh_queue 7 新列 / yzh_queue_task 4 新列 + Payload=json）
-- ─────────────────────────────────────────────

SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLUMN_COMMENT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = @db
  AND ((TABLE_NAME = 'yzh_queue' AND COLUMN_NAME IN
        ('SkippedCount','Priority','RetryCount','MaxRetryCount','LockCode','LockedUntil','LastError'))
    OR (TABLE_NAME = 'yzh_queue_task' AND COLUMN_NAME IN
        ('ItemCode','Seq','ExtractVersionStamp','WorkflowExecTaskCode','Payload')))
ORDER BY TABLE_NAME, ORDINAL_POSITION;

-- 回滚（当前 0 行，无数据风险）：
-- ALTER TABLE yzh_queue DROP COLUMN SkippedCount, DROP COLUMN Priority, DROP COLUMN RetryCount,
--   DROP COLUMN MaxRetryCount, DROP COLUMN LockCode, DROP COLUMN LockedUntil, DROP COLUMN LastError;
-- ALTER TABLE yzh_queue_task DROP COLUMN ItemCode, DROP COLUMN Seq,
--   DROP COLUMN ExtractVersionStamp, DROP COLUMN WorkflowExecTaskCode;
-- ALTER TABLE yzh_queue_task MODIFY COLUMN Payload text NULL;
