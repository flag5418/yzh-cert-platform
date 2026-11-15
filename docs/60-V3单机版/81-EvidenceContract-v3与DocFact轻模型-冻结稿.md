# 81-EvidenceContract-v3与DocFact轻模型-冻结稿

> 版本：`v3`  
> 冻结日期：2026-11-15  
> 性质：P0 冻结稿，不写库、不覆盖生产提示词。  
> 用途：作为 P1/P2/P3 的 schema 基准和 5 条条款的 oracle 对照。

---

## 0. 冻结范围

本次只冻结四件事：

1. `EvidenceContract v3`：条款侧“期望什么证据”；
2. `DocFact` 轻模型：文档侧“实际有什么事实”；
3. `AuditEvidence` 输出：审核侧“证据项 → 事实 → 缺口”；
4. 5 条条款的 oracle 契约卡：`7.5.1 / 8.2.4 / 8.2.6 / 8.3.2 / 7.6`。

**不冻结：**具体提示词正文、数据库行、页面字段、全量条款。

---

## 1. EvidenceContract v3

### 1.1 顶层结构

```json
{
  "ContractVersion": "v3",
  "ChapterCode": "8",
  "PDCAPhase": ["C"],
  "EvidenceItems": [],
  "Dependencies": [],
  "CrossChecks": [],
  "AuditorRules": []
}
```

### 1.2 EvidenceItem

```json
{
  "ItemId": "E1",
  "DocRole": "procedure | work_instruction | record | manual | policy",
  "EvidenceRole": "establishment | implementation | criteria | support",
  "Name": "内部审核程序",
  "NameAliases": ["内部审核控制程序"],
  "Required": true,
  "Condition": "",
  "ExpectedFields": ["审核范围", "审核准则", "审核员", "频次"],
  "Frequency": "每年至少一次",
  "Coverage": "全部门",
  "ReportUse": ["coverage", "counter"]
}
```

**字段规则：**

| 字段 | 规则 |
|---|---|
| `ItemId` | 条款内唯一，`E1/E2/...` |
| `DocRole` | 只能取 5 类 |
| `EvidenceRole` | `establishment` 证明“有规定”；`implementation` 证明“有执行”；`criteria` 证明“验收准则”；`support` 辅助 |
| `Name` | 条款期望的文档名，不写企业实际文件名 |
| `NameAliases` | 报告/企业常用别名，最多 3 个 |
| `Required` | 缺此项即契约不完整；条件项为 `false` |
| `Condition` | 条件项写触发条件；通用项空串 |
| `ExpectedFields` | 这条证据至少要看哪些字段 |
| `Frequency` | `每批/每事件/每年至少一次/每设备/每周期/未明确` |
| `Coverage` | `全批次/全部门/全设备/全产品/抽样/未明确` |
| `ReportUse` | `establishment/sample/coverage/counter/criteria/combination/support` |

### 1.3 Dependencies

```json
[
  {"Clause": "4.2.5", "Role": "evidence_rule", "Use": "记录控制要求"},
  {"Clause": "7.5.9", "Role": "support", "Use": "批号追溯"}
]
```

`Role` 只能取：
- `evidence_rule`：记录控制/文件控制这类“证据规则”，不单独开检查项；
- `support`：辅助证据；
- `prerequisite`：前置条件。

### 1.4 CrossChecks

```json
[
  {
    "CheckId": "C1",
    "Question": "计划中的审核员与检查表、内审报告中的审核员是否一致",
    "Docs": ["内审计划", "内审检查表", "内部质量审核报告"],
    "MismatchUse": "doubts"
  }
]
```

**规则：**`MismatchUse` 只能是 `doubts | report_only | gap`，⛔ 不允许直接 `nc`。

### 1.5 AuditorRules

```json
[
  "模板存在但抽样为空 = template_only，不判未执行",
  "报告里的批次数据只能作 report_only，不回填文档事实",
  "名单不一致只进 doubts，不直接判 NC"
]
```

**规则：**`AuditorRules` 只给审核员/证据组织用，⛔ 不参与提取判定。

---

## 2. DocFact 轻模型

### 2.1 公共字段

```json
{
  "DocCode": "QR-015",
  "DocName": "内部审核计划",
  "DocClass": "manual | procedure | work_instruction | record | policy",
  "DocRole": "establishment | implementation | criteria | support",
  "ExtractionDepth": "citation_only | structured | criteria | fact_profile",
  "NameMatch": "exact | alias | mismatch | not_found",
  "ApplicabilityMatch": "same | different | unknown"
}
```

