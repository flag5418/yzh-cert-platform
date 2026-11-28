-- ============================================================================
-- 20261127_prompt_restore_missing_types_V1.sql
-- 提示词单一化（批 0B）· 补齐 cert_prompt_template 缺失的提示词类型
--
-- 背景（承 27 号执行计划 §0.3）：
--   wf_prompt_template 早前被 CodeFirst 重建清空（与 Sys_Menu 丢 10 条菜单同批事故），
--   业务提示词正文大量丢失，仅 audit_prep / audit_check 曾于 20261111 恢复。
--   本次「提示词单一化」把唯一数据源切到 cert_prompt_template 后，
--   下列类型在 cert 表中不存在 ⇒ 对应功能静默失败。
--
-- 本次补齐（正文逐字取自各历史种子脚本的**最新有效版本**）：
--   doc_group         ← 20261014_prompt_contract_fix_V1.sql（批次 items[] 契约版）
--   doc_content       ← 20261014_prompt_contract_fix_V1.sql（作用四段式版）
--   prompt_generator  ← 20261003_prompt_readable_V3.sql（元提示词）
--   nc_suggest        ← 20261030_audit_prompt_templates_V1.sql
--   onsite_plan       ← 20261103_onsite_plan_item_and_prompt_V1.sql
--
-- ⛔ 未补（有意）：
--   evidence_link —— 全仓无 .cs 消费方（PromptTypes.EvidenceLink 仅常量，无引用）
--   doc_essential —— 全仓无种子正文；代码 PickAnyScopeAsync 取不到时**优雅跳过**，不阻断
--
-- 口径（D4）：平台级 = OrgCode='' + ScopeKey=''（对全部标准生效）。
-- ⛔ 行级模型参数（ModelName/MaxTokens/Temperature）一律留 NULL —— 走 cert_sys_config 三键。
-- 幂等：INSERT ... WHERE NOT EXISTS（cert_prompt_template 无唯一索引，不能用 ON DUPLICATE KEY）。
-- ⛔ 铁律九：启用唯一字段 = IsValid。⛔ 无 Enable。
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────────────────────
-- 1) doc_group —— 文档分类（批次提示词）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `cert_prompt_template`
  (`PromptCode`, `PromptName`, `OrgCode`, `ScopeKey`, `Template`, `Placeholders`, `Remark`,
   `Sort`, `IsValid`, `Status`, `IsDeleted`, `Code`, `CreateTime`, `CreateBy`)
SELECT
  'doc_group', '体系认证·文档分类与标注（分类提示词）', '', '',
  '# 体系认证 · 文档分类与标注（分类提示词）

## 一、角色与任务

你是体系认证的文档分类专家。本批次给你一份企业资料文件清单，你要为清单里的每一个文件给出四项结论：标签集合、处理策略、一句话作用、置信度。

## 二、本标准上下文

⚠️ 本节由系统按「当前选中的标准」自动注入。换一个标准，下面三段内容就完全不同，所以先读完本节，再读文件清单。

**当前标准**

{{standard_name}}

**本标准要求的标准文档清单**（企业资料最终要填的就是这些）

{{std_doc_catalog}}

**本标准的资料目录结构**（企业文档可能落在哪一层）

{{folder_tree}}

⚠️ 判断一个文件「与本标准是否相关」的唯一依据，是它能不能对上上面那份标准文档清单。对不上不代表文件是垃圾，只代表它对本标准没有填写价值 —— 此时仍要为它给出正确的形态 / 业务标签，只把 suggestedPolicy 判为 ignore（无关）或保持 analyze（待全文精判），⛔ 不要因此改标签。

## 三、输入说明

输入是下方「八、本批次文件清单」给出的文件清单：共 N 个文件，每个文件给出三项 —— 文件名、文档标题、开头片段（约 300 字）。
(⚠️ 此处**故意不再写占位符** —— 渲染是全局替换，同一占位符写两次会把整份清单注入两次)

⛔ 你拿不到全文，所以不要试图判断文件的内部细节（有几张表、有哪些字段），也不要按扩展名编造内容。

## 四、判定规则

