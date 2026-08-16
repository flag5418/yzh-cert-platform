# LogicFlow 工作流设计器实施分析与建议

> **版本**：V1.0 | **日期**：2026-08-14 | **状态**：成熟态（基于用户决策更新）
>
> **前置文档**：
> - `YZH-AI引擎详细设计-V1.md`（七件套四件套已完成，S1-S4 通过）
> - `工作流引擎选型与技术研究-V1.md`（LogicFlow 选型已定，PoC 强制前置）
> - `核心工作原理-V1.md`（三引擎复用原理、F-03/F-04 数据链路）
> - `数据库表设计-V2.md`（域 F 四表定义）
>
> **关联 TODO**：
> - `YZH-AI引擎详细设计-V1.md` §11 [TODO:P2-4] LogicFlow 可视化配置
> - `工作流引擎选型与技术研究-V1.md` §十 [TODO:P2] 阶段 C 可视化配置工具
>
> **变更记录**：
> | 版本 | 日期 | 变更说明 |
> |------|------|---------|
> | V1.0 | 2026-08-14 | 基于项目现状核实 + 用户补充需求（自定义数据节点配置），形成完整实施分析与建议 |

---

## 一、核心结论速览

```
结论 1：必须先完成数据提取管道（B-08/B-09 有真实数据），再启动工作流设计器开发。
  └─ 原因：所有校验/报告工作流节点（get_field/get_table）都依赖 B-08/B-09 的已存提取值。
           没有数据，设计器配出来的 DAG 运行时永远查询不到数据，无法验证任何规则。

结论 2：工作流节点配置的是"标签模板"（F-02 label_tag），运行时注入"企业上下文"
  └─ 一套工作流可跨企业复用，不同企业只需 B-08/B-09 中有对应数据即可执行。

结论 3：LogicFlow 选型不变（V3.1 总体设计已定案），但实施必须按 E→F→G 三阶段顺序。
  └─ Phase E：数据管道接通（P0，阻塞 Phase F）
  └─ Phase F：LogicFlow 设计器（P1，依赖 Phase E）
  └─ Phase G：校验/报告引擎接入（P2，依赖 Phase F）
```

---

## 二、当前实施状态核实

### 2.1 后端 YZH.Core 四件套（已完成）

| 模块 | 位置 | 状态 | 证据 |
|------|------|:---:|------|
| SkillRegistry | `YZH-Framework/YZH.Core/Workflow/SkillRegistry.cs` | ✅ | `AllCodes()` 可查，单测 74/74 通过 |
| LlmClient + 三 Provider | `YZH-Framework/YZH.Core/AI/Clients/` | ✅ | Qwen/Ollama/Mock 三 Provider 路由正确，熔断/信号量实现 |
| PromptInterpreter | `YZH-Framework/YZH.Core/AI/Prompt/PromptInterpreter.cs` | ✅ | 占位符渲染 + JSON 围栏剥离 + 错误恢复 |
| WorkflowEngine | `YZH-Framework/YZH.Core/Workflow/WorkflowEngine.cs` | ✅ | Kahn 拓扑排序 + 条件分支 + F-04 留痕 |
| 内置 6 个 Skill | `YZH-Framework/YZH.Core/Skills/` | ✅ | GetFieldSkill/GetTableSkill/CompareSkill/AssembleSkill/LlmExtractSkill/DocumentExtractSkill |

### 2.2 运行期全链路（未接通，阻塞点）

| 环节 | 位置 | 状态 | 阻塞原因 |
|------|------|:---:|---------|
| **DocumentExtractSkill 接 IFileExtractor** | `Skills/DocumentExtractSkill.cs:150-156` | ❌ | 代码是骨架，`ExtractAsync` 调用处写 `// TODO[接入层]`，未接 MinIO Stream |
| **LlmExtractSkill 落 B-08/B-09** | `Skills/LlmExtractSkill.cs` | ❌ | 返回 `SkillResult` 但无落库代码，`Outputs` 未写入 `ent_extraction_result` / `ent_table_extraction_result` |
| **上传触发提取** | `DocExtractionRuleService` 4 个私有方法 | ❌ | 全部为 `TODO 模拟返回空`，无真实文件 → 提取 → 落库 链路 |
| **GetFieldSkill 查询 B-08** | `Skills/GetFieldSkill.cs:20-30` | ⚠️ | 代码逻辑正确，但 B-08 无任何记录，运行时永远返回"未找到" |
| **GetTableSkill 查询 B-09** | `Skills/GetTableSkill.cs:20-30` | ⚠️ | 同上，B-09 无任何记录 |

**关键结论**：YZH.Core 四件套是"引擎已造好，油没加"的状态。引擎能跑，但没有输入数据就没有输出。

### 2.3 前端工作流相关（不存在）

