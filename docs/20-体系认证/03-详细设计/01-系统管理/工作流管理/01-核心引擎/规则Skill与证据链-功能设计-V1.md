# 规则 Skill 与证据链 — 功能设计 V1

> **日期**：2026-09-27
> **版本**：V1 | **状态**：待评审
> **定位**：NC 检查规则 / 报告章节生成的**能力层设计**（Skill 体系 + 节点证据模型）
> **前置文档**：
> - `审核规则库与工作流设计器-功能设计-V4.md`（规则配置与 DAG 规范）
> - `工作流引擎-总体架构设计-V3.md`（三层任务模型、节点类型→执行器）
> - `AI节点-详细设计-V2.md`（ai_node 的 references 多输入机制）
> - `ISO体系认证NC与报告标准约束-V1.md`（NC 四要素、报告七要素）
> **验证依据**：`docs/90-归档/案例资料/CS河北雄安尚龙医疗科技有限公司13485体系材料`（真实企业资料 160+ 份）

---

## 0. 本文档要解决的两个问题

| # | 问题 | 来源 | 结论 |
|---|------|------|------|
| P-A | Skill 体系只有「值、逻辑」够不够？ | 真实文档走查发现：`≤2%`、空值、`2026.05.15-16` 无单位/区间/缺失，确定性算子直接崩溃 | **必须引入 AI 语义分析作为一等公民能力**，与值/逻辑并列 |
| P-B | 结果为什么不可信、不可执行？ | 现有输出只有 `result` 布尔值，没有「比的是什么、从哪份文档哪一段取的」 | **每个节点必须产出证据**：段落级引用 + 节点说明 + 可信度 |

两个问题本质是同一件事：**规则引擎要从「计算正确」升级为「结论可辩护」**。

---

## 1. 走查实证：为什么单靠值比较不成立

### 1.1 三组真实数据（同一份案例资料）

| 提取点 | 文件 | AI 可能提取出的值 | 确定性 `compare` 的结局 |
|---|---|---|---|
| 质量目标 | `1质量手册/附录二 质量方针和质量目标.doc` | `客户投诉率≤2%` | `double.TryParse` 失败 → 字符串分支 → `<=` 抛异常（`CompareSkill.cs:80`） |
| 实际值 | `1质量手册/目标分解考核统计报表.docx` | `0` / `100%` / `-`（表格空单元格） | `100%` 解析失败；`-` 解析失败 |
| 内审日期 | `QR-019 内部质量审核报告.doc` | `2026.05.15-16`（区间） | `DateTime.TryParse` 失败 → 抛异常 |
| 评审日期 | `QR-013 管理评审报告.doc` | `2026.05.25` | 可解析，但对端是区间 → 不成立 |
| 顾客满意度 | `目标分解考核统计报表.docx` | `96分` / `-` | 带单位或空 → 失败 |

**实测结论**：真实企业文档中，**能被 `CompareSkill` 直接消费的"干净值"不到一半**。

### 1.2 但同一组数据，人/AI 一眼能判

| 输入 | 人的判断 | 依据 |
|---|---|---|
| 目标 `客户投诉率≤2%` vs 实际 `0` | **达标**（0 ≤ 2%） | 理解 `≤` 是运算符、`%` 是单位、`0` 是同量纲实测值 |
| 目标缺失（空单元格） | **数据不足，待人工** | 知道"没填"不等于"不合规"，也不等于"合规" |
| 评审日 `2026.05.25` vs 审核日 `2026.05.15-16` | **9 天后，符合 12 个月内要求** | 理解区间取任一端均可 |
| 目标 `95分` vs 实际 `-` | **本期未测量，需补录** | 理解 `-` 是"无数据"占位符而非 0 |

> 这正是 P-A 的核心论据：**语义理解不是"锦上添花的 AI 功能"，而是真实数据进入确定性比较之前的必经环节。**

---

## 2. Skill 体系：四层能力模型

> 设计原则：**确定性优先，AI 兜底；AI 结果必须带可信度与理由。**

