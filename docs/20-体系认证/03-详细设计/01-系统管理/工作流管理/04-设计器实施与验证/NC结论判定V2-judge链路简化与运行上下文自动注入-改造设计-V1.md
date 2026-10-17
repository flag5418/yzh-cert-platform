\---  
status: draft  
date: 2026-10-15  
author: AI 编码助手（依据用户 2026-10-15 提出的两个设计问题）  
supersedes: 无（增量设计，不改 2026-10-14 已裁决的「多 judge 分支 + 盖章 + 聚合」口径，只改其\*\*落地形态\*\*）  
related:  
  \- docs/20-体系认证/03-详细设计/01-系统管理/工作流管理/04-设计器实施与验证/AI节点参数与审计字段统一-改造设计-V1.md（同目录，本文沿用其「参数即边 / 白话说明书 / 分批落地」体例）  
  \- docs/20-体系认证/03-详细设计/01-系统管理/工作流管理/01-核心引擎/AI节点-详细设计-V2.md  
  \- src/certplatform-api/CertPlatform.Admin/Services/Workflow/Skills/StampJudgeLevelSkill.cs  
  \- src/certplatform-api/CertPlatform.Admin/Services/Workflow/Skills/CollectJudgesSkill.cs  
  \- src/certplatform-api/CertPlatform.Admin/Services/Workflow/Skills/NcConclusionV2Skill.cs  
\---

# NC 结论判定 V2 — judge 链路简化 与 运行上下文自动注入 · 改造设计 V1

> **本文档回答用户 2026-10-15 的两个问题**：  
> ① 「一条 judge 可能分 3 种情况（对应 verdict）⇒ 是不是要 3 个 `judge盖章级别`？2 条就要 6 个，再分 2 条路组织 3 个 `聚合多路judge`，复杂度非常高」  
> ② 「`NC结论判定v2(多judge)` 在后台测试、乃至专家端，机构编码应该是**自动带出**的」
>
> **结论（先给）：两个问题是同一个根因 —— 配置者被迫承担「系统本该自己知道」的信息。** 且第 ① 问里藏着一个**尚未被发现的引擎缺陷**（§1.2），它比「3 个节点」严重得多：**任何「多条路径汇聚到一个节点」的画布，汇聚节点都只看得见第一条路径的数据。** 换句话说，即使用户咬牙摆 3 个 `聚合多路judge`，也**收不齐**。

---

## 0. 白话说明书（给不看代码的人）

### 0.1 你现在遇到的两件事

**第一件：节点会越摆越多。**  
你在画布上给一条检查项盖章（比如「缺少审批签字 ⇒ critical」）。但你想：「这一项也可能**没问题**啊，那 verdict 就该是 pass；也可能**判不了**（材料没交），那就是 unverifiable。三种情况怎么办？难道摆 3 个盖章节点？」

摆 3 个之后，两条检查项就是 6 个；然后这些节点还要**汇总**到一个「聚合」节点里，再进「结论判定」。你还发现分支后要「分 2 条路」，于是「聚合」节点也得跟着分 2 个……越摆越乱。

**第二件：机构编码要手填。**  
`NC结论判定v2(多judge)` 有个 `org_code` 参数。你现在的做法是手打一串 GUID（`906e8b2a…`）。但机构是**当前这次检查自己就知道**的事 —— 后台测试时你知道，专家端跑任务时更知道。让你手抄一遍，既没必要、又容易抄错（抄错了不报错，只是悄悄换了另一套判定标准）。

### 0.2 改完之后是什么样

**关于节点数**：一条检查项 = **1 个判定节点 + 1 个盖章节点**，永远这么两个，**不管它有三种还是三十种结果**。原因是「结论」这件事改成**从上一条连线流进来**（叫 `verdict` 的入线），而不是在盖章节点里写死。

而且**不再需要「聚合」节点**，也**不再需要把 N 个盖章节点的线都拉到一个点**。系统的做法变成：**谁盖了章，就自动记到这次检查的「判定清单」里**；「结论判定」节点直接读这张清单。所以：

```
现在（每条检查 2 个节点 + 1 个聚合 + 1 个结论）：
  N 条检查 ⇒ N 个判定 + N 个盖章 + 1 个聚合 + 1 个结论 = 2N + 2 个节点，且有 N 条线汇到一个点

改完（判定自动登记）：
  N 条检查 ⇒ N 个判定 + N 个盖章 + 1 个结论 = 2N + 1 个节点，且**没有汇聚线**
```

**关于机构编码**：`org_code`、`standard_code`、`phase_code` 这三个框**从画布上消失**。它们不再由你填，而是由引擎在开跑前**自动装进「本次运行的上下文」**，节点自己去取。机构编码的来源顺序是：**规则上挂的机构 → 企业归属的机构 → 当前登录人的机构 → 空（平台标准级）**。

### 0.3 一个必须先说的坏消息（§1.2）

你担心的「分 2 条路要摆 3 个聚合节点」——**其实摆 3 个也没用**。我查了引擎，它有一个**跨路径复用**机制：同一个节点如果出现在两条路径上，**第二条路径不会再执行它，直接拿第一条路径的结果**。

举例：画布是 `A → 汇总` 和 `B → 汇总` 两条路径。

- 跑第一条：`A` 执行，`汇总` 执行（只看到 A）。
- 跑第二条：`B` 执行，`汇总` **被复用**（还是只有 A 的结果）。

⇒ **B 的数据永远到不了「汇总」。** 这是引擎级缺陷，**不修它，任何「多路汇聚」的画布都是假的**。本方案（判定池）恰好绕开了这个问题：既然不需要汇聚节点，也就没有这个问题。

