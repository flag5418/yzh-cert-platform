-- ============================================================================
-- 20261002_prompt_workbench_V1.sql
-- 提示词工作台 —— 表结构补列 + 首批提示词种子
--
-- 设计依据（2026-10-02 用户逐字裁决）：
--   「一个机构，一个标准，一个阶段来生成不同的分组提示词和文件内容提示词」
--   → 后续修正：「我统一只按标准分，阶段没太大意义」
--   「企业材料一般都会有标题，可以先提取生成 markdown 文件后，再通过语义分析获取该文件具体的作用」
--   「选择一个分类的时候，针对分类和作用都有一个 ai 自动生成」
--   「我们可以针对标题提示词和作用提示词进行修改，我们还可以在该界面上传文件进行测试
--     （上传的时候，自动转 markdown），调用提示词得到结果，后直接删除该文件」
--   「我支持在控制成本情况下合理切换模型，并设置模型参数」
--     → ⛔ 2026-10-02 用户复议后**作废**（Q3 = a）：改「统一 AI 配置」，
--        模型/参数一律读 cert_sys_config 三键，本工作台不再逐条覆盖。
--
-- ⛔ 铁律：
--   1. DB列名 = C#属性名 = TS字段名，PascalCase 逐字一致
--   2. 启用/禁用唯一字段 = IsValid（int，0/1）。⛔ 禁 Enable
--   3. StandardCode 存 cert_iso_standard.Code（GUID），⛔ 不是可读编码 iso9001-2015
--   4. 前置：先跑 20261002_tag_dict_seed_V1.sql —— 提示词注入 {{tag_list}}，
--      cert_tag_dict 为空时提示词退化为「字典暂无标签」，输出必然全部越界
-- ============================================================================

SET NAMES utf8mb4;

-- ────────────────────────────────────────────────────────────────────────────
-- 一、wf_prompt_template 补列（作用域；模型参数三列已转为遗留列，本工作台不再读）
-- ────────────────────────────────────────────────────────────────────────────
-- StandardCode：提示词适用的标准（GUID）；NULL = 不限（平台默认 / 跨标准通用）
-- ModelName / MaxTokens / Temperature：本提示词专用的模型配置；NULL = 用 cert_sys_config 系统默认
--   ★ 动机（用户口径）：「支持在控制成本情况下合理切换模型，并设置模型参数」
--     分类任务可用 qwen-flash（更便宜），内容提取用 qwen-plus（更稳）—— 逐条提示词可覆盖
-- ────────────────────────────────────────────────────────────────────────────

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_prompt_template' AND COLUMN_NAME = 'StandardCode') = 0,
  'ALTER TABLE `wf_prompt_template` ADD COLUMN `StandardCode` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''★适用标准（cert_iso_standard.Code，GUID）；NULL=不限（平台默认/跨标准通用）'' AFTER `SkillTarget`',
  'SELECT ''StandardCode 已存在，跳过'' AS Info');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_prompt_template' AND COLUMN_NAME = 'ModelName') = 0,
  'ALTER TABLE `wf_prompt_template` ADD COLUMN `ModelName` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci DEFAULT NULL COMMENT ''本提示词专用模型名（如 qwen-flash / qwen-plus）；NULL=用系统默认 ai_model_name'' AFTER `Template`',
  'SELECT ''ModelName 已存在，跳过'' AS Info');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_prompt_template' AND COLUMN_NAME = 'MaxTokens') = 0,
  'ALTER TABLE `wf_prompt_template` ADD COLUMN `MaxTokens` int DEFAULT NULL COMMENT ''本提示词专用输出上限；NULL=用系统默认 ai_max_tokens'' AFTER `ModelName`',
  'SELECT ''MaxTokens 已存在，跳过'' AS Info');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_prompt_template' AND COLUMN_NAME = 'Temperature') = 0,
  'ALTER TABLE `wf_prompt_template` ADD COLUMN `Temperature` decimal(3,2) DEFAULT NULL COMMENT ''本提示词专用温度（0.00~1.00）；NULL=用系统默认 ai_temperature'' AFTER `MaxTokens`',
  'SELECT ''Temperature 已存在，跳过'' AS Info');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- 作用域索引：按「类型 + 标准」定位（三层回退：标准级 → 平台级）
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'wf_prompt_template' AND INDEX_NAME = 'idx_prompt_scope') = 0,
  'ALTER TABLE `wf_prompt_template` ADD INDEX `idx_prompt_scope` (`PromptType`, `StandardCode`, `IsActive`)',
  'SELECT ''idx_prompt_scope 已存在，跳过'' AS Info');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ────────────────────────────────────────────────────────────────────────────
