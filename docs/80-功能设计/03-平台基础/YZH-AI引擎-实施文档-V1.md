# YZH-AI引擎实施文档-V1

> **版本**：V1.0 | **日期**：2026-08-11 | **状态**：已完成
> **关联设计文档**：[YZH-AI引擎详细设计-V1.md](./YZH-AI引擎详细设计-V1.md)
> **实施TODO清单**：[YZH-AI引擎-实施TODO清单-V1.md](./YZH-AI引擎-实施TODO清单-V1.md)

---

## 一、总体架构

```
前端（Vue3）
    ↓ HTTP API
DocExtractionRuleController（VOL.WebApi）
    ↓
DocExtractionRuleService（VOL.Builder）← S5 接入层
    ↓
IWorkflowEngine（YZH.Core.Workflow）
    ↓ 按 skill_code 查注册表
SkillRegistry（YZH.Core.Workflow）
    ↓ 调用具体执行器
内置 Skill 实现（YZH.Core.Skills）
    ├─ LlmExtractSkill → ILlmClient + IPromptInterpreter
    ├─ DocumentExtractSkill → IFileExtractor
    ├─ CompareSkill / GetFieldSkill / GetTableSkill / AssembleSkill
    ↓
Provider 路由（ILlmProvider）
    ├─ QwenApiProvider（云端 qwen-turbo）
    ├─ OllamaProvider（本地断网兜底）
    └─ MockProvider（测试桩）
    ↓
落库（EF Core）
    ├─ B-08 ent_extraction_result（字段级）
    └─ B-09 ent_table_extraction_result（表格级）
    └─ F-04 wf_workflow_execution_log（执行留痕）
```

---

## 二、YZH.Core 新增文件清单

### 2.1 AI/Clients/ — LLM 网关层（S1）

| 文件 | 职责 | 关键设计 |
|------|------|----------|
| `ILlmClient.cs` | LLM 统一入口接口 | `CompleteAsync(LlmRequest)` + `ActiveProvider` 属性 |
| `ILlmProvider.cs` | Provider 抽象接口 | `Name` + `ChatAsync()`，实现类负责协议差异 |
| `LlmRequest.cs` | 请求模型 | Provider/Model/Messages/Temperature/MaxTokens/JsonMode/TimeoutSeconds |
| `LlmResponse.cs` | 响应模型 | Success/Content/RawJson/Tokens/DurationMs/Provider/Model/Error |
| `LlmMessage.cs` | 消息模型 | Role(system/user/assistant) + Content |
| `LlmClient.cs` | **核心网关实现** | Provider路由 + 指数退避重试 + 熔断 + 信号量 |
| `QwenApiProvider.cs` | 千问云端 Provider | OpenAI兼容协议，`AI_QWEN_API_KEY` 环境变量优先 |
| `OllamaProvider.cs` | 本地断网 Provider | `/api/chat` 端点，无鉴权 |
| `MockProvider.cs` | 测试桩 Provider | 返回固定JSON，不消耗token |
| `LlmCallException.cs` | LLM调用异常 | `IsTimeout` / `IsUnreachable` 语义标记 |

**LlmClient 核心逻辑**（`LlmClient.cs:47-114`）：

```
CompleteAsync(request)
  ├── 1. 熔断检查：当前时间 < _circuitBreakerUntil → 直接抛异常
  ├── 2. Provider解析：request.Provider 非空 → 使用；否则读 ActiveProvider（appsettings Ai:Provider）
  ├── 3. 未注册检查：显式指定 Provider 但注册表无匹配 → 抛 LlmCallException
  ├── 4. 降级链遍历：ProviderOrder(首选项) → [qwen, ollama, mock]
  │     └── 对每个 Provider：
  │           ├── 获取信号量（默认并发2）
  │           ├── 调用 provider.ChatAsync()
  │           ├── 成功 → 重置连续失败计数，返回结果
  │           └── 失败（超时/429/5xx）：
  │                 ├── 累计失败次数，>5次 → 设置熔断30s
  │                 └── 指数退避：1s / 3s / 7s 重试，最多3次
  └── 5. 所有Provider失败 → 抛 LlmCallException
```

### 2.2 AI/Prompt/ — 提示词渲染与解析层（S3）

