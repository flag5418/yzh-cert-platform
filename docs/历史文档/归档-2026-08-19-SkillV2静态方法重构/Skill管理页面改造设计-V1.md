# Skill 管理页面彻底改造设计 V1

> **版本**：V1.0 | **创建日期**：2026-08-19 | **状态**：待审批
>
> **关联文档**：
> - `自定义工作流引擎-功能设计-V1.md`（引擎设计，§4 节点体系）
> - `图形化设计器-前端组件设计方案-V2.md`（画布设计器，§5 节点目录 API）
> - `数据库表设计-V2.md`（§5 wf_skill 系列表）

---

## 一、问题诊断

### 1.1 用户反馈的 4 个问题

| # | 问题 | 根因分析 |
|---|---|---|
| **1** | "编辑的信息不正确，为什么还有提示词之类的" | 基本信息Tab混入了引擎层概念（`skillPrompt` 是给解释器组装给AI的名词解释），不属于Skill管理页面的维护范畴；`sideEffect`（功能性/逻辑性）、`outputStrict`（强/弱约束）也是引擎内部概念，管理员不需要关心 |
| **2** | "不了解输出强、弱约束是什么意思" | `output_strict` 是解释器执行时的校验策略（强约束=按Output表校验每个端口是否缺失；弱约束=ai_node放行不校验），这个概念属于引擎内部实现，不应暴露给管理页面 |
| **3** | "特殊skill为什么也在这个skill中" | 数据库 `wf_skill` 表中注册了 `ai_node`（AI 节点），这是旧设计遗留——V1.3 已将特殊节点（start/end/logic/ai/loop/docField/docTable）改为前后端硬编码，不落 `wf_skill` 表。数据库中残留的 `ai_node` 等记录应该清理 |
| **4** | "输出应该是固定的，只是对应的类型需要定义" | 当前输出Tab允许自由定义端口名、类型、解读提示词——但实际功能节点的输出端口是**代码写死的**（C# SkillBase 子类的 OutputDecls），数据库只是注册/声明，不能改变代码的输出。所以输出Tab不应该让管理员自由编辑，而应该展示C#代码声明的输出端口（只读），只允许修改端口的**类型标注**和**说明文字** |

### 1.2 深层问题：数据库注册 vs 代码声明不一致

当前存在**双重声明**的问题：

| 声明位置 | 用途 | 问题 |
|---|---|---|
| C# 代码 `OutputDecls` | 解释器执行时实际使用的端口声明 | 权威源，代码写死 |
| 数据库 `wf_skill_output` 表 | 画布设计器渲染面板 + catalog API 返回 | 应该与代码一致，但当前大部分 Skill 在数据库中没有注册输出端口 |

实际数据对照：

| Skill | C# 代码声明 OutputDecls | 数据库 wf_skill_output 注册 | 一致性 |
|---|---|---|---|
| `get_field` | fieldValue/json, confidence/number, fieldName/string, isManualEdited/boolean | fieldValue/json, confidence/number | **部分缺失**（缺 fieldName, isManualEdited） |
| `get_table` | rows/json, extractedJson/json, tableCode/string, confidence/number | （无记录） | **完全缺失** |
| `compare` | compare_result/json | compare_result/json | 一致 |
| `assemble` | assembled_text/string | （无记录） | **完全缺失** |
| `document_extract` | sections/json, tables/json, full_text/string, source_type/string, file_name/string, effective_path/string, is_converted_version/boolean | （无记录） | **完全缺失** |
| `llm_extract` | fields/json, tables/json, raw_json/string, prompt_tokens/number, completion_tokens/number | （无记录） | **完全缺失** |

输入端口同样存在此问题：

| Skill | C# 代码声明 InputDecls | 数据库 wf_skill_input 注册 | 一致性 |
|---|---|---|---|
| `get_field` | fieldCode/field_ref, enterpriseCode/string, fileCode/string | fieldCode/field_ref, enterpriseCode/text, fileCode/text | **类型不一致**（string vs text） |
| `get_table` | tableCode/string, enterpriseCode/string, fileCode/string | （无记录） | **完全缺失** |
| `compare` | valueA/json, valueB/json, operator/string, conditionLogic/string | （无记录） | **完全缺失** |
| `assemble` | segments/json | （无记录） | **完全缺失** |
| `document_extract` | storage_path/string, converted_storage_path/string, convert_status/string, convert_message/string | （无记录） | **完全缺失** |
| `llm_extract` | document_content/string, prompt/string, fields_json/json, tables_json/json | （无记录） | **完全缺失** |

---

## 二、设计原则

### 2.1 核心定位：数据库注册 = 代码声明的镜像

> **Skill 管理页面不是"创建新 Skill"的地方，而是"注册和同步已开发 Skill 元数据"的地方。**

