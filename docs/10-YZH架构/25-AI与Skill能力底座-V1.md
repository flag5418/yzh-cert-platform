# AI / Skill 能力底座 — 现状盘点与建设路线（V1）

> **版本**：V1 | **日期**：2026-10-03 | **状态**：调研结论 + 路线，待决策
> **定位**：用户 2026-10-03 定调 ——「这是最后一个短板，就是我们的 ai、skill 能力，我们的很多功能都需要这个」
> **关联**：[`36-企业原始资料管理设计-V1.md`](../20-体系认证/03-详细设计/05-企业资料规范化/36-企业原始资料管理设计-V1.md) §十 L7 · [`37-特定证件文档识别方案-V1.md`](../20-体系认证/03-详细设计/05-企业资料规范化/37-特定证件文档识别方案-V1.md)

---

## 〇、一句话结论

**Skill 骨架已成（16 个），LLM 调用也收敛到唯一出口（8 个调用点 / 1 个 HTTP 通道）** —— 这两块不用重做。
真正的短板是**三处「零」**：

| 零 | 现状 | 证据 |
|---|---|---|
| **零视觉** | 图片/扫描件**无任何** OCR 或视觉能力 | `DefaultOcrProvider.cs:30` `IsAvailable => false`；`LlmInvokeService.cs:98-111` messages content 恒为 `string`，表达不了多模态数组 |
| **零费用可见** | `cert_ai_usage_log.CostUsd` **恒为 0**；前端「AI 费用分析」显示的 `$0.0000` 是假的 | 两处写入点都没给 `CostUsd` 赋值；注释自承「全仓无单价表」（`PromptWorkbenchService.cs:1241`） |
| **零韧性** | 8 个 LLM 调用点里 **7 个零重试**；无熔断、无 Polly、无退避、429 不与 5xx 区分 | `LlmInvokeService.cs:137` 单次 `SendAsync`；grep `Polly\|AddPolicyHandler` = **0 命中**；`CreateClient("LlmInvoke")` 这个命名客户端**从未被配置** |

外加一块**僵尸资产**：报告生成、NC 判定、文档填写三条链路的 AI 能力**设计上写了、代码里没有**（详见 §五）。

---

## 一、能力矩阵（实测，非文档）

### 1.1 模型能力（2026-10-03 真调 API 实测）

| 模型 | 纯文本 | 传图识别 | 图片→Markdown | 实测 tokens |
|---|---|---|---|---|
| **`qwen-flash`**（当前 `ai_model_name`） | ✅ | ⛔ **静默忽略**（HTTP 200，回答「请提供图片」） | ⛔ | — |
| `qwen-vl-max` | ✅ | ✅ | ❌ | 1469 |
| **`qwen3-vl-flash`** | ✅ | ✅ | ✅（提示词 `qwenvl markdown`） | **2010**（image 1874） |
| `qwen3-vl-plus` | ✅ | ✅ | ✅（带表格结构） | 2029 |

⚠️ **`qwen-flash` 的失败模式是最危险的一种**：不报错、返回正常回答，但图片被丢弃。代码里若不显式区分，会一直以为「AI 已经在看图了」。

> ✅ **官方能力**：Qwen3-VL 文档明确支持把扫描件/图片 PDF 解析为 Markdown，推荐提示词就是 `qwenvl markdown`。这意味着**「所有格式统一成 Markdown」是可达成的**（详见 37 号）。

### 1.2 Skill 体系（DB 15 行 / 代码 16 类）

| 分类 | Skill | 数量 | 真调 LLM？ |
|---|---|---|---|
| `ai_generate` | `llm_extract` | 1 | ✅ **全项目唯一** |
| `ai_judge` | `compare` / `nc_conclusion_calc` / `nc_validate` / `is_field_empty` / `is_table_empty` / `is_std_file_missing` / `build_nc_prompt` | 7 | ❌ **全是纯确定性代码** |
| `data_access` | `get_field` / `get_table` | 2 | ❌ |
| `data_process` | `assemble` | 1 | ❌ |
| `doc_src_scalar` | `src_global_param` / `src_manual` | 2 | ❌ |
| `doc_fill` | `fill_cell` / `fill_table` | 2 | ❌ |

⚠️ `ai_judge` 分类已**失去信息量**（7 个里 0 个调 AI），是分类错配而非能力缺失。

