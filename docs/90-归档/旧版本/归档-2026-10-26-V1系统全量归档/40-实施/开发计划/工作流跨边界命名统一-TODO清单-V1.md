# 工作流跨边界命名统一 - TODO 清单-V1

> **文档版本**：V1
> **创建时间**：2026-10-16
> **状态**：✅ **已完成（2026-10-16，P0–P10 全部落地）** · **P11（2026-10-17 追加：C 上下文键强制 PascalCase）✅**
> **关联方案**：`docs/40-实施/开发计划/工作流跨边界命名统一方案-V1.md`
> **一句话**：把**跨边界键**（`rule_json` 外层 / `config` 内 / 端口边界）+ **运行上下文键 C**（2026-10-17 裁定）统一为 PascalCase，纯内部常量与 D/F 组不动。

---

## 〇、进度总览

| 阶段 | 内容 | 状态 |
|:--:|------|:--:|
| P0 | 键归类盘点 | ✅ 已完成（方案 §二） |
| D1 | 拍板 O2 与「呈现层落 DB」先后 | ✅ **已拍板：先做 O2（2026-10-16）** |
| P1 | 兼容读取入口 + config 读侧改调（**已含 P5 后端读半**） | ✅ **已完成（2026-10-16）** |
| P2 | 边界契约测试（**后端 ✅ / 前端 ✅**） | ✅ **已完成（2026-10-16）** |
| P3 | 前端端口字段翻 PascalCase | ✅ **已完成（2026-10-16）** |
| P4 | `rule_json` 外层键翻 PascalCase | ✅ **已完成（2026-10-16）** |
| P5 | `config` 键翻 PascalCase（后端 P1 已含；前端写入侧本阶段） | ✅ **已完成（2026-10-16）** |
| P6 | 删归一化层 `portNormalize.ts` | ✅ **已完成（2026-10-16，随 P3）** |
| P7 | 守卫（G1–G5） | ✅ **已完成（2026-10-16）** |
| P8 | §16.9 例外登记 E8 | ✅ **已完成（2026-10-16）** |
| P9 | 幂等迁移脚本（条件执行） | ✅ **已完成（2026-10-16，存量=0 空跑）** |
| P10 | 文档收尾 + 索引登记 | ✅ **已完成（2026-10-16）** |

---

## D1 — 拍板：O2 与「呈现层落 DB」的先后 ✅ 已拍板

| 选项 | 说明 | 结论 |
|:--:|------|:--:|
| D1-a | 先做「呈现层落 DB」，再做 O2 | ✗ |
| **D1-b** | **先做 O2**（存量 = 0，最便宜） | ✅ **采纳（2026-10-16）** |
| D1-c | 维持 O1（不动） | ✗ |

> **决议**：先做 O2。理由：`rule_json` 存量全 NULL（唯一零迁移窗口）+「呈现层落 DB」无计划文档。代价：`specialNodes.ts` 日后会被再触碰一次。

---

## P1 — 兼容读取入口（不改数据、不改写入方）✅ 已完成

**落地形态**：新增 `WorkflowConfigAccess`（大小写不敏感读取），并把后端**所有 config 键读取**改为「走该入口 + 字面量直接写成 PascalCase」。
因入口本身大小写不敏感 ⇒ 读侧**同时命中 camel / Pascal**，故 **P5 的后端「读」半已随 P1 一并完成**，P5 仅剩前端写入侧（P5.2–P5.4）。