| 模块 | 状态 |
|------|:---:|
| `wf_skill` 后端 Service/Controller | ❌ 不存在 |
| `wf_workflow_definition` 后端 Service/Controller | ❌ 不存在 |
| `wf_field_label_mapping` 后端 Service/Controller | ❌ 不存在 |
| LogicFlow 依赖安装 | ❌ 未执行 |
| `workflow-designer` 独立模块 | ❌ 不存在 |
| `/CertPlatform/WorkflowDesigner` 路由 | ❌ 不存在 |
| 工作流配置前端页面 | ❌ 不存在 |

---

## 三、数据提取管道实施计划（Phase E，P0，先做）

### 3.1 为什么必须先做 Phase E

```
工作流节点依赖关系图：

get_field[label_tag] ──→ B-08 ExtractionResult（必须有记录）
get_table[table_code] ──→ B-09 TableExtractionResult（必须有记录）
compare / date_diff ────→ get_field 的输出（上游无数据，下游无意义）
llm_judge / llm_generate ──→ 从 B-08 读取已有提取值做语义判断

如果不先接通数据管道：
  ① 工作流设计器配出来的 DAG 无法验证（节点永远查不到数据）
  ② 审核员点击"审核" → 工作流执行 → 所有 get_field 节点返回"未找到" → 失败
  ③ 整个三引擎（提取/校验/报告）都无法运转
```

### 3.2 Phase E 任务分解

#### E1：DocumentExtractSkill 接真实 IFileExtractor（0.5 天）

**目标**：从 MinIO 下载文件 Stream，调用 `IFileExtractor.ExtractAsync()`，返回完整 `FileExtractionResult`。

**改动位置**：`YZH-Framework/YZH.Core/Skills/DocumentExtractSkill.cs:148-156`

```csharp
// 当前（骨架）：
// extraction = new FileExtractionResult { FullText = "", Fields = new(), Tables = new() };

// 改为（接真实 IFileExtractor）：
using var stream = await _minioClient.GetObjectAsync(
    bucket: "cert-files",
    objectName: effectivePath,
    ct: ct);
extraction = await extractor.ExtractAsync(stream, effectiveExt, ct);
```

**前置依赖**：`MinIO 客户端已注入`（需确认 `_minioClient` 是否在 Skill 构造函数中可注入）。

**验证标准**：
- 传入一个已知 docx 文件的 `storage_path`
- `extraction.FullText` 非空（包含文件实际文字内容）
- `extraction.Fields` 和 `extraction.Tables` 有结构（即使为空，也不抛异常）

---

#### E2：LlmExtractSkill 落 B-08/B-09（1 天）

**目标**：`LlmExtractSkill` 解析 LLM 输出后，将 `fields[]` 写入 `ent_extraction_result`（B-08），将 `tables[]` 写入 `ent_table_extraction_result`（B-09）。

**改动位置**：`YZH-Framework/YZH.Core/Skills/LlmExtractSkill.cs:175-192`（SkillResult 返回前增加落库）

```csharp
// 落库逻辑（在返回 SkillResult 之前）：
if (parsed.Value?.Fields.Count > 0)
{
    foreach (var field in parsed.Value.Fields)
    {
        await _db.Set<ExtractionResult>().AddAsync(new ExtractionResult
        {
            FileCode = context.Inputs.TryGetValue("file_code", out var fc) ? fc?.ToString() : null,
            RuleCode = context.Inputs.TryGetValue("rule_code", out var rc) ? rc?.ToString() : null,
            LabelTag = field.FieldCode,  // 对齐 F-02 label_tag 格式
            ExtractedValue = field.FieldValue?.ToString(),
            Confidence = field.Confidence,
            PositionInfo = field.PositionInfo != null ? JsonSerializer.Serialize(field.PositionInfo) : null,
            IsManualEdited = false,
            ExtractedAt = DateTime.Now
        }, ct);
    }
    await _db.SaveChangesAsync(ct);
}

if (parsed.Value?.Tables.Count > 0)
{
    foreach (var table in parsed.Value.Tables)
    {
        await _db.Set<TableExtractionResult>().AddAsync(new TableExtractionResult
        {
            FileCode = context.Inputs.TryGetValue("file_code", out var fc) ? fc?.ToString() : null,
            RuleCode = context.Inputs.TryGetValue("rule_code", out var rc) ? rc?.ToString() : null,
            TableIndex = tableIndex++,
            ExtractedJson = JsonSerializer.Serialize(table.Rows),
            Confidence = table.Confidence,
            PositionInfo = table.PositionInfo != null ? JsonSerializer.Serialize(table.PositionInfo) : null,
            ExtractedAt = DateTime.Now
        }, ct);
    }
    await _db.SaveChangesAsync(ct);
}
```

**实体字段确认**：`VOL.Entity/CertPlatform/Ent/ExtractionResult.cs` 和 `TableExtractionResult.cs` 需确认有 `FileCode`、`RuleCode`、`LabelTag`、`IsManualEdited` 字段（对照数据库表设计-V2 §B-08/B-09）。

