# TreeTable 基类架构设计规范 V1

> **文档性质**: YZH 架构规范（**待审核**，审核通过后方可进入实施）
> **创建时间**: 2026-09-20
> **适用范围**: 所有"左树右表"形态页面。**后续同类业务必须遵循本规范**，不得另起炉灶。
> **关联文档**:
> - `docs/10-YZH架构/单页面基类架构设计规范-V1.md`（后端 `YzhControllerBase` / `TreeTableControllerBase`）
> - `docs/50-任务/前端Logic基类增强与Composable封装实施计划-V1.md`（单表基类代码级设计）
> - `docs/50-任务/前端TreeTable基类增强与Composable封装实施计划-V2.md`（约定矩阵 / 全量改造清单）
>
> **状态说明**: 本文档先定义「是什么、为什么、怎么用」，评审通过后再落代码。

---

## 一、定位与设计目标

### 1.1 是什么

TreeTable 基类逻辑块（`TreeTableLogic`）是**左树右表页面的统一内核**：它继承单表 CRUD 基类 `SingleTableCore` 获得全部表格能力，再叠加"树"的全部能力（加载 / 选中 / 懒加载 / 节点增删改 / 树表联动 / 大树可扩展）。

### 1.2 为什么能成立

三层固定约定（详见 V2 计划）是本基类的前提：

1. **端点约定**：树能力固定为 `treepconfig / tree/root / tree/children / tree/add / tree/update / tree/delete / tree/toggle-valid / tree/action/{m}`；表格能力固定为 `config / filter / add / update / delete / toggle-valid / action/{m}`。
2. **命名约定**：JSON 字段名 = 实体属性名 = 列名（PascalCase）；`Code` 业务键。
3. **契约约定**：统一 `ApiResponse`、`PagedData{Items,TotalCount}`、`TreeItemDto{Code,Name,ParentCode,NodeType,IsLeaf,Extra}`。

因为端点与契约固定，前端基类才能"闭眼调用"；因为后端提供 `virtual` 钩子、前端提供 getter 覆写，业务差异才能只写少量覆盖点。

### 1.3 设计目标

| 目标 | 度量 |
|------|------|
| 开箱即用 | 简单左树右表页面 `logic.ts` ≤ 30 行、`.vue` ≤ 140 行 |
| 差异可表达 | 业务差异仅通过覆盖点（getter/钩子）表达，不复制基类代码 |
| 契约单一 | 全站仅一套树节点命名（PascalCase）与一套端点 |
| 可扩展 | 支持懒加载、千级节点、虚拟滚动 |
| 可防回潮 | 约定可被 ESLint/CI 守卫校验 |

---

## 二、类体系与职责分层

```
                         ┌────────────────────────────────┐
                         │   TreeSideMixin(Base)  混入树能力 │
                         │  treeData / selectedNode /      │
                         │  loadTreeRoot / loadChildren /  │
                         │  dtoToNode / nodeIndex /        │
                         │  findNode / 搜索 / 展开          │
                         └───────────────┬────────────────┘
                                         │ 混入
        ┌────────────────────────────────┴───────────────────────────────┐
        │                                                                │
┌───────▼────────────────┐                            ┌──────────────────▼─────────────┐
│      SingleTableCore      │                            │      BaseLogic（轻量根）        │
│  单表 CRUD 内核（已有）  │                            │  （无 CRUD，仅状态/钩子骨架）    │
│  config/rows/分页/搜索   │                            └──────────────────┬─────────────┘
│  add/update/delete      │                                               │
└───────┬────────────────┘                                               │
        │ 混入 TreeSide                                              混入 TreeSide
┌───────▼────────────────┐                            ┌──────────────────▼─────────────┐
│    TreeTableLogic       │                            │     AssociationTreeLogic       │
│  ① 左树 + 右表 CRUD      │                            │  ② 左树 + 右侧"关联态"（无弹窗）│
│  （标准左树右表）        │                            │                                │
└─────────────────────────┘                            └───────┬──────────────┬─────────┘
                                                               │              │
                                              ┌────────────────▼───┐   ┌──────▼──────────┐
                                              │   CheckTreeLogic    │   │  LinkTableLogic │
                                              │ 类型A 选中-授权型   │   │ 类型B 勾选-关联型│
                                              │ role-user/menu/api  │   │ link-org-standard│
                                              │ checkTree/check/*   │   │ list/save        │
                                              └─────────────────────┘   └─────────────────┘
```

