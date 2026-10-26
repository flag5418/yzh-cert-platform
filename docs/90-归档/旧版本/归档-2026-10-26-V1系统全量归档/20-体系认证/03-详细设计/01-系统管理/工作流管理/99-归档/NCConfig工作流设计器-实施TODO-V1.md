# NCConfig 工作流设计器 — 实施 TODO 清单 V1

> **版本**：V1.1 | **日期**：2026-08-19 | **状态**：实施中
>
> **定位**：NCConfig 页面工作流设计器的分步实施计划，按"数据先行→交互递进→稳定收口"闭环组织。
>
> **依赖文档**：
> - `自定义工作流引擎-功能设计-V1.md`（V1.4，引擎权威设计）
> - `图形化设计器-前端组件设计方案-V2.md`（V2.1，前端组件设计）
> - `LogicFlow工作流设计器实施分析与建议-V1.md`（Phase E/F/G 实施指南）
> - `Skill清单-V1.md`（V1.1，Skill 体系权威约定）

---

## 一、开发顺序合理性分析

```
Phase 1: 后端数据基础          → 功能性 Skill 接口返回完整信息 + 文档选择支持
Phase 2: 前端节点库布局        → 特殊节点 + 分类功能性节点，可拖拽
Phase 3: 节点属性动态渲染      → 点击不同节点，属性面板按类型动态变化
Phase 4: 连线机制              → 端口级连线 + 输入绑定
Phase 5: 未连线/连线测试       → 单节点独立测试 + 流程测试
Phase 6: 校验与保存            → 保存前校验 + 后端持久化
```

**合理性结论**：每一步都依赖前一步的产出，无跳跃。Phase 1-2 建立数据基础和视觉骨架，Phase 3-4 建立交互逻辑，Phase 5-6 建立质量保障。这是最小可行闭环。

---

## 二、Phase 1：后端功能性 Skill 接口完善

### 目标
后端返回的 Skill 详情包含完整的输入/输出端口信息，含绑定模式和字典来源。

### TODO

| # | 任务 | 状态 | 说明 |
|---|------|:---:|------|
| 1.1 | `GET /api/skill/list-active` 返回含 inputs（含 bindMode/enumSource）和 outputs | [x] | `GetActiveSkillsAsync` → `BuildDetailDto` 已含 inputs/outputs；`ReplaceChildrenAsync` 已同步 bindMode/enumSource |
| 1.2 | `GET /api/skill/{skillCode}` 返回完整详情（inputs/outputs/reflection） | [x] | `GetDetailAsync` → `BuildDetailDto` 已实现 |
| 1.3 | `POST /api/skill/analyze` 反射分析返回含 bindMode/enumSource | [x] | `SkillExecutor.Analyze` → `SkillPortInfo` 已含 |
| 1.4 | 功能节点目录接口 `GET /api/skill/catalog` 返回含 inputPorts（含 bindMode/enumSource） | [x] | `GetCatalogAsync` 已输出 bindMode/enumSource |
| 1.5 | 特殊节点（start/end/logic/ai_node/loop/docField/docTable）端口声明 | [x] | SkillPanel.vue 特殊节点已补充完整 inputPorts/outputPorts |
| 1.6 | 重建 compare/assemble/document_extract Skill 的 input 记录 | [x] | 数据库已重建，含正确的 bind_mode 和 enum_source |
| 1.7 | `wf_skill_input` 表新增 `bind_mode`、`enum_source` 字段 | [x] | WfSkillInput 实体 + SQL 迁移脚本已执行 |
| 1.8 | `GET /api/DocExtractionRule/configured-rules` 返回含 fileName | [x] | Service 关联 cert_standard_directory_file 获取文件名 |

---

## 三、Phase 2：前端节点库布局

### 目标
SkillPanel 按"特殊节点 + 分类功能性节点"分组，支持拖拽到画布。

### TODO

| # | 任务 | 状态 | 说明 |
|---|------|:---:|------|
| 2.1 | SkillPanel 分组：控制流节点 + AI/循环 + 文档操作 + 功能性节点 | [x] | 4 组分类 |
| 2.2 | 功能性节点从 `api/skill/list-active` 加载，按 category 分组显示 | [x] | 已实现 |
| 2.3 | 节点项显示：名称 + 编码 + 分类色标 | [x] | skill-item 已有 name/code/dot |
| 2.4 | 拖拽到画布：dragstart 携带 nodeData JSON，drop 解析并添加节点 | [x] | onDragStart + onCanvasDrop 已实现 |
| 2.5 | 节点库增加搜索过滤功能 | [x] | searchText + filteredCategories 已实现 |
| 2.6 | 节点项 hover 显示 tooltip：输入端口数 + 输出端口数 + 描述 | [ ] | 增强 UX |

---

