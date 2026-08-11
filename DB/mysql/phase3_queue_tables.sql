-- ============================================================
-- Phase 3: 文件转换队列化 - 数据库表结构
-- 1. cert_sys_config - 全局系统参数配置
-- 2. cert_message - 站内消息
-- 3. cert_file_convert_job - 改造（增加 task_id/user_id 等字段）
-- ============================================================

-- ===== 1. cert_sys_config =====
CREATE TABLE IF NOT EXISTS cert_sys_config (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  config_key VARCHAR(100) NOT NULL COMMENT '参数键',
  config_value VARCHAR(500) NULL COMMENT '参数值',
  config_type VARCHAR(20) DEFAULT 'string' COMMENT '类型：string/int/bool/json',
  category VARCHAR(50) NOT NULL COMMENT '分类',
  display_name VARCHAR(100) NOT NULL COMMENT '显示名称',
  description VARCHAR(500) NULL COMMENT '说明',
  sort_order INT DEFAULT 0 COMMENT '排序',
  is_readonly TINYINT DEFAULT 0 COMMENT '是否只读（系统级参数）',
  create_date DATETIME DEFAULT CURRENT_TIMESTAMP,
  modify_date DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  UNIQUE KEY uk_config_key (config_key),
  INDEX idx_category (category)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='全局系统参数配置';

-- 预置参数
INSERT INTO cert_sys_config (config_key, config_value, config_type, category, display_name, description, sort_order, is_readonly) VALUES
-- 文件转换队列
('convert_max_concurrent', '5', 'int', 'convert_queue', '最大并发转换数', '同时运行的 LibreOffice 进程数', 1, 0),
('convert_timeout_seconds', '300', 'int', 'convert_queue', '单文件转换超时(秒)', '超过此时间强制终止转换进程', 2, 0),
('convert_max_retry', '3', 'int', 'convert_queue', '最大重试次数', '转换失败后自动重试次数', 3, 0),
('convert_queue_enabled', 'true', 'bool', 'convert_queue', '启用转换队列', '关闭后上传不触发转换', 4, 0),
('convert_polling_interval', '3', 'int', 'convert_queue', '队列轮询间隔(秒)', '后台服务检查队列的频率', 5, 0),
-- AI 模型配置
('ai_provider', 'deepseek', 'string', 'ai_model', 'AI 服务商', 'deepseek/openai/zhipu', 10, 0),
('ai_api_key', '', 'string', 'ai_model', 'AI API Key', 'API 密钥', 11, 0),
('ai_base_url', 'https://api.deepseek.com', 'string', 'ai_model', 'AI Base URL', 'API 基础地址', 12, 0),
('ai_model_name', 'deepseek-chat', 'string', 'ai_model', '模型名称', '使用的模型', 13, 0),
('ai_max_tokens', '4096', 'int', 'ai_model', '最大 Token 数', '单次请求最大 token', 14, 0),
('ai_temperature', '0.7', 'string', 'ai_model', '温度参数', '0-1 之间', 15, 0),
-- OCR 配置
('ocr_provider', 'tencent', 'string', 'ocr', 'OCR 服务商', 'tencent/baidu/local', 20, 0),
('ocr_secret_id', '', 'string', 'ocr', 'OCR SecretId', '', 21, 0),
('ocr_secret_key', '', 'string', 'ocr', 'OCR SecretKey', '', 22, 0),
-- MinIO 配置
('minio_endpoint', '127.0.0.1:9000', 'string', 'storage', 'MinIO 地址', '', 30, 0),
('minio_bucket', 'cert-platform', 'string', 'storage', 'MinIO Bucket', '', 31, 0),
('minio_upload_max_size', '104857600', 'int', 'storage', '最大上传大小(字节)', '默认 100MB', 32, 0),
-- 系统级（只读）
('system_version', '3.0', 'string', 'system', '系统版本', '', 100, 1),
('system_name', '映智汇认证审核管理系统', 'string', 'system', '系统名称', '', 101, 1)
ON DUPLICATE KEY UPDATE modify_date = NOW();

-- ===== 2. cert_message =====
CREATE TABLE IF NOT EXISTS cert_message (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  user_id INT NOT NULL COMMENT '接收用户ID',
  user_name VARCHAR(100) NULL COMMENT '接收用户名',
  title VARCHAR(200) NOT NULL COMMENT '消息标题',
  content TEXT NULL COMMENT '消息内容',
  message_type VARCHAR(50) DEFAULT 'system' COMMENT '消息类型：system/convert/task',
  is_read TINYINT DEFAULT 0 COMMENT '是否已读',
  extra_data JSON NULL COMMENT '附加数据(JSON)',
  create_date DATETIME DEFAULT CURRENT_TIMESTAMP,
  read_date DATETIME NULL,
  INDEX idx_user_read (user_id, is_read),
  INDEX idx_create_date (create_date)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='站内消息';

-- ===== 3. ALTER cert_file_convert_job =====
-- 检查 task_id 列是否存在，不存在则添加（幂等操作）
SET @col_exists = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME = 'cert_file_convert_job' AND COLUMN_NAME = 'task_id');
SET @sql = IF(@col_exists = 0, 'ALTER TABLE cert_file_convert_job ADD COLUMN task_id VARCHAR(64) NULL COMMENT ''上传批次任务ID''', 'SELECT ''task_id already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME = 'cert_file_convert_job' AND COLUMN_NAME = 'user_id');
SET @sql = IF(@col_exists = 0, 'ALTER TABLE cert_file_convert_job ADD COLUMN user_id INT NULL COMMENT ''发起用户ID''', 'SELECT ''user_id already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME = 'cert_file_convert_job' AND COLUMN_NAME = 'user_name');
SET @sql = IF(@col_exists = 0, 'ALTER TABLE cert_file_convert_job ADD COLUMN user_name VARCHAR(100) NULL COMMENT ''发起用户名''', 'SELECT ''user_name already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME = 'cert_file_convert_job' AND COLUMN_NAME = 'org_code');
SET @sql = IF(@col_exists = 0, 'ALTER TABLE cert_file_convert_job ADD COLUMN org_code VARCHAR(50) NULL COMMENT ''机构编码''', 'SELECT ''org_code already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME = 'cert_file_convert_job' AND COLUMN_NAME = 'priority');
SET @sql = IF(@col_exists = 0, 'ALTER TABLE cert_file_convert_job ADD COLUMN priority INT DEFAULT 0 COMMENT ''优先级''', 'SELECT ''priority already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME = 'cert_file_convert_job' AND COLUMN_NAME = 'locked_at');
SET @sql = IF(@col_exists = 0, 'ALTER TABLE cert_file_convert_job ADD COLUMN locked_at DATETIME NULL COMMENT ''锁定时间''', 'SELECT ''locked_at already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists = (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME = 'cert_file_convert_job' AND COLUMN_NAME = 'locked_by');
SET @sql = IF(@col_exists = 0, 'ALTER TABLE cert_file_convert_job ADD COLUMN locked_by VARCHAR(100) NULL COMMENT ''Worker标识''', 'SELECT ''locked_by already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 添加索引（幂等）
SET @idx_exists = (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME = 'cert_file_convert_job' AND INDEX_NAME = 'idx_task_id');
SET @sql = IF(@idx_exists = 0, 'ALTER TABLE cert_file_convert_job ADD INDEX idx_task_id (task_id)', 'SELECT ''idx_task_id already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @idx_exists = (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME = 'cert_file_convert_job' AND INDEX_NAME = 'idx_status_priority');
SET @sql = IF(@idx_exists = 0, 'ALTER TABLE cert_file_convert_job ADD INDEX idx_status_priority (status, priority)', 'SELECT ''idx_status_priority already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @idx_exists = (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME = 'cert_file_convert_job' AND INDEX_NAME = 'idx_user_id');
SET @sql = IF(@idx_exists = 0, 'ALTER TABLE cert_file_convert_job ADD INDEX idx_user_id (user_id)', 'SELECT ''idx_user_id already exists''');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ===== 验证 =====
SELECT '=== cert_sys_config ===' AS info;
SELECT config_key, config_value, category, display_name FROM cert_sys_config ORDER BY sort_order;

SELECT '=== cert_message ===' AS info;
SELECT COUNT(*) AS table_exists FROM information_schema.TABLES WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME = 'cert_message';

SELECT '=== cert_file_convert_job columns ===' AS info;
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = 'yzh_cert_platform' AND TABLE_NAME = 'cert_file_convert_job' ORDER BY ORDINAL_POSITION;
