-- ============================================================================
-- 20261002_ai_config_unify_V1.sql
-- 统一 AI 配置（提示词工作台 Q3 = a）
--
-- 背景：
--   2026-10-02 起，提示词工作台「不再读提示词行上的 ModelName / MaxTokens / Temperature」，
--   一律改读 cert_sys_config 的 ai_model 分类三键 —— 即「只有 Markdown 走缓存、
--   AI 参数全系统统一」的方案甲。
--
-- 本次只动 3 行（幂等，重复执行结果一致）：
--   ai_model_name  : qwen-turbo → qwen-flash
--   ai_max_tokens  : 4096       → 32768
--   ai_temperature : 0.7        → 0.2
--
-- 为什么是这三个值（阿里云百炼官方 help.aliyun.com/zh/model-studio/qwen-flash，2026-10-02 核实）：
--   ① 模型官方 ID 就是 qwen-flash（下划线 qwen_flash **不是**合法 ID）；
--   ② 最大输出 = 32768，打满即可 —— 原 4096 会在长文档分析时把 JSON 截断，
--      与 33 号 §3.3 输出 Schema（tags/purpose/infoItems/fields/tables）冲突；
--   ③ Temperature = 0.2：结构化输出 + 分类判定要稳定，0.7 太随机。
--
-- ⛔ 不动的：
--   · ai_provider / ai_api_key / ai_base_url   —— 本就统一，无需改
--   · wf_prompt_template.ModelName/MaxTokens/Temperature 三列 —— **保留**（不删列），
--     NC 链路 BuildNcPromptSkill.cs 仍按行读 Temperature / MaxTokens，删除会炸
--   · 提示词行上的行级参数**仍在库里**，只是本工作台不读了
--
-- 执行：
--   docker exec -i -e MYSQL_PWD="Yzh123456." yzh-mysql mysql -uroot \
--     --default-character-set=utf8mb4 yzh_cert_platform \
--     < scripts/db/20261002_ai_config_unify_V1.sql
--
-- 设计依据：33-文档语义规则设计-V1.md §4.2 / 34-核心模块定位与端到端流程-V1.md §九
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────────────────────
-- ① 模型名：qwen-turbo → qwen-flash
--    ⚠️ ConfigKey 有唯一索引，用 INSERT ... ON DUPLICATE KEY UPDATE 保证幂等
--    （即使本行此前被手工删掉也能自愈）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `cert_sys_config`
    (`ConfigKey`, `ConfigValue`, `ConfigType`, `Category`, `DisplayName`, `Description`,
     `Sort`, `IsReadonly`, `IsDeleted`, `IsValid`, `Status`, `Code`, `CreateTime`)
VALUES
    ('ai_model_name', 'qwen-flash', 'string', 'ai_model', '模型名称',
     '使用的模型。2026-10-02 统一 AI 配置：qwen-flash（上下文 1M / 最大输出 32768 / 支持结构化输出）',
     13, 0, 0, 1, 'active', 'ai_model_name', NOW())
ON DUPLICATE KEY UPDATE
    `ConfigValue` = 'qwen-flash',
    `Description` = VALUES(`Description`),
    `UpdateTime`  = NOW();

-- ────────────────────────────────────────────────────────────────────────────
-- ② 输出上限：4096 → 32768（qwen-flash 官方最大输出）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `cert_sys_config`
    (`ConfigKey`, `ConfigValue`, `ConfigType`, `Category`, `DisplayName`, `Description`,
     `Sort`, `IsReadonly`, `IsDeleted`, `IsValid`, `Status`, `Code`, `CreateTime`)
VALUES
    ('ai_max_tokens', '32768', 'int', 'ai_model', '最大 Token 数',
     '单次请求最大输出 token。2026-10-02 统一 AI 配置：4096 → 32768（qwen-flash 上限，避免长文档 JSON 截断）',
     14, 0, 0, 1, 'active', 'ai_max_tokens', NOW())
ON DUPLICATE KEY UPDATE
    `ConfigValue` = '32768',
    `Description` = VALUES(`Description`),
    `UpdateTime`  = NOW();

-- ────────────────────────────────────────────────────────────────────────────
-- ③ 温度：0.7 → 0.2（结构化输出要求稳定）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `cert_sys_config`
    (`ConfigKey`, `ConfigValue`, `ConfigType`, `Category`, `DisplayName`, `Description`,
     `Sort`, `IsReadonly`, `IsDeleted`, `IsValid`, `Status`, `Code`, `CreateTime`)
VALUES
    ('ai_temperature', '0.2', 'string', 'ai_model', '温度参数',
     '0-1 之间。2026-10-02 统一 AI 配置：0.7 → 0.2（分类判定与结构化输出要求稳定）',
     15, 0, 0, 1, 'active', 'ai_temperature', NOW())
ON DUPLICATE KEY UPDATE
    `ConfigValue` = '0.2',
    `Description` = VALUES(`Description`),
    `UpdateTime`  = NOW();

-- ────────────────────────────────────────────────────────────────────────────
-- 验证（期望三行且值如右）
--   ai_model_name  = qwen-flash
--   ai_max_tokens  = 32768
--   ai_temperature = 0.2
-- ────────────────────────────────────────────────────────────────────────────
SELECT `ConfigKey`, `ConfigValue`, `ConfigType`, `Category`, `IsDeleted`, `IsValid`, `Status`
  FROM `cert_sys_config`
 WHERE `Category` = 'ai_model'
   AND `ConfigKey` IN ('ai_model_name', 'ai_max_tokens', 'ai_temperature')
 ORDER BY `Sort`;

-- 归零检查：三键一个都不能缺
SELECT COUNT(*) AS missing_count
  FROM (SELECT 'ai_model_name' AS k UNION ALL SELECT 'ai_max_tokens' UNION ALL SELECT 'ai_temperature') want
 WHERE NOT EXISTS (SELECT 1 FROM `cert_sys_config` c
                    WHERE c.`ConfigKey` = want.k AND c.`IsDeleted` = 0 AND c.`IsValid` = 1);
-- 期望 missing_count = 0