---

## 1. 现状取证（全部带文件:行号）

### 1.1 参数绑定只有两条路（已穷尽，无第三条）

| 事实                                                                            | 证据                                                                                                   |
| ----------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| `SkillContext` 只有 4 个字段：`Inputs` / `WorkflowInstanceId` / `NodeId` / `Logger` | `Services/Workflow/Skills/SkillContext.cs:10-16`                                                     |
| 反射式静态 skill 的入参绑定**只有两条路**：`[FromService]`（走 DI）/ `context.Inputs`（按参数名取值）    | `Services/Workflow/Skills/SkillExecutor.cs:361-393`                                                  |
| 元数据（画布参数面板的数据源）**跳过** `[FromService]` 与 `CancellationToken` 参数                | `SkillExecutor.cs:227-233`                                                                           |
| 必填校验也只认 `context.Inputs`                                                      | `SkillExecutor.cs:308-327`                                                                           |
| ⇒ **没有任何「运行上下文注入」机制**；`SkillContext` 也不在 DI 里注册（静态 skill 声明它只会拿到 `null`）      | 反证：只有 DI 注册式的 `ISkillNode` 才拿得到 `SkillContext`（`CertSkillRegistry.cs:58-72`、`LlmExtractSkill.cs:31`） |

> ⇒ 这是「`org_code` 只能手填」的**唯一**技术原因，不是设计取舍。

### 1.2 ★★★ 汇聚节点在多路径下只看到第一条路径（引擎级缺陷，本次新发现）

| 事实                                                                | 证据                                                             |
| ----------------------------------------------------------------- | -------------------------------------------------------------- |
| 一个 item 内，`sharedOutputs`（节点输出池）与 `executedNodes`（已执行节点）**跨路径共享** | `Services/Workflow/WorkflowInterpreter.cs:68-73`               |
| 路径执行时，**已在 `executedNodes` 里的节点直接复用，不重跑**                         | `WorkflowInterpreter.cs:202-219`                               |
| 路径是**解析期静态枚举**的：从 start 做 DFS 到 end，branch 按 success/failure 分两支  | `Services/Workflow/WorkflowConfigParser.cs:306-322`、`:360-373` |
| branch 未选中的那条臂，其路径在运行期被**跳过**（`Status="skipped"`）                 | `WorkflowInterpreter.cs:295-315`                               |

**推论（举例）**：画布 `A → 汇总`、`B → 汇总`（同一 end）⇒ 枚举出 2 条路径 `[start,A,汇总,end]`、`[start,B,汇总,end]`。

- 路径 1：`A` 执行 → `汇总` 执行（`sharedOutputs` 里只有 A）→ 记入 `executedNodes`。
- 路径 2：`B` 执行 → `汇总` **命中复用**（`:202`）⇒ **不重跑**，输出仍是路径 1 那次的结果。

⇒ **B 的值永远进不了「汇总」。** 这解释了用户「分 2 条路要摆 3 个聚合节点」的直觉 —— 但**摆 3 个也救不了**，因为每个「汇总」节点各自只看到它**第一次**被执行时的那条路径。

> ⚠️ 与之对照：`docField`/`docTable` 这类**参数即边**的数据源之所以能工作，是因为它们由 AI 节点**预执行**后写进同一个 `sharedOutputs`（`WorkflowInterpreter.cs:222-253`）—— **不是靠连线汇聚**。本方案的「判定池」正是同一思路的推广。

### 1.3 `verdict` 是 `ConstantOnly` ⇒ 一条 judge 只能表达一种结果

| 事实                                                                                                          | 证据                                                                          |
| ----------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------- |
| `verdict` 绑定模式 = `ConstantOnly`（只能手填常量，**不能连线**）                                                            | `Skills/StampJudgeLevelSkill.cs:92-94`                                      |
| `verdict` 留空时走**静默推断**：`null`/空白/空集合 ⇒ `unverifiable`；`true` ⇒ `pass`；`false` ⇒ `fail`；**其余任何非空值 ⇒ `fail`** | `StampJudgeLevelSkill.cs:126-144` + `NcConclusionV2Calculator.InferVerdict` |
| ⇒ 上游是 `ai_node`（总有输出）时留空**恒为 fail**                                                                         | 同上（已实测确认，见记忆 2026-10-15）                                                    |

**推论**：用户的直觉**在现状下完全成立** —— 要让同一条检查项能表达 pass / fail / unverifiable 三种结果，`ConstantOnly` 逼着你**每种结果摆一个节点**。这不是用户误解，是设计缺陷。

### 1.4 `org_code` / `standard_code` / `phase_code` 无处可来

| 事实                                                                                | 证据                                                                                           |
| --------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| 执行上下文 `contextParams` **只有 3 个键**：`enterpriseCode` / `phaseCode` / `standardCode` | 整流运行：`WfExecutionTaskService.cs:144-149`；单节点测试：`WfExecutionTaskService.NodeScope.cs:575-582` |
| start 节点只是把 `contextParams` 原样吐出                                                  | `NodeExecutor.cs:314-324`                                                                    |
| ⛔ **`contextParams` 里没有 `orgCode`**                                               | 同上两处                                                                                         |
| 引擎**已有一半**的「上下文按名解析」能力：连线型入参若值等于某个 `contextParams` 键名，会**先从上下文取值**                | `NodeExecutor.cs:231-247`（`:234-235`）                                                        |
| ⚠️ 但**常量型入参不做这个解析**，直接返回原始字符串                                                     | `NodeExecutor.cs:222-228`                                                                    |
| ⇒ 且该解析**根本没进 skill**：`SkillContext` 构造时只塞了 `Inputs`，`contextParams` 没传进去          | `NodeExecutor.cs:645-663`                                                                    |

