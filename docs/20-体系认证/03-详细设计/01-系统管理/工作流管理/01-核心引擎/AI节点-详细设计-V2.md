---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9a16bac6e27d25132787d930f50d9879_1d078285953411f181ac525400f8a581
    ReservedCode1: y87/8Hf+ByLo/LqXat1eLnPbcgPvueC+sLSjwSlUykUsGigtX1vTdlRMbrTYxKcaTREzlSKmnX+LfYYSYLl1Chc1xWMGYFsncjyBo+Mwrc91lr74ylnMs4F9qhPDWYuIx9digqlpk/m937vRHmcu0k94h+3M9DB/SNIXreL6Uw4BX+o2pzkY9Ij3ieA=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9a16bac6e27d25132787d930f50d9879_1d078285953411f181ac525400f8a581
    ReservedCode2: y87/8Hf+ByLo/LqXat1eLnPbcgPvueC+sLSjwSlUykUsGigtX1vTdlRMbrTYxKcaTREzlSKmnX+LfYYSYLl1Chc1xWMGYFsncjyBo+Mwrc91lr74ylnMs4F9qhPDWYuIx9digqlpk/m937vRHmcu0k94h+3M9DB/SNIXreL6Uw4BX+o2pzkY9Ij3ieA=
---

# AI 节点 — 详细设计 V2

> **版本**：V2.0 | **日期**：2026-08-24 | **状态**：设计定稿
>
> **定位**：AI 节点的重构设计——基于**节点引用 + 隐式依赖**的新架构，替代 V1 的自定义参数模型。
>
> **V1 → V2 变更摘要**：
> - ❌ 移除 `customParams` 三来源模型（link/skill/constant）
> - ✅ 统一为**画布节点引用**：所有数据来自画布上的节点输出
> - ✅ 新增 **常量节点 (constant_node)** 解决固定值输入
> - ✅ 新增 **隐式依赖机制**：提示词中的引用自动参与拓扑排序
> - ✅ 提示词编辑器改造：可视化节点选择器
>
> **前置文档**：
> - `AI节点-详细设计-V1.md`（上一版，已废弃）
> - `工作流执行引擎-数据模型与接口设计-V3.md`
> - `工作流节点定义与属性抽象-V1.md`

---

## 目录