| 文件 | 职责 | 关键设计 |
|------|------|----------|
| `IPromptInterpreter.cs` | 接口定义 | `Render()` + `ParseAsync<T>()` |
| `PromptInterpreter.cs` | **核心实现** | 占位符渲染 + JSON围栏剥离 + 容错解析 |
| `RenderContext.cs` | 渲染上下文 | 继承 `Dictionary<string, object>` |
| `ParseResult.cs` | 解析结果 | `Success` / `Value` / `Error` / `RawText` |
| `PromptParseException.cs` | 解析异常 | 携带 RawText 供调试 |

**PromptInterpreter.Render 逻辑**（`PromptInterpreter.cs:17-31`）：

```
Render(template, context)
  ├── 正则匹配 {name} 占位符
  ├── 对每个匹配：
  │     ├── 从 context 查 key
  │     ├── 存在 → 按类型处理：string 直接返回 / null 返回空 / 其他 JsonSerializer.Serialize
  │     └── 不存在 → 保留原样 {name}（不报错）
  └── 返回渲染后字符串
```

**PromptInterpreter.ParseAsync 逻辑**（`PromptInterpreter.cs:33-62`）：

```
ParseAsync<T>(llmOutput)
  ├── 剥离 ```json ... ``` 围栏（正则）
  ├── 兜底：取首个 { 到最后一个 } 的子串
  ├── System.Text.Json 反序列化（PropertyNameCaseInsensitive）
  ├── 成功 → ParseResult<T> { Success=true, Value=结果, RawText=去围栏后文本 }
  └── 失败 → ParseResult<T> { Success=false, Error=异常信息, RawText=去围栏后文本 }
```

### 2.3 AI/Plan/ — AI 规划模型（S3）

| 文件 | 职责 |
|------|------|
| `AiPlan.cs` | 规划根对象：`PlanName` + `Steps` + `OutputMapping` |
| `AiStep.cs` | 规划步骤：`Order` + `SkillCode` + `Params` |
| `AiPlanParser.cs` | JSON 反序列化为强类型 `AiPlan` |

### 2.4 Workflow/ — 工作流引擎层（S2+S4）

| 文件 | 职责 |
|------|------|
| `ISkillNode.cs` | Skill 执行器契约：`SkillCode` + `ExecuteAsync()` |
| `SkillContext.cs` | 执行上下文：`Inputs` + `WorkflowInstanceId` + `NodeId` + `Logger` |
| `SkillResult.cs` | 执行结果：`Success` + `Outputs` + `Confidence` + `Error` + `DurationMs` |
| `ISkillRegistry.cs` | 注册表接口：`Get` / `RegisterAsync` / `UnregisterAsync` / `AllCodes` |
| `SkillRegistry.cs` | **注册表实现**：`ConcurrentDictionary<string, ISkillNode>`，线程安全 |
| `IWorkflowEngine.cs` | 引擎接口：`RunAsync(workflowConfigJson, context)` |
| `WorkflowEngine.cs` | **引擎核心实现**：拓扑排序 + 节点执行 + 条件分支 + 留痕 |
| `WorkflowContext.cs` | 运行上下文：`WorkflowInstanceId` + `BusinessType` + `BusinessCode` + `Inputs` + `LogStore` |
| `WorkflowRunResult.cs` | 运行结果：`Success` + `NodeOutputs` + `FailedNodeId` + `Error` + `DurationMs` |
| `IExecutionLogStore.cs` | 留痕接口：`WriteAsync` / `QueryByInstanceAsync` |
| `ExecutionLogEntry.cs` | 日志条目模型（见下方） |
| `InMemoryExecutionLogStore.cs` | 内存实现（单测用） |
| `WorkflowExceptions.cs` | 自定义异常：`WorkflowExecutionException` / `UnknownSkillException` |

**Workflow/Models/ — 工作流配置模型**：

| 文件 | 职责 |
|------|------|
| `WorkflowConfig.cs` | 根配置：`Nodes` + `Edges` + `Branches` + `OutputConfig` + `AllNodes()` + `FindNode()` |
| `WorkflowNode.cs` | 节点定义：`NodeId` + `SkillCode` + `Inputs` + `Output` |
| `WorkflowEdge.cs` | 边定义：`From` + `To` |
| `BranchConfig.cs` | 分支定义：`From` + `Condition` + `Then`（子节点列表） |
| `BranchCondition.cs` | 条件定义：`Field` + `Op`（枚举） + `Value`，含自定义 JSON 转换器 |

