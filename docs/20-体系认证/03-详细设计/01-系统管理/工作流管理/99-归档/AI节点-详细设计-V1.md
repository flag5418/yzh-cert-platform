---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9a16bac6e27d25132787d930f50d9879_1d078285953411f181ac525400f8a581
    ReservedCode1: y87/8Hf+ByLo/LqXat1eLnPbcgPvueC+sLSjwSlUykUsGigtX1vTdlRMbrtYxKcaTREzlSKmnX+LfYYSYLl1Chc1xWMGYFsncjyBo+Mwrc91lr74ylnMs4F9qhPDWYuIx9digqlpk/m937vRHmcu0k94h+3M9DB/SNIXreL6Uw4BX+o2pzkY9Ij3ieA=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9a16bac6e27d25132787d930f50d9879_1d078285953411f181ac525400f8a581
    ReservedCode2: y87/8Hf+ByLo/LqXat1eLnPbcgPvueC+sLSjwSlUykUsGigtX1vTdlRMbrtYxKcaTREzlSKmnX+LfYYSYLl1Chc1xWMGYFsncjyBo+Mwrc91lr74ylnMs4F9qhPDWYuIx9digqlpk/m937vRHmcu0k94h+3M9DB/SNIXreL6Uw4BX+o2pzkY9Ij3ieA=
---

# AI 节点 — 详细设计 V1

> **版本**：V1.0 | **日期**：2026-08-24 | **状态**：设计定稿
>
> **定位**：AI 节点（ai_node）的完整技术设计——前后端数据契约、执行流程、测试接口、前端交互，作为实现的唯一权威依据。
>
> **前置文档**：
> - `AI提示词规则-功能设计-V1.md`：提示词占位符、三段式结构、输出契约
> - `工作流执行引擎-数据模型与接口设计-V3.md`：节点配置的数据模型基础
> - `工作流节点定义与属性抽象-V1.md`：节点元数据定义
> - `Skill清单-V1.md`：Skill 体系总则

---

## 目录

