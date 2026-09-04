-- ============================================================
-- 审核员注册极简改造 + 微信支持预留
-- ============================================================
-- 功能：为 Sys_User 增加微信相关字段，支持未来微信注册/登录
-- 说明：openid/unionid 为预留字段，当前注册流程暂不使用
-- ============================================================

-- 1. 添加微信 openid 字段（可用于微信登录）
ALTER TABLE sys_user ADD COLUMN wechat_openid VARCHAR(64) DEFAULT NULL COMMENT '微信 OpenID' AFTER Email;

-- 2. 添加微信 unionid 字段（微信开放平台统一标识）
ALTER TABLE sys_user ADD COLUMN wechat_unionid VARCHAR(64) DEFAULT NULL COMMENT '微信 UnionID' AFTER wechat_openid;

-- 3. 添加 unionid 唯一索引（确保同一微信账号只绑定一个用户）
ALTER TABLE sys_user ADD UNIQUE INDEX uk_wechat_unionid (wechat_unionid);

-- 4. 添加 openid 唯一索引
ALTER TABLE sys_user ADD UNIQUE INDEX uk_wechat_openid (wechat_openid);

-- 5. 验证字段添加成功
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLUMN_COMMENT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'sys_user'
  AND COLUMN_NAME IN ('wechat_openid', 'wechat_unionid')
ORDER BY ORDINAL_POSITION;