**验证标准**：
- 调用 `LlmExtractSkill.ExecuteAsync`（MockProvider 返回固定 JSON）
- 查询 B-08：至少有一条记录，`confidence ∈ [0,1]`，`label_tag` 非空
- 查询 B-09：至少有一条记录，`extracted_json` 非空

---

#### E3：上传触发提取链路接入（1 天）

**目标**：文件上传完成后，自动入 `yzh_queue`（`queue_type=file_extract`），Worker 取出后执行 DocumentExtractSkill + LlmExtractSkill，落库 B-08/B-09。

**改动位置**：`VOL.Builder/Services/CertPlatform/DocExtractionRuleService.cs`（4 个私有方法改真）

```
文件上传完成
  ↓
DocExtractionRuleService.UploadAsync(file)
  ↓
入 yzh_queue（queue_type=file_extract，payload={fileCode, skill}）
  ↓
YzhQueueManager 调度 Worker
  ↓
DocumentExtractSkill（本地提取 → full_text + fields + tables）
  ↓
LlmExtractSkill（LLM 补全 + 结构化 → fields[] + tables[]）
  ↓
落 B-08/B-09（E2 实现的逻辑）
  ↓
通知前端（SignalR 或轮询 yzh_queue_task.status=completed）
```

**验证标准**：
- Postman 调 `/api/DocExtractionRule/upload` 上传一个 docx
- `yzh_queue` 出现一条 `file_extract` 类型记录
- `yzh_queue_task` 状态从 `pending → processing → completed`
- B-08 新增记录（`label_tag` 非空）
- 前端 DocExtractionRule 页面刷新后，`analysisFields` 有数据回显

---

### 3.3 Phase E 完成标志

```
[ ] dotnet build YZH.Core 无新增错误
[ ] dotnet test 全部通过（含新增 E1/E2/E3 集成测试）
[ ] Postman 可跑通：上传 docx → B-08/B-09 有真实提取记录
[ ] GetFieldSkill 查询 B-08 返回真实数据（不再返回"未找到"）
[ ] GetTableSkill 查询 B-09 返回真实数据
```

---

## 四、LogicFlow 工作流设计器实施计划（Phase F，P1，Phase E 完成后启动）

### 4.1 LogicFlow vs Vue Flow 最终决策

| 决策项 | 结论 | 依据 |
|--------|------|------|
| 前端设计器选型 | **LogicFlow v2.x** | `工作流引擎选型-V1.md` §5.3 定案；`总体设计-V3.md` V3.1 变更已同步 |
| 封装策略 | **独立模块 `workflow-designer`**，只暴露导入/导出 JSON 接口 | `工作流引擎选型-V1.md` §5.3 隔离策略 |
| 平替路径 | 维护断档时平替为 Vue Flow，业务页面无感知 | 封装隔离已保证 |
| PoC 强制前置 | **两条链路必须通过**后才可进入 Phase F 实施 | `工作流引擎选型-V1.md` §5.4 强制要求 |

### 4.2 Phase F 任务分解

#### F1：LogicFlow PoC（1 天，强制前置）

**PoC 一：自定义节点注册**

```
步骤：
1. npm install @logicflow/core@2.0.0 @logicflow/extension@2.0.0
2. 新建 src/components/workflow-designer/PoC1_SkillNode.vue
3. 注册节点：lf.register({ type: 'skill-node', model, view })
4. 验证：拖拽 SkillNode 到画布，显示 icon + 类型色标 + 端口（handle）

通过标准：
  ✅ 节点在画布中正常渲染
  ✅ 节点可拖拽移动
  ✅ 点击节点右侧弹出属性表单（inputs 可编辑）
```

**PoC 二：JSON 导出含端口语义**

```
步骤：
1. 在自定义 SkillNode 上挂载 sourceHandle/targetHandle
2. 连线并指定 handle 名称（如 n1.value → n3.dateA）
3. 调用 lf.getGraphData()，编写 compiler 转换为 workflow_config JSON
4. 验证：导出的 edges 含 sourceHandle/targetHandle，nodes 含 outputs 声明

通过标准：
  ✅ 导出 JSON 能被 WorkflowEngine.TopoSort 正确解析
  ✅ edges[].sourceHandle 对应上游节点 outputs 中的端口名
  ✅ edges[].targetHandle 对应下游节点 inputs 中的参数名
```

**PoC 失败任一链路**：立即切换 Vue Flow 重验（`workflow-designer` 模块封装已隔离，平替成本低）。

---

#### F2：workflow-designer 独立模块（3 天）

**目录结构**：