-- 二、提示词种子（★ 2026-10-02 重写：对齐 33 号 §3.3 输出 Schema + §4.2 占位符）
--     ★ 2026-10-02 二次修订（33 号 D.4-2）：2.1 元提示词首版实测只回吐 680 字纯 JSON Schema
--       ——「下游模型只输出 JSON」被模型误读成对自己的输出要求，且正文无骨架。
--       改法：§二 改「先分清你输出什么 / 下游模型输出什么」对照表；新增 §五 正文骨架（7 段 + ≥600 字）；
--       §七 补「⛔ 不要输出 JSON Schema 本体 / 示例 JSON」；章节顺延重编号 五→六、六→七、七→八。
--       配套后端兜底：PromptWorkbenchService.ValidateGeneratedPrompt（JSON 本体 / 缺占位符 / <400 字）
--       → 携错因纠偏重问一次，仍不合格直接 Fail。
-- ────────────────────────────────────────────────────────────────────────────
-- PromptType 约定：
--   prompt_generator = 元提示词（L5 元生成层；「AI 生成」/「AI 优化」两用）
--   doc_group        = ★ 分组提示词（吃文件名清单，批量 → 每文件 tags + 策略）
--   doc_content      = ★ 作用提示词（吃单份 Markdown 全文 → §3.3 完整对象）
--
-- ★ 三条提示词共用的占位符（由 PromptWorkbenchService.BuildSemanticContextAsync 注入，
--   渲染在 {{file_list}} / {{document_content}} 注入**之前**）：
--     {{tag_list}}       标签清单（cert_tag_dict，按标准裁剪）
--     {{output_schema}}  §3.3 输出 JSON Schema
--     {{tag_constraint}} 只能从字典选的硬约束
--     {{code_prefix_map}} L0 编号前缀规则（分组用）
--     {{folder_tree}}    标准目录文件夹树
-- ────────────────────────────────────────────────────────────────────────────

-- 定位 9001 标准 Code（GUID）；找不到则 2.2/2.3 落到平台级（StandardCode NULL）
SET @std9001 := (SELECT `Code` FROM `cert_iso_standard`
                 WHERE `StandardCode` = 'iso9001-2015' AND `IsDeleted` = 0 LIMIT 1);

-- ── 2.1 元提示词（prompt_generator）—— 生成 / 优化两用 ────────────────────────
INSERT INTO `wf_prompt_template`
  (`Code`, `PromptCode`, `PromptName`, `PromptType`, `SkillTarget`, `StandardCode`,
   `Template`, `Description`, `Version`, `IsActive`,
   `ModelName`, `MaxTokens`, `Temperature`,
   `Status`, `IsDeleted`, `IsValid`, `CreateTime`)
