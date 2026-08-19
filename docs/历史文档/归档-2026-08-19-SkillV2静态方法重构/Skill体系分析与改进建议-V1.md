# Skill 体系分析与改进建议 V1

> **版本**：V1.0 | **状态**：分析报告（待审批） | **创建日期**：2026-08-18
>
> **定位**：本稿基于对当前 Skill 管理系统的代码和文档的全面审查，**不受之前设计文档的约束**，从用户明确的六个补充意见出发，进行彻底的分析和改进建议。
>
> **用户补充意见回顾**：
> 1. 功能性节点抽象的原则：不调整整体引擎，新增 Skill 不改引擎
> 2. `config_schema` 是干什么的（需解释）
> 3. 特殊节点可以硬编码（前后端固定约定）
> 4. 先优化 Skill 配置页面，检查所有功能性 Skill 是否做了严格抽象
> 5. 讨论流程应完善到文档中，不急于改代码
> 6. 完善文档后进行彻底分析，大刀阔斧改进，待审批后实施

---

## 一、现状全貌

### 1.1 已有代码资产清单

| 层 | 文件/位置 | 当前状态 | 问题 |
|---|---|---|---|
| **后端实体** | `Skill.cs`（wf_skill 主表） | 6 张表（主表 + input + output + reflection + api + category） | 保留了 3 个旧列（input_schema/output_schema/endpoint_config）；skill_type 有 method/api 两种但用户明确只有 method |
| **后端 DTO** | `SkillDetailDto.cs` | 聚合主表 + 子表 | 结构正确，但包含 Api 子表（用户明确不需要 api 型） |
| **后端 Service** | `WfSkillService.cs` | CRUD + 主子表事务 | 功能完整，但保存逻辑包含 Api 子表处理 |
| **后端 Controller** | `WfSkillController.cs` | RESTful API | 功能完整 |
| **后端 Skill 实现** | `YZH.Core/Skills/` | 6 个已实现：get_field/get_table/compare/assemble/document_extract/llm_extract | document_extract 和 llm_extract 未继承 SkillBase，无声明式端口声明 |
| **后端基类** | `SkillBase.cs` | 声明式元数据 + ExecuteAsync 模板方法 | 设计优秀：InputDecls/OutputDecls 声明 + 必填校验 + 输出契约校验 |
| **前端管理页** | `SkillManage/index.vue` | 5 Tab 编辑弹窗 | 包含 API Tab（应删除）；前端列表列有"类型"列（method/api）应简化 |
| **前端设计器** | `WorkflowDesigner.vue` + `compiler.js` | 基础框架 | 仅为骨架，未实现 V2 设计文档中的面板/目录驱动等能力 |
| **设计文档** | V2 前端组件设计方案 | 详尽但过度设计 | `configSchema` 等概念超前实现，脱离当前阶段 |

### 1.2 已实现的 6 个 Skill 抽象检查

| Skill | 继承 SkillBase? | InputDecls | OutputDecls | 严格抽象? | 问题 |
|---|---|---|---|---|---|
| **get_field** | 是 | field_code(string,必填) + enterprise_code(string,必填) + file_code(string,可选) | field_value(json,必填) + field_name(string) + confidence(number,必填) + is_manual_edited(boolean) | **合格** | 无 |
| **get_table** | 是 | table_code(string,必填) + enterprise_code(string,必填) + file_code(string,可选) + table_index(number,可选) | rows(json,必填) + extracted_json(json,必填) + table_code(string,必填) + confidence(number,必填) | **合格** | 无 |
| **compare** | 是 | value(json) + operator(string) + threshold(json) + date_a(date) + date_b(date) + unit(string) | result(json,必填) | **基本合格** | 输入端口过多且全可选，语义不明确；三种模式（数值/日期/非空）混在一个 Skill 里 |
| **assemble** | 是 | parts(json,必填) + joiner(string,可选) | assembled_text(string,必填) | **合格** | 无 |
| **document_extract** | **否**（直接实现 ISkillNode） | 无声明 | 无声明 | **不合格** | 输入输出全靠代码硬编码，无声明式元数据，画布无法自动生成表单 |
| **llm_extract** | **否**（直接实现 ISkillNode） | 无声明 | 无声明 | **不合格** | 同上 |

### 1.3 核心问题诊断

| # | 问题 | 严重度 | 说明 |
|---|---|---|---|
| 1 | **skill_type 有 method/api 两种** | 中 | 用户明确：所有 Skill 都是 method 型（反射执行），api 型不需要——第三方 API 调用封装在方法内部 |
| 2 | **document_extract/llm_extract 未继承 SkillBase** | 高 | 无声明式端口声明，画布无法生成输入表单，无法做输出契约校验——破坏了"新增 Skill 不改引擎"原则 |
| 3 | **config_schema 概念混淆** | 中 | V2 设计文档中的 `configSchema` 是"前端属性面板渲染定义"，但当前 Skill 管理页面完全没有这个字段——它属于"节点目录"概念而非 Skill 本身 |
| 4 | **前端管理页包含 API Tab** | 低 | 用户明确不需要 api 型，应删除 API Tab 和相关逻辑 |
| 5 | **旧列残留** | 低 | input_schema/output_schema/endpoint_config 三个旧列标注"保留兼容"但已无使用 |
| 6 | **特殊节点 vs 功能节点未在代码中体现** | 高 | V2 设计文档定义了 7 种特殊节点（start/end/logic/ai/loop/docField/docTable），但当前代码完全没有特殊节点的任何实现 |
| 7 | **WfSkillService 和 SkillService 并存** | 中 | 有两个 Service 类做类似事情，职责不清 |