```
vol.web/src/components/workflow-designer/
├── WorkflowDesigner.vue          ← 画布容器（LogicFlow 实例 + Panel + 工具栏）
├── SkillPanel.vue                ← 左侧节点面板（读 F-01 skill 列表动态渲染）
├── NodePropertyForm.vue          ← 右侧属性表单（inputs/outputs 配置）
├── BranchConditionForm.vue       ← 条件分支配置（branches[].condition）
├── compiler.js                   ← 草稿态 → workflow_config 发布态编译器
├── decompiler.js                 ← workflow_config → 草稿态（用于编辑已有工作流）
├── schema-validator.js           ← 连线合法性校验（基于 F-01 input/output schema）
├── labels/TreeSelector.vue       ← F-02 字段标签树形选择器（嵌入 NodePropertyForm）
└── index.js                      ← 对外暴露：loadWorkflow(id)、exportWorkflow()
```

**compiler.js 核心逻辑示意**：

```javascript
/**
 * 将 LogicFlow getGraphData() 结果编译为 workflow_config（发布态）
 * 丢弃所有 UI 字段（x/y/样式），只保留可执行语义
 */
export function compileToWorkflowConfig(graphData) {
  const nodes = graphData.nodes.map(n => ({
    nodeId: n.id,
    skillCode: n.data.skillCode,
    config: n.data.config ?? {},
    inputs: n.data.inputs ?? {},
    outputs: n.data.outputs ?? {}  // 多端口字典（新结构，替代旧 output 单值）
  }))

  // 分离普通边和条件边（condition != null 的边 → branches）
  const normalEdges = []
  const branches = []

  for (const e of graphData.edges) {
    const condition = e.data?.condition ?? null
    if (condition) {
      // 条件边 → branches 数组
      branches.push({
        from: e.sourceNodeId,
        condition: condition,
        then: [{
          nodeId: e.targetNodeId,
          skillCode: findNodeSkill(e.targetNodeId, nodes),
          inputs: findNodeInputs(e.targetNodeId, nodes),
          outputs: findNodeOutputs(e.targetNodeId, nodes)
        }]
      })
    } else {
      normalEdges.push({
        source: e.sourceNodeId,
        target: e.targetNodeId,
        sourceHandle: e.data?.sourceHandle ?? null,
        targetHandle: e.data?.targetHandle ?? null
      })
    }
  }

  return {
    version: 1,
    workflowType: graphData.meta?.workflowType ?? 'validation',
    nodes,
    edges: normalEdges,
    branches,
    outputConfig: graphData.meta?.outputConfig ?? {}
  }
}
```

---

#### F3：后端 Service/Controller（2 天）

**新增 Partial 三件套**：

```
VOL.Builder/Services/CertPlatform/Partial/
├── WfSkillService.cs                        # F-01 CRUD（Skill 列表/详情/注册/注销）
├── WfWorkflowDefinitionService.cs           # F-03 CRUD + publish（编译+写入 workflow_config）
│                                              + test（运行一次取 sample_output 供沙箱验证）
└── WfFieldLabelMappingService.cs            # F-02 树形接口（供设计器标签选择器读取）

VOL.WebApi/Controllers/CertPlatform/Partial/
├── WfSkillController.cs                     # GET list / GET {id} / POST register / DELETE {id}
├── WfWorkflowDefinitionController.cs        # GET list（含 workflow_type 筛选）
│                                           # GET {id} / POST publish / POST test
└── WfFieldLabelMappingController.cs         # GET tree（树形 F-02 标签，供设计器读取）
```

**路由注册**（`viewGird.js` 新增）：

```javascript
// 工作流配置模块（独立菜单，不在 cert/Standard 下）
{
  path: '/CertPlatform/WorkflowDesigner',
  name: 'WorkflowDesigner',
  redirect: '/CertPlatform/WorkflowDesigner/List',
  meta: { title: '工作流配置' },
  children: [
    {
      path: 'List',
      name: 'WorkflowDefinitionList',
      component: () => import('@/views/cert/Standard/WorkflowDesigner/List.vue'),
      meta: { title: '工作流列表' }
    },
    {
      path: 'Designer/:id',
      name: 'WorkflowDesigner',
      component: () => import('@/views/cert/Standard/WorkflowDesigner/Designer.vue'),
      meta: { title: '工作流设计器' }
    }
  ]
}
```

---

#### F4：工作流配置前端页面（1.5 天）

**页面结构**：

```
views/cert/Standard/WorkflowDesigner/
├── List.vue              ← 列表页（view-grid 模式，参考 AuditTask.vue）
│                           列：workflow_code / workflow_name / workflow_type / version / is_active / actions
└── Designer.vue          ← 设计器页（嵌入 workflow-designer 组件）
    └── 依赖：@/components/workflow-designer/WorkflowDesigner.vue
```

**List.vue 关键交互**：
- 点击"新建"→ 跳转 `/CertPlatform/WorkflowDesigner/Designer/new`（新工作流）
- 点击"编辑"→ 跳转 `/CertPlatform/WorkflowDesigner/Designer/{id}`（加载已有 workflow_config 到设计器）
- 点击"测试"→ 调 `POST /api/WfWorkflowDefinition/test/{id}?businessType=validation&businessId=1`，返回 sample_output

