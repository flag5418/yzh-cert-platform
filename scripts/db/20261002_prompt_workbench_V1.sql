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
--
-- ⛔ 铁律：
--   1. DB列名 = C#属性名 = TS字段名，PascalCase 逐字一致
--   2. 启用/禁用唯一字段 = IsValid（int，0/1）。⛔ 禁 Enable
--   3. StandardCode 存 cert_iso_standard.Code（GUID），⛔ 不是可读编码 iso9001-2015
-- ============================================================================

SET NAMES utf8mb4;

-- ────────────────────────────────────────────────────────────────────────────
-- 一、wf_prompt_template 补列（作用域 + 模型参数）
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
-- 二、提示词种子
-- ────────────────────────────────────────────────────────────────────────────
-- PromptType 约定：
--   prompt_generator = 元提示词（供「AI 自动生成」按钮调用，生成下面两类提示词）
--   doc_group        = ★ 标题/分类提示词（输入文件清单 → 输出每个文件的类别）
--   doc_content      = ★ 作用提示词（输入单文件 Markdown → 输出该文件的作用/摘要/条款）
-- ────────────────────────────────────────────────────────────────────────────

-- 定位 9001 标准 Code（GUID）；找不到则整段种子跳过（不报错）
SET @std9001 := (SELECT `Code` FROM `cert_iso_standard`
                 WHERE `StandardCode` = 'iso9001-2015' AND `IsDeleted` = 0 LIMIT 1);

-- ── 2.1 元提示词：生成「分类提示词」与「作用提示词」────────────────────────
INSERT INTO `wf_prompt_template`
  (`Code`, `PromptCode`, `PromptName`, `PromptType`, `SkillTarget`, `StandardCode`,
   `Template`, `Description`, `Version`, `IsActive`, `ModelName`, `MaxTokens`, `Temperature`,
   `Status`, `IsDeleted`, `IsValid`, `CreateTime`)
VALUES
  (UUID(), 'prompt_generator', '提示词生成器（元提示词）', 'prompt_generator', NULL, NULL,
   '你是体系认证信息化系统的提示词工程师。请根据用户给定的「提示词类型」和「适用标准」，生成一条可直接投入生产使用的提示词。

## 你要生成的提示词类型

### A. 分类提示词（prompt_type = doc_group）
用途：把一批企业资料按文档类别归组。
输入：文件清单（文件名 + 文档标题 + 开头片段）。
输出：每个文件 → 一个类别。
必须包含以下要素：
1. **类别体系**：给出固定枚举（手册 / 程序文件 / 作业指导书 / 记录表格 / 证书证照 / 合同协议 / 报告类 / 其他），每类给出识别特征
2. **判定优先级**：标题关键词 > 文件名编号规则 > 开头片段用途描述 > 兜底 other
3. **硬性约束**：每个文件必须输出类别；依据不足选 other，禁止臆测；只输出 JSON
4. **输出格式**：给出严格的 JSON 示例
5. **占位符**：正文末尾必须保留 `{{file_list}}` 作为文件清单注入点

### B. 作用提示词（prompt_type = doc_content）
用途：判断单份文件在体系认证中承担的作用。
输入：该文件的 Markdown 全文。
输出：类别 + 作用 + 摘要 + 可服务的标准条款 + 关键信息点。
必须包含以下要素：
1. **输出字段定义**：category（类别枚举）/ purpose（一句话作用）/ summary（摘要）/ clauses（可服务的标准条款号数组）/ keyPoints（关键信息点数组）
2. **判定原则**：必须基于文档实际内容，禁止根据文件名臆测；条款对应关系宁少勿滥
3. **硬性约束**：内容为空或无法识别时 purpose 填「无法识别」，其余输出空数组；只输出 JSON
4. **输出格式**：给出严格的 JSON 示例
5. **占位符**：正文末尾必须保留 `{{document_content}}` 作为正文注入点

## 生成要求
- 面向**真实生产**，不要写成教学示例；语气是给 AI 模型看的指令，不是给人看的说明
- 分类提示词要针对该标准实际会收到的资料类型；作用提示词要能对应到该标准的条款体系
- 只输出**提示词正文本身**，不要任何前言、说明、Markdown 代码围栏

## 本次生成任务
- 提示词类型：{{prompt_type}}
- 提示词类型中文名：{{prompt_type_name}}
- 适用标准：{{standard_name}}
- 额外要求：{{extra_requirement}}',
   '元提示词：按「提示词类型 + 适用标准」自动生成分类提示词或作用提示词草稿。由工作台「AI 生成」按钮调用，生成结果不落库，用户确认后保存。',
   1, 1, 'qwen-plus', 8192, 0.30,
   'active', 0, 1, NOW())