---

## 二、config_schema 解释

### 2.1 它是什么

`configSchema` 出现在 V2 前端组件设计文档 §5.5 的"class 目录元数据 schema"中：

```json
{
  "classCode": "get_field",
  "configSchema": [
    { "key": "docCode", "label": "文档", "type": "docFieldPicker", "required": true }
  ]
}
```

**它的用途**：定义功能性节点在画布上的**属性面板**应该渲染哪些配置控件。例如 `get_field` 节点需要配置"选哪个文档的哪个字段"，`configSchema` 就是告诉前端"渲染一个文档字段级联选择器"。

### 2.2 为什么当前不需要它

| 原因 | 说明 |
|---|---|
| **概念层级错误** | `configSchema` 属于"节点目录"（V2 §5.5 的 class catalog）而非 Skill 本身。Skill 管理页面管理的是"能力注册表"，不是"画布节点面板定义" |
| **当前阶段不需要** | 当前阶段 Skill 的输入参数已经由 `wf_skill_input` 表定义（input_name/input_type/input_label 等），这就是输入表单模板。`configSchema` 是额外再定义一层面板渲染，过度设计 |
| **通用功能节点不需要定制面板** | 用户明确：功能性节点的输入参数是固定的，用通用表单渲染即可。只有特殊节点（docField/docTable/ai/loop）才需要定制面板，而特殊节点是前端硬编码的 |

### 2.3 结论

`configSchema` 概念应从 Skill 管理中移除。它属于未来"节点目录 API"的范畴，在画布设计器阶段再定义。当前 Skill 管理只需 `wf_skill_input` 表即可满足"画布生成输入表单"的需求。

---

## 三、用户六个补充意见的逐条分析与改进建议

### 意见 1：功能性节点抽象原则

**用户原话**：功能性节点抽象的原则就是不调整整体引擎，新增了 Skill 由于输入和输出都有强约定，这样就确保 Skill 可以不断完善但不用每次增加一个 Skill 就修改引擎。

**分析**：当前设计基本符合这个原则——
- `SkillBase` 基类提供 `InputDecls`/`OutputDecls` 声明式元数据
- `ExecuteAsync` 模板方法统一处理必填校验 + 输出契约校验
- 新增 Skill 只需继承 `SkillBase` + 声明端口 + 实现 `ExecuteCoreAsync`
- 引擎通过 `ISkillNode` 接口调用，不关心具体实现

**但存在破坏原则的情况**：
- `document_extract` 和 `llm_extract` 没有继承 `SkillBase`，无声明式端口——引擎无法自动校验其输入输出，画布无法自动生成表单
- 这两个 Skill 是早期硬编码实现，绕过了抽象体系

**改进建议**：

| # | 改进项 | 优先级 |
|---|---|---|
| 1.1 | `document_extract` 改为继承 `SkillBase`，声明 InputDecls/OutputDecls | P0 |
| 1.2 | `llm_extract` 改为继承 `SkillBase`，声明 InputDecls/OutputDecls | P0 |
| 1.3 | 所有未来 Skill 必须继承 `SkillBase`，禁止直接实现 `ISkillNode` | 原则 |
| 1.4 | `SkillBase` 增加 `Version` 属性声明，与 `wf_skill.version` 互相校验 | P1 |

### 意见 2：config_schema 是干什么的

**分析**：已在 §二 中详细解释。`configSchema` 是 V2 设计文档中"节点目录"的概念，用于定义画布属性面板的渲染控件。它不属于 Skill 管理范畴，当前阶段不需要。

**改进建议**：

| # | 改进项 | 优先级 |
|---|---|---|
| 2.1 | Skill 管理页面不增加 `configSchema` 字段 | 原则 |
| 2.2 | V2 设计文档中 `configSchema` 标注为"画布设计器阶段实现" | P1 |
| 2.3 | `wf_skill_input` 表的 `input_type` 字段已覆盖表单渲染需求 | 已有 |

### 意见 3：特殊节点可以硬编码

**用户原话**：肯定可以硬编码，这些是前后端的固定约定，前端传使用了特殊节点，后端肯定明白应该如何去实现。

**分析**：这与 V2 设计文档 §4.3 "class 目录驱动"中"后端生成完整节点分类"的设计有冲突。V2 文档主张"前端零硬编码"，但用户明确特殊节点可以硬编码。

**两种节点的分界线**：

| 节点类别 | 定义方式 | 前端处理 | 后端处理 |
|---|---|---|---|
| **特殊节点**（start/end/logic/ai/loop/docField/docTable） | 前后端固定约定，硬编码 | 前端硬编码面板/渲染/交互 | 后端硬编码执行逻辑 |
| **功能节点**（get_field/get_table/compare/assemble/...） | `wf_skill` 表注册，可扩展 | 前端通用表单渲染（由 `wf_skill_input` 驱动） | 后端反射执行（`wf_skill_reflection` 驱动） |

**改进建议**：

| # | 改进项 | 优先级 |
|---|---|---|
| 3.1 | 明确"特殊节点"清单：start/end/logic/ai/loop/docField/docTable 共 7 种 | P0 |
| 3.2 | 特殊节点的前端面板、后端执行全部硬编码，不落 `wf_skill` 表 | P0 |
| 3.3 | 功能节点完全由 `wf_skill` 表注册驱动，前端通用渲染，后端反射执行 | P0 |
| 3.4 | 前端节点面板 = 特殊节点（硬编码面板）+ 功能节点（通用表单）| P0 |
| 3.5 | V2 文档中"后端生成完整节点分类"修改为"后端只返回功能节点目录，特殊节点前端硬编码" | P1 |