VALUES
  (UUID(), 'prompt_generator', '提示词生成器（元提示词）', 'prompt_generator', NULL, NULL,
'你是体系认证信息化系统的提示词工程师。请按「本次任务」生成（或优化）一条可直接投入生产的业务提示词。

## 一、两种工作模式

**work_mode = generate（从零生成）**
依据「本次任务」里的标准信息，从头写一条完整提示词。

**work_mode = optimize（优化现有内容）**
「现有提示词」字段给出了一条**人工手写的**提示词。请**保留**它的业务判断口径、分类体系、
判定优先级与任何人工校正过的细节，只做以下修补，**禁止推倒重写**：
1. 补齐缺失的硬约束（一文档多标签、依据不足用 OTHER、只输出 JSON）
2. 修正输出结构，使其与「输出 JSON Schema」完全一致
3. 确保标签清单位置使用**标签清单占位符**（见下方书写铁律）
4. 删除与输出结构无关的冗余说明
优化后正文里若已含占位符，保持原样不要改动其写法。

## 二、先分清「你输出什么」与「下游模型输出什么」（★ 最容易搞错的一节）

| 谁 | 输出什么 |
|---|---|
| **你**（提示词工程师） | **提示词正文**：Markdown 指令文本，有标题、有规则、有表格，是写给下游 AI 看的 |
| **下游 AI 模型**（运行时读你写的提示词） | **只输出 JSON**，不解释、不加 Markdown 代码围栏 |

⚠️ 下面整节描述的是**下游模型**的输出契约，**不是**你现在要输出的格式。
**你绝对不能只丢一份 JSON 就交差** —— 把契约当成正文 = 失败输出，会被直接打回。

【输出 JSON Schema —— 以契约为准】
（该字段由系统在运行时注入到业务提示词中；**你在正文里只写占位符名 output_schema，
禁止把 JSON 本体粘贴进正文**）

下游模型必须遵守的契约要点（供你在正文里写说明用）：
- `tags[]`：每项 `tagCode` / `tagName` / `confidence` / `reason`。**一文档多标签，至少一个**
- `purpose`：四段式作用 —— 【是什么】【审核关注点】【来源口径】【包含信息】
- `infoItems[]`：要素清单（`itemName` / `itemDesc` / `valueType` / `isKey`），供覆盖度计算
- `typeGuess` / `fields[]` / `tables[]`：类型猜测与结构化抽取
- `suggestedPolicy`（只能取 `analyze` / `skip` / `ignore`）与 `policyReason`
- `confidence`：0~1

## 三、A. 分类提示词（prompt_type = doc_group）

- 粒度：**批量**，输入是文件清单（文件名 + 标题 + 开头片段），**不是**全文
- 输出外层是 `items` 数组，每项含 `index` / `fileName`，再加**子集**：
  `tags` / `suggestedPolicy` / `policyReason` / `purpose` / `confidence`
  （**不要**要求它输出 `fields` / `tables` / `infoItems`）
- 必须写清判定优先级：**编号前缀确定性规则 > 文档标题关键词 > 文件名 > 开头片段 > 兜底 OTHER**
- 必须写清策略口径：编号或标题即可确定且 confidence ≥ 0.9 → `skip`；
  与体系认证无关 → `ignore`；其余 → `analyze`
- 必须保留「标签清单位置」与「编号前缀规则位置」两个占位符
- 结尾必须保留**文件清单占位符**

## 四、B. 作用提示词（prompt_type = doc_content）

- 粒度：**单份**，输入是该文件的 Markdown 全文
- 输出**单个对象**（不套 `items`），字段取「输出 JSON Schema」**全部**字段
- 必须写清四段式 `purpose` 的写法与 100~400 字篇幅要求
- 必须写清判定原则：基于**实际内容**，禁止按文件名臆测；
  内容为空或纯图片无文本时 `purpose` 给「无法识别」、`tagCode` 给 `OTHER`、
  `confidence` ≤ 0.5
- 必须写清 `fields` / `tables` 的抽取规则：Markdown 表格必须进 `tables` 不得拆成字段；
  `fields` 只取普通段落中的标签:值对
- 结尾必须保留**单文件全文占位符**

## 五、正文骨架（照这个骨架成文，禁止自由发挥）

你输出的提示词正文必须按下表顺序成文（小节标题可微调，**结构不可少、顺序不可乱**）：

| 序 | 小节 | 内容要点 |
|---|---|---|
| 1 | 角色与目标 | 一句「你是…」+ 这条提示词要完成什么 |
| 2 | 输入说明 | 下游模型会拿到什么：文件清单（A）/ 单份 Markdown 全文（B） |
| 3 | 判定规则 | 分类优先级、判定原则、空文档与臆测的处理、置信度口径 |
| 4 | 标签清单 | 标签清单占位符 + 硬约束占位符 +「只能从字典选、依据不足给 OTHER」 |
| 5 | 输出结构 | 输出结构占位符 +「只输出 JSON，无解释、无围栏」硬约束 |
| 6 | 运行规则 | 禁止编造、禁止遗漏、字段取值范围、篇幅要求 |
| 7 | 待处理数据 | 文件清单占位符（仅 A）或单文件全文占位符（仅 B），放正文**末尾** |

硬性要求：
- 用 Markdown 小节标题分节（`##` / `###`），**全文不少于 600 字**
- 占位符必须放在**它该被注入的那句话**上，不要全堆到末尾（末尾只留第 7 条）
- 正文必须读起来像一份**可直接投产的生产提示词**，不是提纲、不是需求说明、不是 JSON

## 六、占位符书写铁律（★ 不可违反）

你输出的提示词正文里，下列占位符必须**原样出现**，供系统运行时替换。
写法 = **两个左花括号 + 名称 + 两个右花括号，中间禁止空格**：

| 名称 | 含义 | 哪条提示词需要 |
|---|---|---|
| 标签清单占位符（名称 `tag_list`） | 该标准启用的标签，LLM 只能从中选 | A 和 B 都必须 |
| 输出结构占位符（名称 `output_schema`） | 上面那份 JSON Schema | A 和 B 都必须 |
| 硬约束占位符（名称 `tag_constraint`） | 只能从字典选的约束文案 | A 和 B 都必须 |
| 文件清单占位符（名称 `file_list`） | 本批次文件清单 | 仅 A |
| 全文占位符（名称 `document_content`） | 单份 Markdown 全文 | 仅 B |
| 编号前缀占位符（名称 `code_prefix_map`） | L0 确定性规则 | A 建议加 |

> ⚠️ 本说明里的占位符**故意不写花括号** —— 若写了，本元提示词自身在渲染时会被提前替换掉，
> 你就看不到占位符的名字了。你输出时**必须补上花括号**。

## 七、生成要求

- 面向**真实生产**，是写给 AI 模型看的指令，不是给人看的教学说明
- 分类提示词要贴合该标准实际会收到的资料；作用提示词要能对应到该标准的条款体系
- 只输出**提示词正文本身**：无前言、无说明、无 Markdown 代码围栏
- ⛔ 不要输出 JSON Schema 本体、不要输出示例 JSON、不要输出「以下是提示词」之类的引导语
- 中文撰写

## 八、本次任务

- 工作模式（work_mode）：{{work_mode}}
- 提示词类型（prompt_type）：{{prompt_type}}
- 提示词类型中文名：{{prompt_type_name}}
- 适用标准：{{standard_name}}
- 额外要求：{{extra_requirement}}
- 现有提示词（work_mode = optimize 时必读，为空则从零生成）：
{{current_template}}',
'元提示词（L5 元生成层）：按「标准 + 类型」生成或优化 doc_group / doc_content 两条业务提示词。由工作台「AI 生成」「AI 优化」按钮调用，结果不落库，用户确认后保存。',
   1, 1, NULL, NULL, NULL,
   'active', 0, 1, NOW())
