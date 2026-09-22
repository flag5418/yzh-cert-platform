# 前端原子组件与 CRUD 内核重构 TODO 清单 V1

> **分支**: `refactor/frontend-atomic-core`（重构前快照已提交推送：`e95a920`）
> **创建时间**: 2026-09-20
> **性质**: 重构施工清单（可逐项勾选销项）
> **适用规范**:
> - `docs/10-YZH架构/单页面基类架构设计规范-V1.md`（后端约定）
> - `docs/10-YZH架构/TreeTable基类架构设计规范-V1.md`（左树右表内核）
> - `docs/10-YZH架构/前端原子组件与逻辑内核分层架构设计规范-V1.md`（分层与零依赖）
> - `docs/50-任务/前端TreeTable基类增强与Composable封装实施计划-V2.md`（约定矩阵/改造清单）

---

## 〇、命名决策

### 0.1 结论：`SingleTableCore` / `TreeTableCore` 比现状更合理 ✅

| 对比项 | 现状 `CrudPageLogic` / `TreeTableLogic` | 提案 `SingleTableCore` / `TreeTableCore` |
|--------|------------------------------------------|------------------------------------------|
| 对称性 | ✗ 一个讲"CRUD/Page"，一个讲"TreeTable"，不成对 | ✅ 同一命名轴（形态 + Core），并列清晰 |
| 语义 | ✗ `CrudPageLogic` 未表达"单表"，与 TreeTable 不是同一维度 | ✅ 明确区分"单表"与"左树右表" |
| 可扩展 | ✗ 新增关联型无处安放 | ✅ 可并列 `AssociationTreeCore` / `CheckTreeCore` / `LinkTableCore` |
| 心智 | 尚可 | ✅ `Core` 明确表达"框架内核"；业务类 `XxxLogic extends XxxCore` |

**采纳**，并做三点微调：

1. **类名与文件名 PascalCase**：类 `SingleTableCore` / `TreeTableCore`；文件 `SingleTableCore.ts` / `TreeTableCore.ts`（不写 `singletablecore`）。
2. **三层命名定式**：框架内核用 `*Core`，业务页面逻辑用 `*Logic`，组合式用 `use*`。即 `CertStageLogic extends SingleTableCore`。
3. **过渡期保留别名**：`CrudPageLogic` / `TreeTableLogic` 保留为 `@deprecated` 重导出（指向新类），避免一次性大爆炸；全部迁移完成后再删除。

### 0.2 建议的最终命名族