### 意见 4：先优化 Skill 配置页面，检查严格抽象

**分析**：当前 Skill 管理页面（`SkillManage/index.vue`）存在以下问题：

| # | 问题 | 改进 |
|---|---|---|
| 4.1 | 有 API Tab | 删除 API Tab 和相关逻辑 |
| 4.2 | 列表有"类型"列（method/api） | 删除，因为只有 method 型 |
| 4.3 | 编辑弹窗有 `skillType` 选择器 | 删除，固定为 method |
| 4.4 | `emptyApi()` 和 `editForm.api` 残留 | 清除 |
| 4.5 | 输入项 Tab 缺少"端口名 camelCase"提示 | 增加提示 |
| 4.6 | 输出项 Tab 缺少"标准输出端口"提示 | 增加：success(boolean) + error(string) + result(json) |
| 4.7 | 反射信息 Tab 的"参数绑定"不够直观 | 增加说明：可留空，默认按 InputDecls 顺序绑定 |

**严格抽象检查结果**：

| Skill | 状态 | 行动项 |
|---|---|---|
| get_field | 合格 | 无 |
| get_table | 合格 | 无 |
| compare | 基本合格 | 输入端口过多且全可选，建议拆分为 `compare_number` + `compare_date` + `not_empty` 三个 Skill（P2，不紧急） |
| assemble | 合格 | 无 |
| document_extract | **不合格** | 改为继承 SkillBase，声明端口 |
| llm_extract | **不合格** | 改为继承 SkillBase，声明端口 |

**改进建议**：

| # | 改进项 | 优先级 |
|---|---|---|
| 4.1 | 删除前端 API Tab、skillType 选择器、emptyApi 等 | P0 |
| 4.2 | document_extract 改为继承 SkillBase | P0 |
| 4.3 | llm_extract 改为继承 SkillBase | P0 |
| 4.4 | SkillBase 增加"标准输出端口"约定 | P0（见 §四） |

### 意见 5：讨论流程完善到文档中

**分析**：用户明确要求"先完善文档，不急于改代码"。本稿即为这一原则的执行——所有分析和改进建议写入文档，待审批后再实施。

**改进建议**：

| # | 改进项 | 优先级 |
|---|---|---|
| 5.1 | 本文档作为 Skill 体系改进的审批文档 | 进行中 |
| 5.2 | 审批通过后，同步更新 `自定义工作流引擎-功能设计-V1.md` | P1 |
| 5.3 | 审批通过后，同步更新 `图形化设计器-前端组件设计方案-V2.md` | P1 |
| 5.4 | 审批通过后，同步更新数据库表设计文档 | P1 |

### 意见 6：大刀阔斧改进建议

以下是大刀阔斧的改进建议，不受之前文档约束：

---

## 四、大刀阔斧改进建议

### 4.1 简化 Skill 类型：只有 method

**当前**：`skill_type` 有 method/api 两种。
**改为**：只有 method。第三方 API 调用封装在方法内部。

| 改动项 | 具体内容 |
|---|---|
| `wf_skill` 表 | `skill_type` 列保留但固定值 `method`；新增 Skill 默认 method |
| `wf_skill_api` 表 | 保留表结构但不再使用（不删除，避免破坏性变更） |
| `WfSkillApi` 实体 | 保留但不维护 |
| `SkillDetailDto` | 移除 `Api` 属性 |
| `WfSkillService` | `ReplaceChildrenAsync` 中移除 Api 子表处理 |
| 前端 `SkillManage` | 删除 API Tab、skillType 选择器、emptyApi |
| `WfSkillController` | 无需改动（已支持） |

### 4.2 标准化输出端口约定

**当前问题**：每个 Skill 的输出端口名不一致（get_field 输出 field_value，compare 输出 result，assemble 输出 assembled_text），下游节点引用时需要知道每个 Skill 的具体输出名。

**用户原话**：输出格式也是固定的，包括输出的内容组织格式也是固定的，比如先返回是否成功，是否有错误，返回的结果等。

**改进建议**：所有功能性 Skill 的输出统一为标准结构：

```
标准输出端口（所有 Skill 必须包含）：
├── success     (boolean, 必填)  — 是否执行成功
├── error       (string, 可选)    — 失败时的错误信息
├── result      (json, 必填)      — 执行结果（业务数据）
├── confidence  (number, 可选)    — 置信度（AI 相关 Skill）
└── [自定义端口] (按 Skill 需要补充，如 field_name/rows 等)
```

**SkillBase 改造**：