| ID | 文件 | 改动 | 状态 |
|:--:|------|------|:--:|
| P1.1 | `CertPlatform.Shared/Workflow/Models/WorkflowConfigAccess.cs`（**新增**） | `GetConfigValue` / `TryGetConfigValue`（`IDictionary<string,object>`）+ `GetConfig` / `TryGetConfig` / `GetConfigString`（`WorkflowNodeConfig`）：先精确命中、再 `OrdinalIgnoreCase` | ✅ |
| P1.2 | `NodeExecutor.cs` | 4 处：`value`→`Value`、`ruleCode/fieldCode/docType`→Pascal、`ruleCode/tableCode/docType`→Pascal、`standardFileCode`→`StandardFileCode` | ✅ |
| P1.3 | `AiNodeExecutor.cs` | 12 处：`promptTemplate/outputType`（×2）、`PromptKey`、`model`、`systemPrompt`、`customParams`→`CustomParams`、`SourceConfig` 的 `value/nodeId/portName/skillCode/skillParams/skillParamsTypes`→Pascal；另 `ToCustomParam` **写入侧** `SourceConfig` 键同步改 Pascal（`NodeId/PortName/Value`） | ✅ |
| P1.4 | `TaskCacheService.cs` | 6 处：`docType/ruleCode/fieldCode/tableCode`→Pascal（docfield ×3 + doctable ×3） | ✅ |
| P1.5 | `AiParams.cs` | `ConfigKey="Params"`、`PromptKey="PromptTemplate"`；两处 `TryGetValue` → `TryGetConfigValue` | ✅ |
| P1.6 | `GapDetector.cs`（**Auditor**） | 助手 `GetConfig` 改走 `GetConfigString`；3 个调用键 `RuleCode/FieldCode/TableCode` | ✅ |
| P1.7 | `WorkflowTestController.cs` | `SourceConfig` 的 `nodeId/portName`→`NodeId/PortName`（AiParamUsage 回显） | ✅ |
| P1.8 | `WorkflowInterpreter.cs` / `WfExecutionTaskService.*.cs` / `StandardDirectoryService.cs` | **逐处复核为 E3/E4 载荷字典（非 `Config`）⇒ 不动** | ✅ 无需改 |

> ⛔ **P1 只加不删**：入口同时接受 camel / Pascal，**行为等价**，可独立验证、可独立回退。
> ⚠️ **明确留在原处（非 config，不属本阶段）**：`DocTemplateController.cs:651` / `StandardDirectoryService.cs:410`（`Extra` DTO 字典）、`SrcGlobalParamSkill.cs:152` / `BuildNcPromptSkill.cs:278`（**SkillResult 输出载荷**＝E3）、`ResolveLinkParam` 的 `dict.GetValueOrDefault("result"/portName)`（节点输出字典＝E3）。

**验收结果（2026-10-16）**：
```bash
grep -rnE '(Config|SourceConfig)\??\.(GetValueOrDefault|TryGetValue)\(' src/certplatform-api --include=*.cs | grep -v Tests
# 实测：0 处
dotnet build CertPlatform.sln                       # 0 Error
dotnet test CertPlatform.Admin.Tests                # 742/742 通过（Workflow 78/78）
```

---

## P2 — 边界契约测试（“稳定”的唯一证据）✅ 已完成（后端 12 例 + 前端 11 例）

**落地形态**：夹具以 <b>内联 `const string`</b> 形式与 `<see cref="WorkflowConfigParserPortTests"/>` 同构（沿用库内既有范式，避免 `csproj` 复制 JSON 的额外管线）；覆盖 docfield / doctable / constant / ai_node 四类消费 `config` 的节点。

| ID | 文件 | 内容 | 状态 |
|:--:|------|------|:--:|
| P2.1 | `CertPlatform.Admin.Tests/Workflow/WorkflowConfigContractTests.cs` 内联夹具 | 真实形状 `rule_json`（合法拓扑：start→docfield→doctable→const→ai→end；外层 camel、内层 config 混用 camel/Pascal） | ✅ |
| P2.2 | 同上（12 例） | ① 入口语义（精确优先 / 忽略大小写 / 负向对照）② 旧 camel 与新 Pascal 键都可读 ③ `AiParams` 对 `params`/`Params` 均可解析 + 键名不符 ⇒ 空 ④ `CollectSourceNodeIds` 对 `promptTemplate`/`PromptTemplate` 结果一致 | ✅ |
| P2.3 | `cert-admin/src/pages/workflow/serializer.test.ts` | `serialize()` 产出键名 = PascalCase 白名单（顶层 6 键 + 节点/边键全量）；反例「Version vs version 并存」判违规；`deserialize()` 读旧 camelCase；`extractLayout()` 仍为 camelCase（边界 F） | ✅ **11 例**（2026-10-16） |
| P2.4 | 双向对齐（同一份夹具） | 前端序列化产物 == 后端测试输入 | ✅ 由两侧键集对齐保证：`serializer.test.ts` 断言产出为 Pascal 白名单 + round-trip 一致；后端 `WorkflowConfigContractTests` 证明 Pascal/camel **双向可读**。未做进程级联调（无必要） |

