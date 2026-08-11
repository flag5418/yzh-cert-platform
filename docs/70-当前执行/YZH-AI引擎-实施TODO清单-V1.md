# YZH-AI引擎实施TODO清单-V1

> **版本**：V1.0 | **日期**：2026-08-11 | **状态**：实施中
> **关联设计文档**：[YZH-AI引擎详细设计-V1.md](./YZH-AI引擎详细设计-V1.md)
> **实施来源**：设计文档 §10 里程碑 S1~S5 + 项目规则对齐

---

## 实施状态总览

| 阶段 | 预估工时 | 状态 | 完成度 |
|------|----------|------|--------|
| T0 前置：文档与实体对齐 | 0.5 天 | 进行中 | 0% |
| S1 LLM Gateway | 1 天 | ✅ 完成 | 100% |
| S2 SkillRegistry + 内置6个Skill | 1 天 | ✅ 完成 | 100% |
| S3 PromptInterpreter | 1 天 | ✅ 完成 | 100% |
| S4 WorkflowEngine + 留痕 | 1.5 天 | ✅ 完成 | 100% |
| S5 接入验证场 | 2 天 | ✅ 完成 | 100% |
| **合计** | **6.5 天** | — | — |

> **当前基线**：`dotnet build` 0 错误 / 42/42 测试通过 / MySQL(3307) + Redis(6380) 容器运行中

---

## T0：前置对齐（0.5 天）

> **目标**：消除设计文档与现有代码/数据库之间的不一致，为 S1 实施扫清障碍。

- [x] **T0-1** 更新 `YZH-AI引擎详细设计-V1.md` §3.6：
  - `ExecutionLogEntry.WorkflowId (long)` → `WorkflowCode (string)`
  - `ExecutionLogEntry.BusinessId (long)` → `BusinessCode (string)`（当前保留 long，与 DB 对齐）
  - 新增 `ExecutionStatus` 字段（pending/running/success/failed/skipped）
  - 补充说明 AI 配置优先级：`AIConfig`（DB）> 环境变量 `AI_QWEN_API_KEY`
- [x] **T0-2** 修改 `VOL.Entity/CertPlatform/Wf/WorkflowExecutionLog.cs`：
  - 新增 `ExecutionStatus` 字段（`[StringLength(20)]`，默认 `"pending"`，对应 DB `execution_status` 列）
  - 基类 `Status` 保留（实体级启用标记），执行状态用独立 `ExecutionStatus` 字段，避免语义冲突
  - `BusinessId` 保留 `long`（当前 DB 为 `bigint NOT NULL`，与 F-03 `wf_workflow_definition.id` 自增主键对齐）
  - `WorkflowCode` 保留（当前实体字段，对应 DB `workflow_code`）
- [x] **T0-3** 生成 DB 迁移 SQL：
  - 脚本路径：`src/server/Vue.NetCore/DB/mysql/cert_phase_ai_engine_t0_alignment.sql`
  - 新增列：`execution_status VARCHAR(20) DEFAULT 'pending'` + 索引
  - 幂等（存储过程 IF EXISTS 检查）
- [x] **T0-4** 执行迁移 SQL 并验证表结构：
  - `execution_status` 列已加入，默认值 `pending`，含索引 `idx_execution_status`
- [x] **T0-5** 确认 `AIConfig` 实体已就绪（已核实：存在，表 `cert_ai_config`）：
  - 字段：`Provider/ApiKey/Model/Temperature/MaxTokens/IsEnabled`
  - 更新设计文档 §7.2 配置表，注明 DB 优先于环境变量

**T0 验证标准**：
- [ ] `WorkflowExecutionLog` 实体有 `Status` 字段
- [ ] DB 表有 `status` 列
- [ ] `dotnet build YZH.Core` 无新增错误
- [ ] `dotnet test YZH.Core.Tests` 42/42 通过

---

## S1：LLM Gateway（1 天）

> **目标**：建立模型无关的 LLM 调用网关，支持 qwen/ollama/mock 三 Provider，含重试/熔断/信号量。

- [ ] **S1-1** 新建 `YZH.Core/AI/Clients/ILlmClient.cs`
  - 接口：`CompleteAsync(LlmRequest, CancellationToken)` → `Task<LlmResponse>`
  - 属性：`string ActiveProvider { get; }`
  - 含完整 XML 注释
- [ ] **S1-2** 新建 `YZH.Core/AI/Clients/ILlmProvider.cs`
  - 接口：`string Name { get; }` + `ChatAsync(LlmRequest, CancellationToken)`