功能节点的开发流程：
1. **开发者在 C# 代码中实现 SkillBase 子类**（声明 InputDecls/OutputDecls + 实现 ExecuteCoreAsync）
2. **在 Skill 管理页面注册该 Skill**（填写编码、名称、反射信息）
3. **管理页面从 C# 代码反射读取端口声明**，展示为只读，管理员只需确认/补充说明文字

### 2.2 页面应该展示什么 vs 不应该展示什么

| 字段 | 当前页面 | 改造后 | 理由 |
|---|---|---|---|
| Skill 编码 | 可编辑 | **编辑时只读**（新建时可输入） | 编码是唯一标识，不允许修改 |
| Skill 名称 | 可编辑 | 可编辑 | 展示名称，可调整 |
| 功能分类 | 可编辑 | 可编辑 | 面板分组，可调整 |
| **skill_type** | ~~隐藏~~ | **移除字段** | 固定 method，不需要字段 |
| **side_effect（性质）** | 可编辑 | **移除** | 引擎内部概念，C# 代码 `SideEffect` 属性已声明 |
| **output_strict（输出约束）** | 可编辑 | **移除** | 引擎内部概念，C# 代码 `OutputStrict` 属性已声明 |
| **return_type（返回类型）** | 可编辑 | **移除** | 引擎内部概念，C# 代码 `ReturnType` 属性已声明 |
| 版本 | 可编辑 | 可编辑 | 版本管理 |
| 图标 | 可编辑 | 可编辑 | 面板展示 |
| 颜色 | 可编辑 | 可编辑 | 面板展示 |
| 排序 | 可编辑 | 可编辑 | 面板排序 |
| 启用 | 可编辑 | 可编辑 | 上线/下线控制 |
| 作用说明 | 可编辑 | 可编辑 | 管理员填写 |
| **skill_prompt（AI提示词）** | 可编辑 | **移除** | 引擎内部组装概念，不属于Skill管理范畴 |
| 备注 | 可编辑 | 可编辑 | 管理员备注 |
| **输入项** | 可自由增删改 | **从 C# 代码只读同步** | 端口由代码声明，管理员只需补充"显示名"和"说明" |
| **输出项** | 可自由增删改 | **从 C# 代码只读同步** | 端口由代码声明，管理员只需补充"说明" |
| 反射信息 | 可编辑 | 可编辑（新建必填） | 反射执行入口 |

### 2.3 特殊节点不在 Skill 管理页面

数据库 `wf_skill` 表中残留的特殊节点记录（如 `ai_node`）应清理。特殊节点由前端 `specialNodes.js` 硬编码，不通过 Skill 管理页面维护。

---

## 三、改造方案

### 3.1 新增后端接口：从 C# 代码反射读取端口声明

```
GET /api/skill/{skillCode}/ports
```

**返回 C# 代码中声明的端口（权威源）**：
```json
{
  "status": true,
  "data": {
    "skillCode": "get_field",
    "inputDecls": [
      { "name": "fieldCode", "type": "field_ref", "required": true, "description": "字段编码" },
      { "name": "enterpriseCode", "type": "string", "required": true, "description": "企业编码" },
      { "name": "fileCode", "type": "string", "required": false, "description": "文件编码" }
    ],
    "outputDecls": [
      { "name": "fieldValue", "type": "json", "required": true, "description": "提取到的字段值" },
      { "name": "confidence", "type": "number", "required": true, "description": "AI提取可信度0-1" },
      { "name": "fieldName", "type": "string", "required": false, "description": "字段名称" },
      { "name": "isManualEdited", "type": "boolean", "required": false, "description": "是否人工编辑" }
    ],
    "standardOutputs": [
      { "name": "success", "type": "boolean", "description": "是否执行成功" },
      { "name": "error", "type": "string", "description": "失败时的错误信息" },
      { "name": "result", "type": "json", "description": "执行结果（业务数据）" }
    ]
  }
}
```

**实现方式**：通过 DI 容器获取已注册的 `ISkillNode` 实例，读取其 `InputDecls` / `OutputDecls` / `StandardOutputDecls` 属性。

### 3.2 前端编辑页面改造

#### 3.2.1 基本信息 Tab（精简）

**保留的字段**（管理员需要维护的）：

| 字段 | 控件 | 必填 | 说明 |
|---|---|---|---|
| Skill 编码 | input（编辑时禁用） | 是 | 唯一标识，如 `get_field` |
| Skill 名称 | input | 是 | 显示名称 |
| 功能分类 | select | 是 | 从 wf_skill_category 加载 |
| 版本 | input | 是 | 如 `1.0` |
| 图标 | input | 否 | 面板图标名 |
| 颜色 | color-picker | 否 | 面板颜色 |
| 排序 | input-number | 否 | 面板排序 |
| 启用 | switch | 是 | 上线/下线 |
| 作用说明 | textarea | 是 | 该 Skill 的作用说明 |
| 备注 | textarea | 否 | 管理员备注 |