**ExecutionLogEntry 字段**（对应 F-04 `wf_workflow_execution_log`）：

```csharp
public class ExecutionLogEntry
{
    public string WorkflowCode { get; set; }       // F-03.workflow_code
    public int WorkflowVersion { get; set; } = 1;
    public string BusinessType { get; set; } = "file_upload";
    public string BusinessCode { get; set; } = string.Empty;  // 全局 Code 规范
    public long BusinessId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string SkillCode { get; set; } = string.Empty;
    public string? InputDataJson { get; set; }    // 截断 16KB
    public string? OutputDataJson { get; set; }   // 截断 64KB
    public string Status { get; set; } = "pending"; // pending/running/success/failed/skipped
    public string? ErrorMsg { get; set; }
    public long DurationMs { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
```

### 2.5 Skills/ — 内置 Skill 实现（S2）

| 文件 | SkillCode | 职责 | 依赖 |
|------|-----------|------|------|
| `DocumentExtractSkill.cs` | `document_extract` | 包装 IFileExtractor，处理 convertStatus 状态机 | `IFileExtractor` |
| `LlmExtractSkill.cs` | `llm_extract` | **核心AI Skill**：渲染提示词 → 调LLM → 解析JSON | `ILlmClient` + `IPromptInterpreter` |
| `CompareSkill.cs` | `compare` | 确定性比较：数值比较 / 日期差 / not_empty | 无外部依赖 |
| `GetFieldSkill.cs` | `get_field` | 从 B-08 查已落库字段（按 label_tag） | `VOLContext` |
| `GetTableSkill.cs` | `get_table` | 从 B-09 查已落库表格（按 table_code） | `VOLContext` |
| `AssembleSkill.cs` | `assemble` | 字符串拼接（报告引擎用） | 无外部依赖 |

**LlmExtractSkill 执行流程**（`LlmExtractSkill.cs:31-86`）：

```
ExecuteAsync(context)
  ├── 1. 提取入参：document_content / prompt / fields_json / tables_json
  ├── 2. 校验：prompt 为空 → 返回失败
  ├── 3. 渲染提示词：_interpreter.Render(template, {document_content, fields_json, tables_json})
  ├── 4. 最多 2 次 LLM 调用：
  │     ├── 第 1 次：原始渲染 prompt
  │     ├── 第 2 次（仅当 JSON 解析失败）：追加"严格 JSON"提示
  │     └── 每次调用：ILlmClient.CompleteAsync({system: "只输出JSON", user: prompt}, json_mode=true)
  ├── 5. 解析输出：_interpreter.ParseAsync<AiExtractionResult>(llmOutput)
  ├── 6. 计算置信度：fields 最低 confidence（无字段时返回 0）
  └── 7. 返回 SkillResult { outputs: {fields, tables, raw_json}, confidence }
```

---

## 三、Vol 框架层改动

### 3.1 DocExtractionRuleService.AI.cs（新增 Partial 类）

**位置**：`VOL.Builder/Services/CertPlatform/DocExtractionRuleService.AI.cs`

**职责**：将 S1~S4 四件套接入 `DocExtractionRuleService` 的 4 个私有 TODO 方法。

| 方法 | 原状态 | 现实现 |
|------|--------|--------|
| `GetFileInfoAsync(fileCode)` | TODO 返回 null | EF 查询 `StandardDirectoryFile` 按 `FileCode` |
| `ExtractDocumentContentAsync(fileInfo, skill)` | TODO 返回空 | 调用 `IFileExtractor.ExtractAsync()` 取 `FullText` |
| `CallAIForAnalysisAsync(docContent, skill)` | TODO 模拟返回 | 构建 analyze prompt → WorkflowEngine 执行 → 映射为 `AIAnalyzeResponse` |
| `CallAIForExtractionAsync(docContent, prompt)` | TODO 模拟返回 | 构建 extract workflow → WorkflowEngine 执行 → 映射为 `ExtractionData` |

**关键辅助方法**：

| 方法 | 用途 |
|------|------|
| `BuildAnalysisPrompt(skill)` | 生成 analyze 模式提示词（推荐字段/表格） |
| `BuildExtractWorkflow(prompt)` | 生成最小工作流 JSON（单节点 `llm_extract`） |
| `MapAiFieldsToDtos(outputs)` | `List<AiField>` → `List<FieldDefDto>` |
| `MapAiTablesToDtos(outputs)` | `List<AiTable>` → `List<TableDefDto>` |
| `MapOutputsToExtractionData(outputs)` | `outputs` → `ExtractionData`（verify 用） |