**范式**：`WorkflowConfigParserPortTests.cs`（5 例）· `specialNodes.test.ts`（含负向对照）。
**命令**：`./node_modules/.bin/vitest run`（在 `cert/cert-admin`）· `dotnet test`。
**验收结果（2026-10-16）**：`dotnet test` → **754/754 通过**（新增 12 例）；前端 `vitest run` → **387/387 通过**（含新增 `serializer.test.ts` 11 例）。

---

## P3 — 前端端口字段翻 PascalCase（边界 E）✅ 已完成

| ID | 文件 | 改动 | 状态 |
|:--:|------|------|:--:|
| P3.1 | `cert-share/src/composables/workflow/specialNodes.ts` | `PortDef`：`name/label/type/description/bindMode/required/allowEmpty/maxIn/role/display/defaultValue` → **Pascal**，并补 `EnumSource`；17 个端口字面量全部同步 | ✅ |
| P3.2 | `cert-share/src/components/workflow/NodePropertyForm.vue` | 模板端口字段（`port.Name/Label/Required/Description/BindMode/EnumSource`）+ `visibleInput/OutputPorts`（`Display`/`Role`）+ `linkableNodes` 兜底 + `getPortPlaceholder` + 构造循环 | ✅ |
| P3.3 | `cert-share/src/components/workflow/WorkflowDesigner.vue` | `computeNodeInputsFromEdges` 端口读取改 Pascal；`loadSkills` 端口无需改（透传后端 Pascal） | ✅ |
| P3.4 | `cert-share/src/components/workflow/SkillPanel.vue` | 无需改（未读端口字段） | ✅ |
| P3.5 | `specialNodes.test.ts`（端口说明校验器）/ `node-property-form-test-result.test.ts`（夹具） | 同步 Pascal | ✅ |

> ⚠️ `classCode`/`className`/`panelSchema`/`singleton` 等属**内部灰区（E8）**，**不在本阶段**。
> ⚠️ **延后项**：`WorkflowDesigner.loadSkills` 的**内部小写别名**（`skillCode/skillName/description/category/skillType`）暂留 —— 它把后端 Pascal 映射到前端内部模型字段（E8 灰区），与端口无关，非本阶段范围。

---

## P4 — `rule_json` 外层键翻 PascalCase（边界 A）✅ 已完成（2026-10-16）

| ID | 文件 | 改动 | 状态 |
|:--:|------|------|:--:|
| P4.1 | `serializer.ts` | `nodeId→NodeId`/`nodeType→NodeType`/`skillCode→SkillCode`/`inputPorts→InputPorts`/`outputPorts→OutputPorts`/`inputTypes→InputTypes`/`title→Title`/`config→Config`/`inputs→Inputs`；边 `source→Source`/`target→Target`/`sourceHandle→SourceHandle`/`targetHandle→TargetHandle`；顶层 `outputConfig→OutputConfig` | ✅ 整体重写：`serialize()` 只写 Pascal；`deserialize()` 读 Pascal + camel 兜底 |
| P4.2 | `WorkflowNodeConfig.cs` / `WorkflowEdgeConfig.cs` / `PortConfig.cs` / `WorkflowConfig.cs` | 删除显式 camelCase `[JsonPropertyName]`（`nodeId`/`skillCode`/`maxIn`/`sourceHandle`/`outputConfig`…） | ✅（含移除 `using System.Text.Json.Serialization;`） |
| P4.3 | `WorkflowConfigParser.cs` | **保留** `PropertyNameCaseInsensitive = true`（G5 守卫） | ✅ 保留在位 |
| P4.4 | `compiler.ts` / `topology.ts` / `useWorkflowStore.ts` / `nodeIdGenerator.ts` | 读取既有 `rule_json` 处改大写 | ✅ `compiler`/`topology` 写 Pascal + 读容忍；`useWorkflowStore` 经 `deserialize`；`nodeIdGenerator` 不读 `rule_json` 键 |