### 2.2 记录类（record）6 问

```json
{
  "DocClass": "record",
  "CoreRole": "生产过程自检记录",
  "KeyFields": ["产品名称", "型号", "批号", "数量", "检验项目", "检验结果"],
  "FillStatus": "filled | partial | blank | template_only | unreadable | absent",
  "SampleRows": [{"RowRef": "第2行", "Values": {"批号": "20260623"}}],
  "Coverage": "全量 | 抽样 | 无实例"
}
```

**补充：**
- `InstanceStatus`：`has_instance | template_only | zero_event | unknown`；
- `FillStatus=blank` 与 `template_only` 的区别：
  - `blank`：有表头、无任何数据；
  - `template_only`：模板存在，本次抽样无实例；
  - `absent`：文件夹里没有这份文档。

### 2.3 程序类（procedure）6 问

```json
{
  "DocClass": "procedure",
  "CoreRole": "规定生产和服务提供的控制要求",
  "Scope": "适用于手动轮椅车生产过程",
  "KeyControls": ["生产计划", "作业指导", "过程确认", "标识", "放行"],
  "RelatedForms": ["PR-025", "PR-026", "PR-028"],
  "RelatedDocs": ["QP-020"]
}
```

### 2.4 指导类（work_instruction）6 问

```json
{
  "DocClass": "work_instruction",
  "CoreRole": "规定成品检验的检验项目和接收准则",
  "Applicable": "手动轮椅车成品",
  "InspectionItems": ["外观", "尺寸", "装配", "制动"],
  "AcceptanceCriteria": ["符合产品技术要求"],
  "RelatedRecords": ["QR-027"]
}
```

### 2.5 手册/方针（manual/policy）4 问

```json
{
  "DocClass": "manual",
  "CoreRole": "质量手册，覆盖体系范围和删减",
  "ClauseAnchors": ["4.1", "4.2", "5.1"],
  "KeyCommitments": ["建立并保持质量管理体系"],
  "RelatedProcedures": ["QP-001", "QP-002"]
}
```

### 2.6 DocFact 边界

| 允许 | 不允许 |
|---|---|
| 企业文档里实际有的字段、样本行、填写状态 | 报告里的批次、数量、结论 |
| 文档名/编号/版本/日期 | ERP/现场照片里的数据（应进 `onsite`） |
| 名称别名匹配结果 | “未执行/未批准”等推断 |
| 模板存在但抽样为空 | 把空白模板写成“缺失” |

---

## 3. AuditEvidence 输出

### 3.1 结构

```json
{
  "evidence_used": [
    {
      "item_id": "E1",
      "doc_code": "QR-015",
      "doc_name": "内部审核计划",
      "match": "exact | alias | partial | missing",
      "fact": "2026.05.15–16，组长尚月永，组员王倩，五部门"
    }
  ],
  "coverage_gap": [
    {
      "item_id": "E3",
      "gap": "template_only | blank | partial | not_found",
      "note": "成品检验记录模板存在，抽样为空"
    }
  ],
  "report_only": [
    {
      "item_id": "E3",
      "source": "report",
      "note": "报告有 2026-06-23 批次 100 台，文件夹无对应记录"
    }
  ],
  "cross_check_results": [
    {
      "check_id": "C1",
      "result": "consistent | inconsistent | unknown",
      "note": "计划只列王倩，检查表出现董晶晶，报告只写王倩"
    }
  ],
  "audit_evidence": [
    {
      "clause_number": "8.2.4",
      "text": "1、查阅《内部审核计划》……；2、查阅《内部审核检查表》……"
    }
  ],
  "conclusion": "conformant | minor_nc | major_nc | observation | unknown",
  "doubts": []
}
```

### 3.2 规则

1. `evidence_used` 必须引用 `EvidenceItem.ItemId`；
2. `coverage_gap` 只写“文档侧缺什么”，不写“未执行”；
3. `report_only` 单独成块，⛔ 不进 `evidence_used`；
4. `cross_check_results` 不一致只进 `doubts`；
5. `audit_evidence` 只写事实，不写“符合/不符合”；
6. `conclusion` 最后判，且 `unknown` 是合法值。

---

## 4. 校验规则