**机构编码的可选来源**（均为实测存在）：

| 来源                             | 证据                                                                              | 备注                                                    |
| ------------------------------ | ------------------------------------------------------------------------------- | ----------------------------------------------------- |
| `cert_validation_rule.OrgCode` | 当前测试规则 `4e7e9f76…` 的 OrgCode **为空**（= 平台标准级）                                    | 最贴近「本次检查用哪套标准」                                        |
| `cert_enterprise.OrgCode`      | `Auditor/Controllers/EnterpriseController.cs:191/245`（`= ws.Data.Code`）         | 企业归属机构                                                |
| `Sys_User.OrgCode`             | `Auditor/Services/WorkspaceContextService.cs:18`、`:106`                         | ⚠️ 指向 **L3 分组**，非机构本身；需经 `WorkspaceContextService` 换算 |
| 专家端工作区                         | `EnterpriseNormalizeController.cs:471`（`req.OrgCode = scope.Data.CertBodyCode`） | 专家端「机构」= 工作区 `CertBodyCode`                           |
| `wf_execution_task`            | 有 `RuleCode`/`EnterpriseCode`/`PhaseCode`，**无 `OrgCode`**                       | ⇒ 机构必须**推导**，不是直接读                                    |

### 1.5 两个已实测的「描述与实现不一致」（顺带修）

| #  | 不一致                                                                                                                                                               | 证据                                           |
| -- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------- |
| I1 | `org_code` 描述写「空串 = 平台标准级」，但 `AllowEmpty = Deny` ⇒ 实测传空串直接报 `PRECONDITION_EMPTY`；而 `LoadMatrixAsync` 的查询 `(x.OrgCode == orgCode \|\| x.OrgCode == "")` **本意支持空串** | `NcConclusionV2Skill.cs:76-78` vs `:169-171` |
| I2 | 描述写「取不到规则 ⇒ 中断」，实际**不中断**：回落全缺省矩阵 + 标 `RuleSource="DEFAULT_MATRIX"`                                                                                               | `NcConclusionV2Skill.cs:56-58` vs `:187-206` |

---

## 2. 问题一定性：不是「3 种情况 ⇒ 3 个节点」，而是两处叠加

用户的现象描述 = **两个独立成因叠加**：

| 成因                                      | 现状后果                                     | 本方案对策                                                    |
| --------------------------------------- | ---------------------------------------- | -------------------------------------------------------- |
| **C1** `verdict` 是 `ConstantOnly`（§1.3） | 一条 judge × 3 结果 ⇒ 3 个盖章节点                | **机制一**：`verdict` 改 `LinkOrConstant`，1 条 judge = 1 个盖章节点 |
| **C2** 汇聚靠「多条线拉到一个节点」（§1.2）             | ① 要 1 个 `聚合多路judge`；② 分支后要多个；③ **而且收不齐** | **机制二**：判定自动登记到「判定池」，**取消汇聚节点与汇聚线**                      |

⇒ **C1 + C2 的乘积**才是「复杂度非常高」。只修 C1，节点数仍随分支数线性增长且数据收不齐；只修 C2，节点数仍随结果数增长。

---

## 3. 问题二定性：`org_code` 不该由配置者提供

| 判据          | 说明                                                                                                                                                                    |
| ----------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **谁知道机构？**  | 引擎。整流运行时来自任务/规则/企业；专家端来自工作区 `CertBodyCode`；后台测试时来自请求的 `RuleCode`/`EnterpriseCode`。                                                                                    |
| **配置者知道吗？** | 只有专家端跑单条时才知道；**在后台配置规则时根本不知道**（同 `is_std_file_missing` 的 `enterprise_code` 情形，见 `NodeExecutor.cs:714-733`）。                                                           |
| **用户视角铁律**  | 「用户**不需理解系统为何这么做**，⛔ **不能把内部准备负担转嫁给用户**」⇒ 手填 GUID 就是转嫁。                                                                                                               |
| **现有先例**    | `docField`/`docTable`/`is_std_file_missing` 的 `enterprise_code` 全部**从 `contextParams` 取**，画布上不出现（`NodeExecutor.cs:455`/`:538`/`:762`）。⇒ 本方案只是把这套做法**补全到 `org_code`**。 |

---

## 4. ★ 推荐方案 A：运行上下文自动注入 + 判定池

### 4.1 机制一：`[FromContext]` —— 运行上下文按名注入

**目标**：让 skill 能声明「这个值请从**本次运行的上下文**里取」，且该参数**不出现在画布上**。

**做法（3 处，全在引擎，不动 skill 内部逻辑）**：

```csharp
// ① 新增特性（同文件，与 [FromService] 并列）
//    Services/Workflow/Skills/SkillAttributes.cs
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public class FromContextAttribute : Attribute
{
    /// <summary>上下文键名（如 "orgCode"）。缺省 = 用参数名。</summary>
    public string? Key { get; set; }
    public FromContextAttribute(string? key = null) { Key = key; }
}
```

```csharp
// ② SkillContext 增一个字典（运行上下文，只读语义）
//    Services/Workflow/Skills/SkillContext.cs
public IDictionary<string, object> ContextParams { get; set; } = new Dictionary<string, object>();
```