### 2.1 为什么用 Mixin 而不是复制

TS 单继承下，`TreeTableLogic` 根是 `SingleTableCore`，`AssociationTreeLogic` 根不是 CRUD。两者需要**同一套树能力**。选择：

| 方案 | 说明 | 取舍 |
|------|------|------|
| **Mixin（推荐）** | `TreeSideMixin(Base)` 返回注入了树成员的类；两种逻辑各自混入 | 单一实现、无重复；牺牲少量类型可读性，需辅助类型声明 |
| 复制树能力 | 两边各写一份 | 重复，违反本规范初衷 |
| 组合（this.tree） | 内嵌 `TreeSide` 助手对象 | 调用变 `this.tree.xxx`，破坏"业务只继承"的心智，且模板要改引用 |

**决策**：采用 Mixin，并在文档中固定其公开成员与钩子，业务只面对最终类。

### 2.2 三个逻辑类的适用边界

| 逻辑类 | 右侧形态 | 是否有 CRUD 弹窗 | 保存方式 | 典型页面 |
|--------|---------|----------------|---------|---------|
| `TreeTableLogic` | 标准分页表格 | 有（行+树节点） | 表单提交 | iso-standard / skill-manage / organization / dictionary / role |
| `CheckTreeLogic` | 混合树（含勾选态） | 无 | 勾选即存（check/add、check/remove） | role-user / role-menu / role-api |
| `LinkTableLogic` | 全量列表（含 `Linked`） | 无 | 勾选即存（list/save） | cert-org-standard / cert-org-stage |

> 决策依据：**右侧交互语义不同 → 基类不同**；但**左侧树与约定完全相同 → 共用 TreeSideMixin**。

---

## 三、状态模型（TreeTableLogic）

### 3.1 树侧

| 成员 | 类型 | 说明 |
|------|------|------|
| `treeTableConfig` | `Ref<TreeTableConfigDto\|null>` | `treepconfig` 返回（含 TableConfig/TreeConfig/TreeFormConfig） |
| `treeData` | `Ref<TreeNode[]>` | 树数据（PascalCase） |
| `treeLoading` | `Ref<boolean>` | 树加载态 |
| `selectedNode` | `Ref<TreeNode\|null>` | 当前选中节点 |
| `treeDialogVisible / treeDialogMode / treeFormData / treeSubmitting` | — | 树节点弹窗状态（基类统一持有） |
| `treeParentNode / treeEditingNode` | `Ref<TreeNode\|null>` | 新增父节点 / 编辑节点 |
| `nodeIndex` | `Map<string, TreeNode>`（内部） | Code → 节点，O(1) 查找 |

### 3.2 表侧（继承自 SingleTableCore）

`config / rows / loading / selectedRows / pagination / searchParams / sortField / sortOrder / dialogVisible / dialogMode / formData / submitting / showDisabled`。

### 3.3 继承关系约束

- `treeTableConfig.TableConfig` 赋给 `config`，从而复用单表全部派生能力（`columns/formFields/searchFields/toolbarButtons/rowActionButtons`）。
- `treeTableConfig.TreeFormConfig` 提供树节点表单字段（`treeFormFields`）。

---

## 四、生命周期