## 四、Phase 3：节点属性动态渲染

### 目标
点击不同类型的节点，右侧属性面板按节点类型动态变化。

### TODO

| # | 任务 | 状态 | 说明 |
|---|------|:---:|------|
| 3.1 | NodePropertyForm 按 nodeType 分发：start/end/skill/logic/ai_node/docField/docTable | [x] | 已完整实现 8 种节点类型 |
| 3.2 | 功能性节点输入表单：根据 `bind_mode` 渲染不同控件 | [x] | Enum=下拉选择，Link=连线提示，LinkOrConstant=文本输入 |
| 3.3 | Enum 模式参数：compare_operator 等内置选项 | [x] | getEnumOptions 已实现 |
| 3.4 | 输出端口动态渲染（已修复硬编码 json 类型） | [x] | outputList 从 outputPorts 声明渲染 |
| 3.5 | start 节点面板：显示运行时注入参数说明 | [x] | alert + outputPorts 列表 |
| 3.6 | end 节点面板：输出结论 JSON 编辑 | [x] | endResultJson textarea |
| 3.7 | logic 节点面板：conditions 结构化编辑器 | [x] | valueA/operator/valueB + and/or |
| 3.8 | ai_node 节点面板：提示词编辑器 | [x] | aiPrompt textarea |
| 3.9 | docField/docTable 节点：文档→字段/表格级联选择 | [x] | 文档下拉 + 字段/表格下拉 |
| 3.10 | 移除"静态配置"JSON textarea | [x] | 已移除 |
| 3.11 | 文档选择：进入页面自动加载已配置提取规则的文档列表 | [x] | loadDocRules + docRules prop |
| 3.12 | 文档选择：选中文档后自动加载字段/表格定义 | [x] | onDocRuleChange + loadFieldsAndTables |
| 3.13 | 文档选择下拉框显示文件名（非编码） | [x] | r.fileName \|\| r.standardFileCode |
| 3.14 | docField/docTable 节点选择文档时通知父组件加载字段/表格 | [x] | emit('load-doc-fields') + onNodeDocChange |
| 3.15 | 点击已有 docField/docTable 节点时，自动加载其文档的字段/表格 | [x] | watch selectedNode 中检测 docCode 并触发 load-doc-fields |

---

## 五、Phase 4：连线机制

### 目标
端口级连线，连线自动绑定输入参数，断线自动回退。

### TODO

| # | 任务 | 状态 | 说明 |
|---|------|:---:|------|
| 4.1 | 端口级连线：LogicFlow sourceHandle/targetHandle 映射 | [ ] | 当前有基础连线，需做端口级 |
| 4.2 | 连线约束：禁止自连、禁止重复边、maxIn=1（end 例外） | [ ] | 操作层校验 |
| 4.3 | 连线自动绑定：连线成功后自动写入目标节点 inputs[targetHandle] | [ ] | connect 操作 |
| 4.4 | 断线自动回退：disconnect 后 inputs 端口清空 | [ ] | disconnect 操作 |
| 4.5 | 类型兼容校验：上游 outputType 与下游 inputType 匹配 | [ ] | 连线时实时校验 |
| 4.6 | logic 双锚点：success/failure 两个出边锚点 | [ ] | 需自定义 logic 节点渲染 |
| 4.7 | 端口连线视觉：不同端口类型用不同颜色/形状标识 | [ ] | UX 增强 |

---

## 六、Phase 5：未连线/连线测试

### 目标
未连线节点可手动输入参数独立测试；已连线节点可从 start 逐个执行到当前节点。

### TODO

| # | 任务 | 状态 | 说明 |
|---|------|:---:|------|
| 5.1 | 后端单节点测试 API | [ ] | 传入 skillCode + inputs，执行并返回结果 |
| 5.2 | 后端流程测试 API | [ ] | 依赖引擎解释器 |
| 5.3 | 前端"测试此节点"按钮 | [ ] | 属性面板底部 |
| 5.4 | 测试结果面板 | [ ] | 结果回显 |
| 5.5 | 画布标注：已执行节点标绿/失败标红 | [ ] | 执行状态可视化 |
| 5.6 | overrides 注入：测试时可手动覆盖某节点输出 | [ ] | 调参重跑 |

---

## 七、Phase 6：校验与保存

### 目标
保存前校验工作流结构完整性，后端持久化 workflow_config JSON。

### TODO