```csharp
// ③ 三个方法各加一个分支（与现有 [FromService] 分支同构）
//    SkillExecutor.BindArguments      → 命中 [FromContext] ⇒ 从 context.ContextParams 取
//    SkillExecutor.Analyze            → 命中 [FromContext] ⇒ skip（⇒ 画布上不出现该端口）
//    SkillExecutor.CollectMissingInputs → 命中 [FromContext] ⇒ skip（缺值由引擎负责，⛔ 不算配置者的锅）
```

```csharp
// ④ NodeExecutor 构造 SkillContext 时把 contextParams 传进去
//    NodeExecutor.cs:658-663
var context = new SkillContext {
    Inputs = resolvedInputs ?? new(),
    ContextParams = contextParams ?? new(),      // ★ 新增
    WorkflowInstanceId = $"{taskCode}:{itemCode}",
    NodeId = node.NodeId
};
```

**上下文键清单（本方案要装的 5 个）**：

| 键                | 来源（优先级从高到低）                                                                                                                   | 缺失时                  |
| ---------------- | ----------------------------------------------------------------------------------------------------------------------------- | -------------------- |
| `orgCode`        | ① 规则 `cert_validation_rule.OrgCode` ② 企业 `cert_enterprise.OrgCode` ③ 工作区 `CertBodyCode`（专家端） ④ `Sys_User.OrgCode` ⑤ 空 = 平台标准级 | 空串（**合法**，见 §1.5-I1） |
| `standardCode`   | 已有（`contextParams["standardCode"]`）                                                                                           | 空串                   |
| `phaseCode`      | 已有（`contextParams["phaseCode"]`）                                                                                              | 空串                   |
| `enterpriseCode` | 已有                                                                                                                            | 空串                   |
| `ruleCode`       | 已有（`WfExecutionTaskService.cs:140/154` 的 `request.RuleCode`）                                                                  | 空串                   |

> ★ `orgCode` 的**推导责任放在调用方**（`WfExecutionTaskService` 两处入口），**⛔ 不放进 skill**：skill 是纯函数（G-4），⛔ 不查库推导归属。

**收益**：

- `nc_conclusion_v2` 的三个编码参数**从画布消失**（`Analyze` skip ⇒ `InputPorts` 少 3 个）⇒ 用户不再看到、不再手填。
- 对**后台测试**同样生效（单节点测试的 `contextParams` 同源，`NodeScope.cs:135`）。
- 通用：将来任何 skill 需要 `enterpriseCode`/`ruleCode` 都能用，⛔ 不用再各写一套。

**代价**：

- ⚠️ `contextParams` 的键名必须**与 skill 声明一致**（`orgCode` 是 camelCase）。写错 = 静默拿到 `null`。⇒ **落地时必须配一个「键名一致性」单测**（枚举全仓 `[FromContext]` 的 key，断言都在上下文装配处出现）。
- ⚠️ `Analyze` 跳过 ⇒ 画布上**看不见**这个参数。为了「可见性」（避免「配了不生效且不报错」的观感），建议**在节点参数区渲染一条只读说明**：「机构编码：运行时自动注入（当前：{orgCode}）」。与既有「锚点定位 = 只读条」体例一致。

### 4.2 机制二：判定池（JudgePool）—— 判定自动登记，取消汇聚

**目标**：`stamp_judge_level` 盖完章后，结果**自动**进入「本次检查的判定清单」；`nc_conclusion_v2` 直接读清单。**画布上不需要 `collect_judges` 节点，也不需要把 N 条线拉到一个点。**

**做法**：

```
① 声明式登记（不硬编码 skill 名到引擎里）
   StampJudgeLevelSkill 的 [Skill] 特性增一个标记：
     [Skill(..., RegistersTo = "judge")]
   ⇒ 语义 =「本节点成功执行后，把主输出（result）登记进名为 judge 的运行级池」

② 池的生命周期 = 一个 item（一次检查）
   WorkflowInterpreter.ExecuteItemAsync 里已经有 item 级的 sharedOutputs/executedNodes（:70-73）
   ⇒ 同一处新增：
     var runCtx = new Dictionary<string, object>(contextParams);   // ★ 克隆，⛔ 不污染调用方
     runCtx[WorkflowContextKeys.JudgePool] = new List<object>();    // 新池
   ⇒ 后续 ExecutePathAsync / NodeExecutor 全部用 runCtx（而不是原 contextParams）

③ 登记点（唯一）
   NodeExecutor 在节点成功、且该 skill 声明了 RegistersTo 时：
     池.Add(主输出)                       // 与 PickFromOutput 同一取值口径
   ⇒ 幂等：节点被跨路径复用时（:202）不会重复登记（因为不重跑）

④ 消费
   nc_conclusion_v2 用 [FromContext("__judgePool")] 拿 List ⇒ 内部序列化为 judges_json
```

**为什么这样能绕开 §1.2 的缺陷**：

- 池是 **item 级**的，跨路径共享（与 `sharedOutputs` 同生命周期）。
- 登记发生在**节点成功那一刻**，与「谁来汇聚」无关 ⇒ 分支裁剪（`:295-315`）天然正确：跳过路径上的盖章节点不执行 ⇒ 不登记。
- 跨路径复用 ⇒ 不会重复登记。
- ⇒ **不需要任何汇聚连线**，也就没有「汇聚节点只看到第一条路径」的问题。


**节点数对比（N = 检查项条数）**：

| 方案                | 画布节点                              | 汇聚连线    | 分支后是否收得齐        |
| ----------------- | --------------------------------- | ------- | --------------- |
| 现状（v2 原设计）        | 2N + 2（N 判定 + N 盖章 + 1 聚合 + 1 结论） | N 条     | ❌ **收不齐**（§1.2） |
| 用户设想（3 结果 × 3 节点） | 3N + 3 + 1                        | 3N 条    | ❌ 收不齐           |
| **本方案 A**         | **2N + 1**（N 判定 + N 盖章 + 1 结论）    | **0 条** | ✅               |