```csharp
public abstract class SkillBase : ISkillNode
{
    // ... 已有声明 ...

    /// <summary>标准输出端口（所有 Skill 自动包含）</summary>
    public sealed IReadOnlyList<SkillParam> StandardOutputDecls => new[]
    {
        new SkillParam { Name = "success", Type = "boolean", Required = true, Description = "是否执行成功" },
        new SkillParam { Name = "error", Type = "string", Required = false, Description = "失败时的错误信息" },
        new SkillParam { Name = "result", Type = "json", Required = true, Description = "执行结果" }
    };

    /// <summary>最终输出声明 = 标准端口 + 自定义端口</summary>
    public IReadOnlyList<SkillParam> AllOutputDecls =>
        StandardOutputDecls.Concat(OutputDecls).ToList();

    /// <summary>子类只需声明业务自定义输出端口</summary>
    public virtual IReadOnlyList<SkillParam> OutputDecls { get; } = Array.Empty<SkillParam>();

    protected async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct)
    {
        // ... 已有逻辑 ...
        // 执行后自动包装标准输出
        if (result.Success)
        {
            var wrappedOutputs = new Dictionary<string, object>
            {
                ["success"] = true,
                ["error"] = string.Empty,
                ["result"] = result.Outputs  // 原始输出包装到 result 里
            };
            // 补充自定义输出端口
            foreach (var kv in result.Outputs)
                wrappedOutputs[kv.Key] = kv.Value;
            return SkillResult.Ok(wrappedOutputs, result.Confidence);
        }
        else
        {
            return SkillResult.Ok(new Dictionary<string, object>
            {
                ["success"] = false,
                ["error"] = result.Error ?? string.Empty,
                ["result"] = new Dictionary<string, object>()
            });
        }
    }
}
```

**好处**：
- 下游节点统一引用 `nX.success` 判断是否成功，`nX.result` 取结果
- 新增 Skill 不需要考虑标准输出，只关注业务输出
- 引擎执行逻辑统一：先检查 `success`，再取 `result`

**已有 Skill 改造**：

| Skill | 当前输出 | 改造后 |
|---|---|---|
| get_field | field_value/field_name/confidence/is_manual_edited | success/error/result(含 field_value/field_name/confidence/is_manual_edited) |
| get_table | rows/extracted_json/table_code/confidence | success/error/result(含 rows/extracted_json/table_code/confidence) |
| compare | result(boolean) | success/error/result(含比较结果) |
| assemble | assembled_text | success/error/result(含 assembled_text) |
| document_extract | sections/tables/full_text/... | success/error/result(含 sections/tables/full_text/...) |
| llm_extract | fields/tables/raw_json/... | success/error/result(含 fields/tables/raw_json/...) |

### 4.3 统一节点目录 API

**当前**：前端画布需要知道有哪些节点可以拖入，但目前没有"节点目录"API。

**改进**：新增 `GET /api/skill/catalog` 接口，返回**功能性节点目录 + 特殊节点清单**。

**功能性节点目录（后端返回）**：

```json
[
  {
    "skillCode": "get_field",
    "skillName": "获取字段值",
    "category": "data_access",
    "description": "按 field_code 查询提取结果",
    "icon": "Document",
    "color": "#409EFF",
    "inputs": [
      { "name": "field_code", "label": "字段编码", "type": "string", "required": true },
      { "name": "enterprise_code", "label": "企业编码", "type": "string", "required": true },
      { "name": "file_code", "label": "文件编码", "type": "string", "required": false }
    ],
    "outputs": [
      { "name": "success", "label": "是否成功", "type": "boolean" },
      { "name": "error", "label": "错误信息", "type": "string" },
      { "name": "result", "label": "执行结果", "type": "json" }
    ],
    "testable": true
  }
]
```

**特殊节点清单（前端硬编码）**：

```js
const SPECIAL_NODES = [
  { classCode: 'start', className: '开始', category: 'control', singleton: true, maxOut: 1, testable: false },
  { classCode: 'end', className: '结束', category: 'control', maxIn: -1, testable: false },
  { classCode: 'logic', className: '逻辑判断', category: 'control', maxOut: 2, testable: true,
    outputPorts: [{ name: 'success', anchor: 'right-top' }, { name: 'failure', anchor: 'right-bottom' }] },
  { classCode: 'ai', className: 'AI 节点', category: 'ai', testable: true },
  { classCode: 'loop', className: '循环节点', category: 'ai', testable: true },
  { classCode: 'docField', className: '自定义字段', category: 'special', testable: true },
  { classCode: 'docTable', className: '自定义表格', category: 'special', testable: true }
]
```

**前端画布面板** = 特殊节点（硬编码） + 功能节点（API 返回）。

### 4.4 清理冗余代码和表结构

| # | 清理项 | 方式 |
|---|---|---|
| 1 | `wf_skill` 表 `input_schema`/`output_schema`/`endpoint_config` 旧列 | ALTER DROP（有迁移脚本时执行） |
| 2 | `wf_skill_api` 表 | 保留不删（避免破坏性变更），前端和 Service 不再使用 |
| 3 | `SkillDetailDto.Api` 属性 | 移除 |
| 4 | `WfSkillService.ReplaceChildrenAsync` 中 Api 处理块 | 移除 |
| 5 | 前端 `SkillManage` 中 API Tab | 移除 |
| 6 | 前端 `SkillManage` 中 `skillType` 选择器 | 移除（固定 method） |
| 7 | `SkillService.cs`（与 `WfSkillService` 重复） | 标注 `[Obsolete]`，后续清理 |

### 4.5 Skill 管理页面优化

**当前 5 Tab → 改为 4 Tab**：

| Tab | 内容 | 变更 |
|---|---|---|
| 基本信息 | skillCode/skillName/category/sideEffect/outputStrict/returnType/version/icon/color/sortOrder/isActive/description/skillPrompt/remark | 删除 skillType 选择器（固定 method） |
| 输入项 | wf_skill_input 列表 | 增加提示：端口名使用 camelCase；标准端口(success/error/result)无需在此声明 |
| 输出项 | wf_skill_output 列表 | 增加提示：标准端口(success/error/result)自动包含，此处声明业务自定义输出端口 |
| 反射信息 | classPath/methodName/paramBinding | 增加说明：classPath = 命名空间.类名（如 YZH.Core.Skills.GetFieldSkill）；methodName 默认 ExecuteAsync；paramBinding 可留空（默认按 InputDecls 顺序绑定） |

