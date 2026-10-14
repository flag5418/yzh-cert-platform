-- ============================================================
-- 文档语义规则 · 提示词契约修复（2026-10-14）
--
-- 起因（用户：“该页面是文档规范化的底座，必须消灭可能的隐患”）：
--   线上 doc_group / doc_content 已偏离下游代码契约，存在三处高危缺陷：
--   ① doc_group 输出契约与下游不匹配（🔴 最严重、静默失败）
--      线上提示词示例 + 注入的 {{output_schema}} 都是【单对象】，
--      而下游 CertPlatform.Shared.Fill.SemanticHints.ParseGroupItems
--      **只读 root.items[]**，且每项必须带 fileName。
--      ⇒ 模型按提示词输出单对象 ⇒ ParseGroupItems 返回空 map
--      ⇒ 整批文件的分组标签 / 建议策略全部落空，且零报错。
--      本版提示词强制外层 items[] + index + fileName（后端 OutputSchema 已同步拆为
--      OutputSchemaGroupJson / OutputSchemaContentJson，按类型注入）。
--   ② 缺失 suggestedPolicy（skip / ignore / analyze）判定规则
--      它驱动后续分流（skip=固定文档终点 / ignore=丢弃 / analyze=进全文提取），
--      规则缺失 ⇒ 模型只能瞎猜，直接影响“要不要读全文”。
--   ③ 缺失 fields / tables / infoItems 的抽取规则
--      用户核心诉求是“提取核心数据”，而线上仅有一个示例、无任何抽取规则。
--
-- 同时修正的线上缺陷：
--   · doc_group「输入说明」写“含路径、大小等元信息”，实际注入是
--     「文件名 + 标题 + 开头 300 字片段」（BuildFileListJson）——误导模型浪费片段信号。
--   · 第二节“若文件不在标准文档清单中则归入 OTHER”把【标签】与【是否相关】混淆，
--     会让大量正常文件被打成 OTHER，摧毁分类本身。本版改为只影响 suggestedPolicy。
--   · doc_group“输出 ≤2000 字符”与批次多文件物理冲突（模型只能截断丢文件）——已删。
--   · R1–R27 硬编码关键词与 cert_tag_dict.MatchFeature 形成双源（字典改一条，提示词静默过期）；
--     本版改为引用注入的 {{tag_list}} / {{code_prefix_map}}，字典成为唯一事实源。
--   · 占位符补全 {{tag_constraint}}（此前线上未使用，约束靠手抄）。
--
-- 口径：标准无关 —— 不再硬编码 “ISO 13485”，一律走 {{standard_name}} /
--       {{std_doc_catalog}} / {{folder_tree}}，换标准自动差异化。
--
-- 幂等：可重复执行（按 PromptType + IsDeleted=0 覆盖正文）。
-- 前置：后端 PromptWorkbenchService 已按类型注入 OutputSchema（本仓同批改动）。
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────
-- ① 分类提示词（doc_group）
--    输入：{{file_list}}（本批次企业文件名 + 标题 + 开头片段）
--    输出：{ "items": [ … ] } 批次数组，每项带 index + fileName
-- ────────────────────────────────────────────────────────────
UPDATE `wf_prompt_template`
   SET `Template` = '# 体系认证 · 文档分类与标注（分类提示词）

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

{{file_list}}'
 WHERE `PromptType` = 'doc_group'
   AND `IsDeleted` = 0;

-- ────────────────────────────────────────────────────────────
-- ② 作用提示词（doc_content）
--    输入：{{document_content}}（单份企业文件全文）
--    输出：单个 JSON 对象
-- ────────────────────────────────────────────────────────────
UPDATE `wf_prompt_template`
   SET `Template` = '# 体系认证 · 文档作用分析（作用提示词）

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

{{document_content}}'
 WHERE `PromptType` = 'doc_content'
   AND `IsDeleted` = 0;

-- ────────────────────────────────────────────────────────────
-- 验证
-- ────────────────────────────────────────────────────────────
SELECT `PromptCode`,
       `PromptType`,
       CHAR_LENGTH(`Template`)                                                   AS 正文字数,
       LOCATE('{{standard_name}}', `Template`)   > 0                             AS 有标准名,
       LOCATE('{{std_doc_catalog}}', `Template`) > 0                             AS 有标准文档清单,
       LOCATE('{{folder_tree}}', `Template`)     > 0                             AS 有目录树,
       LOCATE('{{tag_list}}', `Template`)        > 0                             AS 有标签清单,
       LOCATE('{{output_schema}}', `Template`)   > 0                             AS 有输出契约,
       LOCATE('{{tag_constraint}}', `Template`)  > 0                             AS 有字典约束,
       -- ⚠️ 批批次契约的判据必须用 JSON 片段 '"items":[' 而非裸 'items' ——
       --    因为 'infoItems' 也含 'items' 子串，裸判会把 doc_content 误报为批次。
       LOCATE('"items":[', `Template`)          > 0                             AS 批次items契约,
       LOCATE('suggestedPolicy', `Template`)     > 0                             AS 有策略规则,
       LOCATE('infoItems', `Template`)           > 0                             AS 有抽取规则,
       -- ⚠️ 数据占位符必须**恰好出现 1 次**：PromptRenderer 是全局 Regex.Replace，
       --    写成两次会把整份文件清单/全文注入两次（白烧 token 且模型困惑）。
       GREATEST(
         (CHAR_LENGTH(`Template`) - CHAR_LENGTH(REPLACE(`Template`, '{{file_list}}', '')))        / CHAR_LENGTH('{{file_list}}'),
         (CHAR_LENGTH(`Template`) - CHAR_LENGTH(REPLACE(`Template`, '{{document_content}}', ''))) / CHAR_LENGTH('{{document_content}}')
       )                                                                         AS 数据占位符次数
  FROM `wf_prompt_template`
 WHERE `IsDeleted` = 0
 ORDER BY `PromptType`;
-- 期望：
--   doc_group    → 有标准名/标准文档清单/目录树/标签清单/输出契约/字典约束 = 1；
--                  批次items契约 = 1；有策略规则 = 1；有抽取规则 = 0（批次不抽字段，正确）；
--                  数据占位符次数 = 1（恰好一次）。
--   doc_content  → 同上 6 项 = 1；批次items契约 = 0（单对象，正确）；
--                  有策略规则 = 1；有抽取规则 = 1；数据占位符次数 = 1。
--   prompt_generator → 输出契约/字典约束/标签清单 = 1；标准文档清单/目录树 = 0（元提示词不需要）。

-- 残留占位符体检：正文里出现但**不在白名单**内的 {{xxx}} = 「写了不会替换」的死占位符。
-- （PromptRenderer 未命中时保留原文 ⇒ 模型会看到无意义的字面量）
SELECT `PromptCode`,
       REGEXP_SUBSTR(`Template`, '\\{\\{[a-zA-Z_]+\\}\\}')  AS 首个花括号片段,
       (CHAR_LENGTH(`Template`) - CHAR_LENGTH(REPLACE(`Template`, '{{', ''))) / 2 AS 花括号对数
  FROM `wf_prompt_template`
 WHERE `IsDeleted` = 0
 ORDER BY `PromptType`;