### 1.3 LLM 调用底座能力矩阵

| 能力 | 支持 | 证据 |
|---|---|---|
| `response_format: json_object` | ✅ | `LlmInvokeService.cs:113-121` |
| JSON 截断抢救（括号栈补齐 / 围栏剥离） | ✅ | `:223-270` |
| `temperature` / `max_tokens` / `system_prompt` | ✅ | `:113-128` |
| **多模态 `image_url`** | ❌ | `:98-111` content 恒 `string` |
| **流式** | ❌ | `:137` 非 `ResponseHeadersRead`；grep `stream` = 0 |
| **重试 / 退避 / 熔断** | ❌ | 8 处调用只有 `LlmExtractSkill.cs:59-88` 有 2 次（且是格式纠偏非错误重试） |
| **429 与 5xx 区分** | ❌ | `:141-151` 只看 `IsSuccessStatusCode`，错误信息截 300 字符 |
| **工具调用 / Function Calling** | ❌ | grep `tool_calls` = 0 |
| **思考模式 `enable_thinking`** | ❌ | 请求体无此参数 |
| 超时可配置 | ❌ | `TimeoutSeconds = 180` 硬编码（`:69`），无配置键、无 per-request |
| 多模型路由 | ❌ | 8 处全用 `settings.Model`；`AiNodeExecutor.cs:338` 有 `node.config.model` 入口但生产配置里没有该键 |

### 1.4 依赖 AI 的业务功能（16 项）

| 状态 | 功能 |
|---|---|
| ✅ **AI 真在跑** | 文档提取规则 AI 分析 / Prompt 验证 / 企业资料实体抽取 / 企业原始资料 doc_group·doc_content / 提示词工作台生成·试跑 / 工作流 ai_node |
| ⚠️ **降级运行** | NC 检查结论（生产工作流只接了 `docField→ai_node(直接输出)→end`，**未接** `build_nc_prompt`/`nc_conclusion_calc`/`nc_validate`） |
| ❌ **AI 能力为零** | **报告生成**（`cert_report_section.WorkflowConfig` 实测 2 条全为空 ⇒ 恒走 `degraded`，用模板示例正文，代码注释自承「⛔ 它不是 AI 生成的初稿」）；**文档填写 AI**（`cert_doc_fill_prompt` 有 2 行 v1/v2，但 grep `DocFillPrompt` **0 处业务消费**） |
| ❌ **无能力** | 图片/扫描件 OCR；embedding / 向量检索（grep `embedding\|vector\|milvus\|faiss` = **0 命中**，文档召回只靠编号前缀 + 标题关键词） |

---

## 二、缺口清单（按严重度）

### 🔴 P0 — 阻断生产可用

| # | 缺口 | 落点 | 建议动作 | 人日 |
|---|---|---|---|---|
| **P0-1** | **零视觉能力** | `DefaultOcrProvider.cs:30`（空实现）+ `LlmInvokeService.cs:98-111`（表达不了多模态） | ① `LlmInvokeRequest` 加 `Images: List<byte[]>`，`messages.content` 改数组形态<br>② `cert_sys_config` 加 `ai_model_vision`（默认 `qwen3-vl-flash`）<br>③ `DefaultOcrProvider` 接 VLM + `qwenvl markdown`<br>④ **多页 PDF 需先栅格化**（`DefaultOcrProvider.cs:20-22` 已记录：`soffice --convert-to png` 只能出第 1 页，需给 libreoffice 镜像加 poppler-utils） | 3 |
| **P0-2** | **费用不可见**（`CostUsd` 恒 0） | 新增 `cert_ai_model_price`（Model + 生效时间 + 输入/输出单价）；两处写入点补赋值 | 让「AI 费用分析」从假 0 变真 | 1 |
| **P0-3** | **零韧性** | `LlmInvokeService` + `CertPlatformAdminServiceExtensions.cs:29` 裸 `AddHttpClient()` | ① 抽 `IAiClient` 接口；② `AddHttpClient("LlmInvoke").AddStandardResilienceHandler()`（.NET 8 内置，**不需要 Polly**）；③ 429 读 `Retry-After`、5xx 指数退避；④ 超时读配置；⑤ **错误码枚举替代 `error.Contains("超时")` 字符串匹配**（`AiNodeExecutor.cs:169`） | 2–3 |
| **P0-4** | **ai_node 结构化输出不稳定** | `AiNodeExecutor.cs:361` `ForceJson=false`；`:691-702` `TryParseNumber` 用 `TakeWhile` 抽前缀数字 | ⛔ **「抽取 5 分」会静默变成 `5`、零报错**。① 加 `OutputSchema` 列；② 按需开 `ForceJson`；③ 实现设计文档要求的 N 次纠偏重试（当前零实现） | 2–3 |
| **P0-5** | **ai_node 用量不入账** | `AiNodeExecutor.cs:200-206` 只填节点 token，不写 `cert_ai_usage_log` | 实测 18 次 ai_node 调用全部游离账外；表加 `UserCode`/`OrgCode` | 1 |
| **P0-6** | **API Key 明文入库** | `cert_sys_config.ai_api_key`；3 个备份 SQL 含明文 | 迁环境变量或 DPAPI；清理备份；`cert_ai_config.ApiKey` 注释承诺的「加密存储」要么实现要么改注释 | 1 |
| **P0-7** | **无多模型路由** | `build_nc_prompt` 输出的 `model`/`temperature`/`max_tokens` 是**死输出**（grep 无下游） | 加 `ai_model_fast` / `ai_model_reasoning` / `ai_model_vision` 三键并真正接进调用层 | 2 |

