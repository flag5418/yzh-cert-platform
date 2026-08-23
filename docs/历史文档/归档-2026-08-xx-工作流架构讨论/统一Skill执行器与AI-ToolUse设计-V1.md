# 统一 Skill 执行器与 AI Tool Use 设计-V1

> **目标**：所有功能节点通过统一接口执行；AI 节点可在运行时动态调用任意 skill 作为工具。

## 一、核心设计思想

### 1.1 问题现状

```
当前架构：
  AI 节点 → prompt 写死引用格式（{{n1.field}}）→ 引擎模板渲染 → 调 LLM

问题：
  - AI 无法动态获取运行时数据（如：先让 AI 判断需要什么，再去取）
  - 测试/执行代码路径不一致
  - 每个 skill 独立实现，无法组合调用
```

### 1.2 目标架构

```
统一后：
  AI 节点 → 声明可用 Tools（skill 列表）→ LLM 输出 tool_call 引擎执行 → 结果回传 LLM → 最终输出
  
  普通节点 → 统一接口 ExecuteAsync(context) → NodeOutput
  
  测试节点 → 统一接口 TestAsync(context) → 任意结果
```

## 二、统一执行接口

### 2.1 接口定义（YZH.Framework）

```csharp
namespace YZH.Framework.Workflow;

/// <summary>
/// 所有功能节点的统一执行接口
/// </summary>
public interface ISkillExecutor
{
    /// <summary>
    /// 节点元数据（端口声明、面板 Schema）
    /// </summary>
    SkillMeta Meta { get; }
    
    /// <summary>
    /// 正式执行
    /// </summary>
    Task<NodeOutput> ExecuteAsync(SkillContext context);
    
    /// <summary>
    /// 测试执行（可返回中间数据，如样本、置信度）
    /// </summary>
    Task<TestResult> TestAsync(SkillContext context);
}

/// <summary>
/// 执行上下文（封装所有输入参数）
/// </summary>
public class SkillContext
{
    /// <summary>工作流级上下文（引擎注入）</summary>
    public WorkflowContext Workflow { get; set; }
    
    /// <summary>节点配置（config + 解析后的 inputs）</summary>
    public ResolvedNode Node { get; set; }
    
    /// <summary>DI 容器（获取 DB/缓存/外部服务）</summary>
    public IServiceProvider Services { get; set; }
    
    /// <summary>工作流执行 ID（用于日志追踪）</summary>
    public string ExecutionId { get; set; }
}

/// <summary>
/// 工作流上下文
/// </summary>
public class WorkflowContext
{
    public string OrgCode { get; set; }
    public string StandardCode { get; set; }
    public string PhaseCode { get; set; }
    public string FileCode { get; set; }
    public Dictionary<string, object> Variables { get; set; } = new();
}

/// <summary>
/// 已解析的节点参数
/// </summary>
public class ResolvedNode
{
    public string NodeId { get; set; }
    public string NodeType { get; set; }
    public Dictionary<string, object> Config { get; set; }
    public Dictionary<string, object> Inputs { get; set; }  // 已解析为实际值
}

/// <summary>
/// 节点输出
/// </summary>
public class NodeOutput
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public Dictionary<string, object> Ports { get; set; } = new();
    
    /// <summary>默认输出（result 端口）</summary>
    public object Result
    {
        get => Ports.GetValueOrDefault("result");
        set => Ports["result"] = value;
    }
}

/// <summary>
/// 测试结果
/// </summary>
public class TestResult
{
    public bool Success { get; set; }
    public object Data { get; set; }
    public double? Confidence { get; set; }
    public string Source { get; set; }
}
```

## 三、Skill 注册与发现

### 3.1 方案对比

| 方案 | 实现 | 耦合度 | 热加载 | 推荐 |
|------|------|--------|--------|------|
| A. 引擎直接引用 | `new CompareSkill()` | 高 | ❌ | ❌ |
| B. 插件 DLL | 加载外部 dll，反射实例化 | 低 | ✅ | ✅ |
| C. 数据库注册 | wf_skill 存 Assembly + TypeName | 中 | ✅ | ✅ |
| D. 特性扫描 | `[Skill("compare")]` + Assembly 扫描 | 中 | ❌ | ⭐ |

