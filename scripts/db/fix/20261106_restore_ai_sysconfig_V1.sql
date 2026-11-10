-- ============================================================================
-- 20261106_restore_ai_sysconfig_V1.sql
-- 恢复 cert_sys_config 的 AI 参数（幂等，可重复执行）
--
-- 背景（2026-11-06 用户报障「/system/config 中 ai 的参数还是没有」）：
--   2026-11-05 iCloud 同步灾难后数据库被 CodeFirst 全量重建，cert_sys_config
--   重建后只剩 6 行 AI 参数，且状态是「初版残骸」：
--     ① IsValid = 0（页面 /system/config 默认过滤 IsValid=0 ⇒ 6 行全部不可见）
--     ② ConfigValue 停留初版：qwen-turbo / 4096 / 0.7（后续演进全部丢失）
--     ③ Code = NULL（编辑保存会被 OnBeforeUpdate 拦下：
--        「更新失败：缺少业务键 Code」——准则 A 的必然结果）
--     ④ ai_vision_config 整行丢失（视觉模型配置不存在）
--     ⑤ uk_config_key 唯一索引丢失（20261002_ai_config_unify_V1.sql 的
--        INSERT ... ON DUPLICATE KEY UPDATE 幂等机制依赖它）
--
-- 权威演进链（本脚本据此恢复终态，全部来自已有的 -V1 脚本）：
--   20261002_ai_config_unify_V1.sql  → qwen-flash / 32768 / 0.2，Sort 13/14/15
--   20261004_ai_vision_config_V1.sql → ai_vision_config 行（JSON 13 键）
--   20261009_ai_vision_config_sort_V1.sql → ai_vision_config Sort 100 → 16
--   20261014_ai_vision_model_V1.sql  → Model → qwen-vl-ocr（含说明文字）
--   20261017_ai_model_qwen_plus_V1.sql → 系统默认模型 qwen-flash → qwen-plus
--
-- ⛔ 不动的：
--   · ai_api_key / ai_base_url / ai_provider 的值（现有值即真实配置，保留）
--   · 现有 6 行的 Sort（1-6）与 DisplayName —— 显示顺序保持现状
--
-- 执行：
--   docker exec -i -e MYSQL_PWD="CHANGE_ME" yzh-mysql mysql -uroot \
--     --default-character-set=utf8mb4 yzh_cert_platform \
--     < scripts/db/fix/20261106_restore_ai_sysconfig_V1.sql
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────────────────────
-- ① 结构对齐旧表（重建后偏差：列宽 500→255、索引丢失）
--    ⚠️ 旧表冻结结构 = scripts/db/all_tables_ddl.sql:1109
--       · ConfigValue varchar(500)：ai_vision_config 的 JSON 约 490 字符，
--         varchar(255) 会在 INSERT 时报 1406（实测）⇒ 必须扩回 500
--       · uk_config_key / code 唯一索引 / idx_category：10-02 等脚本的
--         INSERT ... ON DUPLICATE KEY UPDATE 幂等机制依赖 uk_config_key
-- ────────────────────────────────────────────────────────────────────────────
SET @ddl := (
  SELECT IF(COLUMN_TYPE = 'varchar(500)', 'DO 0',
    'ALTER TABLE `cert_sys_config` MODIFY COLUMN `ConfigValue` varchar(500) COLLATE utf8mb4_general_ci DEFAULT NULL')
    FROM information_schema.COLUMNS
   WHERE TABLE_SCHEMA = 'yzh_cert_platform'
     AND TABLE_NAME = 'cert_sys_config'
     AND COLUMN_NAME = 'ConfigValue');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl := (
  SELECT IF(COUNT(*) = 0,
    'ALTER TABLE `cert_sys_config` ADD UNIQUE KEY `uk_config_key` (`ConfigKey`)',
    'DO 0')
    FROM information_schema.STATISTICS
   WHERE TABLE_SCHEMA = 'yzh_cert_platform'
     AND TABLE_NAME = 'cert_sys_config'
     AND INDEX_NAME = 'uk_config_key');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Code 唯一索引（旧库 UNIQUE KEY `code`）：补 Code 后须保证不重复
