-- ============================================================
-- Phase 5: 队列中心 yzh 框架化
-- 将队列中心从项目级（cert_ 前缀）提升为 yzh 核心框架级（yzh_ 前缀）
-- 1. yzh_queue                 队列主表（通用）
-- 2. yzh_queue_task            队列子任务表（通用：业务数据进 payload JSON）
-- 3. yzh_queue_resource_lock   队列资源锁定表
-- 4. 数据迁移（cert_queue / cert_file_convert_job / cert_queue_resource_lock）
-- 5. 删除旧表
-- 规范：yzh_ 前缀 = 架构级/跨项目复用表；snake_case 列；code(GUID) + 审计字段 + org_code
-- ============================================================

-- ===== 1. yzh_queue 队列主表 =====
CREATE TABLE IF NOT EXISTS yzh_queue (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  code VARCHAR(36) NOT NULL COMMENT '全局唯一编码(GUID)，表间关联用',
  queue_code VARCHAR(64) NOT NULL COMMENT '队列业务编码：Q-{yyyyMMdd}-{6位随机}',
  queue_type VARCHAR(30) NOT NULL COMMENT '队列类型：file_convert/auto_verify/report_generate',
  queue_name VARCHAR(200) NULL COMMENT '队列名称（人话）',
  scope_key VARCHAR(200) NULL COMMENT '范围键（按类型约定格式，如 file_convert=机构|标准|阶段）',
  scope_info JSON NULL COMMENT '冗余展示数据 JSON',
  source_type VARCHAR(30) NULL COMMENT '来源类型：upload_task/verify_req/report_req',
  source_id VARCHAR(64) NULL COMMENT '来源ID：上传任务taskId等',
  status VARCHAR(20) NOT NULL DEFAULT 'pending' COMMENT 'pending/running/completed/failed/cancelled',
  total_count INT DEFAULT 0 COMMENT '子任务总数',
  pending_count INT DEFAULT 0,
  processing_count INT DEFAULT 0,
  completed_count INT DEFAULT 0,
  failed_count INT DEFAULT 0,
  cancelled_count INT DEFAULT 0,
  progress INT DEFAULT 0 COMMENT '0-100',
  start_time DATETIME NULL,
  end_time DATETIME NULL,
  remark VARCHAR(500) NULL,
  org_code VARCHAR(50) NULL COMMENT '机构编码（多租户）',
  create_id INT NULL,
  creator VARCHAR(50) NULL,
  create_date DATETIME DEFAULT CURRENT_TIMESTAMP,
  modify_id INT NULL,
  modifier VARCHAR(50) NULL,
  modify_date DATETIME NULL,
  delete_id INT NULL,
  deleter VARCHAR(50) NULL,
  delete_time DATETIME NULL,
  UNIQUE KEY uk_code (code),
  UNIQUE KEY uk_queue_code (queue_code),
  UNIQUE KEY uk_source (source_type, source_id),
  KEY idx_scope_status (queue_type, scope_key, status),
  KEY idx_status (status),
  KEY idx_create_date (create_date)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='yzh队列主表（通用队列中心）';

-- ===== 2. yzh_queue_task 队列子任务表（通用） =====
CREATE TABLE IF NOT EXISTS yzh_queue_task (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  code VARCHAR(36) NOT NULL COMMENT '全局唯一编码(GUID)',
  queue_code VARCHAR(64) NOT NULL COMMENT '所属队列编码(yzh_queue.queue_code)',
  task_type VARCHAR(30) NOT NULL COMMENT '任务类型：file_convert/auto_verify/report_generate',
  payload TEXT NULL COMMENT '业务数据 JSON（file_convert={fileCode,fileName,sourcePath,targetPath,convertType}）',
  status VARCHAR(20) NOT NULL DEFAULT 'pending' COMMENT 'pending/processing/completed/failed/cancelled',
  error_type VARCHAR(20) NULL COMMENT '错误分类: retryable(可重试)/permanent(永久)',
  error_message VARCHAR(2000) NULL COMMENT '错误信息',
  retry_count INT DEFAULT 0,
  max_retry_count INT DEFAULT 3,
  next_retry_at DATETIME NULL COMMENT '下次重试时间(指数退避+抖动)',
  locked_until DATETIME NULL COMMENT '领取租约到期时间(worker续期,到期可被重新领取)',
  locked_at DATETIME NULL COMMENT '领取时间',
  locked_by VARCHAR(100) NULL COMMENT '领取 Worker 标识',
  process_time DATETIME NULL COMMENT '开始处理时间',
  complete_time DATETIME NULL COMMENT '完成/失败/取消时间',
  create_time DATETIME NULL COMMENT '入队时间',
  task_id VARCHAR(64) NULL COMMENT '来源批次ID（如上传任务taskId）',
  user_id INT NULL COMMENT '发起用户ID',
  user_name VARCHAR(100) NULL COMMENT '发起用户名',
  org_code VARCHAR(50) NULL COMMENT '机构编码',
  priority INT DEFAULT 0 COMMENT '优先级（0=普通，10=高优先）',
  lock_codes VARCHAR(500) NULL COMMENT '本任务持有的资源锁编码(逗号分隔，对应 yzh_queue_resource_lock.code)',
  create_id INT NULL,
  creator VARCHAR(50) NULL,
  create_date DATETIME DEFAULT CURRENT_TIMESTAMP,
  modify_id INT NULL,
  modifier VARCHAR(50) NULL,
  modify_date DATETIME NULL,
  delete_id INT NULL,
  deleter VARCHAR(50) NULL,
  delete_time DATETIME NULL,
  UNIQUE KEY uk_code (code),
  KEY idx_queue (queue_code),
  KEY idx_claim (status, next_retry_at),
  KEY idx_lease (status, locked_until),
  KEY idx_task_id (task_id),
  KEY idx_type (task_type)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='yzh队列子任务表（通用任务）';

-- ===== 3. yzh_queue_resource_lock 队列资源锁定表 =====
CREATE TABLE IF NOT EXISTS yzh_queue_resource_lock (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  code VARCHAR(36) NOT NULL COMMENT '全局唯一编码(GUID)',
  queue_code VARCHAR(64) NOT NULL COMMENT '所属队列编码',
  resource_table VARCHAR(50) NOT NULL COMMENT '资源表名（如 cert_standard_directory_file / 任意业务表）',
  resource_code VARCHAR(200) NOT NULL COMMENT '资源唯一编码',
  resource_name VARCHAR(200) NULL COMMENT '资源名称快照',
  task_no INT NULL COMMENT '占用该资源的子任务序号（NULL=队列级锁）',
  status VARCHAR(20) DEFAULT 'locked' COMMENT 'locked/released',
  active_key VARCHAR(260) NULL COMMENT '活跃锁键:{resource_table}|{resource_code}，释放时置NULL；uk_active唯一索引实现同一资源同时仅一个活跃锁',
  create_time DATETIME DEFAULT CURRENT_TIMESTAMP,
  release_time DATETIME NULL,
  expire_at DATETIME NULL COMMENT '锁租约安全网',
  org_code VARCHAR(50) NULL,
  create_id INT NULL,
  creator VARCHAR(50) NULL,
  create_date DATETIME DEFAULT CURRENT_TIMESTAMP,
  modify_id INT NULL,
  modifier VARCHAR(50) NULL,
  modify_date DATETIME NULL,
  delete_id INT NULL,
  deleter VARCHAR(50) NULL,
  delete_time DATETIME NULL,
  UNIQUE KEY uk_code (code),
  UNIQUE KEY uk_active (active_key),
  KEY idx_queue (queue_code, status),
  KEY idx_locked (resource_table, resource_code, status),
  KEY idx_expire (status, expire_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='yzh队列资源锁定表';

-- ===== 4. 数据迁移 =====
-- 可重跑：先清空已迁入数据（本脚本为一次性迁移脚本）
TRUNCATE TABLE yzh_queue_resource_lock;
TRUNCATE TABLE yzh_queue_task;
TRUNCATE TABLE yzh_queue;

-- 4.1 yzh_queue ← cert_queue
INSERT INTO yzh_queue
  (code, queue_code, queue_type, queue_name, scope_key, scope_info, source_type, source_id,
   status, total_count, pending_count, processing_count, completed_count, failed_count, cancelled_count,
   progress, start_time, end_time, remark, org_code, create_id, creator, create_date,
   modify_id, modifier, modify_date, delete_id, deleter, delete_time)
SELECT code, queue_code, queue_type, queue_name, scope_key, scope_info, source_type, source_id,
   status, total_count, pending_count, processing_count, completed_count, failed_count, cancelled_count,
   progress, start_time, end_time, remark, org_code, create_id, creator, create_date,
   modify_id, modifier, modify_date, delete_id, deleter, delete_time
FROM cert_queue;

-- 4.2 yzh_queue_task ← cert_file_convert_job（业务列组装为 payload JSON）
-- 注：仅迁移有 queue_code 的队列任务；无 queue_code 的 620 条为旧系统直接落库的孤儿记录
--     （队列体系建立前的转换记录，文件当前状态已冗余在 cert_standard_directory_file.convert_status）
INSERT INTO yzh_queue_task
  (code, queue_code, task_type, payload, status, error_type, error_message,
   retry_count, max_retry_count, next_retry_at, locked_until, locked_at, locked_by,
   process_time, complete_time, create_time, task_id, user_id, user_name, org_code, priority, lock_codes,
   create_id, creator, create_date)
SELECT UUID(), queue_code, 'file_convert',
   JSON_OBJECT('fileCode', file_code, 'sourcePath', source_path, 'targetPath', target_path, 'convertType', convert_type),
   status, error_type, error_message,
   retry_count, max_retry_count, next_retry_at, locked_until, locked_at, locked_by,
   process_time, complete_time, create_time, task_id, user_id, user_name, org_code, priority, lock_codes,
   user_id, user_name, create_time
FROM cert_file_convert_job
WHERE queue_code IS NOT NULL;

-- 4.3 yzh_queue_resource_lock ← cert_queue_resource_lock
INSERT INTO yzh_queue_resource_lock
  (code, queue_code, resource_table, resource_code, resource_name, task_no, status, active_key,
   create_time, release_time, expire_at, org_code, create_id, creator, create_date,
   modify_id, modifier, modify_date, delete_id, deleter, delete_time)
SELECT code, queue_code, resource_table, resource_code, resource_name, task_no, status, active_key,
   create_time, release_time, expire_at, org_code, create_id, creator, create_date,
   modify_id, modifier, modify_date, delete_id, deleter, delete_time
FROM cert_queue_resource_lock;

-- ===== 5. 删除旧表 =====
DROP TABLE IF EXISTS cert_file_convert_job;
DROP TABLE IF EXISTS cert_queue;
DROP TABLE IF EXISTS cert_queue_resource_lock;

-- ===== 验证 =====
SELECT '=== yzh_queue ===' AS info;
SELECT COUNT(*) AS queue_rows FROM yzh_queue;
SELECT '=== yzh_queue_task ===' AS info;
SELECT COUNT(*) AS task_rows FROM yzh_queue_task;
SELECT queue_code, task_type, status, LEFT(payload, 120) AS payload_sample FROM yzh_queue_task LIMIT 3;
SELECT '=== yzh_queue_resource_lock ===' AS info;
SELECT COUNT(*) AS lock_rows FROM yzh_queue_resource_lock;
SELECT '=== old tables dropped ===' AS info;
SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME IN ('cert_queue','cert_queue_resource_lock','cert_file_convert_job');