- [ ] **S1-3** 新建 `YZH.Core/AI/Clients/Models/LlmRequest.cs`
  - 字段：Provider/Model/Messages/Temperature/MaxTokens/JsonMode/TimeoutSeconds
  - 默认值：Provider="qwen", Model="qwen-turbo", Temperature=0.1, MaxTokens=4096
- [ ] **S1-4** 新建 `YZH.Core/AI/Clients/Models/LlmResponse.cs`
  - 字段：Success/Content/RawJson/PromptTokens/CompletionTokens/DurationMs/Provider/Model/Error
- [ ] **S1-5** 新建 `YZH.Core/AI/Clients/Models/LlmMessage.cs`
  - 字段：Role/Content
- [ ] **S1-6** 新建 `YZH.Core/AI/Clients/QwenApiProvider.cs`
  - 端点：`https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions`
  - 鉴权：Bearer Token（优先环境变量 `AI_QWEN_API_KEY`）
  - 失败抛 `LlmCallException`（含 HTTP 状态码）
- [ ] **S1-7** 新建 `YZH.Core/AI/Clients/OllamaProvider.cs`
  - 端点：`http://localhost:11434/api/chat`
  - 失败抛 `LlmCallException(IsUnreachable=true)`
- [ ] **S1-8** 新建 `YZH.Core/AI/Clients/MockProvider.cs`
  - 返回固定 JSON，不消耗 token
- [ ] **S1-9** 新建 `YZH.Core/AI/Clients/LlmClient.cs`
  - Provider 路由：按 `request.Provider` 或 `ActiveProvider` 兜底
  - 全局信号量：`SemaphoreSlim`（默认并发 2，可配 `Ai:MaxConcurrency`）
  - 指数退避重试：429/5xx/Timeout → 1s/3s/7s（最多 3 次）
  - 熔断：连续失败 5 次 → 30s 内快速失败（`IsUnreachable=true`）
  - 降级链：当前 Provider 失败 → 自动切下一个（qwen→ollama→mock）
- [ ] **S1-10** 新建 `YZH.Core/AI/Clients/LlmCallException.cs`（领域异常）
  - 继承 `Exception`，含 `IsTimeout/IsUnreachable` 属性
- [ ] **S1-11** 修改 `YZHModule.cs`：注册 3 Provider + LlmClient（`InstancePerLifetimeScope`）
- [ ] **S1-12** 新建测试 `S1_LlmClientTests.cs`：
  - 路由正确性（qwen/ollama/mock 三 Provider）
  - 未知 Provider 抛 `LlmCallException`
  - MockProvider 固定 JSON 解析
  - 信号量并发（Task.WhenAll 10 次无死锁）
- [ ] **S1-13** 验证：
  - [ ] `dotnet build YZH.Core` 无错误
  - [ ] `dotnet test` 新测试全部通过
  - [ ] 启动日志打印 `ActiveProvider = qwen`

---

## S2：SkillRegistry + 内置 Skill（1 天）

> **目标**：建立 Skill 注册表 + 6 个内置 Skill 实现，作为工作流引擎的节点库。

- [ ] **S2-1** 新建 `YZH.Core/Workflow/ISkillNode.cs`
  - 属性：`string SkillCode { get; }`
  - 方法：`Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct)`
- [ ] **S2-2** 新建 `YZH.Core/Workflow/SkillContext.cs`
  - 字段：Inputs/WorkflowInstanceId/NodeId/Logger
- [ ] **S2-3** 新建 `YZH.Core/Workflow/SkillResult.cs`
  - 字段：Success/Outputs/Confidence/Error/DurationMs
- [ ] **S2-4** 新建 `YZH.Core/Workflow/ISkillRegistry.cs`
  - 方法：`Get(string)` / `RegisterAsync(ISkillNode)` / `UnregisterAsync(string)` / `AllCodes()`
- [ ] **S2-5** 新建 `YZH.Core/Workflow/SkillRegistry.cs`
  - 实现：`ConcurrentDictionary<string, ISkillNode>` + `ILogger`
  - 启动日志：注册时打印 SkillCode
- [ ] **S2-6** 新建 `YZH.Core/Skills/DocumentExtractSkill.cs`
  - SkillCode = `"document_extract"`
  - 包装 `IFileExtractor`，处理 `convertStatus` 状态机（pending/failed/converted）
  - 输出：`fields/tables/full_text/effective_path/is_converted_version`
- [ ] **S2-7** 新建 `YZH.Core/Skills/LlmExtractSkill.cs`
  - SkillCode = `"llm_extract"`
  - 依赖 `ILlmClient` + `IPromptInterpreter`（S3 接口先定义，实现后注入）
  - 最多 2 次 LLM 调用（JSON 解析失败重试 1 次）
  - 输出：`fields/tables/raw_json`，`Confidence` = fields 最低置信度