ON DUPLICATE KEY UPDATE
  `PromptName` = VALUES(`PromptName`), `PromptType` = VALUES(`PromptType`),
  `Template` = VALUES(`Template`), `Description` = VALUES(`Description`),
  `ModelName` = VALUES(`ModelName`), `MaxTokens` = VALUES(`MaxTokens`),
  `Temperature` = VALUES(`Temperature`), `IsValid` = 1, `IsDeleted` = 0,
  `UpdateTime` = NOW();

-- ── 2.2 分类（标题）提示词 —— ISO 9001 ─────────────────────────────────────
INSERT INTO `wf_prompt_template`
  (`Code`, `PromptCode`, `PromptName`, `PromptType`, `SkillTarget`, `StandardCode`,
   `Template`, `Description`, `Version`, `IsActive`, `ModelName`, `MaxTokens`, `Temperature`,
   `Status`, `IsDeleted`, `IsValid`, `CreateTime`)
VALUES
  (UUID(), 'doc_group_iso9001', 'ISO 9001 资料分类提示词', 'doc_group', NULL, @std9001,
   '你是 ISO 9001 质量管理体系认证的资料分类专家。下面是一批企业提交的资料文件清单（含文件名、文档标题、开头片段）。请判断每个文件属于哪一类。

## 分类体系（只能选以下之一，category 用英文码）

| category | 中文名 | 识别特征 |
|---|---|---|
| manual | 手册类 | 质量手册、管理手册、体系手册；通常含「质量方针」「组织机构」「体系范围」「过程识别」 |
| procedure | 程序文件 | 文件名或标题含「程序」「流程」「管理办法」；通常规定某项活动的职责与步骤 |
| instruction | 作业指导书 / 制度 | 含「作业指导书」「操作规程」「管理制度」「规范」「规定」；针对具体岗位或设备 |
| record | 记录表格 | 含「记录」「台账」「清单」「表格」「检查表」「签到表」「一览表」；以表格为主体 |
| certificate | 证书证照 | 营业执照、资质证书、许可证、认证证书、检验报告、检测报告、开户许可 |
| contract | 合同协议 | 含「合同」「协议」「订单」「承诺书」「授权书」 |
| report | 报告类 | 含「审核报告」「评价报告」「内审报告」「管理评审报告」「分析报告」「自查报告」 |
| other | 其他 | 无法归入以上任何一类 |

## 判定优先级（严格按顺序）
1. **文档标题关键词** —— 最可靠。标题含「程序」→ procedure；含「记录」/「表」→ record；含「手册」→ manual
2. **文件名编号规则** —— 体系文件常有编号前缀：QM/QP/QW/QR 分别对应手册/程序/作业文件/记录
3. **开头片段中的用途描述** —— 如「本程序规定了……」「本表用于记录……」
4. 以上都不足以判断 → **other**

## 硬性约束
- 清单中**每一个文件都必须输出一条结果**，禁止遗漏、禁止合并
- 依据不足时选 `other`，**禁止根据常识臆测**
- `reason` 必须引用**实际看到的依据**（标题/编号/片段中的原文），禁止写「推测」「可能是」
- 只输出 JSON，不要任何解释文字

## 输出格式（严格 JSON）
{"items":[{"index":1,"fileName":"QP-01 文件控制程序.doc","category":"procedure","categoryName":"程序文件","reason":"标题含「程序」且文件名以 QP- 开头"}]}

## 文件清单
{{file_list}}',
   '按标题/编号/开头片段将企业资料归入 8 个类别。输入文件清单，输出每个文件的类别。依据不足一律 other，禁止臆测。',
   1, 1, 'qwen-flash', 8192, 0.10,
   'active', 0, 1, NOW())
ON DUPLICATE KEY UPDATE
  `PromptName` = VALUES(`PromptName`), `PromptType` = VALUES(`PromptType`),
  `StandardCode` = VALUES(`StandardCode`), `Template` = VALUES(`Template`),
  `Description` = VALUES(`Description`), `ModelName` = VALUES(`ModelName`),
  `MaxTokens` = VALUES(`MaxTokens`), `Temperature` = VALUES(`Temperature`),
  `IsValid` = 1, `IsDeleted` = 0, `UpdateTime` = NOW();