### 🟠 P1 — 影响质量

| # | 缺口 | 落点 | 人日 |
|---|---|---|---|
| P1-1 | **报告生成 AI 能力为零** | `cert_report_section.WorkflowConfig` 2 条全空 ⇒ 恒 `degraded` | 3 |
| P1-2 | **NC 链路没真正用上 Skill** | 生产 `RuleJson` 未接 `build_nc_prompt`/`nc_conclusion_calc`/`nc_validate`；`NcValidateSkill.cs:175-182` C2 维度校验是**空实现**；`:156` C5 依赖的 `wf_node_execution.SourceFileCode` 恒 NULL ⇒ 自动跳过 | 3 |
| P1-3 | **文档填写 AI 未实现** | `cert_doc_fill_prompt` 有 2 行但 0 消费；`src_semantic`/`src_ai_field`/`src_ai_table`/`src_dict` 4 个 Skill 未实现 | 4 |
| P1-4 | **提示词无版本管理** | `wf_prompt_template.Version` 列存在但前端契约已删（`prompt-workbench.ts:66-67`「⛔ 不做版本管理」），保存即覆盖 ⇒ **历史结论无法回溯所用提示词** | 1 |
| P1-5 | **提示词无机构级隔离** | `ResolveActiveAsync`（`:193-205`）只按 `StandardCode` 两层回退，无 `OrgCode` ⇒ 多机构共用一条 | 1 |
| P1-6 | **三份重复的 `AiSettings`** | `DocExtractionRuleService.AI.cs:132` / `AiNodeExecutor.cs:66` / `PromptWorkbenchService.cs:1212` 各自 `new` + 各自 `foreach`，默认值还不一致 | 抽 `IAiSettingsProvider` |
| P1-7 | **总开关名不副实** | `ai_extract_enabled` 只有文档提取读（`AI.cs:144`）；工作流 ai_node 与提示词工作台照跑照计费 | 0.5 |
| P1-8 | **死配置 `cert_ai_config`** | 1 行且值与实际生效**冲突**（`qwen-turbo/0.7/4096` vs 实际 `qwen-flash/0.2/32768`），**不参与任何调用**，但页面上看得见 | 删表或接线 |
| P1-9 | **Skill 参数不支持数组** | `SkillExecutor.cs:252-269` `ConvertValue` 只处理标量 ⇒ 数组必须序列化成 JSON 字符串 | 1 |
| P1-10 | **Skill 错误语义反了** | `SkillExecutor.cs:52/60/68/78/108` 五处错误路径返回 `SkillResult.Ok()`（`Success=true` + `error` 非空） | 0.5 |
| P1-11 | **Skill 契约未被强制** | `wf_skill.OutputStrict`/`ReturnType` 列存在，`SkillExecutor.cs:86-103` **完全不读** | 1 |
| P1-12 | **无 embedding / 语义检索** | 全仓 0 命中 ⇒ 召回只靠编号前缀 + 标题关键词 | 5 |
| P1-13 | **Skill 体系文档严重漂移** | `docs/30-项目规则/Skill清单-V1.md:277-292` 称「已建立 2 个」、`get_field`/`get_table` 已移除、`LlmExtractSkill` 是「臆想的」—— 与 DB 15 行 / 代码 16 类全部矛盾 | 0.5 |