**移除的字段**（引擎内部概念，不属于管理范畴）：

| 移除字段 | 理由 |
|---|---|
| ~~skill_type~~ | 固定 method，不需要 |
| ~~side_effect（性质）~~ | C# 代码 `SideEffect` 属性已声明 |
| ~~output_strict（输出约束）~~ | C# 代码 `OutputStrict` 属性已声明 |
| ~~return_type（返回类型）~~ | C# 代码 `ReturnType` 属性已声明 |
| ~~skill_prompt（AI提示词）~~ | 引擎组装概念，不属于Skill管理 |

#### 3.2.2 输入端口 Tab（只读 + 补充说明）

**改为只读展示从 C# 代码反射读取的输入端口声明**，管理员只能补充"显示名"和"说明"：

| 列 | 控件 | 说明 |
|---|---|---|
| 端口名 | **只读文本** | 从 C# 代码 `InputDecls` 读取 |
| 类型 | **只读标签** | 从 C# 代码 `InputDecls` 读取（string/number/date/boolean/json/field_ref/table_ref） |
| 必填 | **只读标签** | 从 C# 代码 `InputDecls` 读取 |
| 显示名 | input | 管理员补充（画布面板展示用） |
| 说明 | input | 管理员补充（画布面板提示用） |

**操作**：
- 打开编辑时，调用 `GET /api/skill/{skillCode}/ports` 获取 C# 代码声明的端口
- 与数据库 `wf_skill_input` 已有记录做匹配：
  - 代码有、数据库有 → 显示数据库中的"显示名"和"说明"
  - 代码有、数据库无 → 自动同步插入（默认显示名=端口名，说明=代码描述）
  - 代码无、数据库有 → 标记为"代码已移除"（灰显，保存时删除数据库记录）
- 不允许手动增删行

#### 3.2.3 输出端口 Tab（只读 + 补充说明）

**展示标准输出端口 + 从 C# 代码反射读取的业务输出端口**：

分为两个区域：

**区域一：标准输出端口（只读，不可编辑）**

| 端口名 | 类型 | 说明 |
|---|---|---|
| success | boolean | 是否执行成功 |
| error | string | 失败时的错误信息 |
| result | json | 执行结果（业务数据） |

**区域二：业务输出端口（只读端口名/类型 + 可编辑说明）**

| 列 | 控件 | 说明 |
|---|---|---|
| 端口名 | **只读文本** | 从 C# 代码 `OutputDecls` 读取 |
| 类型 | **只读标签** | 从 C# 代码 `OutputDecls` 读取（string/number/date/boolean/json） |
| 说明 | input | 管理员补充（画布面板提示用） |

**移除的列**：

| 移除列 | 理由 |
|---|---|
| ~~解读提示词（output_prompt）~~ | 引擎组装概念，不属于Skill管理 |

#### 3.2.4 反射信息 Tab（保持不变）

| 字段 | 控件 | 必填 | 说明 |
|---|---|---|---|
| 反射地址（classPath） | input | 是 | 类型全名，如 `YZH.Core.Skills.GetFieldSkill` |
| 反射方法（methodName） | input | 是 | 默认 `ExecuteAsync` |
| 参数绑定（paramBinding） | textarea | 否 | JSON 格式 |

### 3.3 列表页改造

**移除的列**：

| 移除列 | 理由 |
|---|---|
| ~~类型~~ | 固定 method |
| ~~性质（sideEffect）~~ | 引擎内部概念 |
| ~~输出约束（outputStrict）~~ | 引擎内部概念 |
| ~~返回类型（returnType）~~ | 引擎内部概念 |

**保留的列**：

| 列 | 说明 |
|---|---|
| 编码 | skill_code |
| 名称 | skill_name |
| 分类 | category（显示名称） |
| 版本 | version |
| 输入端口数 | 显示数量（如 3 个） |
| 输出端口数 | 显示数量（如 2 个，不含标准端口） |
| 启用 | switch |
| 操作 | 编辑/删除 |

### 3.4 数据库清理

清理 `wf_skill` 表中不属于功能节点的记录：

| skill_code | 清理操作 | 理由 |
|---|---|---|
| `ai_node` | DELETE | 特殊节点，前端硬编码 |
| `assemble_text` | DELETE | 旧名称，已被 `assemble` 替代 |
| `date_diff` | DELETE（如果未实现代码） | 未实现的 Skill |
| `text_merge` | DELETE（如果未实现代码） | 未实现的 Skill |
| `llm_judge` | DELETE（如果未实现代码） | 未实现的 Skill |
| `llm_generate` | DELETE（如果未实现代码） | 未实现的 Skill |
| `create_nc` | DELETE（如果未实现代码） | 未实现的 Skill |
| `save_result` | DELETE（如果未实现代码） | 未实现的 Skill |