**R1 标签怎么选**
只能从「五、标签与编号规则」列出的编码里选，⛔ 禁止自造、⛔ 禁止同义改写。一个文件可以给多个标签：既给形态标签（记录表格 / 程序文件 / 制度文件 / 证书证照…），也给业务过程标签（内部审核 / 管理评审 / 采购…）。能确定时尽量给全。

**R2 判定优先级（冲突时按此裁决）**
编号前缀规则 > 文档标题关键词 > 文件名关键词 > 开头片段 > 兜底 OTHER。

**R3 一词多标签的裁决**
同一关键词命中多个标签时（例如「台账」可能同时指向记录表格、设备与计量、仓储与防护），取「识别特征」更具体者优先；仍无法区分时，可以同时给出多个标签，并在 reason 里写明依据不足。

**R4 处理策略（suggestedPolicy，三选一，必须给）**

| 值 | 判定条件 | 后果 |
|---|---|---|
| skip | 固定文档：营业执照 / 生产许可证 / 资质证书 / 法人身份证 / 体系认证证书这类证件，或该值已在企业全局参数中直接给定 | 匹配即终点，不再读全文 |
| ignore | 与体系认证完全无关：个人材料、临时文件、乱码、无关截图 | 丢弃 |
| analyze | 其余全部情况 | 需再读全文精判 |

⚠️ skip 与「省一次 AI 调用」无关，它是一个业务分流决定，判错会让资料永远不进入填写环节。
⛔ 编号前缀命中、标签置信度高，都**不构成** skip 的理由 —— 记录表、程序文件、制度文件绝大多数都应给 analyze，因为它们正是要填进标准文档的正文材料。

**R5 作用描述（purpose）**
≤ 80 字，按「是什么 + 主要用途 + 能填标准清单里的哪一份」写。第三点最重要：写不出对应哪一份，说明这个文件对本标准没有填写价值。依据不足时写「依据不足，待全文精判」。

**R6 置信度**
编号前缀或标题能直接判定 → 置信度 ≥ 0.90；无编号、片段信息不足 → 压低置信度，并在 reason 里写清依据不足。无论如何都要给出一个最接近的标签，彻底无法归类才给 OTHER。

## 五、标签与编号规则

**可选标签**（tagCode 只能取下面出现的编码）

{{tag_list}}

**编号前缀 → 标签**（L0 确定性规则，命中即定标签，优先于语义判断）

{{code_prefix_map}}

## 六、输出结构

严格 JSON，只输出 JSON：⛔ 不要解释文字、⛔ 不要 Markdown 围栏、⛔ 不要多余顶层字段。

外层固定为 items 数组，一个文件一条，顺序与清单一致：

{{output_schema}}

示例：

{"items":[{"index":1,"fileName":"XASL-QR-014 年度内审计划.doc","tags":[{"tagCode":"InternalAudit","tagName":"内部审核","confidence":0.96,"reason":"标题含「内审」"},{"tagCode":"RecordForm","tagName":"记录表格","confidence":0.9,"reason":"编号前缀 QR-"}],"suggestedPolicy":"analyze","policyReason":"可对应标准文档《年度内审计划》，需读全文判定填写价值","purpose":"年度内部审核的策划安排，明确审核范围、日程与分工。","confidence":0.95}]}

## 七、硬性约束

{{tag_constraint}}

1. 清单里每个文件都必须输出一条，⛔ 禁止遗漏、禁止合并、禁止编造清单外的文件；每个 item 必须带 fileName，且与清单里的文件名逐字一致。
2. reason 必须引用实际看到的文件名 / 标题 / 片段原文，⛔ 禁止写「推测」「可能是」。
3. 所有分数取值 0~1，保留两位小数。
4. 只输出 JSON。

## 八、本批次文件清单

{{file_list}}',
  '',
  '文档分类（批次）提示词：吃文件名清单，逐文件产出标签 / 处理策略 / 作用 / 置信度。标准无关，走 {{standard_name}} 注入。',
  10, 1, 'active', 0, 'PT_doc_group', NOW(), 'seed_prompt_restore_20261127'
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM cert_prompt_template
  WHERE PromptCode = 'doc_group' AND OrgCode = '' AND ScopeKey = '' AND IsDeleted = 0
);

