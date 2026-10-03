# 文档填写 Skill 详细设计

> **版本**：V1.2 | **日期**：2026-10-03 | **状态**：**✅ 已批准 —— 实施中（S1 完成 / S2 完成）**
> **V1.2 修订（实施反哺设计）**：用户批准后按 §十四 顺序开工，**编码时实读源码 + 核对项目引用**
> 查出并修正 **3 处硬错误 + 1 处结构性阻塞**（全部在 §4.2 `src_global_param`）：
> ① `FillParamValue` 实体**不在** `CertPlatform.Shared.Entities.Param`，而在 **`CertPlatform.Auditor`**；
> ② `ParamValueResolver` **不在** `CertPlatform.Admin.Services.Param`，而在 **`CertPlatform.Shared.Fill`**；
> ③ `PickMostSpecific` 入参是 **`FillParamDef`（定义）**，不是 `FillParamValue`（值）；
> ④ ★★★ **结构性阻塞**：`Auditor → Admin` 单向引用 ⇒ **Admin 内的 Skill 看不到 `FillParamValue`**
> ⇒ 企业已填值改由**编排器预取**传入（详见 §4.2 修正块）。同时 **作废 `Q-23`**（实测已实现，无需改动）。
> **V1.1 修订**：用户读后认可「**可以实现**」，并提出 2 个待补问题 ⇒ 新增 **§十六（`{{}}` 的格式化设计）** +
> **§十七（AI 建议表）**，并新增 **`Q-26`/`Q-27`**。**§一~§十五 未推翻。**
> **定位**：`38` 号（**开发计划**）的**下游详细设计**。`38` 号 回答「**做哪些 Skill、怎么排期、有哪些裁决**」，
> 本文回答「**每个 Skill 的函数签名、参数、内部步骤、边界、测试用例是什么**」—— 目标是**照着能写代码**。
> **来源**：`38` 号 §十四/§十五 + **实读源码 8 个文件**（`SkillExecutor` / `SkillContext` / `SkillAttributes` /
> `GetFieldSkill` / `LlmExtractSkill` / `LlmInvokeService` / `OfficeFillModels`）+ **实测 DB 3 张表结构**。
> **⛔ 本文是详细设计，不是实现记录。**
> **编号说明**：本册编号被两个会话交叉写过（`38` 号 已登记），本文取目录最大号 **`39`**。

---

## 一、本文要解决什么（先承认 `38` 号 的 3 处不一致）

用户指出「**继续完善 Skill 建设，还没看到具体的设计**」。我复查 `38` 号 后，确认它**有 3 处内部不一致** ——
**这 3 处如果不收敛，照着做必然写歪**：

| # | 不一致 | 位置 | 后果 |
|---|---|---|---|
| **I-1** | **同一批 Skill 有两套命名** | §5.2 / §6.1 用 `src_from_profile` / `src_related_doc` / `src_manual` / `src_self` / `src_sibling` / `src_compute`；§14.2 / §十五 用 `src_global_param` / `src_ai_field` / `src_ai_table` | **来源链里配的 SkillCode 与注册表里的对不上** ⇒ 静默找不到 Skill |
| **I-2** | **§14.2 的「一期/二期」列未同步 §十五** | §14.2 表里 `src_semantic`/`src_ai_field`/`src_ai_table` 仍标「二期」；§十五 已宣布**一期必做** | 排期时按哪张表做，两个答案 |
| **I-3** | **§14.2 的「主要参数」列已过期** | 写的是 `upstream_doc_code` / `field_code`；但 §15.10 的两级过滤管线已把输入变成「**已过滤的企业文档 markdown**」 | 照 §14.2 写签名 ⇒ 参数根本拿不到数据 |

**⇒ 本文 §二 先做命名收敛，§三 定通用契约，§四~§十一 逐个给出可照抄的设计，§十二 给 AI 提示词契约。**

### 1.1 本文对 `38` 号的 2 处实质修正（★ 不是抄，是改）

| 修正 | `38` 号 原设计 | 问题 | 本文改为 |
|---|---|---|---|
| **M-1** | §14.6 `fill_cell` 签名收 `OfficeFillRequest? request = null` | **走不通**：声明式来源链的参数是 **JSON 存库**的，取回后是 `JsonElement` / `Dictionary<string,object>`；而 `SkillExecutor.ConvertValue` **只转基础类型**（`string/bool/int/long/double/decimal/DateTime`），**复杂对象原样返回** ⇒ **永远无法还原成 `OfficeFillRequest`** | 引入 **`FillSession`**（见 §三.6）：编排器在 `context.Inputs` 里**直接放对象实例**，`ConvertValue` 原样透传 |
| **M-2** | §14.6 `fill_cell` 用 `IDictionary<string, FillValue>` 收值 | 同上：`FillValue` 是复杂类型，JSON 链上还原不了 | `fill_cell` 改为收 **`IDictionary<string, object>`（值描述）**，内部转 `FillValue`；★ 但**编排器直连路径**仍可直接给 `FillValue`（`ConvertValue` 透传，需运行期判型） |

> **★ M-1/M-2 的意义**：这不是细节，是「**声明式配置**」与「**反射调用**」两个世界的接缝。
> `38` 号 只画了架构，没碰到这条缝；**碰到就会卡住**。

---

## 二、★ 命名统一裁定（收敛 I-1）

### 2.1 最终清单：**8 个 Skill = 6 内容类 + 2 操作类**

| # | SkillCode | 名称 | 类别 | 产出 | 一期 |
|---|---|---|---|---|---|
| 1 | **`src_global_param`** | 全局参数取值 | 内容·标量 | `FillValue` | ✅ |
| 2 | **`src_manual`** | 人工待办声明 | 内容·**空值** | **待办标记（⛔ 不产值）** | ✅ |
| 3 | **`src_semantic`** | AI 语义改写 | 内容·标量 | `FillValue` | ✅ |
| 4 | **`src_ai_field`** | AI 单元格填写 | 内容·标量 | `FillValue` | ✅ |
| 5 | **`src_ai_table`** | AI 表格填写 | 内容·**表格** | `TablePayload` | ✅ |
| 6 | **`src_dict`** | 字典取值 | 内容·标量 | `FillValue` | 二期 |
| 7 | **`fill_cell`** | 单元格填写 | 操作 | 写入指令 | ✅ |
| 8 | **`fill_table`** | 表格填写 | 操作 | 写入指令 | ✅ |

> **★ 为什么 `src_manual` 算「内容类」却不产值**：它的职责是**显式声明「这一格要人工填」**，
> 让 `onMissing: todo` 有落点（`38` 号 §5.2）。它产出**待办标记**而不是值 ⇒
> **`38` 号 §14.2 说「5 个内容类」是按「产出值的来源」分类，`src_manual` 不产值所以被排除；
> 本文按「参与来源链的 Skill」分类 ⇒ 6 个**。两者不矛盾，但**必须有一处说清**，否则排期时数量对不上。

### 2.2 旧名 → 新名映射（**迁移脚本用**）

| `38` 号 §5.2/§6.1 旧名 | 裁定 | 理由 |
|---|---|---|
| `src_from_profile` | → **`src_ai_field`** | 同一件事（从企业资料取单元格值），旧名强调「画像」这个**实现细节**，新名强调「AI 动态分析」这个**能力** |
| `src_related_doc` | → **`src_ai_field`** | 同上；「关联文档」在两级过滤管线（§15.10）里已变成「**已过滤的企业文档**」，是**输入**不是 Skill |
| `src_self` | → **`src_global_param`** | 「取本模板自身字段」= 全局参数表里 `Scope='self'` 的一类，**同表不同参数**，不必独立 Skill |
| `src_sibling` | → **`src_global_param`** | 同上（`Scope='sibling'`） |
| `src_compute` | → **一期不做** | 无真实需求（`22` 号 `expr` 字段已能表达简单拼接）；**列入 `Q-24`** |
| `src_manual` | → **保留 `src_manual`** | 语义清晰，且 `onMissing: todo` 需要它 |
| `src_global_param` / `src_dict` / `src_semantic` / `src_ai_table` | 不变 | — |

**⇒ 收敛后 `38` 号 §5.2 的示例 JSON 应改为**：

```json
{
  "combine": "firstHit",
  "sources": [
    { "skill": "src_global_param", "params": { "param_code": "ENT_NAME" }, "onMissing": "next" },
    { "skill": "src_ai_field",     "params": { "anchor_code": "ENT_NAME", "instruction": "企业全称" }, "onMissing": "next" },
    { "skill": "src_manual",       "params": { "hint": "请填写企业名称" }, "onMissing": "todo" }
  ]
}
```

### 2.3 `wf_skill` 注册行的最终值（**实测列名，可直接执行**）

```sql
-- ★ 实测 wf_skill 列：Code/SkillCode/Name/SkillType/CategoryCode/SideEffect/Description/
--    PromptTemplate/IsActive/OutputStrict/ReturnType/Version/Icon/Color/SortOrder
INSERT INTO wf_skill
  (Code, SkillCode, Name, SkillType, CategoryCode, SideEffect, ReturnType,
   Description, IsActive, OutputStrict, Version, SortOrder, Icon, Color)
VALUES
  ('SK_SRC_GLOBAL_PARAM','src_global_param','全局参数取值','method','doc_src_scalar',0,'json',
   '按参数编码取全局参数值（标准/阶段特化，取最特异一条）',1,1,'1.0',100,'el-icon-set-up','#409EFF'),
  ('SK_SRC_MANUAL','src_manual','人工待办声明','method','doc_src_scalar',0,'json',
   '声明「该锚点需人工填写」，产出待办标记（不产值）',1,1,'1.0',101,'el-icon-edit-outline','#909399'),
  ('SK_SRC_SEMANTIC','src_semantic','AI 语义改写','method','doc_src_scalar',0,'json',
   '按企业实际情况改写标准原文（不依赖企业文档）',1,1,'1.0',102,'el-icon-magic-stick','#9B59B6'),
  ('SK_SRC_AI_FIELD','src_ai_field','AI 单元格填写','method','doc_src_scalar',0,'json',
   '提示词 + 已过滤企业文档 → 动态分析出单元格值',1,1,'1.0',103,'el-icon-magic-stick','#9B59B6'),
  ('SK_SRC_AI_TABLE','src_ai_table','AI 表格填写','method','doc_src_table',0,'json',
   '提示词 + 已过滤企业文档 → 动态分析出表格数据',1,1,'1.0',104,'el-icon-magic-stick','#9B59B6'),
  ('SK_SRC_DICT','src_dict','字典取值','method','doc_src_scalar',0,'json',
   '按字典编码取字典项（支持级联）',0,1,'1.0',105,'el-icon-collection','#67C23A'),
  ('SK_FILL_CELL','fill_cell','单元格填写','method','doc_fill',1,'json',
   '把值装配成单元格填充指令（含页眉）。★ 不落盘',1,1,'1.0',200,'el-icon-edit','#E6A23C'),
  ('SK_FILL_TABLE','fill_table','表格填写','method','doc_fill',1,'json',
   '把表格数据装配成区域填充指令（Word 表格 / Excel 区域）。★ 不落盘',1,1,'1.0',201,'el-icon-grid','#E6A23C');

-- ★ 实测 wf_skill_reflection 列：Code/SkillCode/ClassPath/MethodName/ParamBinding/Status
INSERT INTO wf_skill_reflection (Code, SkillCode, ClassPath, MethodName, Status)
VALUES
  ('SKR_SRC_GLOBAL_PARAM','src_global_param','CertPlatform.Admin.Services.Workflow.Skills.Fill.SrcGlobalParamSkill','ExecuteAsync','active'),
  ('SKR_SRC_MANUAL','src_manual','CertPlatform.Admin.Services.Workflow.Skills.Fill.SrcManualSkill','ExecuteAsync','active'),
  ('SKR_SRC_SEMANTIC','src_semantic','CertPlatform.Admin.Services.Workflow.Skills.Fill.SrcSemanticSkill','ExecuteAsync','active'),
  ('SKR_SRC_AI_FIELD','src_ai_field','CertPlatform.Admin.Services.Workflow.Skills.Fill.SrcAiFieldSkill','ExecuteAsync','active'),
  ('SKR_SRC_AI_TABLE','src_ai_table','CertPlatform.Admin.Services.Workflow.Skills.Fill.SrcAiTableSkill','ExecuteAsync','active'),
  ('SKR_SRC_DICT','src_dict','CertPlatform.Admin.Services.Workflow.Skills.Fill.SrcDictSkill','ExecuteAsync','active'),
  ('SKR_FILL_CELL','fill_cell','CertPlatform.Admin.Services.Workflow.Skills.Fill.FillCellSkill','ExecuteAsync','active'),
  ('SKR_FILL_TABLE','fill_table','CertPlatform.Admin.Services.Workflow.Skills.Fill.FillTableSkill','ExecuteAsync','active');
```