- [1. 设计目标与原则](#1-设计目标与原则)
- [2. 自定义参数模型](#2-自定义参数模型)
- [3. 节点配置数据结构](#3-节点配置数据结构)
- [4. 执行流程](#4-执行流程)
- [5. 方法预执行机制](#5-方法预执行机制)
- [6. 循环处理机制](#6-循环处理机制)
- [7. 测试接口设计](#7-测试接口设计)
- [8. 前端属性面板设计](#8-前端属性面板设计)
- [9. 后端代码骨架](#9-后端代码骨架)
- [10. 日志与监控](#10-日志与监控)

---

## 1. 设计目标与原则

### 1.1 核心目标

| 目标 | 说明 |
|------|------|
| **零代码操作** | 通过可视化下拉选择组装提示词，无需手写占位符语法 |
| **预计算确定性** | 数据获取型方法由后端预执行，不依赖 AI，保证 100% 准确 |
| **成本可控** | AI 只负责推理判断，不参与流程编排，单次调用完成 |
| **单节点可测试** | 支持独立测试，需传入完整工作流上下文 |

### 1.2 核心原则

1. **自定义参数 = 变量声明**：用户定义的每个参数都是变量，有名称、来源、值
2. **方法 ≠ 提示词拼接**：方法说明注入 system prompt，由 AI 决定是否调用（Agent 能力预留）
3. **模板替换在 LLM 调用之前**：所有可预计算的值先替换，再驱动 AI
4. **输出自动包装**：无论 AI 返回什么，最终包装为 `{ success, error, result }`

---

## 2. 自定义参数模型

### 2.1 参数来源分类

```
┌─────────────────────────────────────────────────────────────┐
│                    自定义参数来源                              │
├─────────────────┬─────────────────┬─────────────────────────┤
│  节点结果引用    │  方法调用        │  常量输入               │
│  (link)         │  (skill)        │  (constant)             │
├─────────────────┼─────────────────┼─────────────────────────┤
│ 从上游已执行     │ 后端预执行 Skill │ 用户手动输入            │
│ 节点读取输出     │ 结果放入参数池   │ 文本/数字/布尔/日期     │
└─────────────────┴─────────────────┴─────────────────────────┘
```

### 2.2 参数数据结构

```json
{
  "paramName": "企业规模",
  "paramType": "string",
  "sourceType": "link|skill|constant",
  
  // sourceType = link 时
  "sourceConfig": {
    "nodeId": "get_field_n3",
    "portName": "fieldValue"
  },
  
  // sourceType = skill 时
  "sourceConfig": {
    "skillCode": "get_field",
    "skillParams": {
      "field_code": "constant:HR_COUNT",
      "enterprise_code": "ctx.enterpriseCode",
      "file_code": {
        "nodeId": "start_n1",
        "portName": "fileCode"
      }
    },
    "skillParamsTypes": {
      "field_code": "constant",
      "enterprise_code": "link",
      "file_code": "link"
    }
  },
  
  // sourceType = constant 时
  "sourceConfig": {
    "value": "100"
  }
}
```

### 2.3 字段定义

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `paramName` | string | ✅ | 参数名（提示词中引用标识，同一 AI 节点内唯一） |
| `paramType` | string | ✅ | 参数类型：`string` / `number` / `boolean` / `date` / `json` |
| `sourceType` | string | ✅ | 来源类型：`link`（节点结果）/ `skill`（方法调用）/ `constant`（常量） |
| `sourceConfig` | object | ✅ | 来源配置（按 sourceType 结构不同） |

**sourceConfig 子字段（sourceType=link）**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `nodeId` | string | 上游节点 ID |
| `portName` | string | 上游节点输出端口名 |

**sourceConfig 子字段（sourceType=skill）**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `skillCode` | string | Skill 编码（如 `get_field`） |
| `skillParams` | object | Skill 参数映射（paramName → 值） |
| `skillParamsTypes` | object | Skill 参数类型（paramName → "constant"/"link"/"ctx"） |

**sourceConfig 子字段（sourceType=constant）**：

| 字段 | 类型 | 说明 |
|------|------|------|
| `value` | any | 常量值 |

---

## 3. 节点配置数据结构

### 3.1 rule_json 中的 ai_node 配置

```json
{
  "nodeId": "ai_node_n6",
  "nodeType": "ai_node",
  "title": "AI 合规判断",
  "config": {
    "promptTemplate": "判断企业规模为{{企业规模}}人，是否满足{{标准规模}}人的最低要求。结论只需回答 true 或 false。",
    "outputType": "boolean",
    "systemPrompt": "你是认证审核AI助手，只做合规判断，不输出解释。"
  },
  "inputPorts": [
    { "name": "企业规模", "type": "string", "required": true },
    { "name": "标准规模", "type": "number", "required": true },
    { "name": "判断结果", "type": "boolean", "required": false }
  ],
  "outputPorts": [
    { "name": "success", "type": "boolean", "display": "visible" },
    { "name": "error", "type": "string", "display": "visible" },
    { "name": "result", "type": "boolean", "display": "visible" }
  ],
  "inputs": {
    "企业规模": "link:get_field_n3.fieldValue",
    "标准规模": "constant:200",
    "判断结果": ""
  },
  "inputTypes": {
    "企业规模": "link",
    "标准规模": "constant",
    "判断结果": "constant"
  },
  "customParams": [
    {
      "paramName": "企业规模",
      "paramType": "string",
      "sourceType": "link",
      "sourceConfig": {
        "nodeId": "get_field_n3",
        "portName": "fieldValue"
      }
    },
    {
      "paramName": "标准规模",
      "paramType": "number",
      "sourceType": "skill",
      "sourceConfig": {
        "skillCode": "get_param",
        "skillParams": {
          "param_code": "constant:MIN_COMPANY_SIZE"
        },
        "skillParamsTypes": {
          "param_code": "constant"
        }
      }
    }
  ]
}
```

### 3.2 config 字段定义

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `promptTemplate` | string | ✅ | 提示词模板，使用 `{{参数名}}` 引用自定义参数 |
| `outputType` | string | ✅ | 输出类型：`string` / `number` / `boolean` / `date` / `json` |
| `systemPrompt` | string | ⬚ | 自定义系统提示词（可选） |

> **后端默认值**：`model=qwen-turbo`、`temperature=0.1`、`maxTokens=4096` 由后端硬编码，前端不暴露。

### 3.3 customParams 与 inputs 的关系

| 字段 | 用途 | 谁消费 |
|------|------|--------|
| `customParams` | 前端编辑时的结构化声明 | 前端属性面板 |
| `inputs` | 后端执行时的扁平映射 | 后端执行器 |
| `inputPorts` | 端口声明（UI + 校验） | 前端 + 后端 |

**转换规则**：
```
customParams → 前端保存时编译 → inputs + inputTypes

编译逻辑：
  sourceType=link     → inputs[paramName] = "link:nodeId.portName" → inputTypes[paramName] = "link"
  sourceType=constant → inputs[paramName] = "constant:value"       → inputTypes[paramName] = "constant"
  sourceType=skill    → inputs[paramName] = "skill:skillCode"      → inputTypes[paramName] = "skill"
```

---

## 4. 执行流程

### 4.1 总体流程

```
┌─────────────────────────────────────────────────────────────────┐
│                    AI 节点执行流程                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ① 解析自定义参数                                                │
│     ├─ sourceType=link     → 从 sharedOutputs 读取              │
│     ├─ sourceType=skill    → 后端预执行 Skill                    │
│     └─ sourceType=constant → 直接使用常量值                      │
│                                                                  │
│  ② 构建参数池                                                    │
│     params = { "企业规模": "500人", "标准规模": 200 }             │
│                                                                  │
│  ③ 渲染提示词模板                                                │
│     promptTemplate + params → 真实提示词                         │
│     "判断企业规模为500人，是否满足200人的最低要求..."              │
│                                                                  │
│  ④ 组装三段式提示词                                              │
│     system: config.systemPrompt 或默认                           │
│     user:   渲染后的提示词                                        │
│                                                                  │
│  ⑤ 单次 LLM 调用                                                 │
│     ILlmClient.CompleteAsync() → response                        │
│                                                                  │
│  ⑥ 结果类型转换                                                  │
│     按 config.outputType 转换 response.Content                   │
│                                                                  │
│  ⑦ 输出包装                                                      │
│     { success: true, error: null, result: <转换后的值> }          │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 详细伪代码

```csharp
async Task<Dictionary<string, object>> ExecuteAiNodeAsync(
    WorkflowNodeConfig node, string taskCode, string itemCode,
    Dictionary<string, object> sharedOutputs,
    Dictionary<string, object> contextParams, CancellationToken ct)
{
    var config = node.Config;
    var template = config["promptTemplate"]?.ToString();
    var outputType = config.GetValueOrDefault("outputType")?.ToString() ?? "string";
    
    // ① 解析自定义参数
    var customParams = ParseCustomParams(node.Config["customParams"]);
    var paramPool = new Dictionary<string, object>();
    
    foreach (var param in customParams)
    {
        var value = await ResolveParamAsync(param, sharedOutputs, contextParams, ct);
        paramPool[param.ParamName] = value;
    }
    
    // ② 渲染提示词
    var renderedPrompt = RenderTemplate(template, paramPool);
    
    // ③ 构建 system prompt
    var systemPrompt = config.GetValueOrDefault("systemPrompt")?.ToString() 
        ?? "你是认证审核AI助手，只需输出结果，不要解释。";
    
    // ④ LLM 调用
    var response = await _llm.CompleteAsync(new LlmRequest
    {
        Messages = new List<LlmMessage>
        {
            new("system", systemPrompt),
            new("user", renderedPrompt)
        },
        Temperature = config.TryGetValue("temperature", out var t) ? Convert.ToDouble(t) : 0.1,
        MaxTokens = 4096
    }, ct);
    
    if (!response.Success)
        throw new InvalidOperationException($"LLM 调用失败: {response.Error}");
    
    // ⑤ 类型转换
    var result = ConvertOutputType(response.Content, outputType);
    
    // ⑥ 包装输出
    return new Dictionary<string, object>
    {
        ["success"] = true,
        ["error"] = null!,
        ["result"] = result
    };
}
```

### 4.3 参数解析详解

```csharp
async Task<object> ResolveParamAsync(CustomParam param, 
    Dictionary<string, object> sharedOutputs,
    Dictionary<string, object> contextParams, CancellationToken ct)
{
    switch (param.SourceType)
    {
        case "link":
            // 从已执行节点输出读取
            var nodeId = param.SourceConfig["nodeId"]?.ToString();
            var portName = param.SourceConfig["portName"]?.ToString();
            if (sharedOutputs.TryGetValue(nodeId, out var nodeOutput))
            {
                if (nodeOutput is Dictionary<string, object> dict)
                    return dict.GetValueOrDefault(portName);
            }
            return null;
            
        case "skill":
            // 预执行 Skill
            var skillCode = param.SourceConfig["skillCode"]?.ToString();
            var skillParams = ParseSkillParams(param.SourceConfig["skillParams"], 
                param.SourceConfig["skillParamsTypes"], sharedOutputs, contextParams);
            var skillContext = new SkillContext { Inputs = skillParams };
            var skillResult = await _skillRegistry.ExecuteAsync(skillCode, skillContext, ct);
            return skillResult.Outputs.GetValueOrDefault("result");
            
        case "constant":
            // 直接使用常量
            return param.SourceConfig["value"];
            
        default:
            throw new NotSupportedException($"不支持的参数来源类型: {param.SourceType}");
    }
}
```

---

## 5. 方法预执行机制

### 5.1 思想

> **数据获取型 Skill 由后端预执行，不交给 AI**

| Skill 类型 | 执行方 | 说明 |
|-----------|--------|------|
| `get_field`（数据获取） | 后端预执行 | 直接查数据库，100% 准确 |
| `get_table`（数据获取） | 后端预执行 | 同上 |
| `get_param`（数据获取） | 后端预执行 | 查询系统参数 |
| `ai_judge`（推理判断） | AI 自主调用 | 预留：需 LLM 推理 |

### 5.2 方法说明注入（Agent 能力预留）

当前版本所有方法都是预执行，但设计上预留"AI 自主调用"的扩展点。

**system prompt 注入格式**（预留，本期不开启）：

```
你可以使用以下方法获取额外数据：
- get_field(field_code, enterprise_code): 获取已提取的字段值，返回 { field_value: string, confidence: number }
- get_table(table_code, enterprise_code): 获取已提取的表格数据，返回 { rows: array }

如果需要调用，请以 JSON 格式返回：
{ "tool_call": { "name": "get_field", "args": { "field_code": "xxx" } } }
```

---

## 6. 循环处理机制

### 6.1 场景说明

针对表格数据（如人员花名册、设备清单），需要对每行执行相同的判断逻辑。

### 6.2 实现方式

```
自定义参数配置：
┌──────────┬───────────┬──────────────┐
│ 参数名    │ 来源类型   │ 值           │
├──────────┼───────────┼──────────────┤
│ 表格数据  │ skill     │ get_table    │
│ (loopSource=true)        │              │
└──────────┴───────────┴──────────────┘

提示词模板（启用循环）：
"判断以下每行人员的 age 是否 >= 18，返回 JSON 数组：
{{表格数据}}
格式：[{{ \"row_index\": 0, \"pass\": true, \"reason\": \"...\" }}]"
```

### 6.3 循环配置

```json
{
  "config": {
    "loopConfig": {
      "enabled": true,
      "sourceParam": "表格数据",
      "batchSize": 10,
      "maxIterations": 100,
      "aggregation": "array"
    }
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `enabled` | boolean | 是否启用循环 |
| `sourceParam` | string | 来源于哪个自定义参数（该参数值必须是数组） |
| `batchSize` | number | 每批处理的行数（控制 prompt 长度） |
| `maxIterations` | number | 最大迭代次数（安全限制） |
| `aggregation` | string | 结果汇总方式：`array`（数组合并）/ `merge`（对象合并） |

---

## 7. 测试接口设计

### 7.1 为什么需要完整工作流上下文

AI 节点的自定义参数可能引用上游节点的输出（如 `get_field_n3.fieldValue`）。测试单个 AI 节点时，无法直接从数据库获取这些值（因为不是正式执行任务）。

**解决方案**：测试时传入完整的 `rule_json`，后端模拟执行，向上游节点注入模拟数据或真实缓存数据。

### 7.2 测试接口

```
POST /api/workflow/test/ai-node
```

**请求体**：

```json
{
  "nodeId": "ai_node_n6",
  "nodeType": "ai_node",
  "title": "AI 合规判断",
  "config": {
    "promptTemplate": "判断企业规模为{{企业规模}}人，是否满足最低要求。",
    "outputType": "boolean"
  },
  "inputPorts": [...],
  "outputPorts": [...],
  "inputs": {
    "企业规模": "get_field_n3.fieldValue"
  },
  "inputTypes": {
    "企业规模": "link"
  },
  "customParams": [...],
  
  // 关键：传入完整工作流上下文
  "workflowContext": {
    "ruleJson": "{完整的 rule_json}",
    "contextParams": {
      "enterpriseCode": "ENT_001",
      "phaseCode": "PHASE_01",
      "standardCode": "ISO9001"
    },
    "mockOutputs": {
      "get_field_n3": {
        "fieldValue": "500人",
        "confidence": 0.95
      }
    }
  }
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `nodeId` | string | ✅ | 当前 AI 节点 ID |
| `nodeType` | string | ✅ | `"ai_node"` |
| `config` | object | ✅ | 节点配置 |
| `customParams` | array | ✅ | 自定义参数列表 |
| `workflowContext` | object | ✅ | 工作流上下文 |
| `workflowContext.ruleJson` | string | ✅ | 完整的 rule_json（含所有节点配置） |
| `workflowContext.contextParams` | object | ✅ | 运行时上下文参数 |
| `workflowContext.mockOutputs` | object | ⬚ | 模拟的上游节点输出（nodeId → output） |

### 7.3 响应体

```json
{
  "success": true,
  "data": {
    "success": true,
    "error": null,
    "result": true,
    "durationMs": 1234,
    "debug": {
      "renderedPrompt": "判断企业规模为500人，是否满足最低要求。",
      "paramPool": {
        "企业规模": "500人"
      },
      "llmResponse": "true",
      "convertedResult": true
    }
  }
}
```

**debug 字段说明**（方便前端调试）：

| 字段 | 类型 | 说明 |
|------|------|------|
| `renderedPrompt` | string | 替换后的真实提示词 |
| `paramPool` | object | 参数池（参数名 → 值） |
| `llmResponse` | string | LLM 原始返回 |
| `convertedResult` | any | 按 outputType 转换后的结果 |

### 7.4 后端测试逻辑

```csharp
[HttpPost("test/ai-node")]
public async Task<IActionResult> TestAiNode([FromBody] AiNodeTestRequest request, CancellationToken ct)
{
    // 1. 解析完整 ruleJson
    var workflowConfig = JsonSerializer.Deserialize<WorkflowConfig>(request.WorkflowContext.RuleJson);
    
    // 2. 找到当前 AI 节点在 ruleJson 中的位置
    var currentNode = workflowConfig.Nodes.First(n => n.NodeId == request.NodeId);
    
    // 3. 优先使用 mockOutputs，其次尝试从缓存/数据库加载真实数据
    var sharedOutputs = request.WorkflowContext.MockOutputs ?? new();
    
    // 4. 解析链路：递归解析上游节点输出（如果需要）
    //    - 如果 mockOutputs 中没有上游节点的值
    //    - 尝试模拟执行上游节点（仅针对预计算型 Skill）
    
    // 5. 创建 AI 节点执行器
    var aiExecutor = new AiNodeExecutor(_llm, _skillRegistry, _promptInterpreter, _logger);
    
    // 6. 执行
    var result = await aiExecutor.ExecuteAsync(
        currentNode, "TEST", "AI_NODE_TEST", 
        sharedOutputs, request.WorkflowContext.ContextParams, ct);
    
    // 7. 返回（带 debug 信息）
    return Ok(new { success = true, data = result });
}
```

---

## 8. 前端属性面板设计

### 8.1 设计理念：参数节点化

> **核心洞察**：自定义参数 = 隐形节点。每个参数与普通节点结构完全一致（nodeId/title/inputs/inputTypes），只是不连线、半透明显示在画布中。

```
普通节点 (docField_n3)          参数节点 (param_n6_1)
┌──────────────────┐           ┌──────────────────┐
│ nodeId: field_n3 │           │ nodeId: param_1  │
│ nodeType: docField│          │ nodeType: param  │
│ title: 字段提取   │           │ title: 企业规模   │
│ inputs: {}        │           │ inputs:          │
│ config: {...}     │           │   source:        │
└──────────────────┘           │   "link:n3.x"    │
       │                       └──────────────────┘
       │ 连线到AI节点                    │
                              不连线，仅提供数据
                              结构完全一致
```

### 8.2 面板结构（V2 — 移除模型选择、参数在模板之后）

```
┌─────────────────────────────────────────────────────────────────────┐
│  🤖 AI 节点                                                          │
│  节点名称: [AI 合规判断                                     ]        │
├─────────────────────────────────────────────────────────────────────┤
│  📝 模板编辑器                                      [+ 插入引用 ▾]   │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │ 判断企业规模为 {{企业规模}} 人，是否满足 {{标准规模}} 人的    │    │
│  │ 最低要求。结论只需回答 true 或 false。                       │    │
│  └─────────────────────────────────────────────────────────────┘    │
│  💡 点击「+ 插入引用」选择已定义参数或工作流节点                     │
├─────────────────────────────────────────────────────────────────────┤
│  📦 参数定义（每个参数 = 一个隐形节点）                               │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │ ┌─────┐ ┌────────┐ ┌──────────┐ ┌──────────────────────┐  │    │
│  │ │🔗   │ │企业规模│ ▾文本    ▾ │ ▾节点结果            │  │    │
│  │ └─────┘ └────────┘ └──────────┘ └──────────────────────┘  │    │
│  │   ▾ get_field_n3.fieldValue                                 │    │
│  ├─────────────────────────────────────────────────────────────┤    │
│  │ ┌─────┐ ┌────────┐ ┌──────────┐ ┌──────────────────────┐  │    │
│  │ │🔢   │ │标准规模│ ▾数字    ▾ │ ▾常量                │  │    │
│  │ └─────┘ └────────┘ └──────────┘ └──────────────────────┘  │    │
│  │   [200                                           ]         │    │
│  ├─────────────────────────────────────────────────────────────┤    │
│  │ ┌─────┐ ┌────────┐ ┌──────────┐ ┌──────────────────────┐  │    │
│  │ │🔧   │ │企业类型│ ▾文本    ▾ │ ▾方法调用            │  │    │
│  │ └─────┘ └────────┘ └──────────┘ └──────────────────────┘  │    │
│  │   ▾ get_param(param_code=MIN_COMPANY_SIZE)                │    │
│  │                                                             │    │
│  │ [+ 添加参数]  ← 创建一个隐形节点                             │    │
│  └─────────────────────────────────────────────────────────────┘    │
├─────────────────────────────────────────────────────────────────────┤
│  🔧 输出配置                                                         │
│  输出类型: [▾布尔  ]                                                │
├─────────────────────────────────────────────────────────────────────┤
│  [▶ 测试节点]                                  [保存] [取消]         │
└─────────────────────────────────────────────────────────────────────┘
```

### 8.3 字段配置顺序（panelSchema）

| 顺序 | 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|------|
| 1 | `config.promptTemplate` | textarea | ✅ | 提示词模板，使用 `{{参数名}}` 引用 |
| 2 | `config.customParams` | customParamsEditor | ✅ | 参数定义编辑器 |
| 3 | `config.systemPrompt` | textarea | ⬚ | 自定义系统提示词 |
| 4 | `config.outputType` | select | ✅ | 输出类型 |

> **移除字段**：`config.model`、`config.temperature` — 前端不再暴露，后端使用默认值（qwen-turbo / 0.1）

### 8.4 参数节点卡片交互

每个参数节点卡片包含：

```
┌───────────────────────────────────────────────────────────────┐
│ ┌─────┐  ┌────────────┐  ┌──────┐  ┌─────────────────────┐   │
│ │ 🔗  │  │ 参数名     │  │类型▾│  │ [🗑️]                │   │
│ └─────┘  └────────────┘  └──────┘  └─────────────────────┘   │
│                                                               │
│  来源: ◉ 常量  ○ 节点结果  ○ 方法调用                          │
│                                                               │
│  ── 来源 = 节点结果 ──────────────────                        │
│  选择节点: [▾ get_field_n3 (字段提取)       ]                 │
│  选择输出: [▾ fieldValue                   ]                  │
│                                                               │
│  ── 来源 = 常量 ──────────────────────                        │
│  常量值:   [500                               ]                │
│                                                               │
│  ── 来源 = 方法调用 ──────────────────                        │
│  选择方法: [▾ get_param                       ]                │
│  方法参数:                                                    │
│    param_code = ▾常量 [MIN_COMPANY_SIZE     ]                 │
│                                                               │
│  ─────────────────────────────────────                        │
│  预览: {{企业规模}} → get_field_n3.fieldValue                 │
└───────────────────────────────────────────────────────────────┘
```

### 8.5 插入引用交互

1. 用户点击「+ 插入引用」下拉
2. 展示当前工作流中所有可用数据源：
   - **工作流节点**：列出画布上所有节点的可见输出端口
   - **已定义参数**：列出当前 AI 节点的自定义参数
   - **上下文参数**：ctx.enterpriseCode、ctx.phaseCode 等
3. 选择后自动在光标位置插入 `{{参数名}}`

### 8.6 模型配置策略

| 配置项 | 策略 | 说明 |
|--------|------|------|
| 模型选择 | 后端默认 | 固定为 `qwen-turbo`，前端不暴露 |
| temperature | 后端默认 | 固定为 `0.1`（审核任务需要确定性输出） |
| maxTokens | 后端默认 | 固定为 `4096` |
| systemPrompt | 前端可编辑 | 默认值"你是认证审核AI助手..." |

---

## 9. 后端代码骨架

### 9.1 新增类

| 类名 | 位置 | 职责 |
|------|------|------|
| `AiNodeExecutor` | `WorkflowEngine/` | AI 节点专用执行器 |
| `CustomParam` | `WorkflowEngine/Models/` | 自定义参数模型 |
| `AiNodeTestRequest` | `WorkflowEngine/Models/` | 测试请求模型 |

### 9.2 AiNodeExecutor 骨架

```csharp
public class AiNodeExecutor
{
    private readonly ILlmClient _llm;
    private readonly ISkillRegistry _skillRegistry;
    private readonly IPromptInterpreter _promptInterpreter;
    private readonly ILogger<AiNodeExecutor> _logger;
    
    // 依赖注入构造
    
    /// <summary>
    /// 执行 AI 节点
    /// </summary>
    public async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNodeConfig node,
        string taskCode,
        string itemCode,
        Dictionary<string, object> sharedOutputs,
        Dictionary<string, object> contextParams,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            // 1. 解析自定义参数
            var paramPool = await ResolveCustomParamsAsync(
                node.Config.CustomParams, sharedOutputs, contextParams, ct);
            
            // 2. 渲染提示词
            var rendered = RenderPrompt(node.Config.PromptTemplate, paramPool);
            
            // 3. LLM 调用
            var response = await CallLlmAsync(node.Config, rendered, ct);
            
            // 4. 类型转换
            var result = ConvertResult(response.Content, node.Config.OutputType);
            
            sw.Stop();
            return NodeExecutionResult.Ok(WrapResult(result), (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return NodeExecutionResult.Fail(ex.Message, (int)sw.ElapsedMilliseconds);
        }
    }
    
    // 其他方法...
}
```

### 9.3 NodeExecutor 路由注册

```csharp
// NodeExecutor.cs 中修改分发
"ai_node" => await ExecuteAiNodeAsync(node, taskCode, itemCode, resolvedInputs, contextParams, ct),

// 新增方法
private async Task<Dictionary<string, object>> ExecuteAiNodeAsync(...)
{
    // 委托给 AiNodeExecutor 或直接在本地实现
}
```

---

## 10. 日志与监控

### 10.1 关键日志点

| 阶段 | 日志关键字 | 内容 |
|------|-----------|------|
| 参数解析 | `[AI_PARAMS]` | 列出每个参数的解析结果 |
| 模板渲染 | `[AI_PROMPT]` | 输出替换后的真实提示词 |
| LLM 调用 | `[AI_LLM_CALL]` | 模型、Token 数、耗时 |
| 结果转换 | `[AI_CONVERT]` | 原始返回 → 转换后结果 |
| 执行完成 | `[AI_DONE]` | 总耗时、最终结果 |

### 10.2 日志示例

```
[AI_PARAMS] nodeId=ai_node_n6, params={ "企业规模": "500人", "标准规模": 200 }
[AI_PROMPT] nodeId=ai_node_n6, rendered="判断企业规模为500人，是否满足200人的最低要求。"
[AI_LLM_CALL] nodeId=ai_node_n6, model=qwen-turbo, tokens_in=45, tokens_out=1, durationMs=890
[AI_CONVERT] nodeId=ai_node_n6, raw="true", converted=true, targetType=boolean
[AI_DONE] nodeId=ai_node_n6, result=true, durationMs=920
```

---

## 附录 A：自定义参数与占位符语法的映射

| 用户操作 | customParams | inputs（编译后） | 提示词中写法 |
|----------|-------------|------------------|-------------|
| 选择节点结果 | `sourceType=link, nodeId=n3, portName=fieldValue` | `n3.fieldValue` | `{{企业规模}}` |
| 选择方法调用 | `sourceType=skill, skillCode=get_param` | `skill:get_param` | `{{标准规模}}` |
| 输入常量 | `sourceType=constant, value=100` | `constant:100` | `{{阈值}}` |

用户在提示词编辑器中看到的是 `{{参数名}}`（友好名），后端编译后使用 `inputs` 映射实现替换。

---

## 附录 B：测试流程时序

```
前端                          Controller                     AiNodeExecutor
 │                                │                              │
 │─ POST /test/ai-node ─────────▶│                              │
 │  (含 ruleJson + mockOutputs)   │                              │
 │                                │─ Parse ruleJson ──▶          │
 │                                │─ Find ai_node ──▶            │
 │                                │─ Build sharedOutputs ──▶     │
 │                                │                              │
 │                                │── ExecuteAsync ─────────────▶│
 │                                │                              │─ ResolveParams
 │                                │                              │─ RenderPrompt
 │                                │                              │─ LlmComplete
 │                                │                              │─ ConvertResult
 │                              ◀── Result ─────────────────────│
 │◀─ 200 OK + debug info ────────│                              │
 │                                │                              │
```