-- ────────────────────────────────────────────────────────────────────────────
-- 2) doc_content —— 文档作用（单份提示词）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `cert_prompt_template`
  (`PromptCode`, `PromptName`, `OrgCode`, `ScopeKey`, `Template`, `Placeholders`, `Remark`,
   `Sort`, `IsValid`, `Status`, `IsDeleted`, `Code`, `CreateTime`, `CreateBy`)
SELECT
  'doc_content', '体系认证·文档作用分析（作用提示词）', '', '',
  '# 体系认证 · 文档作用分析（作用提示词）

## 一、角色与任务

你是体系认证的审核专家。下面是单份企业资料转换成的 Markdown 全文。你的任务不是复述文件，而是判断：这份资料能填本标准下的哪份标准文档、值不值得填、缺什么。输出**单个 JSON 对象**。

## 二、本标准上下文

⚠️ 本节由系统按「当前选中的标准」自动注入。换一个标准，下面三段内容就完全不同，所以先读完本节，再读文件全文。

**当前标准**

{{standard_name}}

**本标准要求的标准文档清单**（企业资料最终要填的就是这些）

{{std_doc_catalog}}

**本标准的资料目录结构**

{{folder_tree}}

## 三、输入说明

输入是下方「八、待分析的企业文件全文」，即单份企业资料（已从 doc / docx / xls / pdf 转换）的 Markdown 全文。Markdown 表格保留为以 | 开头的行块；扫描件若没有文本层，正文可能近乎为空。

## 四、判定规则

**R1 一切以正文为准**
⛔ 禁止按文件名或标题臆测。文件名与正文不符时以正文为准，并在 policyReason 里注明。

**R2 作用四段式（purpose，100~400 字，必须按这四段写，段首用【】标注）**
- 【是什么】这份文件是什么、覆盖什么范围
- 【审核关注点】本标准下审核员会关注什么（含建议对应的标准文档）
- 【来源口径】谁编制、谁批准、修订周期、版本控制方式
- 【包含信息】这份文件实际提供了哪些可填写要素（与 infoItems 对应）

**R3 与标准文档的对应关系（本提示词的核心价值）**
必须在【审核关注点】里显式说明这份文件能填标准清单里的哪一份（或哪几份）：

| 情况 | 怎么写 | suggestedPolicy |
|---|---|---|
| 能明确对应清单里某一份 | 写明文档名 + 为什么对得上 | analyze |
| 与清单某几份都沾边、但不确定填哪份 | 写出候选文档名 + 需要人工确认的点 | analyze |
| 属于固定文档（营业执照 / 许可证 / 资质证书 / 证件类） | 说明是靠什么确定的（文件名 / 证号 / 内容特征） | skip |
| 与体系认证完全无关 | 直接说无关 | ignore |

⚠️ skip 只用于固定文档（证件类，匹配即终点）。
⛔ 编号前缀命中、标签置信度高，都**不构成** skip 的理由 —— 记录表、程序文件、制度文件绝大多数都应给 analyze，因为它们正是要填进标准文档的正文材料。

**R4 结构化抽取（本提示词的核心产出，务必逐项落实）**
- fields —— 普通段落里的「标签:值」对（中文冒号或英文冒号均识别）；key 取标签的拼音下划线形式，value 为该标签对应的值
- tables —— **只取 Markdown 表格**（以 | 开头的连续行块）；header 为表头列名数组，rowCount / columnCount 为行列数
- infoItems —— 只列**这份文件里真实存在**的关键要素，⛔ 不要按常规模板臆造
- 三者都没有就给空数组，⛔ 不要硬凑

**R5 置信度**
依据不足时压低 confidence，并在 policyReason 里说明。

## 五、标签与编号规则

**可选标签**（tagCode 只能取下面出现的编码）

{{tag_list}}

**编号前缀 → 标签**（辅助判断用，不决定处理策略）

{{code_prefix_map}}

## 六、输出结构

严格 JSON，只输出 JSON：⛔ 不要解释文字、⛔ 不要 Markdown 围栏。

单对象，字段如下：

{{output_schema}}

示例（只用于理解字段含义，实际值必须基于本文件内容）：

