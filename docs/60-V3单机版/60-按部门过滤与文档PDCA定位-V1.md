# 60-按部门过滤与文档 PDCA 定位-V1

> 起因：59 号文完成按类型路由后，用户追加两条执行侧要求：
> 1. 审核是按「部门 + 条款」检查的，执行时要能按部门过滤对应的过程、指导书、记录，判断该部门有哪些证据、哪些记录。
> 2. 每份文档按标准的 PDCA 可以判断它属于方针、策划、执行还是绩效，这是重要信息。
>
> 本文记录这两项能力的代码接入、运行时实测结果和仍然存在的缺口。

---

## 一、结论

| 能力 | 状态 | 实测 |
|---|---|---|
| `ExtractRequest.DeptNames` 按部门过滤 | 已接运行时 | 生产部 → 入队 2；品质部 → 入队 2；不存在部门 → `Queued=0` + 人话原因 |
| `ModeOutput.PdcaPhase` | 已接运行时 | 17/10/09 = 执行；01 外部因素 = 策划；内审检查表 = 绩效 |
| `ModeOutput.PdcaBasis` | 已接运行时 | `8.4.2、8.6 → 执行`；`4.1 → 策划`；过程画像无条款时写“过程画像（实施类）→ 执行” |
| `ModeOutput.DeptNames` | 已接运行时 | 17 = 生产部/品质部/技术部/仓库；10 = 品质部；01 外部因素 = 综合部 |
| 画像回写 `SupportDepts` | 已修复并落库 | 17 画像已写 `SupportDepts=["品质部","技术部"]` |
| 指导书证据指针原文校验 | 已接运行时 | 09 的 `来料检验记录/不良标签` 被删，只保留原文有的 `修理牌` |

---

## 二、部门过滤怎么用

### 2.1 请求

```json
POST /api/Auditor/EnterpriseDoc/extract
{
  "EnterpriseCode": "7f9c7fa37cbf4c14aa1f4bffdc30ae51",
  "FileCodes": ["...17", "...10", "...09", "...05"],
  "DeptNames": ["生产部"],
  "ReExtract": true
}
```

- `FileCodes` 先收窄文件集合；
- `DocTypes` 再收窄文档类型；
- `DeptNames` 最后按画像 `OwnerDept` / `SupportDepts` 包含匹配；
- 全不命中返回 `Queued=0`，`Message` 直接说“没有匹配部门「XX」的文档”，不调用 LLM。

### 2.2 实测

| 过滤 | 文件集合 | 结果 |
|---|---|---:|
| `DeptNames=["生产部"]` | 17、10、09、05 | `Queued=2`（17、09） |
| `DeptNames=["品质部"]` | 17、10、09、05 | `Queued=2`（17 通过 `SupportDepts` 命中，10 通过 `OwnerDept` 命中） |
| `DeptNames=["不存在部门"]` | 17、10、09、05 | `Queued=0`，提示“没有匹配部门” |

### 2.3 画像回写

17 原画像只有 `OwnerDept=生产部`，没有 `SupportDepts`。本次修复后：

```json
{
  "OwnerDept": "生产部",
  "SupportDepts": ["品质部", "技术部"],
  "ExtractionModeName": "过程画像"
}
```

> 坑：`JsonNode.ToJsonString(new JsonSerializerOptions { Encoder = ... })` 在 .NET 8 下会抛
> `JsonSerializerOptions instance must specify a TypeInfoResolver setting before being marked as read-only`。
> 已补 `TypeInfoResolver = new DefaultJsonTypeInfoResolver()`，否则 `SupportDepts` 回写会静默失败。

---

## 三、PDCA 定位口径

代码位置：`src/certplatform-api/CertPlatform.Auditor/Services/Ent/DocPdcaClassifier.cs`。

| 条款 | 阶段 |
|---|---|
| `5.2` | 方针 |
| `4.*` / `5.*` / `6.*` | 策划 |
| `7.*` / `8.*` | 执行 |
| `9.*` | 绩效 |
| `10.*` | 改进 |
| 跨 2 个以上阶段，或手册 | 综合 |
| 无条款 | 按模式兜底：过程/指导/文件控制 → 执行；组织环境 → 策划；审核/评审 → 绩效 |

`PdcaBasis` 同时输出依据，例如：

