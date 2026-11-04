-- =============================================================================
-- 2026-11-03 认证检查提示词：判定依据改为「认证报告式」Markdown 排版
-- -----------------------------------------------------------------------------
-- 背景：
--   原 audit_check 模板只要求 summary = "判定依据摘要"，模型会把证据、缺口、
--   风险全挤成一大段（实测 5.5.1 条款输出为一整段连续文字），审核员无法审阅。
--
-- 处置：
--   模板改为要求分段输出（结论段 → 证据支持 → 证据缺口/风险，列表用「- 」），
--   换行以 JSON 转义 \n 表达；前端 CheckDetailPage 已按 Markdown-lite 渲染并支持编辑。
--
-- 【幂等】按 Code 更新；备份表 _bak_prompt_20261103。
-- =============================================================================
USE `yzh_cert_platform`;
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 1. 备份（幂等）
CREATE TABLE IF NOT EXISTS `_bak_prompt_20261103` LIKE `wf_prompt_template`;
INSERT IGNORE INTO `_bak_prompt_20261103`
SELECT * FROM `wf_prompt_template`
WHERE `Code` = '611f30bfd43011f1a0746e326917f828';

-- 2. 更新模板（字符串内 \\n 落库为字面量 \n，供模型理解换行指令）
UPDATE `wf_prompt_template`
SET `Template` =
'作为认证审核专家，请根据以下条款要求与已挂接证据，对检查项进行符合性判定。\n\n条款信息：{{ClauseInfo}}\n\n检查项要求：{{CheckRequirement}}\n\n已挂接证据：{{EvidenceList}}\n\n【判定依据 summary 的写法】按正式认证报告的口径书写，便于审核员审阅：\n第一段先用一句话给出结论与总体判断；\n随后分段列出「证据支持」与「证据缺口 / 风险」，每条以「- 」开头，必要时注明条款号、文件或部门；\n段落之间用 \n\n 分隔，列表项之间用 \n 分隔，禁止写成一大段。\n\n只输出 JSON（不要代码块围栏、不要额外文字），summary 等字符串内换行必须转义为 \n：\n{"conclusion":"conformant|minor_nc|major_nc|observation","summary":"...","recommendation":"整改建议","confidence":0-100}',
    `UpdateBy`   = 'migration',
    `UpdateTime` = DATE_ADD(UTC_TIMESTAMP(), INTERVAL 8 HOUR)
WHERE `Code` = '611f30bfd43011f1a0746e326917f828';

-- 3. 验证
SELECT '提示词长度（期望 > 600）' AS Step, LENGTH(`Template`) AS L, `Version`
FROM `wf_prompt_template`
WHERE `Code` = '611f30bfd43011f1a0746e326917f828';