```
init()
 ├─ loadConfig()            GET treepconfig → treeTableConfig + config
 ├─ loadTreeRoot()          POST tree/root → treeData（建 nodeIndex）
 ├─ afterTreeLoaded()       钩子：注入虚拟节点 / 展开策略
 ├─ autoSelectFirstNode?    true → onNodeClick(首节点)；false → loadPageWithoutTree()
 └─ onAfterInit()           钩子：额外初始化（字典、权限等）
```

| 钩子 | 时机 | 典型用途 |
|------|------|---------|
| `afterTreeLoaded()` | 根节点载入后 | skill-manage 注入"全部"虚拟节点 |
| `autoSelectFirstNode` | 决定是否默认选中 | skill-manage=true |
| `onAfterInit()` | 全部完成 | 加载业务字典 |

---

## 五、能力域

### 5.1 树加载

| 方法 | 端点 | 说明 |
|------|------|------|
| `loadTreeRoot()` | `tree/root` | 载入根并建索引 |
| `loadChildren(node, resolve)` | `tree/children` | 懒加载；`treeConfig.Lazy` 驱动 |
| `dtoToNode(dto, parent)` | — | `TreeItemDto → TreeNode`（计算 level、IsLeaf） |
| `refreshTree()` | — | 重载根 |

### 5.2 树节点 CRUD（泛型流）

| 方法 | 端点 | 说明 |
|------|------|------|
| `openTreeNodeDialog(mode, node?, parent?)` | — | 统一弹窗（复用 `treeDialog*`） |
| `submitTreeNodeForm()` | `tree/add` / `tree/update` | 统一提交 |
| `deleteTreeNodeWithConfirm(node)` | `tree/delete` | 确认 + 局部删除 + 右侧刷新 |
| `toggleTreeNodeWithConfirm(node)` | `tree/toggle-valid` | 确认 + 局部更新 `Extra` |
| `executeTreeAction(method, node)` | `tree/action/{m}` | 自定义 |

钩子：`defaultTreeValues` / `treeEntityNameField` / `treeNodeDisplayName` / `onBeforeAddTree` / `onAfterAddTree` / …（与后端树钩子对齐）。

### 5.3 行 CRUD（继承 + 泛型流）

| 方法 | 端点 |
|------|------|
| `dataLoader(params)` | `filter`（自动注入 `RelateField` 过滤） |
| `openRowDialog(row?)` | —（默认值 + 关联字段自动填充） |
| `submitRowForm()` | `add` / `update` |
| `deleteRow(row)` / `batchDeleteRows(rows?)` | `delete` |
| `toggleRowIsValidWithConfirm(row)` | `toggle-valid` |
| `executeAction(method, row)` | `action/{m}` |

钩子：`defaultValues` / `entityNameField` / `requireTreeSelectionForAdd` / `canAddUnderNode` / `normalizeBeforeSubmit` / `postprocessRows`。

### 5.4 树→表联动

| 方法 | 说明 |
|------|------|
| `onNodeClick(node)` | 选中 → 分页重置 → 触发表格刷新 |
| `shouldApplyTreeFilter()` | 是否注入 `RelateField` 过滤 |
| `isVirtualNode(node)` | 虚拟节点（"全部"）不注入过滤 |
| `loadPageWithoutTree()` | 未选中时按 `NoSelectionBehavior`（empty/all） |

### 5.5 按钮

- `rowActionButtons: Record<string,string> | ((row)=>Record)`（字典，支持按行动态）。
- `nodeActions: Record<string,string>`（来自 `TreeConfig.AllowEdit/AllowDelete` + `EnableField`）。
- `getNodeActionLabel(action, node)`（如 toggle-valid 动态文案）。

### 5.6 增量更新（Split，不重新请求）

`removeRowByCode / replaceRowByCode / insertRow`（表侧）；`removeNodeFromTree / replaceTreeNode / refreshChildren`（树侧）。均优先走组件 ref，无 ref 时回退本地数据。

### 5.7 可扩展性（大树）