> **★ 命名空间裁定**：新建子目录 `Skills/Fill/`（与 `Skills/` 平级文件区分），
> `ClassPath` 前缀 = **`CertPlatform.Admin.Services.Workflow.Skills.Fill.*`**。
> ⚠️ 与 `38` 号 §14.7 写的 `...Skills.FillCellSkill` **不同** —— 本文加了 `.Fill` 段，**以本文为准**。
> ⛔ `ClassPath` 与 `MethodName` 是**字符串匹配**，写错 ⇒ `ResolveType` 返回 null ⇒ 报「无法找到类型」。

---

## 三、通用契约（8 条，所有 Skill 必须遵守）

### 3.1 ★ 范式裁定：用「**静态方法 + `[SkillParam]`**」，⛔ 不用 `ISkillNode`

**实测项目里有两种范式**：

| 范式 | 样例 | 注册方式 | 能否产 UI 表单 |
|---|---|---|---|
| **静态方法 + 特性** | `GetFieldSkill` | `wf_skill_reflection` 的 `ClassPath`+`MethodName` | ✅ **能**（`SkillExecutor.Analyze` 反射出 `InputPorts`） |
| **`ISkillNode` 实例** | `LlmExtractSkill` | DI 容器注册 | ⛔ **不能**（`Analyze` 只认静态方法） |

**⇒ 裁定：8 个 Skill 全部用静态方法范式。** 理由：
1. **UI 要自动生成参数表单**（`38` 号 §14.4「操作方法自动生成」）⇒ 必须能反射出 `InputPorts`；
2. `ISkillNode` 的注释自己写着「**回退通道**」；
3. 需要 DI 的服务用 **`[FromService]`** 注入（`GetFieldSkill` 已有先例：`[FromService] IDbOrm db = null!`）。

> ⚠️ **`LlmExtractSkill` 仍是重要资产**：它是**唯一**已验证「LLM → 结构化 JSON」全链路的代码。
> 它积累的 **5 项经验必须被 `IAiFillInvoker` 继承**（见 §十二.1），⛔ 不要重写。

### 3.2 ★★ `SkillResult` 的真实语义（**最容易踩的坑**）

**实测 `SkillExecutor.ExecuteAsync` 逐字行为**：

```csharp
// 失败时 —— 注意：返回的仍是 Ok！
standardOutputs["error"] = result.Error ?? string.Empty;
return SkillResult.Ok(standardOutputs, result.Confidence);   // ← Success 恒为 true
```

**⇒ 结论（★ 必须记住）**：

| 层 | 判据 |
|---|---|
| `SkillResult.Success` | **恒为 `true`**（除非抛异常） |
| **真正的成败** | **`Outputs["success"]`（bool）** |
| 业务输出 | **平铺到 `Outputs` 顶层**（`standardOutputs[kv.Key] = kv.Value`）⇒ 可 `Outputs["value"]` 直接取 |

**⇒ 对 8 个 Skill 的要求**：
1. **⛔ 不要抛异常**表达业务失败 —— 用 `SkillResult.Fail("...")`；
2. 编排器取结果**必须**先判 `Outputs["success"]`，⛔ 不要判 `result.Success`。

### 3.3 ★★ `ConvertValue` 只转基础类型（**M-1/M-2 的根因**）

**实测 `SkillExecutor.ConvertValue` 支持的类型**：`string` / `bool` / `int` / `long` / `double` / `decimal` / `DateTime`。
**其余类型原样返回**（不报错）。

**⇒ 三条使用规则**：

| 场景 | 做法 |
|---|---|
| 参数是 `FillValue` / `TablePayload` / `OfficeFillRequest` / `FillSession` | **编排器在 `context.Inputs` 里直接放对象实例** ⇒ `ConvertValue` 原样透传 ⇒ ✅ 可行 |
| 参数来自**声明式配置**（JSON 存库） | 到达时是 `JsonElement` / `Dictionary<string,object>` ⇒ **必须由 Skill 自己反序列化**，⛔ 不能声明成强类型参数 |
| 参数是 JSON 字符串 | 声明为 `string`，Skill 内部 `JsonSerializer.Deserialize` |

### 3.4 ★ `ValidateRequired`：**无默认值 = 必填**

**实测行为**：`ValidateRequired` 把「没有默认值的参数」视为必填，缺失时报
`"{skillAttr.Code} 缺少必填入参: xxx"`。

**⇒ 对「复杂对象参数」的规避技巧**（★ 关键）：

```csharp
// ⛔ 错：session 无默认值 ⇒ 被当必填 ⇒ 单项试跑（不带 session）直接失败
FillSession session,

// ✅ 对：给一个默认值 ⇒ 变成可选 ⇒ 单项试跑可自行 new 一个
FillSession? session = null,
```

**⇒ 规则**：**只有「业务上真的必须有」的参数才不给默认值**；其余一律给默认值。

### 3.5 参数命名规范

| 规范 | 值 |
|---|---|
| 风格 | **`snake_case`**（与现有 11 个 Skill 一致：`field_code` / `enterprise_code` / `file_code`） |
| 锚点参数名 | 统一 `anchor_code`（⛔ 不用 `code` / `key`） |
| 表格标签参数名 | 统一 `table_tag` |
| 值类型参数名 | 统一 `value_kind`（值域 `text`/`number`/`date`/`bool`） |
| 格式参数名 | 统一 `number_format` |
| 文档参数名 | 统一 `enterprise_docs`（**已过滤的 markdown 拼接文本**） |
| 提示词参数名 | 统一 `prompt_code` + `instruction` |

### 3.6 ★★ 新增契约类型：`FillSession`（M-1 的解法）

**位置建议**：`CertPlatform.Shared/Office/FillSession.cs`（**放 Shared**，因为 Admin 的 Skill 与编排器都要用）。

```csharp
namespace CertPlatform.Shared.Office;

/// <summary>
/// 一次「文档填写」的会话状态 —— 编排器持有，各 Skill 累积写入。
///
/// <para><b>为什么需要它</b>：<c>38</c> 号 原设计让每个填充 Skill 各收一个
/// <c>OfficeFillRequest</c> 再返回新的；但 <c>OfficeFillRequest</c> 是复杂对象，
/// 走「声明式配置（JSON 存库）→ 反射调用」这条路时会被 <c>ConvertValue</c>
/// <b>原样返回成 JsonElement</b>，永远还原不成强类型 ⇒ 走不通。</para>
///
/// <para><b>本类的作用</b>：把「累积状态」收进一个<b>由编排器直接持有的对象实例</b>，
/// 通过 <c>context.Inputs</c> 传给 Skill（<c>ConvertValue</c> 对复杂类型原样透传）⇒
/// 绕开 JSON 往返，同时天然实现「<b>一份文档只落盘一次</b>」。</para>
/// </summary>
public sealed class FillSession
{
    /// <summary>模板文件编码（<c>cert_standard_directory_file.Code</c>），用于留痕</summary>
    public string TemplateFileCode { get; set; } = string.Empty;

    /// <summary>模板字节（.docx / .xlsx）</summary>
    public byte[] Template { get; set; } = Array.Empty<byte>();

    /// <summary>文件类型：<c>word</c> / <c>excel</c>（决定 fill_table 的定位参数形状）</summary>
    public string FileKind { get; set; } = string.Empty;

    /// <summary>累积的填充请求 —— 各 Skill 往这里写，⛔ 不各自落盘</summary>
    public OfficeFillRequest Request { get; set; } = new();

    /// <summary>★ 已写入锚点 → 来源 Skill（用于「重复赋值检测」与审计）</summary>
    public Dictionary<string, string> AnchorOwners { get; set; } = new(StringComparer.Ordinal);

    /// <summary>待办清单（<c>src_manual</c> 等产出）</summary>
    public List<FillTodo> Todos { get; set; } = new();

    /// <summary>调用轨迹（哪个 Skill 写了什么），写入 <c>cert_doc_fill_log</c></summary>
    public List<string> Trace { get; set; } = new();
}

/// <summary>一处待人工填写</summary>
public sealed class FillTodo
{
    public string AnchorCode { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
    public string Source { get; set; } = "src_manual";
}
```

### 3.7 留痕：`cert_doc_fill_log`（**表未建，本文给 DDL**）