---

## P5 — `config` 键翻 PascalCase（边界 B，**最高危**）

> ★ **后端（读+写）已随 P1 完成**（P5.1 / P5.5 / P5.6 ✅）；**前端写入侧本阶段已完成**（2026-10-16）。

| ID | 文件 | 改动 | 状态 |
|:--:|------|------|:--:|
| P5.1 | `AiParams.cs` | `ConfigKey = "Params"` / `PromptKey = "PromptTemplate"` | ✅（P1.5） |
| P5.2 | `specialNodes.ts` `panelSchema[].field` | `config.docType` → `config.DocType` …（11 处） | ✅（14 处含 `visibleWhen`） |
| P5.3 | `NodePropertyForm.vue` | `config.*` 读写改大写 | ✅（`panelFieldValues['config.RuleCode'/'FieldCode'/'TableCode']`、`commitAiParams` 写 `Params`、`syncStdContextToConfig` 写 `StandardCode/StageCode`、测试体内部读改 Pascal，**HTTP body 键仍 camel**） |
| P5.4 | `aiParams.ts` | `config.params` / `config.promptTemplate` 读写改大写 | ✅（`config?.Params` / `config?.PromptTemplate`；`topology.ts` 的 `PromptTemplate` 同步） |
| P5.5 | 后端 P1 入口默认键 | `GetConfig(node,"DocType")` 等 | ✅（P1） |
| P5.6 | `TaskCacheService.cs` / `NodeExecutor.cs` / `AiNodeExecutor.cs` | config 键改大写 | ✅（P1） |

> ⚠️ **禁止**动 D 组载荷键（E3）。**C 组上下文键已改为 PascalCase**（2026-10-17 裁定，见 P11）。
> ⚠️ **前后端过渡期一致性**：入口大小写不敏感 ⇒ 前端仍写 camelCase 时后端照读；前端翻 Pascal 后亦照读。故 P5.2–P5.4 可独立推进、可独立回退。
> ★ **P5 范围裁定（2026-10-16）**：仅 `config.X` **容器键**翻 Pascal；`config.Params[]` **元素键**（`name/label/sourceType/nodeId/portName`）**保持 camelCase** —— 后端 `AiParamDecl`/`CustomParam` 大小写不敏感读取（`CustomParam.cs` 甚至显式声明 camel `[JsonPropertyName]`），且 `SourceConfig` 子对象已在 P1 `ToCustomParam` 翻 Pascal。

---

## P6 — 删归一化层（唯一实质收益）✅ 已完成（随 P3）

| ID | 项 | 验收 | 状态 |
|:--:|----|------|:--:|
| P6.1 | 删 `cert-share/.../portNormalize.ts` | 文件不存在 | ✅ |
| P6.2 | `serializer.ts` / `useWorkflowStore.ts` 去 `normalizePorts` 调用（共 4 处） | 无引用 | ✅ |
| P6.3 | `NodePropertyForm.vue` 去 `normalizePorts` 调用 | 无引用 | ✅ |
| P6.4 | 删 `portNormalize.test.ts`；全局引用检查 | `grep -rn portNormalize` 仅剩说明性注释 = 0 代码引用 | ✅ |

> ⚠️ `WorkflowDesigner.loadSkills` 的**内部小写别名清理**（原 P6.3）已**移出本阶段**（见 P3 延后项）：那是前端内部 meta 字段映射，非端口、属 E8 灰区。

---

## P7 — 守卫（防回退）✅ 已完成（2026-10-16）