```
┌─────────────────────────────────────────────────────────┐
│ L4 裁决层   verdict / severity_rule / evidence_assemble  │ ← 出 NC 结论
├─────────────────────────────────────────────────────────┤
│ L3 语义层   ai_normalize / ai_judge / ai_correlate       │ ← 本文档新增核心
├─────────────────────────────────────────────────────────┤
│ L2 逻辑层   logic(AND/OR/NOT) / compare / count / range  │ ← 现有 compare 扩展
├─────────────────────────────────────────────────────────┤
│ L1 取数层   get_field / get_table / get_snippet          │ ← 现有 + 新增 get_snippet
└─────────────────────────────────────────────────────────┘
```

### 2.1 完整 Skill 清单

| 层 | SkillCode | 名称 | 输入 | 输出 | 是否调 AI | 状态 |
|---|---|---|---|---|---|---|
| L1 | `get_field` | 取字段值 | fieldCode, enterpriseCode | value, confidence, **evidence** | 否 | ✅ 已有，**需补 evidence** |
| L1 | `get_table` | 取表格 | tableCode, enterpriseCode | rows, rowCount, **evidence** | 否 | ✅ 已有，**需补 evidence** |
| L1 | **`get_snippet`** | **取原文段落** | fieldCode / 定位锚点 | **paragraphText, position, fileName** | 否 | ❌ **新增**（P-B 关键） |
| L2 | `compare` | 值比较 | a, b, operator | result, diff | 否 | ✅ 已有，需扩 `contains/in/between` |
| L2 | **`logic`** | 逻辑组合 | N×boolean | result | 否 | ❌ **设计有、实现缺**（`NodeExecutor.cs:95` 无 case） |
| L2 | **`count` / `range`** | 计数/区间 | list 或 value | result | 否 | ❌ 新增 |
| L2 | **`set_ops`** | 集合运算 | listA, listB, op | result, diffList | 否 | ❌ 新增（部门覆盖类规则） |
| L3 | **`ai_normalize`** | **语义归一化** | rawValue, targetDataType, hint | **value, normalizedFrom, confidence, reason** | **是** | ❌ **新增（P-A 关键）** |
| L3 | **`ai_judge`** | **语义判定** | N×(value+evidence), ruleStatement | **verdict, confidence, reason** | **是** | ❌ **新增（P-A 关键）** |
| L3 | **`ai_correlate`** | **跨文档关联** | N×(value+evidence), correlationStatement | **verdict, confidence, reason, links** | **是** | ❌ 新增 |
| L4 | **`verdict`** | **三态裁决** | pass/fail/unknown + confidence | **isViolated, severity, evidenceBundle** | 否 | ❌ **新增（P0 缺口）** |
| L4 | **`evidence_assemble`** | 证据组装 | N×evidence | evidenceBundle, reportText | 否 | ❌ 新增（报告侧） |
| L4 | `assemble` | 文本拼接 | prefix, suffix, joiner | assembled_text | 否 | ✅ 已有 |
| L3 | `llm_extract` | LLM 抽取 | prompt | json | 是 | ✅ 已有（保留，定位为"结构化抽取"） |

**现有 5 个 → 目标 16 个**，其中新增 11 个，改造 3 个。

---

## 3. P-A：AI 语义层三个 Skill 的设计

### 3.1 为什么拆成三个而不是一个

| Skill | 解决什么 | 输入规模 | 输出性质 | 典型场景 |
|---|---|---|---|---|
| `ai_normalize` | **单值清洗**：`≤2%`→`2`、`-`→null、`2026.05.15-16`→`2026-05-15` | 1 个值 | 规范值（可继续走确定性比较） | 取数后、比较前 |
| `ai_judge` | **单文档多值判定**：一段话里多个信息 → 一个结论 | 2~5 个值 | verdict + confidence + reason | 管理评审输入是否齐全 |
| `ai_correlate` | **跨文档语义关联**：不同文档的记录是否指向同一件事 | 5~10 个值/段落 | verdict + 关联链 | 内审 NC ↔ 管理评审是否体现 |