| # | 任务 | 状态 | 说明 |
|---|------|:---:|------|
| 6.1 | 前端保存前校验：start/end 存在、logic 双分支完整、引用可解析 | [ ] | validateGraph 增强 |
| 6.2 | 后端保存 API 确认 rule_json 字段接收 workflow_config | [~] | 已有保存接口，需确认 JSON 结构对齐 |
| 6.3 | 后端保存校验：九项校验 | [ ] | WorkflowEngine 校验逻辑 |
| 6.4 | 布局保存：layout_json 独立存储/恢复 | [~] | 已有 layoutJson 保存，需确认恢复 |
| 6.5 | 保存后重开一致性：加载 → 编辑 → 保存 → 重开 → 数据一致 | [ ] | 端到端验收 |
| 6.6 | 脏标记：修改后标记脏，未保存离开提示 | [ ] | UX 保护 |

---

## 八、已修复的 Bug

| # | Bug | 修复 |
|---|-----|------|
| B1 | Skill 编辑弹窗输出端口表格硬编码 `result` 类型为 `json` | 改为 `getStandardOutputs(analyzed?.returnType)` 动态获取 |
| B2 | 编辑已有 Skill 重新打开时端口信息消失（`analyzed` 为 null） | `openEdit` 加载详情后自动从 DB 镜像重建 `analyzed` |
| B3 | 保存时 `body.inputs` 写死空数组 | 改为 `editForm.inputs` |
| B4 | `wf_skill_input` 表缺少 `bind_mode` / `enum_source` 字段 | 新增字段 + SQL 迁移脚本 + 后端同步 |
| B5 | `WfSkillService.ReplaceChildrenAsync` 未同步 bindMode/enumSource | 从反射结果 port.BindMode/port.EnumSource 同步 |
| B6 | `configured-rules` 路由被 `{standardFileCode}` catch-all 拦截 | Controller 中具体路由放在参数路由之前 |
| B7 | NodePropertyForm 输入参数不显示（从 inputs 对象构建空列表） | 改为从 inputPorts 端口声明渲染 |
| B8 | NodePropertyForm docField/docTable 选择文档后不加载字段/表格 | emit('load-doc-fields') + NCConfig onNodeDocChange |
| B9 | 文档选择下拉框显示编码而非文件名 | 使用 r.fileName \|\| r.standardFileCode |
| B10 | NodePropertyForm docField/docTable 重选文档时字段/表格未清空 | onDocFieldChange/onDocTableChange 重置 fieldCode/tableCode |

---

## 九、文件变更清单

### 后端

| 文件 | 变更 |
|------|------|
| `VOL.Entity/CertPlatform/Wf/WfSkillInput.cs` | 新增 bind_mode、enum_source 字段，移除 enum_values |
| `VOL.Builder/Services/CertPlatform/WfSkillService.cs` | ReplaceChildrenAsync 同步 bindMode/enumSource；GetCatalogAsync 输出 bindMode/enumSource |
| `VOL.Entity/CertPlatform/Wf/SkillDetailDto.cs` | Inputs/Outputs 列表增加 bindMode/enumSource |
| `DB/mysql/skill_v15_bind_mode.sql` | 新建：ALTER TABLE 添加新字段 + 更新 compare 参数 |
| `VOL.WebApi/Controllers/CertPlatform/DocExtractionRuleController.cs` | 新增 configured-rules 和 {ruleCode}/fields-tables 路由（放在 {standardFileCode} 之前） |
| `VOL.Builder/IServices/CertPlatform/IDocExtractionRuleService.cs` | 新增 GetConfiguredRulesAsync、GetFieldsAndTablesAsync 接口 |
| `VOL.Builder/Services/CertPlatform/DocExtractionRuleService.cs` | 实现配置规则列表（关联 StandardDirectoryFile 获取 fileName）和字段/表格查询 |

### 前端

| 文件 | 变更 |
|------|------|
| `SkillManage/index.vue` | 修复 openEdit analyzed 重建、save inputs 传递、输出端口类型动态化 |
| `SkillPanel.vue` | 特殊节点（start/end/logic/ai_node/loop/docField/docTable）补充完整 inputPorts/outputPorts |
| `NodePropertyForm.vue` | 完全重写：8 种节点类型分发、bindMode 控件渲染、文档级联选择、load-doc-fields 事件 |
| `NCConfig/index.vue` | defaultNodeData 支持所有节点类型、文档选择栏、loadFieldsAndTables、onNodeDocChange |
| `compiler.js` | 编译/反编译保留 inputPorts/outputPorts、新节点类型配色 |

---

## 十、下一阶段行动

- **Phase 4（连线机制）** 是下一个核心任务，需要：
  1. LogicFlow 自定义节点注册（带端口锚点）
  2. 连线约束逻辑
  3. 连线自动绑定 / 断线回退
  4. 类型兼容校验

> 每完成一个 Phase 后做一次"保存→重开→数据一致"的快速验证，不等到最后才验收。

---

> **下一步行动**：按 Phase 1→6 顺序逐项实施，每完成一个 Phase 验收后再进入下一个。当前 Phase 1-3 已完成，进入 Phase 4 连线机制。
