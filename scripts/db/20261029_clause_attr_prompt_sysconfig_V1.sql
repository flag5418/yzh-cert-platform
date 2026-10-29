-- ============================================================================
-- 20261029_clause_attr_prompt_sysconfig_V1.sql
--   ① 属性池加 `PromptHint`（属性级生成契约）
--   ② 条款属性预生成提示词改存 `cert_sys_config`（单条全局参数，取代 wf_prompt_template）
--   ③ 旧 wf_prompt_template 的 clause_attr 行停用（保留行，不删 —— 留痕可回溯）
-- ★ 幂等：可重复执行；已有值不覆盖（提示词与 PromptHint 都是会被人工调优的资产）
-- ============================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ---------------------------------------------------------------------------
-- ① 属性池：加 PromptHint
--   为什么单独一列而不是复用 Remark：
--     Remark 是「作用」（≤500 字，给配置者看），PromptHint 是「取值要求」
--     （给模型看，Artifacts / EvidenceChain 需要整段结构化契约），语义不同、长度不同。
-- ---------------------------------------------------------------------------
SET @has_col := (
  SELECT COUNT(*) FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'cert_clause_attr_def'
    AND COLUMN_NAME = 'PromptHint'
);
SET @ddl := IF(@has_col = 0,
  'ALTER TABLE `cert_clause_attr_def` ADD COLUMN `PromptHint` TEXT NULL COMMENT ''预生成提示词里的取值要求（属性级生成契约）；空 = 按值类型兜底'' AFTER `Remark`',
  'SELECT ''PromptHint 已存在，跳过'' AS note');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 回填 8 条属性（仅当为空 —— 不冲掉人工调优过的内容）
UPDATE `cert_clause_attr_def` SET `PromptHint` =
'一句话说清这条款到底在管什么（不超过 40 字）。要提炼「管什么」，⛔ 不要抄条款原文，⛔ 不要写成「应建立并保持…」这种把条款换个说法的套话。'
WHERE `Code` = 'ClauseIntent' AND (`PromptHint` IS NULL OR `PromptHint` = '');

UPDATE `cert_clause_attr_def` SET `PromptHint` =
'看条款**主句**（第一句带义务动词的话）的措辞定档：应/必须/shall ⇒ shall；宜/建议/should ⇒ should；可/可以/may ⇒ may；纯「注」⇒ note。一条里出现多种措辞时取主句，⛔ 不要取最严格的。'
WHERE `Code` = 'Obligation' AND (`PromptHint` IS NULL OR `PromptHint` = '');

UPDATE `cert_clause_attr_def` SET `PromptHint` =
'这条要求是不是所有该标准的企业都得做：都必做 ⇒ general；只在特定条件下才适用（「当…时」「适用时」「如有时」「组织承担设计职责时」）⇒ conditional。⚠️ 能申请认证 ≠ 所有条款都适用，按业务实质判，⛔ 不要一律给 general。'
WHERE `Code` = 'Applicability' AND (`PromptHint` IS NULL OR `PromptHint` = '');

UPDATE `cert_clause_attr_def` SET `PromptHint` =
'仅 Applicability = conditional 时填：把触发条件写成一句可判断的话（如「组织承担产品设计职责」「存在外包过程」），⛔ 不许写「视情况而定」。general 时给空串。'
WHERE `Code` = 'ApplicabilityCondition' AND (`PromptHint` IS NULL OR `PromptHint` = '');

UPDATE `cert_clause_attr_def` SET `PromptHint` =
'从这个字典里选 1~3 个最相关的过程码，按相关度排序，输出 JSON 数组。⛔ 不许自造码，⛔ 不许把所有过程都选上（全选等于没选）。'
WHERE `Code` = 'Process' AND (`PromptHint` IS NULL OR `PromptHint` = '');

UPDATE `cert_clause_attr_def` SET `PromptHint` =
'产出一段**提示词正文**（不是结论）。将来这段提示词会连同企业提交的资料一起喂给模型，让模型回答「材料里满足这条要求的成文信息有哪些」。因此必须：
① 明确要求模型输出结构化 JSON 数组，元素结构固定为 {"Kind":"材料类型码","FileName":"文件名","DeviceNo":"设备/器具编号，无则省略","ValidUntil":"有效期至，无则省略","Complete":true或false}，Kind 只能取 ledger(台账)/certificate(证书)/procedure(程序文件)/record(记录)/list(清单)/report(报告)/plan(计划)/license(资质证照)/mark(标识)/other(其他)；
② 明确要求只报「读到了什么」，⛔ 不许判断符合/不符合，⛔ 不许给整改建议；
③ 明确要求找不到就交空数组，⛔ 不许推测、不许编造文件名；
④ 用条款原文里的关键词描述要找的东西（本条讲「监视和测量资源」就写清「计量器具台账 / 校准证书 / 检定记录」），但只说找什么，不说怎么抽样。
⚠️ 这段提示词会被企业看到（它是企业准备资料的清单）⇒ ⛔ 绝不能写取证手法、抽样数量、访谈问题。'
WHERE `Code` = 'Artifacts' AND (`PromptHint` IS NULL OR `PromptHint` = '');