```sql
CREATE TABLE `cert_doc_fill_log` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) NOT NULL,
  `OrgCode` varchar(36) NOT NULL,
  `EnterpriseCode` varchar(36) NOT NULL,
  `StageCode` varchar(36) NOT NULL DEFAULT '',
  `StandardCode` varchar(36) NOT NULL DEFAULT '',
  `TemplateFileCode` varchar(36) NOT NULL COMMENT '模板文件编码',
  `FileKind` varchar(10) NOT NULL DEFAULT '' COMMENT 'word/excel',
  `OutputStoragePath` varchar(512) NOT NULL DEFAULT '' COMMENT '产物路径（⛔ 不覆盖模板）',
  `TotalAnchors` int NOT NULL DEFAULT 0,
  `ResolvedCount` int NOT NULL DEFAULT 0,
  `PendingCount` int NOT NULL DEFAULT 0,
  `Completion` decimal(5,4) NOT NULL DEFAULT 0.0000,
  `RegionCount` int NOT NULL DEFAULT 0,
  `ClonedRows` int NOT NULL DEFAULT 0,
  `Verified` tinyint(1) NOT NULL DEFAULT 0 COMMENT '自验收：无残留锚点且无残留标记',
  `LeftoverTokens` json DEFAULT NULL COMMENT '残留锚点原文',
  `PendingsJson` json DEFAULT NULL COMMENT '待办明细',
  `RetrievedDocCodes` json DEFAULT NULL COMMENT '★ 两级过滤入选的企业文档 Code 清单（可审计）',
  `SkillTrace` json DEFAULT NULL COMMENT '各 Skill 调用轨迹',
  `PromptTokens` int NOT NULL DEFAULT 0,
  `CompletionTokens` int NOT NULL DEFAULT 0,
  `DurationMs` int NOT NULL DEFAULT 0,
  `Status` varchar(20) NOT NULL DEFAULT 'success' COMMENT 'success/partial/failed',
  `Message` varchar(1024) DEFAULT NULL,
  `CreateTime` datetime NOT NULL,
  `CreateBy` varchar(64) DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(64) DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT 0,
  `DeleteBy` varchar(64) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsValid` int NOT NULL DEFAULT 1,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_doc_fill_log_code` (`Code`),
  KEY `idx_doc_fill_log_ent` (`EnterpriseCode`,`TemplateFileCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='文档填写执行留痕';
```

> ⚠️ **对齐铁律**（记忆 §二十）：`IsDeleted` + `IsValid` 双列必须建，且**唯一键不含 `IsDeleted`**。

### 3.8 错误处理规约

| 场景 | 做法 |
|---|---|
| 参数为空/非法 | `SkillResult.Fail("xxx 不能为空")` —— **⛔ 不抛异常** |
| 依赖查不到（如参数不存在） | `SkillResult.Fail("未找到 param_code=X")`，**⛔ 不返回空值**（空值会被当成「填了空」，无法区分「没找到」） |
| LLM 调用失败 | 透传 `resp.Message`（如「AI 端点未配置」） |
| LLM 返回 JSON 不合法 | `IAiFillInvoker` **内部重试 1 次**；仍失败 ⇒ `Fail` |
| **锚点重复赋值** | `Fail("锚点 X 被重复赋值（已有值来自 Y）")` —— ★ **必须报错，⛔ 不静默覆盖** |

---

## 四、`src_global_param` 全局参数取值（一期）

### 4.1 职责

按 `param_code`（+ 可选标准/阶段）从全局参数表取**最特异**的一条值，产出 `FillValue`。

### 4.2 签名（可照抄）

> **🔴 2026-10-03 实施修正 —— 原文有 3 处硬错误 + 1 处结构性阻塞（★ 编码时实读源码 + 核对项目引用后发现）**
>
> | # | 原文（错） | 实际 | 后果 |
> |---|---|---|---|
> | 1 | `CertPlatform.Shared.Entities.Param.FillParamValue` | 实体在 **`CertPlatform.Auditor/Entities/Cert/FillParamValue.cs`**，命名空间 `CertPlatform.Auditor.Entities.Cert` | 按原文写**编译不过**（类型不存在） |
> | 2 | `CertPlatform.Admin.Services.Param.ParamValueResolver` | 实际在 **`CertPlatform.Shared/Fill/ParamValueResolver.cs`**，命名空间 `CertPlatform.Shared.Fill` | 同上 |
> | 3 | `PickMostSpecific(rows)`，`rows` 是 `FillParamValue` | `PickMostSpecific` 入参是 **`IEnumerable<FillParamDef>`**（**定义**表，不是**值**表） | 语义也不对：**去重是对「定义」去重** |
>
> **★★★ 结构性阻塞（要害）**：`FillParamValue` 定义在 **`CertPlatform.Auditor`**，而引用方向是
> **`Auditor → Admin` 单向**（`Auditor.csproj` 引用 `Admin`，反之没有）
> ⇒ **本 Skill（在 Admin 项目内）根本看不到该实体**。
>
> **⇒ 实施采用的做法**：企业已填值由**编排器预取**（编排层在 Auditor，两边都能看）后经
> `saved_value` / `saved_value_source` / `saved_is_manual_edited` 三个入参传入。
> 本 Skill 只查 **`FillParamDef`（在 Shared ✅）** + 企业档案，然后调
> **`ParamValueResolver.Resolve`**（全项目唯一决策点）。好处：① Skill 近乎**纯函数**、可单测；
> ② 取值语义仍**只有一处**；③ 不为一个字段搬实体（跨项目重构）。
> ⚠️ 若日后编排层迁进 Admin，则把 `FillParamValue` 移到 `CertPlatform.Shared/Entities/Cert/`
> （与 `FillParamDef` 成对）即可改回 Skill 内查询 —— **但决策点仍只能是 `ParamValueResolver`**。

**✅ 已实施版本**（`CertPlatform.Admin/Services/Workflow/Skills/Fill/SrcGlobalParamSkill.cs`，**以代码为准**）：

```csharp
using CertPlatform.Shared.Entities.Cert;      // FillParamDef（Shared ✅）
using CertPlatform.Shared.Office;             // FillValue / FillValueFactory
using YZH.Core.DataBase.Interfaces;
using SharedFill = CertPlatform.Shared.Fill;  // ★ 别名：避免与 .Skills.Fill 命名空间混淆

namespace CertPlatform.Admin.Services.Workflow.Skills.Fill
{
    [Skill(Code = "src_global_param", Name = "全局参数取值", ReturnType = "json",
        Description = "按参数编码取全局参数值（标准/阶段特化，取最特异一条）。产出 FillValue。")]
    public static class SrcGlobalParamSkill
    {
        public static async Task<SkillResult> ExecuteAsync(
            [SkillParam(Description = "参数编码，如 company_name")] string param_code,
            [SkillParam(Description = "锚点编码（空=用 param_code）")] string? anchor_code = null,
            [SkillParam(Description = "标准编码（空=只命中「不限标准」的定义）")] string? standard_code = null,
            [SkillParam(Description = "阶段编码（空=只命中「不限阶段」的定义）")] string? stage_code = null,
            [SkillParam(Description = "机构编码（空=不加机构过滤）")] string? org_code = null,
            [SkillParam(Description = "企业编码（给了才按企业档案自动带出）")] string? enterprise_code = null,
            [SkillParam(Description = "值类型（空=用参数定义的值类型）")] string? value_kind = null,
            [SkillParam(Description = "格式串（.NET 方言）")] string? number_format = null,
            [SkillParam(Description = "★ 企业已填值（编排器预取）")] string? saved_value = null,
            [SkillParam(Description = "已填值来源：auto/manual/ai/import")] string? saved_value_source = null,
            [SkillParam(Description = "企业是否人工改过")] bool saved_is_manual_edited = false,
            [FromService] IDbOrm db = null!, CancellationToken ct = default)
        {
            // ① 取候选【定义】。★ 条件 = OrgCode + (StandardCode='' OR S) + (StageCode='' OR P)
            //    —— OR 组合，FilterItem 表达不了 ⇒ 必须手写表达式（同 38 号 §「生效参数集」）
            //    ★ 用两个分支而非「!hasOrg || …」：常量折叠交给 ORM 容易踩翻译坑，显式分支最稳
            var defs = string.IsNullOrWhiteSpace(org_code)
                ? (await db.GetListAsync<FillParamDef>(x => x.ParamCode == param_code
                        && (x.StandardCode == "" || x.StandardCode == standard_code)
                        && (x.StageCode == "" || x.StageCode == stage_code))).Data
                : (await db.GetListAsync<FillParamDef>(x => x.ParamCode == param_code
                        && x.OrgCode == org_code
                        && (x.StandardCode == "" || x.StandardCode == standard_code)
                        && (x.StageCode == "" || x.StageCode == stage_code))).Data;

            // ② 去重口径：全项目唯一 = PickMostSpecific（⚠️ 入参是【定义】）
            var def = SharedFill.ParamValueResolver.PickMostSpecific(defs ?? new()).FirstOrDefault();
            if (def == null) return SkillResult.Fail($"未找到 param_code={param_code}");

            // ③ 企业档案快照（⚠️ GetOneAsync 返回 Result<T> ⇒ 必须取 .Data）
            var enterprise = new SharedFill.EnterpriseInfo();
            if (!string.IsNullOrWhiteSpace(enterprise_code))
            {
                var ent = (await db.GetOneAsync<Enterprise>(x => x.Code == enterprise_code)).Data;
                if (ent != null) enterprise = MapToInfo(ent);
            }

            // ④ 取值决策 + 组值（★ 抽成 BuildValue 以便单测，不依赖 DB/DI）
            var (ok, value, error) = BuildValue(def, enterprise,
                saved_value, saved_value_source, saved_is_manual_edited,
                anchor_code ?? param_code, value_kind, number_format);

            if (!ok) return SkillResult.Fail(error ?? "取值失败");

            return SkillResult.Ok(new Dictionary<string, object>
            {
                ["value"] = value!, ["anchor_code"] = anchor_code ?? param_code, ["hit"] = true,
            }, value!.Confidence);
        }