---

#### F5：与 DocExtractionRule 页集成（0.5 天）

在 `views/cert/Standard/DocExtractionRule/index.vue` 增加"工作流编辑器"Tab：
- 当当前文件有绑定的 `workflow_id` 时，显示工作流图（只读预览）
- 点击"编辑工作流"→ 跳转 `/CertPlatform/WorkflowDesigner/Designer/{workflowId}`
- 保存后回写 `A-10 ValidationRule.workflow_id`（校验规则）或 `A-13 ClauseExtractionRule.workflow_id`（条款提取）

---

### 4.3 Phase F 完成标志

```
[ ] LogicFlow PoC 两条链路均通过
[ ] dotnet build VOL.WebApi 无新增错误
[ ] 前端 /CertPlatform/WorkflowDesigner 可正常访问
[ ] 能在设计器中拖拽 get_field 节点、配置 inputs.label_tag、连线、导出 workflow_config JSON
[ ] 导出的 JSON 能被 WorkflowEngine.RunAsync() 成功解析（至少线性管道）
```

---

## 五、数据节点可视化配置设计（核心需求响应）

### 5.1 问题重述

> "图形化地配置自定义数据节点：需要先选择对应的那个文档，勾选哪些字段、哪些表格，再调用其他 Skill 节点进行运算和组合"

这是工作流设计器的**核心交互场景**——审核员/配置员在设计器中编排工作流时，需要直观地配置每个数据节点的"数据源"。

### 5.2 设计期 vs 运行期的数据引用模型

```
设计期（配置工作流，存 F-03.workflow_config）：
  节点.inputs.label_tag = "[ISO9001_一监_管理评审记录_评审日期]"
  └─ 这是"模板"，不绑定具体企业

运行期（审核员执行审核，触发工作流）：
  WorkflowEngine.RunAsync(workflowConfig, {
    BusinessType: 'audit_task',
    BusinessId: auditTaskId,          // 关联到具体审核任务
    EnterpriseCode: enterpriseCode,   // 运行时注入的企业编码
    PhaseCode: phaseCode              // 运行时注入的阶段编码
  })
  └─ GetFieldSkill 查询 B-08 WHERE label_tag=? AND enterprise_code=?
```

**关键设计决策**：
- `label_tag` 是**设计期配置**（工作流模板级，配置一次多次复用）
- `enterprise_code`/`phase_code` 是**运行期注入**（每次执行自动带入，无需在设计器中配置）
- 设计器**不需要**知道"这是哪个企业的哪份文件"——工作流是标准配置，企业数据由审核任务上下文带入

### 5.3 设计器节点属性面板设计

```
┌──────────────────────────────────────────────────────────────────┐
│  📋 节点属性：get_field                                           │
│  Skill: get_field  │  类型: 输入  │  输出: value(string)         │
│  ─────────────────────────────────────────────────────────────── │
│                                                                   │
│  字段标签（F-02 树形选择器，多选）                                │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ 🔍 搜索标签...                                             │  │
│  ├────────────────────────────────────────────────────────────┤  │
│  │ 📁 [ISO9001_一监]                                          │  │
│  │   ├─ 📁 管理评审记录                                        │  │
│  │  │  ☑ 评审日期  [ISO9001_一监_管理评审记录_评审日期]        │  │
│  │  │  ☐ 评审结论  [ISO9001_一监_管理评审记录_评审结论]        │  │
│  │  │  ☐ 参与人员  [ISO9001_一监_管理评审记录_参与人员]        │  │
│  │  ├─ 📁 内审计划                                             │  │
│  │  │  ☑ 审核范围  [ISO9001_一监_内审计划_审核范围]            │  │
│  │  │  ☐ 计划日期  [ISO9001_一监_内审计划_计划日期]            │  │
│  │  └─ 📁 合规评价报告                                         │  │
│  │     ☐ 评价日期  [ISO9001_一监_合规评价报告_评价日期]        │  │
│  ├────────────────────────────────────────────────────────────┤  │
│  │ 已选 2 个标签                                              │  │
│  └────────────────────────────────────────────────────────────┘  │
│  [↑ 点击标签后自动写入 inputs.label_tags[] 数组]                 │
│                                                                   │
│  输出端口声明（自动推断，可手动修改）                              │
│  ├─ value      string    ✓（从 F-01 output_schema 推断）         │
│  └─ confidence number    ✓（从 F-01 output_schema 推断）         │
│                                                                   │
│  ⚙ 高级（可选）                                                   │
│  ├─ file_code  自由文本  [留空=运行时不限制文件]                  │
│  └─ is_manual_only  ☐ 仅读取人工复核过的提取值                    │
└──────────────────────────────────────────────────────────────────┘
```

### 5.4 get_table 节点同理

