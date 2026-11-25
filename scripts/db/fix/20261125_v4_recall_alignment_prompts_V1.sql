-- =============================================================================
-- V4 · 提示词「体裁定位 + 召回对齐」与「空白表单/零事件正面证据」修正（幂等）
-- 依据：docs/80-v4/19-召回路径实施方案-V1.md §四、§五
-- 目标表：cert_prompt_template（平台级行 ScopeKey 为空或 ''）
--
--   改造 A（程序文件 / 作业指导书 / 管理制度 / 记录 四类体裁定位）
--     → 给 doc_extract_process_profile / work_instruction_citation / policy_profile /
--       record_profile 追加「体裁定位与召回对齐」块：明确四类体裁的识别线索与输出侧重，
--       并把输出字段与召回轴 RM1(条款)/RM2(过程)/RM3(部门)/RM4(编号) 对齐。
--
--   改造 B（空白表单认知修正：空白表单 = 企业已建立该记录载体 / 制度，属正面证据）
--     ① 定点改正 record_profile 的 ZeroEvent / HasInstance / RecordState 三条规则口径；
--     ② 追加「空白表单与零事件是正面证据」块（记录类专属）。
--
-- 幂等：追加块仅在缺失时执行（NOT LIKE 守卫）；定点 REPLACE 重复执行无副作用。
-- 回滚：CREATE TABLE cert_prompt_template_bak_20261125b AS SELECT * FROM cert_prompt_template;
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- -----------------------------------------------------------------------------
-- 改造 A：四类体裁「体裁定位与召回对齐」块（幂等追加）
-- -----------------------------------------------------------------------------
SET @pos_block = '\n\n## 体裁定位与召回对齐（V4 补充）\n\n本提示词服务「召回路径」：输出字段要能被 条款(RM1)/过程(RM2)/部门(RM3)/编号(RM4) 四条召回轴直接命中，缺字段就等于召回不到。\n\n### 先判「这是什么体裁」（据内容判，⛔ 不据文件名）\n- **程序文件**：规定“谁、在什么时机、按什么流程做什么”，含流程步骤 + 职责分工 + 引用的记录。→ 抽 ProcessArea、OwnerDept/职责矩阵、引用的记录清单。\n- **作业指导书**：规定“具体怎么操作”，含步骤/参数/操作要点，面向岗位。→ 抽适用岗位与部门、操作步骤、关键参数、引用的记录。\n- **管理制度**：企业自定的管理规定（考勤/设备/化学品/文件/安全…），不一定按标准条文结构写。→ 抽管辖范围、责任部门、强制要求、引用的记录/表单。\n- **记录/表单**：执行留下的痕迹（台账/检验单/培训记录/内审包…）。→ 抽 Records 的日期/型号/批次/实测值/签署人。\n\n### 召回对齐（必填/尽量填）\n1. `ProcessArea` 必填（八类之一）—— RM2 过程轴靠它把本件挂到对应过程条款。\n2. `OwnerDept` / `SupportDepts` 尽量写全 —— RM3 部门轴靠它按部门引用。\n3. `Metadata.DocCode` 原样抄文件编号（如 `QP-08-01`）—— RM4 编号轴靠它关联程序并上溯条款；没有给空串，⛔ 不许编。\n4. 引用的记录/表单「名称 + 编号」写进引用类字段（`EvidencePointers` 等）—— 供“制度→记录”链条核对。\n';

UPDATE cert_prompt_template
SET Template = CONCAT(Template, @pos_block)
WHERE PromptCode IN (
        'doc_extract_process_profile',
        'doc_extract_work_instruction_citation',
        'doc_extract_policy_profile',
        'doc_extract_record_profile'
      )
  AND (ScopeKey IS NULL OR ScopeKey = '')
  AND IsValid = 1
  AND IsDeleted = 0
  AND Template NOT LIKE '%体裁定位与召回对齐%';