        /// <summary>★ 纯函数：定义 + 企业档案 + 已填值 → FillValue（可单测）</summary>
        public static (bool Ok, FillValue? Value, string? Error) BuildValue(
            FillParamDef def, SharedFill.EnterpriseInfo enterprise,
            string? savedValue, string? savedValueSource, bool savedIsManualEdited,
            string anchorCode, string? valueKind, string? numberFormat)
        {
            // ★ 全项目唯一决策点（auto=恒实时取企业档案 / both=可覆盖 / manual=手工）
            var decision = SharedFill.ParamValueResolver.Resolve(
                def, enterprise, savedValue, savedValueSource, savedIsManualEdited);

            if (string.IsNullOrWhiteSpace(decision.Value))
                return (false, null, $"未取到 param_code={def.ParamCode} 的值（{decision.SourceRef}）");

            var kind = string.IsNullOrWhiteSpace(valueKind) ? def.ValueType : valueKind;
            var (ok, value, error) = FillValueFactory.TryCreate(anchorCode, decision.Value, kind, numberFormat);
            if (!ok) return (false, null, error);

            value!.Source = decision.SourceRef;
            value.Confidence = 1.0;
            return (true, value, null);
        }
    }
}
```

> **⚠️ 与 §4.2 原签名的差异（4 点，均为修正所致）**：
> ① 新增 `org_code` / `enterprise_code` / `saved_value` / `saved_value_source` / `saved_is_manual_edited` 五个入参；
> ② `value_kind` 默认由 `"text"` 改为 **`null`**（空 ⇒ 用 `FillParamDef.ValueType`，比硬编码 `text` 更准）；
> ③ `param_code` **仍是唯一必填入参**（无默认值 ⇒ `ValidateRequired` 视为必填，其余全部给默认值）；
> ④ 组值改走 **`FillValueFactory.TryCreate`**（★ 唯一构造入口，⛔ 不各写一份）。

### 4.3 参数表

| 参数 | 类型 | 必填 | 默认 | 说明 |
|---|---|---|---|---|
| `param_code` | string | **✅** | — | 参数编码（**唯一必填入参**） |
| `anchor_code` | string? | — | `param_code` | 写入锚点键 |
| `standard_code` | string? | — | null | 标准特化 |
| `stage_code` | string? | — | null | 阶段特化 |
| `org_code` | string? | — | null | 机构隔离键（空 ⇒ 不加过滤；⚠️ 多机构库中应显式传） |
| `enterprise_code` | string? | — | null | 给了才按企业档案自动带出（`auto`/`both` 的初值来源） |
| `value_kind` | string? | — | **null** | `text`/`number`/`date`/`bool`/`enum`；**空 ⇒ 用 `FillParamDef.ValueType`** |
| `number_format` | string? | — | null | 格式串（**.NET 方言**，见 §十六） |
| `saved_value` | string? | — | null | ★ **企业已填值（编排器预取）** —— 见 §4.2 修正说明 |
| `saved_value_source` | string? | — | null | 已填值来源：`auto`/`manual`/`ai`/`import` |
| `saved_is_manual_edited` | bool | — | `false` | 企业是否人工改过 |

### 4.4 输出

| 键 | 类型 | 说明 |
|---|---|---|
| `value` | **`FillValue`**（对象实例） | 供 `fill_cell` 消费 |
| `anchor_code` | string | 回显 |
| `hit` | bool | 恒 `true`（未命中走 `Fail`） |

### 4.5 边界与错误

| 场景 | 行为 |
|---|---|
| `param_code` 空 | `Fail("param_code 不能为空")` |
| 查不到值 | `Fail("未找到 param_code=...")` —— ⛔ **不返回空值** |
| 同特异度多条 | 由 `PickMostSpecific` 内部规则裁决（**沿用，⛔ 不在此重写**） |
| `value_kind=number` 但值非数字 | **`Fail("参数值 X 不是合法数字")`** ⇒ ⛔ 不静默降级成 text（否则 Excel 数字变文本，`38` 号 §4.2 已警告） |

### 4.6 测试用例

| # | 输入 | 期望 |
|---|---|---|
| T1 | `param_code=ENT_NAME`，库中有通用值 | `success=true`，`value.Text` = 企业名，`anchor_code=ENT_NAME` |
| T2 | `param_code=ENT_NAME`，同时有通用值与「9001+复审」特化值 | 取**特化值**（`Specificity` 更高） |
| T3 | `param_code=NOT_EXIST` | `success=false`，`error` 含 `未找到` |
| T4 | `param_code=ENT_NAME` + `value_kind=number`，值是 `ABC` | `success=false`，`error` 含 `不是合法数字` |
| T5 | `anchor_code=company_name` | `value.AnchorCode == "company_name"`（⛔ 不是 `ENT_NAME`） |

---

## 五、`src_manual` 人工待办声明（一期）

### 5.1 职责

声明「**该锚点需人工填写**」—— 产出**待办标记**，⛔ **不产值**。让 `onMissing: todo` 有落点。

### 5.2 签名

```csharp
[Skill(
    Code = "src_manual",
    Name = "人工待办声明",
    ReturnType = "json",
    Description = "声明「该锚点需人工填写」，产出待办标记（不产值）。"
)]
public static class SrcManualSkill
{
    public static Task<SkillResult> ExecuteAsync(
        [SkillParam(Description = "锚点编码")] string anchor_code,
        [SkillParam(Description = "给填写人的提示，如「请填写企业名称」")] string hint,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(anchor_code))
            return Task.FromResult(SkillResult.Fail("anchor_code 不能为空"));

        var todo = new FillTodo
        {
            AnchorCode = anchor_code,
            Hint = string.IsNullOrWhiteSpace(hint) ? "请人工填写" : hint,
            Source = "src_manual",
        };

        // ★ 注意：不返回 value ⇒ 上游组合器（firstHit）会继续找下一个来源；
        //   全部落空时，编排器把 todos 转成 OfficeFillReport.Pendings
        return Task.FromResult(SkillResult.Ok(new Dictionary<string, object>
        {
            ["todo"] = todo,
            ["anchor_code"] = anchor_code,
            ["hit"] = false,
        }));
    }
}
```

### 5.3 ★ 为什么返回 `hit=false`

来源链的语义是 `firstHit`（第一个命中的来源胜出）。`src_manual` **没有值** ⇒
必须让组合器**继续往下找**；只有**全部来源都落空**时，才把它变成 `Pendings`。
⇒ **`hit=false` 是正确语义，⛔ 不是 bug。**

### 5.4 测试用例

| # | 输入 | 期望 |
|---|---|---|
| T1 | 正常 | `success=true`，`hit=false`，`todo.Hint` 回显 |
| T2 | `anchor_code` 空 | `success=false` |
| T3 | `hint` 空 | `todo.Hint == "请人工填写"` |

---

## 六、`src_semantic` AI 语义改写（一期，AI）

### 6.1 职责

把**模板里的标准原文**按**企业实际情况**改写。**★ 不需要企业文档**（`38` 号 §15.2 已界定）。

### 6.2 签名

```csharp
[Skill(
    Code = "src_semantic",
    Name = "AI 语义改写",
    ReturnType = "json",
    Description = "按企业实际情况改写标准原文（不依赖企业文档）。"
)]
public static class SrcSemanticSkill
{
    public static async Task<SkillResult> ExecuteAsync(
        [SkillParam(Description = "锚点编码")] string anchor_code,

        [SkillParam(Description = "待改写的标准原文", BindMode = SkillParamBindMode.LinkOrConstant)]
        string source_text,

        [SkillParam(Description = "改写要求（模板里这一项的填写说明）", BindMode = SkillParamBindMode.LinkOrConstant)]
        string instruction,

        [SkillParam(Description = "提示词编码 cert_doc_fill_prompt.Code；空=用 Skill 默认 PromptTemplate")]
        string? prompt_code = null,

        [SkillParam(Description = "企业画像/参数摘要（可选，用于风格与事实校正）",
                    BindMode = SkillParamBindMode.LinkOrConstant)]
        string? enterprise_context = null,

        [FromService] IDbOrm db = null!,
        [FromService] IAiFillInvoker llm = null!,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(source_text))
            return SkillResult.Fail("source_text 不能为空");

        // ① 组装（★ 与批量路径共用同一装配器 —— 见 §十二.2）
        var prompt = await AiFillPromptBuilder.BuildSingleAsync(
            db, prompt_code, skillCode: "src_semantic",
            payload: new Dictionary<string, object>
            {
                ["anchor_code"] = anchor_code,
                ["source_text"] = source_text,
                ["instruction"] = instruction,
                ["enterprise_context"] = enterprise_context ?? "",
            }, ct);

        // ② 调 LLM（★ 复用 IAiFillInvoker，内含重试 + NormalizeJson）
        var resp = await llm.InvokeAsync(prompt, ct);
        if (!resp.Success) return SkillResult.Fail(resp.Error ?? "AI 调用失败");

        // ③ 取字段（约定：semantic.{anchor_code}）
        var text = AiFillJsonReader.ReadString(resp.Json, "semantic", anchor_code);
        if (text == null)
            return SkillResult.Fail($"AI 未返回 semantic.{anchor_code}");

        var value = new FillValue
        {
            AnchorCode = anchor_code,
            Kind = FillValueKind.Text,
            Text = text,
            Source = "AI 语义改写",
            Confidence = AiFillJsonReader.ReadConfidence(resp.Json, "semantic", anchor_code),
        };

        return SkillResult.Ok(new Dictionary<string, object>
        {
            ["value"] = value,
            ["anchor_code"] = anchor_code,
            ["hit"] = true,
        });
    }
}
```

### 6.3 边界

| 场景 | 行为 |
|---|---|
| `source_text` 空 | `Fail`（⛔ 不产空值） |
| AI 未返回该键 | `Fail($"AI 未返回 semantic.{anchor_code}")` ⇒ ★ **由编排器回退成单项重试**（`38` 号 §15.4 缺项回退） |
| AI 返回空字符串 | **`hit=true` 但 `value.Text=""`** —— ⚠️ 这是「AI 认为该改写为空」的**合法结果**，与「没返回」区分 |
| `instruction` 空 | 允许（退化为「润色」） |

---

## 七、`src_ai_field` AI 单元格填写（一期，AI，**核心**）

### 7.1 职责

**★ 本系统最核心的能力**：给定「**这个格子要填什么**」（`instruction`）+「**已过滤的企业文档 markdown**」，
让 LLM **动态分析**出单元格值。**形态确定（单元格）、内容不确定（企业有什么）**。

### 7.2 签名

```csharp
[Skill(
    Code = "src_ai_field",
    Name = "AI 单元格填写",
    ReturnType = "json",
    Description = "提示词 + 已过滤企业文档 → 动态分析出单元格值。"
)]
public static class SrcAiFieldSkill
{
    public static async Task<SkillResult> ExecuteAsync(
        [SkillParam(Description = "锚点编码")] string anchor_code,

        [SkillParam(Description = "这个格子要填什么（模板里这一项的填写说明）",
                    BindMode = SkillParamBindMode.LinkOrConstant)]
        string instruction,

        [SkillParam(Description = "★ 已过滤的企业文档 markdown（由 IEnterpriseDocRetriever 产出）",
                    BindMode = SkillParamBindMode.LinkOrConstant)]
        string? enterprise_docs = null,

        [SkillParam(Description = "提示词编码；空=用 Skill 默认 PromptTemplate")]
        string? prompt_code = null,

        [SkillParam(Description = "值类型：text/number/date/bool")]
        string value_kind = "text",

        [SkillParam(Description = "Excel 数字/日期格式串")]
        string? number_format = null,

        [FromService] IDbOrm db = null!,
        [FromService] IAiFillInvoker llm = null!,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(instruction))
            return SkillResult.Fail("instruction 不能为空");

        var prompt = await AiFillPromptBuilder.BuildSingleAsync(
            db, prompt_code, skillCode: "src_ai_field",
            payload: new Dictionary<string, object>
            {
                ["anchor_code"] = anchor_code,
                ["instruction"] = instruction,
                ["enterprise_docs"] = enterprise_docs ?? "",
            }, ct);

        var resp = await llm.InvokeAsync(prompt, ct);
        if (!resp.Success) return SkillResult.Fail(resp.Error ?? "AI 调用失败");

        // ★ 约定：fields.{anchor_code} = { "value": ..., "confidence": 0.9, "source_doc": "..." }
        var node = AiFillJsonReader.ReadObject(resp.Json, "fields", anchor_code);
        if (node == null)
            return SkillResult.Fail($"AI 未返回 fields.{anchor_code}");

        var rawValue = AiFillJsonReader.ReadRaw(node.Value, "value");
        if (rawValue == null)
            return SkillResult.Fail($"fields.{anchor_code} 缺 value");

        var value = FillValueFactory.FromRaw(anchor_code, rawValue, value_kind, number_format);
        value.Source = AiFillJsonReader.ReadString(node.Value, "source_doc") ?? "AI 单元格填写";
        value.Confidence = AiFillJsonReader.ReadConfidence(node.Value, "confidence");

