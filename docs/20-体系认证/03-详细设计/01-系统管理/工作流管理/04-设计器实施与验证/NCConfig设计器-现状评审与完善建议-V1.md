# NCConfig 设计器 - 现状评审与完善建议 V1

> **日期**：2026-09-16 | **状态**：评审稿
> **评审对象**：
> - 页面：`/business/nc-config`（`cert-admin/src/pages/workflow/nc-config/designer.vue`，LogicFlow 工作流设计器）
> - 配套：同目录 `index.vue`（/business/workflow-rules 规则列表）、`report-rule-config/index.vue`（复用 nc-config 组件）
> - 后端：`CertPlatform.Admin/Controllers/Workflow/*`、`CertPlatform.Shared/Entities/Wf/*`
> - 上游文档：`01-核心引擎/工作流引擎-总体架构设计-V3.md`、`审核规则库与工作流设计器-功能设计-V4.md`、`02-NC规则配置/NC规则配置-开发计划-V3.md`、`docs/50-迁移计划/迁移代码架构审核报告-V1.md`
> **评审方法**：文档比对 + 本地 9990 端口实测（浏览器 DOM/控制台/网络取证）

---

## 一、总体结论

| 层 | 现状 | 成熟度 |
|---|---|---|
| 配置层（规则 CRUD，index.vue） | 5 字段极简方案落地，条款树选择、左树右表、复制/启停均已实现 | ★★★★☆ 基本可用，细节待打磨 |
| 设计器层（designer.vue） | 4 级树、节点库、画布拖拽连线、属性面板、拓扑校验、保存 rule_json/layout_json 均已实现 | ★★★☆☆ 功能齐但存在 P0 布局缺陷 |
| 执行层（引擎） | 前端已对接 `/api/Workflow/test/run|node|ai-node`；后端**无对应 Controller**，5 表 DDL 与实体已备但无服务实现 | ★☆☆☆☆ 最大断层 |
| 文档治理 | 权威稿体系完整，但与实现双向漂移（详见 §六） | ★★★☆☆ |

**三大断层**：① 引擎后端缺位（设计器"运行"按钮必然失败）；② 设计器 UI 在常规分辨率（≤1440px）下画布塌陷到不可用；③ 文档与实现漂移未闭环。

---

## 二、实测问题清单（9990 现场取证）

### 🔴 P0-1：中栏画布塌陷，LogicFlow 渲染失败

**现象**（视口 1067px、菜单展开时实测）：

| 测量项 | 值 |
|---|---|
| `el-main` 内容区 | 783px（837 - padding 24×2） |
| `left-panel`（固定） | 280px |
| `right-panel`（固定） | 360px |
| `main-content` / 画布 | **143px** |
| `.canvas-title` 被挤压 | 26px（"工作流：测试规则1"竖排一字一行） |
| LogicFlow SVG | **未创建**（`querySelector('svg')` 为 null） |

- 控制台警告：`渲染画布的时候无法获取画布宽高`（LogicFlow #675）。
- 工具栏 5 个按钮总宽约 380px（`.toolbar-actions` nowrap 不收缩），在 143px 容器内溢出，"保存"按钮被截断不可点。
- 根因链：`el-main` 可用宽 < 左右固定栏之和 → `main-content`（flex:1 无 min-width）被压扁 → toolbar 无 `flex-wrap`/`min-width` 保护 → 画布宽度 < LogicFlow 最小渲染阈值 → 初始化时 SVG 未挂载，ResizeObserver 只 `resize()` 不重渲染，**无自愈路径**。
- 附带：`.studio-layout` 用 `height:100vh`，但父布局已有顶栏/面包屑，整页出现纵向滚动，页脚统计栏被裁。

**建议**（组合拳）：
1. 左右栏增加折叠按钮（树/面板收起后画布回到 ≥ 700px），面板宽支持拖拽分隔条。
2. `.main-content` 设 `min-width: 480px`；`.canvas-toolbar` 允许 `flex-wrap: wrap` 或将低频按钮（布局/清空）收进"更多"下拉。
3. `initDiagram` 增加兜底：容器宽高为 0 时延迟重试；ResizeObserver 回调里首次获得有效尺寸时若画布为空且有待渲染数据则重新 `render()`。
4. `.studio-layout` 高度改 `calc(100vh - 顶栏高)` 或交给父布局 flex 填充，消除整页滚动。

