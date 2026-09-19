-- =============================================================================
-- 2026-09-19：队列三表 yzh_queue / yzh_queue_task / yzh_queue_resource_lock
--                snake_case 列名 → 全部统一为 BaseEntity 标准 PascalCase
-- =============================================================================
-- 修复内容:
-- 1. YzhQueueTask.cs / YzhQueue.cs / YzhQueueResourceLock.cs 移除所有 [SugarColumn(ColumnName="snake_case")]
-- 2. 统一用 BaseEntity.CreateTime / CreateBy / UpdateTime / UpdateBy
-- 3. 删除冗余审计字段（creator / modifier / deleter / create_date 等）
-- 4. DB 全部 RENAME COLUMN snake_case → PascalCase
-- =============================================================================

-- ============================================================
-- yzh_queue_task：所有 snake_case 列 → PascalCase
-- ============================================================
-- 上一步已执行：id, code, QueueCode, TaskType, Payload, Status, ErrorType, ErrorMessage,
--               RetryCount, MaxRetryCount, NextRetryAt, LockedUntil, LockedAt, LockedBy,
--               ProcessTime, CompleteTime, TaskId, UserId, UserName, UserCode, OrgCode,
--               Priority, LockCodes
-- BaseEntity 标准列也一并重命名为 PascalCase:
ALTER TABLE yzh_queue_task RENAME COLUMN create_by TO CreateBy;
ALTER TABLE yzh_queue_task RENAME COLUMN create_time TO CreateTime;
ALTER TABLE yzh_queue_task RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE yzh_queue_task RENAME COLUMN update_time TO UpdateTime;
ALTER TABLE yzh_queue_task RENAME COLUMN is_deleted TO IsDeleted;
ALTER TABLE yzh_queue_task RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE yzh_queue_task RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE yzh_queue_task RENAME COLUMN is_valid TO IsValid;

-- ============================================================
-- yzh_queue：业务列 + BaseEntity 列
-- ============================================================
ALTER TABLE yzh_queue RENAME COLUMN queue_code TO QueueCode;
ALTER TABLE yzh_queue RENAME COLUMN queue_type TO QueueType;
ALTER TABLE yzh_queue RENAME COLUMN queue_name TO QueueName;
ALTER TABLE yzh_queue RENAME COLUMN scope_key TO ScopeKey;
ALTER TABLE yzh_queue RENAME COLUMN scope_info TO ScopeInfo;
ALTER TABLE yzh_queue RENAME COLUMN source_type TO SourceType;
ALTER TABLE yzh_queue RENAME COLUMN source_id TO SourceId;
ALTER TABLE yzh_queue RENAME COLUMN status TO Status;
ALTER TABLE yzh_queue RENAME COLUMN total_count TO TotalCount;
ALTER TABLE yzh_queue RENAME COLUMN pending_count TO PendingCount;
ALTER TABLE yzh_queue RENAME COLUMN processing_count TO ProcessingCount;
ALTER TABLE yzh_queue RENAME COLUMN completed_count TO CompletedCount;
ALTER TABLE yzh_queue RENAME COLUMN failed_count TO FailedCount;
ALTER TABLE yzh_queue RENAME COLUMN cancelled_count TO CancelledCount;
ALTER TABLE yzh_queue RENAME COLUMN progress TO Progress;
ALTER TABLE yzh_queue RENAME COLUMN start_time TO StartTime;
ALTER TABLE yzh_queue RENAME COLUMN end_time TO EndTime;
ALTER TABLE yzh_queue RENAME COLUMN remark TO Remark;
ALTER TABLE yzh_queue RENAME COLUMN org_code TO OrgCode;
ALTER TABLE yzh_queue RENAME COLUMN create_by TO CreateBy;
ALTER TABLE yzh_queue RENAME COLUMN create_time TO CreateTime;
ALTER TABLE yzh_queue RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE yzh_queue RENAME COLUMN update_time TO UpdateTime;
ALTER TABLE yzh_queue RENAME COLUMN is_deleted TO IsDeleted;
ALTER TABLE yzh_queue RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE yzh_queue RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE yzh_queue RENAME COLUMN is_valid TO IsValid;

-- ============================================================
-- yzh_queue_resource_lock：BaseEntity 列
-- ============================================================
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN queue_code TO QueueCode;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN resource_table TO ResourceTable;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN resource_code TO ResourceCode;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN resource_name TO ResourceName;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN task_no TO TaskNo;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN status TO Status;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN active_key TO ActiveKey;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN release_time TO ReleaseTime;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN expire_at TO ExpireAt;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN org_code TO OrgCode;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN create_by TO CreateBy;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN create_time TO CreateTime;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN update_by TO UpdateBy;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN update_time TO UpdateTime;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN is_deleted TO IsDeleted;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN delete_by TO DeleteBy;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN delete_time TO DeleteTime;
ALTER TABLE yzh_queue_resource_lock RENAME COLUMN is_valid TO IsValid;

-- yzh_queue_task 同样 expire_at 不在实体中；LockTime 额外列（由框架添加）