ON DUPLICATE KEY UPDATE
  `PromptName` = VALUES(`PromptName`), `PromptType` = VALUES(`PromptType`),
  `Template` = VALUES(`Template`), `Description` = VALUES(`Description`),
  `ModelName` = NULL, `MaxTokens` = NULL, `Temperature` = NULL,
  `IsValid` = 1, `IsDeleted` = 0, `UpdateTime` = NOW();

-- ── 2.2 分组提示词（doc_group）—— 批量吃文件清单 ─────────────────────────────
INSERT INTO `wf_prompt_template`
  (`Code`, `PromptCode`, `PromptName`, `PromptType`, `SkillTarget`, `StandardCode`,
   `Template`, `Description`, `Version`, `IsActive`,
   `ModelName`, `MaxTokens`, `Temperature`,
   `Status`, `IsDeleted`, `IsValid`, `CreateTime`)
VALUES
  (UUID(), 'doc_group_iso9001', 'ISO 9001 分组提示词（标题/分类）', 'doc_group', NULL, @std9001,
'你是体系认证的文档分类与语义标注专家。
下方给出本批次企业提交的资料文件清单（含文件名、文档标题、开头片段）。
请为**清单里的每一个文件**给出：标签集合、处理策略、一句话作用、置信度。

## 一、可选标签（tagCode 只能取下面出现的编码，禁止自造）

{{tag_list}}

## 二、编号前缀确定性规则（L0：命中即定标签，优先于语义判断）

{{code_prefix_map}}

## 三、输出结构（严格 JSON，只输出 JSON，无解释、无 Markdown 围栏）

外层固定为 items 数组，**一个文件一条**，数组元素按下表字段输出
（**只要** index / fileName / tags / suggestedPolicy / policyReason / purpose / confidence；
**不要**输出 fields / tables / infoItems / typeGuess）：

{{output_schema}}

示例：
{"items":[{"index":1,"fileName":"XASL-QR-014 年度内审计划.doc",
 "tags":[{"tagCode":"RecordForm","tagName":"记录表格","confidence":0.95,"reason":"编号前缀 QR- 且标题含「计划」"},
         {"tagCode":"InternalAudit","tagName":"内部审核","confidence":0.96,"reason":"标题含「内审」"}],
 "suggestedPolicy":"skip","policyReason":"编号前缀与标题双重确定",
 "purpose":"年度内部审核的策划安排，明确审核范围、日程与分工。",
 "confidence":0.95}]}

## 四、标签规则

- **一文档多标签，至少一个**：既给「形态」标签（记录表格/程序文件…），
  也给「业务过程」标签（内部审核/管理评审…），能确定时尽量给全
- 编号前缀或标题能直接判定 → `confidence` ≥ 0.9，`reason` 引用**命中的原文**
- 无编号、片段信息不足 → 降低 `confidence`，`reason` 写清依据不足，标签仍从字典里选最接近的一个
- 彻底无法归类 → `tagCode` = `OTHER`

## 五、处理策略 suggestedPolicy（三选一，必须给）

| 值 | 判定条件 | 后续动作 |
|---|---|---|
| `skip` | 编号前缀 + 标题即可确定标签，且 `confidence` ≥ 0.9 | 不再读全文精判 |
| `ignore` | 与体系认证完全无关（无关个人材料、临时文件、乱码等） | 直接忽略 |
| `analyze` | 其余情况 | 需再读全文精判 |

`policyReason` 一句话说明为什么是这个策略。

## 六、硬性约束

{{tag_constraint}}

1. 清单里**每个文件都必须输出一条**，顺序与 `index` 一致，禁止遗漏、禁止合并、禁止编造清单外的文件
2. `reason` 必须引用**实际看到的**文件名 / 标题 / 片段原文，禁止写「推测」「可能是」
3. `purpose` ≤ 80 字，按「是什么 + 主要用途」写；依据不足时写「依据不足，待全文精判」
4. 所有分数取值 0~1，保留两位小数
5. 只输出 JSON

## 七、本批次文件清单

{{file_list}}',
'分组提示词：批量吃文件清单，为每个文件产出 tags（多标签）+ suggestedPolicy + purpose。依据不足用 OTHER，策略按 skip/ignore/analyze 三档给出。',
   1, 1, NULL, NULL, NULL,
   'active', 0, 1, NOW())