| ID | 守卫 | 判据 | 期望 | 落地 |
|:--:|------|------|:--:|------|
| P7.1 | **G1** 后端小写 config 读 | `GetValueOrDefault("<小写B键>")` + `WorkflowConfigAccess` 访问器（`GetConfigValue`/`GetConfigString`/`TryGetConfigValue`/`GetConfig`）传小写字面量键 | 0 | ✅ `scripts/backend/check-workflow-config-keys.sh`（排除 `*.Tests`：P2 契约测试**故意**用小写键证明兜底） |
| P7.2 | **G2** 前端小写 config 写 | `config.<小写B键>`（代码形态）+ `'config.<小写B键\|value>'`（字符串形态） | 0 | ✅ `guards.mjs` **R22**（`config.value` 只查字符串形态：代码形态 `this.config.value` 是 Vue ref 正常写法） |
| P7.3 | **G3** 归一化层复活 | `portNormalize.ts` 文件存在 / 代码引用 `portNormalize`\|`normalizePorts` | 0 | ✅ `guards.mjs` **R23** |
| P7.4 | **G4** 契约测试入 CI | P2 纳入 | 必过 | ✅ `WorkflowConfigContractTests` 属 `CertPlatform.Admin.Tests`，随 `dotnet test CertPlatform.sln` 全量执行（785/785） |
| P7.5 | **G5** 大小写不敏感兜底 | `PropertyNameCaseInsensitive = true` 存在 | 保留 | ✅ `scripts/backend/check-workflow-config-keys.sh` 内含断言 |

> 落地到 `scripts/backend/check-workflow-config-keys.sh`（G1+G5）与 `scripts/frontend`→`guards.mjs` R22/R23（G2+G3）；守卫总数 32 → **34 条规则**。
> 自测：临时注入违规探针文件，R22/R23/G1 均能命中（已删探针）。

---

## P8 — 补齐例外登记（消除灰区）

| ID | 文件 | 改动 |
|:--:|------|------|
| P8.1 | `项目全局规则.md` §16.9 | 新增 **E8**：工作流设计器**前端目录元数据内部字段**（`classCode`/`className`/`category`/`color`/`icon`/`singleton`/`testable`/`renameable`/`panelSchema`/`field`/`visibleWhen`…）：无 DB 列、不落库、不跨端 ⇒ camelCase 合法 |
| P8.2 | 备注 | 若「呈现层落 DB」实施，显示名/说明/分组落列后**须转 PascalCase**，届时 E8 相应收缩（⛔ 不要与 O2 混谈） |

---

## P9 — 幂等迁移脚本（条件执行）✅ 已完成（2026-10-16，存量=0 空跑）

| ID | 项 | 说明 | 状态 |
|:--:|----|------|:--:|
| P9.1 | 前置判定 | 实测 `cert_validation_rule` 1 行 `RuleJson=NULL`；`cert_report_section` 8 行 `LayoutJson=NULL` ⇒ **当前无需迁移** | ✅ |
| P9.2 | 脚本模板 | `scripts/db/fix/20261016_workflow_keys_to_pascal_V1.sql`：备份 → 幂等 → `JSON_KEYS` 精确替换 → 校验 → 回滚说明 | ✅（§2 顶层改名幂等；§3 嵌套改名**不实现**并说明原因） |
| P9.3 | 校验 SQL | `SELECT COUNT(*) ... WHERE RuleJson LIKE '%"nodeId"%'` = 0 | ✅ 实测 `C_rulejson_小写键残留_期望0 = 0` · `C_rulejson_非法JSON_期望0 = 0` |
| P9.4 | ⛔ 禁止 | 全文 `REPLACE`（会误伤 `value`/说明正文） | ✅ 仅用 `JSON_SET`/`JSON_REMOVE` |

> ★ **§3 嵌套键（nodes[]/edges[] 元素键）为何不写进脚本**：MySQL 8 `JSON_SET` 不支持数组通配路径（`$.nodes[*].nodeId`），
> 纯 SQL 唯一路径 `JSON_TABLE`+`JSON_ARRAYAGG` 会**打乱数组次序**（8.0 不支持 `ORDER BY`）。
> 存量=0 ⇒ 无需；若将来触发，走**应用侧迁移**（读 JSON→改名→写回，保序可控），⛔ 不得用粗糙 `REPLACE`。
> 脚本执行后备份表 `_bak_wfkeys_20261016_*` 为空表，已清理（DB 保持原样）。