SET @ddl := (
  SELECT IF(COUNT(*) = 0,
    'ALTER TABLE `cert_sys_config` ADD UNIQUE KEY `code` (`Code`)',
    'DO 0')
    FROM information_schema.STATISTICS
   WHERE TABLE_SCHEMA = 'yzh_cert_platform'
     AND TABLE_NAME = 'cert_sys_config'
     AND INDEX_NAME = 'code');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl := (
  SELECT IF(COUNT(*) = 0,
    'ALTER TABLE `cert_sys_config` ADD INDEX `idx_category` (`Category`)',
    'DO 0')
    FROM information_schema.STATISTICS
   WHERE TABLE_SCHEMA = 'yzh_cert_platform'
     AND TABLE_NAME = 'cert_sys_config'
     AND INDEX_NAME = 'idx_category');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ────────────────────────────────────────────────────────────────────────────
-- ② 6 行 AI 参数：启用 + 补业务键 Code（Code = ConfigKey，同 20261002 脚本口径）
--    UpdateBy 用整词 'migration' —— 已在 verify_createby.sql 白名单登记
--    （记录「这行由脚本改」；写成某个用户 = 伪造审计留痕）
-- ────────────────────────────────────────────────────────────────────────────
UPDATE `cert_sys_config`
   SET `IsValid` = 1,
       `Code` = `ConfigKey`,
       `UpdateTime` = NOW(),
       `UpdateBy` = 'migration'
 WHERE `ConfigKey` IN ('ai_api_key', 'ai_base_url', 'ai_model_name',
                       'ai_provider', 'ai_max_tokens', 'ai_temperature')
   AND (`IsValid` = 0 OR `Code` IS NULL);

-- ────────────────────────────────────────────────────────────────────────────
-- ③ 值恢复为权威终态（各脚本的条件写法：值已正确则 0 行更新）
-- ③-1 模型：qwen-turbo → qwen-plus（2026-10-17 用户裁决）
-- ────────────────────────────────────────────────────────────────────────────
UPDATE `cert_sys_config`
   SET `ConfigValue` = 'qwen-plus',
       `Description` = '使用的模型（系统默认）。默认 qwen-plus（质量/成本均衡；最大输出 32768；支持结构化输出）。切换前先核对账号 /compatible-mode/v1/models 是否存在该名字，否则整条 AI 能力静默不可用。',
       `UpdateTime` = NOW(),
       `UpdateBy` = 'migration'
 WHERE `ConfigKey` = 'ai_model_name'
   AND `ConfigValue` <> 'qwen-plus';

-- ③-2 上限：4096 → 32768（长文档 JSON 防截断）
-- ────────────────────────────────────────────────────────────────────────────
UPDATE `cert_sys_config`
   SET `ConfigValue` = '32768',
       `Description` = '单次请求最大输出 token。2026-10-02 统一 AI 配置：4096 → 32768（2026-10-17 切 qwen-plus 后仍适用，该上限已实测可用，避免长文档 JSON 截断）',
       `UpdateTime` = NOW(),
       `UpdateBy` = 'migration'
 WHERE `ConfigKey` = 'ai_max_tokens'
   AND `ConfigValue` <> '32768';

-- ③-3 温度：0.7 → 0.2（结构化输出要求稳定）
-- ────────────────────────────────────────────────────────────────────────────
UPDATE `cert_sys_config`
   SET `ConfigValue` = '0.2',
       `Description` = '0-1 之间。2026-10-02 统一 AI 配置：0.7 → 0.2（分类判定与结构化输出要求稳定）',
       `UpdateTime` = NOW(),
       `UpdateBy` = 'migration'
 WHERE `ConfigKey` = 'ai_temperature'
   AND `ConfigValue` <> '0.2';