### 4.3 三个 v2 skill 的改造后签名

```csharp
// ① StampJudgeLevelSkill —— 唯一改动：verdict 可连线
[Skill(Code="stamp_judge_level", Name="judge盖章级别", ResultPort="result",
       RegistersTo="judge",                       // ★ 新增：自动登记到判定池
       Description="★ 给判定结果盖级别并登记到本次检查的判定清单；级别缺省按 major（安全侧）")]
ExecuteAsync(
  [SkillParam(BindMode=LinkOrConstant, AllowEmpty=Deny)]  object? result,      // 证据（连线）
  [SkillParam(BindMode=ConstantOnly,   AllowEmpty=Deny)]  string? judge_code,  // 名字
  [SkillParam(BindMode=ConstantOnly,   AllowEmpty=Allow)] string? level,       // 权重
  [SkillParam(BindMode=LinkOrConstant, AllowEmpty=Allow)] string? verdict,     // ★ 判定（可连线！）
  [SkillParam(BindMode=ConstantOnly,   AllowEmpty=Allow)] string? reason)

// verdict 取值契约（★ 新增，⛔ 不再「静默推断」）：
//   bool         → true = pass，false = fail
//   字符串（不分大小写）→ "pass" / "fail" / "unverifiable"
//   null / 空白  → 若 result 为 bool 则按 result；否则 ⇒ Fail(CONFIG_ERROR)
//   ★ 其他任何值 → Fail(CONFIG_ERROR)，⛔ 绝不静默判 fail
```

```csharp
// ② CollectJudgesSkill —— 保留，但退化为「兼容/调试」通道（画布上不再需要）
//    待 v2 稳定后与 v1 三个 skill 同批摘除

// ③ NcConclusionV2Skill —— 三个编码参数改上下文注入；judges 改从池取
[Skill(Code="nc_conclusion_v2", Name="NC结论判定v2(多judge)", ResultPort="conclusion",
       Description="★ 纯代码决策矩阵：对本次检查已登记的全部判定算权威结论；机构/标准/阶段由运行上下文自动注入")]
ExecuteAsync(
  [FromContext(WorkflowContextKeys.JudgePool)] string? judges_json,   // ★ 从池取（可空：允许 0 条判定）
  [FromContext("orgCode")]                     string? org_code,      // ★ 画布上消失
  [FromContext("standardCode")]                string? standard_code, // ★ 画布上消失
  [FromContext("phaseCode")]                   string? phase_code,    // ★ 画布上消失
  [FromService] IDbOrm db = null!,
  CancellationToken ct = default)
```

> ⚠️ **`judges_json` 类型不变**（仍是 JSON 字符串），只是**来源**从「连线」变成「运行上下文」——`JudgeResultParser.Parse` 与决策矩阵**零改动**。

### 4.4 对引擎的改动清单（按风险排序）

| # | 位置                                                   | 改动                                                                       | 风险                           |
| - | ---------------------------------------------------- | ------------------------------------------------------------------------ | ---------------------------- |
| 1 | `SkillAttributes.cs`                                 | 新增 `[FromContext]` 特性；`[Skill]` 增 `RegistersTo` 字段                       | 低（纯新增）                       |
| 2 | `SkillContext.cs`                                    | 增 `ContextParams` 字段                                                     | 低（纯新增，默认空字典 ⇒ 零行为变化）         |
| 3 | `SkillExecutor.cs`                                   | `BindArguments`/`Analyze`/`CollectMissingInputs` 各加一个 `[FromContext]` 分支 | **中**（`Analyze` 分支错 ⇒ 画布端口错） |
| 4 | `NodeExecutor.cs`                                    | 构造 `SkillContext` 时传 `ContextParams`；节点成功后按 `RegistersTo` 登记池            | **中**（登记点必须唯一且幂等）            |
| 5 | `WorkflowInterpreter.cs`                             | `ExecuteItemAsync` 克隆 `contextParams` + 建池，向下传 `runCtx`                  | **中**（⛔ 必须克隆，否则池跨 item 泄漏）   |
| 6 | `WfExecutionTaskService`（两处入口）                       | 装配 `orgCode`（推导链 §4.1）                                                   | 中（推导优先级是业务口径）                |
| 7 | `NcConclusionV2Skill.cs` / `StampJudgeLevelSkill.cs` | 改参数声明 + verdict 契约                                                       | 低（逻辑基本不动）                    |
| 8 | `NcConclusionV2Skill.cs`                             | 修 §1.5 的两处描述/实现不一致                                                       | 低                            |

**建议分批**（同 §同目录文档体例）：

| 批次        | 内容                                      | 验收判据                                                                                                          |
| --------- | --------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| **第 1 批** | 机制一（`[FromContext]`）+ 装配 `orgCode`      | `nc_conclusion_v2` 画布端口 **4 → 1**（只剩 `judges_json`）；后台测试传 `RuleCode`/`EnterpriseCode` 后**自动取到正确机构**；键名一致性单测通过 |
| **第 2 批** | 机制二（判定池）+ `verdict` 可连线                 | 画布 `A→盖章A`、`B→盖章B`、`结论`（**无汇聚线**）⇒ 结论 stats.total = 2（⛔ 不是 1）；分支裁剪后只登记被选中臂                                    |
| **第 3 批** | 修 §1.5 两处不一致 + `collect_judges` 标注为兼容通道 | 传空串 `org_code` 不再报 `PRECONDITION_EMPTY`；描述与实现一致                                                               |