        return SkillResult.Ok(new Dictionary<string, object>
        {
            ["value"] = value,
            ["anchor_code"] = anchor_code,
            ["hit"] = true,
            ["source_doc"] = value.Source,
        });
    }
}
```

### 7.3 ★★ 关键设计点：`value_kind` 由**锚点属性**给，⛔ 不由 AI 猜

**`38` 号 §4.2 已定死**：Excel 传错 `SetCellValue` 重载**不报错但结果错**。
⇒ **类型必须由层 2 显式给出**，而 `src_ai_field` 就是层 2。
⇒ **AI 只负责「值是什么」，⛔ 不负责「值是什么类型」** —— 类型来自 `cert_doc_template_anchor.ValueType`。

### 7.4 边界

| 场景 | 行为 |
|---|---|
| `enterprise_docs` 为空 | ⚠️ **允许调用**（AI 会答「未提供资料」）⇒ 但**编排器应记 `Pending`**，因为「没资料」和「资料里没有」不是一回事 |
| AI 返回 `null` 值 | `Fail` ⇒ 回退单项重试 |
| AI 返回多个候选 | 取 `value` 字段；⚠️ 多候选信息进 `source_doc` 供人工复核 |

### 7.5 测试用例

| # | 输入 | 期望 |
|---|---|---|
| T1 | 1 份真企业 markdown + `instruction="企业营业执照上的注册资本"` | `success=true`，`value.Text` 为金额，`value.Source` 非空 |
| T2 | `value_kind=number` | `value.Kind == Number`，`value.Number` 有值 |
| T3 | `instruction` 空 | `success=false` |
| T4 | AI 未返回该锚点 | `success=false`，`error` 含 `未返回 fields.` |
| T5 | `enterprise_docs=""` | `success=true` 但值应为「未找到」，`confidence` 低 |

---

## 八、`src_ai_table` AI 表格填写（一期，AI，**核心**）

### 8.1 职责

给定「**表格要什么列**」（来自**模板样板行的 `{{col:X}}` 扫描结果**）+「已过滤的企业文档」，
让 LLM 产出**对象数组**（`TablePayload`）。

### 8.2 签名

```csharp
[Skill(
    Code = "src_ai_table",
    Name = "AI 表格填写",
    ReturnType = "json",
    Description = "提示词 + 已过滤企业文档 → 动态分析出表格数据。"
)]
public static class SrcAiTableSkill
{
    public static async Task<SkillResult> ExecuteAsync(
        [SkillParam(Description = "表格标签（对应模板 {{table:Tag}}）")] string table_tag,

        [SkillParam(Description = "列定义 JSON：[{\"field_code\":\"name\",\"title\":\"姓名\",\"kind\":\"text\"}]",
                    BindMode = SkillParamBindMode.LinkOrConstant)]
        string columns_json,

        [SkillParam(Description = "表格要填什么（填写说明）", BindMode = SkillParamBindMode.LinkOrConstant)]
        string instruction,

        [SkillParam(Description = "★ 已过滤的企业文档 markdown", BindMode = SkillParamBindMode.LinkOrConstant)]
        string? enterprise_docs = null,

        [SkillParam(Description = "提示词编码；空=用 Skill 默认 PromptTemplate")]
        string? prompt_code = null,

        [SkillParam(Description = "最多返回行数（保护，0=不限制）")] int max_rows = 200,

        [FromService] IDbOrm db = null!,
        [FromService] IAiFillInvoker llm = null!,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(table_tag))
            return SkillResult.Fail("table_tag 不能为空");
        if (string.IsNullOrWhiteSpace(columns_json))
            return SkillResult.Fail("columns_json 不能为空");

        var prompt = await AiFillPromptBuilder.BuildSingleAsync(
            db, prompt_code, skillCode: "src_ai_table",
            payload: new Dictionary<string, object>
            {
                ["table_tag"] = table_tag,
                ["columns_json"] = columns_json,
                ["instruction"] = instruction,
                ["enterprise_docs"] = enterprise_docs ?? "",
            }, ct);

        var resp = await llm.InvokeAsync(prompt, ct);
        if (!resp.Success) return SkillResult.Fail(resp.Error ?? "AI 调用失败");

        // ★ 约定：tables.{table_tag} = { "columns": [...], "rows": [ {...}, ... ] }
        var table = AiFillJsonReader.ReadObject(resp.Json, "tables", table_tag);
        if (table == null)
            return SkillResult.Fail($"AI 未返回 tables.{table_tag}");

        var payload = TablePayloadFactory.FromAiNode(table.Value, table_tag, columns_json);
        if (payload.Rows.Count == 0)
            return SkillResult.Fail($"tables.{table_tag}.rows 为空");
        if (max_rows > 0 && payload.Rows.Count > max_rows)
            payload.Rows = payload.Rows.Take(max_rows).ToList();

        return SkillResult.Ok(new Dictionary<string, object>
        {
            ["table"] = payload,                 // ★ TablePayload 实例
            ["table_tag"] = table_tag,
            ["row_count"] = payload.Rows.Count,
            ["hit"] = true,
        });
    }
}
```

### 8.3 ★ 为什么 `columns_json` 是**输入**而不是让 AI 自己定列

**`38` 号 §13.4 第 5b 条已定**：**列由模板决定**（模板样板行写 `{{col:FieldCode}}`，S5 扫描出来）。
⇒ AI **只负责填行**，⛔ **不负责定列** —— 否则 AI 返回的列名与模板列对不上，`fill_table` 的列映射层会静默丢数据。

### 8.4 `TablePayload` 契约（★ 与 `38` 号 §14.6 一致，本文补 `RowSource`）

```csharp
public sealed class TablePayload
{
    public string? TableTag { get; set; }
    public List<TableColumn> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public Dictionary<string, object?> Totals { get; set; } = new();
}

public sealed class TableColumn
{
    public string FieldCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;   // ★ 给 AI 看的列名（中文）
    public string Kind { get; set; } = "text";
    public string? Format { get; set; }
}
```

> **★ `Title` 是新增字段**：AI 需要**中文列名**才能理解要填什么；`FieldCode` 是给 `fill_table` 映射用的。
> `38` 号 §14.6 的 `TableColumn` 只有 `FieldCode`/`Kind`/`Format` ⇒ **AI 看不懂 `field_code="reg_capital"` 要填什么**。

---

## 九、`src_dict` 字典取值（**二期**，本文只给接口）

```csharp
[Skill(Code = "src_dict", Name = "字典取值", ReturnType = "json",
       Description = "按字典编码取字典项（支持级联）。")]
public static class SrcDictSkill
{
    public static async Task<SkillResult> ExecuteAsync(
        [SkillParam(Description = "字典编码，如 cert_std_stage")] string dict_no,
        [SkillParam(Description = "字典值（空=取默认项）")] string? dict_value = null,
        [SkillParam(Description = "锚点编码（空=用 dict_no）")] string? anchor_code = null,
        [SkillParam(Description = "级联路径，如 /A/B")] string? cascade_path = null,
        [FromService] IDbOrm db = null!,
        CancellationToken ct = default)
    { /* 二期实现 */ }
}
```

> **★ 为什么是二期**：现有需求里「字典」都能被**全局参数**覆盖（记忆：「字典取值」未被单独要求）。
> ⇒ **一期只注册不实现**（`IsActive=0`，见 §2.3 的 SQL），避免「注册了但调用报错」。

---

## 十、`fill_cell` 单元格填写（一期，操作类）

### 10.1 职责

把**值**装配进 `FillSession.Request.Values`（含页眉）。**★ ⛔ 不落盘**。

### 10.2 签名（**已按 M-1/M-2 修正**）

```csharp
[Skill(
    Code = "fill_cell",
    Name = "单元格填写",
    ReturnType = "json",
    Description = "把值装配成单元格填充指令（含页眉）。★ 不落盘。"
)]
public static class FillCellSkill
{
    public static Task<SkillResult> ExecuteAsync(
        [SkillParam(Description = "★ 填充会话（编排器直接放对象实例；空=新建）",
                    BindMode = SkillParamBindMode.LinkOrConstant)]
        FillSession? session = null,

        [SkillParam(Description = "值字典：{AnchorCode: FillValue 或 值描述}",
                    BindMode = SkillParamBindMode.LinkOrConstant)]
        IDictionary<string, object>? values = null,

        [SkillParam(Description = "是否处理 Word 页眉")] bool fill_header = true,
        [SkillParam(Description = "未命中锚点是否保留原文（仅模板调试）")] bool keep_unresolved = false,
        CancellationToken ct = default)
    {
        if (values == null || values.Count == 0)
            return Task.FromResult(SkillResult.Fail("values 不能为空"));

        session ??= new FillSession();               // ★ 单项试跑可自行新建
        session.Request.FillHeader = fill_header;
        session.Request.KeepUnresolvedAsIs = keep_unresolved;

        var written = 0;
        foreach (var kv in values)
        {
            // ① 归一：FillValue 实例（编排器直连）或 值描述（JSON 链）⇒ 都转成 FillValue
            var fv = FillValueFactory.Coerce(kv.Key, kv.Value);
            if (fv == null)
                return Task.FromResult(SkillResult.Fail($"锚点 {kv.Key} 的值无法识别：{kv.Value?.GetType().Name}"));

            // ② ★ 重复赋值必须报错（⛔ 不静默覆盖 —— 静默覆盖是 38 号 §20 类缺陷）
            if (session.Request.Values.TryGetValue(kv.Key, out var exist))
                return Task.FromResult(SkillResult.Fail(
                    $"锚点 {kv.Key} 被重复赋值（已有值来自 {exist.Source}）"));

            session.Request.Values[kv.Key] = fv;
            session.AnchorOwners[kv.Key] = "fill_cell";
            written++;
        }

        session.Trace.Add($"fill_cell: +{written} anchors");

        return Task.FromResult(SkillResult.Ok(new Dictionary<string, object>
        {
            ["session"] = session,
            ["anchor_count"] = session.Request.Values.Count,
            ["written"] = written,
        }));
    }
}
```

### 10.3 参数表

| 参数 | 类型 | 必填 | 默认 | 说明 |
|---|---|---|---|---|
| `session` | `FillSession?` | — | **null ⇒ 新建** | ★ 给默认值 ⇒ 不被当必填（§3.4） |
| `values` | `IDictionary<string, object>?` | ✅（内部判） | null | 值字典 |
| `fill_header` | bool | — | `true` | Word 页眉 |
| `keep_unresolved` | bool | — | `false` | 调试用 |

### 10.4 边界

| 场景 | 行为 |
|---|---|
| `values` 空 | `Fail` |
| 锚点重复 | **`Fail`** ⇒ ⛔ 不覆盖（避免「后写的把先写的抹掉」） |
| 值类型不认识 | `Fail` ⇒ ⛔ 不猜 |
| `session` 为 null | **新建**（支持单项试跑） |

---

## 十一、`fill_table` 表格填写（一期，操作类）

### 11.1 职责

把 `TablePayload` 投影成 `OfficeFillRegion.Rows`（**列映射层**）。**★ ⛔ 不落盘**。

### 11.2 签名

```csharp
[Skill(
    Code = "fill_table",
    Name = "表格填写",
    ReturnType = "json",
    Description = "把表格数据装配成区域填充指令（Word 表格 / Excel 区域）。★ 不落盘。"
)]
public static class FillTableSkill
{
    public static Task<SkillResult> ExecuteAsync(
        [SkillParam(Description = "★ 填充会话（编排器直接放对象实例；空=新建）",
                    BindMode = SkillParamBindMode.LinkOrConstant)]
        FillSession? session = null,

        [SkillParam(Description = "表格数据：{table_tag, columns, rows, totals}",
                    BindMode = SkillParamBindMode.LinkOrConstant)]
        TablePayload? table = null,

        [SkillParam(Description = "Excel 起始行（0-based；<0=按模板样板行自动探测）")] int start_row = -1,
        [SkillParam(Description = "Excel 起始列（0-based；<0=自动探测）")] int start_col = -1,
        [SkillParam(Description = "Excel 工作表名（空=第 1 个工作表）")] string? sheet_name = null,
        CancellationToken ct = default)
    {
        if (table == null || table.Columns.Count == 0)
            return Task.FromResult(SkillResult.Fail("table.columns 不能为空"));
        if (table.Rows.Count == 0)
            return Task.FromResult(SkillResult.Fail("table.rows 不能为空"));

        session ??= new FillSession();

        // ① 列映射：按 table.Columns 的顺序，把每行的字典投影成 List<FillValue?>
        var matrix = new List<List<FillValue?>>(table.Rows.Count);
        foreach (var row in table.Rows)
        {
            var line = new List<FillValue?>(table.Columns.Count);
            foreach (var col in table.Columns)
            {
                row.TryGetValue(col.FieldCode, out var raw);
                line.Add(TableValueMapper.ToFillValue(col, raw));   // null ⇒ 写空
            }
            matrix.Add(line);
        }

        // ② ★ 按 FileKind 分派定位参数形状（⛔ 不暴露给用户配）
        var region = session.FileKind == "excel"
            ? new OfficeFillRegion
              {
                  Kind = OfficeRegionKind.ExcelRange,
                  SheetName = sheet_name,
                  StartRow = start_row < 0 ? 0 : start_row,
                  StartCol = start_col < 0 ? 0 : start_col,
                  Rows = matrix,
              }
            : new OfficeFillRegion
              {
                  Kind = OfficeRegionKind.WordTable,
                  TableTag = table.TableTag,
                  Rows = matrix,
              };

        session.Request.Regions.Add(region);
        session.Trace.Add($"fill_table: {table.TableTag} {matrix.Count} rows");

        return Task.FromResult(SkillResult.Ok(new Dictionary<string, object>
        {
            ["session"] = session,
            ["region_count"] = session.Request.Regions.Count,
            ["row_count"] = matrix.Count,
            ["location"] = region.Describe(),
        }));
    }
}
```

### 11.3 ★ 定位参数形状由 `FileKind` 分派（对应 `38` 号 §4.2「泄漏点②」）

| `FileKind` | `Kind` | 用的字段 | 忽略的字段 |
|---|---|---|---|
| `excel` | `ExcelRange` | `SheetName` + `StartRow` + `StartCol` | `TableTag` |
| `word` | `WordTable` | `TableTag` | `SheetName`/`StartRow`/`StartCol` |

⇒ **同一份 `TablePayload` 在 Word/Excel 下走不同分支，但 Skill 签名完全相同** —— 这正是 `38` 号 §4.2 说的「对外统一，对内分派」。

### 11.4 行数对齐语义（★ 沿用层 1，⛔ 不在此实现）

| 情况 | 层 1 行为 |
|---|---|
| 数据行 > 模板行 | **克隆最后一行** |
| 数据行 < 模板行 | **多余行留空，⛔ 不删除** |
| 列多 | **丢弃** |

### 11.5 测试用例

| # | 输入 | 期望 |
|---|---|---|
| T1 | `FileKind=excel` + 8 行数据 + `start_row=2,start_col=0` | `Regions[0].Kind==ExcelRange`，`Rows.Count==8` |
| T2 | `FileKind=word` + `table_tag=items` | `Regions[0].Kind==WordTable`，`TableTag=="items"` |
| T3 | 数据行某列缺键 | 该格 `null`（**写空**，⛔ 不报错） |
| T4 | `columns` 空 | `Fail("table.columns 不能为空")` |
| T5 | `session` 已有 1 个 region | 追加为第 2 个（⛔ 不覆盖） |

---

## 十二、AI 三件套的公共件（★ 抽出来，⛔ 不各写一份）

### 12.1 `IAiFillInvoker` —— 继承 `LlmExtractSkill` 的 5 项经验

```csharp
public interface IAiFillInvoker
{
    Task<AiFillInvokeResult> InvokeAsync(AiFillInvokeRequest req, CancellationToken ct);
}