### 3.2 推荐方案：特性扫描 + DI（方案 D 变体）

```csharp
namespace YZH.Framework.Workflow;

/// <summary>
/// 特性标记：注册为 Skill 节点
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class SkillAttribute : Attribute
{
    public string Code { get; }
    public string Category { get; }
    
    public SkillAttribute(string code, string category = "skill")
    {
        Code = code;
        Category = category;
    }
}

/// <summary>
/// 注册扩展方法
/// </summary>
public static class SkillCollectionExtensions
{
    public static IServiceCollection AddWorkflowSkills(
        this IServiceCollection services, 
        Assembly[] skillAssemblies)
    {
        foreach (var asm in skillAssemblies)
        {
            var skillTypes = asm.GetTypes()
                .Where(t => t.GetCustomAttribute<SkillAttribute>() != null 
                            && !t.IsAbstract 
                            && typeof(ISkillExecutor).IsAssignableFrom(t));
            
            foreach (var type in skillTypes)
            {
                var attr = type.GetCustomAttribute<SkillAttribute>();
                services.AddKeyedTransient<ISkillExecutor>(attr.Code, type);
            }
        }
        return services;
    }
}
```

### 3.3 Skill 实现示例

```namespace YZH.Skills;

[Skill("compare", Category = "data_process")]
public class CompareSkill : ISkillExecutor
{
    private readonly IDocExtractionService _docService;
    
    public SkillMeta Meta { get; } = new()
    {
        Code = "compare",
        Name = "比较",
        InputPorts = new[]
        {
            new PortDef { Name = "valueA", Label = "值A", BindMode = "LinkOrConstant", Required = true, Type = "any" },
            new PortDef { Name = "operator", Label = "操作符", BindMode = "Enum", Required = true, EnumSource = "compare_operator" },
            new PortDef { Name = "valueB", Label = "值B", BindMode = "LinkOrConstant", Required = true, Type = "any" }
        },
        OutputPorts = new[]
        {
            new PortDef { Name = "result", Label = "比较结果", Type = "boolean" }
        },
        PanelSchema = Array.Empty<PanelField>()
    };

    // 函数式实现（无 DI）
    public CompareSkill() { }
    
    // 有 DI 的构造器
    public CompareSkill(IDocExtractionService docService)
    {
        _docService = docService;
    }
    
    public Task<NodeOutput> ExecuteAsync(SkillContext context)
    {
        var valueA = context.Node.Inputs.GetValueOrDefault("valueA");
        var op = context.Node.Inputs.GetValueOrDefault("operator")?.ToString();
        var valueB = context.Node.Inputs.GetValueOrDefault("valueB");
        
        var result = op switch
        {
            ">" => Comparer.Default.Compare(valueA, valueB) > 0,
            ">=" => Comparer.Default.Compare(valueA, valueB) >= 0,
            "<" => Comparer.Default.Compare(valueA, valueB) < 0,
            "<=" => Comparer.Default.Compare(valueA, valueB) <= 0,
            "==" => Equals(valueA, valueB),
            "!=" => !Equals(valueA, valueB),
            _ => throw new ArgumentException($"未知操作符: {op}")
        };
        
        return Task.FromResult(new NodeOutput
        {
            Success = true,
            Result = result
        });
    }
    
    public Task<TestResult> TestAsync(SkillContext context)
    {
        var output = ExecuteAsync(context).Result;
        return Task.FromResult(new TestResult
        {
            Success = output.Success,
            Data = output.Result
        });
    }
}

[Skill("get_field", Category = "data_access")]
public class GetFieldSkill : ISkillExecutor
{
    private readonly IDocExtractionService _docService;
    
    public SkillMeta Meta => new() { /* ... */ };
    
    public GetFieldSkill(IDocExtractionService docService)
    {
        _docService = docService;
    }
    
    public async Task<NodeOutput> ExecuteAsync(SkillContext context)
    {
        var ruleCode = context.Node.Config["ruleCode"]?.ToString();
        var fieldCode = context.Node.Config["fieldCode"]?.ToString();
        
        var result = await _docService.ExtractAsync(
            context.Workflow.OrgCode, 
            context.Workflow.FileCode, 
            ruleCode, 
            fieldCode);
        
        return new NodeOutput
        {
            Success = true,
            Ports = new Dictionary<string, object>
            {
                ["fieldValue"] = result.Value,
                ["confidence"] = result.Confidence,
                ["result"] = result.Value  // 默认输出
            }
        };
    }
}
```