### 🟡 P2 — 体验与工程化

`PromptRenderer` 零截断保护（设计要求 2000 字符截断）· 无流式（用户干等 6–11s）· 节点输出 64KB 静默截断 · 路径级串行（线性放大延迟）· `InvalidateCache` 无调用方（改 Skill 后 5 分钟才生效）· 超时硬编码 180s · 无 token 预算感知 · `ai_judge` 分类失真 · `wf_skill_api`/`wf_skill_category`/`cert_doc_ai_suggestion` 孤儿表

---

## 三、建议路线（三阶段）

```
阶段一「能看」 ──────────────────────────────── 2 周
  P0-1  视觉能力：LlmInvokeService + image_url + qwen3-vl-flash + qwenvl markdown
  P0-2  费用可见：cert_ai_model_price + CostUsd 真实填充
  P0-3  零韧性 → 有韧性：IAiClient + 内置 Resilience + 429/5xx 分类 + 错误码枚举
  P0-6  API Key 治理
  ➜ 交付：图片/扫描件能进 Markdown；费用报表说真话；LLM 抖动不炸链路

阶段二「可控」 ──────────────────────────────── 3 周
  P0-4  ai_node 结构化输出契约（Schema + 纠偏重试 + 禁静默数值转换）
  P0-5  ai_node 用量入账 + 用户/机构维度
  P0-7  多模型路由（fast / reasoning / vision 三键）
  P1-4  提示词版本管理（可回溯「这条结论用哪版提示词」）
  P1-6  抽 IAiSettingsProvider（消除三份重复）
  P1-7/P1-8  总开关接线 + 删死配置
  ➜ 交付：输出稳定、成本可控、结论可追溯

阶段三「成能力」 ──────────────────────────────── 4 周
  P1-1  报告生成接 AI（补 WorkflowConfig）
  P1-2  NC 链路接 Skill（补 C2 维度校验、修 C5）
  P1-3  文档填写接 AI（src_semantic / src_ai_field / src_ai_table）
  P1-12 embedding 语义检索
  P1-9/P1-10/P1-11  Skill 参数 / 错误语义 / 契约强制
  ➜ 交付：三条「设计有代码无」的链路真正落地
```

---

## 四、「特定格式证件」在底座里的位置（承接 37 号）

阶段一的 **P0-1 视觉能力**就是 37 号的前置条件。有了它，链路变成：

```
任意文件 ─┬─ 有文本层（Word/文本PDF）→ anydoc 直接出 Markdown ─┐
          └─ 无文本层（图片/扫描件PDF）→ qwen3-vl-flash        ├→ 统一 Markdown
               +「qwenvl markdown」              ─────────────┘
                                                            ↓
                                       ★ IDocumentKindJudge 只读 Markdown
                                         kind ∈ {营业执照,身份证,资质证书,…}
                                         ⇒ 固定格式证件 → 不设标签/作用（已完成）
                                         ⇒ 体系文件 → doc_group/doc_content
```

**判断器接口只需一个方法**（读 Markdown），不必设计视觉通道 —— 这正是用户设想的「拿到 Markdown 就扩展判断方法」。

⚠️ **实测踩到的坑（必须写进规范）**：让 VLM 同时输出 `kind`（枚举）和 `isFixedForm`（布尔）时，
`kind` 全对，但 **`isFixedForm` 全错**（质量手册/内审计划都被判成 `true`）。

⇒ **规范**：⛔ 判断器**只允许 LLM 从固定枚举里选 `kind`**，**派生布尔一律放代码判定**：

```csharp
IsFixedForm = kind is "business_license" or "id_card" or "iso_cert";   // 代码，不由模型判
```

---

## 五、「僵尸资产」清单（设计有、代码无）