> 拆分理由：`ai_normalize` 输出**仍可被确定性算子消费**（它是"预备"），后两者输出**直接是结论**（它们是"判定"）。混为一个会导致可信度无法分级。

### 3.2 `ai_normalize` — 语义归一化

```jsonc
{
  "nodeId": "norm_n3",
  "nodeType": "skill",
  "skillCode": "ai_normalize",
  "title": "归一化-质量目标值",
  "claim": "把质量手册中「客户投诉率≤2%」解析为可比较的数值阈值",
  "config": {
    "targetDataType": "number",     // number | date | date_range | boolean | enum
    "hint": "百分比目标，取不等号右侧数值，单位 %",
    "unit": "%"
  },
  "inputs": { "rawValue": "n1.fieldValue" },
  "outputs": { "value": "number", "confidence": "number", "reason": "string", "isMissing": "boolean" }
}
```

**输出契约（关键）**：

| 端口 | 类型 | 说明 |
|---|---|---|
| `value` | number/date/... | 规范值；`isMissing=true` 时为 null |
| `confidence` | number 0~1 | 归一置信度 |
| `reason` | string | AI 的解析说明（进证据链） |
| **`isMissing`** | **boolean** | **是否为"无数据"** —— 这是三态裁决的输入，`-`/空 ≠ 0 |

**执行策略**：**先确定性、后 AI**
```
① 裸值已是目标类型 → 直接透传，confidence=1.0，不调 AI
② 确定性清洗规则可处理（去 % 、去"≤>=<>"、日期重排）→ 走内置规则，confidence=0.99
③ 仍失败 → 调 LLM，confidence 由 LLM 自报 × 0.9 折减
④ LLM 返回 isMissing=true → 置 isMissing，value=null，**不报错**
```
> 这条策略保证：**只有真正脏的数据才花 token**，且缺失数据不再让整条路径崩溃。

### 3.3 `ai_judge` — 多输入语义判定（你设计的那个 AI 节点）

**复用已有机制**：`AI节点-详细设计-V2.md` §4.3 的 `references[]` 已支持多节点引用，`AiNodeExecutor.ResolveTemplateRefs`（L586）已能从 `sharedOutputs` 自动抽取 `{{别名.端口}}` 填参数池。**V1 需要做的是给它加三样东西：证据注入、结构化输出、置信度出口。**

```jsonc
{
  "nodeId": "ai_n7",
  "nodeType": "ai_node",
  "title": "AI 语义判定-管理评审输入完整性",
  "claim": "判断管理评审报告的评审内容是否覆盖 ISO13485 §5.6.2 要求的全部输入项，并是否体现了 2026-05 内审结果",
  "config": {
    "promptTemplate": "...\n\n【证据】\n{{评审内容.evidenceText}}\n\n【要求】\n{{标准条款.evidenceText}}",
    "outputType": "json",
    "requireEvidence": true          // ← 新增：强制注入证据块
  },
  "references": [
    { "nodeId": "tbl_n3", "alias": "评审内容", "port": "result" },
    { "nodeId": "fld_n4", "alias": "标准条款", "port": "result" },
    { "nodeId": "fld_n5", "alias": "内审日期", "port": "result" }
  ],
  "outputSchema": {                   // ← 新增：强制结构化
    "verdict": "pass|fail|unknown",
    "confidence": "number",
    "reason": "string",
    "missingItems": "string[]",
    "citedQuotes": "string[]"
  }
}
```

**输出契约**：

| 端口 | 类型 | 说明 |
|---|---|---|
| `verdict` | enum `pass/fail/unknown` | **三态**，`unknown` = AI 明确表示信息不足 |
| `confidence` | number 0~1 | AI 自报置信度，引擎做**下限保护**（见 §5.3） |
| `reason` | string | 判定理由（进证据链与报告） |
| `missingItems` | string[] | 缺什么（fail 时填，直接生成 NC 描述） |
| `citedQuotes` | string[] | **AI 引用的原文片段**（进证据链） |