{"tags":[{"tagCode":"InternalAudit","tagName":"内部审核","confidence":0.96,"reason":"标题含「内审」，正文含审核员与日程"},{"tagCode":"RecordForm","tagName":"记录表格","confidence":0.9,"reason":"编号前缀 QR-"}],"purpose":"【是什么】本文件是年度内部审核计划，规定审核范围、日程与分工。【审核关注点】审核员独立于被审部门、日程覆盖全部体系过程，可对应标准文档《年度内审计划》。【来源口径】质量管理部编制、管理者代表批准，每年初编制一次。【包含信息】年度、审核组长、审核范围、审核日期、受审部门。","infoItems":[{"itemName":"年度","itemDesc":"所覆盖年度","valueType":"number","isKey":true}],"typeGuess":"内审计划","fields":[{"name":"文件编号","key":"wen_jian_bian_hao","value":"XASL-QR-014","dataType":"string","confidence":0.95,"isKeyField":true}],"tables":[{"index":0,"name":"审核安排","header":["日期","部门","审核员"],"rowCount":8,"columnCount":3,"confidence":0.9}],"suggestedPolicy":"analyze","policyReason":"需核对日程与实际排产是否冲突","confidence":0.94}

## 七、硬性约束

{{tag_constraint}}

1. 只输出 JSON。
2. tags 至少一个，每个 tagCode 必须逐字取自「五、标签与编号规则」里的标签编码。
3. confidence 取值 0~1，保留两位小数。
4. fields 最多 100 条、tables 最多 50 个，超出截断。
5. 文档内容为空、或全为扫描图片无文本时：purpose 给「无法识别」，tagCode 给 OTHER，confidence ≤ 0.5，fields / tables / infoItems 给空数组。
6. ⛔ 不要输出上述字段以外的额外顶层字段。

## 八、待分析的企业文件全文

{{document_content}}',
  '',
  '文档作用（单份）提示词：读单份 Markdown 全文，产出作用四段式 + 抽取结果 + 处理策略。标准无关。',
  20, 1, 'active', 0, 'PT_doc_content', NOW(), 'seed_prompt_restore_20261127'
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM cert_prompt_template
  WHERE PromptCode = 'doc_content' AND OrgCode = '' AND ScopeKey = '' AND IsDeleted = 0
);

-- ────────────────────────────────────────────────────────────────────────────
-- 3) prompt_generator —— 元提示词（工作台 AI 生成/优化提示词草稿）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `cert_prompt_template`
  (`PromptCode`, `PromptName`, `OrgCode`, `ScopeKey`, `Template`, `Placeholders`, `Remark`,
   `Sort`, `IsValid`, `Status`, `IsDeleted`, `Code`, `CreateTime`, `CreateBy`)
SELECT
  'prompt_generator', '提示词生成器（元提示词）', '', '',
  '# 提示词生成器（元提示词）

你的唯一任务是：**写出一条给下游模型用的提示词正文**。
⛔ 你自己不执行任务，只产出「指令文本」。

## 一、本次任务

| 项 | 值 |
|---|---|
| 目标提示词类型 | {{prompt_type_name}} |
| 适用标准 | {{standard_name}} |
| 工作模式 | {{work_mode}} |
| 用户补充要求 | {{extra_requirement}} |

**工作模式说明**
- generate —— 从零写一条新的提示词正文
- optimize —— 优化「七」里给出的现有正文：**保留它的结构与业务口径，只做修补，⛔ 不要推倒重写**

## 二、先分清「你输出什么」与「下游模型输出什么」

| | 你（元提示词） | 下游模型 |
|---|---|---|
| 输出 | **Markdown 指令文本**（怎么判、判什么、输出什么结构） | 严格 JSON |
| 禁止 | ⛔ 只输出一份 JSON Schema、⛔ 把示例 JSON 当正文、⛔ 把「下游只输出 JSON」当成对你自己的要求 | —— |

⛔ 你绝对不能只丢一份 JSON Schema 就算完成。

## 三、输出正文的固定骨架（必须照此成文，节序不可乱）