| 层 | 命名 | 说明 |
|----|------|------|
| 单表内核 | `SingleTableCore` | 单表 CRUD |
| 左树右表内核 | `TreeTableCore extends SingleTableCore` | 树 + 表 CRUD |
| 关联型父类 | `AssociationTreeCore` | 左树 + 右侧关联态（无弹窗） |
| 类型A | `CheckTreeCore` | 选中-授权（checkTree/check/*） |
| 类型B | `LinkTableCore` | 勾选-关联（list/save） |
| 树能力混入 | `withTreeSide(Base)` / `TreeSideMixin` | 共享树能力 |

---

## 一、阶段总览与依赖

```
P0 约定与守卫 ──▶ AD 适配层 ──▶ ST SingleTableCore ──▶ TT TreeTableCore ──▶ AS 关联内核
   │                                    │                      │
   └────────────▶ C 原子组件 ◀──────────┴──────────────────────┘
                     │
                     ▼
                    CP Composables ──▶ PG 页面修复 ──▶ BE 后端配套 ──▶ DC 文档/验收
```

- **C（原子组件）与 ST/TT 可并行**，但 CP 依赖二者。
- **PG 页面修复** 依赖 TT + CP。
- **BE** 与前端并行。

---

## 二、P0 约定与守卫（前置）

- [ ] **P0-1** 全量修正 TreeNode 小写读取（`node.code/name/extra/children/isLeaf/parentCode`）→ PascalCase
  - 文件：`iso-standard/logic.ts`、`skill-manage/logic.ts`、`dictionary/logic.ts`、`role/logic.ts`、`iso-standard/index.vue`、`skill-manage/index.vue`、`role/index.vue`
  - 验收：`grep` 无输出；5 页浏览器回归建立基线
- [ ] **P0-2** 统一按钮 API：`rowActionButtons` 上移 `SingleTableCore`，`rowButtons` 改为派生
- [ ] **P0-3** `initFormData` / `resetObject` 改 `protected`
- [ ] **P0-4** 清理旧响应契约 `res.code === 200`（menu 等）
- [ ] **P0-5** 确认冗余 API 模块引用后标记 `@deprecated`：`api/system/{organization,user,role,dictionary}.ts`
- [ ] **P0-6** 新增 ESLint/CI 守卫：
  - [ ] 禁小写节点字段
  - [ ] 禁页面直接 import `@/api/**`（白名单除外）
  - [ ] **禁组件目录 import `@share/**`、`@/api/**`、任何 Logic、任何 store**
  - [ ] 禁页面手写 `handleAdd/handleBatchDelete/handleRowAction/handleSubmit`
  - [ ] 禁旧契约 `res.code === 200`

---

## 三、C 原子组件改造（零领域依赖 + 能力下沉）

### C-A `YzhTable`

- [ ] **C-A1 零依赖**：确认无 `@share` / API / Logic 引用（当前 ✅）
- [ ] **C-A2 `YzhAction` 描述符**：新增类型，兼容 `Record<string,string>`
- [ ] **C-A3 行按钮控制下沉**：`icon` / `disabled(row)` / `visible(row)` / `overflow` 折叠 / `confirm` / 按行 loading
- [ ] **C-A4 颜色语义外置**：`getRowActionType` 改为取 `YzhAction.type`，移除内置 `edit/delete/toggle-valid→颜色` 假设
- [ ] **C-A5 溢出策略**：`actionMaxInline`（>N 折叠为"更多"下拉）
- [ ] **C-A6 `render:'tag'`**：列级通用标签渲染（`valueMap`/`tagMap` 由 props 传入，不内置 IsValid 语义）
- [ ] **C-A7 选择模式**：`selectMode: 'none'|'single'|'multiple'`（兼容 `selectable`）
- [ ] **C-A8 `rowKey` 中立默认**：文档明确，默认值不绑定 `Id`
- [ ] **C-A9 工具栏声明式**：`toolbarActions: YzhAction[]` + `toolbar-action` 事件（slot 保留）
- [ ] **C-A10 事件载荷**：`row-action(key,row,action)` / `toolbar-action(key,action)`
- [ ] **C-A11 独立可用性**：仅 mock `dataLoader`/`columns` 即可渲染交互
- [ ] **C-A12 契约测试**：同 props ⇒ 同渲染/同事件序列

### C-B `YzhToolbar`

- [ ] **C-B1** 新增 `buttons: YzhAction[]`（left/right 分组）声明式渲染
- [ ] **C-B2** emit `action(key)`；保留 `#left`/`#right` slot
- [ ] **C-B3** 零依赖校验

### C-C `YzhTree`

- [ ] **C-C1 零实体**：移除 `import TreeNode from '@share/types/tree'`，改用组件自身结构化类型
- [ ] **C-C2 字段参数化**：新增 `nodeKey`(默认 `Code`)/`labelField`(默认 `Name`)/`childrenField`(默认 `Children`)/`isLeafField`
- [ ] **C-C3** `hoveredNode`、`treeProps.label/children/isLeaf`、filter/expand 全部改用上述 props
- [ ] **C-C4 节点动作下沉**：`YzhActions` + `actionResolver`（动态文案/禁用/图标/分隔/danger）
- [ ] **C-C5 搜索防抖 + 不深拷贝**
- [ ] **C-C6 虚拟滚动开关** `virtual`（大树切 `ElTreeV2`）
- [ ] **C-C7 懒加载节点级 loading**

### C-D `YzhTreeTable`

- [ ] **C-D1 零实体**：移除 `@share/types/tree` 依赖
- [ ] **C-D2 字段参数化**：`labelField`/`childrenField` 透传 `YzhTree` 并修正 `filterTreeData`
- [ ] **C-D3** `nodeActions` 透传为 `YzhActions`
- [ ] **C-D4 零业务**：移除对 `data.Code` 等硬编码

### C-E `YzhTreeTableCheckSelector`

- [ ] **C-E1 零业务**：移除默认 `{org:'机构',user:'用户'}`、`checkAllExcludeTypes:['org']`、`searchPlaceholder:'搜索接口名称 / 路径'`，全部改为 props 必传
- [ ] **C-E2** 字段参数化审计（`nodeKey/parentKey/nodeTypeField/checkField` 已可配 ✅）
- [ ] **C-E3** 级联/差集/全选逻辑保持（已较完备）；补充契约测试

### C-F 新增 `YzhFormDialog`

- [ ] **C-F1** 组合 `YzhDialog + YzhForm`：`v-model:visible` + `mode: add|edit|detail`
- [ ] **C-F2** 页脚提交/取消、loading、`@submit`/`@cancel`
- [ ] **C-F3** slot：`#default`（表单）、`#footer`

### C-G 新增通用能力

- [ ] **C-G1** `useConfirm`（ElMessageBox 封装，文案由调用方传入）
- [ ] **C-G2** `YzhStatusBadge`/列 tag 复用 `render:'tag'`

### C-H 废弃项

- [ ] **C-H1** 重构/废弃 `YzhCrudPage.vue`（当前 import `@share/api/generic` + `CrudPageLogic` + `@share/types/contracts`）

---

## 四、AD 适配层（纯函数）

- [ ] **AD-1** 新建 `adapters/`，实现 `EntityConfig → YzhTableColumn[]`
- [ ] **AD-2** `EntityConfig → YzhFormField[]`
- [ ] **AD-3** `EntityConfig → YzhAction[]`（Toolbar/RowButtons/CustomButtons）
- [ ] **AD-4** `TreeBehaviorConfig → YzhActions`（节点动作）
- [ ] **AD-5** `TreeItemDto → 组件节点`（字段参数化后返回纯数据）
- [ ] **AD-6** 适配器为纯函数、无副作用、可单测
- [ ] **AD-7** 业务字段名仅出现在适配器层（审计）

---

## 五、ST `SingleTableCore`（单表内核）

> 由 `CrudPageLogic` 演进并改名。

- [ ] **ST-1 改名**：`CrudPageLogic` → `SingleTableCore`；`CrudPageLogic.ts` → `SingleTableCore.ts`
- [ ] **ST-2 过渡别名**：`export { SingleTableCore as CrudPageLogic }`（`@deprecated`）
- [ ] **ST-3 `defaultValues`** getter（新增默认值）
- [ ] **ST-4 `entityNameField`** getter（确认弹窗名称）
- [ ] **ST-5 `onAfterInit()`** 钩子 + `init()` 统一流程
- [ ] **ST-6 `rowActionButtons`** getter（字典/函数式）+ `rowButtons` 派生
- [ ] **ST-7 `dispatch(key,target)` + `registerHandler(key,fn)`**：内置 add/edit/delete/toggle-valid/export
- [ ] **ST-8 `editingRow`** 保护属性（提交合并）
- [ ] **ST-9 `normalizeBeforeSubmit` / `postprocessRows`** 钩子
- [ ] **ST-10 `confirmDelete(rows, {entityName})`** 支持逐行名称
- [ ] **ST-11 改用适配层**：`columns/formFields/searchFields/rowActionButtons` 走 `adapters/`
- [ ] **ST-12 单测**：默认值、dispatch 内置分支、增量方法
- [ ] **ST-13** 生命周期与后端钩子对齐（onBeforeAdd/onAfterAdd/…）

---

## 六、TT `TreeTableCore`（左树右表内核）

> 由 `TreeTableLogic` 演进并改名；树能力抽 `TreeSideMixin`。

- [ ] **TT-1 改名**：`TreeTableLogic` → `TreeTableCore`；过渡别名
- [ ] **TT-2 `TreeSideMixin`**：树状态/加载/索引/dtoToNode/搜索/展开抽为混入
- [ ] **TT-3 `init()`**：`loadConfig → loadTreeRoot → afterTreeLoaded → autoSelectFirstNode → onAfterInit`
- [ ] **TT-4 行 CRUD 泛型流**：`openRowDialog/submitRowForm/deleteRow/batchDeleteRows`
- [ ] **TT-5 树节点 CRUD 泛型流**：`openTreeNodeDialog/submitTreeNodeForm/deleteTreeNodeWithConfirm`
- [ ] **TT-6 统一 `dataLoader`**：自动注入 `RelateField`；`shouldApplyTreeFilter` / `isVirtualNode`
- [ ] **TT-7 联动统一**：`onNodeClick → refresh`；`NoSelectionBehavior`
- [ ] **TT-8 覆盖点**：`defaultTreeValues / treeEntityNameField / requireTreeSelectionForAdd / canAddUnderNode / relatedValue / normalizeBeforeSubmit / postprocessRows / afterTreeLoaded / autoSelectFirstNode`
- [ ] **TT-9 按钮**：`rowActionButtons` 继承自 ST；`nodeActions: YzhActions` + `getNodeActionLabel`
- [ ] **TT-10 `dispatch` 扩展**：add-child / node-edit / node-delete / node-toggle-valid
- [ ] **TT-11 节点索引**：`nodeIndex` O(1) 查找/替换
- [ ] **TT-12 可扩展**：`treeLazy` 透出、搜索防抖、虚拟滚动开关
- [ ] **TT-13 单测**

---

## 七、AS 关联型内核（特殊业务）

- [ ] **AS-1 `AssociationTreeCore`**：左树 + 保存编排 + 乐观更新/回滚 + badge 基类
- [ ] **AS-2 `CheckTreeCore`**：`checkTree/check/add/check/remove/check/all`、关联缓存、祖先补全钩子
- [ ] **AS-3 `LinkTableCore`**：`list/save`、`linkedKeyField`、差集保存、失败回滚、可选分页
- [ ] **AS-4** 字段/配置驱动 `columns`（不硬编码）
- [ ] **AS-5 单测**：差集、级联、回滚

---

## 八、CP Composables

- [ ] **CP-1** `useSingleTable(CoreClass)`（原 `useCrudPage` 改名）
- [ ] **CP-2** `useTreeTable(CoreClass)`
- [ ] **CP-3** `useCheckTree(CoreClass)`
- [ ] **CP-4** `useLinkTable(CoreClass)`
- [ ] **CP-5** 统一 `onMounted → init → nextTick → 注入 refs`
- [ ] **CP-6** 导出到 `@yzh-core` + `composables/index.ts`
- [ ] **CP-7** 类型推导完整（IDE 提示）

---

## 九、PG 受影响前端逻辑修复清单

### PG-A 单表页（8）

- [ ] **PG-A1** `system/user` — 改用 `useSingleTable`，删本地 handler
- [ ] **PG-A2** `system/config` — 同上，`rowButtons.reduce` → `rowActionButtons`
- [ ] **PG-A3** `workflow/job-skill`
- [ ] **PG-A4** `workflow/prompt-template`
- [ ] **PG-A5** `workflow/nc-config`
- [ ] **PG-A6** `foundation/cert-stage` — 用 `defaultValues`
- [ ] **PG-A7** `foundation/phase-definition` — 用 `defaultValues`
- [ ] **PG-A8** `foundation/certification-body` — 用 `defaultValues`

### PG-B 左树右表页（5）

- [ ] **PG-B1** `system/role` — 小写修正；`openAddDialog(parent)` 覆写 → `openTreeNodeDialog`；删 `parentNode/editingNode`；`index.vue` 小写修正；迁移 `useTreeTable`
- [ ] **PG-B2** `foundation/iso-standard` — ISO-1..ISO-10 全部销项（见 V2 §2.7）
- [ ] **PG-B3** `workflow/skill-manage` — SK-1..SK-8 全部销项
- [ ] **PG-B4** `system/dictionary` — DIC-1..DIC-8 全部销项
- [ ] **PG-B5** `system/organization` — ORG-1..ORG-8 全部销项
- [ ] **PG-B6** 全量 `grep`：业务 `.vue` 无手写 `handleAdd/handleBatchDelete/handleRowAction/handleSubmit`

### PG-C 关联型页（5）

- [ ] **PG-C1** `system/role-user` — 迁 `CheckTreeCore`，去内联 `yzhApi`
- [ ] **PG-C2** `system/role-menu`
- [ ] **PG-C3** `system/role-api`
- [ ] **PG-C4** `foundation/cert-org-standard` — 迁 `LinkTableCore`（消除与 stage 的重复）
- [ ] **PG-C5** `foundation/cert-org-stage` — 迁 `LinkTableCore`

### PG-D 手写/未继承页

- [ ] **PG-D1** `system/menu` — 迁 `TreeTableCore`（或按后端形态），去旧契约
- [ ] **PG-D2** `system/api` — 判定是否白名单（接口同步/Swagger，非 CRUD）
- [ ] **PG-D3** 清理 `api/system/{organization,user,role,dictionary}.ts` 冗余模块

### PG-E 无 logic.ts 页（依赖后端）

- [ ] **PG-E1** `workflow/directory`（`StandardDirectoryController`）
- [ ] **PG-E2** `workflow/doc-extraction-rule`
- [ ] **PG-E3** `workflow/report-rule`
- [ ] **PG-E4** `workflow/report-rule-config`
- [ ] **PG-E5** `workflow/ai-usage`（非 CRUD → 白名单）
- [ ] **PG-E6** `workflow/queue`（非 CRUD → 白名单）

---

## 十、BE 后端配套

- [ ] **BE-1** `ConfigDtoConverter` 补齐 `treepconfig` 的 `SearchFields` 映射（dictionary 依赖）
- [ ] **BE-2** `MenuManagementController` 评估迁 `TreeTableControllerBase`
- [ ] **BE-3** `cert-org-standard/stage` 迁 `TreeTableControllerBase` 并固化 `list`/`save` 约定
- [ ] **BE-4** E 类 6 个 `ControllerBase` 逐个评估迁移或列白名单
- [ ] **BE-5** 树节点钩子 `OnBeforeAddTree` 等与前端 `onBeforeAddTree` 对齐

---

## 十一、DC 文档与验收

- [ ] **DC-1** 更新 `docs/10-YZH架构/03-前端架构.md`（新分层与命名）
- [ ] **DC-2** 更新 `docs/10-YZH架构/07-开发流程.md`（新页面标准步骤）
- [ ] **DC-3** 新增页面模板（`logic.ts` / `index.vue`）
- [ ] **DC-4** 页面决策树（SingleTableCore / TreeTableCore / 关联核心 / 纯组件）
- [ ] **DC-5** 守卫规则文档化
- [ ] **DC-6** 回归：5 个左树右表页 + 2 个关联页 + 8 单表页
- [ ] **DC-7** `vue-tsc --noEmit` 0 error
- [ ] **DC-8** 代码量验收：单表 Logic ≤ 15 行、左树右表 Logic ≤ 90 行、`.vue` ≤ 140 行

---

## 十二、里程碑

| 里程碑 | 内容 | 出口标准 |
|--------|------|---------|
| **M1 契约干净** | P0 + C-C1/C-D1/C-E1 | 守卫通过；无小写节点、无组件 `@share` |
| **M2 内核改名完成** | ST-1..ST-12 + TT-1..TT-6 | 别名过渡，编译通过 |
| **M3 组件下沉完成** | C-A..C-G | 组件可脱离 Logic 独立使用 |
| **M4 全站迁移完成** | CP + PG | 页面无手写端点/switch |
| **M5 关联型收敛** | AS + PG-C | 两页重复消除 |
| **M6 后端齐平** | BE | E 类页面可迁移 |

---

## 十三、执行原则

1. **每项独立可回滚**，一个提交一项或一组强相关项。
2. **先守卫、后重构**：P0-6 先行，防止边改边回潮。
3. **别名过渡**：改名阶段保留旧名 re-export，避免大爆炸。
4. **组件先行**：C 阶段尽早完成，ST/TT 依赖其契约稳定。
5. **每阶段跑 `vue-tsc` + 浏览器回归**，再进入下一阶段。