## 四、AI 节点 Tool Use 集成

### 4.1 设计原理

```
传统（当前）：
  prompt: "企业名称是{{docField_n1.fieldValue}}，请判断..."
  问题：必须提前知道要取哪些字段

Tool Use（目标）：
  prompt: "请审核企业的合规性。如需获取信息可使用提供的工具。"
  tools: [get_field(ruleCode, fieldCode), get_table(ruleCode, tableCode)]
  
  LLM 输出：
  {
    "tool_calls": [
      {"function": "get_field", "arguments": {"ruleCode": "CERT-001", "fieldCode": "company_name"}},
      {"function": "get_table", "arguments": {"ruleCode": "CERT-001", "tableCode": "audit_records"}}
    ]
  }
  
  引擎执行 tool_calls → 结果回传 LLM → 最终推理
```

### 4.2 AI Skill 实现

```csharp
[Skill("ai_node", Category = "ai")]
public class AINodeSkill : ISkillExecutor, ISkillWithTools
{
    public SkillMeta Meta => new() { /* 已知定义 */ };
    
    private readonly ISkillRegistry _skillRegistry;
    private readonly ILLMService _llmService;
    
    public AINodeSkill(ISkillRegistry registry, ILLMService llmService)
    {
        _skillRegistry = registry;
        _llmService = llmService;
    }
    
    public async Task<NodeOutput> ExecuteAsync(SkillContext context)
    {
        // 1. 构建 Tool 列表
        var availableTools = GetAvailableTools(context);
        
        // 2. 渲染 prompt（已有的输入引用）
        var renderedPrompt = RenderPrompt(context);
        
        // 3. 首次 LLM 调用
        var response = await _llmService.ChatAsync(new ChatRequest
        {
            Prompt = renderedPrompt,
            Tools = availableTools.Select(t => t.ToFunctionDefinition()).ToList(),
            JsonMode = true
        });
        
        // 4. 处理 tool_calls（可能有多个，可能需要多轮）
        int maxToolRound = 5;
        int round = 0;
        
        while (response.ToolCalls.Any() && round < maxToolRound)
        {
            var toolResults = new List<ToolResult>();
            
            foreach (var call in response.ToolCalls)
            {
                var skill = _skillRegistry.Get(call.Function.Name);
                var result = await skill.ExecuteAsync(new SkillContext
                {
                    Workflow = context.Workflow,
                    Node = new ResolvedNode
                    {
                        Config = DeserializeConfig(call.Function.Arguments),
                        Inputs = ParseInputs(call.Function.Arguments, skill.Meta)
                    },
                    Services = context.Services
                });
                
                toolResults.Add(new ToolResult
                {
                    ToolCallId = call.Id,
                    Content = JsonSerializer.Serialize(result.Ports)
                });
            }
            
            // 5. 结果回传 LLM
            response = await _llmService.ChatAsync(new ChatRequest
            {
                Prompt = renderedPrompt,
                Tools = availableTools.Select(t => t.ToFunctionDefinition()).ToList(),
                History = response.History,
                ToolResults = toolResults
            });
            
            round++;
        }
        
        // 6. 最终输出
        return new NodeOutput
        {
            Success = true,
            Ports = new Dictionary<string, object>
            {
                ["content"] = response.Text,
                ["json"] = response.Parsed,
                ["confidence"] = response.Confidence
            }
        };
    }
    
    /// <summary>
    /// 获取当前上下文可用的 Tools（依赖图中上游节点 + 全局 skill）
    /// </summary>
    private List<ISkillExecutor> GetAvailableTools(SkillContext context)
    {
        // 方案 A：根据依赖图动态决定
        // 方案 B：读取 wf_skill 表中标记为 toolable=true 的 skill
        var toolableCodes = new[] { "get_field", "get_table", "query_nc_history" };
        return toolableCodes
            .Select(code => _skillRegistry.Get(code))
            .Where(s => s != null)
            .ToList();
    }
}
```