| 序 | 小节 | 内容要点 |
|---|---|---|
| 一 | 角色与任务 | 一句「你是…」+ 这条提示词要完成什么 |
| 二 | 本标准上下文 | **按标准差异化的唯一位置**。必须放三个占位符：标准名、标准文档清单、资料目录结构；并写明「判断有没有用，唯一标准是对不对得上文档清单」 |
| 三 | 输入说明 | 下游会拿到什么：文件清单（分类）还是单份 Markdown 全文（作用） |
| 四 | 判定规则 | 用 **R1 / R2 / R3…** 编号；一条规则只写一次，⛔ 不要跨节重复 |
| 五 | 标签与编号规则 | 标签清单占位符 + 编号前缀占位符 |
| 六 | 输出结构 | 输出结构占位符 +「只输出 JSON，无解释、无围栏」+ 一个示例 JSON |
| 七 | 硬性约束 | 硬约束占位符 + 编号列表（禁止自造标签、禁止臆造字段、篇幅与取值范围） |
| 八 | 待处理数据 | 文件清单占位符（分类）或单文件全文占位符（作用），**必须放正文末尾** |

**写作风格（面向实施人员维护，必须遵守）**

1. 装饰符号**只允许两种**：⚠️ 表示风险提示，⛔ 表示禁止事项。
   ⛔ 不要使用 ★、★★、〇、◆ 这类装饰符号 —— 维护者要花精力去记符号含义。
2. 规则必须编号（R1、R2…），**一条规则只出现一次**；需要引用时写「见 R3」，⛔ 不要复制一遍。
3. 每个占位符**首次出现处**补一句人话说明，让维护者知道它运行时会被替换成什么。
4. 全文 1200~2500 字。超出就拆节，⛔ 不要堆成一大段。
5. 中文撰写，读起来要像一份**可直接投产的生产提示词**，不是提纲、不是需求说明、不是 JSON。

## 四、占位符书写铁律

你输出的正文里必须**原样出现**下列占位符，供系统运行时替换。
写法 = 两个左花括号 + 名称 + 两个右花括号，中间禁止空格。

| 名称 | 含义 | 哪类提示词需要 |
|---|---|---|
| tag_list | 该标准启用的标签清单，下游只能从中选 | 两类都要 |
| output_schema | 下游必须遵守的输出 JSON Schema | 两类都要 |
| tag_constraint | 「只能从字典选」的硬约束文案 | 两类都要 |
| standard_name | 当前标准的身份信息 | 两类都要 |
| std_doc_catalog | 本标准要求的标准文档清单 | 两类都要 |
| folder_tree | 本标准的资料目录结构 | 两类都要 |
| code_prefix_map | 编号前缀 → 标签 的 L0 规则 | 建议加 |
| file_list | 本批次文件清单 | 仅分类提示词 |
| document_content | 单份 Markdown 全文 | 仅作用提示词 |

⚠️ 本表里的名称**故意不写花括号**：写了的话，本元提示词自身在渲染时会被提前替换掉。
你输出时必须补上花括号。

⛔ 不要编造本表以外的占位符 —— 写了后端不会替换，会以字面量残留在最终提示词里。

## 五、输出契约（供你转述给下游模型，⛔ 不要原样粘贴）

{{output_schema}}

{{tag_constraint}}

## 六、标签与编号规则（写进正文「五」节的内容）

{{tag_list}}

{{code_prefix_map}}

## 七、用户现有的提示词正文（optimize 模式必读；为空则从零生成）

{{current_template}}

## 八、生成要求

- 只输出**提示词正文本身**：⛔ 无前言、⛔ 无说明、⛔ 无 Markdown 代码围栏
- ⛔ 不要输出 JSON Schema 本体、⛔ 不要输出「以下是提示词」之类的引导语',
  '',
  '元提示词：工作台「AI 生成 / 优化」提示词草稿时使用（唯一产出 = 提示词正文本身，不落库）。',
  30, 1, 'active', 0, 'PT_prompt_generator', NOW(), 'seed_prompt_restore_20261127'
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM cert_prompt_template
  WHERE PromptCode = 'prompt_generator' AND OrgCode = '' AND ScopeKey = '' AND IsDeleted = 0
);