**Prompt 三段式契约**（沿用 `AI提示词规则-功能设计-V1.md`）：
```
【角色】你是 ISO 13485 医疗器械质量管理体系审核员
【证据】…  ← 引擎自动注入：每条 = 值 + 出处文件 + 原文段落 + 提取置信度
【任务】…  ← 专家在设计器里写的自然语言规则
【输出】严格返回 JSON：{verdict, confidence, reason, missingItems, citedQuotes}
```

> **证据注入由引擎完成，专家只写【任务】** —— 这是让"未来专家只维护规则、不碰工程"的关键设计。

### 3.4 `ai_correlate` — 跨文档关联

针对走查中的 P5（跨文档无关联键）。不需要建 join，改为**让 AI 做语义对齐**：

```jsonc
{
  "skillCode": "ai_correlate",
  "claim": "确认内审发现的 1 项不符合（7.5.8 标识）在管理评审报告中被作为评审输入提及，且整改已完成",
  "config": {
    "correlationStatement": "两段记录是否指向同一不符合项，且后者提及前者已闭环",
    "matchKeys": ["条款号", "日期", "部门", "不符合描述"]
  },
  "inputs": {
    "recordSetA": "n1.rows",     // 内审不合格报告表
    "recordSetB": "n2.fieldValue" // 管理评审报告正文
  }
}
```
输出额外带 `links[]`：A 中第几条 ↔ B 中第几段，供报告"审核发现"章节逐条引用。

---

## 4. P-B：证据链设计

### 4.1 现状：三个"设计了但没接上"的地方

| # | 位置 | 现状 | 后果 |
|---|---|---|---|
| E1 | `ExtractionResult.PositionInfo` / `TableExtractionResult.PositionInfo` | **全仓 2 处引用，均为实体声明，无任何写入** | 提取结果没有位置，无法回指原文 |
| E2 | `AI节点-详细设计-V2.md` §2.1 规定 `doc_field` 有 `sourceText` 端口 | **后端 `NodeExecutor.ExecuteDocFieldAsync` 只输出 `fieldValue/confidence/source`，无 sourceText** | 节点拿不到原文段落 |
| E3 | `ent_file_compliance_check.Detail json`（注释："含具体位置、偏离描述"）、`audit_nonconformity.EvidenceRef text`、`rpt_report_section_source.SourceDescription/Confidence` | **三张下游表都建好了，无任何写入方** | NC 与报告的证据字段永远是空的 |

> 结论：**证据链的"骨架"（三张表 + 一个字段）在 DDL 里已经存在，缺的是上游供给。**

### 4.2 证据粒度：段落级，不是单元格级

你说的这一点是本设计的核心。定义证据对象：

```jsonc
Evidence {
  "evidenceId": "ev_01H...",
  "level": "paragraph",              // paragraph | table_row | section
  "fileCode": "XASL-QR-013",
  "fileName": "管理评审报告.doc",
  "filePath": "4记录文件/质量类/XASL-QR-013 管理评审报告.doc",
  "versionNumber": 1,
  "position": {                      // 位置锚点
    "page": 1,
    "section": "评审内容及相关资料 / 1）2026.05.15-16内审",
    "tableIndex": null,
    "rowIndex": null,
    "charStart": 312, "charEnd": 487
  },
  "quote": "根据年度内审计划要求，对我公司建立的质量管理体系运行情况进行内部审核。本次审核共发现1项不符合项。已完成全部整改。",   // ← 原文段落，非单元格值
  "extractedValue": "2026.05.15-16", // 从该段抽出的值（可为 null）
  "extractConfidence": 0.93,
  "retrievedAt": "2026-09-27T10:31:00Z"
}
```

**关键决定**：`quote` 存**整段原文**，`extractedValue` 存抽出的值，两者并存。
- 报告要"说明" → 用 `quote`
- 比较要"数值" → 用 `extractedValue`
- 争议要"追溯" → 用 `position` + `quote` 回到原文

### 4.3 供给端：提取阶段就落证据

`DocExtractionRuleService` 的 AI 分析 prompt 需要增加输出要求：

