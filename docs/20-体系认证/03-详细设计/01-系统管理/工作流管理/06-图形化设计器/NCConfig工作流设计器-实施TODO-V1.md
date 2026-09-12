# NCConfig 工作流设计器 — 实施 TODO V1.2

> 版本：V1.2 | 创建日期：2026-08-20 | 最近修订：2026-08-20
>
> 关联文档：`工作流节点定义与属性抽象-V1.md`（V1.2）、`AI提示词规则-功能设计-V1.md`、`图形化设计器-前端组件设计方案-V2.md`

---

## 已完成（V1.0-V1.1）

| # | 任务 | 状态 | 日期 |
|---|------|:---:|------|
| 1 | NodeIdGenerator（classCode_n{序号}，删除不复用） | ✅ | 2026-08-20 |
| 2 | specialNodes.js 统一元数据源（branch 取代 logic） | ✅ | 2026-08-20 |
| 3 | model/serializer.js（configJSON ⇄ 业务层 + 旧数据迁移） | ✅ | 2026-08-20 |
| 4 | store/useWorkflowStore.js（操作层封闭集合 + 编号器 + 脏标记） | ✅ | 2026-08-20 |
| 5 | NCConfig 集成 store + NodeIdGenerator | ✅ | 2026-08-20 |
| 6 | PortControl.vue（bindMode 三分法：Link/LinkOrConstant/Enum） | ✅ | 2026-08-20 |
| 7 | NodePropertyForm 重构（PortControl + branch 条件提示 + 空状态优化） | ✅ | 2026-08-20 |
| 8 | 连线即数据绑定（edge:add/edge:delete 同步 inputs + store） | ✅ | 2026-08-20 |
| 9 | autoLayout 只更新坐标不断线（两层模型） | ✅ | 2026-08-20 |
| 10 | 保存前校验（非 AI 部分） | ✅ | 2026-08-20 |

---

## 阶段 A：docField/docTable 配置期验证（V1.2）

> 核心目标：选择标准文档 + 字段/表格 → 点击测试 → 展示提取数据

| # | 任务 | 依赖 | 说明 |
|---|------|------|------|
| A1 | 后端 API：`POST /api/DocExtractionRule/test-field` | 无 | 输入 ruleCode + fieldCode + docType，返回字段值 + 置信度 + sampleData |
| A2 | 后端 API：`POST /api/DocExtractionRule/test-table` | 无 | 输入 ruleCode + tableCode + docType，返回表格行数据 + 置信度 |
| A3 | 前端 docField 面板：文档类型选择（standard/enterprise）+ 文档下拉 + 字段下拉 + 测试按钮 | A1 | 选择后调用 test-field API，展示测试结果 |
| A4 | 前端 docTable 面板：文档类型选择 + 文档下拉 + 表格下拉 + 测试按钮 | A2 | 选择后调用 test-table API，展示测试结果 |
| A5 | 测试结果展示面板：字段值/表格数据 + 置信度 + 原始数据折叠查看 | A3,A4 | 统一的测试结果展示组件 |
| A6 | docField/docTable config 结构更新：增加 docType 字段 | 无 | 与节点定义 V1.2 对齐 |
| A7 | 特殊节点元数据更新：docField/docTable 的 panelSchema 增加 docType 选择 | A6 | specialNodes.js 更新 |

---

## 阶段 B：AI 节点提示词编辑器（V1.2）

> 核心目标：可用功能/可用数据选择器 + 结构化提示词编辑 + 引用插入 + 编译

| # | 任务 | 依赖 | 说明 |
|---|------|------|------|
| B1 | AiPromptPanel.vue 组件：三段式布局（系统指令只读 + 数据上下文只读 + 用户指令可编辑） | 无 | 基础编辑器框架 |
| B2 | 可用功能选择器：列出 skill/docField/docTable/branch 节点（排除 start/end/ai_node） | 无 | 点击后插入 `{{节点别名}}` 到编辑器 |
| B3 | 可用数据选择器：列出所有节点的输出端口（排除 start/end） | 无 | 点击后插入 `{{节点别名.portName}}` 到编辑器 |
| B4 | 编译器：`{{别名}}` → `{{nodeId.result}}`，`{{别名.port}}` → `{{nodeId.port}}` | B2,B3 | 保存时编译，加载时反编译 |
| B5 | 提示词校验（5 条）：语法闭合 / 别名存在 / 端口存在且 visible / 引用无环 / 长度限制 | B4 | 保存前校验 |
| B6 | AI 输出标准包装：success/error/result（与 SkillExecutor 一致） | 无 | 引擎执行后自动包装 |
| B7 | NodePropertyForm 集成 AiPromptPanel：ai_node 类型显示编辑器而非通用 textarea | B1 | 替换当前的通用 textarea |

---

## 阶段 C：引擎核心补全

> 核心目标：stopAt 单步调试 + OutputConfig 解析 + TRACE 生成

| # | 任务 | 依赖 | 说明 |
|---|------|------|------|
| C1 | WorkflowEngine 增加 `options` 参数（stopAt: nodeId） | 无 | 执行到指定节点暂停，返回中间状态 |
| C2 | OutputConfig 解析：end 节点按引用独立解析，未执行分支取 default | 无 | WorkflowRunResult 增加 OutputConfig 字段 |
| C3 | TRACE 生成：每节点执行后追加 TRACE 序列 | 无 | WorkflowRunResult 增加 Trace 字段 |
| C4 | WorkflowValidator 九项校验（非 AI 部分） | 无 | 保存前调用，返回校验结果 |

---

## 阶段 D：端到端验证

| # | 任务 | 依赖 | 说明 |
|---|------|------|------|
| D1 | 保存→重开→数据一致验证 | A,B,C | 选择节点 → 配置 → 保存 → 重新打开 → 数据完整 |
| D2 | 旧数据迁移验证 | 1 | n${Date.now()} 格式 ID 自动迁移为 classCode_n{序号} |
| D3 | docField 配置期完整流程验证 | A | 选标准文档 → 选字段 → 测试 → 看到数据 |
| D4 | AI 提示词编辑完整流程验证 | B | 选功能/数据 → 插入引用 → 编辑提示词 → 保存 → 重开 → 引用完整 |

---

## 变更记录

| 日期 | 版本 | 变更内容 |
|------|------|----------|
| 2026-08-20 | V1.2 | 新增阶段 A（docField/docTable 配置期验证）、阶段 B（AI 提示词编辑器）；更新节点定义文档 V1.2 |
| 2026-08-20 | V1.1 | 初稿：完成 M1 基础设施 + M2 属性面板 + M3 布局校验 |