**列表页变更**：
- 删除"类型"列
- "性质"列保留（功能性/逻辑性）
- "输出约束"列保留

### 4.6 特殊节点执行约定

特殊节点的后端执行逻辑硬编码在引擎中，不落 `wf_skill` 表：

| 特殊节点 | 后端执行方式 | 前端面板 |
|---|---|---|
| **start** | 引擎注入上下文（enterpriseCode/standardCode/phaseCode/fileCode/orgCode）到 start 输出端口 | 只读面板，显示工作流输入参数 |
| **end** | 引擎收集所有 end 节点的 config.result 作为工作流输出 | 输出结论配置面板（result 结构编辑） |
| **logic** | 引擎按 conditions[] 比较 valueA/valueB，走 success/failure 分支 | conditions 结构化编辑器（8 操作符 + and/or） |
| **ai** | 引擎组装提示词 → 调 LLM → 返回 content/json/confidence | 提示词编辑器 + 插入引用 |
| **loop** | 引擎将 collection 整包 + 循环提示词 → LLM 一次调用 → 输出 results 数组 | 提示词编辑器 + 聚合配置 |
| **docField** | 引擎按 config.docCode + config.fieldCode 查 B-08 提取结果 | 文档→字段级联选择器 |
| **docTable** | 引擎按 config.docCode + config.tableCode + config.columns 查 B-09 提取结果 | 文档→表格→勾列选择器 |

**关键约定**：
- 特殊节点的 `classCode` 是固定编码，前端硬编码，后端硬编码
- 特殊节点的端口是固定声明，不落表
- 特殊节点的 config 结构是固定约定，前端面板硬编码渲染

### 4.7 引擎执行流程（单节点 → 连线 → 完整流程）

**用户原话**：核心逻辑都一样，都是先执行单个节点的置入传入的参数，然后运行功能性方法得到结果，再将结果作为下一步节点的参数，以此类推。

**统一执行流程**：

```
1. 解析节点 config + inputs
   → config: 节点静态配置（如 docField 的 docCode/fieldCode）
   → inputs: 端口引用值（如 enterprise_code = ctx.enterpriseCode 或 nX.result.field_value）

2. 解析 inputs 引用
   → 字面量: 直接使用
   → ctx.xxx: 从工作流上下文取值
   → nX.port: 从上游节点输出取值（先执行上游节点）

3. 实例化 Skill
   → DI 容器查找（内置 6 个 Skill 已注册）
   → 反射加载（wf_skill_reflection.class_path → ReflectionSkillLoader）

4. 执行 Skill
   → SkillBase.ExecuteAsync(context)
   → 自动：必填校验 → ExecuteCoreAsync → 输出契约校验
   → 返回标准结构：{ success, error, result, ...自定义 }

5. 传递结果给下游
   → 下游节点 inputs 引用 nX.result.xxx 或 nX.success 等
   → 引擎按依赖图（边 ∪ 引用）拓扑排序，逐节点执行
```

**单节点测试**：
```
POST /api/workflow/run-node
{
  "nodeId": "get_field_n1",
  "manualInputs": { "field_code": "HR_STAFF_COUNT", "enterprise_code": "YZH-STD-ENT" }
}
→ 只执行该节点，manualInputs 覆盖输入解析
→ 返回 { status, inputs, outputs: { success, error, result } }
```

**连线测试**：
```
POST /api/workflow/run-flow
{
  "config": { ...workflow_config },
  "stopAt": "get_field_n2"   // 执行到该节点停止
}
→ 从 start 按连线逐个执行到 stopAt
→ 返回每个节点的真实输入/输出
```

**完整流程测试**：
```
POST /api/workflow/run
{
  "config": { ...workflow_config },
  "context": { "enterpriseCode": "YZH-STD-ENT", ... }
}
→ 从 start 到 end 完整执行
→ 返回所有节点轨迹 + end 结论
```

---

## 五、风险与注意事项

| # | 风险 | 应对 |
|---|---|---|
| 1 | 标准输出端口改造影响已有工作流 JSON | 当前无生产数据，无兼容风险；改造后旧 JSON 需迁移 |
| 2 | document_extract/llm_extract 改为继承 SkillBase 可能影响引擎调用 | 引擎通过 ISkillNode 接口调用，SkillBase 实现了该接口，无影响 |
| 3 | 删除 API Tab 后 wf_skill_api 表数据 | 不删除表和数据，仅停止维护 |
| 4 | 特殊节点硬编码与 V2 设计文档"零硬编码"冲突 | V2 文档相应章节同步更新 |

---

## 六、TODO 执行清单

> 以下清单按执行顺序排列，每项标注涉及文件、改动内容、验收标准。
> 审批后逐项执行，每项完成后打勾。

### 第一阶段：文档同步（审批后首先执行）

- [ ] **T1.1** 更新 `自定义工作流引擎-功能设计-V1.md`
  - 文件：`docs/80-功能设计/01-系统管理/工作流管理/自定义工作流引擎-功能设计-V1.md`
  - 改动：
    - §4.1 节点分类：明确"特殊节点硬编码，功能节点表注册"取代"前端零硬编码"
    - §4.2 六种节点定义：同步标准输出端口约定（success/error/result）
    - §5.1 涉及表：标注 `wf_skill_api` 不再使用（保留不删）
    - 新增 §4.X 标准输出端口约定章节
  - 验收：文档内无"api 型 Skill"作为执行类型的描述；标准输出端口约定有独立章节