| 能力 | 默认 | 说明 |
|------|------|------|
| 节点索引 | 开 | `nodeIndex` O(1) 查找 |
| 懒加载 | 由 `TreeConfig.Lazy` | 大树必开 |
| 搜索防抖 + 不拷贝 | 开 | 避免每次输入深拷贝整树 |
| 虚拟滚动 | 开关（阈值默认 2000） | 切 `ElTreeV2` |

---

## 六、覆盖点总表（模板方法）

> **业务只允许通过下表覆写差异**；不得复制基类方法体。

| 覆盖点 | 默认 | 语义 | 示例 |
|--------|------|------|------|
| `controllerName` | 抽象 | 路由前缀 | `'Foundation/ISOStandardTreeTable'` |
| `defaultValues` | `{}` | 行新增默认值 | `{IsValid:1,SortOrder:0}` |
| `defaultTreeValues` | `{}` | 树节点新增默认值 | `{IsValid:1,Color:'#409EFF'}` |
| `entityNameField` | `'Name'` | 行名（确认弹窗） | dictionary `'DicName'` |
| `treeEntityNameField` | `TreeConfig.NameField` | 节点名 | organization `'OrgName'` |
| `requireTreeSelectionForAdd` | `true` | 新增是否要求选中节点 | |
| `canAddUnderNode(node)` | `true` | 节点下可否新增 | organization 仅叶子 |
| `relatedValue()` | 选中 Code（虚拟节点 null） | 关联字段值 | |
| `shouldApplyTreeFilter()` | 有选中且非虚拟 | 是否注入过滤 | |
| `isVirtualNode(node)` | `NodeType==='virtual'` | 虚拟节点 | skill-manage `__all__` |
| `postprocessRows(rows)` | 原样 | 行后处理 | skill-manage 翻译分类名 |
| `normalizeBeforeSubmit(payload)` | 原样 | 提交前归一 | dictionary Decimal→number |
| `afterTreeLoaded()` | 空 | 树载入后 | 注入"全部" |
| `autoSelectFirstNode` | `false` | 默认选中首节点 | skill-manage |
| `buildFilters(extra?)` | 基类 | 附加过滤 | organization ShowDisabled（基类已自动） |
| `on<Xxx>` 钩子族 | 空 | 与后端钩子对齐 | |

### 6.1 与后端钩子对齐表

| 后端 | 前端 |
|------|------|
| `OnBuildingFilter` | `buildFilters` / `shouldApplyTreeFilter` |
| `OnQueried` | `onDataLoaded` / `postprocessRows` |
| `OnBeforeAdd/OnAfterAdd` | `onBeforeAdd/onAfterAdd` |
| `OnBeforeUpdate/OnAfterUpdate` | `onBeforeUpdate/onAfterUpdate` |
| `OnBeforeDelete/OnAfterDelete` | `onDelete/onAfterDelete` |
| `OnBeforeAddTree/OnAfterAddTree` | `onBeforeAddTree/onAfterAddTree` |
| `OnBeforeUpdateTree/OnAfterUpdateTree` | `onBeforeUpdateTree/onAfterUpdateTree` |
| `OnBeforeDeleteTree/OnAfterDeleteTree` | `onBeforeDeleteTree/onAfterDeleteTree` |
| `MapToTreeItem` | `dtoToNode` |

---

## 七、Composables

| Composable | 用于 | 返回 |
|-----------|------|------|
| `useCrudPage(LogicClass)` | 单表页面 | `logic / tableRef / rowActionButtons / dataLoader / handle*` |
| `useTreeTable(LogicClass)` | 左树右表（CRUD） | 上表 + `treeTableRef / nodeActions / handleNodeClick / handleNodeAction` |
| `useCheckTree(LogicClass)` | 类型A 选中-授权 | `logic / roleTreeRef / checkSelectorRef / handlers` |
| `useLinkTable(LogicClass)` | 类型B 勾选-关联 | `logic / treeRef / tableRef / handlers` |