---

## 5. 备选方案 B：最小改动（不推荐，但成本最低）

若暂不想动引擎：

| 项  | 做法                                                                        | 遗留问题                                                                                                               |
| -- | ------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| B1 | `verdict` 改 `LinkOrConstant`（仅改 1 行特性）                                    | ✅ 解决「3 种情况 3 个节点」                                                                                                  |
| B2 | `org_code` 等三参数：**约定在画布上填常量**，并把值改为「上下文键名」（如填 `orgCode`）+ 把该输入声明为**连线**类型 | ⚠️ 依赖 §1.4 那条**已有的、半成品**的按名解析（`NodeExecutor.cs:234-235`）；仍需手填、仍可能写错；且 `contextParams` 里**没有 `orgCode`** ⇒ 需先在引擎里补键 |
| B3 | 汇聚：**约定判定链必须是「单路径」**（⛔ 禁止用 branch 分叉判定），否则汇聚失效                            | ❌ 把 §1.2 的缺陷**变成用户的纪律**，与「不能把内部负担转嫁给用户」冲突                                                                          |

⇒ **B 只是把问题挪到用户的配置纪律里，不解决根因。** 仅当用户希望「先看效果、后动引擎」时作为过渡。

---

## 6. 待裁决项

| # | 事项                              | 选项                                                                                                               | 建议                                                                                        |
| - | ------------------------------- | ---------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| 1 | 是否采用方案 A（`[FromContext]` + 判定池） | (a) 采用（推荐） / (b) 先用方案 B 过渡 / (c) 其他                                                                              | **(a)**                                                                                   |
| 2 | `verdict` 留空的语义                 | (a) 保留现有「静默推断」（非空 = fail） / (b) **改为显式报错**（⛔ 不静默）                                                                | **(b)** —— 与「一条路径的判定应当是确定的」一致；代价：需回归已有节点配置                                                |
| 3 | `orgCode` 推导优先级                 | (a) 规则 → 企业 → 用户 → 空（推荐） / (b) 企业 → 规则 → 用户 → 空 / (c) 专家端一律用工作区 `CertBodyCode` 覆盖                                | **(a)**，专家端在**最前**加一档工作区                                                                  |
| 4 | 判定池取不到任何判定时的行为                  | (a) 返回 `conclusion=unverifiable` + warning（推荐） / (b) `Fail(PRECONDITION_EMPTY)`（与现 `collect_judges` 全空即 Fail 一致） | **(a)**：`collect_judges` 的 Fail 语义是「画布上没接 judge = 配置错」；池方案下「0 条」可能只是**本次确实无判定项**，⛔ 不该当配置错 |
| 5 | 画布上是否显示只读的「机构编码：自动注入」说明条        | (a) 显示（推荐，防「看不见 = 以为没配」） / (b) 完全不显示                                                                             | **(a)**                                                                                   |
| 6 | `collect_judges` 的去留            | (a) 保留为兼容通道，v2 稳定后与 v1 三个 skill 同批摘除（推荐） / (b) 立即摘除                                                              | **(a)** —— 与用户「先保留 6 个、v2 稳定后去 v1」的裁决一致                                                   |

---

## 7. 本文档明确的「不做」

- ⛔ **不修 §1.2 的引擎缺陷本身**（即「让汇聚节点真正看到所有路径」）。判定池**绕开**它；改「跨路径复用」为「汇聚点重算」会动摇整个执行模型（跨路径复用是 `sharedOutputs` 的性能与一致性基础），风险远大于收益。⇒ **登记为已知限制**。
- ⛔ **不把 `judge_code`/`level` 吸收进判定节点**（虽然能再省 N 个节点）。判定节点类型各异（`ai_node`/`compare`/`is_empty`…），逐个加配置字段 = 大面积改动；且用户明确「`judge盖章级别` 本身设计非常好」⇒ **保留独立盖章节点**。
- ⛔ **不改 `nc_conclusion_v2` 的决策矩阵算法**（`NcConclusionV2Calculator`）与 `cert_conclusion_rule` 表结构。本方案只改**入参来源**。
- ⛔ **不在本方案内摘除 v1 三个 skill**（`build_nc_prompt` / `nc_conclusion_calc` / `nc_validate`）—— 按用户裁决，待 v2 稳定后单独一批处理（⚠️ 摘除时除 `wf_skill` 外须清 `wf_skill_reflection` 的 3 行反射登记）。
- ⛔ **不做 AI 节点并行**（与同目录 `AI节点参数与审计字段统一-改造设计-V1.md` §6 一致）。

---

## 7.5 落地记录（2026-10-16 · 第 3 批「collect_judges 标注为兼容通道」的补丁）

> 触发：用户在 NC 配置页点「运行测试」测 `聚合多路judge`（`collect_judges_n1`），得到
> `节点执行失败: Skill 'collect_judges' 执行失败[PRECONDITION_EMPTY]: …未收到任何 judge 结果`。
> 排查发现三层问题（**用户界面上一个都看不见**，故一并修）：