```json
{"PdcaPhase":"执行","PdcaBasis":"8.4.2、8.6 → 执行"}
{"PdcaPhase":"策划","PdcaBasis":"4.1 → 策划"}
{"PdcaPhase":"绩效","PdcaBasis":"模式 audit_profile 无条款"}
```

### 3.1 实测

| 文档 | 模式 | `PdcaPhase` | `PdcaBasis` | `DeptNames` |
|---|---|---|---|---|
| 17 生产和服务提供控制程序 | process_profile | 执行 | 过程画像（实施类）→ 执行（代码口径） | 生产部、品质部、技术部、仓库 |
| 10 来料检查作业指示书 | work_instruction_citation | 执行 | 8.4.2、8.6 → 执行 | 品质部 |
| 09 通用设备安全操作规程 | work_instruction_citation | 执行 | 7.1.3 → 执行 | 生产部 |
| 01-外部因素识别监视评价总清单 | context_list | 策划 | 4.1 → 策划 | 综合部 |
| 05-1 内审检查表（生产部） | audit_profile | 绩效 | 审核/评审（第 9 章）→ 绩效 | 生产部 |
| 01设备台账 | record_profile | 执行 | 记录表单（执行层）→ 执行 | 设备管理部 |

> 17 的那次运行时 `PdcaBasis` 还是旧口径“模式 process_profile 无条款”；代码已改为“过程画像（实施类）→ 执行”，未再复跑。
> 内审复测后 `AuditProfile.Scope` 已进入 `DeptNames=["生产部"]`，`PdcaBasis` 为“审核/评审（第 9 章）→ 绩效”。
> 设备台账是记录类兜底：没有条款时不再落“未判定”，按记录表单 → 执行。

---

## 四、指导书证据指针必须原文命中

09 第一版运行时出现串档：

```json
"DocumentEvidencePointers": ["修理牌", "来料检验记录", "不良标签"]
```

但 09 原文只有“修理牌”，`来料检验记录` / `不良标签` 是模型从别的文档串进来的。已加代码 guard：

- 只保留 `DocumentEvidencePointers` 中文原文命中的项；
- `ReportEvidenceHints` 只保留与命中项同名的项；
- 全部不命中回 `["无记录"]`。

复测 09：

```json
"DocumentEvidencePointers": ["修理牌"],
"ReportEvidenceHints": [{"Name":"修理牌","RelatedClause":"7.1.3","Source":"document"}]
```

这说明“让模型自己保证证据指针真实”不可靠，必须由代码拿原文校验。

---

## 五、当前边界

1. PDCA 只覆盖新路由模式；旧 `ClauseFull` 手册结果没有 `PdcaPhase`。
2. 17 的过程画像没有 `PrimaryClauses`，所以 `PdcaBasis` 只能写“过程画像（实施类）→ 执行”；如果要按条款追溯，需要过程提示词补 `PrimaryClauses` 或在代码里按 `ProcessArea` 映射。
3. `DeptNames` 过滤依赖画像的 `OwnerDept/SupportDepts`；旧画像没有 `SupportDepts` 的文档，需要重跑一次才会命中支持部门。
4. 内审/评审的 `Scope` 已收进 `ModeOutput.DeptNames`，但画像侧没有 `OwnerDept`，按部门过滤仍只能靠 `FileCodes`；后续需要把审计范围回写画像。
5. 记录类已补“记录表单（执行层）→ 执行”兜底，`01设备台账` 实测通过；但检验记录、不合格记录、管理评审记录还没有按条款细分阶段。
6. 前端仍没有 `DeptNames`、`DocTypes`、`PdcaPhase` 的筛选/展示控件。

---

## 六、建议下一步

1. 给过程提示词补 `PrimaryClauses`（至少 `8.1`、`7.1.3` 等），让 17 的 PDCA 有条款依据，而不是模式兜底。
2. 记录类补 `PdcaPhase` 兜底：检验/生产记录 → 执行；内审/管理评审记录 → 绩效；不合格/纠正措施记录 → 改进。
3. 用“部门 + 条款”做一次真实收窄测试：生产部 + `8.5.1`，看能否只返回 17/生产记录/指导书，不返回品质部来料指导书。
4. 前端加三个只读标签：`DocType`、`OwnerDept/DeptNames`、`PdcaPhase`，先不做筛选器。