- [ ] **S2-8** 新建 `YZH.Core/Skills/CompareSkill.cs`
  - SkillCode = `"compare"`（含 `date_diff` 作为别名）
  - 确定性计算，无外部依赖
- [ ] **S2-9** 新建 `YZH.Core/Skills/GetFieldSkill.cs`
  - SkillCode = `"get_field"`
  - 依赖 `VOLContext`，按 `label_tag` 查 `ent_extraction_result`
- [ ] **S2-10** 新建 `YZH.Core/Skills/GetTableSkill.cs`
  - SkillCode = `"get_table"`
  - 依赖 `VOLContext`，按 `table_code` 查 `ent_table_extraction_result`
- [ ] **S2-11** 新建 `YZH.Core/Skills/AssembleSkill.cs`
  - SkillCode = `"assemble"`
  - 字符串拼接，无外部依赖
- [ ] **S2-12** 修改 `YZHModule.cs`：注册 `SkillRegistry` + 6 个 Skill 实现
- [ ] **S2-13** 新建测试 `S2_SkillRegistryTests.cs`：
  - 注册/查询/覆盖/注销/并发注册（100 次）
- [ ] **S2-14** 新建测试 `S2_LlmExtractSkillTests.cs`：
  - MockProvider 固定 JSON → Outputs.fields 正确，Confidence 计算正确
- [ ] **S2-15** 验证：
  - [ ] `dotnet build YZH.Core` 无错误
  - [ ] `dotnet test` 全部通过
  - [ ] `SkillRegistry.AllCodes()` 含 6 个 SkillCode

---

## S3：PromptInterpreter（1 天）

> **目标**：提示词渲染 + JSON 结构化解析，纯字符串层，无外部依赖。

- [ ] **S3-1** 新建 `YZH.Core/AI/Prompt/IPromptInterpreter.cs`
  - 方法：`Render(string template, IDictionary<string, object> context)`
  - 方法：`ParseAsync<T>(string llmOutput, CancellationToken ct)`
- [ ] **S3-2** 新建 `YZH.Core/AI/Prompt/Models/RenderContext.cs`（继承 `Dictionary<string, object>`）
- [ ] **S3-3** 新建 `YZH.Core/AI/Prompt/Models/ParseResult.cs`
  - 字段：Success/Value/Error/RawText