public sealed class AiFillInvokeRequest
{
    public string SystemPrompt { get; set; } = "";
    public string UserPrompt { get; set; } = "";
    public string? OutputSchema { get; set; }
    public decimal Temperature { get; set; } = 0.00m;   // ★ 低温，保证可复现
    public int MaxTokens { get; set; } = 8192;          // ★ 4096 会截断（LlmExtractSkill 的教训）
    public bool RetryOnInvalidJson { get; set; } = true;
}
```

**★ 必须继承的 5 项（逐条来自实读 `LlmExtractSkill.cs`）**：

| # | 经验 | 为什么 |
|---|---|---|
| 1 | **两次重试 + 提示词补正**（`attempt 0/1`，第 2 次追加「仅输出符合 Schema 的 JSON」） | LLM 首次返回带 markdown 围栏是常态 |
| 2 | **`MaxTokens = 8192`** | 默认 4096 会在**字符串中间截断** ⇒ JSON 解析失败 |
| 3 | **`NormalizeJson` 深转换**（`JsonElement` → `Dictionary<string,object>`/`List<object>`） | ⛔ 不转的话下游拿到 `JsonElement`，`ConvertValue` 原样返回 ⇒ **取不到值** |
| 4 | **六键从 `cert_sys_config` 注入**（`__model`/`__base_url`/`__api_key`） | 保证「实际调用模型 = 系统参数配置的模型」 |
| 5 | **`LlmInvokeService` 已内建**：markdown 围栏剥离（`CleanJsonText`）+ **截断抢救**（`TryRepairTruncatedJson`） | ⛔ 不要重写 |

> **★ 复用而非继承的说明**：`LlmExtractSkill` 是 `ISkillNode`（§3.1 已裁定不用该范式）。
> ⇒ **把它的 `Render` / `NormalizeJson` / 重试逻辑**搬到 `AiFillInvoker` 实现里，⛔ 不要改 `LlmExtractSkill` 本身
> （它仍被现有流程使用，`Q-2` 才建议清理）。

### 12.2 `AiFillPromptBuilder` —— **批量与单项的唯一装配器**

```csharp
public interface IAiFillPromptBuilder
{
    /// <summary>单项：一个锚点一次请求（试跑 / 缺项回退）</summary>
    Task<AiFillInvokeRequest> BuildSingleAsync(IDbOrm db, string? promptCode, string skillCode,
        IDictionary<string, object> payload, CancellationToken ct);

    /// <summary>批量：一份文档一次请求（★ 生产路径）</summary>
    Task<AiFillInvokeRequest> BuildBatchAsync(IDbOrm db, string? promptCode,
        AiFillBuildContext ctx, CancellationToken ct);
}
```

**★ 为什么必须共用**：`38` 号 §15.4 已警告 —— **两套 prompt 组装逻辑必然漂移**
（「页面上试跑是对的，正式跑却不对」）。

### 12.3 `cert_doc_fill_prompt.UserTemplate` 全文示例（★ 可直接录入）

```text
你是认证文档填写助手。请根据「待填锚点」与「企业文档」，为每个锚点给出值。

## 待填锚点
{{__FILL__.anchors}}

## 企业文档（已按相关性过滤）
{{__FILL__.enterprise_docs}}

## 要求
1. 只使用上面企业文档中出现的事实，⛔ 不要编造。
2. 找不到依据的锚点，value 填 null，并在 note 里说明「资料中未提及」。
3. 严格按以下 JSON 结构输出，⛔ 不要输出解释文字。

