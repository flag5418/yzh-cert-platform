USE `yzh_cert_platform`;

-- 插入 CertStage 的页面配置（避免 404 错误）
INSERT INTO yzh_page_config (
  page_key, page_title, entity_name, table_name,
  controller_name, key_field, is_active, created_at
) VALUES (
  'CertStage',
  '认证阶段定义',
  'CertStage',
  'cert_cert_stage',
  'CertStage',
  'Id',
  1,
  NOW()
) ON DUPLICATE KEY UPDATE updated_at = NOW();

-- 验证
SELECT id, page_key, page_title, entity_name FROM yzh_page_config WHERE page_key = 'CertStage';