ON DUPLICATE KEY UPDATE
  `PromptName` = VALUES(`PromptName`), `PromptType` = VALUES(`PromptType`),
  `StandardCode` = VALUES(`StandardCode`), `Template` = VALUES(`Template`),
  `Description` = VALUES(`Description`),
  `ModelName` = NULL, `MaxTokens` = NULL, `Temperature` = NULL,
  `IsValid` = 1, `IsDeleted` = 0, `UpdateTime` = NOW();

-- ── 2.3 作用提示词（doc_content）—— 单份吃 Markdown 全文 ─────────────────────
INSERT INTO `wf_prompt_template`
  (`Code`, `PromptCode`, `PromptName`, `PromptType`, `SkillTarget`, `StandardCode`,
   `Template`, `Description`, `Version`, `IsActive`,
   `ModelName`, `MaxTokens`, `Temperature`,
   `Status`, `IsDeleted`, `IsValid`, `CreateTime`)
VALUES
  (UUID(), 'doc_content_iso9001', 'ISO 9001 作用提示词（文档作用）', 'doc_content', NULL, @std9001,
'你是体系认证的审核专家。下方是一份企业资料转换成的 Markdown 全文。
请判断这份文件的标签、作用、要素、结构与处理策略，输出**单个 JSON 对象**。

## 一、可选标签（tagCode 只能取下面出现的编码，禁止自造）

{{tag_list}}

## 二、输出 JSON Schema（严格遵守，只输出 JSON，无解释、无 Markdown 围栏）

{{output_schema}}

示例（供理解字段，实际值必须基于本文件内容）：
{"tags":[{"tagCode":"InternalAudit","tagName":"内部审核","confidence":0.96,"reason":"标题含「内审」，正文含审核员与日程"},
         {"tagCode":"RecordForm","tagName":"记录表格","confidence":0.9,"reason":"编号前缀 QR-"}],
 "purpose":"【是什么】本文件是年度内部审核计划，规定审核范围、日程与分工。【审核关注点】审核员独立于被审部门、日程覆盖全部体系过程。【来源口径】质量管理部编制、管理者代表批准，每年初编制一次。【包含信息】年度、审核组长、审核范围、审核日期、受审部门。",
 "infoItems":[{"itemName":"年度","itemDesc":"所覆盖年度","valueType":"number","isKey":true},
              {"itemName":"审核组长","itemDesc":"本次内审负责人","valueType":"org","isKey":true},
              {"itemName":"审核日期","itemDesc":"计划起止日期","valueType":"date","isKey":false}],
 "typeGuess":"内审计划",
 "fields":[{"name":"文件编号","key":"wen_jian_bian_hao","value":"XASL-QR-014","dataType":"string","confidence":0.95,"isKeyField":true}],
 "tables":[{"index":0,"name":"审核安排","header":["日期","部门","审核员"],"rowCount":8,"columnCount":3,"confidence":0.9}],
 "suggestedPolicy":"analyze","policyReason":"需核对日程与实际排产是否冲突","confidence":0.94}

## 三、字段口径

1. **tags** —— 一文档多标签，至少一个；既给「形态」也给「业务过程」。
   `reason` 必须引用**实际看到的**内容（标题、编号、正文原句）。
2. **purpose** —— 四段式，100~400 字，段间空一格：
   - 【是什么】核心内容
   - 【审核关注点】审核时该看什么
   - 【来源口径】谁编、谁批、多久改一次（两侧同口径，供比对）
   - 【包含信息】这份文件能提供哪些信息要素
3. **infoItems** —— 从 `purpose` 的【包含信息】里抽出结构化要素；
   `valueType` 取 `string` / `number` / `date` / `org` / `list`；
   `isKey` = 是否为匹配/填写时的必填要素
4. **typeGuess** —— 一句话猜这份文件是什么（如「内审计划」「管理评审报告」）
5. **fields** —— 普通段落里的**标签:值**对（中文冒号或英文冒号均识别）；
   每项给 `name`（文档原标签）、`key`（小写拼音驼峰）、`value`、`dataType`、`confidence`、`isKeyField`
6. **tables** —— **只取 Markdown 表格**（以 `|` 开头的连续行块）；
   给 `header` / `rowCount` / `columnCount` / `confidence`。
   ⛔ 文档中没有真实 Markdown 表格时必须输出 `[]`，
   禁止根据文件名、目录名、标题或常识臆造表格；
   ⛔ 表格单元格内容**不得**再作为 `fields` 拆出来
7. **suggestedPolicy** —— 只能取 `analyze` / `skip` / `ignore`：
   内容完整且标签确定 → `skip`；与体系认证无关 → `ignore`；其余 → `analyze`
8. **confidence** —— 0~1，整体置信度，保留两位小数

## 四、硬性约束

{{tag_constraint}}

1. 一切判断基于**文档实际内容**，⛔ 禁止按文件名或标题臆测；
   内容与文件名不符时**以内容为准**，并在 `reason` 里注明
2. 文档内容为空、或全为扫描图片无文本时：
   `purpose` 给「无法识别」，`tagCode` 给 `OTHER`，`confidence` ≤ 0.5，
   `fields` / `tables` / `infoItems` 给 `[]`
3. `fields` 最多 100 条、`tables` 最多 50 个，超出截断
4. 只输出 JSON

## 五、单文件 Markdown 全文

{{document_content}}',
'作用提示词：吃单份 Markdown 全文，输出 §3.3 完整对象（tags / purpose 四段 / infoItems / fields / tables / 策略）。基于实际内容判定，禁止臆测。',
   1, 1, NULL, NULL, NULL,
   'active', 0, 1, NOW())