### 🔴 P0-2：加载已有规则后状态立即显示"未保存"

- 实测：选中规则"测试规则1"后页脚显示 `节点: 1 | 边: 0 | 状态: 未保存`，"保存"按钮点亮。
- 根因：`selectRule → renderWorkflow → store.loadFromData()` 后未调用 `store.markClean()`（对比 `handleSave` 成功后有 markClean）。
- 风险：用户误以为有未保存改动；更重要的是 **dirty 判定靠手工标记而非数据指纹**，任何新增同步路径（如 `syncSelectedNodeInputs` 里 `store.setInputValue(nodeId, null, null)` 纯为标脏）都会造成误报/漏报。

**建议**：加载完成后 markClean；中期将 dirty 判定改为"当前画布 serialize 结果 vs 数据库 rule_json + layout_json 的指纹对比"，手工 markDirty 仅作性能优化（脏标志 + 指纹双重确认）。

### 🟡 P1-3：保存未执行强制校验（违反 V4 硬性条款）

- V4 §0.1#10："**保存必须校验**、无草稿/发布状态机"。当前 `handleSave` 直接 serialize 落库，仅在"运行测试"路径才调 `analyzeWorkflowTopology`。用户可以保存一张拓扑非法的图。
- **建议**：`handleSave` 前置调用 `validateGraph()`（静默模式），校验失败阻断保存并列出错误；校验通过再弹确认框。

### 🟡 P1-4：设计器保存写死 `SeverityIfViolated: 'minor'`

- `handleSave` 的 payload 默认 `SeverityIfViolated = 'minor'`。而 `02-NC规则配置/NC规则配置-开发计划-V3.md` 关键决策 3 明确："**严重度由工作流引擎动态决定，配置页不填写**"。设计器落库时写死会污染语义（保存的 minor 既不是用户意图也不是引擎结论）。
- **建议**：designer 保存 payload 不再传该字段（update 场景保留原值）；V3 数据模型落定后由引擎在 wf_item_result 写入严重度。

### 🟡 P1-5：校验/错误展示用 ElMessageBox.alert 承载长文本

- 拓扑校验的路径详情、错误清单全部塞进 alert（长 JSON 文本、不可复制、模态打断）。V4 设计的是调试面板形态。
- **建议**：校验结果改为画布下方/右侧可停靠面板（与 ExecutionResultPanel 同级），错误项点击后画布高亮对应节点/边（LogicFlow `setProperties` 高亮已有基础）。

### 🟡 P1-6：执行结果面板信息量不足，未对齐 V3 可观测设计

- ExecutionResultPanel 仅展示 status/耗时/NC 结果/路径 nodeIds/TaskCode/ItemCode。V3 §4.2/§6.6 要求节点级执行记录（输入输出、is_reused、token、分支决策、翻译日志）。
- 且该面板依赖的 `/api/Workflow/test/run` 后端不存在（见 §五），当前"运行"是断头路。
- **建议**：后端最小闭环落地后（§五 P0-A），面板按"路径 → 节点"两级折叠展示每个节点的输出摘要、耗时、token；数据源直接读 `wf_node_output` + `wf_execution_log`。

### 🟢 P2-7：树徽标语义错误

- 实测：机构节点 badge=1（子标准数）、标准节点 badge=9（子阶段数）。数量徽标应表达业务含义——建议改为"该节点下已配置工作流的检查项数 / 检查项总数"（如 `2/5`），阶段节点懒加载后回填。

### 🟢 P2-8：401 无统一前端处理

- 控制台 4 次 `401 (Unauthorized)`（无 token 的 GET 请求）。yzhApi 层应统一拦截：401 → 清理凭证跳登录，而非静默失败留下空树。

### 🟢 P2-9：双页面导航割裂

- 规则在 `/business/workflow-rules`（index.vue）维护，工作流在 `/business/nc-config`（designer.vue）编排。designer 不能新建/重命名规则，index 的"工作流设计器"按钮跳过去后**不携带当前选中规则**，用户要重新在树里找。
- **建议**：跳转带 query（`?ruleCode=`）自动定位展开并选中；designer 树上支持"新建检查项"（复用 index 的弹窗组件），形成闭环。