统一约定：composable 在 `onMounted` 内 `await logic.init()`、`await nextTick()`、注入组件 ref。**`<script setup>` 中不得再手写 `handle*`。**

---

## 八、使用方法（How To）

### 8.1 标准左树右表（新增页面三步）

**Step 1 后端**：`XxxTreeTableController : TreeTableControllerBase<TTree, TEntity>`，设置 `TreeConfig`（RelateField/NameField/EnableField/NoSelectionBehavior/AllowEdit/AllowDelete）与 `TreeFormConfigName`。

**Step 2 `logic.ts`**（只写差异）：

```ts
import { TreeTableLogic } from '@yzh-core'

export class XxxTreeTableLogic extends TreeTableLogic<any> {
  controllerName = 'Area/XxxTreeTable'
  protected get defaultValues() { return { IsValid: 1, SortOrder: 0 } }
  protected get defaultTreeValues() { return { IsValid: 1 } }
  get entityNameField() { return 'Name' }
}
```

**Step 3 `index.vue`**：

```vue
<script setup lang="ts">
import { YzhForm, YzhTable, YzhTreeTableLayout, useTreeTable } from '@yzh-core'
import { XxxTreeTableLogic } from './logic'
const t = useTreeTable(XxxTreeTableLogic)
</script>

<template>
  <YzhTreeTableLayout ref="t.treeTableRef" :tree-data="t.logic.treeData.value"
    :tree-lazy="t.logic.treeLazy" :node-actions="t.nodeActions.value"
    :get-action-label="t.getNodeActionLabel"
    @tree-node-click="t.handleNodeClick" @tree-node-action="t.handleNodeAction">
    <template #treeFooter>
      <el-button type="primary" @click="t.logic.openTreeNodeDialog('add')">新增分类</el-button>
    </template>
    <template #default>
      <YzhTable ref="t.tableRef" :columns="t.logic.columns as any" :data-loader="t.dataLoader"
        :search-fields="t.logic.searchFields as any" :selectable="true"
        :row-action-buttons="t.rowActionButtons" row-key="Code"
        @selection-change="t.logic.onSelectionChange($event)" @row-action="t.handleRowAction">
        <template #toolbar-left>
          <el-button type="primary" @click="t.handleAddRow">新增</el-button>
          <el-button type="danger" @click="t.handleBatchDeleteRows">批量删除</el-button>
        </template>
      </YzhTable>
    </template>
  </YzhTreeTableLayout>

  <el-dialog v-model="t.logic.dialogVisible.value" width="640px">
    <YzhForm v-model="t.logic.formData" :fields="t.logic.formFields as any"
      :loading="t.logic.submitting.value" @submit="t.handleRowSubmit"
      @reset="t.logic.dialogVisible.value = false" />
  </el-dialog>

  <el-dialog v-model="t.logic.treeDialogVisible.value" width="560px">
    <YzhForm v-model="t.logic.treeFormData" :fields="t.logic.treeFormFields as any"
      :loading="t.logic.treeSubmitting.value" @submit="() => t.logic.submitTreeNodeForm()"
      @reset="t.logic.treeDialogVisible.value = false" />
  </el-dialog>
</template>
```

### 8.2 覆盖点使用示例（业务差异）

```ts
export class OrgPageLogic extends TreeTableLogic<any> {
  controllerName = 'Organization'
  get treeEntityNameField() { return 'OrgName' }
  protected get requireTreeSelectionForAdd() { return true }
  protected canAddUnderNode(node: TreeNode) {
    if (node.IsLeaf !== true) { ElMessage.warning('请选择末端机构'); return false }
    return true
  }
  // 按行动态按钮（二选一）
  get rowActionButtons() {
    return (row: any) => ({
      edit: '编辑', delete: '删除',
      ...(row.Enable === 1 ? { disable: '禁用' } : { enable: '启用' }),
    })
  }
}
```

### 8.3 命名与契约铁律（使用中必须遵守）

