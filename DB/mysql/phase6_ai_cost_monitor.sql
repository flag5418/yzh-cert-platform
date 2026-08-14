-- ============================================================
-- AI 费用监控：记录表 + 系统参数 + 菜单入口
-- 用途：自动记录每次 AI 调用，提供余额与消费查询页面
-- ============================================================

-- ===== 1. AI 调用日志表 =====
CREATE TABLE IF NOT EXISTS cert_ai_usage_log (
  id BIGINT PRIMARY KEY AUTO_INCREMENT,
  call_id VARCHAR(64) NOT NULL COMMENT '调用唯一ID(GUID)',
  business_type VARCHAR(50) NOT NULL DEFAULT 'doc_extraction' COMMENT '业务类型',
  business_ref VARCHAR(100) NULL COMMENT '业务关联（如文件编码）',
  skill VARCHAR(50) NULL COMMENT '技能名称：analyze/verify',
  provider VARCHAR(50) NULL COMMENT '模型提供商：qwen/deepseek',
  model VARCHAR(100) NULL COMMENT '模型名称：qwen-turbo/qwen-plus 等',
  prompt_tokens INT DEFAULT 0 COMMENT '输入 token 数',
  completion_tokens INT DEFAULT 0 COMMENT '输出 token 数',
  total_tokens INT DEFAULT 0 COMMENT '总 token 数',
  cost_usd DECIMAL(10,6) DEFAULT 0 COMMENT '本次费用（美元）',
  duration_ms BIGINT DEFAULT 0 COMMENT '耗时（毫秒）',
  success TINYINT(1) DEFAULT 1 COMMENT '是否成功',
  error_message VARCHAR(500) NULL COMMENT '失败原因',
  create_date DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '调用时间',
  UNIQUE KEY uk_call_id (call_id),
  KEY idx_create_date (create_date),
  KEY idx_model (model),
  KEY idx_success (success)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='AI 调用日志（用于费用统计）';

-- ===== 2. 新增系统参数：阿里云 AccessKey（用于查询实时余额）=====
INSERT IGNORE INTO cert_sys_config (config_key, config_value, config_type, category, display_name, description, sort_order, is_readonly, create_date)
VALUES
  ('aliyun_access_key_id', '', 'string', 'aliyun', '阿里云 AccessKey ID', '用于查询 DashScope 账户余额和消费明细，在阿里云控制台 RAM 获取', 1, 0, NOW()),
  ('aliyun_access_key_secret', '', 'string', 'aliyun', '阿里云 AccessKey Secret', '对应上面的 Secret，请妥善保管', 2, 0, NOW()),
  ('aliyun_dashboard_url', 'https://usercenter2.aliyun.com/finance/overage', 'string', 'aliyun', '阿里云费用中心链接', '点击可跳转至阿里云费用中心查看实时余额', 3, 1, NOW());

-- ===== 3. 新增菜单权限 =====
-- ParentId=304 是"体系认证平台"根节点，OrderNo=110 插在 Prompt模板管理(105) 之后
INSERT IGNORE INTO sys_menu (MenuName, ParentId, Url, OrderNo, MenuType, Icon, Description, Enable, CreateDate, Creator)
SELECT 'AI 费用监控', 304, '/CertPlatform/AIUsageMonitor', 110, 1, 'Money', 'AI调用费用监控与消费明细', 1, NOW(), 'system'
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM sys_menu WHERE Url = '/CertPlatform/AIUsageMonitor');