| # | 问题 | 修法 | 落点 |
| - | ---- | ---- | ---- |
| 1 | **失败上抛时诊断丢失**：`PRECONDITION_EMPTY` → `WorkflowDataMissingException` **按设计穿透** `NodeExecutor`（交给 `WorkflowInterpreter` 落补录缺口），而单节点测试没有解释器兜底 ⇒ 挂在 `NodeExecutionResult.UnresolvedInputs` 上的「哪些端口没取到值」**永远回不到调用方**；同时 `PersistNodeScopeAsync` 被跳过 ⇒ **本次测试不落库**（与 `WfExecutionTaskService.NodeScope` 类注释的「可回溯」承诺相悖） | 异常带上 `UnresolvedInputs`（`WorkflowDataMissingException` 新增可空属性）；`RunSingleNodeAsync` **收口**该异常 → 合成失败结果 → 走同一条落库路径；错误文案追加「本次单节点测试未执行上游：{端口}…」 | `Shared/Exceptions/WorkflowDataMissingException.cs`、`Admin/Services/Workflow/NodeExecutor.cs`、`WfExecutionTaskService.NodeScope.cs` |
| 2 | **兼容通道会把残缺清单盖过完整池**：`nc_conclusion_v2` 是「连线优先」，而 `collect_judges` 收的正是 §1.2 那条「只看得见第一条路径」的线 ⇒ 存量画布拿到**残缺清单**且两边都不报错 | `collect_judges` 槽位一条都没接到时回落 `[FromContext(JudgePool)]`（与 `nc_conclusion_v2` 逐字同一条口径）；两个来源都空才 Fail；输出补 `judges_source`（`slots`/`judge_pool`）作诊断面 | `Admin/Services/Workflow/Skills/CollectJudgesSkill.cs` |
| 3 | **节点级失败被渲染成绿底「执行成功」**：面板只看信封 `success`，不看 `data.Success`；且失败时前端把 `data` 丢掉（`onError(msg, null)`）⇒ 「连线端口未取到值」告警框**永远不显示** | 面板新增 `nodeFailed`/`testOk`/`testErrorText`（同时兼容 PascalCase/camelCase）；两处测试入口失败时回传 `res.data` | `cert-share/src/components/workflow/NodePropertyForm.vue`、同目录 `WorkflowDesigner.vue` |

验收（2026-10-16 实测）：

- 复现用户的原始请求 ⇒ `success:true` 信封 + `data.Success:false` + `data.Error` 含「本次单节点测试未执行上游：judge_1 端口没有值…」+ `data.UnresolvedInputs:[ "judge_1" ]`，且 `wf_node_execution` 有行（`NodeId=collect_judges_n1`）。
- `nc_conclusion_v2` 单节点测试（`judges_json` 传真实 JSON）⇒ `conclusion=fail`，正常路径不受影响。
- 单测：`CollectJudgesPoolFallbackTests`（5 例，含「槽位有值则忽略池」负向对照）、`WorkflowDataMissingUnresolvedInputsTests`（4 例，含 `ConstantOnly` 不进清单负向对照）；全量 `723 通过 / 0 失败`。
- 前端组件测试：`cert-admin/src/pages/workflow/node-property-form-test-result.test.ts`（4 例，含「成功仍是成功」负向对照；已实测「把模板改回只看信封」即 N1 变红）；全量 `396 通过 / 0 失败`。
- 前端守卫 `guards.mjs` 与后端 `scripts/backend/guards.mjs` 均 0 违规；`cert-admin` / `cert-auditor` 的 `vue-tsc` 均 0 错误。

⚠️ 仍未做（本补丁**不含**）：§7 的「不修 §1.2 引擎缺陷」不变 —— 上述回落只是让**存量画布**不再静默拿到残缺清单，推荐链路仍是「判定自动登记 + `nc_conclusion_v2` 读池」，⛔ 画布上不应再摆 `聚合多路judge`。

---

### 7.6 落地记录（2026-10-16 · M3 改为「证据优先判空」）

> 触发：用户在 NC 配置页测 `stamp_judge_level`（只连了「证据」`result`），
> 得到 `CONFIG_ERROR: 参数 judge_code 未填写`，并据此复述了 judge 节点的设计意图。

**结论（用户裁定）**：

| # | 事项 | 裁定 |
| - | ---- | ---- |
| 1 | `judge_code` 是否必填 | ✅ **保持必填**（`ConstantOnly + Deny`）。它是下游 `collect_judges`/`nc_conclusion_v2` 区分判定来源的唯一键，与「证据 / 判定二选一」无关。用户实测报文缺的正是它。 |
| 2 | M3（证据 + 判定都连）语义 | ★ **改为「证据优先判空」**（原为「判定优先」）。 |

**M3 新语义（唯一口径，按序）**：

```
① result 与 verdict 端口都未连接              ⇒ Fail(CONFIG_ERROR)
② verdict 有值但非法                          ⇒ Fail(CONFIG_ERROR)（先校验，不被证据判空吞掉）
③ verdict 合法 且 证据已配置但为空            ⇒ unverifiable（★ 证据优先，判定不介入）
④ verdict 合法 且 （证据未配置 或 有值）      ⇒ 按 verdict：pass / fail / unverifiable
⑤ verdict 空 且 result 有值                   ⇒ pass
⑥ verdict 空 且 result 空                     ⇒ unverifiable
```

判据补充：**「证据已配置」= 该端口已连线 或 有非空常量**，口径与
`NodeExecutor.ConnectedPorts`（按值非空过滤）一致 ⇒ 用于区分 M2（只连判定，⛔ 不因「证据为空」判 unverifiable）
与 M3（都连，证据空 ⇒ 优先 unverifiable）。

**连带改动**：

