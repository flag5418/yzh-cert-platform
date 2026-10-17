-- ============================================================================
-- 20261017_ai_model_qwen_plus_V1.sql
-- 系统默认文本模型切换：qwen-flash → qwen-plus（幂等）
--
-- 背景（2026-10-17 用户裁决）：
--   系统默认 `cert_sys_config.ai_model_name` 长期为 `qwen-flash`（最便宜档）。
--   工作流 ai_node / 文档语义规则 / 指令提取等**长文档 JSON 分析**任务，flash 档在
--   复杂判定（如 NC 结论、章节归类、整篇摘要）上质量不足 ⇒ 切换为均衡档 `qwen-plus`。
--
-- ★ 事实核对（2026-10-17，列举账号 /compatible-mode/v1/models）：
--   `qwen-plus` 与 `qwen-flash` 均**存在**（可切换）。
--   真实冒烟（2026-10-17）：
--     · qwen-plus 最小请求 → 正常返回；
--     · qwen-plus + max_tokens=32768 → 正常返回（★ 现有 ai_max_tokens=32768 **无需调小**）。
--
-- ⚠️ 只改「系统默认」，⛔ 不动 per-prompt 覆盖（`cert_wf_prompt_template.Model` 为 NULL
--   才回落本默认；显式配了模型的提示词保持原样）。
-- ⚠️ 说明文字**必须同步改**（2026-10-14 教训：只改 ConfigValue 不改 Description ⇒
--   说明仍写 qwen-flash，操作员被误导）—— 但说明里不写死为「唯一答案」，写「默认 + 核对法」。
--
-- 幂等：可重复执行；已是 qwen-plus 则 0 行更新。
-- 回滚：将 ConfigValue 改回 'qwen-flash' 即可（无需备份表，单值可逆）。
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ① 系统默认模型：归一为 qwen-plus
UPDATE `cert_sys_config`
   SET `ConfigValue` = 'qwen-plus',
       `UpdateTime`  = NOW()
 WHERE `ConfigKey` = 'ai_model_name'
   AND `ConfigValue` <> 'qwen-plus';

-- ② 同步「模型」参数说明（原写 qwen-flash，改后必须一致）
UPDATE `cert_sys_config`
   SET `Description` = '使用的模型（系统默认）。默认 qwen-plus（质量/成本均衡；最大输出 32768；支持结构化输出）。切换前先核对账号 /compatible-mode/v1/models 是否存在该名字，否则整条 AI 能力静默不可用。',
       `UpdateTime`  = NOW()
 WHERE `ConfigKey` = 'ai_model_name';

-- ③ 同步「最大输出」参数说明（原写「qwen-flash 上限」）
UPDATE `cert_sys_config`
   SET `Description` = '单次请求最大输出 token。2026-10-02 统一 AI 配置：4096 → 32768（2026-10-17 切 qwen-plus 后仍适用，该上限已实测可用，避免长文档 JSON 截断）',
       `UpdateTime`  = NOW()
 WHERE `ConfigKey` = 'ai_max_tokens';

-- 验证：模型已为 qwen-plus = 1；说明已同步（含 qwen-plus / 不含 qwen-flash）
SELECT `ConfigKey`,
       `ConfigValue`,
       `ConfigValue` = 'qwen-plus' AS 已是plus,
       LOCATE('qwen-plus', `Description`) > 0 AS 说明含plus,
       LOCATE('qwen-flash', `Description`) > 0 AS 说明残留flash
  FROM `cert_sys_config`
 WHERE `ConfigKey` = 'ai_model_name';