```diff
  ## 需要提取的字段
  | 序号 | 字段名称(中文) | 英文名称(field_code) | 数据类型 | 描述 |
+ ## 需要返回的证据
+ 每个字段额外返回 sourceQuote（该字段所在的完整段落原文）、
+ sourcePosition（页码/章节路径/表格行列）、sourceFileName
```

落库改动：`cert_extraction_result` 增列（或复用 `PositionInfo` 列存 JSON）：

| 列 | 类型 | 说明 |
|---|---|---|
| `PositionInfo` | json | **激活**，存 §4.2 的 `position`（现为空列） |
| `SourceQuote` | text | **新增**，段落原文 |
| `SourceFileName` / `SourceFilePath` | varchar | **新增**，便于不 join 文件表也能出证据 |
| `SourceVersionNumber` | int | **新增**，版本可追溯 |

### 4.4 节点级：每个节点的三要素

> 你的原话："每一个节点，应该有一段描述，就哪怕最简单的值比较，我们也应该有一个说明，该比较的是什么内容，我们用的什么文档，什么内容进行比较。"

节点配置期新增**必填/可自动生成**的 `claim` 字段：

```jsonc
{
  "nodeId": "cmp_n5",
  "nodeType": "skill",
  "skillCode": "compare",
  "title": "评审时效比较",
  "claim": "比较管理评审日期与内部审核结束日期，要求评审不早于审核且间隔 ≤ 12 个月",   // ← 人读的"这个节点在干嘛"
  "config": { "operator": "<=" },
  ...
}
```

`claim` 的三个用途：
1. **设计器展示**：专家打开 DAG 即知每个节点语义，不必读 prompt/config
2. **自动渲染成证据**：执行后，`claim` + 输入 Evidence + 输出值 = 该节点的证据条目
3. **报告/NC 描述生成**：`evidence_assemble` 直接引用 `claim` 拼装"审核发现"段落

**`claim` 生成策略**（降低专家负担）：
- 专家手写（推荐，最准确）
- 或由设计器按 skillCode + config **模板自动生成**：`比较 {{输入A.label}} 与 {{输入B.label}}，运算符 {{op}}，阈值 {{threshold}}`

### 4.5 执行期：证据随输出一起落

**每个节点的输出统一扩为五元组**（现有只有 `result`）：

```jsonc
{
  "result": false,                      // 原有
  "confidence": 0.94,                   // 原有(docField 有，skill/ai 部分有)
  "claim": "比较...",                    // ← 新增：节点说明
  "evidence": [ Evidence, ... ],         // ← 新增：本节点产生的/引用的证据
  "reason": "管理评审日期 2026-05-25 距审核日 2026-05-16 共 9 天，≤365 天"  // ← 新增
}
```

**证据传递规则**：
| 节点类型 | evidence 来源 |
|---|---|
| `get_field` / `get_table` / `get_snippet` | 从 `cert_extraction_result.SourceQuote` 直接取出（**生产者**） |
| `normalize` / `ai_normalize` | 引用上游 evidence + 追加自己的 `reason` |
| `compare` / `logic` / `set_ops` | **引用全部输入的 evidence**（不产生新证据，做聚合） |
| `ai_judge` / `ai_correlate` | 引用输入 evidence + 追加 `citedQuotes`（**AI 明确引用的段落**） |
| `verdict` | 聚合为 `evidenceBundle`，作为 NC / 报告章节的完整证据包 |

> **不产生新证据的节点也要有输出** —— `claim` + `reason` 让"比较这个动作"本身可读、可审。

### 4.6 落库：三处下游