```
┌──────────────────────────────────────────────────────────────────┐
│  📋 节点属性：get_table                                           │
│  Skill: get_table  │  类型: 输入  │  输出: rows(array),           │
│                     │              confidence(number)             │
│  ─────────────────────────────────────────────────────────────── │
│                                                                   │
│  表格标签（F-02 树形选择器）                                      │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ 📁 [ISO9001_一监]                                          │  │
│  │   ├─ 📁 供应商评价表                                        │  │
│  │  │  ☑ 评价结果汇总  [ISO9001_一监_供应商评价表_评价结果汇总] │  │
│  │  └─ 📁 内审记录表                                         │  │
│  │     ☐ 检查项明细  [ISO9001_一监_内审记录表_检查项明细]       │  │
│  └────────────────────────────────────────────────────────────┘  │
│                                                                   │
│  table_index（可选，默认取最新一条）                               │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ 表格序号：[1]  （输入框，留空=取最近一条）                   │  │
│  └────────────────────────────────────────────────────────────┘  │
│                                                                   │
│  输出端口声明                                                     │
│  ├─ rows         array     ✓（JSON 序列化的表格数据）             │
│  ├─ table_code   string    ✓（表格编码）                         │
│  └─ confidence   number    ✓（AI 提取可信度）                    │
└──────────────────────────────────────────────────────────────────┘
```

### 5.5 节点连线与数据流可视化

```
画布上的数据流示意：

  [n1: get_field] ──value──▶ [n3: compare_date_diff]
  [n2: get_field] ──value──▶      ↑
                                 │
                    diff(months) │
                                 ▼
                    [n4: compare] ──is_violation──▶ [n5: create_nc]
                                         ↑
                              condition: equals(true)
                              （branches 条件边，视觉上用虚线或不同颜色标识）
```

**设计器连线规范**：
- 实线 = 普通数据流边（nodes/edges 数组）
- 虚线/色标边 = 条件分支边（branches 数组）
- 连线时实时校验：上游 output_schema 端口名必须存在于下游 input_schema

---

## 六、数据节点新增的运行时字段（影响 B-08/B-09 表结构）

### 6.1 需新增的列

```sql
-- B-08 ExtractionResult 需新增（用于运行时企业级过滤）
ALTER TABLE ent_extraction_result
  ADD COLUMN IF NOT EXISTS enterprise_code VARCHAR(50) NULL
    COMMENT '所属企业编码（运行时注入，用于过滤）',
  ADD COLUMN IF NOT EXISTS phase_code VARCHAR(20) NULL
    COMMENT '所属阶段编码（运行时注入，用于过滤）';

-- B-09 TableExtractionResult 同样新增
ALTER TABLE ent_table_extraction_result
  ADD COLUMN IF NOT EXISTS enterprise_code VARCHAR(50) NULL,
  ADD COLUMN IF NOT EXISTS phase_code VARCHAR(20) NULL;

-- 索引（高频查询）
CREATE INDEX IF NOT EXISTS idx_ent_ext_result_enterprise_phase
  ON ent_extraction_result(enterprise_code, phase_code, label_tag);
CREATE INDEX IF NOT EXISTS idx_ent_table_ext_result_enterprise_phase
  ON ent_table_extraction_result(enterprise_code, phase_code, table_code);
```

### 6.2 C# 实体同步修改

```csharp
// VOL.Entity/CertPlatform/Ent/ExtractionResult.cs
[Column("enterprise_code")]
public string? EnterpriseCode { get; set; }

[Column("phase_code")]
public string? PhaseCode { get; set; }

// VOL.Entity/CertPlatform/Ent/TableExtractionResult.cs
[Column("enterprise_code")]
public string? EnterpriseCode { get; set; }

[Column("phase_code")]
public string? PhaseCode { get; set; }
```

### 6.3 WorkflowContext 新增字段

```csharp
// YZH.Core/Workflow/WorkflowContext.cs
public class WorkflowContext
{
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public string BusinessType { get; set; } = "file_upload";
    public long BusinessId { get; set; }

    // 新增：运行时企业上下文（审核员执行时自动注入，无需设计器配置）
    public string? EnterpriseCode { get; set; }
    public string? PhaseCode { get; set; }

    public IDictionary<string, object> Inputs { get; set; } = new Dictionary<string, object>();
    public IExecutionLogStore? LogStore { get; set; }
}
```

### 6.4 GetFieldSkill 查询逻辑更新