-- ────────────────────────────────────────────────────────────────────────────
-- 4) nc_suggest —— NC 不符合项描述生成
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `cert_prompt_template`
  (`PromptCode`, `PromptName`, `OrgCode`, `ScopeKey`, `Template`, `Placeholders`, `Remark`,
   `Sort`, `IsValid`, `Status`, `IsDeleted`, `Code`, `CreateTime`, `CreateBy`)
SELECT
  'nc_suggest', 'NC 不符合项描述生成', '', '',
  '作为认证审核专家，请根据以下不符合项信息生成 NC 不符合项描述与处置建议。\n\n不符合项背景：{{NcContext}}\n\n条款原文：{{ClauseText}}\n\n检查发现：{{FindingDesc}}\n\n请输出 JSON（不要 markdown）：\n{"fact_desc":"不符合事实描述","requirement_ref":"违反条款原文引用","severity":"major|minor","corrective_req":"整改要求","due_days":30}',
  '',
  'NC 不符合项描述与处置建议生成（消费方：CertNcService）。',
  40, 1, 'active', 0, 'PT_nc_suggest', NOW(), 'seed_prompt_restore_20261127'
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM cert_prompt_template
  WHERE PromptCode = 'nc_suggest' AND OrgCode = '' AND ScopeKey = '' AND IsDeleted = 0
);

-- ────────────────────────────────────────────────────────────────────────────
-- 5) onsite_plan —— 入场检查内容生成
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `cert_prompt_template`
  (`PromptCode`, `PromptName`, `OrgCode`, `ScopeKey`, `Template`, `Placeholders`, `Remark`,
   `Sort`, `IsValid`, `Status`, `IsDeleted`, `Code`, `CreateTime`, `CreateBy`)
SELECT
  'onsite_plan', '入场检查内容生成', '', '',
  '你是资深 ISO 管理体系审核员。请针对下列检查项，生成「入场检查内容」清单（审核员到场后具体查什么、怎么查）。

【检查项条款信息】
{{ClauseInfo}}

【受检部门】
{{dept_names}}

输出严格 JSON（不要输出任何解释文字、不要用 Markdown 代码块）：
{
  "items": [
    {
      "check_what": "具体检查内容（一句话，可操作、可验证）",
      "check_method": "文件查阅|现场观察|人员访谈|抽样验证|记录核对（可组合，如：文件查阅+人员访谈）",
      "target_dept": "应检查的部门或岗位（没有则留空）",
      "sample_requirement": "抽样要求（如：抽 3 份近 6 个月记录；没有则留空）",
      "expected_finding": "期望看到的合规证据（没有则留空）",
      "clause_numbers": "对应条款号，多个用英文逗号分隔"
    }
  ]
}

要求：
1. 生成 3–8 条，每条只针对一个检查点，不要重复条款原文；
2. 检查方式必须落到具体手段（文件 / 现场 / 访谈 / 抽样）；
3. 只能基于给定条款信息推断，没有依据不要编造；
4. clause_numbers 必须来自检查项条款信息。
',
  '',
  '认证准备阶段为每个检查项生成入场检查内容（查什么 / 怎么查），供审核员编辑与入场使用（消费方：CertCheckEngineService）。',
  50, 1, 'active', 0, 'PT_onsite_plan', NOW(), 'seed_prompt_restore_20261127'
FROM DUAL
WHERE NOT EXISTS (
  SELECT 1 FROM cert_prompt_template
  WHERE PromptCode = 'onsite_plan' AND OrgCode = '' AND ScopeKey = '' AND IsDeleted = 0
);

-- ────────────────────────────────────────────────────────────────────────────
-- 验证（期望：5 行全部 present=1）
-- ────────────────────────────────────────────────────────────────────────────
SELECT
  t.PromptCode,
  t.PromptName,
  CHAR_LENGTH(t.Template) AS 正文字数,
  (SELECT COUNT(*) FROM cert_prompt_template x
    WHERE x.PromptCode = t.PromptCode AND x.IsValid = 1 AND x.IsDeleted = 0) AS 生效行数
FROM cert_prompt_template t
WHERE t.PromptCode IN ('doc_group', 'doc_content', 'prompt_generator', 'nc_suggest', 'onsite_plan')
  AND t.IsDeleted = 0
ORDER BY t.PromptCode;