| 下游表 | 字段 | 写入方 | 用途 |
|---|---|---|---|
| `wf_node_execution.output_json` | 已有 json | 引擎 | 节点级证据留痕、回放 |
| `ent_file_compliance_check` | `CheckStatus`(pass/fail/warning/**blocked**), `Message`, `Detail` | `verdict` Skill | **规则级结论 + 完整证据包**；`blocked` 对应三态的 unknown |
| `audit_nonconformity` | `Description`(=claim+reason), **`EvidenceRef`**(=evidenceBundle), `RequirementRef`, `Severity` | 违规时从 check 记录转写 | NC 四要素齐全（ISO 17021-1 §9.4.4.2） |
| `rpt_report_section_source` | `SourceType`=`extraction/nc/compliance`, `SourceCode`, `SourceDescription`(=claim+quote), `Confidence` | 报告章节生成 | 报告可追溯，满足 §9.4.6 |

### 4.7 可信度传递规则

```
证据可信度 = 提取置信度 (extractConfidence)
节点可信度 = f(节点类型):
    确定性节点(min/avg)  → min(输入节点可信度)          // 木桶原理
    ai_normalize         → 自报 × 0.9
    ai_judge/ai_correlate→ 自报 × 输入节点可信度均值      // AI 不能凭空提高
规则可信度 = min(参与判定的全部节点可信度)
```

**三级处置**（兑现三态裁决）：

| 规则可信度 | CheckStatus | 动作 |
|---|---|---|
| ≥ 0.85 | `pass` / `fail` | 自动通过 / 自动开 NC |
| 0.60 ~ 0.85 | `warning` | 结论**仅供审核员参考**，必须人工确认 |
| < 0.60 或 `isMissing`/`unknown` | `blocked` | **不下结论**，转人工，附证据包 |

---

## 5. P0 补齐：三态裁决出口（`verdict` Skill）

> 沿用上一轮分析的 P0 结论，本文档给出具体契约。

```jsonc
{
  "nodeId": "v_n9",
  "nodeType": "skill",
  "skillCode": "verdict",
  "title": "裁决-管理评审时效",
  "claim": "汇总比较结果与可信度，产出是否违规及严重度",
  "config": {
    "severityRules": [
      { "when": "isMissing == true",        "severity": "minor",  "message": "管理评审记录缺失或日期未填写" },
      { "when": "result == false",          "severity": "major",  "message": "管理评审未在审核后12个月内进行" },
      { "when": "confidence < 0.6",         "verdict":  "unknown" }
    ]
  },
  "inputs": { "result": "n5.result", "confidence": "n5.confidence", "isMissing": "n3.isMissing" },
  "outputs": { "isViolated": "boolean", "verdict": "string", "severity": "string", "message": "string", "evidenceBundle": "json" }
}
```

**输出即 `ent_file_compliance_check` 的一行**：
```
CheckStatus = verdict(pass→pass / fail→fail / unknown→blocked)
Message      = message
Detail       = { claim, reason, evidence[], confidence, severity }
```

**引擎侧唯一改动**：`WorkflowInterpreter.AggregateNcResult`（`WorkflowInterpreter.cs:228`）从
> 「有路径 completed → success=true」

改为
> 「读取 `verdict` 节点输出 → success 表示**执行成功**，新增 `verdict/isViolated/severity` 表示**业务结论**，两者分离」

同时激活 `WorkflowConfig.OutputConfig`（`WorkflowConfig.cs:27`，现为死配置）：约定 `outputConfig.verdict = "n9.verdict"`。

---

## 6. 两条真实规则的完整形态（验证设计）

### 6.1 规则 A：质量目标达成（简单 → 仍需语义层）

**原文**：`附录二`「客户投诉率≤2%」 vs `目标分解考核统计报表` 结果行「0」

```
n1 get_field(目标值)                        → "客户投诉率≤2%"  evidence: 段落quote
n2 get_field(实际值)                        → "0"              evidence: 表格行quote
n3 ai_normalize(n1, targetDataType=number)  → 2, conf=0.97, reason="≤2% 取阈值 2"
n4 ai_normalize(n2, targetDataType=number)  → 0, conf=0.99, isMissing=false
n5 compare(n4.value, n3.value, "<=")        → true, conf=0.97
n6 verdict(n5.result, n5.confidence)        → {verdict:"pass", severity:null}
```
**证据包**：`claim="比较投诉率实际值0与目标阈值2"` + 两段 quote + `reason` + conf 0.97

**对照**：走查中 P1（`≤2%` 崩溃）→ 由 `n3` 解决；证据缺失 → 由 `get_field` 的 `SourceQuote` 解决。

### 6.2 规则 B：内审 NC 闭环 + 管理评审体现（复杂）

```
n1  get_table(年度内审计划)                       → 5 行部门      evidence
n2  get_field(内审报告-审核日期)                   → "2026.05.15-16"
n3  get_field(不合格报告-实际完成日)               → "2026.05.17"
n4  get_field(管理评审报告-评审日期)               → "2026.05.25"
n5  get_field(管理评审报告-评审内容)               → 长文本        evidence: 段落
n6  ai_normalize(n2, date_range) → 2026-05-16, conf=0.95
n7  ai_normalize(n4, date)       → 2026-05-25, conf=0.99
n8  compare(n7, n6, "<=")        → true
n9  compare(n3, n7, "<=")        → true   // 整改在评审前完成
n10 logic AND(n8, n9)            → true
n11 ai_correlate(n5, n3记录, statement="评审内容是否提及该NC且已闭环")
                                   → verdict=pass, conf=0.88, links[], citedQuotes[]
n12 logic AND(n10, n11)          → true
n13 verdict(n12, min(conf))      → {verdict:"pass", confidence:0.86}
```

**若 `n5` 提取到空** → `ai_normalize` 返回 `isMissing=true` → `n11` 返回 `unknown` → `n13` 输出 `blocked` → **转人工，附完整证据包**，而不是报"程序错误"。
> 这正是 P8（缺失≠违规≠故障）的解法。

---

## 7. Skill 能力演进阶梯（给专家的"可配范围"说明）

| 阶梯 | 能力 | 专家能配什么 | 依赖 |
|---|---|---|---|
| **S0** | 取值 + 比较 | 干净数值/日期的二元比较 | 已有 |
| **S1** | + 归一化 | 真实文档（带单位/符号/区间/缺失） | `ai_normalize` |
| **S2** | + 逻辑组合 | 多条件 AND/OR/NOT | `logic` 节点补齐 |
| **S3** | + 记录集 | 逐条判定（100 条 NC 每条闭环） | `fetch_records` / `count` / `any` |
| **S4** | + 语义判定 | 段落级合规、跨文档一致性 | `ai_judge` / `ai_correlate` |
| **S5** | + 证据与裁决 | 自动开 NC、自动写报告、可追溯 | `verdict` / `evidence_assemble` / 三态 |

**每上一级，专家能覆盖的 ISO 规则比例大致为**：S0≈15%、S1≈35%、S2≈50%、S3≈70%、S4≈90%、S5≈100%（可执行交付）。

---

## 8. 实施优先级

| 优先级 | 事项 | 理由 | 涉及 |
|---|---|---|---|
| **P0-1** | `verdict` Skill + `AggregateNcResult` 三态分离 | 没有它，所有规则只报"执行成功"，NC 检测不成立 | 引擎 + 新 Skill |
| **P0-2** | `PositionInfo` 激活 + `SourceQuote` 落库 | 证据链源头，**必须在提取阶段做，事后无法补** | 提取服务 + DDL |
| **P0-3** | `ai_normalize` | 没有它，S1 以下规则在真实文档上大面积崩溃 | 新 Skill |
| **P1-1** | 节点 `claim` 字段 + 输出五元组 | 证据可读性的基础 | 设计器 + 引擎 |
| **P1-2** | `ai_judge` + 证据注入 prompt | 语义类规则主战场 | AiNodeExecutor + 新 Skill |
| **P1-3** | `logic` 节点补齐（DDL 已有 `logic`，`NodeExecutor.cs:95` 无 case） | 多条件必需 | 引擎 |
| **P2-1** | `get_snippet` / `fetch_records` / `count` / `set_ops` | 记录集与段落取数 | 新 Skill |
| **P2-2** | `ai_correlate` | 跨文档关联 | 新 Skill |
| **P2-3** | 三处下游写入（compliance_check / nonconformity / report_section_source） | 闭环 | 服务层 |

---

## 9. 职责边界

| 层 | 归属 | 内容 |
|---|---|---|
| **证据供给** | 研发 | 提取 prompt 加 quote/position 输出、`PositionInfo` 激活、`get_snippet` |
| **证据传递** | 研发 | 节点输出五元组、`claim` 字段、可信度传递算法 |
| **裁决与落库** | 研发 | `verdict` Skill、`AggregateNcResult` 三态、三处下游写入 |
| **AI 语义能力** | 研发 | `ai_normalize` / `ai_judge` / `ai_correlate` 的骨架、输出 schema、证据注入机制 |
| **Prompt【任务】段** | **专家/维护** | 自然语言规则描述、判定口径、`missingItems` 定义 |
| **`claim` 文案** | **专家/维护** | 每个节点"在比什么"的人读说明（可由模板生成后修订） |
| **`severityRules`** | **专家/维护** | 违规 → major/minor/observation 映射 |
| **可信度阈值** | **专家/维护** | 0.85 / 0.60 分级线可按机构策略调整 |
| **规则内容与阈值** | **专家/维护** | 选条款、配 fieldCode、设目标值 |

---

## 附录 A：AI 节点证据注入后的完整 Prompt 示例（规则 B · n11）

```
【角色】
你是 ISO 13485 医疗器械质量管理体系审核员，仅输出 JSON，不作解释。

【证据】
[证据1] 来源：4记录文件/质量类/XASL-QR-013 管理评审报告.doc (v1, 第1页, 评审内容及相关资料)
原文：「根据年度内审计划要求，对我公司建立的质量管理体系运行情况进行内部审核。本次审核共发现1项不符合项。已完成全部整改。」
提取值：2026.05.15-16 内审，1 项不符合，已整改  提取置信度：0.93

[证据2] 来源：4记录文件/质量类/XASL-QR-018 内审不合格报告.doc (v1, 第1页, 生产部)
原文：「在生产车间半成品储存区有多种型号，未发现物料状态标识，不符合标准7.5.8…计划完成日期：2026.05.17 实际完成日期：2026.05.17」
条款号：ISO13485 7.5.8  类别：一般不合格  提取置信度：0.96

【任务】
判断管理评审报告的评审内容是否提及了该内审发现的不符合项，且是否说明其整改已闭环。
两段记录是否指向同一不符合项（比对条款号、部门、日期）。

【输出】
严格返回 JSON：
{ "verdict": "pass|fail|unknown", "confidence": 0.0-1.0, "reason": "...",
  "links": [{"aIndex":1,"bIndex":1,"matchKeys":["7.5.8","生产部"]}],
  "citedQuotes": ["<你实际引用的原文片段>"] }
```

---

## 附录 B：本文档引用的代码与数据位置

| 主题 | 位置 |
|---|---|
| compare 字符串分支不支持 `<=` | `CertPlatform.Admin/Services/Workflow/Skills/CompareSkill.cs:80` |
| docField 取数、无 sourceText 输出 | `CertPlatform.Admin/Services/Workflow/NodeExecutor.cs:319-356` |
| AI 节点多引用解析（已有） | `CertPlatform.Admin/Services/Workflow/AiNodeExecutor.cs:586 ResolveTemplateRefs` |
| AI 节点输出无 confidence/claim | `CertPlatform.Admin/Services/Workflow/AiNodeExecutor.cs:186-194` |
| `logic` 节点无执行 case | `CertPlatform.Admin/Services/Workflow/NodeExecutor.cs:95`（switch 无 `logic`） |
| 聚合把"执行成功"当"NC 通过" | `CertPlatform.Admin/Services/Workflow/WorkflowInterpreter.cs:228-256` |
| `outputConfig` 死配置 | `CertPlatform.Admin/Services/Workflow/Models/WorkflowConfig.cs:27` |
| `PositionInfo` 声明但无写入 | `CertPlatform.Shared/Entities/Doc/ExtractionResult.cs:58`、`TableExtractionResult.cs:58` |
| 三张下游表已建、无写入方 | `scripts/db/all_tables_ddl.sql` L1417 / L145 / L1679 |
| 真实文档数据 | `docs/90-归档/案例资料/CS河北雄安尚龙医疗科技有限公司13485体系材料/` |