```csharp
// YZH.Core/Skills/GetFieldSkill.cs
public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct = default)
{
    var labelTag = context.Inputs.TryGetValue("label_tag", out var lt) ? lt?.ToString() : string.Empty;
    var fileCode = context.Inputs.TryGetValue("file_code", out var fc) ? fc?.ToString() : string.Empty;

    if (string.IsNullOrWhiteSpace(labelTag))
        return new SkillResult { Success = false, Error = "get_field 需要 label_tag 入参" };

    var query = _db.Set<ExtractionResult>()
                   .Where(x => x.LabelTag == labelTag);

    // 运行时企业上下文过滤（来自 WorkflowContext.EnterpriseCode/PhaseCode）
    if (!string.IsNullOrWhiteSpace(context.EnterpriseCode))
        query = query.Where(x => x.EnterpriseCode == context.EnterpriseCode);
    if (!string.IsNullOrWhiteSpace(context.PhaseCode))
        query = query.Where(x => x.PhaseCode == context.PhaseCode);

    if (!string.IsNullOrWhiteSpace(fileCode))
        query = query.Where(x => x.FileCode == fileCode);

    var field = await query.OrderByDescending(x => x.ExtractedAt).FirstOrDefaultAsync(ct);
    // ... 同原逻辑
}
```

---

## 七、完整实施顺序与依赖图

```
Phase E（数据管道接通）                           Phase F（设计器）                    Phase G（引擎接入）
═══════════════════════════════════════          ════════════════════════════════════    ════════════════════════════════════

E1 DocumentExtractSkill 接真实 IFileExtractor    F1 LogicFlow PoC（两条链路）           G1 ValidationRule.workflow_id
       ↑                                           ↑ 依赖 F0 文档对齐                   → 触发校验引擎执行
E2 LlmExtractSkill 落 B-08/B-09                  ↓                                        G2 ReportTemplate.section_config
       ↑                                           F2 workflow-designer 模块              → 触发报告引擎执行
E3 上传触发提取链路接入（yzh_queue）               ↓                                        G3 端到端：审核→自动NC→报告
       ↑                                           F3 后端 Service/Controller
       └─ 完成后 B-08/B-09 有真实数据 ──────────────┼─────────────────────────────────────┘
                                                    F4 前端页面 + 路由
                                                    F5 与 DocExtractionRule 页集成
```

**总预估工时**：Phase E（2.5 天）+ Phase F（7.5 天）= **约 10 个工作日**（约 2 周）

---

## 八、准备清单

### 8.1 文档准备（Phase E 启动前完成）

| # | 文档 | 动作 | 归属目录 |
|---|------|------|---------|
| 1 | `数据库表设计-V2.md` §F-03 | 将 workflow_config JSON 示例更新为**新结构**（多端口 outputs + sourceHandle/targetHandle + branches） | `docs/20-架构决策/` |
| 2 | `YZH-AI引擎详细设计-V1.md` §4.2 | 同步 workflow_config 结构至新格式（camelCase 键名统一） | `docs/80-功能设计/01-系统管理/工作流管理/` → 完成后移入 `docs/80-功能设计/03-平台基础/` |
| 3 | 更新本文件（本文档） | 根据实际实施情况更新各 Phase 状态 | `docs/80-功能设计/01-系统管理/工作流管理/` |
| 4 | `docs/80-功能设计/README.md` | 新增本文档条目 | `docs/80-功能设计/` |
| 5 | `docs/00-工程体系/README.md` | 同步新增本文档 | `docs/00-工程体系/` |

### 8.2 数据库准备（Phase E 启动前执行）

```bash
# 1. 执行 B-08/B-09 新增列迁移
docker exec -i yzh-mysql mysql -uroot -pYzh123456. yzh_cert_platform \
  < src/server/Vue.NetCore/DB/mysql/cert_phase_workflow_node_context.sql

# 2. 确认 F-01 wf_skill 表有基础 Skill 数据（以下 8 条）
#    get_field / get_table / compare / date_diff
#    llm_judge / llm_generate / create_nc / assemble_text
#    如不存在，执行以下 SQL 插入
```

### 8.3 前端技术依赖（Phase F 启动前安装）

```bash
cd src/server/Vue.NetCore/vol.web
npm install @logicflow/core@2.0.0 @logicflow/extension@2.0.0
# 注：LogicFlow v2.x 原生支持 Vue 3；v1.x 仅支持 Vue 2
```

### 8.4 后端代码修改清单（Phase E）

| 文件 | 修改内容 |
|------|---------|
| `YZH.Core/Skills/DocumentExtractSkill.cs` | E1：接真实 `IFileExtractor` + MinIO Stream |
| `YZH.Core/Skills/LlmExtractSkill.cs` | E2：解析 LLM 输出后落 B-08/B-09 |
| `YZH.Core/Workflow/WorkflowContext.cs` | 新增 `EnterpriseCode`/`PhaseCode` 字段 |
| `YZH.Core/Skills/GetFieldSkill.cs` | 新增 enterprise_code/phase_code 过滤 |
| `YZH.Core/Skills/GetTableSkill.cs` | 同上 |
| `VOL.Entity/CertPlatform/Ent/ExtractionResult.cs` | 新增 `EnterpriseCode`/`PhaseCode` 属性 |
| `VOL.Entity/CertPlatform/Ent/TableExtractionResult.cs` | 同上 |
| `VOL.Builder/Services/CertPlatform/DocExtractionRuleService.cs` | E3：上传后入 yzh_queue |