| 落点 | 改动 |
| ---- | ---- |
| `StampJudgeLevelSkill.cs` | `ResolveVerdict` 增 `evidenceConfigured` 参数 + M3 证据优先；类注释「4 条」→「6 条」；`[Skill(Description)]` ③ 改「证据优先」 |
| `IsEmptySkill.cs` / `specialNodes.ts` | 「空 = 不符合」指引补充：连 `is_empty` 取反→`verdict` 时 **⛔ 不要连「证据」**（证据空会盖成不可判定） |
| `NcConclusionV2Tests.cs` | `BothConnected_VerdictWins` → `BothConnected_EvidencePresent_VerdictWins`；新增「证据空⇒unverifiable」「非法 verdict 优先」两例 |

⚠️ **§4.3 的 `verdict` 取值契约**（「null/空白 …否则 Fail(CONFIG_ERROR)」）以本节 ①–⑥ 为准（该契约自 2026-10-15 起已过期）。

---

### 7.7 落地记录（2026-10-16 · `judge_code` 删除 ⇒ judge 标识改取「节点名称」）

> 触发：用户反馈「`judge盖章级别` 的『judge 标识』能否不单独传？每个节点本来就有独立名称，
> 改节点名称就等于给 judge 标识赋值；否则要在**两个地方**赋值，很奇怪。」

**结论（用户裁定）**：✅ **彻底去掉「judge 标识」输入框**，judge 标识 = **本节点名称**（`WorkflowNodeConfig.Title`）。

**为什么可行（取证）**：

| 判据 | 证据 |
| ---- | ---- |
| 节点名称在画布内**唯一** | `cert-share/src/composables/workflow/useWorkflowStore.ts` —— 新增即 `generateUniqueName()` 去重；改名经 `isNameUnique()` 拦截重名（`:233`） |
| `judge_code` **只是展示标签** | `NcConclusionV2Calculator.JudgeResult.JudgeCode` 注释「审计展示用」；全仓**无查表 / 分组 / 去重**，只是原样进 judge 记录与输出 |
| 注入成本极低 | `NodeExecutor.ExecuteSkillAsync` 已持有 `node.Title`，`skillCtxParams` 本就是为「逐节点注入」新建的字典 ⇒ **零装配方改动** |

**改动清单**：

| 落点 | 改动 |
| ---- | ---- |
| `WorkflowContextKeys.cs` | 新增 `NodeTitle = "nodeTitle"`（逐节点注入；文档注明「画布内唯一标识」） |
| `NodeExecutor.cs` | `ExecuteSkillAsync` 的 `skillCtxParams` 增 `[NodeTitle] = node.Title ?? ""` |
| `StampJudgeLevelSkill.cs` | ⛔ 删 `judge_code` 参数（`ConstantOnly + Deny`）；新增 `[FromContext(NodeTitle)] string? nodeTitle`；`judgeCode = nodeTitle.Trim()`，空 ⇒ `Fail(CONFIG_ERROR)`「本节点未命名…」；`[Skill(Description)]` 与类注释同步 |
| 前端 | **零改动** —— 端口由后端 `Analyze` 反射生成，参数删除即从属性面板消失 |

**同类回归（测试）**：

| 文件 | 改动 |
| ---- | ---- |
| `FromContextInjectionTests.cs` | 画布端口 5 → **4**（`result/level/verdict/reason`）；`judge_code`/`nodeTitle` 均不可见；stamp 声明 `NodeTitle` 键 |
| `SkillParamCompletenessTests.cs` | 「`ConstantOnly` 为空 ⇒ `CONFIG_ERROR`」样例从 `stamp.judge_code` 改用 `src_manual.anchor_code`（同属性、免 DB） |
| `NcConclusionV2Tests.cs` | `RunRecord` 第 2 实参改作 `nodeTitle` 注入；`EmptyJudgeCode_Fails` → `EmptyNodeTitle_Fails`；新增「`judge_code` 参数不存在 + `nodeTitle` 带 `[FromContext]`」断言 |
| `WorkflowConfigParserPortTests.cs` | 「真实快照」fixture 的 `judge_code` 端口 → `level`（`ConstantOnly` 样例不变） |

**影响 / 边界**：

- 存量规则里已填的 `judge_code` 常量**被忽略**，judge 标签从旧值变为**节点名称**（仅展示层，δ 不影响结论计算）。
- 名称唯一性目前**仅前端 store 守**；手改 `rule_json` 造成重名时，只表现为两条 judge **标签相同**，⛔ 不影响判定矩阵。
- ⚠️ **本节取代 §7.6 结论表第 1 行**（「`judge_code` 保持必填」已作废）。

---

## 8. 附：本方案对既有文档/记忆的影响

| 对象                                                                               | 影响                                                                  |
| -------------------------------------------------------------------------------- | ------------------------------------------------------------------- |
| 记忆「v2 链路 = 多条判定分支 → `stamp_judge_level` → `collect_judges` → `nc_conclusion_v2`」 | **落地形态变更**：`collect_judges` 由「必经」降为「兼容通道」；链路改为 `判定 → 盖章（自动登记） → 结论` |
| `NcConclusionV2Skill.cs` 类注释里「⛔ 三个 code 参数不该连 `collect_judges`」                  | 升级为「三个 code 参数**已从画布移除**，由运行上下文注入」                                  |
| `StampJudgeLevelSkill.cs` 类注释「verdict 建议一律显式填写」                                  | 升级为「verdict 可连线；留空且无法判定 ⇒ **报错**」                                   |
| 记忆「`stamp_judge_level` 的 `judge_code` 必填」                                             | 作废（§7.7）：**画布上已无 judge 标识输入框**，judge 标识 = 本节点名称（`[FromContext] nodeTitle`） |
| 记忆「需裁决项必须配白话说明书」                                                                 | 本文档 §0 已配 ✅                                                         |