- [ ] **S3-4** 新建 `YZH.Core/AI/Prompt/PromptInterpreter.cs`
  - `Render`：`{name}` 占位符替换，缺失占位符保留原样
  - `ParseAsync`：剥离 `` ```json `` 围栏 → 兜底取首个 `{...}` 子串 → `System.Text.Json` 反序列化
  - 非法 JSON 返回 `Success=false` + Error 消息
- [ ] **S3-5** 新建 `YZH.Core/AI/Plan/AiPlan.cs`
  - 字段：PlanName/Steps/AwaitOutputMapping
- [ ] **S3-6** 新建 `YZH.Core/AI/Plan/AiStep.cs`
  - 字段：Order/SkillCode/Params
- [ ] **S3-7** 新建 `YZH.Core/AI/Plan/AiPlanParser.cs`
  - 解析 LLM 返回的 plan JSON → `AiPlan` 强类型
- [ ] **S3-8** 新建测试 `S3_PromptInterpreterTests.cs`：
  - Render 占位符替换（字符串/对象/缺失保留）4 用例
  - Parse JSON 围栏剥离（围栏/纯JSON/杂讯/非法）4 用例
- [ ] **S3-9** 新建测试 `S3_AiPlanParserTests.cs`：
  - plan JSON → AiPlan 解析，steps 顺序正确
- [ ] **S3-10** 验证：
  - [ ] `dotnet build YZH.Core` 无错误
  - [ ] `dotnet test` 全部通过（S3 新增 10 用例）

---

## S4：WorkflowEngine + 留痕（1.5 天）

> **目标**：轻量工作流解释器（线性管道 + 条件分支）+ F-04 执行日志。

- [ ] **S4-1** 新建 `YZH.Core/Workflow/Models/WorkflowConfig.cs`
  - 字段：Nodes/Edges/Branches/OutputConfig
- [ ] **S4-2** 新建 `YZH.Core/Workflow/Models/WorkflowNode.cs`
  - 字段：NodeId/SkillCode/Inputs/Output
- [ ] **S4-3** 新建 `YZH.Core/Workflow/Models/WorkflowEdge.cs`
  - 字段：From/To
- [ ] **S4-4** 新建 `YZH.Core/Workflow/Models/BranchConfig.cs`
  - 字段：From/Condition/Then
- [ ] **S4-5** 新建 `YZH.Core/Workflow/Models/BranchCondition.cs`
  - 字段：Field/Op/Value（op 枚举：equals/not_equals/gt/gte/lt/lte/truthy）
- [ ] **S4-6** 新建 `YZH.Core/Workflow/IWorkflowEngine.cs`
  - 方法：`RunAsync(string workflowConfigJson, WorkflowContext context, CancellationToken ct)`
- [ ] **S4-7** 新建 `YZH.Core/Workflow/WorkflowContext.cs`
  - 字段：WorkflowInstanceId/BusinessType/BusinessCode/Inputs/LogStore
- [ ] **S4-8** 新建 `YZH.Core/Workflow/WorkflowRunResult.cs`
  - 字段：Success/NodeOutputs/FailedNodeId/Error/DurationMs
- [ ] **S4-9** 新建 `YZH.Core/Workflow/IExecutionLogStore.cs`
  - 方法：`WriteAsync(ExecutionLogEntry, ct)` / `QueryByInstanceAsync(string workflowCode, ct)`
- [ ] **S4-10** 新建 `YZH.Core/Workflow/ExecutionLogEntry.cs`
  - 字段：WorkflowCode/WorkflowVersion/BusinessType/BusinessCode/NodeId/SkillCode/InputDataJson/OutputDataJson/Status/ErrorMsg/DurationMs/StartedAt/CompletedAt
- [ ] **S4-11** 新建 `YZH.Core/Workflow/InMemoryExecutionLogStore.cs`（单测用）
- [ ] **S4-12** 新建 `YZH.Core/Workflow/ExecutionLogStoreEf.cs`
  - 依赖 `VOLContext`，写入 `wf_workflow_execution_log` 表
  - 使用 EF LINQ（非 EFsql）
- [ ] **S4-13** 新建 `YZH.Core/Workflow/WorkflowEngine.cs`
  - `TopoSort`：Kahn 算法 + 环检测（主 edges + branches.then 全集）
  - `ResolveInputs`：`{{nX.port}}` 模板求值
  - `MatchCondition`：按 op 比较 from 节点输出端口值
  - 每节点执行后写 F-04 留痕（失败不阻断）
- [ ] **S4-14** 修改 `YZHModule.cs`：注册 `WorkflowEngine` + `InMemoryExecutionLogStore`（单测）/ `ExecutionLogStoreEf`（生产）
- [ ] **S4-15** 新建测试 `S4_WorkflowEngineTests.cs`：
  - 线性管道 3 节点按序执行，输出按端口正确传递
  - 条件分支 condition=true 走 then；false 跳过
  - 环检测抛 `WorkflowExecutionException`
  - 未知 Skill 抛 `UnknownSkillException`
  - F-04 留痕：每次节点执行有对应 log 记录
- [ ] **S4-16** 验证：
  - [ ] `dotnet build YZH.Core` 无错误
  - [ ] `dotnet test` 全部通过（S4 新增 10+ 用例）
  - [ ] F-04 表结构与设计文档 §8.2 一致

---

## S5：接入验证场（2 天）

> **目标**：将四件套接入 `DocExtractionRuleService`，打通上传→提取→LLM→落库完整链路。

- [ ] **S5-1** 修改 `DocExtractionRuleService.cs` 私有方法改真：
  - `GetFileInfoAsync`：从 `StandardDirectoryFile` 按 `fileCode` 查 `StoragePath`
  - `ExtractDocumentContentAsync`：调用 `IFileExtractor.ExtractAsync(filePath)` 取 `FullText`
  - `CallAIForAnalysisAsync`：通过 `WorkflowEngine` 调用 `LlmExtractSkill`（analyze 模式 prompt）
  - `CallAIForExtractionAsync`：通过 `WorkflowEngine` 调用 `LlmExtractSkill`（extract 模式 prompt）
- [ ] **S5-2** 新建 `DocExtractionController.cs`（Partial Controller）
  - `GET /api/doc-extraction/files/tree`：返回 MinIO 文件目录树
  - `GET /api/doc-extraction/files/{fileCode}/content`：返回文件全文（IFileExtractor）
  - `POST /api/doc-extraction/rules/{ruleId}/analyze`：AI 推荐字段/表格
  - `POST /api/doc-extraction/rules/{ruleId}/generate-prompt`：渲染 Prompt 模板
  - `POST /api/doc-extraction/rules/{ruleId}/verify`：真实调用 LLM，返回 sample_data
  - `POST /api/doc-extraction/rules/{ruleId}/save`：保存规则 + 字段/表格定义
  - 所有接口返回 `JsonNormal(...)`（非标准getPageData）
- [ ] **S5-3** 实现运行期提取链路：
  - 上传文件 → `IFileExtractor` → `DocumentExtractSkill` → `LlmExtractSkill`
  - LLM 输出解析 → 落 `ent_extraction_result`（B-08）+ `ent_table_extraction_result`（B-09）
  - 旧版 Office `convertStatus=pending` → 返回友好提示；`convertStatus=converted` → 走 `convertedStoragePath`
- [ ] **S5-4** 低置信度人工复核标记：
  - `confidence < 0.8` → 前端红标 + `is_manual_edited=false`（待复核）
  - 人工修改 → `is_manual_edited=true`
- [ ] **S5-5** F-04 留痕闭环：
  - 一次完整提取产生可查的 `wf_workflow_execution_log` 记录
  - 含 condition 未命中的 skipped 分支条目
- [ ] **S5-6** 前端 4 按钮接真（`index.vue`）：
  - analyze/generate-prompt/verify/save 统一走 `this.$http.yzPost`
  - DocPreview + 提取结果 Tab 联调无刷新
- [ ] **S5-7** 旧版 Office 转换联动端到端验证：
  - 上传 `.xls` → convertStatus pending→converted → DocPreview 预览转换后 `.xlsx`
  - analyze 按钮走 `DocumentExtractSkill` 的 converted 分支 → B-08/B-09 落库
- [ ] **S5-8** 更新文档：
  - 生成 `docs/70-当前执行/YZH-AI引擎-实施记录-V1.md`
  - 将 `YZH-AI引擎详细设计-V1.md` 状态更新为"已实施"，移入 `docs/20-架构决策/`

**S5 验证标准**：
- [ ] Postman 可跑通 6 个 API
- [ ] 样例 docx 端到端：B-08 每条 field 一条记录、confidence∈[0,1]
- [ ] convertStatus=pending 时返回"正在转换中"提示
- [ ] F-04 有对应执行日志

---

## 已完成状态记录

| 日期 | 完成内容 | 验证方式 |
|------|----------|----------|
| 2026-08-11 | 设计文档 V1.2 阅读 + 现状核实 | 代码核实 |
| 2026-08-11 | 构建基线确认：YZH.Core 0 错误 / 42/42 测试通过 | `dotnet build` + `dotnet test` |
| 2026-08-11 | 数据库容器确认：MySQL(3307) + Redis(6380) 运行中 | `docker ps` |
| 2026-08-11 | AIConfig 实体确认：`cert_ai_config` 表存在 | 代码核实 |
| 2026-08-11 | WorkflowExecutionLog 字段差异确认：缺 Status / BusinessId 类型 | 代码核实 |
| 2026-08-11 | S1 LLM Gateway 完成：9个文件，7个测试 | 构建0错误 / 49/49测试通过 |
| 2026-08-11 | S2 SkillRegistry+6个Skill完成：12个文件，8个测试 | 构建0错误 / 57/57测试通过 |
| 2026-08-11 | S3 PromptInterpreter完成：8个文件，10个测试 | 构建0错误 / 67/67测试通过 |
| 2026-08-11 | S4 WorkflowEngine完成：9个文件，7个测试 | 构建0错误 / 74/74测试通过 |
| 2026-08-11 | S5 接入完成：DocExtractionRuleService.AI.cs + Controller扩展 | VOL.WebApi构建0错误 |

---

## 待讨论事项（实施过程中随时暂停讨论）

| # | 问题 | 影响 | 待确认 |
|---|------|------|--------|
| D1 | `WorkflowExecutionLog.BusinessId` 当前为 `long`，设计文档要求 `bigint FK→F-03.id` 还是改用 `business_code VARCHAR(36)`？ | S4 实体对齐 | 用户确认 |
| D2 | `AIConfig.ApiKey` 目前明文存储，设计文档建议环境变量优先，是否保留 DB 明文作为兜底？ | S1 鉴权实现 | 用户确认 |
| D3 | `StandardDirectoryService.GetFileInfoAsync` 返回类型 `object`，实际应返回 `StandardDirectoryFile`，是否需要重构？ | S5 接入层 | 用户确认 |
| D4 | F-04 `workflow_id` 在数据库设计-V2 中是 `bigint FK→F-03.id`，但实体用 `WorkflowCode string`，是否需要改为 FK？ | 表结构对齐 | 用户确认 |

*（内容由AI生成，仅供参考）*