- [ ] **T1.2** 更新 `图形化设计器-前端组件设计方案-V2.md`
  - 文件：`docs/80-功能设计/01-系统管理/工作流管理/06-图形化设计器/图形化设计器-前端组件设计方案-V2.md`
  - 改动：
    - §2.2 第 4 条"后端生成完整节点分类"修改为"后端返回功能节点目录，特殊节点前端硬编码"
    - §4.3 class 目录驱动：标注"特殊节点前端硬编码，功能节点后端目录驱动"
    - §4.4 面板注册表：`configSchema` 标注为"画布设计器阶段实现，Skill 管理不涉及"
    - §5.5 configSchema 字段：标注"属于节点目录概念，非 Skill 管理范畴"
    - §6.1 class 目录 API：修改响应结构，只返回功能节点，特殊节点不在此 API
  - 验收：文档中"零硬编码"表述全部修改；configSchema 标注为画布阶段

- [ ] **T1.3** 更新数据库表设计文档
  - 文件：`docs/20-架构决策/数据库表设计-V2.md`
  - 改动：
    - F-01 wf_skill：标注 `skill_type` 固定值 `method`；标注 `input_schema`/`output_schema`/`endpoint_config` 为"待清理旧列"
    - F-07 wf_skill_api：标注"不再使用，保留不删"
  - 验收：表设计文档与代码实际状态一致

### 第二阶段：后端 Skill 抽象修复

- [ ] **T2.1** `SkillBase` 增加标准输出端口封装
  - 文件：`src/server/YZH-Framework/YZH.Core/Workflow/SkillBase.cs`
  - 改动：
    - 新增 `StandardOutputDecls`（只读）：success(boolean) + error(string) + result(json)
    - `AllOutputDecls` = 标准端口 + 子类 `OutputDecls`
    - `ExecuteAsync` 模板方法改造：执行成功时自动包装 `{ success: true, error: "", result: { ...原始输出 } }`；失败时包装 `{ success: false, error: "错误信息", result: {} }`
    - 子类 `OutputDecls` 改为"业务自定义输出端口"（不含标准端口）
    - 输出契约校验改为校验 `AllOutputDecls`
  - 验收：单元测试——任意 SkillBase 子类执行后输出 Dictionary 中必含 success/error/result 三个键

- [ ] **T2.2** `document_extract` 改为继承 `SkillBase`
  - 文件：`src/server/YZH-Framework/YZH.Core/Skills/DocumentExtractSkill.cs`
  - 改动：
    - `class DocumentExtractSkill : SkillBase`（替代 `ISkillNode`）
    - 声明 `SkillCode`/`SkillName`/`Category`/`SideEffect`/`ReturnType`
    - 声明 `InputDecls`：storage_path(string,必填) + converted_storage_path(string,可选) + convert_status(string,可选) + convert_message(string,可选)
    - 声明 `OutputDecls`（业务自定义）：sections(json) + tables(json) + full_text(string) + source_type(string) + file_name(string) + effective_path(string) + is_converted_version(boolean)
    - 将 `ExecuteAsync` 逻辑移入 `ExecuteCoreAsync`，返回 `SkillResult.Ok(outputs, confidence)`
    - 删除原 `ISkillNode.ExecuteAsync` 显式实现（由 SkillBase 提供）
  - 验收：编译通过；`SkillBase.ExecuteAsync` 能自动校验其输入输出

- [ ] **T2.3** `llm_extract` 改为继承 `SkillBase`
  - 文件：`src/server/YZH-Framework/YZH.Core/Skills/LlmExtractSkill.cs`
  - 改动：
    - `class LlmExtractSkill : SkillBase`（替代 `ISkillNode`）
    - 声明 `SkillCode`/`SkillName`/`Category`/`SideEffect`/`ReturnType`
    - 声明 `InputDecls`：document_content(string,必填) + prompt(string,必填) + fields_json(json,可选) + tables_json(json,可选)
    - 声明 `OutputDecls`（业务自定义）：fields(json) + tables(json) + raw_json(string) + prompt_tokens(number) + completion_tokens(number)
    - 将 `ExecuteAsync` 逻辑移入 `ExecuteCoreAsync`
  - 验收：编译通过；声明式端口与实际代码使用的输入输出一致

- [ ] **T2.4** 适配 `get_field` 标准输出
  - 文件：`src/server/YZH-Framework/YZH.Core/Skills/GetFieldSkill.cs`
  - 改动：
    - `OutputDecls` 改为业务自定义端口：field_value(json) + field_name(string) + confidence(number) + is_manual_edited(boolean)
    - `ExecuteCoreAsync` 返回 `SkillResult.Ok(outputs)`——标准端口由 SkillBase 自动包装
  - 验收：执行后输出包含 success/error/result + field_value/field_name/confidence/is_manual_edited

- [ ] **T2.5** 适配 `get_table` 标准输出
  - 文件：`src/server/YZH-Framework/YZH.Core/Skills/GetTableSkill.cs`
  - 改动：
    - `OutputDecls` 改为业务自定义端口：rows(json) + extracted_json(json) + table_code(string) + confidence(number)
    - `ExecuteCoreAsync` 返回不变，标准端口由 SkillBase 自动包装
  - 验收：执行后输出包含 success/error/result + rows/extracted_json/table_code/confidence