ON DUPLICATE KEY UPDATE
  `PromptName` = VALUES(`PromptName`), `PromptType` = VALUES(`PromptType`),
  `StandardCode` = VALUES(`StandardCode`), `Template` = VALUES(`Template`),
  `Description` = VALUES(`Description`),
  `ModelName` = NULL, `MaxTokens` = NULL, `Temperature` = NULL,
  `IsValid` = 1, `IsDeleted` = 0, `UpdateTime` = NOW();

-- ────────────────────────────────────────────────────────────────────────────
-- 三、数据字典（供 EntityConfig 的 DictCode 下拉）
-- ────────────────────────────────────────────────────────────────────────────
-- ⚠️ 2026-10-02 实测更正：`YzhForm` 的 DictCode 通路**已打通**（2026-09-26 框架改动）：
--    JSON 写 "DictCode": "xxx" → 前端自动 GET /api/System/Dictionary/items/by-no/xxx
--    → 返回 Value = **DicValue（业务值）** / Label = DicName。**无需页面注入 options**。
--    （易混点：`items/{code}` 那个端点返 Value = 字典项 Code(GUID)，**不是** YzhForm 用的那个）
-- ⚠️ 字典明细表关联键是 `Sys_DictionaryList.DicCode = Sys_Dictionary.Code`（不是 DicNo）
-- ────────────────────────────────────────────────────────────────────────────

