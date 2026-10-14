-- ============================================================
-- 视觉模型纠错 + 降本（2026-10-14）
--
-- 起因：PromptWorkbenchService 试跑接入视觉 OCR 后，实测上传 PNG 报
--   404 Model not exist（model_not_found）。
--   根因：ai_vision_config.Model = qwen3-vl-flash 在该账号【不存在】
--   （2026-10-14 列举 /compatible-mode/v1/models，可用视觉模型：
--     qwen3-vl-plus / qwen-vl-ocr / qwen-vl-ocr-2025-11-20 / qwen3.5-ocr）。
--   ⇒ 图片 / 扫描件识别整条能力实际不可用（此前工作台直连 anydoc 掩盖了它）。
--
-- ★ 选型：改用【最便宜】的专用 OCR 模型 qwen-vl-ocr
--   官方定价（华北2·北京，元/百万 Token，2026-10-14 查 help.aliyun.com/zh/model-studio/model-pricing）：
--     qwen-vl-ocr     : 输入 0.3  输出 0.5   ← 选用（Batch 更低：0.15/0.25）
--     qwen3.5-ocr     : 输入 0.5  输出 2
--     qwen3-vl-plus   : 输入 1 / 1.5 / 3（阶梯）  输出 10 / 15 / 30
--   相对 qwen3-vl-plus（≤32K 档），输出单价低 20 倍、输入低约 3 倍；
--   且 qwen-vl-ocr 名称含 "vl" ⇒ 通过 VisionSettings.LooksLikeVisionModel 闸门
--   （⚠️ qwen3.5-ocr 不含 vl/vision/omni，会被该闸门拦下，不可用）。
--   实测（2026-10-14）：同一张中文 PNG，qwen-vl-ocr 正确产出 Markdown 与 items[] 结论。
--
-- 幂等：可重复执行；把所有历史模型名归一为 qwen-vl-ocr。
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ① 带空格写法
UPDATE `cert_sys_config`
   SET `ConfigValue` = REPLACE(REPLACE(REPLACE(REPLACE(
                         `ConfigValue`,
                         '"Model": "qwen3-vl-flash"', '"Model": "qwen-vl-ocr"'),
                         '"Model": "qwen3-vl-plus"',  '"Model": "qwen-vl-ocr"'),
                         '"Model": "qwen-vl-max"',    '"Model": "qwen-vl-ocr"'),
                         '"Model": "qwen-vl-ocr"',    '"Model": "qwen-vl-ocr"'),
       `UpdateTime`  = NOW()
 WHERE `ConfigKey` = 'ai_vision_config';

-- ② 紧凑写法（无空格）
UPDATE `cert_sys_config`
   SET `ConfigValue` = REPLACE(REPLACE(REPLACE(
                         `ConfigValue`,
                         '"Model":"qwen3-vl-flash"', '"Model":"qwen-vl-ocr"'),
                         '"Model":"qwen3-vl-plus"',  '"Model":"qwen-vl-ocr"'),
                         '"Model":"qwen-vl-max"',    '"Model":"qwen-vl-ocr"'),
       `UpdateTime`  = NOW()
 WHERE `ConfigKey` = 'ai_vision_config';

-- ③ 参数信息（Description / Remark）补齐 —— ★ 2026-10-14
--
-- 起因：本次只改了 ConfigValue（Model），**说明文字一字未动**；
--   而原说明只讲「BaseUrl/ApiKey 回填」⇒ 操作员看不到
--   ① 「必须填视觉模型」这条硬规则（名字闸门，违反 = 整条能力静默禁用）；
--   ② 当前建议填什么模型；③ 图片按 token 计费。
--   正是这个空白的说明，让「配了个不存在的模型」持续了几个礼拜都没人发现。
--
-- ⚠️ 说明里**不写死具体模型为唯一答案**（写死会在换模型时立刻过时），
--   而是写「规则 + 示例」：规则长期有效，示例随行更新。
UPDATE `cert_sys_config`
   SET `Description` = '图片/扫描件 → Markdown。⭐ Model 必须是视觉模型（名字含 vl / vision / omni，如 qwen-vl-ocr），否则整条视觉能力被静默禁用；BaseUrl/ApiKey 留空则回填 ai_base_url/ai_api_key（同厂商不必重复配）。图片按 token 计费（长边压到 ImageMaxEdge 约省一半）。',
       `Remark`      = '图片/扫描件 → Markdown。⭐ Model 必须是视觉模型（名字含 vl / vision / omni，如 qwen-vl-ocr），否则整条视觉能力被静默禁用；BaseUrl/ApiKey 留空则回填 ai_base_url/ai_api_key（同厂商不必重复配）。图片按 token 计费（长边压到 ImageMaxEdge 约省一半）。',
       `UpdateTime`  = NOW()
 WHERE `ConfigKey` = 'ai_vision_config';

-- 验证：已选便宜模型 = 1；旧模型全部 = 0；说明已含「视觉模型」闸门规则 = 1
SELECT `ConfigKey`,
       LOCATE('"qwen-vl-ocr"',    `ConfigValue`) > 0 AS 已选便宜OCR,
       LOCATE('qwen3-vl-flash',   `ConfigValue`) > 0 AS 仍有flash,
       LOCATE('qwen3-vl-plus',    `ConfigValue`) > 0 AS 仍有plus,
       LOCATE('视觉模型',          `Description`) > 0 AS 说明含闸门规则,
       LOCATE('qwen-vl-ocr',      `Description`) > 0 AS 说明含示例模型
  FROM `cert_sys_config`
 WHERE `ConfigKey` = 'ai_vision_config';