### 8.5 后端新增文件清单（Phase F）

| 文件 | 职责 |
|------|------|
| `VOL.Builder/Services/CertPlatform/Partial/WfSkillService.cs` | F-01 CRUD |
| `VOL.Builder/Services/CertPlatform/Partial/WfWorkflowDefinitionService.cs` | F-03 CRUD + publish + test |
| `VOL.Builder/Services/CertPlatform/Partial/WfFieldLabelMappingService.cs` | F-02 树形接口 |
| `VOL.WebApi/Controllers/CertPlatform/Partial/WfSkillController.cs` | F-01 API |
| `VOL.WebApi/Controllers/CertPlatform/Partial/WfWorkflowDefinitionController.cs` | F-03 API |
| `VOL.WebApi/Controllers/CertPlatform/Partial/WfFieldLabelMappingController.cs` | F-02 API |

### 8.6 前端新增文件清单（Phase F）

| 文件 | 职责 |
|------|------|
| `src/components/workflow-designer/WorkflowDesigner.vue` | 画布容器（LogicFlow 实例） |
| `src/components/workflow-designer/SkillPanel.vue` | 左侧 Skill 节点面板 |
| `src/components/workflow-designer/NodePropertyForm.vue` | 右侧属性表单 |
| `src/components/workflow-designer/BranchConditionForm.vue` | 条件分支配置 |
| `src/components/workflow-designer/compiler.js` | 草稿态 → workflow_config 编译器 |
| `src/components/workflow-designer/decompiler.js` | workflow_config → 草稿态反编译器 |
| `src/components/workflow-designer/schema-validator.js` | 连线合法性校验 |
| `src/components/workflow-designer/labels/TreeSelector.vue` | F-02 标签树形选择器 |
| `src/views/cert/Standard/WorkflowDesigner/List.vue` | 工作流列表页 |
| `src/views/cert/Standard/WorkflowDesigner/Designer.vue` | 工作流设计器页 |

---

## 九、风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| **B-08/B-09 表结构需 ALTER** | Phase E 开始前需确认 DB 变更脚本幂等 | 所有 ALTER 语句加 `IF NOT EXISTS`，执行前备份 |
| **LogicFlow v2.x 兼容性** | 可能与现有 Element Plus / Vue 3 版本有冲突 | PoC 阶段立即验证，失败则平替 Vue Flow |
| **F-03 workflow_config 旧示例与新结构不一致** | 设计器编译器基于旧结构会导致解析失败 | Phase E0（文档对齐）必须优先完成，更新 `数据库表设计-V2.md` §F-03 |
| **企业上下文注入时机** | EnterpriseCode/PhaseCode 需在运行时从审核任务上下文取得 | 在 `AuditTaskService` 触发校验工作流时，从 `AuditTask.Phase.EnterpriseCode` 注入 |
| **多端口 outputs 设计器交互复杂度** | 每个节点需配置多个输出端口，比单值 output 复杂 | 设计器默认自动生成端口（从 F-01 output_schema 推断），手动编辑为高级功能 |

---

## 十、验收标准（全文档）

### Phase E 验收

```
[ ] B-08 有真实提取记录（Postman 上传 docx → 查 ent_extraction_result 有数据）
[ ] B-09 有真实表格记录（上传 xlsx → 查 ent_table_extraction_result 有数据）
[ ] GetFieldSkill 查询 B-08 返回真实 label_tag 对应值（不返回"未找到"）
[ ] GetTableSkill 查询 B-09 返回真实表格数据
[ ] WorkflowContext 新增 EnterpriseCode/PhaseCode 字段，编译无错误
[ ] dotnet build 0 错误，dotnet test 全部通过
```

### Phase F 验收

```
[ ] LogicFlow PoC 两条链路均通过（自定义节点注册 + JSON 导出含端口语义）
[ ] workflow-designer 独立模块可独立运行（import 到任意页面）
[ ] 设计器可加载 F-01 Skill 列表并渲染节点面板
[ ] 设计器可配置 get_field 节点的 inputs.label_tags（从 F-02 树形选择器选取）
[ ] 设计器可导出 workflow_config JSON（含 branches 条件边）
[ ] 导出的 JSON 能被 WorkflowEngine.RunAsync() 成功解析（线性管道 + 条件分支）
[ ] /CertPlatform/WorkflowDesigner 路由可访问，列表页和数据均正常
```

---

> **文档版本**：V1.0
> **创建时间**：2026-08-14
> **创建者**：Agnes（AI 编程助手）
> **状态**：成熟态——待 Phase E 启动
>
> **下一步行动**：
> 1. 执行 Phase E0（文档对齐：更新 `数据库表设计-V2.md` §F-03 新结构）
> 2. 执行 Phase E1-E3（数据提取管道接通）
> 3. Phase E 全部验收通过后，启动 Phase F（LogicFlow 设计器）

*（内容由AI生成，仅供参考）*
