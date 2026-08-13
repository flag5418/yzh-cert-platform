-- ============================================================
-- Phase 4: 队列中心（通用队列底座）
-- 1. cert_queue                 队列主表（通用，不含业务列）
-- 2. cert_queue_resource_lock   队列资源锁定表
-- 3. cert_file_convert_job      扩展（queue_code/lock_codes/error_type/next_retry_at/locked_until）
-- 4. 菜单改名：转换队列监控 → 队列监控
-- 规范：表名前缀 cert_，snake_case 列名（与 cert_file_convert_job/cert_message 一致），
--       每表包含 code(GUID 全局唯一) + 审计字段（create_id/creator/create_date/modify_*/delete_*）+ org_code
-- ============================================================

-- ===== 1. cert_queue 队列主表 =====
CREATE TABLE IF NOT EXISTS cert_queue (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  code VARCHAR(36) NOT NULL COMMENT '全局唯一编码(GUID)，表间关联用',
  queue_code VARCHAR(64) NOT NULL COMMENT '队列业务编码：Q-{yyyyMMdd}-{6位随机}',
  queue_type VARCHAR(30) NOT NULL COMMENT '队列类型：file_convert/auto_verify/report_generate',
  queue_name VARCHAR(200) NULL COMMENT '队列名称（人话）：文档转换-12个文件',
  scope_key VARCHAR(200) NULL COMMENT '范围键（按类型约定格式，如 file_convert=机构|标准|阶段）',
  scope_info JSON NULL COMMENT '冗余展示数据：{orgCode,orgName,standardCode,phaseCode,directoryCode,...}',
  source_type VARCHAR(30) NULL COMMENT '来源：upload_task/verify_req/report_req',
  source_id VARCHAR(64) NULL COMMENT '来源ID：上传任务taskId等',
  status VARCHAR(20) NOT NULL DEFAULT 'pending' COMMENT 'pending/running/completed/failed/cancelled',
  total_count INT DEFAULT 0 COMMENT '子任务总数',
  pending_count INT DEFAULT 0,
  processing_count INT DEFAULT 0,
  completed_count INT DEFAULT 0,
  failed_count INT DEFAULT 0,
  cancelled_count INT DEFAULT 0,
  progress INT DEFAULT 0 COMMENT '0-100',
  start_time DATETIME NULL COMMENT '开始时间（进入running）',
  end_time DATETIME NULL COMMENT '结束时间（终态）',
  remark VARCHAR(500) NULL,
  org_code VARCHAR(50) NULL COMMENT '机构编码',
  create_id INT NULL COMMENT '创建人ID',
  creator VARCHAR(50) NULL COMMENT '创建人姓名',
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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='队列主表（通用队列中心）';

-- ===== 2. cert_queue_resource_lock 队列资源锁定表 =====
CREATE TABLE IF NOT EXISTS cert_queue_resource_lock (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  code VARCHAR(36) NOT NULL COMMENT '全局唯一编码(GUID)',
  queue_code VARCHAR(64) NOT NULL COMMENT '所属队列编码',
  resource_table VARCHAR(50) NOT NULL COMMENT '资源表名：cert_standard_directory_file/cert_standard_directory_folder/cert_standard_directory/cert_report...',
  resource_code VARCHAR(200) NOT NULL COMMENT '资源唯一编码（各表 Code/FileCode/FolderCode/DirectoryCode）',
  resource_name VARCHAR(200) NULL COMMENT '资源名称快照（锁定时记录，显示用）',
  task_no INT NULL COMMENT '占用该资源的子任务序号（NULL=队列级锁，如目录锁）',
  status VARCHAR(20) DEFAULT 'locked' COMMENT 'locked/released',
  active_key VARCHAR(260) NULL COMMENT '活跃锁键:{resource_table}|{resource_code}，释放时置NULL；uk_active唯一索引实现同一资源同时仅一个活跃锁',
  create_time DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '加锁时间',
  release_time DATETIME NULL COMMENT '释放时间',
  expire_at DATETIME NULL COMMENT '锁租约安全网：回收任务扫描超时锁强制释放',
  org_code VARCHAR(50) NULL COMMENT '机构编码',
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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='队列资源锁定表';

-- ===== 3. ALTER cert_file_convert_job 扩展 =====
SET @db_name = 'yzh_cert_platform';

SET @col_exists = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = @db_name AND TABLE_NAME = 'cert_file_convert_job' AND COLUMN_NAME = 'queue_code');
SET @sql = IF(@col_exists = 0, 'ALTER TABLE cert_file_convert_job ADD COLUMN queue_code VARCHAR(64) NULL COMMENT ''所属队列编码(cert_queue.queue_code)''', 'SELECT ''queue_code already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = @db_name AND TABLE_NAME = 'cert_file_convert_job' AND COLUMN_NAME = 'lock_codes');
SET @sql = IF(@col_exists = 0, 'ALTER TABLE cert_file_convert_job ADD COLUMN lock_codes VARCHAR(500) NULL COMMENT ''本任务持有的资源锁编码(逗号分隔)''', 'SELECT ''lock_codes already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = @db_name AND TABLE_NAME = 'cert_file_convert_job' AND COLUMN_NAME = 'error_type');
SET @sql = IF(@col_exists = 0, 'ALTER TABLE cert_file_convert_job ADD COLUMN error_type VARCHAR(20) NULL COMMENT ''错误分类: retryable(可重试)/permanent(永久)''', 'SELECT ''error_type already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = @db_name AND TABLE_NAME = 'cert_file_convert_job' AND COLUMN_NAME = 'next_retry_at');
SET @sql = IF(@col_exists = 0, 'ALTER TABLE cert_file_convert_job ADD COLUMN next_retry_at DATETIME NULL COMMENT ''下次重试时间(指数退避+抖动)''', 'SELECT ''next_retry_at already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = @db_name AND TABLE_NAME = 'cert_file_convert_job' AND COLUMN_NAME = 'locked_until');
SET @sql = IF(@col_exists = 0, 'ALTER TABLE cert_file_convert_job ADD COLUMN locked_until DATETIME NULL COMMENT ''领取租约到期时间(worker续期,到期可被重新领取)''', 'SELECT ''locked_until already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 索引（幂等）
SET @idx_exists = (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = @db_name AND TABLE_NAME = 'cert_file_convert_job' AND INDEX_NAME = 'idx_queue_code');
SET @sql = IF(@idx_exists = 0, 'ALTER TABLE cert_file_convert_job ADD INDEX idx_queue_code (queue_code)', 'SELECT ''idx_queue_code already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @idx_exists = (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = @db_name AND TABLE_NAME = 'cert_file_convert_job' AND INDEX_NAME = 'idx_lease');
SET @sql = IF(@idx_exists = 0, 'ALTER TABLE cert_file_convert_job ADD INDEX idx_lease (status, locked_until)', 'SELECT ''idx_lease already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ===== 4. 菜单改名：转换队列监控 → 队列监控 =====
UPDATE sys_menu SET MenuName = '队列监控' WHERE MenuName = '转换队列监控' AND Url = '/CertPlatform/ConvertQueueMonitor';

-- ===== 验证 =====
SELECT '=== cert_queue ===' AS info;
SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = @db_name AND TABLE_NAME = 'cert_queue' ORDER BY ORDINAL_POSITION;
SELECT '=== cert_queue_resource_lock ===' AS info;
SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = @db_name AND TABLE_NAME = 'cert_queue_resource_lock' ORDER BY ORDINAL_POSITION;
SELECT '=== cert_file_convert_job new columns ===' AS info;
SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = @db_name AND TABLE_NAME = 'cert_file_convert_job' AND COLUMN_NAME IN ('queue_code','lock_codes','error_type','next_retry_at','locked_until') ORDER BY ORDINAL_POSITION;
SELECT '=== menu renamed ===' AS info;
SELECT MenuName, Url FROM sys_menu WHERE Url = '/CertPlatform/ConvertQueueMonitor';