### 🟢 P2-10：可用性细节

- 无撤销/重做（LogicFlow keyboard 被禁用，删除靠自研 keydown，误删节点即丢失）。
- 删除节点无确认（直接删）、清空画布有确认——标准不一致。
- `loadRulesForPhase` PageSize 200 上限无分页提示；`loadClauseTree` 每次开弹窗重复拉取（迁移审核报告 P2-3 已提，未修）。
- 搜索框只过滤已加载节点文本，不检索检查项名（未展开的阶段搜不到）。

---

## 三、配置层（index.vue / logic.ts）完善建议

| # | 建议 | 依据 |
|---|---|---|
| 1 | 切回 `YzhTable + :columns`，删除手写 el-table 列模板；`logic.ts` 的 `tableColumns` 已定义 `ClauseNumber` 列但页面未用，且 `OnQueried` 回填的 `ClauseTitle` 未展示 | 迁移审核报告 P2-1 |
| 2 | `Operator` 写法统一：logic.ts 用 `'eq'`、designer.vue 用 `'Equal'`，后端 FilterItem 兼容两者，但同一业务字段两种写法易在框架演进时单边失效 | 项目规范 §16 同构 |
| 3 | 编辑弹窗打开 `loadClauseTree` 增加按 standardCode 缓存 | 迁移审核报告 P2-3 |
| 4 | "复制"功能与 V3 开发计划 §6.1"移除复制"矛盾——实现走了增强路线，应回写 V3 文档（或移除），消除决策歧义 | 文档先行原则 |
| 5 | 阶段下无规则时给空态引导（"去设计器创建第一个检查项"按钮），而非空白表格 | 体验 |

---

## 四、引擎层差距与落地路线（最高优先级方向）

**现状证据**：
- 前端调用：`/api/Workflow/test/run`、`/api/Workflow/test/node`、`/api/Workflow/test/ai-node`（designer.vue L1132/1178/1269、report-rule-config 同款）。
- 后端 Workflow 域仅 5 个 Controller：StandardDirectory、WfSkill、WfSkillCategory、DocExtractionRule、ValidationRule——**无任何 test/run、执行任务、解释器端点**；全库检索 `ExecutionTaskManager|IWorkflowInterpreter|WorkflowEngine` 仅命中注释。
- 已就绪资产：`wf_execution_task / wf_execution_task_item / wf_node_output / wf_node_execution_*` 实体与 `all_tables_ddl.sql` DDL 齐备；`LlmInvokeService`（OpenAI 兼容直连）可用；WfSkill/输入输出定义表已建。

**路线建议**（与 V3 §12 分阶段路线图对齐，但先补最小闭环）：

| 阶段 | 内容 | 说明 |
|---|---|---|
| **P0-A 最小闭环**（先行） | 新增 `WorkflowTestController`：`POST test/run`（同步解释执行 TEST 任务）、`test/node`、`test/ai-node` | 同步路径，直接用画布 configJson；产出 task/item/node_output 三层记录（即便先写内存/简化表），让设计器"运行/节点测试"真正可用。**不要**等队列、SignalR、断点续跑齐备才交付 |
| P0-B 校验后端化 | 前端 `analyzeWorkflowTopology` 的规则在后端复刻（环检测、孤立节点、端口类型匹配），保存接口服务端兜底校验 | 防绕过前端直调 API 落脏配置 |
| P1 队列化 | 按 V3 §7 接 yzh_queue：节点级入队、超时/重试、看门狗（30min 无事件判卡死）、幂等键 | 正式 NC_CHECK/REPORT_GENERATE 的前置 |
| P1 结果复用与断点续跑 | `wf_node_output` 唯一键 `(task_code,item_code,node_id,port)` 已设计好；AI 节点默认重跑、确定性节点复用 | V3 §4.1 |
| P2 可观测 | SignalR 节点事件推送；翻译日志 `wf_node_execution_translated` 写入；设计器执行面板读取展示 | 支撑审核员可读证据链 |
| P2 正式触发 | 企业资料上传完成 → 按 机构+标准+阶段 批量创建 NC_CHECK 任务（三层任务模型 item 并行） | V3 §1.2 |