1. **TreeNode 一律 PascalCase**：`node.Code / node.Name / node.Extra / node.IsLeaf / node.Children`。**禁止** `node.code` 等。
2. **响应一律 `ApiResponse`**：`res.success` / `res.data`，**禁止** `res.code === 200`。
3. **端点一律走基类方法**：**禁止**页面内手写 `/api/...`。
4. **业务差异一律走覆盖点**：**禁止**复制基类方法体、**禁止**篡改基类方法签名（如 `openAddDialog(parent)`）。
5. **Code 由后端生成**：前端可生成临时 Code 仅用于树节点新增占位，最终以后端返回为准。

---

## 九、两类特殊左树右表业务分析

> 这两类**不能**套 `TreeTableLogic`（右侧没有实体 CRUD 弹窗，保存方式为"勾选即存"），但**左侧树与约定完全一致**。因此共用 `TreeSideMixin`，另立关联型基类。

### 9.1 类型A：选中-授权型（`/system/role-user`）

**页面结构**：左侧角色树（`YzhTree`，懒加载）＋ 右侧 `YzhTreeTableCheckSelector`（机构+用户混合树，`CheckFlag` 勾选）。

**数据流**：

```
onMounted
 ├─ initCache()           POST check/all → 建立 associationCache（全量关联）
 ├─ loadRoleTreeRoot()    POST tree/root → 角色树（含 badge 数量）
 └─ 选中角色
      ├─ POST checkTree {RoleCode} → 混合树（平铺，含 CheckFlag）
      ├─ 用 associationCache 覆盖 CheckFlag（切换角色零请求）
      └─ 子类后处理（role-api 分组跟随）
勾选/取消
 └─ POST check/add | check/remove → 局部更新缓存（用服务端回传 Applied，含祖先补全）
      └─ 仅刷新当前角色节点 badge（不重建整棵树）
```

**固定端点**：`tree/root`、`tree/children`、`checkTree`、`check/add`、`check/remove`、`check/all`。

**现有实现问题**：

| # | 问题 | 影响 |
|---|------|------|
| A1 | `BaseRoleTreeLogic` 自造一套树加载（`roleTreeData/selectedRole`），未复用框架基类 | 约定双实现 |
| A2 | API 通过构造函数注入（`RoleTreeApi`），`role-user` 还要内联 `yzhApi` 拼 `/api/Role/tree/root` | 端点硬编码、无法统一 |
| A3 | `columns` 在业务类硬编码（`{prop,label}`），非配置驱动 | 与"配置驱动"原则冲突 |
| A4 | `role-user` 有 `injectBadges`，`role-menu/api` 没有；badge/计数逻辑散落 | 能力不齐 |
| A5 | 缓存、勾选保存、祖先补全逻辑全部手写 | 3 份重复 |

**统一设计 `CheckTreeLogic extends AssociationTreeLogic`**：

| 成员 | 说明 |
|------|------|
| 左树 | 复用 `TreeSideMixin`（`tree/root`、`tree/children`，懒加载） |
| `associationCache: Map<contextCode, Set<targetCode>>` | `check/all` 建立 |
| `checkData: Ref<CheckTreeNode[]>` | `checkTree` 结果（平铺混合树） |
| `loadRightSide(node)` | 内部 `checkTree` + 应用缓存 + `onCheckTreeLoaded` 钩子 |
| `handleCheckChange({added,removed})` | `check/add` / `check/remove` + 缓存增量 |
| `selectableNodeTypes` | 抽象：可勾选 NodeType（user/menu/api） |
| `handleCheckTreeLoaded(data, cache)` | 钩子：祖先补全/分组跟随 |
| `getCount(code)` / `badgeFor(code)` | 计数与徽标（统一能力） |
| `columns` | **来自后端配置**（建议 checkTree 一并返回列配置），不再硬编码 |

> `role-user / role-menu / role-api` 收敛为：`controllerName + selectableNodeTypes + columns + 可选 handleCheckTreeLoaded`。