UPDATE `cert_clause_attr_def` SET `PromptHint` =
'产出一段**提示词正文**（不是结论）。将来这段提示词给审核员用，让模型生成「到这家企业该怎么取证」的步骤。因此必须：
① 明确要求模型输出结构化 JSON 数组，元素结构固定为 {"Kind":"site|record|interview|doc","Action":"做什么","Target":"去哪个部门/看哪份文件/问谁","Sample":"抽多少（如 3 个样本/近 12 个月），无则省略","Finding":"实际发现，取证时填写，此处留空"}，Kind 含义：site=现场观察 / record=查记录 / interview=访谈 / doc=查文件；
② 明确要求只描述「去哪里、看什么、抽多少、问什么」，⛔ 不许预先写结论，⛔ 不许写符合/不符合；
③ 明确要求找不到证据就留空，⛔ 不许推测；
④ 至少覆盖 2 种 Kind（纯查文件不足以证明体系在运行）。
⚠️ 这是取证剧本，⛔ 不提前给企业。'
WHERE `Code` = 'EvidenceChain' AND (`PromptHint` IS NULL OR `PromptHint` = '');

UPDATE `cert_clause_attr_def` SET `PromptHint` =
'违反这条通常算几级不符合：critical = 涉及法规强制要求 / 产品安全有效 / 证书真实性（如医疗器械上市许可、灭菌、可追溯）；major = 涉及体系运行的关键过程控制（设计控制、采购控制、不合格品控制、内审与管理评审）；minor = 文件措辞、记录格式、职责描述的完整性。'
WHERE `Code` = 'Severity' AND (`PromptHint` IS NULL OR `PromptHint` = '');

-- ---------------------------------------------------------------------------
-- ② 提示词改存系统参数：cert_sys_config / ConfigKey = clause_attr_prompt
--
--    ★ 为什么不放 wf_prompt_template（2026-10-29 用户裁决）：
--      这条提示词**对全部标准、全部条款生效**，是一条「全局配置」，不是"某标准的工作流资产"。
--      放 wf_prompt_template 会带来 Version / IsActive / StandardCode 三层机制，
--      现实里只会制造「标准级有专属 → 改了平台级不生效」的静默坑。
--      ⇒ 单条全局参数，与 AI 参数（ai_model 分类）同表同源。
--
--    ★ 与属性池联动：正文里的 {{attr_pool}} 由后端按 `cert_clause_attr_def` **实时渲染**，
--      属性池增删改属性 ⇒ 提示词自动跟上，⛔ 不需要再改这里的正文。
-- ---------------------------------------------------------------------------
INSERT INTO `cert_sys_config`
  (`Code`, `ConfigKey`, `ConfigValue`, `ConfigType`, `Category`, `DisplayName`,
   `Description`, `Sort`, `IsReadonly`, `Status`, `IsValid`, `IsDeleted`,
   `CreateBy`, `CreateTime`)
VALUES
  ('clause_attr_prompt', 'clause_attr_prompt',
   '你是 ISO 管理体系认证的资深审核技术专家。你的任务：读一条标准条款，为这条条款产出「属性配置」JSON。

## 输入

- 标准：{{standard_name}}
- 条款号：{{clause_number}}
- 条款标题：{{clause_title}}
- 条款正文：
{{clause_content}}

## 输出

只输出一个 JSON 对象，不要 markdown 代码块，不要任何解释文字。
字段名 = 文末「属性清单」里的字段名，**逐个都要输出**；⛔ 不许增、删、改名，⛔ 不许输出清单外的字段。

## 通用判据

① 先读条款正文的**主句**（第一句带义务动词的话），再看细节；正文为空或纯说明性 ⇒ 按「不产生检查要求」处理，各字段给空值。
② 出现「适用时 / 如有时 / 当…时」这类限定语 ⇒ 该条款是**条件适用**，必须把触发条件写出来，⛔ 不许笼统写「视情况而定」。
③ 判不了就留空串（或空数组），⛔ 不许编造、⛔ 不许用通用套话把字段填满。空值 = 待人工确认，是合法输出。
④ 所有文本用简体中文；字段名保持英文原样。

## 硬约束

- ⛔ 「企业该提供什么」与「审核员怎么取证」必须分开产出（各归各的属性），合并会导致企业照着取证剧本准备、审核失效。
- ⛔ 不许把条款原文抄一遍充当答案。
- 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。

{{attr_pool}}',
   'string', 'clause_attr', '条款属性预生成提示词',
   '「条款属性配置」页 AI 预生成用的通用提示词（对全部标准、全部条款生效）。正文里的 {{attr_pool}} 由系统按条款属性池实时渲染 —— 改属性池即可，无需改这里。',
   100, 0, 'active', 1, 0,
   'clause-attr-20261029', NOW())
ON DUPLICATE KEY UPDATE `ConfigKey` = `ConfigKey`;

-- ---------------------------------------------------------------------------
-- ③ 旧提示词停用（⛔ 不删：留痕；wf_prompt_template 的其它 PromptType 不受影响）
-- ---------------------------------------------------------------------------
UPDATE `wf_prompt_template`
SET `IsActive` = 0
WHERE `PromptType` = 'clause_attr' AND `IsDeleted` = 0;

-- ---------------------------------------------------------------------------
-- 验证（期望：col_ok=1；8 行 PromptHint 非空；config 1 行；旧提示词 0 条生效）
-- ---------------------------------------------------------------------------
SELECT
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cert_clause_attr_def'
       AND COLUMN_NAME = 'PromptHint') AS col_ok;
SELECT `Code`, `ValueType`, CHAR_LENGTH(`PromptHint`) AS hint_len
FROM `cert_clause_attr_def` WHERE `IsDeleted` = 0 ORDER BY `Sort`;
SELECT `ConfigKey`, `Category`, CHAR_LENGTH(`ConfigValue`) AS tpl_len, `IsValid`
FROM `cert_sys_config` WHERE `ConfigKey` = 'clause_attr_prompt';
SELECT COUNT(*) AS active_legacy_prompts
FROM `wf_prompt_template`
WHERE `PromptType` = 'clause_attr' AND `IsDeleted` = 0 AND `IsActive` = 1;