### 4.3 LLM Function Definition 生成

```csharp
public static class SkillExtensions
{
    /// <summary>
    /// 生成 OpenAI/Claude 兼容的 Function Calling 定义
    /// </summary>
    public static FunctionDefinition ToFunctionDefinition(this ISkillExecutor executor)
    {
        var parameters = new ParameterSchema
        {
            Type = "object",
            Properties = new Dictionary<string, ParameterProperty>(),
            Required = new List<string>()
        };
        
        foreach (var port in executor.Meta.InputPorts)
        {
            parameters.Properties[port.Name] = new ParameterProperty
            {
                Type = port.Type switch 
                { 
                    "number" => "number",
                    "boolean" => "boolean", 
                    _ => "string" 
                },
                Description = port.Description ?? port.Label
            };
            
            if (port.Required) parameters.Required.Add(port.Name);
        }
        
        // config 参数也加入
        foreach (var field in executor.Meta.PanelSchema)
        {
            parameters.Properties["config." + field.Field.Split('.').Last()] = new ParameterProperty
            {
                Type = "string",
                Description = field.Description
            };
        }
        
        return new FunctionDefinition
        {
            Name = executor.Meta.Code,
            Description = executor.Meta.Description,
            Parameters = parameters
        };
    }
}
```

### 4.4 Function Calling JSON 示例

```json
{
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "get_field",
        "description": "从文档中提取指定字段的值",
        "parameters": {
          "type": "object",
          "properties": {
            "ruleCode": {"type": "string", "description": "文档编码"},
            "fieldCode": {"type": "string", "description": "字段编码"}
          },
          "required": ["ruleCode", "fieldCode"]
        }
      }
    },
    {
      "type": "function", 
      "function": {
        "name": "get_table",
        "description": "从文档中提取表格数据",
        "parameters": {
          "type": "object",
          "properties": {
            "ruleCode": {"type": "string", "description": "文档编码"},
            "tableCode": {"type": "string", "description": "表格编码"}
          },
          "required": ["ruleCode", "tableCode"]
        }
      }
    }
  ]
}
```

## 五、执行引擎统一调用

### 5.1 引擎执行循环

```csharp
public class WorkflowEngine
{
    private readonly IServiceProvider _services;
    private readonly ISkillRegistry _registry;
    
    public async Task<WorkflowOutput> ExecuteAsync(
        WorkflowConfig config, 
        WorkflowContext wfContext)
    {
        // 1. 拓扑排序
        var sorted = topologyService.Sort(config);
        
        // 2. 按序执行
        var outputs = new Dictionary<string, NodeOutput>();
        
        foreach (var nodeId in sorted.Order)
        {
            var nodeConfig = config.nodes.First(n => n.NodeId == nodeId);
            var nodeType = nodeConfig.NodeType;
            
            NodeOutput output;
            
            // 特殊节点
            if (nodeType == "start") { continue; }
            if (nodeType == "end") { return BuildEndOutput(outputs, nodeConfig); }
            if (nodeType == "branch") { /* 分支选择逻辑 */ continue; }
            
            // 功能节点：统一执行
            if (_registry.TryGet(nodeType, out var executor))
            {
                // 解析 inputs（把引用替换为实际值）
                var resolvedInputs = ResolveInputs(nodeConfig.Inputs, outputs, wfContext);
                
                var context = new SkillContext
                {
                    Workflow = wfContext,
                    Node = new ResolvedNode 
                    { 
                        NodeId = nodeId,
                        Config = nodeConfig.Config,
                        Inputs = resolvedInputs
                    },
                    Services = _services,
                    ExecutionId = wfContext.ExecutionId
                };
                
                output = await ExecuteWithTimeoutAsync(executor, context);
                outputs[nodeId] = output;
            }
        }
        
        return new WorkflowOutput { Success = true };
    }
    
    private async Task<NodeOutput> ExecuteWithTimeoutAsync(
        ISkillExecutor executor, SkillContext context, int timeoutMs = 30000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            return await executor.ExecuteAsync(context).WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return new NodeOutput 
            { 
                Success = false, 
                ErrorMessage = $"执行超时（{timeoutMs}ms）" 
            };
        }
    }
}
```