保留的记录（有对应 C# 代码实现）：

| skill_code | C# 实现 |
|---|---|
| `get_field` | GetFieldSkill.cs |
| `get_table` | GetTableSkill.cs |
| `compare` | CompareSkill.cs |
| `assemble` | AssembleSkill.cs |
| `document_extract` | DocumentExtractSkill.cs |
| `llm_extract` | LlmExtractSkill.cs |

### 3.5 后端字段清理

`wf_skill` 表移除不再使用的字段：

| 字段 | 操作 | 理由 |
|---|---|---|
| `skill_type` | 保留，固定 `method` | 兼容性，不删列 |
| `side_effect` | 保留，但前端不再展示 | C# 代码已声明 |
| `output_strict` | 保留，但前端不再展示 | C# 代码已声明 |
| `return_type` | 保留，但前端不再展示 | C# 代码已声明 |
| `skill_prompt` | 保留，但前端不再展示 | 引擎组装概念 |

> **策略**：数据库列保留不删（避免破坏性变更），前端和 DTO 不再传递这些字段。C# SkillBase 子类已通过属性声明这些值，解释器直接从代码读取。

---

## 四、TODO 执行清单

### 阶段 A：后端改造

| # | 任务 | 文件 | 说明 |
|---|---|---|---|
| A1 | 新增 `GET /api/skill/{skillCode}/ports` 接口 | WfSkillController.cs + IWfSkillService.cs + WfSkillService.cs | 从 DI 容器获取 ISkillNode 实例，反射读取 InputDecls/OutputDecls/StandardOutputDecls |
| A2 | SkillDetailDto 精简 | SkillDetailDto.cs | 移除 SideEffect/OutputStrict/ReturnType/SkillPrompt（前端不再传递） |
| A3 | WfSkillService.SaveAsync 精简 | WfSkillService.cs | 保存时不再写 SideEffect/OutputStrict/ReturnType/SkillPrompt（由代码声明） |
| A4 | 编译验证 | - | 0 error |

### 阶段 B：前端改造

| # | 任务 | 文件 | 说明 |
|---|---|---|---|
| B1 | 基本信息Tab精简 | SkillManage/index.vue | 移除 sideEffect/outputStrict/returnType/skillPrompt 字段 |
| B2 | 列表页精简 | SkillManage/index.vue | 移除性质/输出约束/返回类型列，增加输入/输出端口数列 |
| B3 | 输入端口Tab改为只读+补充 | SkillManage/index.vue | 调用 /ports 接口获取代码声明，只读展示，允许补充显示名和说明 |
| B4 | 输出端口Tab改为只读+补充 | SkillManage/index.vue | 标准端口只读展示 + 业务端口只读展示+可编辑说明，移除解读提示词列 |
| B5 | 前端编译验证 | - | Vite build 0 error |

### 阶段 C：数据同步

| # | 任务 | 说明 |
|---|---|---|
| C1 | 编写数据同步 SQL | 清理 wf_skill 表中的特殊节点和未实现 Skill 记录；同步 wf_skill_input/wf_skill_output 表与 C# 代码声明一致 |
| C2 | 执行 SQL | `docker exec -i yzh-mysql mysql` 自动执行 |
| C3 | 验证 catalog 接口 | 确认所有 Skill 的输入/输出端口与代码声明一致 |

### 阶段 D：文档更新

| # | 任务 | 文件 | 说明 |
|---|---|---|---|
| D1 | 更新引擎设计文档 | 自定义工作流引擎-功能设计-V1.md | §4.3 补充"数据库注册=代码声明镜像"原则 |
| D2 | 更新数据库表设计 | 数据库表设计-V2.md | wf_skill 表字段标注"前端不再展示" |

---

## 五、改造前后对比

### 5.1 编辑弹窗对比

**改造前（4 Tab，20+ 字段）**：
```
基本信息 Tab：编码/名称/类型(已隐藏)/分类/性质/输出约束/返回类型/版本/图标/颜色/排序/启用/作用说明/AI提示词/备注
输入项 Tab：可自由增删改 7 列
输出项 Tab：可自由增删改 5 列（含解读提示词）
反射信息 Tab：3 字段
```

**改造后（3 Tab，10 字段）**：
```
基本信息 Tab：编码/名称/分类/版本/图标/颜色/排序/启用/作用说明/备注
输入端口 Tab：从代码只读同步，可补充显示名和说明
输出端口 Tab：标准端口只读 + 业务端口从代码只读同步，可补充说明
反射信息 Tab：3 字段（不变）
```

### 5.2 列表页对比

**改造前**：编码/名称/性质/输出约束/返回类型/版本/启用/操作

**改造后**：编码/名称/分类/版本/输入端口数/输出端口数/启用/操作