| 编号 | 规则 | 失败处理 |
|---|---|---|
| V1 | `ContractVersion=v3` | 拒收 |
| V2 | `EvidenceItems` ≥ 3 | 拒收 |
| V3 | `ItemId` 唯一 | 拒收 |
| V4 | `Required=true` 的项必须有 `ExpectedFields` | 拒收 |
| V5 | `Condition` 非空时 `Required` 必须为 false | 拒收 |
| V6 | `CrossChecks` 的 `MismatchUse` 只能是 `doubts/report_only/gap` | 拒收 |
| V7 | `ReportUse` 只能取枚举值 | 拒收 |
| V8 | `AuditorRules` 不得出现“判 NC”指令 | 拒收 |
| V9 | `DocFact` 不得包含 `report_only` 数据 | 拒收 |
| V10 | `FillStatus` 只能取 6 态 | 拒收 |

---

## 5. 5 条条款 oracle 契约卡

| 条款 | 章节/PDCA | 核心证据项 | 依赖 | 交叉检查 | 关键边界 |
|---|---|---|---|---|---|
| `7.5.1` | 7 / P-D | 生产程序、装配/包装指导、生产记录、放行记录 | `7.5.9` 追溯、`4.2.5` 记录控制 | 批号/数量/设备/人员一致 | 批记录空白=`template_only`，报告 3 批只作 `report_only` |
| `8.2.4` | 8 / C | 内审程序、计划、签到、检查表、不合格报告、内审报告 | `4.2.5` 记录控制、`6.2` 审核员能力 | 计划/检查表/报告名单与部门一致 | 单文档不能出“本次内审结论”；名单不一致只进 `doubts` |
| `8.2.6` | 8 / C | 监视测量程序、过程/成品检验指导、过程/成品检验记录、放行记录 | `4.2.5`、`7.6` 设备校准 | 批号/数量/检验项目/设备一致 | 产品线/规程不匹配=`ApplicabilityMatch=different`；报告批次只作 `report_only` |
| `8.3.2` | 8 / A | 不合格程序、通知/评审/处置表、让步/报废/返工记录、进货不合格记录 | `4.2.5`、`8.3.1`、`7.4.2` | 数量平衡、处置方式、法规声明 | 处置记录空白=`template_only`，不推断“未处置” |
| `7.6` | 7 / C | 装置程序、设备台账、周期检定计划、校准记录、软件确认 | `4.2.5`、`8.2.6` | 台账/校准/使用记录编号一致 | 台账半填=`partial`；报告 4 台只作 `report_only`；无软件=`not_applicable` |

完整 JSON 见 `81-5条条款Oracle契约卡.json`。

---

## 6. 与现有字段的映射

| 现有 | v3 | 处理 |
|---|---|---|
| `EvidenceType[]` | `EvidenceItems[].DocRole` | 拆分到每项 |
| `EvidenceName[]` | `EvidenceItems[].Name` | 拆分到每项 |
| `MustHaveFields[]` | `EvidenceItems[].ExpectedFields` | 拆分到每项 |
| `Frequency` | `EvidenceItems[].Frequency` | 保留 |
| `Coverage` | `EvidenceItems[].Coverage` | 保留 |
| `Signer` | `EvidenceItems[].ExpectedFields` 之一 | 不单独字段 |
| `JudgementRules` | `AuditorRules` | 改名，明确给审核员 |
| `ReportUse` | `EvidenceItems[].ReportUse` | 拆分到每项 |

**属性池不新增顶层属性。** `EvidenceContract` 仍是第 10 个属性，只是 JSON 内部升级。

---

## 7. 不改什么

1. 不改 `ChapterProfile` 的 4/5/6/7/8 章节划分；
2. 不新增 `EvidenceDependencies` / `CrossChecks` / `DocRole` 等顶层属性；
3. 不把 `4.2.5` 单独开成检查项；它只作 `evidence_rule`；
4. 不把报告数据写进 `DocFact`；
5. 不把 `NC` 从文档反推；
6. 不覆盖 `cert_prompt_template` 现有生产行。

---

## 8. 下一步 P1

1. 用本冻结稿写 `doc_fact_record_v3` 影子提示词；
2. 跑 5 条条款涉及的记录，产出 `DocFact`；
3. 与 79 号文档事实表逐项对比；
4. 通过后改 `doc_extract_process_profile` / `doc_extract_work_instruction_citation` / `doc_extract_policy_profile`。

> P1 第一次实测已完成：见 `82-记录类影子提示词与5份DocFact实测-V1.md`；5/5 `FillStatus` 与人工事实一致。

---

## 附：关联文件

- `80-提示词与属性池优化计划-V1.md`
- `79-5条条款章节骨架验证与报告对账-V1.md`
- `78-文档四层模型与章节骨架-V1.md`
- `81-5条条款Oracle契约卡.json`