## 六、前端测试流程统一

```
当前：
  节点 A 测试 → 前端调 api/xxx/test → 后端独立实现
  
统一后：
  节点 A 测试 → 前端 workflowDesigner.testNode(node) → 
    后端 WorkflowEngine.TestNode(nodeId) → 
      ISkillExecutor.TestAsync(context) → 
        返回 TestResult
```

前端调用：

```javascript
// WorkflowDesigner.vue 中的测试逻辑
async function testNode(nodeId) {
  const res = await proxy.http.post('api/workflow/test-node', {
    ruleCode: currentRule.ruleCode,
    nodeId: nodeId,  // 只需传 nodeId
    workflowConfig: serialize(store.state.nodes, store.state.edges)
  });
  return res.data;
}
```

后端只需实现一个 TestNode endpoint：

```csharp
[HttpPost("test-node")]
public async Task<IActionResult> TestNode([FromBody] TestNodeRequest req)
{
    var config = JsonSerializer.Deserialize<WorkflowConfig>(req.WorkflowConfig);
    var node = config.nodes.First(n => n.NodeId == req.NodeId);
    
    // 统一入口：不需要 switch(nodeType) 逐个判断
    if (_registry.TryGet(node.NodeType, out var executor))
    {
        var result = await executor.TestAsync(new SkillContext
        {
            Workflow = BuildWorkflowContext(req),
            Node = BuildResolvedNode(node, config),
            Services = _serviceProvider
        });
        return Ok(result);
    }
    
    return BadRequest($"未知节点类型: {node.NodeType}");
}
```

## 七、注册与配置

### 7.1 后端 Program.cs

```csharp
// 注册所有 Skill 实现
var skillAssemblies = new[] 
{ 
    typeof(CompareSkill).Assembly,
    typeof(GetFieldSkill).Assembly,
    // ...
};

builder.Services.AddWorkflowSkills(skillAssemblies);
```

### 7.2 wf_skill 表扩展（可选）

```sql
ALTER TABLE wf_skill ADD COLUMN assembly_name VARCHAR(200) NULL COMMENT '实现类 dll';
ALTER TABLE wf_skill ADD COLUMN class_name VARCHAR(200) NULL COMMENT '实现类全名';
ALTER TABLE wf_skill ADD COLUMN is_toolable TINYINT(1) DEFAULT 0 COMMENT 'AI 是否可作为 Tool 调用';
```

## 八、优势总结

| 维度 | 当前 | 统一后 |
|------|------|--------|
| 新增 Skill | 写 Controller + Service + 单个 API | 实现一个类 + 加特性 |
| AI 调用 | 模板渲染，静态 | Function Calling，动态获取 |
| 测试 | 每个节点单独 API | 统一 `TestAsync`，一个 endpoint |
| 执行 | 按类型 switch / if-else | 统一 `ExecuteAsync`，无分支 |
| 可观测性 | 分散 | 统一日志、超时、异常处理 |
| Skill 组合 | 不可行 | A skill 调 B skill 完全可行 |

## 九、实施路径

```
Phase 1（本周）：
  ✓ YZH.Framework 定义 ISkillExecutor / SkillContext / NodeOutput
  ✓ 现有 2-3 个节点改为 ISkillExecutor 实现
  ✓ 引擎 switch 改为 ISkillRegistry 调用

Phase 2（下迭代）：
  - AI 节点 Function Calling 改造
  - wf_skill 表扩展（is_toolable / 函数定义 Schema）
  - 前端测试统一

Phase 3（远期）：
  - 热加载插件 DLL
  - Skill 市场/版本管理
  - 可视化编排 + LLM 自动编排
```

---

*文档版本：V1 | 最后更新：2026-08-21 | 架构组评审*