- [ ] **T2.6** 适配 `compare` 标准输出
  - 文件：`src/server/YZH-Framework/YZH.Core/Skills/CompareSkill.cs`
  - 改动：
    - `OutputDecls` 改为业务自定义端口：result(json)
    - `ExecuteCoreAsync` 返回不变
  - 验收：执行后输出包含 success/error/result（标准） + result（比较结果）

- [ ] **T2.7** 适配 `assemble` 标准输出
  - 文件：`src/server/YZH-Framework/YZH.Core/Skills/AssembleSkill.cs`
  - 改动：
    - `OutputDecls` 改为业务自定义端口：assembled_text(string)
    - `ExecuteCoreAsync` 返回不变
  - 验收：执行后输出包含 success/error/result + assembled_text

- [ ] **T2.8** 编译验证 + 后端重启
  - 命令：`cd src/server/Vue.NetCore && dotnet build && scripts/backend/restart.sh`
  - 验收：编译 0 error 0 warning；后端正常启动

### 第三阶段：后端 DTO / Service 清理

- [ ] **T3.1** `SkillDetailDto` 移除 Api 属性
  - 文件：`src/server/Vue.NetCore/vol.api/VOL.Entity/CertPlatform/Wf/SkillDetailDto.cs`
  - 改动：删除 `public WfSkillApi Api { get; set; }` 属性
  - 验收：编译通过（需同步修改 WfSkillService 中引用）

- [ ] **T3.2** `WfSkillService` 清理 Api 处理
  - 文件：`src/server/Vue.NetCore/vol.api/VOL.Builder/Services/CertPlatform/WfSkillService.cs`
  - 改动：
    - `BuildDetailAsync`：删除 `var api = ...` 和 `Api = api` 赋值
    - `ReplaceChildrenAsync`：删除 `db.Set<WfSkillApi>()...ExecuteDelete()` 和 `if (dto.Api != null ...)` 块
    - `DeleteAsync`：删除 `db.Set<WfSkillApi>()...ExecuteDelete()` 行
  - 验收：编译通过；Skill 保存/删除不再涉及 wf_skill_api 表

- [ ] **T3.3** `SkillService.cs` 标注 Obsolete
  - 文件：`src/server/Vue.NetCore/vol.api/VOL.Builder/Services/CertPlatform/SkillService.cs`
  - 改动：类上加 `[Obsolete("请使用 WfSkillService，此类保留仅为向后兼容")]`
  - 验收：编译通过；有 Obsolete 警告

- [ ] **T3.4** 编译验证 + 后端重启
  - 验收：编译通过；后端正常启动

### 第四阶段：前端 Skill 管理页面优化

- [ ] **T4.1** 删除 API Tab
  - 文件：`src/server/Vue.NetCore/vol.web/src/views/cert/Standard/SkillManage/index.vue`
  - 改动：
    - 删除 `<el-tab-pane label="API 信息" name="api">` 整块（约 30 行）
    - 删除 `emptyApi()` 函数
    - 删除 `editForm` 中的 `api: emptyApi()` 属性
    - 删除 `resetForm()` 中的 `api: emptyApi()` 赋值
    - 删除 `openEdit()` 中的 `api: d.api ? { ...d.api } : emptyApi()` 赋值
    - 删除 `handleSave()` 中的 `api: editForm.api` 属性
  - 验收：编辑弹窗只有 4 个 Tab（基本信息/输入项/输出项/反射信息）

- [ ] **T4.2** 删除 skillType 选择器和"类型"列
  - 文件：同上
  - 改动：
    - 删除基本信息 Tab 中"类型"表单项（`<el-form-item label="类型">` 整块）
    - 删除列表表格中"类型"列（`<el-table-column label="类型">` 整块）
    - `editForm` 中 `skillType` 固定为 `'method'`（在 resetForm 和初始化中）
  - 验收：编辑弹窗基本信息无"类型"选择器；列表无"类型"列

- [ ] **T4.3** 输入/输出 Tab 增加标准端口提示
  - 文件：同上
  - 改动：
    - 输入项 Tab：`subtable-tip` 修改为"输入表单模板（画布生成输入表单用）。标准端口 success/error/result 由引擎自动处理，无需在此声明。端口名使用 camelCase。"
    - 输出项 Tab：`subtable-tip` 修改为"业务自定义输出端口（标准端口 success/error/result 自动包含，此处只声明业务输出）。强约束 Skill 的输出在执行时强校验。"
  - 验收：两个 Tab 有清晰提示

- [ ] **T4.4** 前端编译验证
  - 命令：`cd src/server/Vue.NetCore/vol.web && npx vite build --mode development`
  - 验收：编译通过无报错

### 第五阶段：数据库旧列清理

- [ ] **T5.1** 编写旧列清理 SQL 脚本
  - 文件：`src/server/Vue.NetCore/DB/mysql/phase11_skill_cleanup.sql`
  - 内容：
    ```sql
    -- 清理 wf_skill 表旧列（已由 wf_skill_input/wf_skill_output/wf_skill_reflection 子表替代）
    ALTER TABLE wf_skill DROP COLUMN IF EXISTS input_schema;
    ALTER TABLE wf_skill DROP COLUMN IF EXISTS output_schema;
    ALTER TABLE wf_skill DROP COLUMN IF EXISTS endpoint_config;
    -- wf_skill_api 表保留不删（避免破坏性变更）
    ```
  - 验收：SQL 语法正确；执行后旧列已删除

- [ ] **T5.2** 同步 `Skill.cs` 实体移除旧列映射
  - 文件：`src/server/Vue.NetCore/vol.api/VOL.Entity/CertPlatform/Wf/Skill.cs`
  - 改动：删除 `InputSchema`/`OutputSchema`/`EndpointConfig` 三个属性及其 `[Column]` 特性
  - 验收：编译通过