### 3.2 DocExtractionRuleController.cs（新增两个接口）

**位置**：`VOL.WebApi/Controllers/CertPlatform/DocExtractionRuleController.cs`

| 接口 | 方法 | 职责 |
|------|------|------|
| `GET api/DocExtractionRule/files/tree?directoryCode=xxx` | `GetFileTree` | 调用 `IStandardDirectoryService.GetStageFileTree()` 返回文件树 |
| `GET api/DocExtractionRule/files/{fileCode}/content` | `GetFileContent` | 查 `StandardDirectoryFile` → `IFileExtractor.ExtractAsync()` → 返回全文 |

### 3.3 VOL.Builder.csproj（新增项目引用）

```xml
<ProjectReference Include="..\..\..\YZH-Framework\YZH.Core\YZH.Core.csproj" />
```

---

## 四、配置文件

### 4.1 appsettings.json（AI 配置）

```json
{
  "Ai": {
    "Provider": "qwen",
    "MaxConcurrency": 2
  }
}
```

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| `Ai:Provider` | 默认 Provider（qwen/ollama/mock） | `qwen` |
| `Ai:MaxConcurrency` | 全局并发信号量上限 | `2` |

### 4.2 环境变量

| 变量名 | 说明 |
|--------|------|
| `AI_QWEN_API_KEY` | 千问 API Key（QwenApiProvider 优先读取） |
| `AI_MAX_CONCURRENCY` | 覆盖 appsettings 的并发数（1~32） |

---

## 五、数据库变更

### 5.1 迁移脚本

**路径**：`src/server/Vue.NetCore/DB/mysql/cert_phase_ai_engine_t0_alignment.sql`

```sql
-- 新增执行状态列（区别于基类 Status 实体启用标记）
ALTER TABLE wf_workflow_execution_log
  ADD COLUMN IF NOT EXISTS execution_status VARCHAR(20) DEFAULT 'pending'
    COMMENT '执行状态：pending/running/success/failed/skipped';

-- 执行状态索引
ALTER TABLE wf_workflow_execution_log
  ADD INDEX IF NOT EXISTS idx_execution_status (execution_status);
```

**执行命令**：
```bash
docker exec -i yzh-mysql mysql -uroot -pYzh123456. yzh_cert_platform \
  < src/server/Vue.NetCore/DB/mysql/cert_phase_ai_engine_t0_alignment.sql
```

### 5.2 字段映射对照

| 设计文档字段 | 实体字段 | DB 列名 | 说明 |
|-------------|---------|---------|------|
| `WorkflowId (long)` | `WorkflowCode (string)` | `workflow_code` | 全局 Code 规范 |
| `BusinessId (long)` | `BusinessId (long)` | `business_id` | 保持 long（关联 F-03.id） |
| — | `ExecutionStatus (string)` | `execution_status` | 新增，区别于基类 Status |

---

## 六、代码运行主链路

### 6.1 配置期：analyze → generate-prompt → verify → save

```
前端点击"AI分析"
  ↓ POST /api/DocExtractionRule/analyze
DocExtractionRuleController.AIAnalyze()
  ↓
DocExtractionRuleService.AIAnalyzeAsync()
  ├── GetFileInfoAsync(fileCode) → StandardDirectoryFile（查库）
  ├── ExtractDocumentContentAsync() → IFileExtractor.ExtractAsync() → FullText
  └── CallAIForAnalysisAsync(docContent, skill)
        ├── BuildAnalysisPrompt(skill) → 分析提示词
        ├── BuildExtractWorkflow(prompt) → 工作流JSON
        ├── WorkflowEngine.RunAsync()
        │     ├── 拓扑排序（Kahn算法）
        │     ├── ExecuteNodeAsync("n1")
        │     │     ├── SkillRegistry.Get("llm_extract") → LlmExtractSkill
        │     │     ├── ResolveInputs({document_content, prompt})
        │     │     └── LlmExtractSkill.ExecuteAsync()
        │     │           ├── PromptInterpreter.Render() → 最终 Prompt
        │     │           ├── ILlmClient.CompleteAsync() → QwenApiProvider
        │     │           └── PromptInterpreter.ParseAsync<AiExtractionResult>()
        │     └── 返回 NodeOutputs["n1"]
        └── MapAiFieldsToDtos() + MapAiTablesToDtos()
  ↓
返回 AIAnalyzeResponse { Fields, Tables }
```

