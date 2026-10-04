-- ============================================================================
-- 20261004_ai_vision_config_V1.sql
-- 视觉模型（图片 / 扫描件 → Markdown）系统参数
--
-- 背景（36 号 §十 L7 / 25 号 §五 P0-1）：
--   · `DefaultOcrProvider` 此前是恒 `IsAvailable => false` 的空壳 ⇒ 图片/扫描件全链路无能力；
--   · `LlmInvokeService` 只发纯文本 ⇒ 视觉模型根本吃不到图。
--   两者已补齐，本参数是**唯一**的视觉配置入口。
--
-- ★ 为什么是「一条 JSON」而不是像文本那样六个 ai_* 键（用户 2026-10-03 裁决）：
--   文本模型只有 base_url/api_key/model/max_tokens/temperature 五个维度，拆键没问题；
--   视觉还要加「提示词 / 厂商开关 / 超时 / 是否启用 / 压缩阈值」⇒ 键会膨胀到十几个。
--   做成 JSON 的最大好处：**换厂商只改一条配置、不改代码**（先用百炼，将来可切 agnes / 私有化）。
--
-- ⚠️ 边界（诚实声明）：
--   1. 图片按 token 计费（实测 1520×1240 ≈ 1874 image_tokens），ImageMaxEdge 钩子已留但缩放库未接；
--   2. 多页扫描 PDF **只识别第 1 页**（soffice --convert-to png 只能出首页）；
--      彻底解决需给 yzh-libreoffice 镜像加 poppler-utils 并逐页栅格化。
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

INSERT INTO `cert_sys_config`
  (`Code`, `ConfigKey`, `ConfigValue`, `ConfigType`, `Category`, `DisplayName`,
   `Description`, `Sort`, `IsReadonly`, `Status`, `Remark`, `CreateTime`, `CreateBy`, `IsDeleted`, `IsValid`)
VALUES
  ('ai-vision-config', 'ai_vision_config',
'{
  "Enabled": true,
  "Provider": "qianwen",
  "Model": "qwen3-vl-flash",
  "BaseUrl": "",
  "ApiKey": "",
  "TimeoutSeconds": 180,
  "MaxTokens": 8192,
  "Temperature": 0.1,
  "DocumentParsePrompt": "qwenvl markdown",
  "ImageJudgePrompt": "判断这份资料的类型。只输出一个 JSON：{\\"kind\\":\\"体系文件|营业执照|身份证|资质证书|许可证|检测报告|其他\\",\\"confidence\\":0.0-1.0,\\"reason\\":\\"一句话\\"}",
  "ImageMaxEdge": 1600,
  "MaxImageBytes": 12582912
}',
  'json', 'ai_model', '视觉模型配置',
  '图片/扫描件 → Markdown。BaseUrl/ApiKey 留空则回填 ai_base_url/ai_api_key（同厂商不必重复配；切 agnes 在此覆盖）',
  100, 0, 'active',
  '图片/扫描件 → Markdown。BaseUrl/ApiKey 留空则回填 ai_base_url/ai_api_key（同厂商不必重复配；切 agnes 在此覆盖）',
  NOW(), 'seed_ai_vision', 0, 1)
ON DUPLICATE KEY UPDATE
  `ConfigValue` = VALUES(`ConfigValue`),
  `Remark`     = VALUES(`Remark`),
  `UpdateTime` = NOW();

-- 验证
-- SELECT ConfigKey, LEFT(ConfigValue, 200) FROM cert_sys_config WHERE ConfigKey = 'ai_vision_config';