### 9.2 类型B：勾选-关联型（`/cert/link-org-standard`）

**页面结构**：左侧机构树（扁平）＋ 右侧全量目标实体列表（`el-table` + `selection` 列），行含 `Linked` 标记；**勾选/取消即保存**。

**数据流**：

```
onMounted → loadTree()                 POST tree/root → 机构树
选中机构 → loadTable(orgCode)
    ├─ POST list {OrgCode} → 全量目标 + Linked 态
    ├─ clearSelection() → nextTick → toggleRowSelection(Linked=true)
    └─ suppressSelectionChange 抑制初始化勾选事件
勾选/取消 → handleSelectionChange
    ├─ 计算 prev Linked vs current selection 差集
    ├─ 新增 → POST save {OrgCode, XxxCode, Linked:true}
    ├─ 取消 → POST save {OrgCode, XxxCode, Linked:false}
    └─ 更新本地 Linked
```

**固定端点**：`tree/root`、`list`、`save`（**当前后端为 `ControllerBase`，未走基类**）。

**现有实现问题**：

| # | 问题 | 影响 |
|---|------|------|
| B1 | `cert-org-standard` 与 `cert-org-stage` 两页**几乎逐行重复**（仅字段 StandardCode/StageCode、Phase* 不同） | 大量重复，修一处漏一处 |
| B2 | 逻辑全部内联在 `.vue`（无 `logic.ts`） | 无法复用/测试 |
| B3 | 手写 `suppressSelectionChange`、`clearSelection + toggleRowSelection + nextTick` | 易碎（时序竞态） |
| B4 | `save` 失败只 toast，不回滚本地 `Linked` | 状态不一致 |
| B5 | 后端 `ControllerBase`，端点/契约不固定 | 与基类约定不符 |
| B6 | 无分页，全量加载 | 大数据性能 |

**统一设计 `LinkTableLogic extends AssociationTreeLogic`**：

| 成员 | 说明 |
|------|------|
| 左树 | 复用 `TreeSideMixin` |
| `rightRows: Ref<LinkedRow[]>` | 右侧全量/分页列表（含 `Linked`） |
| `linkedKeyField` | 抽象：目标关联键（`StandardCode`/`StageCode`） |
| `loadRightSide(node)` | `list` 接入 + 同步勾选（内部封装 `nextTick` 时序） |
| `handleLinkChange(selection)` | 差集计算 + `save` + **乐观更新 + 失败回滚** |
| `linkFilterKeyword` / `filteredRows` | 搜索（不再每个页面各写 `watch`） |
| `pagination` | 可选分页 |
| 后端 | 迁 `TreeTableControllerBase` 并新增 `list` / `save` 约定端点 |

> 两个页面收敛为：`controllerName + linkedKeyField + 显示列（配置驱动）+ 可选分页`。

### 9.3 三类对比

| 维度 | TreeTableLogic | CheckTreeLogic（A） | LinkTableLogic（B） |
|------|---------------|--------------------|--------------------|
| 左侧树 | ✅ 共用 TreeSide | ✅ 共用 TreeSide | ✅ 共用 TreeSide |
| 右侧 | 分页表格 | 混合勾选树 | 全量列表 + Linked |
| CRUD 弹窗 | 有 | 无 | 无 |
| 保存 | `add/update/delete` | `check/add、check/remove` | `list`/`save` |
| 本地缓存 | 无 | `check/all` 全量缓存 | 行内 `Linked` |
| 局部更新 | Split 方法 | 缓存 + badge 局部刷新 | 乐观更新 + 回滚 |
| 后端基类 | `TreeTableControllerBase` | `TreeTableControllerBase`（check 虚方法） | `TreeTableControllerBase` |

### 9.4 是否合并为一个基类？

**不合并**，但共用 `AssociationTreeLogic` 与 `TreeSideMixin`：