- [1. 核心设计思想](#1-核心设计思想)
- [2. 节点类型总览](#2-节点类型总览)
- [3. 常量/输入节点 (constant_node)](#3-常量输入节点-constant_node)
- [4. AI 节点新数据结构](#4-ai-节点新数据结构)
- [5. 隐式依赖机制（解决孤立节点问题）](#5-隐式依赖机制解决孤立节点问题)
- [6. 前端交互设计](#6-前端交互设计)
- [7. 执行流程](#7-执行流程)
- [8. 后端代码变更](#8-后端代码变更)
- [9. 迁移指南](#9-迁移指南)

---

## 1. 核心设计思想

### 1.1 一切皆节点

```
┌─────────────────────────────────────────────────────────────┐
│                    V2 节点生态                               │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌───────────┐  ┌───────────┐  ┌───────────┐              │
│  │ 业务节点   │  │ 方法节点   │  │ 常量节点   │              │
│  │           │  │           │  │           │              │
│  │ docField  │  │ get_field │  │ constant  │              │
│  │ tableExt  │  │ get_table │  │ input     │              │
│  └─────┬─────┘  └─────┬─────┘  └─────┬─────┘              │
│        │              │              │                     │
│        └──────────────┼──────────────┘                     │
│                       ▼                                    │
│               ┌───────────────┐                           │
│               │   AI 节点      │                           │
│               │  引用上述节点   │                           │
│               └───────────────┘                           │
│                                                             │
│  规则：所有节点的 output 都可被 AI 节点的 promptTemplate 引用 │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 引用语法

| 语法 | 说明 | 示例 |
|------|------|------|
| `{{别名.result}}` | 引用节点的默认输出端口 | `{{字段提取.result}}` |
| `{{别名.portName}}` | 引用节点的指定输出端口 | `{{字段提取.fieldValue}}` |

### 1.3 V1 vs V2 对比

| 维度 | V1（已废弃） | V2（当前） |
|------|-------------|-----------|
| 参数来源 | 3 种：link / skill / constant | **1 种：节点引用** |
| 常量处理 | sourceType=constant（嵌套在参数内） | **独立 constant_node 节点** |
| 方法调用 | sourceType=skill（嵌套配置） | **独立 skill_node 节点** |
| 提示词编辑 | 手写 `{{参数名}}` + 属性面板定义参数 | **点击📎选节点，自动插入** |
| 依赖关系 | 仅物理连线 | **物理连线 + 隐式引用（自动解析）** |
| 孤立节点 | 不存在（参数内嵌） | **通过 references 字段参与拓扑** |

---

## 2. 节点类型总览

### 2.1 节点分类表

| 分类 | nodeType | 说明 | 输出端口 |
|------|----------|------|---------|
| **业务节点** | `doc_field` | 文档字段提取 | result, confidence, sourceText |
| | `table_extract` | 表格数据提取 | result(rows), totalCount |
| | `file_read` | 文件内容读取 | result(content), fileName, fileSize |
| **方法节点** | `skill_get_field` | 获取已提取字段值 | result(fieldValue), confidence |
| | `skill_get_table` | 获取已提取表格数据 | result(rows), totalCount |
| | `skill_get_param` | 获取系统参数值 | result(value) |
| **常量节点** | `constant` | 固定常量值 | result(=config.value) |
| | `input` | 运行时用户输入 | result(=运行时输入) |
| **AI 节点** | `ai_node` | LLM 推理判断 | success, error, result |
| **控制节点** | `branch` | 条件分支 | — |
| | `loop` | 循环处理 | — |

### 2.2 节点视觉规范

| 类型 | 图标 | 颜色 | 形状 | 尺寸 |
|------|------|------|------|------|
| 业务节点 | 📄 | `#409EFF` 蓝 | 矩形 | 标准 |
| 方法节点 | 🔧 | `#9B59B6` 紫 | 圆角矩形 | 标准 |
| 常量节点 | 🔢 | `#909399` 灰 | 菱形 | 小（120×60） |
| 输入节点 | ✏️ | `#67C23A` 绿 | 菱形 | 小（120×60） |
| AI 节点 | 🤖 | `#E6A23C` 橙 | 大圆角矩形 | 大（240×140） |

---

## 3. 常量/输入节点 (constant_node)

### 3.1 设计目标

解决"用户需要输入固定值或运行时变量"的场景：

| 场景 | 示例 | 节点类型 |
|------|------|---------|
| 判断阈值 | 最低人数 `200` | `constant` |
| 版本号 | `ISO13485:2016` | `constant` |
| 审核备注 | 运行时手动填写 | `input` |
| 审核员姓名 | 从上下文获取 | `input` + ctx 绑定 |

### 3.2 数据结构

#### constant 节点

```json
{
  "nodeId": "const_n5",
  "nodeType": "constant",
  "title": "标准阈值",
  "alias": "标准阈值",
  "config": {
    "valueType": "number",
    "value": 200,
    "description": "企业最低人数要求"
  },
  "inputPorts": [],
  "outputPorts": [
    { "name": "result", "type": "number", "display": "visible" }
  ]
}
```

#### input 节点（运行时输入）

```json
{
  "nodeId": "input_n8",
  "nodeType": "input",
  "title": "审核员备注",
  "alias": "审核备注",
  "config": {
    "valueType": "string",
    "defaultValue": "",
    "placeholder": "请输入审核意见",
    "description": "审核员手动填写的备注",
    "required": false,
    "ctxBinding": ""  // 可选：绑定到上下文参数如 ctx.auditorName
  },
  "inputPorts": [],
  "outputPorts": [
    { "name": "result", "type": "string", "display": "visible" }
  ]
}
```

### 3.3 config 字段定义

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `valueType` | string | ✅ | 值类型：`string` / `number` / `boolean` / `json` |
| `value` | any | ⬚(constant) | 固定值（constant 节点必填） |
| `defaultValue` | any | ⬚(input) | 默认值（input 节点使用） |
| `placeholder` | string | ⬚ | 输入提示文字 |
| `description` | string | ⬚ | 节点描述（悬停显示） |
| `required` | boolean | ⬚ | 是否必填，默认 false |
| `ctxBinding` | string | ⬚ | 上下文绑定路径，如 `ctx.auditorName` |

### 3.4 执行逻辑

```csharp
// ConstantNodeExecutor / InputNodeExecutor
public async Task<NodeExecutionResult> ExecuteAsync(WorkflowNodeConfig node, ...)
{
    var config = node.Config;
    
    if (node.NodeType == "constant")
    {
        // 常量节点：直接返回 config.value
        var value = config["value"];
        return NodeExecutionResult.Ok(new Dictionary<string, object>
        {
            ["result"] = value
        });
    }
    else // input
    {
        // 输入节点：优先从 contextParams 取值，其次用 defaultValue
        var ctxBinding = config["ctxBinding"]?.ToString();
        object value;
        
        if (!string.IsNullOrEmpty(ctxBinding) && contextParams.TryGetValue(ctxBinding, out var ctxVal))
        {
            value = ctxVal;
        }
        else
        {
            value = config["defaultValue"] ?? "";
        }
        
        return NodeExecutionResult.Ok(new Dictionary<string, object>
        {
            ["result"] = value
        });
    }
}
```

---

## 4. AI 节点新数据结构

### 4.1 rule_json 中的 ai_node 配置（V2）

```json
{
  "nodeId": "ai_node_n6",
  "nodeType": "ai_node",
  "title": "AI 合规判断",
  "alias": "合规判断",
  "config": {
    "promptTemplate": "判断企业规模为{{字段提取.result}}人，是否满足{{标准阈值.result}}人的最低要求。结论只需回答 true 或 false。",
    "outputType": "boolean",
    "systemPrompt": "你是认证审核AI助手，只做合规判断，不输出解释。"
  },
  "references": [
    {
      "nodeId": "field_n3",
      "alias": "字段提取",
      "port": "result"
    },
    {
      "nodeId": "const_n5",
      "alias": "标准阈值",
      "port": "result"
    }
  ],
  "inputPorts": [],
  "outputPorts": [
    { "name": "success", "type": "boolean", "display": "visible" },
    { "name": "error", "type": "string", "display": "visible" },
    { "name": "result", "type": "boolean", "display": "visible" }
  ]
}
```

### 4.2 V2 vs V1 字段对比

| V1 字段 | V2 字段 | 变化说明 |
|---------|---------|---------|
| `customParams` | ~~移除~~ | 替换为 references |
| `inputs` | ~~移除~~ | 替换为 references |
| `inputTypes` | ~~移除~~ | 不再需要 |
| — | `references` | **新增**：节点引用列表 |
| `config.promptTemplate` | `config.promptTemplate` | 保留，语法不变 |
| `config.outputType` | `config.outputType` | 保留 |
| `config.systemPrompt` | `config.systemPrompt` | 保留 |

### 4.3 references 字段定义

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `nodeId` | string | ✅ | 被引用的节点 ID（画布上必须存在） |
| `alias` | string | ✅ | 节点别名（用于提示词中的 `{{别名.result}}`） |
| `port` | string | ⬚ | 输出端口名，默认 `"result"` |

**references 与 promptTemplate 的关系**：
- `promptTemplate` 中使用 `{{别名.port}}` 语法引用
- `references` 是结构化的引用声明（前端编辑时生成）
- 后端执行时两者必须一致（以 references 为准解析）

---

## 5. 隐式依赖机制（解决孤立节点问题）

### 5.1 问题定义

采用"节点引用"方案后，画布上会出现**没有物理连线但被 AI 节点引用**的节点：

```
     ┌──────────┐
     │ field_n3 │  ← 没有连线到 AI 节点
     │ 字段提取  │     但被 promptTemplate 引用
     └──────────┘
          :
          : 引用（虚线，非物理连线）
          :
     ┌──────────┐
     │ ai_n6    │
     │ AI判断   │
     └──────────┘
```

这些节点如果被当作"孤立节点"跳过执行，AI 节点将拿不到数据。

### 5.2 解决方案：隐式依赖图

**核心思路**：从 AI 节点的 `promptTemplate` / `references` 中解析出被引用的节点 ID，自动建立依赖边，纳入拓扑排序。

```
┌─────────────────────────────────────────────────────────────┐
│                    隐式依赖解析流程                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. 收集所有 AI 节点                                        │
│     ↓                                                       │
│  2. 解析每个 AI 节点的 references                            │
│     ↓                                                       │
│  3. 构建隐式依赖边：referencedNode → aiNode                 │
│     ↓                                                       │
│  4. 合并到主 DAG（物理连线边 ∪ 隐式引用边）                   │
│     ↓                                                       │
│  5. 拓扑排序 → 确保被引用节点先于 AI 节点执行                │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 5.3 数据结构

```csharp
/// <summary>
/// 隐式依赖边（从 promptTemplate/references 解析得出）
/// </summary>
public class ImplicitEdge
{
    public string FromNodeId { get; set; }  // 被引用的节点
    public string ToNodeId { get; set; }    // AI 节点
    public string Alias { get; set; }       // 引用别名
    public string Port { get; set; }        // 输出端口
    public EdgeType Type => EdgeType.Implicit;  // 区别于物理连线的 EdgeType.Explicit
}
```

### 5.4 前端渲染：虚线引用线

| 属性 | 物理连线（Explicit） | 引用线（Implicit） |
|------|---------------------|-------------------|
| 线型 | 实线 `———` | 虚线 `- - -` |
| 颜色 | `#409EFF` 蓝 | `#C0C4CC` 浅灰 |
| 粗细 | 2px | 1px |
| 可交互 | 可拖拽创建/删除 | 只读（自动生成） |
| 折叠 | 不可折叠 | 默认折叠，点击展开 |

**画布效果**：

```
默认状态（虚线折叠）：          展开状态（显示虚线）：

 ┌──────┐                      ┌──────┐
 │field │                      │field │
 │ _n3  │                      │_n3   │
 └──┬───┘                      └--:---┘
    │                             : (隐式引用)
    │  （无可见连线）               :
 ┌──▼───┐                      ┌-:----┐
 │ai_n6 │                      │ai_n6 │
 └──────┘                      └──────┘
```

### 5.5 后端拓扑排序集成

```csharp
/// <summary>
/// WorkflowInterpreter.BuildExecutionOrder() 改造点
/// </summary>
public List<string> BuildExecutionOrder(WorkflowConfig workflow)
{
    var explicitEdges = BuildExplicitEdges(workflow.Edges);  // 物理连线
    var implicitEdges = BuildImplicitEdges(workflow.Nodes);   // ← 新增：解析引用
    
    // 合并边集
    var allEdges = explicitEdges.Concat(implicitEdges).ToList();
    
    // 拓扑排序
    return TopologicalSort(workflow.Nodes.Select(n => n.NodeId), allEdges);
}

/// <summary>
/// 从所有 AI 节点的 references 中解析隐式依赖
/// </summary>
private List<ImplicitEdge> BuildImplicitEdges(List<WorkflowNodeConfig> nodes)
{
    var implicitEdges = new List<ImplicitEdge>();
    
    foreach (var node in nodes.Where(n => n.NodeType == "ai_node"))
    {
        var references = node.Config?["references"] as List<object>;
        if (references == null)
        {
            // 兜底：从 promptTemplate 正则解析
            references = ExtractReferencesFromTemplate(
                node.Config?["promptTemplate"]?.ToString());
        }
        
        foreach (var ref in references)
        {
            implicitEdges.Add(new ImplicitEdge
            {
                FromNodeId = ref.nodeId,
                ToNodeId = node.NodeId,
                Alias = ref.alias,
                Port = ref.port ?? "result"
            });
        }
    }
    
    return implicitEdges;
}
```

### 5.6 孤立节点检测（保存时校验）

```
真正孤立的节点 = 既没有出/入物理连线，也没有被任何节点引用

保存工作流时：
1. 计算所有节点集合 N
2. 计算有物理连线的节点 P
3. 计算 references 中被引用的节点 R
4. 孤立节点 = N - P - R
5. 如果孤立节点非空 → 弹出警告（不阻止保存）
```

---

## 6. 前端交互设计

### 6.1 提示词编辑器（核心改造）

```
┌─────────────────────────────────────────────────────────────┐
│ 🤖 AI 节点属性                                               │
├─────────────────────────────────────────────────────────────┤
│ 节点名称: [AI 合规判断                                 ]     │
│ 节点别名: [合规判断                                   ]     │
├─────────────────────────────────────────────────────────────┤
│ 📝 提示词模板                                   [+ 引用 ▾] │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ 判断企业规模为 {{字段提取.result}} 人，是否满足          │ │
│ │ {{标准阈值.result}} 人的最低要求。                       │ │
│ │ 结论只需回答 true 或 false。                             │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                             │
│ 🔗 已引用变量（点击 📎 可快速定位到源节点）：                  │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ 🟢 字段提取 (field_n3).result → "500人"         [✕]   │ │
│ │ 🔵 标准阈值 (const_n5).result → 200             [✕]   │ │
│ └─────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────┤
│ 🔧 输出配置                                                 │
│ 输出类型: [▾布尔    ]                                       │
├─────────────────────────────────────────────────────────────┤
│ [▶ 测试节点]                        [✓ 保存] [取消]         │
└─────────────────────────────────────────────────────────────┘
```

### 6.2 点击「+ 引用」的交互流程

```
用户点击 [+ 引用]
       │
       ▼
┌─────────────────────────────┐
│ 📎 选择数据源               │
│                             │
│ ── 画布节点 ──              │
│ ○ 字段提取 (field_n3)      │  ← 列出画布上所有可用节点
│ ○ 表格提取 (table_n4)      │
│ ● 标准阈值 (const_n5)      │  ← 选中
│ ○ 审核备注 (input_n8)      │
│                             │
│ ── 上下文变量 ──            │
│ ○ ctx.enterpriseCode       │
│ ○ ctx.phaseCode            │
│ ○ ctx.fileCode             │
│                             │
│ 输出端口: [▾ result     ]   │  ← 选择该节点的哪个输出
│                             │
│        [确定]  [取消]       │
└─────────────────────────────┘
       │
       ▼
自动在光标位置插入: {{标准阈值.result}}
同时更新 references 数组
```

### 6.3 节点选择器数据源

```typescript
// 前端：构建可选节点列表
function buildNodeOptions(currentWorkflow): SelectOption[] {
  const options: SelectOption[] = []
  
  for (const node of currentWorkflow.nodes) {
    // 排除自己（AI 节点不能引用自己）
    if (node.nodeId === currentAiNode.nodeId) continue
    
    // 排除下游节点（避免循环依赖）
    if (isDownstream(node.nodeId, currentAiNode.nodeId)) continue
    
    for (const port of node.outputPorts || []) {
      options.push({
        label: `${node.title || node.alias}.${port.name}`,
        value: `${node.alias}.${port.name}`,
        nodeId: node.nodeId,
        alias: node.alias,
        port: port.name,
        type: port.type,
        group: getNodeGroup(node.nodeType)  // 分组显示
      })
    }
  }
  
  return options
}

function getNodeGroup(nodeType: string): string {
  switch (nodeType) {
    case 'constant': case 'input': return '常量/输入'
    case 'skill_get_field': case 'skill_get_table': case 'skill_get_param': return '方法节点'
    case 'doc_field': case 'table_extract': case 'file_read': return '业务节点'
    default: return '其他'
  }
}
```

### 6.4 常量节点属性面板

```
┌─────────────────────────────────────────┐
│ 🔢 常量节点                              │
├─────────────────────────────────────────┤
│ 节点名称: [标准阈值                 ]   │
│ 节点别名: [标准阈值                 ]   │  ← 用于 {{别名.result}} 引用
├─────────────────────────────────────────┤
│ 值类型:  [▾ 数字    ]                    │
│                                         │
│ 常量值:  [200                      ]   │
│                                         │
│ 描述:    [企业最低人数要求          ]   │
├─────────────────────────────────────────┤
│                    [✓ 保存] [取消]      │
└─────────────────────────────────────────┘
```

---

## 7. 执行流程

### 7.1 总体流程（V2）

```
┌─────────────────────────────────────────────────────────────────┐
│                    AI 节点执行流程（V2）                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ① 解析 references                                              │
│     ├─ 遍历 references[] 列表                                   │
│     ├─ 对每条引用：从 sharedOutputs[nodeId][port] 取值            │
│     └─ 构建参数池 params = { "字段提取": "500人", "标准阈值": 200 }│
│                                                                  │
│  ② 渲染提示词模板                                                │
│     promptTemplate + params → 真实提示词                         │
│     "判断企业规模为500人，是否满足200人的最低要求..."              │
│                                                                  │
│  ③ 组装三段式提示词                                              │
│     system: config.systemPrompt                                  │
│     user: 渲染后的提示词                                         │
│                                                                  │
│  ④ 单次 LLM 调用                                                 │
│     ILlmClient.CompleteAsync() → response                        │
│                                                                  │
│  ⑤ 结果类型转换                                                  │
│     按 config.outputType 转换 response.Content                   │
│                                                                  │
│  ⑥ 输出包装                                                      │
│     { success, error, result }                                   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 7.2 参数解析伪代码（V2 简化版）

```csharp
async Task<Dictionary<string, object>> ResolveReferencesAsync(
    List<NodeReference> references,
    Dictionary<string, object> sharedOutputs,
    CancellationToken ct)
{
    var paramPool = new Dictionary<string, object>();
    
    foreach (var ref in references)
    {
        // 从共享输出中读取被引用节点的结果
        if (sharedOutputs.TryGetValue(ref.NodeId, out var nodeOutput))
        {
            object value;
            
            if (nodeOutput is Dictionary<string, object> dict)
            {
                // 指定端口
                value = dict.GetValueOrDefault(ref.Port ?? "result");
            }
            else
            {
                // 节点只有一个输出（直接取值）
                value = nodeOutput;
            }
            
            paramPool[ref.Alias] = value;
        }
        else
        {
            // 被引用节点尚未执行 → 这是拓扑排序 bug
            throw new InvalidOperationException(
                $"引用节点 {ref.NodeId}({ref.Alias}) 尚未执行，检查拓扑排序");
        }
    }
    
    return paramPool;
}
```

### 7.3 与 V1 的对比

| 步骤 | V1 | V2 |
|------|----|----|
| 参数解析 | 遍历 customParams，按 sourceType 分发 | 遍历 references，统一从 sharedOutputs 取值 |
| link 来源 | `sharedOutputs[nodeId][port]` | 同左 |
| skill 来源 | `_skillRegistry.ExecuteAsync()` | **不存在**（Skill 已是独立节点） |
| constant 来源 | 直接取 `sourceConfig.value` | **不存在**（常量已是独立节点） |
| 复杂度 | 高（3 种分支） | **低（统一 1 种逻辑）** |

---

## 8. 后端代码变更

### 8.1 新增文件

| 文件 | 说明 |
|------|------|
| `WorkflowEngine/Nodes/ConstantNodeExecutor.cs` | 常量节点执行器 |
| `WorkflowEngine/Nodes/InputNodeExecutor.cs` | 输入节点执行器 |
| `WorkflowEngine/Models/NodeReference.cs` | 引用数据模型 |
| `WorkflowEngine/Models/ImplicitEdge.cs` | 隐式依赖边模型 |

### 8.2 修改文件

| 文件 | 变更说明 |
|------|---------|
| `WorkflowInterpreter.cs` | BuildExecutionOrder() 加入隐式依赖解析 |
| `AiNodeExecutor.cs` | 移除 customParams 逻辑，改为解析 references |
| `WorkflowTestController.cs` | 测试接口适配新数据结构 |
| `NodeExecutor.cs` | 路由注册增加 `constant` / `input` 类型 |

### 8.3 ConstantNodeExecutor 完整实现

```csharp
namespace VOL.Builder.Services.CertPlatform.WorkflowEngine.Nodes
{
    /// <summary>
    /// 常量节点执行器：直接返回配置的固定值
    /// </summary>
    public class ConstantNodeExecutor : INodeExecutor
    {
        public async Task<NodeExecutionResult> ExecuteAsync(
            WorkflowNodeConfig node,
            string taskCode,
            string itemCode,
            Dictionary<string, object> inputs,
            Dictionary<string, object> contextParams,
            CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                var valueType = node.Config?.GetValueOrDefault("valueType")?.ToString() ?? "string";
                var rawValue = node.Config?["value"];
                
                // 类型转换
                var convertedValue = ConvertValueType(rawValue, valueType);
                
                sw.Stop();
                return NodeExecutionResult.Ok(new Dictionary<string, object>
                {
                    ["result"] = convertedValue
                }, (int)sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return NodeExecutionResult.Fail($"常量节点执行失败: {ex.Message}", (int)sw.ElapsedMilliseconds);
            }
        }
        
        private object ConvertValueType(object rawValue, string valueType)
        {
            if (rawValue == null) return null;
            
            return valueType?.ToLower() switch
            {
                "number" => Convert.ToDouble(rawValue),
                "boolean" => Convert.ToBoolean(rawValue),
                "integer" => Convert.ToInt32(rawValue),
                _ => rawValue.ToString()
            };
        }
    }
}
```

### 8.4 AiNodeExecutor 改造（关键差异）

```csharp
/// <summary>
/// AI 节点执行器 V2 —— 基于 references 的简化版
/// </summary>
public class AiNodeExecutor : INodeExecutor
{
    // ... 依赖注入同 V1 ...
    
    public async Task<NodeExecutionResult> ExecuteAsync(...)
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            // V2 核心：解析 references（替代 V1 的 customParams）
            var references = ParseReferences(node.Config);
            var paramPool = await ResolveReferencesAsync(references, sharedOutputs, ct);
            
            // 渲染提示词
            var template = node.Config?["promptTemplate"]?.ToString() ?? "";
            var renderedPrompt = RenderTemplate(template, paramPool);
            
            // system prompt
            var systemPrompt = node.Config?.GetValueOrDefault("systemPrompt")?.ToString()
                ?? "你是认证审核AI助手，只需输出结果，不要解释。";
            
            // output type
            var outputType = node.Config?.GetValueOrDefault("outputType")?.ToString() ?? "string";
            
            // LLM 调用
            var response = await _llm.CompleteAsync(new LlmRequest
            {
                Messages = new List<LlmMessage>
                {
                    new("system", systemPrompt),
                    new("user", renderedPrompt)
                },
                Temperature = 0.1,
                MaxTokens = 4096
            }, ct);
            
            if (!response.Success)
                throw new InvalidOperationException($"LLM 调用失败: {response.Error}");
            
            // 类型转换 + 包装
            var result = ConvertOutputType(response.Content, outputType);
            
            sw.Stop();
            return NodeExecutionResult.Ok(WrapResult(result), (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return NodeExecutionResult.Fail(ex.Message, (int)sw.ElapsedMilliseconds);
        }
    }
    
    /// <summary>
    /// 从 config.references 解析引用列表
    /// </summary>
    private List<NodeReference> ParseReferences(JsonElement config)
    {
        var result = new List<NodeReference>();
        
        if (config.TryGetProperty("references", out var refsElem) && refsElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var refElem in refsElem.EnumerateArray())
            {
                result.Add(new NodeReference
                {
                    NodeId = refElem.GetProperty("nodeId").GetString(),
                    Alias = refElem.GetProperty("alias").GetString(),
                    Port = refElem.GetProperty("port").GetString() ?? "result"
                });
            }
        }
        
        return result;
    }
}
```

---

## 9. 迁移指南

### 9.1 V1 → V2 数据迁移

| V1 数据 | 迁移规则 | V2 数据 |
|---------|---------|---------|
| `customParams[i].sourceType="link"` | → references 增加 `{ nodeId, alias: paramName, port }` | `references[]` |
| `customParams[i].sourceType="constant"` | → 创建新的 constant 节点 + references 引用它 | `constant_node` + `references[]` |
| `customParams[i].sourceType="skill"` | → 创建新的 skill_node + references 引用它 | `skill_node` + `references[]` |
| `inputs` | ~~删除~~ | ~~删除~~ |
| `inputTypes` | ~~删除~~ | ~~删除~~ |

### 9.2 自动迁移脚本（后端）

```csharp
/// <summary>
/// 将 V1 格式的 ai_node 迁移为 V2 格式
/// </summary>
public static WorkflowNodeConfig MigrateV1ToV2(WorkflowNodeConfig v1Node, WorkflowConfig workflow)
{
    var v2Node = JsonSerializer.Deserialize<WorkflowNodeConfig>(
        JsonSerializer.Serialize(v1Node));  // 深拷贝
    
    var customParams = v1Node.Config?["customParams"] as List<object>;
    if (customParams == null || customParams.Count == 0) return v2Node;
    
    var references = new List<object>();
    var newNodes = new List<WorkflowNodeConfig>();  // 需要新增的常量/方法节点
    
    int extraIndex = workflow.Nodes.Count;
    
    foreach (var param in customParams)
    {
        var sourceType = param.sourceType;
        var paramName = param.paramName;
        
        if (sourceType == "link")
        {
            // 直接转换为引用
            references.Add(new
            {
                nodeId = param.sourceConfig.nodeId,
                alias = paramName,
                port = param.sourceConfig.portName ?? "result"
            });
        }
        else if (sourceType == "constant")
        {
            // 创建常量节点
            var constNodeId = $"const_auto_{extraIndex++}";
            var constNode = CreateConstantNode(constNodeId, paramName, param.sourceConfig.value);
            newNodes.Add(constNode);
            
            references.Add(new { nodeId = constNodeId, alias = paramName, port = "result" });
        }
        else if (sourceType == "skill")
        {
            // TODO: 创建方法节点（后续迭代）
            // 当前降级处理：转为常量警告
            Console.WriteLine($"[Migration] Warning: skill type param '{paramName}' needs manual migration");
        }
    }
    
    // 更新 AI 节点
    v2Node.Config["references"] = references;
    ((IDictionary<string, object>)v2Node.Config).Remove("customParams");
    ((IDictionary<string, object>)v2Node.Config).Remove("inputs");
    ((IDictionary<string, object>)v2Node.Config).Remove("inputTypes");
    
    // 将新节点加入工作流
    workflow.Nodes.AddRange(newNodes);
    
    return v2Node;
}
```

### 9.3 前端兼容策略

| 场景 | 处理方式 |
|------|---------|
| 打开旧格式工作流 | 自动触发迁移弹窗："检测到 V1 格式，是否升级为 V2？" |
| 保存时检测 | 如果同时存在 customParams 和 references → 提示冲突 |
| API 兼容 | 后端同时支持两套格式一个版本，V1 标记 deprecated |

---

## 附录 A：references 完整示例

### 场景：文档合规性检查

**画布布局**：

```
┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐
│ 文档读取  │  │ 字段提取  │  │ 标准阈值  │  │ 版本号    │
│ file_n1  │→ │ field_n3 │  │ const_n5 │  │ const_n6 │
└──────────┘  └──────────┘  └──────────┘  └──────────┘
                              │             │
                              : 引用        : 引用
                              :             :
                    ┌─────────┴─────────────┴──┐
                    │        AI 合规判断        │
                    │         ai_n7            │
                    └──────────────────────────┘
```

**ai_n7 的完整配置**：

```json
{
  "nodeId": "ai_n7",
  "nodeType": "ai_node",
  "title": "AI 合规判断",
  "alias": "合规判断",
  "config": {
    "promptTemplate": "根据以下信息判断文档是否符合 {{版本号.result}} 标准：\n\n企业规模：{{字段提取.result}}\n最低人数要求：{{标准阈值.result}} 人\n\n请给出合规性结论。",
    "outputType": "json",
    "systemPrompt": "你是 ISO 体系认证审核 AI。返回 JSON：{ \"pass\": true/false, \"reason\": \"...\", \"items\": [...] }"
  },
  "references": [
    { "nodeId": "field_n3", "alias": "字段提取", "port": "result" },
    { "nodeId": "const_n5", "alias": "标准阈值", "port": "result" },
    { "nodeId": "const_n6", "alias": "版本号", "port": "result" }
  ],
  "inputPorts": [],
  "outputPorts": [
    { "name": "success", "type": "boolean" },
    { "name": "error", "type": "string" },
    { "name": "result", "type": "json" }
  ]
}
```

**执行时的参数池**：

```json
{
  "字段提取": "企业现有员工 500 人，其中质检人员 30 人",
  "标准阈值": 200,
  "版本号": "ISO13485:2016"
}
```

**渲染后的提示词**：

```
根据以下信息判断文档符合 ISO13485:2016 标准：

企业规模：企业现有员工 500 人，其中质检人员 30 人
最低人数要求：200 人

请给出合规性结论。
```

---

## 附录 B：虚线引用线渲染规格

| 属性 | 值 | CSS/SVG |
|------|-----|---------|
| stroke | `#C0C4CC` | `stroke="#C0C4CC"` |
| strokeWidth | 1 | `stroke-width="1"` |
| strokeDasharray | `5,5` | `stroke-dasharray="5,5"` |
| opacity | 0.6（默认）/ 1.0（hover） | `opacity="0.6"` |
| 动画 | 无 | — |
| 交互 | hover 时高亮 + 显示 tooltip | `@mouseenter` / `@mouseleave` |
| tooltip 内容 | `{{别名}} → nodeId` | 自定义 popover |

---

*（文档结束。本文档替代 V1，作为 AI 节点实现的唯一权威依据。）*