-- ────────────────────────────────────────────────────────────────────────────
-- ④ 补回 ai_vision_config 整行
--    JSON 内容 = 20261004_ai_vision_config_V1.sql 原文（Model 已是 10-14 终态
--    qwen-vl-ocr）；Description/Remark = 20261014 终版；Sort = 10-09 终值 16
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `cert_sys_config`
  (`Code`, `ConfigKey`, `ConfigValue`, `ConfigType`, `Category`, `DisplayName`,
   `Description`, `Sort`, `IsReadonly`, `Remark`, `CreateTime`, `CreateBy`,
   `IsDeleted`, `IsValid`)
VALUES
  ('ai-vision-config', 'ai_vision_config',
'{
  "Enabled": true,
  "Provider": "qianwen",
  "Model": "qwen-vl-ocr",
  "BaseUrl": "",
  "ApiKey": "",
  "TimeoutSeconds": 180,
  "MaxTokens": 8192,
  "Temperature": 0.1,
  "DocumentParsePrompt": "qwenvl markdown",
  "ImageJudgePrompt": "判断这份资料的类型。只输出一个 JSON：{\"kind\":\"体系文件|营业执照|身份证|资质证书|许可证|检测报告|其他\",\"confidence\":0.0-1.0,\"reason\":\"一句话\"}",
  "ImageMaxEdge": 1600,
  "MaxImageBytes": 12582912
}',
  'json', 'ai_model', '视觉模型配置',
  '图片/扫描件 → Markdown。⭐ Model 必须是视觉模型（名字含 vl / vision / omni，如 qwen-vl-ocr），否则整条视觉能力被静默禁用；BaseUrl/ApiKey 留空则回填 ai_base_url/ai_api_key（同厂商不必重复配）。图片按 token 计费（长边压到 ImageMaxEdge 约省一半）。',
  16, 0,
  '图片/扫描件 → Markdown。⭐ Model 必须是视觉模型（名字含 vl / vision / omni，如 qwen-vl-ocr），否则整条视觉能力被静默禁用；BaseUrl/ApiKey 留空则回填 ai_base_url/ai_api_key（同厂商不必重复配）。图片按 token 计费（长边压到 ImageMaxEdge 约省一半）。',
  NOW(), 'migration', 0, 1)
ON DUPLICATE KEY UPDATE
  `ConfigValue` = VALUES(`ConfigValue`),
  `Remark`     = VALUES(`Remark`),
  `UpdateTime` = NOW();

-- ────────────────────────────────────────────────────────────────────────────
-- 验证（期望：Category='ai_model' 共 7 行，全部 IsValid=1，值如右）
--   ai_provider     = qianwen（不动）
--   ai_api_key      = 现有真实 key（不动）
--   ai_base_url     = https://dashscope.aliyuncs.com/compatible-mode/v1（不动）
--   ai_model_name   = qwen-plus
--   ai_max_tokens   = 32768
--   ai_temperature  = 0.2
--   ai_vision_config= JSON（Model=qwen-vl-ocr，13 键）
-- ────────────────────────────────────────────────────────────────────────────
SELECT `ConfigKey`, LEFT(`ConfigValue`, 40) AS `ConfigValue40`,
       `ConfigType`, `Category`, `Sort`, `IsValid`, `IsDeleted`, `Code`
  FROM `cert_sys_config`
 WHERE `Category` = 'ai_model'
 ORDER BY `Sort`, `Id`;

-- 归零检查：7 键一个都不能缺（缺 = 1）
SELECT COUNT(*) AS missing_count
  FROM (SELECT 'ai_provider' AS k UNION ALL SELECT 'ai_api_key'
        UNION ALL SELECT 'ai_base_url' UNION ALL SELECT 'ai_model_name'
        UNION ALL SELECT 'ai_max_tokens' UNION ALL SELECT 'ai_temperature'
        UNION ALL SELECT 'ai_vision_config') want
 WHERE NOT EXISTS (SELECT 1 FROM `cert_sys_config` c
                    WHERE c.`ConfigKey` = want.k
                      AND c.`IsDeleted` = 0
                      AND c.`IsValid` = 1);
-- 期望 missing_count = 0