INSERT INTO `Sys_Dictionary` (`Code`, `DicName`, `DicNo`, `ParentCode`, `OrderNo`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`, `Remark`)
VALUES
  ('PROMPT_TYPE',  '提示词类型', 'prompt_type',  '26b0f1d2ae6c11f1953796fd503fd974', 400, 1, 0, NOW(), 'seed_prompt_wb', 'wf_prompt_template.PromptType 下拉'),
  ('SKILL_TARGET', '适用技能',   'skill_target', '26b0f1d2ae6c11f1953796fd503fd974', 410, 1, 0, NOW(), 'seed_prompt_wb', 'wf_prompt_template.SkillTarget 下拉')
ON DUPLICATE KEY UPDATE
  `DicName` = VALUES(`DicName`), `ParentCode` = VALUES(`ParentCode`),
  `OrderNo` = VALUES(`OrderNo`), `IsValid` = 1, `IsDeleted` = 0;

INSERT INTO `Sys_DictionaryList` (`Code`, `DicCode`, `DicValue`, `DicName`, `OrderNo`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  -- 提示词类型（★ 前 3 个是工作台在用的；document_analysis 是旧的字段/表格提取路径）
  (CONCAT('PT_', 'doc_group'),        'PROMPT_TYPE', 'doc_group',        '文档分类（分组提示词）', 10, 1, 0, NOW(), 'seed_prompt_wb'),
  (CONCAT('PT_', 'doc_content'),      'PROMPT_TYPE', 'doc_content',      '文档作用（内容提示词）', 20, 1, 0, NOW(), 'seed_prompt_wb'),
  (CONCAT('PT_', 'prompt_generator'), 'PROMPT_TYPE', 'prompt_generator', '提示词生成器（元提示词）', 30, 1, 0, NOW(), 'seed_prompt_wb'),
  (CONCAT('PT_', 'document_analysis'),'PROMPT_TYPE', 'document_analysis','文档分析（字段/表格提取）', 40, 1, 0, NOW(), 'seed_prompt_wb'),
  -- 适用技能（值 = 代码中实际按 PromptCode 定位的那几条；all = 通用不限定）
  (CONCAT('ST_', 'all'),           'SKILL_TARGET', 'all',           '通用（不限定技能）',   10, 1, 0, NOW(), 'seed_prompt_wb'),
  (CONCAT('ST_', 'analyze_word'),  'SKILL_TARGET', 'analyze_word',  'Word 文档分析',        20, 1, 0, NOW(), 'seed_prompt_wb'),
  (CONCAT('ST_', 'analyze_excel'), 'SKILL_TARGET', 'analyze_excel', 'Excel 文档分析',       30, 1, 0, NOW(), 'seed_prompt_wb'),
  (CONCAT('ST_', 'analyze_pdf'),   'SKILL_TARGET', 'analyze_pdf',   'PDF 文档分析',         40, 1, 0, NOW(), 'seed_prompt_wb'),
  (CONCAT('ST_', 'nc_judge'),      'SKILL_TARGET', 'nc_judge',      'NC 判定',              50, 1, 0, NOW(), 'seed_prompt_wb'),
  (CONCAT('ST_', 'nc_conclude'),   'SKILL_TARGET', 'nc_conclude',   'NC 结论',              60, 1, 0, NOW(), 'seed_prompt_wb')
ON DUPLICATE KEY UPDATE
  `DicValue` = VALUES(`DicValue`), `DicName` = VALUES(`DicName`),
  `OrderNo` = VALUES(`OrderNo`), `IsValid` = 1, `IsDeleted` = 0;

-- ────────────────────────────────────────────────────────────────────────────
-- 四、验证
-- ────────────────────────────────────────────────────────────────────────────
-- 期望 3 行；ModelName/MaxTokens/Temperature 三个前置条件（统一 AI 配置）应为 NULL
SELECT `PromptCode`, `PromptName`, `PromptType`, `StandardCode`,
       `ModelName`, `MaxTokens`, `Temperature`, `Version`, `IsActive`, `IsValid`,
       CHAR_LENGTH(`Template`) AS TemplateLen,
       (`Template` LIKE '%{{tag_list}}%')         AS has_tag_list,
       (`Template` LIKE '%{{output_schema}}%')    AS has_output_schema,
       (`Template` LIKE '%{{tag_constraint}}%')   AS has_tag_constraint,
       (`Template` LIKE '%{{file_list}}%')        AS has_file_list,
       (`Template` LIKE '%{{document_content}}%') AS has_doc_content
FROM `wf_prompt_template`
WHERE `PromptType` IN ('prompt_generator', 'doc_group', 'doc_content')
ORDER BY `PromptType`, `PromptCode`;

-- 占位符归零检查：任一条业务提示词缺 tag_list / output_schema / tag_constraint 即为不达标
SELECT `PromptCode`,
       (`Template` NOT LIKE '%{{tag_list}}%')       AS miss_tag_list,
       (`Template` NOT LIKE '%{{output_schema}}%')  AS miss_output_schema,
       (`Template` NOT LIKE '%{{tag_constraint}}%') AS miss_tag_constraint
FROM `wf_prompt_template`
WHERE `PromptType` IN ('doc_group', 'doc_content') AND `IsDeleted` = 0;
-- 期望 miss_* 全为 0

SELECT d.`DicNo`, d.`DicName`, l.`DicValue`, l.`DicName` AS ItemName, l.`OrderNo`
FROM `Sys_Dictionary` d
JOIN `Sys_DictionaryList` l ON l.`DicCode` = d.`Code`
WHERE d.`DicNo` IN ('prompt_type', 'skill_target') AND d.`IsDeleted` = 0
ORDER BY d.`DicNo`, l.`OrderNo`;

-- 标签字典（提示词前置）：期望 27 行，含 OTHER
SELECT COUNT(*) AS tag_total, SUM(`TagCode` = 'OTHER') AS has_other
FROM `cert_tag_dict` WHERE `IsDeleted` = 0 AND `IsValid` = 1;