- [ ] **T5.3** 执行 SQL + 编译验证
  - 验收：SQL 执行成功；后端编译通过并正常启动

### 第六阶段：节点目录 API（为画布设计器准备）

- [ ] **T6.1** 新增 `GET /api/skill/catalog` 接口
  - 文件：
    - `WfSkillController.cs`：新增 `[HttpGet("catalog")]` Action
    - `WfSkillService.cs`：新增 `GetCatalogAsync()` 方法
  - 响应结构：
    ```json
    [{
      "skillCode": "get_field",
      "skillName": "获取字段值",
      "category": "data_access",
      "description": "按 field_code 查询提取结果",
      "icon": "Document", "color": "#409EFF",
      "inputs": [{ "name": "field_code", "label": "字段编码", "type": "string", "required": true }],
      "outputs": [{ "name": "success", "label": "是否成功", "type": "boolean" },
                  { "name": "error", "label": "错误信息", "type": "string" },
                  { "name": "result", "label": "执行结果", "type": "json" },
                  { "name": "field_value", "label": "字段值", "type": "json" }],
      "testable": true
    }]
  - 数据来源：`wf_skill` 主表 + `wf_skill_input` + `wf_skill_output`，只返回 `is_active=1` 的
  - 验收：API 返回所有启用的功能性 Skill 目录

- [ ] **T6.2** 前端硬编码特殊节点清单
  - 文件：`src/server/Vue.NetCore/vol.web/src/components/workflow-designer/constants.js`（新建）
  - 内容：7 种特殊节点定义（classCode/className/category/icon/color/singleton/maxIn/maxOut/testable/inputPorts/outputPorts）
  - 验收：常量文件可被画布组件引入

---

## 七、审批请求

请对以下决策项逐条审批，审批通过后按 T1.1 ~ T6.2 顺序执行：

| # | 决策项 | 选项 |
|---|---|---|
| A | Skill 类型简化为只有 method | 同意 / 不同意 / 讨论 |
| B | 标准输出端口约定（success/error/result） | 同意 / 不同意 / 讨论 |
| C | configSchema 不加入 Skill 管理 | 同意 / 不同意 / 讨论 |
| D | 特殊节点硬编码（7 种），功能节点后端目录驱动 | 同意 / 不同意 / 讨论 |
| E | document_extract/llm_extract 改继承 SkillBase | 同意 / 不同意 / 讨论 |
| F | 前端 SkillManage 删除 API Tab + skillType 选择器 | 同意 / 不同意 / 讨论 |
| G | 旧列清理（input_schema/output_schema/endpoint_config） | 同意 / 不同意 / 推后 |
| H | SkillService.cs 标注 Obsolete | 同意 / 不同意 / 推后 |
| I | V2 设计文档"零硬编码"改为"特殊节点硬编码 + 功能节点目录驱动" | 同意 / 不同意 / 讨论 |

---

> **执行规则**：审批通过后，逐项执行 TODO，每项完成后编译验证，确认无误再进入下一项。每阶段完成后做一次整体编译+重启验证。

---

## 八、V1.4 改造实施记录（2026-08-19）

### 已完成改造项

| 阶段 | 项 | 说明 |
|---|---|---|
| A1 | 新增 `GET /api/skill/{skillCode}/ports` | 从 C# 代码反射读取端口声明（权威源），前端编辑页面调用此接口展示只读端口 |
| A2 | `SkillDetailDto` 精简 | 移除 `SideEffect`/`OutputStrict`/`ReturnType`/`SkillPrompt`/`SkillType`（这些由 C# 代码声明，不需管理员维护） |
| A3 | `WfSkillService.SaveAsync` 精简 | 不再写入引擎内部字段；`SkillType` 固定为 `method`；移除 API Tab 相关逻辑 |
| A4 | 后端编译通过 | 0 错误 |
| B1-B4 | 前端 SkillManage 页面全面改造 | 3 Tab 结构：基本信息（精简）+ 输入端口（只读+补充显示名）+ 输出端口（标准只读+业务只读+补充说明）+ 反射信息 |
| B5 | 前端 Vite 编译通过 | 0 错误 |
| C1 | 数据同步 SQL | 确保 compare 节点有 compare_result 输出端口；所有功能节点有标准输出端口（success/error/result） |
| C2 | SQL 执行成功 | 修复 utf8mb4 collation 冲突 |
| C3 | 后端已重启 | 服务就绪 http://localhost:9992 |

### 核心设计变更

1. **数据库注册 = C# 代码声明镜像**：数据库中的 `wf_skill_input`/`wf_skill_output` 是 C# 代码中 `InputDecls`/`OutputDecls` 的镜像，管理员在页面上只补充"显示名"和"说明"，不再修改端口的 `name`/`type`/`required`。
2. **引擎内部字段不再暴露给管理员**：`SideEffect`/`OutputStrict`/`ReturnType` 由 C# 代码中 `SkillBase` 子类的 `virtual` 属性声明，不需要也不应该在数据库中维护。
3. **标准输出端口约定**：所有功能节点自动包含 `success`(boolean) / `error`(string) / `result`(json)，由 `SkillBase.ExecuteAsync` 自动包装。
4. **特殊节点与功能节点分离**：特殊节点（start/end/logic/ai/loop/docField/docTable）由前端 `specialNodes.js` 硬编码；功能节点由后端 `GET /api/skill/catalog` 返回。