## 输出结构
{{__FILL__.output_schema}}
```

### 12.4 `OutputSchema`（★ 具体 JSON，用于校验 AI 返回）

```json
{
  "type": "object",
  "required": ["semantic", "fields", "tables"],
  "properties": {
    "semantic": {
      "type": "object",
      "description": "锚点编码 → 改写后的文本",
      "additionalProperties": { "type": "string" }
    },
    "fields": {
      "type": "object",
      "description": "锚点编码 → { value, confidence, source_doc, note }",
      "additionalProperties": {
        "type": "object",
        "properties": {
          "value": {},
          "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
          "source_doc": { "type": "string" },
          "note": { "type": "string" }
        }
      }
    },
    "tables": {
      "type": "object",
      "description": "表格标签 → 行数组",
      "additionalProperties": {
        "type": "array",
        "items": { "type": "object" }
      }
    }
  }
}
```

### 12.5 ★★ `semantic` / `fields` / `tables` 三段与 3 个 Skill 的切片关系

| 输出段 | 消费 Skill | 取值路径 |
|---|---|---|
| `semantic` | `src_semantic` | `semantic.{anchor_code}`（string） |
| `fields` | `src_ai_field` | `fields.{anchor_code}.value` |
| `tables` | `src_ai_table` | `tables.{table_tag}`（数组） |

⇒ **一次请求产出三段，编排器按段分派给 3 个 Skill 做校验 + 转 `FillValue`/`TablePayload`。**
⇒ ⚠️ **某段整体缺失时**：该段对应的 Skill 全部记 `Pending`，**并按项回退单项重试**（`38` 号 §15.4）。

---

## 十三、接线：`InputPorts` → UI 表单自动生成

**实测 `SkillMetadata.InputPorts`（`SkillPortInfo`）字段**：`Name`/`Type`/`Required`/`DefaultValue`/`Description`/`BindMode`/`EnumSource`。

**⇒ `38` 号 §14.4「操作方法自动生成」的落地规则**：

| 来源 Skill 的 `CategoryCode` | 允许的操作 Skill | 追加的操作属性 |
|---|---|---|
| `doc_src_scalar` | **只能** `fill_cell` | 无（`fill_header` / `keep_unresolved` 可选） |
| `doc_src_table` | **只能** `fill_table` | Excel ⇒ `start_row`/`start_col`/`sheet_name`；Word ⇒ 无（用 `table_tag`） |

**⚠️ 一个原本担心、实测**不存在**的问题**：`InputPorts` **不会**列出 `[FromService]` 参数 ——
`SkillExecutor.Analyze`（`Skills/SkillExecutor.cs:130-134`）**已经**跳过 `FromServiceAttribute` 参数与
`CancellationToken`。⇒ **`Q-23` 无需任何改动**（原担心的「界面上出现『请填写 db』」**不成立**，
见 §十五 `Q-23` 的作废说明）。

---

## 十四、实施顺序与验收（对 `38` 号 §15.8 的细化）

| 阶段 | 交付 | 可独立验证 |
|---|---|---|
| **S-1** | `.doc/.xls` 归一 + 建 3 表 + **`cert_doc_fill_log`** | 570 份能产 `.docx`；4 张表建好 |
| **S0** | 修 `Skill清单-V1.md` + `OfficeFillModels.cs:94` + **本文 §2.2 的命名迁移** | 纯文档/脚本 |
| **S1** | `FillSession` + `FillValueFactory` + `TablePayloadFactory` + **§2.3 的 8 行注册 SQL** | 单元测试：`FillSession` 累积正确 |
| **S2** | `src_global_param` + `src_manual` | §4.6 的 T1~T5 + §5.4 的 T1~T3 |
| **S2.5** | `IEnterpriseDocRetriever`（两级过滤，见 `38` 号 §15.10） | 给定 1 条需求，选出 ≤5 份并打印清单 |
| **S3** | `IAiFillInvoker` + `AiFillPromptBuilder` + `src_semantic` | **拿 1 份真企业文档 + 1 段提示词，产出 1 个值** |
| **S3.5** | `src_ai_field` + `src_ai_table` + `AiFillBatchService` | **10 个 AI 项只发 1 次请求** |
| **S4** | `fill_cell` + `fill_table` | §10/§11 的测试用例全绿 |
| **S5** | 表格列锚点扫描（`{{col:X}}` → `ParentAnchorCode`） | 扫出列清单 |
| **S6** | 端到端：`src_*` → `fill_*` → 层 1 → 产物 + 写 `cert_doc_fill_log` | 8 行填进 3 行模板，`Verified=true` |
| **S7** | 试跑端点 + `37` 号 Tab1/Tab2 部分解冻 | 界面上能点 |

> **★ 实施进度（2026-10-03）**：**S1 ✅ 完成**（`FillSession` + `FillTodo` + `FillValueFactory`，
> 位置 `CertPlatform.Shared/Office/`）｜**S2 ✅ 完成**（`src_manual` + `src_global_param`，
> 位置 `CertPlatform.Admin/Services/Workflow/Skills/Fill/`）⇒ **单测 301 全绿 / 0 失败**。
> ⏸ **S-1 / S0 未做**（`.doc/.xls` 归一 + 4 张表 + 命名迁移）⇒ **`wf_skill` 注册 SQL 尚未执行**。
> ⏭ 下一步 = **S-1**（建表 + 归一）或 **S2.5**（两级过滤）—— 见 §十五 与 `38` 号 §15.10。

**★ 三条不可让步的验收标准**（来自 `38` 号 §14.8 不变量）：
1. `session.Request.Template` 非空且是 `.docx`/`.xlsx`；
2. **`report.Verified == true`**（无残留锚点、无残留标记）；
3. **`Pendings` 非空必须展示**，⛔ 不得静默留空。

---

## 十五、待裁决（本文新增 `Q-22` ~ `Q-25`；**⚠️ §十六 / §十七 另有 `Q-26` / `Q-27`**）

| ID | 决策点 | 选项 | **建议** |
|---|---|---|---|
| **Q-22** | `fill_cell`/`fill_table` 的**累积状态载体** | A **`FillSession` 对象实例**（编排器直接放 `context.Inputs`，绕开 JSON 往返）<br>B 保持 `38` 号 §14.6 的 `OfficeFillRequest?` 参数（**JSON 链上还原不了 ⇒ 走不通**）<br>C 让 `fill_*` 收 JSON 字符串自己反序列化 | **A** —— B **技术上不成立**（`ConvertValue` 只转基础类型）；C 可行但把序列化责任推给 Skill，且**丢了 `AnchorOwners` 这类内存状态** |
| ~~**Q-23**~~ | ~~`SkillExecutor.Analyze` 是否过滤 `[FromService]` 参数~~ | ✅ **已作废 —— 实测已实现，无需裁决** | **无需改动**：`SkillExecutor.Analyze`（`Skills/SkillExecutor.cs:130-134`）**已经**跳过 `FromServiceAttribute` 与 `CancellationToken` ⇒ 原担心的「UI 会出现『请填写 db』」**不成立**（2026-10-03 实读源码核实） |
| **Q-24** | `src_compute`（表达式计算） | A **一期不做**（`22` 号 `expr` 已能表达简单拼接）<br>B 一期做（新增 1 个 Skill + 1 套表达式引擎） | **A** —— 无真实需求；`38` 号 §22 已定「⛔ 停止前瞻设计」 |
| **Q-25** | `TableColumn` 是否加 **`Title`**（中文列名） | A **加**（AI 需要中文名才懂要填什么）<br>B 不加，靠 `field_code` 猜 | **A** —— ⛔ 不加的话 AI 面对 `reg_capital` 只能猜 |

---

## 十六、`{{}}` 的格式化设计（V1.1 新增）

> **用户原话（逐字）**：「唯一需要考虑的一个问题，就是 **`{{}}` 中的格式问题**，
> 比如针对**日期的，数字的**，我们在**替换的时候，需要考虑如何进行格式化**」

### 16.1 ★ 一句话结论

**格式串只有一个真相源（统一用 .NET 方言），Word 直接用、Excel 侧做一次方言转换。**
**现状：Excel 侧已完整，Word 侧是缺口。**

### 16.2 现状核实（实读源码 3 个文件）

| 能力 | 现状 | 位置 |
|---|---|---|
| 模板语法 `{{key:format}}` | ✅ **已实现** | `FillSyntax.SplitFormat`（`Shared/Fill/FillModels.cs:83`） |
| 语法规则 | 先剥 `ai:` / `@` 前缀 → **拆最后一个冒号** → `:` 后为空视为**无格式** | 同上 |
| 官方示例 | `@doc_date:yyyy年MM月dd日` ⇒ `key=@doc_date`，`format=yyyy年MM月dd日` | `FillModels.cs:77` |
| **Excel 格式应用** | ✅ **完整** | `ExcelCellWriter.ApplyNumberFormat` |
| Excel 优先级 | **值字典给的 > 原格已是格式则不动 > Date 兜底 `yyyy-mm-dd`** | 同上 |
| Excel 防炸 | `StyleCache`（`(原样式索引, 格式串)` 缓存，防 **64,000** 样式上限） | `ExcelCellWriter.cs:93` |
| **Word 格式应用** | ❌ **缺口** | `FillValue.ToDisplayText()` |

**★ 缺口逐字**（`Shared/Office/OfficeFillModels.cs`）：

```csharp
public string ToDisplayText() => Kind switch
{
    FillValueKind.Number => Number?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
    FillValueKind.Date   => Date?.ToString("yyyy-MM-dd") ?? string.Empty,   // ← 硬编码
    FillValueKind.Bool   => Bool == true ? "是" : "否",
    FillValueKind.Field  => Text ?? string.Empty,
    _ => Text ?? string.Empty,
};
```

而 `WordCellWriter.SetText(XWPFTableCell cell, string? text)` **只收 `string`** ⇒ **Word 侧全部走 `ToDisplayText()`**
⇒ **模板里写 `{{doc_date:yyyy年MM月dd日}}` 对 Word 完全无效**（恒输出 `2026-10-03`）。

### 16.3 ★★ 根本差异：Word 没有「数字格式」这个概念

| | Excel | Word |
|---|---|---|
| 值承载 | `ICell` **原生类型**（`double` / `DateTime` / `bool`） | **纯文本**（`<w:t>` 只存字符串） |
| 格式机制 | `CellStyle.DataFormat`（**渲染期**生效，值不变） | ⛔ **无** —— 只能**写前把值格式化成字符串** |
| 后果 | 值仍是数字 ⇒ **可求和 / 可排序** | 一旦写入即**不可逆**，且**失去数值语义** |
| 正确做法 | **保留原生类型 + 套 DataFormat** | **必须预格式化** |

⇒ **同一份值字典要喂两个消费者**：Excel 要「**原始值 + 格式**」，Word 要「**已格式化字符串**」。
⇒ **所以格式串必须两边都能用，且方言差异要有明确归属。**

### 16.4 ★★ 裁定：格式串统一为 **.NET 方言**（唯一真相源），Excel 侧转换

| 理由 | 说明 |
|---|---|
| **Word 只能用 .NET 方言** | `DateTime.ToString(fmt)` / `double.ToString(fmt)` ⇒ 若存 Excel 方言，**Word 用不了** |
| **模板作者写的是 .NET 直觉** | `yyyy年MM月dd日` 在 .NET 里**直接可用** |
| **Excel 侧只需一次转换** | 在 `ApplyNumberFormat` 内转换 ⇒ **单点**、可测 |
| ⛔ 反方案（存 Excel 方言） | Word 侧要写**逆转换器**，而 Excel 方言有 `;` 分段、`[h]` 超 24 小时等 **Word 无对应物** ⇒ **有损** |

### 16.5 ★ 新增组件：`ExcelFormatConverter.ToExcelFormat(string dotNetFormat)`

**★ 两侧格式符的真实差异**（这是**最容易错**的地方）：

| 语义 | .NET 方言 | Excel 方言 | ⚠️ 陷阱 |
|---|---|---|---|
| 年 | `yyyy` / `yy` | `yyyy` / `yy` | 一致 |
| **月** | **`MM`**（大写） | **`mm`**（小写） | 🔴 **.NET 大写 `M`，Excel 小写 `m`** |
| **分** | **`mm`**（小写） | **`mm`**（小写） | 🔴 **.NET 里 `mm` 是「分」！与 Excel 的 `mm`（月）同形异义** |
| 日 | `dd` / `d` | `dd` / `d` | 一致 |
| 时 | `HH`（24h）/ `hh`（12h） | `hh` / `h` | ⚠️ Excel 用 `[h]` 表示超 24 小时 |
| 秒 | `ss` | `ss` | 一致 |
| 毫秒 | `fff` | `000` | 差异 |
| 千分位 | `#,##0` / **`N2`** | `#,##0` | ⚠️ **`N2` 是 .NET 标准格式，Excel 不认** |
| 百分比 | `0.00%` / **`P2`** | `0.00%` | ⚠️ **`P2` Excel 不认** |
| 字面量 | `\` 转义 / 引号 | `"` 包裹 / `\` | 🔴 **汉字在 Excel 里可直接用；英文字母须 `"` 包裹** |

**⇒ 转换器必须处理三类**：

1. **`M` / `m` 大小写映射**（月 vs 分）—— 🔴 **最容易错**，必须按**大小写敏感**逐个字符判定
2. **.NET 标准格式（`N2` / `P2` / `D` / `F`）→ Excel 自定义格式** —— 需要一张映射表
3. **字面量包裹**（连续的非格式符字母 ⇒ `"` 包裹）

**示例对照**：

| 模板里写的 | Word（.NET 直接用） | Excel（转换后） |
|---|---|---|
| `{{d:yyyy年MM月dd日}}` | `2026年10月03日` | `yyyy"年"mm"月"dd"日"` |
| `{{n:#,##0.00}}` | `1,234.50` | `#,##0.00` |
| `{{n:N2}}` | `1,234.50` | `#,##0.00`（**映射**） |
| `{{p:P1}}` | `12.3%` | `0.0%`（**映射**） |
| `{{t:HH:mm}}` | `14:30` | `hh:mm` |

### 16.6 优先级（**三层，不得混用**）