---

## P10 — 收尾 ✅ 已完成（2026-10-16）

| ID | 项 | 状态 |
|:--:|----|:--:|
| P10.1 | `开发计划/README.md` 登记本 TODO + 方案 | ✅ |
| P10.2 | `AGENTS.md` 若涉及铁律/守卫编号则同步 | ✅ 本次未新增铁律；守卫编号 R22/R23 + `scripts/backend/check-workflow-config-keys.sh` 已在 P7 登记 |
| P10.3 | 回归：`node scripts/guards.mjs` + 两端 `npm run build` + 后端 `dotnet build`/`dotnet test` | ✅ guards 34 规则通过 · cert-admin `npm run build`（含 vue-tsc）通过 · `dotnet build` 0 Error · `dotnet test` 785/785 · 前端 vitest 387/387 |
| P10.4 | 审计字段/时间字段若有写入 → 跑 `verify_createby.sql` / `check-utcnow.sh`（本任务通常不涉及） | ✅ 本任务未触碰审计/时间字段写入 ⇒ 不涉及 |

### 最终验收命令与结果（2026-10-16）
```bash
node scripts/guards.mjs                                  # ✓ 通过（34 条规则 / 1372 文件）
./scripts/backend/check-workflow-config-keys.sh          # ✓ G1 无小写读 + G5 兜底在位
dotnet build CertPlatform.sln                            # 0 Error
dotnet test CertPlatform.sln                             # 785/785 通过
cd src/certplatform-web/cert/cert-admin && ./node_modules/.bin/vitest run   # 387/387 通过
cd src/certplatform-web/cert/cert-admin && npm run build # vue-tsc + vite 通过
mysql < scripts/db/fix/20261016_workflow_keys_to_pascal_V1.sql  # 校验 0/0（无存量）
```

---

## P11 — C 运行上下文键强制 PascalCase ✅ 已完成（2026-10-17，用户裁定）

> **依据**：用户 2026-10-17 裁定「数据库 / C# / TS 强制 Pascal 结构，不允许其他编码风格」⇒ 覆盖方案 §2.1 原「C 不动」口径。

| ID | 文件 | 改动 | 状态 |
|:--:|------|------|:--:|
| P11.1 | `WorkflowContextKeys.cs` | 全部常量值改 Pascal：`OrgCode` / `StandardCode` / `PhaseCode` / `EnterpriseCode` / `RuleCode` / `NodeTitle` / `Judge` / `__ConnectedPorts` | ✅ |
| P11.2 | `NodeExecutor.cs` / `AiNodeExecutor.cs` | 12 + 6 处 `GetValueOrDefault("enterpriseCode"…)` 字面量 → 引用 `WorkflowContextKeys.*` 常量 | ✅ |
| P11.3 | `check-workflow-config-keys.sh` | 新增 **G6**：⛔ 禁止小写上下文键字面量（`GetValueOrDefault` / `TryGetValue` / `ReadContextParam` / `FromContext("…")`） | ✅ |
| P11.4 | 前端 | `WorkflowDesigner.vue` 已送 Pascal（无需改） | ✅ 无需改 |
| P11.5 | 守卫 | G6 = 0 + `guards.mjs` 0 违规 | ✅ |

---

## 附：不动清单（改前逐键核对，⛔ 防误改）

- **D 载荷（E3）**：`success/error/errorCode/result/decision/executedPaths/fieldValue/confidence/rows/state`
- **F 布局**：`nodePositions/transforms`
- **内部灰区（E8）**：`classCode/className/category/color/icon/singleton/testable/renameable/panelSchema/field/label/visibleWhen/maxIn/role/display`
- **E4 树 DTO**：`id/label/cbCode/stdCode/phaseCode`（`StandardDirectoryService` 的 `children` 等）

---

*（内容由AI生成，仅供参考）*