-- ── 2.3 作用提示词 —— ISO 9001 ──────────────────────────────────────────────
INSERT INTO `wf_prompt_template`
  (`Code`, `PromptCode`, `PromptName`, `PromptType`, `SkillTarget`, `StandardCode`,
   `Template`, `Description`, `Version`, `IsActive`, `ModelName`, `MaxTokens`, `Temperature`,
   `Status`, `IsDeleted`, `IsValid`, `CreateTime`)
VALUES
  (UUID(), 'doc_content_iso9001', 'ISO 9001 文件作用提示词', 'doc_content', NULL, @std9001,
   '你是 ISO 9001:2015 质量管理体系认证的审核专家。下面是一份企业资料转换成的 Markdown 全文。请判断这份文件在质量管理体系中承担的作用。

## 输出字段

1. **category** —— 文件类别，只能取：manual / procedure / instruction / record / certificate / contract / report / other
2. **categoryName** —— 上面对应的中文名
3. **purpose** —— 该文件的作用，**一句话，不超过 60 字**。说明它在质量管理体系中承担什么职能（如「规定文件从编制到作废的全过程控制要求，确保现场使用的文件均为有效版本」）
4. **summary** —— 内容摘要，不超过 200 字，覆盖文件的主要章节或主要记录内容
5. **clauses** —— 该文件可服务的 ISO 9001:2015 条款号数组（如 ["4.4","7.5.2"]）。**只在文档内容确实对应某条款时输出**，宁少勿滥；无法判断输出 []
6. **keyPoints** —— 关键信息点数组，每项不超过 30 字，**最多 8 条**。如适用范围、责任部门、关键指标、生效日期、版本号

## 判定原则（必须遵守）
1. `purpose` 与 `summary` 必须基于文档**实际内容**，**禁止根据文件名或标题臆测**
2. 文档内容与文件名不符时（如名叫「质量手册」但正文是记录表），**以内容为准**，并在 `keyPoints` 中注明
3. `clauses` 依据文件内容与 ISO 9001:2015 条款的**实质对应关系**判断，不是关键词匹配
4. 文档内容为空、或全为扫描图片无文本时：`purpose` 填「无法识别」，`category` 填 other，其余输出空值或空数组
5. 只输出 JSON，不要任何解释文字

## 输出格式（严格 JSON）
{"category":"procedure","categoryName":"程序文件","purpose":"规定文件从编制到作废的全过程控制要求，确保现场使用的文件均为有效版本。","summary":"文件规定了文件的编制、审核、批准、发放、更改、作废流程，明确了各环节的责任部门与保存期限。","clauses":["7.5.2","7.5.3"],"keyPoints":["文件编号 QP-01","责任部门：综合管理部","保存期限：3 年","更改须经原审批人批准"]}

## 文档内容
{{document_content}}',
   '判断单份文件在 ISO 9001 体系中的作用：类别 + 作用 + 摘要 + 可服务条款 + 关键信息点。依据不足填「无法识别」，禁止臆测。',
   1, 1, 'qwen-plus', 8192, 0.20,
   'active', 0, 1, NOW())
ON DUPLICATE KEY UPDATE
  `PromptName` = VALUES(`PromptName`), `PromptType` = VALUES(`PromptType`),
  `StandardCode` = VALUES(`StandardCode`), `Template` = VALUES(`Template`),
  `Description` = VALUES(`Description`), `ModelName` = VALUES(`ModelName`),
  `MaxTokens` = VALUES(`MaxTokens`), `Temperature` = VALUES(`Temperature`),
  `IsValid` = 1, `IsDeleted` = 0, `UpdateTime` = NOW();

-- ────────────────────────────────────────────────────────────────────────────
-- 四、数据字典（供 EntityConfig 的 DictCode 下拉）
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
-- 五、验证
-- ────────────────────────────────────────────────────────────────────────────
SELECT `PromptCode`, `PromptName`, `PromptType`, `StandardCode`, `ModelName`,
       `MaxTokens`, `Temperature`, `Version`, `IsActive`, `IsValid`,
       CHAR_LENGTH(`Template`) AS TemplateLen
FROM `wf_prompt_template`
ORDER BY `PromptType`, `PromptCode`;

SELECT d.`DicNo`, d.`DicName`, l.`DicValue`, l.`DicName` AS ItemName, l.`OrderNo`
FROM `Sys_Dictionary` d
JOIN `Sys_DictionaryList` l ON l.`DicCode` = d.`Code`
WHERE d.`DicNo` IN ('prompt_type', 'skill_target') AND d.`IsDeleted` = 0
ORDER BY d.`DicNo`, l.`OrderNo`;