**关键红线提醒**（V3 §5.3）：TEST 与正式共用同一执行内核，差异只在 `EnvironmentContext`（数据边界 YZH-STD-ENT / OutputTarget）。P0-A 的同步执行器应从第一天就按"未来异步内核的子集"设计，避免出现第二套执行逻辑。

---

## 五、并发与数据保护

- `cert_validation_rule` 无版本/更新时间戳比对，两个管理员同时编辑同一规则的工作流会**静默互相覆盖**（designer 保存为全量字段 update）。
- **建议**：update 接口带 `CreateTime`（或加 `RowVersion`）乐观锁，不匹配时提示"规则已被他人修改，请刷新"。

## 六、文档治理（宪法级 §一 / §5.1）

| # | 事项 |
|---|---|
| 1 | `01-核心引擎/README.md` 写明实现代码位置仅 `nc-config/`，实际还分散在 `nc-config/panels/`、`adapters/`、`cert-share/src/composables/workflow/`（serializer/topology/nodeIdGenerator/specialNodes）与 `logicflow-patch.ts`——README 应补全映射，防止后续改动找不到权威实现 |
| 2 | V4 功能设计头部过时标注（Skill 体系）已挂 2026-08-19，正文章节未逐节标注，AI/新人易误读——建议在正文各过时节首行加引用块标注 |
| 3 | `99-归档/README.md` 记录的"文档与实现严重脱节"问题需用本评审结论回写：设计器 M1-M6 实际完成度，更新归档原因表述 |
| 4 | `NC规则配置-开发计划-V3.md` 与实现差异（复制功能保留、designer 传 SeverityIfViolated）——按"文档先行"回改文档或改实现，二选一留痕 |
| 5 | 迁移审核报告行动项 #9（`50-迁移计划` 目录归位 `50-任务/`、迁移方案状态更新、PhaseCode 语义纠偏）仍待执行 |
| 6 | 建议每个权威文档增加"对应代码"章节（文件路径 + 关键类/函数名），形成文档↔代码双向索引 |

---

## 七、优先级行动清单

| # | 事项 | 级别 | 预估 | 涉及 |
|---|---|---|---|---|
| 1 | 设计器三栏响应式修复（折叠、min-width、toolbar 收纳、高度修正、initDiagram 自愈） | 🔴 P0 | 3h | designer.vue |
| 2 | 加载后 markClean + dirty 指纹化 | 🔴 P0 | 2h | designer.vue、useWorkflowStore.ts |
| 3 | `WorkflowTestController` 最小闭环（test/run、test/node、test/ai-node 同步执行） | 🔴 P0 | 2-3d | 新 Controller + Interpreter 骨架 |
| 4 | 保存前强制校验（V4 §0.1#10） | 🟡 P1 | 1h | designer.vue |
| 5 | 移除 designer 落库的 SeverityIfViolated 默认值 | 🟡 P1 | 0.5h | designer.vue、ValidationRuleController |
| 6 | 校验/执行结果停靠面板化 + 节点高亮定位 | 🟡 P1 | 4h | designer.vue、ExecutionResultPanel.vue |
| 7 | index.vue 切 YzhTable、ClauseTitle 展示、条款树缓存、operator 统一 | 🟡 P1 | 2h | nc-config/index.vue、logic.ts |
| 8 | 乐观锁（并发编辑保护） | 🟡 P1 | 2h | ValidationRuleController、designer.vue |
| 9 | 树徽标语义、空态引导、跳转携带 ruleCode | 🟢 P2 | 3h | designer.vue、index.vue |
| 10 | 撤销/重做（LogicFlow history 或自研快照栈）、删除确认统一 | 🟢 P2 | 4h | designer.vue |
| 11 | 401 统一拦截跳转 | 🟢 P2 | 1h | yzh-core api client |
| 12 | 文档治理 6 项（§六） | 🟢 P2 | 2h | docs 两级 |

---

## 八、章节变更记录

| 日期 | 版本 | 变更 |
|---|---|---|
| 2026-09-16 | V1 | 初版：9990 实测取证 + 文档比对，P0/P1/P2 分级行动清单 |