### 6.2 运行期：上传 → 提取 → 落库

```
用户上传文件
  ↓ POST /api/DocExtractionRule/verify
DocExtractionRuleService.VerifyPromptAsync()
  ├── GetFileInfoAsync() → StandardDirectoryFile
  ├── ExtractDocumentContentAsync() → IFileExtractor → FullText
  ├── CallAIForExtractionAsync(docContent, prompt)
  │     ├── BuildExtractWorkflow(prompt)
  │     ├── WorkflowEngine.RunAsync()
  │     │     └── LlmExtractSkill → ILlmClient → LLM → 解析 → fields[]/tables[]
  │     └── MapOutputsToExtractionData()
  └── 落库：
        ├── B-08 ent_extraction_result（每条 field 一条记录）
        └── B-09 ent_table_extraction_result（每个 table 一条记录）
  ↓
返回 VerifyPromptResponse { Success, Data }
```

### 6.3 F-04 留痕记录

每次节点执行都会写入 `wf_workflow_execution_log`：

```
WorkflowEngine.ExecuteNodeAsync()
  ├── 执行前：status = "running"
  ├── 执行后：
  │     ├── success → status = "success"
  │     └── failed → status = "failed" + ErrorMsg
  └── IExecutionLogStore.WriteAsync(ExecutionLogEntry)
        ├── InMemoryExecutionLogStore（单测）
        └── ExecutionLogStoreEf（生产，待 S4 补全）
```

---

## 七、测试覆盖

| 测试文件 | 覆盖场景 | 用例数 |
|----------|----------|--------|
| `S1_LlmClientTests.cs` | Provider路由/未知Provider/并发/降级链/熔断 | 7 |
| `S2_SkillRegistryTests.cs` | 注册/查询/覆盖/注销/并发 | 5 |
| `S2_LlmExtractSkillTests.cs` | MockProvider端到端/缺少prompt | 2 |
| `S3_PromptInterpreterTests.cs` | Render占位符/Parse围栏剥离/非法JSON | 8 |
| `S3_AiPlanParserTests.cs` | plan JSON解析/无output_mapping | 2 |
| `S4_WorkflowEngineTests.cs` | 线性管道/条件分支/环检测/未知Skill/留痕/模板解析 | 7 |

**总计**：74 个测试用例，全部通过。

---

## 八、关键依赖关系图

```
DocExtractionRuleService.AI.cs
    ├── IFileExtractor（已实现，42/42测试通过）
    ├── IWorkflowEngine（S4实现）
    │     ├── ISkillRegistry（S2实现）
    │     │     └── ISkillNode 实现列表：
    │     │           ├── DocumentExtractSkill → IFileExtractor
    │     │           ├── LlmExtractSkill → ILlmClient + IPromptInterpreter
    │     │           ├── CompareSkill（无依赖）
    │     │           ├── GetFieldSkill → VOLContext
    │     │           ├── GetTableSkill → VOLContext
    │     │           └── AssembleSkill（无依赖）
    │     └── IExecutionLogStore（S4实现）
    └── IPromptInterpreter（S3实现）
          └── ILlmClient（S1实现）
                └── ILlmProvider 实现列表：
                      ├── QwenApiProvider
                      ├── OllamaProvider
                      └── MockProvider
```

---

## 九、待后续事项

| 优先级 | 事项 | 说明 |
|--------|------|------|
| P0 | `cert_doc_extraction_rule.skill` 收敛 | 当前只有 word/excel/pdf，需对齐 F-01 skill_code |
| P0 | `cert_ai_config.api_key` 环境变量化 | 当前明文存 DB，需改环境变量优先 |
| P1 | F-04 EF 实现 | 当前只有 InMemory 实现，需补 `ExecutionLogStoreEf` |
| P2 | 前端 4 按钮接真 | `index.vue` 调用 `/analyze`、`/verify`、`/save` 接口 |
| P2 | 低置信度人工复核 | `confidence < 0.8` 时前端红标 + `is_manual_edited` 标记 |

---

**文档版本**：V1.0
**创建时间**：2026-08-11
**最后更新**：2026-08-11
**创建者**：AI 编程助手（Agnes）