-- -----------------------------------------------------------------------------
-- 改造 B①：record_profile 三条规则口径定点改正（空白模板 ≠ 无价值）
-- -----------------------------------------------------------------------------
UPDATE cert_prompt_template
SET Template = REPLACE(
      REPLACE(
        REPLACE(Template,
          -- ZeroEvent：零事件 = 正面证据（含“制式表单 + 本期为空”）
          '- `ZeroEvent`：原文明确写“无发生/无投诉/无事故/无退货/无不符合”等零事件时为 true；空白模板 false。',
          '- `ZeroEvent`：满足任一即为 true —— ① 原文明确写“无发生/无投诉/无事故/无退货/无不符合”；② 企业提供了**制式记录表单**（投诉/不合格/事故/退货…）但本期内容为空（本期未发生）。⛔ 只有“连该表单都未提供”时才 false；无投诉记录恰是符合的正面证据。'),
        -- RecordState：blank 是正面制度证据
          '- `RecordState`：只能是 `blank` / `partial` / `filled`；空白模板仍要输出 `FormFields`、`Metadata`、`Samples=[]`、`Rows=[]`、`RowCount=0`。',
          '- `RecordState`：只能是 `blank` / `partial` / `filled`；空白模板仍要输出 `FormFields`、`Metadata`、`Samples=[]`、`Rows=[]`、`RowCount=0`。★ `blank` 表示“该记录载体已建立、相关制度已存在”，是**正面的制度证据**，⛔ 不得当作“无采集/不可用”丢弃。'),
      -- HasInstance：澄清 false 不等于无效
        '- `HasInstance`：有实际数据行、日期、签名、数量、批次、结论等任一项为 true；空白模板 false。',
        '- `HasInstance`：有实际数据行、日期、签名、数量、批次、结论等任一项为 true；空白模板 false（但 false 只表示“本期无实例”，仍是正面制度证据，见下）。')
WHERE PromptCode = 'doc_extract_record_profile'
  AND (ScopeKey IS NULL OR ScopeKey = '')
  AND IsValid = 1
  AND IsDeleted = 0;

-- -----------------------------------------------------------------------------
-- 改造 B②：record_profile 追加「空白表单与零事件是正面证据」块（幂等追加）
-- -----------------------------------------------------------------------------
SET @blank_block = '\n\n## 空白表单与零事件是正面证据（V4 修正）\n\n- **空白表单不是没作用**：企业提供了某记录/表单（即使本期未填写），说明“该记录载体已建立、相关制度已存在”，是**正面的制度证据**。→ `RecordState=blank`、`HasInstance=false`，`ReportSummary` 写明“已建立《X》记录表单（本期无填写实例）”，`Warnings` 记 `blank_form`。⛔ 不许写“无采集/不可用”。\n- **零事件 = 符合的正面证据**：企业提供了**制式记录表单**（投诉/不合格/事故/退货…）且本期内容为空 ⇒ 视为“本期未发生”，`ZeroEvent=true`，`Coverage`/`ReportSummary` 说明“本期无投诉/无不合格发生”。例如“投诉记录”为空 ⇒ 正常，不是缺证据。\n- ⛔ 仅当“连该表单都未提供”时，才 `ZeroEvent=false` 并记 `blank_form`。\n';

UPDATE cert_prompt_template
SET Template = CONCAT(Template, @blank_block)
WHERE PromptCode = 'doc_extract_record_profile'
  AND (ScopeKey IS NULL OR ScopeKey = '')
  AND IsValid = 1
  AND IsDeleted = 0
  AND Template NOT LIKE '%空白表单与零事件是正面证据%';

-- -----------------------------------------------------------------------------
-- 校验（期望：4 条含「体裁定位」；record 含「零事件是正面证据」且不再含「空白模板 false」）
-- -----------------------------------------------------------------------------
SELECT PromptCode,
       (Template LIKE '%体裁定位与召回对齐%')      AS HasPosBlock,
       (Template LIKE '%零事件是正面证据%')         AS HasBlankBlock,
       (Template LIKE '%空白模板 false。%')         AS StillHasOldZeroEvent,
       CHAR_LENGTH(Template)                        AS Len
FROM cert_prompt_template
WHERE (ScopeKey IS NULL OR ScopeKey = '')
  AND PromptCode IN (
        'doc_extract_process_profile',
        'doc_extract_work_instruction_citation',
        'doc_extract_policy_profile',
        'doc_extract_record_profile'
      )
ORDER BY PromptCode;