- 类型A 与类型B 的**右侧数据模型与端点不同**，强行合并会产生大量 `if` 分支。
- 二者**共享**：左树加载、选中、索引、`saving` 态、失败提示、缓存概念、badge。
- 因此抽象出共同父类 `AssociationTreeLogic`（提供左树 + 保存编排 + 乐观更新 + 回滚），再派生出两个语义清晰的子类。

---

## 十、反模式与禁止事项

| 反模式 | 正确做法 |
|--------|---------|
| `node.code / node.name` | `node.Code / node.Name` |
| `res.code === 200` | `res.success` |
| 页面内 `yzhApi.post('/api/...')` | 基类方法 |
| 覆写基类方法并改签名（`openAddDialog(parent)`） | 用 `openTreeNodeDialog` / 覆盖点 |
| 复制基类方法体到业务类 | 覆写钩子 |
| 在 `.vue` 手写 `handleAdd/handleRowAction/...` | `useTreeTable` 返回的 handler |
| 业务类硬编码 `columns` | 配置驱动（后端 EntityConfig） |
| 每个页面各写 `watch + filter` 搜索 | 基类统一搜索 |
| 初始化勾选不抑制事件导致误保存 | 基类封装时序 |

---

## 十一、评审清单（请审核）

请重点确认以下决策，通过后进入实施：

| # | 待确认决策 | 备选 |
|---|-----------|------|
| R1 | 树能力用 **Mixin**（`TreeSideMixin`）在 `TreeTableLogic` 与 `AssociationTreeLogic` 间共享 | 复制 / 组合 |
| R2 | 关联型拆为 **`CheckTreeLogic` + `LinkTableLogic`**，共用 `AssociationTreeLogic` 父类 | 合并为一个 / 各自独立 |
| R3 | 类型A/B 的 **`columns` 改为配置驱动**（后端 checkTree/list 一并下发列配置） | 保留前端硬编码 |
| R4 | 类型B 后端 **迁 `TreeTableControllerBase`** 并固化 `list`/`save` 约定端点 | 维持 `ControllerBase` |
| R5 | 类型A/B 是否纳入 **分页** | 全量 / 分页 |
| R6 | 大树的 **虚拟滚动** 是否默认开启（阈值） | 默认关 / 默认开 |
| R7 | 关联型页面的 **失败回滚** 策略（乐观更新回滚 / 先请求后更新） | 乐观 / 悲观 |
| R8 | `role-*` 是否统一 **badge 能力** 到基类 | 保留差异 |

---

## 十二、附图：最终目录与命名（建议）

```
yzh.vue.core/src/
├─ logic/
│  ├─ SingleTableCore.ts            单表内核
│  ├─ TreeTableLogic.ts           左树右表（CRUD）
│  ├─ AssociationTreeLogic.ts     关联型父类（新增）
│  ├─ CheckTreeLogic.ts           类型A（新增）
│  ├─ LinkTableLogic.ts           类型B（新增）
│  └─ TreeSideMixin.ts            树能力混入（新增）
├─ composables/
│  ├─ useCrudPage.ts
│  ├─ useTreeTable.ts
│  ├─ useCheckTree.ts
│  └─ useLinkTable.ts
```

---

## 十三、结论

1. **`TreeTableLogic` 是"左树右表 CRUD"的唯一内核**，业务只写覆盖点。
2. **树能力抽为 `TreeSideMixin`**，被 CRUD 型与关联型共享，避免第三套树实现。
3. **两类特殊业务（选中-授权 / 勾选-关联）另立 `CheckTreeLogic` / `LinkTableLogic`**，共用 `AssociationTreeLogic`，并统一 badge、缓存、乐观更新与回滚。
4. **所有铁律由守卫固化**（禁小写节点、禁页面直连 API、禁旧契约、禁手写 handler）。
5. 待 R1–R8 确认后，按 `前端TreeTable基类增强与Composable封装实施计划-V2` 的 P 阶段落地。