| 资产 | 现状 | 证据 |
|---|---|---|
| **报告生成 AI** | 2 条章节 `WorkflowConfig` 全空 ⇒ 恒 `degraded` 用模板正文 | `ExpertItemExecutors.cs:455` 注释「⛔ 它不是 AI 生成的初稿」 |
| **NC 判定 Skill 链** | 生产 `RuleJson` 只接 `docField→ai_node(直接输出)`，3 个 Skill 未接 | `cert_validation_rule` 唯一有配置的规则，1416 字符 4 节点 |
| **NC C2 维度校验** | 空实现 | `NcValidateSkill.cs:180-181` `return new HashSet<int>()` |
| **NC C5 证据交叉** | 恒自动跳过 | `NcValidateSkill.cs:156` 自承依赖的列修复前恒 NULL |
| **文档填写 AI** | `cert_doc_fill_prompt` 2 行 v1/v2，0 处消费 | grep `DocFillPrompt` 只有 Controller + Entity |
| **4 个填写 Skill** | `src_semantic` / `src_ai_field` / `src_ai_table` / `src_dict` 未实现 | `phase13_doc_fill_skills.sql:14-17` 自列 |
| **`cert_ai_config`** | 1 行死配置，与生效值冲突，不参与调用 | 3 处引用全在「页面回显/保存」 |
| **`wf_skill_api`** | api 型 Skill 预留表，0 个 Skill 使用 | — |
| **`cert_doc_ai_suggestion`** | 0 行 0 引用 | — |
| **视觉能力** | 代码注释写「实测可用」，但**从未写进代码** | `DefaultOcrProvider.cs:14` |

---

## 六、文档纠错（顺带修）

| 位置 | 错误陈述 | 实际 |
|---|---|---|
| `docs/30-项目规则/Skill清单-V1.md:277-292` | 「已建立的 Skill（**2 个**）」、`get_field`/`get_table`「**已移除**」、`LlmExtractSkill`「**臆想的**」（`:34`,`:455-456`） | DB **15 行** / 代码 **16 类** / `llm_extract` 已 DI 注册并在跑 |
| `scripts/db/all_tables_ddl.sql:2783-2937` | 只有 `wf_skill_api/_category/_input/_output/_reflection` 的 DDL，**无 `wf_skill` 主表**；子表仍是 snake_case | ⛔ 该文件**不能作为 `wf_skill` 的事实源** |
| `scripts/db/20261002_prompt_workbench_V1.sql:412-417` | 把 `analyze_word`/`analyze_excel`/`analyze_pdf` 写成 `SKILL_TARGET` 字典项 | 库中**一条都没有** ⇒ 文档提取分析全部落到内嵌默认兜底 |
| `scripts/db/update_analyze_word_prompt_V1.sql:120` | `UPDATE ... WHERE PromptCode='analyze_word'` | 更新 0 行 |
| `docs/.../统一Skill执行器与AI-ToolUse设计-V1.md` | 设计了工具调用 | 代码 grep `tool_calls` = **0 命中** |
| `docs/.../AI提示词规则-功能设计-V1.md:157-161` | 要求「结构不符重试 N 次，禁止静默降级」 | 代码零实现，且 `TryParseNumber` **正在静默降级** |

---

## 七、待决策

| # | 问题 | 建议 |
|---|------|------|
| 1 | 阶段一是否**本迭代就做**（视觉 + 费用 + 韧性 + Key 治理，约 2 周）？ | 建议做。它同时解掉 37 号（证件识别）的前置 |
| 2 | 视觉模型选 `qwen3-vl-flash`（便宜）还是 `qwen-vl-max`（强）？ | **flash**。已实测够用（`qwenvl markdown` 出标准 Markdown），且图片量少 |
| 3 | 是否**现在就给图片加压缩**（长边 1600px）？ | 建议。图片按 **token** 计费，压缩直接省一半 |
| 4 | 多页扫描 PDF 要栅格化（给 libreoffice 镜像加 poppler-utils）—— 接受镜像变大吗？ | 建议做，否则多页证件只能识别第 1 页 |
| 5 | 阶段三的三条僵尸链路（报告 / NC / 文档填写）优先级如何排？ | 建议 **NC 判定**（认证主业务）> 报告生成 > 文档填写 |
| 6 | `cert_ai_config` 死配置：删表还是接线？ | 建议**删表**（`cert_sys_config` 已是唯一事实源，留着只会误导） |