```
① 模板内联格式   {{key:yyyy年MM月dd日}}         ← 最具体，作者意图
        ↓ 无内联格式时
② 锚点配置       cert_doc_template_anchor.ValueType / NumberFormat
        ↓ 无配置时
③ 类型兜底       Date ⇒ yyyy-MM-dd ；Number ⇒ ⛔ 不兜底（业务判断，属层 2）
```

**★ 落地要求**：**扫描器（`WordDocumentScanner` / `ExcelSheetScanner`）扫到 `{{key:format}}` 时，
把 `format` 写进 `FillValue.NumberFormat`** ⇒ ①② **自动合流**，写入器只看 `FillValue`。
（⚠️ 这正是 `ExcelCellWriter` 注释里「**值字典给的 > 模板锚点里写的**」的实现方式。）

### 16.7 需要的代码改动（**3 处，均小**）

| # | 改动 | 文件 | 说明 |
|---|---|---|---|
| **1** | **`FillValue.ToDisplayText()` 改为读 `NumberFormat`** | `Shared/Office/OfficeFillModels.cs` | Word 侧格式生效；⛔ **保留无格式时的类型兜底** |
| **2** | **新增 `ExcelFormatConverter.ToExcelFormat`** | 新建 `Shared/Office/Excel/ExcelFormatConverter.cs` | 三类转换（§16.5） |
| **3** | **`ExcelCellWriter.ApplyNumberFormat` 内调用转换器** | `Shared/Office/Excel/ExcelCellWriter.cs:66` | **单点**接入 |

**★ 兼容性**：改动 1 **会改变 Word 侧现有行为**（从恒 `yyyy-MM-dd` 变为可用模板格式）——
这是**修复**而非破坏；⚠️ 但**现有 148 个单测中若有断言 `yyyy-MM-dd` 的需同步更新**（实施前先跑一遍确认）。

### 16.8 新增裁决 `Q-26`

| ID | 决策点 | 选项 | **建议** |
|---|---|---|---|
| **Q-26** | **格式串的方言** | A **存 .NET 方言，Excel 侧转换**（单点、可测；Word 直接可用）<br>B 存 Excel 方言，Word 侧逆转换（**有损**：`;` 分段 / `[h]` 无对应）<br>C 存两套（`TextFormat` + `ExcelFormat` 两字段，配置方填两次） | **A** —— B **有损**；C 把负担推给配置方且**必然填错一边** |

---

## 十七、AI 建议表（V1.1 新增）

> **用户原话（逐字）**：「针对 **ai 建议填写的内容，需要一套独立的表**，我们现在**后端设计可以不用考虑**，
> 但**企业资料上传需要**，因为我设计中说过，针对一个单元格填写的内容，**如果有多个可选项**，
> 我们**人工复核**填写内容的时候，**可以手动确定数据源**」

### 17.1 需求拆解（4 条）

| # | 需求 | 含义 |
|---|---|---|
| 1 | **AI 建议要落库** | AI 产出的值不是「一次性的」，要能**回看、复核、重选** |
| 2 | **一个单元格可有多候选** | 同一 `AnchorCode` 可有多行（来自不同文档 / 片段）⇒ **不能是「一锚点一行」** |
| 3 | **人工复核可手动确定数据源** | ★ **数据源要可追溯到具体文档 + 位置**，人工能改选 |
| 4 | **企业资料上传阶段就要写** | 写入时机在**资料分析期**（`S2.5` / `S3`），⛔ 不是填充期 |

### 17.2 ★ 为什么必须**独立表**（不能塞进 `cert_doc_fill_log`）

| 理由 | 说明 |
|---|---|
| **基数不同** | 填充日志是「**一次填充一行**」；AI 建议是「**一个锚点 N 行候选**」⇒ 塞进去会**行爆炸** |
| **生命周期不同** | 日志是**事后审计**（append-only）；建议是**事前/事中工作流**（可改选、可重跑） |
| **写入时机不同** | 建议在**资料分析期**写；日志在**填充完成**后写 |
| **人工介入** | 建议有**人工裁决字段**；日志没有 |

### 17.3 表设计：`cert_doc_ai_suggestion`

```sql
CREATE TABLE `cert_doc_ai_suggestion` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Code` varchar(36) NOT NULL,
  `OrgCode` varchar(36) NOT NULL,
  `EnterpriseCode` varchar(36) NOT NULL,
  `StageCode` varchar(36) NOT NULL DEFAULT '',
  `StandardCode` varchar(36) NOT NULL DEFAULT '',

  `TemplateFileCode` varchar(36) NOT NULL COMMENT '目标模板（cert_standard_directory_file.Code）',
  `AnchorCode` varchar(128) NOT NULL COMMENT '目标单元格锚点',
  `FormKind` varchar(20) NOT NULL DEFAULT 'scalar' COMMENT 'scalar / table',

  `BatchCode` varchar(36) NOT NULL COMMENT '★ 一次 AI 分析的批次（整体重跑/回滚用）',
  `SuggestionIndex` int NOT NULL DEFAULT 0 COMMENT '★ 同锚点第 N 个候选（0 起）',

  `SuggestedValue` text COMMENT 'AI 建议值（表格为 JSON）',
  `ValueKind` varchar(20) NOT NULL DEFAULT 'text' COMMENT 'text/number/date/bool',
  `NumberFormat` varchar(64) DEFAULT NULL COMMENT '★ 格式串（.NET 方言，见 §十六）',
  `Confidence` decimal(3,2) DEFAULT NULL COMMENT '置信度 0~1',

  `SourceDocCode` varchar(36) DEFAULT NULL COMMENT '★ 数据源：企业文档 Code',
  `SourceLocation` varchar(256) DEFAULT NULL COMMENT '★ 数据源：文档内位置（页/段/表）',
  `SourceSnippet` text COMMENT '★ 证据原文片段（「一键看证据摘要」）',
  `Reason` varchar(1000) DEFAULT NULL COMMENT 'AI 给的理由',

  `IsSelected` tinyint(1) NOT NULL DEFAULT 0 COMMENT '★ 人工是否选定（同锚点至多一条为 1）',
  `ManualValue` text COMMENT '人工改写后的值（≠建议值）',
  `SelectedBy` varchar(64) DEFAULT NULL,
  `SelectedTime` datetime DEFAULT NULL,
  `Status` varchar(20) NOT NULL DEFAULT 'pending' COMMENT 'pending/accepted/rejected/edited',

  `SkillCode` varchar(50) DEFAULT NULL COMMENT '产出它的 Skill（src_ai_field / src_ai_table）',
  `ModelName` varchar(100) DEFAULT NULL,
  `PromptTokens` int NOT NULL DEFAULT 0,
  `CompletionTokens` int NOT NULL DEFAULT 0,
  `DurationMs` int NOT NULL DEFAULT 0,

  `CreateTime` datetime NOT NULL,
  `CreateBy` varchar(64) DEFAULT NULL,
  `UpdateTime` datetime DEFAULT NULL,
  `UpdateBy` varchar(64) DEFAULT NULL,
  `IsDeleted` tinyint(1) NOT NULL DEFAULT 0,
  `DeleteBy` varchar(64) DEFAULT NULL,
  `DeleteTime` datetime DEFAULT NULL,
  `IsValid` int NOT NULL DEFAULT 1,

  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_ai_suggestion_code` (`Code`),
  KEY `idx_ai_suggestion_target` (`EnterpriseCode`,`TemplateFileCode`,`AnchorCode`),
  KEY `idx_ai_suggestion_batch` (`BatchCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='AI 填写建议（多候选 + 人工裁决）';
```

### 17.4 ★ 三条设计要点

| # | 要点 | 说明 |
|---|---|---|
| **1** | **`SourceDocCode` + `SourceLocation` + `SourceSnippet` 三件套** | 这就是用户说的「**手动确定数据源**」的落点 —— 人工看到「候选 A 来自《营业执照》第 2 页 / 候选 B 来自《验资报告》」，据此选定 |
| **2** | **用 `BatchCode` 而非「按锚点删」** | AI 重跑时**整批替换**（`WHERE BatchCode=?` 标 `IsValid=0`）⇒ 避免「跑了一半、新旧混杂」 |
| **3** | **`IsSelected` 的「同锚点至多一条」由应用层保证** | ⛔ **数据库不加该唯一索引** —— 会导致「改选」时**先删后插的竞态**；应在应用层事务内**先清后置** |

### 17.5 写入与消费时机

```
企业资料上传 → 归一 → 转 md → 语义分析（落画像）
                                      ↓
                          ★ S2.5 两级过滤（选 2~5 份文档）
                                      ↓
                          ★ S3 AI 三件套（src_ai_field / src_ai_table）
                                      ↓
                          ★ 写 cert_doc_ai_suggestion（BatchCode = 本次批次）
                                      ↓
                        人工复核（37 号 页面）→ 选定 / 改写 / 驳回
                                      ↓
                          S4 填充：读「已选定」的建议 → 值字典
                                      ↓
                          S6 层 1 落盘一次 → 写 cert_doc_fill_log
```

**★ 与 `38` 号 §15.10 的衔接**：AI 建议是**两级过滤管线的产物落点**；
`cert_doc_fill_log.RetrievedDocCodes`（填充日志记的入选清单）与
`cert_doc_ai_suggestion.SourceDocCode`（建议记的数据源）**互为印证**。

### 17.6 ⛔ 本轮不做的部分（用户明确「后端设计可以不用考虑」）

| 不做 | 说明 |
|---|---|
| ⛔ **接口 / 服务层** | 不设计 `IAiSuggestionService` 的读写接口 |
| ⛔ **页面** | 复核界面属 `37` 号，本文不设计 |
| ✅ **只做** | **表结构** + **写入时机** + **与填充链的衔接** |

### 17.7 新增裁决 `Q-27`

| ID | 决策点 | 选项 | **建议** |
|---|---|---|---|
| **Q-27** | AI 建议的**候选上限** | A **每个锚点最多 3 个候选**（提示词里约束，超出丢弃）<br>B 不限制（AI 想给几个给几个）<br>C 每个锚点 1 个（不做多候选） | **A** —— B 会让复核界面失控；C 会**丢掉「人工确定数据源」这个能力**（用户明确要求） |

---

## 附：本文与 `38` 号 的分工

```
38 号（开发计划）                      39 号（本文 · 详细设计）
┌────────────────────────┐           ┌────────────────────────────┐
│ 做哪些 Skill（8 个）    │───约束───→│ 每个 Skill 的签名/参数/逻辑  │
│ 怎么排期（S-1~S7）      │           │ 通用契约（SkillResult 语义） │
│ 有哪些裁决（Q-1~Q-21）  │           │ 命名收敛（§二）              │
│ 架构图与调用链          │           │ 提示词契约（§十二）          │
└────────────────────────┘           └────────────────────────────┘
         ↑ 本文反向修正 2 处：M-1（FillSession）· M-2（值字典形态）
```

> **一句话**：`38` 号 说「**要做 8 个 Skill，这样排期**」，
> 本文说「**第 N 个 Skill 的函数签名长这样，参数这些，边界这些，测试这些**」。
