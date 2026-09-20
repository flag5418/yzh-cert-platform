# 前端 TreeTable 基类增强与 Composable 封装实施计划

> **文档版本**: V1
> **创建时间**: 2026-09-20
> **状态**: 待审批
> **前置文档**: `docs/50-任务/前端Logic基类增强与Composable封装实施计划-V1.md`（单表 CrudPageLogic + useCrudPage）
> **核心目标**: 让"左树右表"页面只需 `extends TreeTableLogic` 并声明少量业务参数，即可获得树加载、树节点增删改、行增删改、树→表联动、大量节点可扩展性的全部能力；新增左树右表页面代码量从 ~350 行降至 ~80 行
> **关联代码**:
> - `src/certplatform-web/yzh.vue.core/src/logic/CrudPageLogic.ts`
> - `src/certplatform-web/yzh.vue.core/src/logic/TreeTableLogic.ts`
> - `src/certplatform-web/yzh.vue.core/src/components/layout/YzhTreeTable.vue`
> - `src/certplatform-web/yzh.vue.core/src/components/layout/YzhTree.vue`
> - `src/certplatform-web/yzh.vue.core/src/types/tree.ts`

---

## 一、背景与目标

### 1.1 用户诉求

TreeTable（左树右表）是本系统的高频页面形态：左侧树承载分类/机构/标准等层级数据，右侧表格承载明细数据。当前每个 TreeTable 页面都在重复实现：

- 树的加载、选中、懒加载、搜索
- 树节点的增删改弹窗（openAddXxx / openEditXxx / submitXxxForm / deleteXxx）
- 表格的 dataLoader（注入关联字段过滤）、行增删改、批量删除、行启用禁用
- 树→表联动的刷新时机与"虚拟根节点/全部节点"的特殊分支

我们希望沉淀一个**默认 TreeTable 基类 TS**，它继承 `CrudPageLogic`（因为 TreeTable 天然包含单表 CRUD 的全部能力），把上述通用逻辑全部下沉。业务页面只需继承基类并实现"业务特殊逻辑"。

### 1.2 设计原则

| 原则 | 说明 |
|------|------|
| **继承单表能力** | `TreeTableLogic extends CrudPageLogic`，行 CRUD 直接复用，不重复造轮子 |
| **配置驱动** | 树表单/表格列/按钮全部来自后端 `TreeTableConfigDto`，前端不维护字段 |
| **钩子定制** | 业务差异通过覆写 getter/钩子表达（默认值、名称字段、是否要求选中节点），而非覆写整个方法 |
| **一次契约** | TreeNode 严格 PascalCase，基类内部统一读写，业务不再关心大小写 |
| **默认可用、按需覆盖** | 基类提供开箱即用的默认实现，90% 页面零覆写；复杂页面可覆盖钩子 |

---

## 二、现状核查（基于当前代码，非假设）

### 2.1 基类继承关系已存在

`TreeTableLogic` 已经是 `CrudPageLogic` 的子类（`TreeTableLogic.ts` 第 73 行 `extends CrudPageLogic<V>`），并叠加了树能力：
`treeData / treeLoading / selectedNode / treeTableConfig`、`loadTreeRoot / loadChildren`、树节点 CRUD、`loadPageWithTree / refreshTable` 等。

**结论**：不需要新建继承链，只需**增强既有 TreeTableLogic**，把业务页面里重复的"行 CRUD + 树节点弹窗"通用流程下沉。

### 2.2 TreeTable 消费页面盘点

| 页面 | Logic | controllerName | 当前行数(Logic) | 重复点 |
|------|-------|---------------|----------------|--------|
| 系统/组织机构 | `system/organization/logic.ts` | `Organization` | ~300 | 行弹窗、机构弹窗、dataLoader、ShowDisabled、perRowActionButtons |
| 系统/数据字典 | `system/dictionary/logic.ts` | `Dictionary` | ~250 | 行弹窗、字典弹窗、fetchItems、数值归一化 |
| 系统/角色 | `system/role/logic.ts` | — | — | 树节点 CRUD |
| 基础/ISO 标准 | `foundation/iso-standard/logic.ts` | `Foundation/ISOStandardTreeTable` | ~200 | 条款弹窗、标准弹窗、dataLoader、refreshTable |
| 工作流/技能管理 | `workflow/skill-manage/logic.ts` | `Workflow/SkillTreeTable` | ~220 | 技能弹窗、分类弹窗、dataLoader、`__all__` 虚拟节点分支 |

这 5 个页面的「行新增/编辑/提交/删除/批量删除」逻辑几乎完全相同；「树节点新增/编辑/提交/删除」也几乎相同，且**基类 `TreeTableLogic` 已经声明了 `treeDialogVisible / treeDialogMode / treeFormData / treeSubmitting / treeParentNode / treeEditingNode`**——但 4 个业务页面都各自另起一套 `std* / category* / org* / tree*` 状态，没有复用基类。

### 2.3 现存缺陷（需在本次一并修复，属真实 bug 级）

#### 缺陷 A：TreeNode 大小写混用（会造成静默失效）

`types/tree.ts` 明确规定 TreeNode 全字段 **PascalCase**（`Code / Name / ParentCode / IsLeaf / Extra / Children`），`dtoToNode` 也按 PascalCase 生成。`YzhTree.vue` 已通过 `readNodeField` 做了兼容读取。但业务 Logic 仍有多处按小写读取：

| 位置 | 代码 | 后果 |
|------|------|------|
| `iso-standard/logic.ts` | `this.selectedNode.value.code`（dataLoader、openAddClauseDialog） | 关联过滤值与 `StandardCode` 恒为 `undefined` |
| `skill-manage/logic.ts` | `this.selectedNode.value.code`、`findCategoryName` 里 `n.code` | 分类过滤、分类名翻译失效 |
| `skill-manage/logic.ts` | `n.children` | 递归查找子节点失效 |
| `dictionary/logic.ts` | `this.selectedNode.value.code`、`node.extra` | 字典项过滤失效 |
| `iso-standard/index.vue` | `node.name`（删除确认标题） | 标题显示 `undefined` |
| `organization/logic.ts` | 已正确使用 PascalCase，并写了详细警告注释 | 反例（正确写法） |

**这是本次改造的最高优先级项**：先统一契约，再谈抽象，否则基类抽出来仍然会被小写读法绕过。

#### 缺陷 B：`rowButtons`（数组）与 `rowActionButtons`（字典）双轨

- `CrudPageLogic.rowButtons`：返回 `{key,text,type}[]` 数组，供 `onToolbarClick/onRowClick` 内部派发。
- `TreeTableLogic.rowActionButtons`：返回 `Record<string,string>`，直接给 `YzhTable` 的 `:row-action-buttons`。
- 单表页面各自在 `.vue` 里用 `rowButtons.reduce(...)` 把数组转字典（cert-stage、certification-body、phase-definition、config）。
- `YzhTable` 实际需要的是 `Record<string,string> | ((row)=>Record)`。

**结论**：统一为基类 `rowActionButtons` getter（返回字典），保留 `rowButtons` 数组作为派生视图（`Object.entries` 即可），消除双轨。

#### 缺陷 C：`initFormData` 是 `private`

业务页面无法复用基类的表单初始化，只能重写 `Object.keys(this.formData).forEach(k => delete ...)` + `Object.assign`。需要改为 `protected`。

#### 缺陷 D：树→表联动双机制

`onNodeClick` 内部调用 `this._tableRef?.refresh()`（依赖模板注入 ref）；而业务页面的 `dataLoader` 又自行按 `selectedNode` 组过滤。两条路径并存，容易漏刷新。需要统一：**基类 `onNodeClick` → 设 selectedNode → 触发表格刷新；`dataLoader` 统一注入关联过滤**。

#### 缺陷 E：虚拟根节点（"全部"）硬编码

`skill-manage` 用 `Code === '__all__'` 特判，`dictionary`/`iso-standard` 用"未选中则空"。（`organization` 支持 `NoSelectionBehavior: 'all'`。）需要基类提供统一语义：`NoSelectionBehavior` 已存在，补一个"虚拟节点"标记与 `shouldApplyTreeFilter()` 钩子。

### 2.4 大量节点（可扩展性）现状

- 5 个页面 `:tree-lazy="false"`，全部一次性加载整棵树。
- `YzhTree.filterTree` / `YzhTreeTable.filterTreeData` 每次输入都**深拷贝节点**（`{ ...node, Children: filteredChildren }`），大树下搜索是 O(n) 拷贝。
- `findNode / removeNodeFromTree / replaceTreeNode` 均为递归线性查找，无节点索引。
- 无虚拟滚动（`el-tree` 不支持；Element Plus 提供 `el-tree-v2` / `ElTreeV2` 支持虚拟化）。

> "大量节点" 在本计划中分两层处理：**结构层**（节点索引、增量更新）与**渲染层**（懒加载、虚拟滚动、搜索防抖）。

---

## 三、目标 API 总览

```
┌──────────────────────────────────────────────────────────────┐
│                     业务页面 index.vue                         │
│   const t = useTreeTable(ISOStandardTreeTableLogic)           │
│   <YzhTreeTable :tree-data="t.logic.treeData.value" ...>      │
│     @tree-node-click="t.handleNodeClick"                      │
│     @tree-node-action="t.handleNodeAction" ...                │
│     <YzhTable :data-loader="t.dataLoader" ...                 │
│       @row-action="t.handleRowAction" />                      │
│   <el-dialog v-model="t.logic.treeDialogVisible.value"> ...   │
└──────────────────────────────────────────────────────────────┘
                            │
┌──────────────────────────────────────────────────────────────┐
│                  TreeTableLogic（增强后）                      │
│  〔继承 CrudPageLogic：行 CRUD / 配置 / 分页 / 搜索 / 导出〕    │
│  + 行 CRUD 泛型流：openRowDialog / submitRowForm /            │
│      deleteRow / batchDeleteRows / dataLoader(注入关联过滤)   │
│  + 树节点 CRUD 泛型流：openTreeNodeDialog / submitTreeNodeForm│
│      / deleteTreeNodeWithConfirm / toggleTreeNodeWithConfirm  │
│  + 联动：onNodeClick / refreshTable / shouldApplyTreeFilter   │
│  + 入口：init() = loadConfig → loadTreeRoot → onAfterInit     │
│  + 可扩展：nodeIndex / treeLazy / searchTree / 虚拟滚动开关    │
│  + 定制钩子：defaultValues / defaultTreeValues /              │
│      entityNameField / treeEntityNameField /                  │
│      requireTreeSelectionForAdd / normalizeBeforeSubmit …     │
└──────────────────────────────────────────────────────────────┘
                            │ 继承
┌──────────────────────────────────────────────────────────────┐
│           业务子类（仅声明业务差异，约 20–40 行）              │
│   controllerName = 'Foundation/ISOStandardTreeTable'          │
│   defaultValues / defaultTreeValues / entityNameField …       │
└──────────────────────────────────────────────────────────────┘
```

---

## 四、Phase 0：契约与类型统一（前置，必须先做）

**目标**：消除大小写与双轨 API，为后续抽象铺平道路。

| 步骤 | 操作 | 文件 | 验收 |
|------|------|------|------|
| P0.1 | 全量修正业务 Logic 的 TreeNode 小写读取 → PascalCase（`node.Code/Name/Extra/Children`） | iso-standard、skill-manage、dictionary、role 的 `logic.ts` | `grep -rn "node\.\(code\|name\|extra\|children\|parentCode\|isLeaf\)\b"` 无输出 |
| P0.2 | 修正 `.vue` 中 `node.name` 等小写引用 | iso-standard/index.vue 等 | 删除确认标题显示正常 |
| P0.3 | 把 `rowActionButtons` getter 上移到 `CrudPageLogic`（返回 `Record<string,string>`），`rowButtons` 改为由它派生 | `CrudPageLogic.ts`、`TreeTableLogic.ts` | 单表/树表页面都可用 `logic.rowActionButtons` |
| P0.4 | `initFormData` 由 `private` 改 `protected` | `CrudPageLogic.ts` | 子类可调用 |
| P0.5 | `TreeTableLogic` 删除重复的 `rowActionButtons` 覆写，改为继承 | `TreeTableLogic.ts` | 编译通过，行为不变 |
| P0.6 | 新增 `normalizeNodeField` 运行时兜底（可选）：`dtoToNode` 内对关键字段做 `?? camelCase` 防御 | `TreeTableLogic.ts` | 大树/异常 DTO 不静默失败 |

> ⚠️ P0.1 会改动现有页面行为（修 bug）。改造前先在浏览器逐页验证：选中节点后右侧过滤、新增时的关联字段、删除确认标题。

---

## 五、Phase 1：CrudPageLogic 增强（承接前置文档）

| 成员 | 类型 | 说明 |
|------|------|------|
| `defaultValues` | `protected get(): Partial<V>` | 新增默认值，基类在 `openAddDialog` 自动 `Object.assign` |
| `entityNameField` | `get(): string`（默认 `'Name'`） | 确认弹窗显示的实体名字段，TreeTable 用于行 |
| `onAfterInit()` | `protected async(): Promise<void>` | `init()` 末尾钩子 |
| `init()` | `async` | `loadConfig → loadPage → onAfterInit` |

> 细节与代码见前置文档第三章 3.1，此处不重复。

---

## 六、Phase 2：TreeTableLogic 行 CRUD 泛型化

**目标**：把 `openAddClauseDialog / openEditClauseDialog / submitClauseForm / deleteClause / batchDeleteClauses` 这类方法从 5 个页面下沉到基类。

### 6.1 新增可覆写钩子

```ts
// ──── 业务差异钩子 ────

/** 新增行时的默认值（替代子类手写 openAddXxxDialog 填充） */
protected get defaultValues(): Partial<V> { return {} }

/** 行实体名称字段（确认弹窗展示，如 'Name'、'ClauseNumber'） */
get entityNameField(): string { return 'Name' }

/** 新增行前是否必须选中树节点（默认 true） */
protected get requireTreeSelectionForAdd(): boolean { return true }

/** 未选中节点时的提示文案 */
protected get selectionRequiredMessage(): string { return '请先在左侧选择节点' }

/** 判定某节点下是否允许新增（默认允许；org 可要求叶子节点） */
protected canAddUnderNode(_node: TreeNode): boolean { return true }

/** 提交前归一化（如字典的数值字段字符串→number） */
protected normalizeBeforeSubmit(payload: Record<string, any>): Record<string, any> { return payload }

/** 行显示名（确认弹窗用，默认读 entityNameField） */
protected rowDisplayName(row: V): string { return String((row as any)[this.entityNameField] ?? '') }
```

### 6.2 统一方法

```ts
/** 打开行弹窗（row 为空=新增，否则=编辑）；返回是否成功打开 */
openRowDialog(row?: V): boolean {
  if (!row && this.requireTreeSelectionForAdd && !this.selectedNode.value) {
    ElMessage.warning(this.selectionRequiredMessage)
    return false
  }
  if (!row && this.selectedNode.value && !this.canAddUnderNode(this.selectedNode.value)) return false

  this.dialogMode.value = row ? 'edit' : 'add'
  this.formGroupIndex.value = '0'
  this.initFormData(row)                 // P0.4 后可用
  if (!row) {
    Object.assign(this.formData, this.defaultValues)
    const rel = this.relatedValue()      // 当前选中节点 Code
    if (rel != null) this.formData[this.relateField] = rel
    this.onPrepareAdd(this.formData)
  } else {
    this.editingRow.value = row
  }
  this.dialogVisible.value = true
  return true
}

/** 当前选中节点的关联值（虚拟节点返回 null） */
protected relatedValue(): string | null {
  const node = this.selectedNode.value
  if (!node || this.isVirtualNode(node)) return null
  return node.Code
}

/** 提交行表单 */
async submitRowForm(): Promise<void> {
  this.submitting.value = true
  try {
    const payload = this.normalizeBeforeSubmit({ ...this.formData })
    if (this.dialogMode.value === 'add') {
      this.onBeforeAdd(payload)
      const saved = await this.add(payload)
      this.onAfterAdd(payload)
      this.insertRow(saved as V)
      ElMessage.success('新增成功')
    } else {
      this.onBeforeUpdate(payload)
      const saved = await this.update(payload)
      this.onAfterUpdate(payload)
      const pk = this.primaryKey
      // 与后端返回值合并，避免表格行丢字段（字典场景）
      this.replaceRowByCode((payload as any)[pk], { ...(this.editingRow.value || {}), ...(saved as any) } as V)
      ElMessage.success('修改成功')
    }
    this.dialogVisible.value = false
  } finally {
    this.submitting.value = false
  }
}

/** 删除单行（确认弹窗由基类统一处理） */
async deleteRow(row: V, options?: { confirm?: boolean }): Promise<void> {
  await this.confirmDelete([row], { entityName: () => this.rowDisplayName(row) })
}

/** 批量删除（默认取选中行） */
async batchDeleteRows(rows?: V[]): Promise<void> {
  await this.confirmDelete(rows ?? this.selectedRows.value)
}
```

> 需给基类 `confirmDelete` 增加可选 `entityName` 展示，支持逐行名称（替代 `删除 ${n} 条记录？`）。
> 新增保护属性 `protected editingRow = ref<V | null>(null)`（提交编辑时合并用）。

### 6.3 统一 dataLoader（自动注入关联过滤）

```ts
/** YzhTable 数据加载器：已合并关联节点过滤，业务无需再覆写 */
async dataLoader(params: PageParams): Promise<Page<V>> {
  this.loading.value = true
  try {
    const { page = 1, rows = this.pagination.pageSize, sort, order, ...searchValues } = params
    const filters = this.buildFilters(searchValues)
    if (this.shouldApplyTreeFilter()) {
      filters.push({ Field: this.relateField, Value: this.selectedNode.value!.Code, Operator: 'eq' })
    }
    const res = await this.apiPost<ApiResponse<PagedData<V>>>('/filter', {
      Page: page, PageSize: rows,
      SortField: sort, SortOrder: order as any, Filters: filters,
    })
    const items = (res.data?.Items ?? []) as V[]
    this.pagination.page = page
    this.pagination.pageSize = rows
    this.pagination.total = res.data?.TotalCount ?? 0
    this.rows.value = items
    this.onDataLoaded(items)
    return { rows: this.postprocessRows(items), total: this.pagination.total }
  } finally {
    this.loading.value = false
  }
}

/** 是否注入关联字段过滤（子类可覆写） */
protected shouldApplyTreeFilter(): boolean {
  const node = this.selectedNode.value
  return !!node && !this.isVirtualNode(node)
}

/** 虚拟节点判定（替代 '__all__' 硬编码） */
protected isVirtualNode(node: TreeNode): boolean {
  return node.NodeType === 'virtual' || (node.Extra as any)?.virtual === true
}

/** 行数据后处理（如 skill-manage 的 CategoryCodeName 翻译） */
protected postprocessRows(rows: V[]): V[] { return rows }
```

**收益**：`iso-standard / skill-manage` 中手写的 `dataLoader` 全部删除；`skill-manage` 的 `__all__` 特判改由 `isVirtualNode` 表达。

---

## 七、Phase 3：TreeTableLogic 树节点 CRUD 泛型化

**目标**：复用基类已有的 `treeDialog*` 状态，把 `openAddStdDialog / openEditStdDialog / submitStdForm / deleteStd`（以及 skill-manage 的 category*、organization 的 org*、dictionary 的 tree*）下沉。

### 7.1 新增钩子与状态

```ts
/** 树节点新增默认值 */
protected get defaultTreeValues(): Partial<Record<string, any>> { return {} }

/** 树节点名称字段（确认/提交用，默认取 TreeConfig.NameField） */
protected get treeEntityNameField(): string { return this.treeConfig?.NameField || 'Name' }

/** 树节点显示名 */
protected treeNodeDisplayName(node: TreeNode): string { return node.Name }

/** 新增树节点前的父亲默认值（如是否生成前端临时 Code） */
protected get treeNodeAutoCode(): boolean { return true }
```

### 7.2 统一方法

```ts
/**
 * 打开树节点弹窗
 * @param mode 'add' | 'edit'
 * @param node 编辑节点（mode=edit 必填）
 * @param parent 新增父节点（缺省取当前选中节点）
 */
openTreeNodeDialog(mode: 'add' | 'edit', node?: TreeNode | null, parent?: TreeNode | null): void {
  this.treeDialogMode.value = mode
  this.treeEditingNode.value = mode === 'edit' ? node ?? null : null
  this.treeParentNode.value = mode === 'add' ? (parent ?? this.selectedNode.value ?? null) : null
  this.resetObject(this.treeFormData)   // resetObject 需改 protected

  const tmpl = this.treeFormConfig?.NewEntity || {}
  const base: Record<string, any> = { ...tmpl, ...this.defaultTreeValues }
  if (this.treeNodeAutoCode && !base[this.primaryKey]) {
    base[this.primaryKey] = crypto.randomUUID?.() || `${Date.now()}`
  }
  if (mode === 'edit' && node) {
    Object.assign(base, this.pickFormValues(this.treeFormFields, node.Extra || {}), {
      [this.primaryKey]: node.Code,
      [this.treeConfig?.ParentCodeField ?? 'ParentCode']: node.ParentCode,
      [this.treeEntityNameField]: node.Name,
    })
  }
  Object.assign(this.treeFormData, base)
  this.treeDialogVisible.value = true
}

/** 提交树节点表单 */
async submitTreeNodeForm(): Promise<void> {
  this.treeSubmitting.value = true
  try {
    const payload = this.normalizeTreeBeforeSubmit({ ...this.treeFormData })
    if (this.treeDialogMode.value === 'add') {
      await this.addTreeNode(this.treeParentNode.value, payload)
    } else {
      await this.updateTreeNode(
        this.treeEditingNode.value!,
        payload[this.treeEntityNameField] ?? '',
        payload,
      )
    }
    this.treeDialogVisible.value = false
  } finally {
    this.treeSubmitting.value = false
  }
}

/** 删除树节点（统一二次确认 + 局部更新 + 表格刷新） */
async deleteTreeNodeWithConfirm(node: TreeNode, options?: { confirm?: boolean }): Promise<void> {
  if (options?.confirm !== false) {
    await ElMessageBox.confirm(`确定删除【${this.treeNodeDisplayName(node)}】？`, '删除确认', {
      type: 'warning', confirmButtonText: '确定删除', cancelButtonText: '取消',
    })
  }
  await this.deleteTreeNode(node, true)   // 基类已做局部删除 + 选中态处理
  await this.refreshTable()
}

/** pickFormValues / normalizeTreeBeforeSubmit 钩子（dictionary 需要） */
protected pickFormValues(fields: Array<{ prop: string }>, extra: Record<string, any>): Record<string, any> { ... }
protected normalizeTreeBeforeSubmit(payload: Record<string, any>): Record<string, any> { return payload }
```

> `resetObject` 由 `private` 改 `protected`。

---

## 八、Phase 4：树→表联动与初始化统一

```ts
/** 初始化：配置 → 树根 → （可选）默认选中 → onAfterInit */
async init(): Promise<void> {
  await this.loadConfig()
  await this.loadTreeRoot()
  await this.afterTreeLoaded()
  if (this.autoSelectFirstNode && this.treeData.value.length > 0) {
    await this.onNodeClick(this.treeData.value[0])
  } else {
    await this.loadPageWithoutTree()
  }
  await this.onAfterInit()
}

/** 树加载后钩子（skill-manage 在此注入"全部"虚拟节点） */
protected async afterTreeLoaded(): Promise<void> {}

/** 是否自动选中第一个节点（默认 false；skill-manage 为 true） */
protected get autoSelectFirstNode(): boolean { return false }
```

`onNodeClick` 统一为：

```ts
async onNodeClick(node: TreeNode): Promise<void> {
  this.selectedNode.value = node
  this.pagination.page = 1
  const onlyLeaf = (this.treeConfig as any)?.OnlyLeafSelectable
  if (onlyLeaf && !node.IsLeaf) return
  await this.refresh()   // 触发 YzhTable 的 dataLoader（内部统一注入关联过滤）
}
```

`refreshTable()` 保留给"无表格 ref"的场景；两者都走 `dataLoader`，消除双机制。

---

## 九、Phase 5：`useTreeTable` Composable

**文件**: `src/certplatform-web/yzh.vue.core/src/composables/useTreeTable.ts`（新建）
**依赖**: 前置文档的 `useCrudPage`（可复用其 `useCrudPageBase` 内部实现）

```ts
export interface UseTreeTableReturn<L extends TreeTableLogic<any>> {
  logic: L
  treeTableRef: Ref<any>
  tableRef: Ref<any>
  rowActionButtons: ComputedRef<Record<string, string>>
  nodeActions: ComputedRef<Record<string, string>>
  getNodeActionLabel: (action: string, node: TreeNode) => string
  dataLoader: (params: PageParams) => Promise<Page<any>>
  // 树事件
  handleNodeClick: (node: TreeNode) => Promise<void>
  handleNodeAction: (action: string, node: TreeNode) => Promise<void>
  handleTreeCheckChange: (nodes: TreeNode[]) => void
  // 行事件
  handleRowAction: (action: string, row: any) => Promise<void>
  handleAddRow: () => void
  handleBatchDeleteRows: () => Promise<void>
  handleRowSubmit: () => Promise<void>
  handleColumnSettingToggle?: () => void
}

export function useTreeTable<L extends TreeTableLogic<any>>(
  LogicClass: new () => L,
): UseTreeTableReturn<L> {
  const logic = new LogicClass()
  const treeTableRef = ref()
  const tableRef = ref()

  const rowActionButtons = computed(() => logic.rowActionButtons)
  const nodeActions = computed(() => logic.nodeActions)

  const dataLoader = (params: PageParams) => logic.dataLoader(params)

  async function handleNodeClick(node: TreeNode) { await logic.onNodeClick(node) }

  async function handleNodeAction(action: string, node: TreeNode) {
    switch (action) {
      case 'edit':         logic.openTreeNodeDialog('edit', node); break
      case 'add-child':    logic.openTreeNodeDialog('add', null, node); break
      case 'delete':       await logic.deleteTreeNodeWithConfirm(node); break
      case 'toggle-valid': await logic.toggleTreeNodeWithConfirm(node); break
      default:
        if (action.startsWith('custom:')) await logic.executeTreeAction(action.slice(7), node)
    }
  }

  async function handleRowAction(action: string, row: any) {
    switch (action) {
      case 'edit':         logic.openRowDialog(row); break
      case 'delete':       await logic.deleteRow(row); break
      case 'toggle-valid': await logic.toggleRowIsValidWithConfirm(row, { entityName: logic.rowDisplayName(row) }); break
      default:
        if (action.startsWith('custom:')) await logic.executeAction(action.slice(7), row)
    }
  }

  const handleAddRow = () => logic.openRowDialog()
  const handleBatchDeleteRows = () => logic.batchDeleteRows()
  async function handleRowSubmit() {
    try { await logic.submitRowForm() }
    catch (e: any) { ElMessage.error(e.message || '保存失败') }
  }

  onMounted(async () => {
    await logic.init()
    await nextTick()
    logic.setTreeTableRef(treeTableRef.value)
    logic.setTableRef(tableRef.value)
  })

  return { logic, treeTableRef, tableRef, rowActionButtons, nodeActions,
           getNodeActionLabel: logic.getNodeActionLabel.bind(logic),
           dataLoader, handleNodeClick, handleNodeAction, handleTreeCheckChange: () => {},
           handleRowAction, handleAddRow, handleBatchDeleteRows, handleRowSubmit }
}
```

**导出**：在 `composables/index.ts`（新建）与 `yzh.vue.core/src/index.ts` 中导出 `useCrudPage`（前置文档）与 `useTreeTable`，命名空间 `@yzh-core`。

---

## 十、Phase 6：大量节点可扩展性

### 10.1 结构层（默认实现，零配置生效）

| 能力 | 设计 | 收益 |
|------|------|------|
| **节点索引** | `private nodeIndex = new Map<string, TreeNode>()`；`loadTreeRoot`/`appendNode`/`removeNodeFromTree`/`replaceTreeNode` 后增量维护；`findNode/replaceTreeNode` 改为 O(1) | 删除/替换从 O(n) 递归 → O(1) |
| **批量建索引** | `rebuildNodeIndex()` 供懒加载、刷新后统一重建 | 一致性 |
| **引用保持** | 局部更新直接改 `node` 字段（响应式），不整体替换数组 | 避免整树重渲染 |

### 10.2 渲染层（配置驱动）

| 能力 | 设计 | 备注 |
|------|------|------|
| **懒加载** | 基类暴露 `get treeLazy()` = `treeConfig?.Lazy ?? false`；`YzhTreeTable :tree-lazy="t.logic.treeLazy"`，`loadChildren` 已实现 | 大树必开 |
| **搜索防抖 + 不拷贝** | `YzhTree/YzhTreeTable` 的 `filterTree` 改为：命中即保留**同引用**节点，用 `VisibleChildren`/`filteredIds` 控制渲染，避免深拷贝；输入 200ms 防抖 | 搜索从 O(n) 拷贝 → O(n) 标记 |
| **虚拟滚动** | 当 `treeConfig.VirtualScroll` 为真或节点数 > `treeVirtualThreshold`（默认 2000），`YzhTreeTable` 内部切换到 `ElTreeV2`（Element Plus 自带虚拟化，需 `height` + 扁平化数据） | P6 可选项，需回归测试 |
| **子节点分页** | `loadChildren` 支持 `PageSize`/`HasMore`，大兄弟节点集合分页追加 | 极端场景 |

> 建议策略：**默认开启懒加载与节点索引；虚拟滚动作为开关**，先在 `organization`（机构可能上千）试点验证后再全量。

---

## 十一、Phase 7：业务页面迁移

### 11.1 迁移清单与前后对比

| 页面 | Logic 前 | Logic 后（预估） | `.vue` 前 | `.vue` 后（预估） |
|------|---------|----------------|----------|------------------|
| iso-standard | ~200 | ~45 | ~300 | ~120 |
| skill-manage | ~220 | ~50 | ~250 | ~110 |
| organization | ~300 | ~90 | ~290 | ~140 |
| dictionary | ~250 | ~70 | ~250 | ~120 |
| role | — | ~25 | — | ~90 |

（organization/dictionary 因有 ShowDisabled、数值归一化等业务差异，保留更多覆写。）

### 11.2 迁移步骤（每页同构）

1. 删除自定义行弹窗方法 → 用 `openRowDialog / submitRowForm / deleteRow / batchDeleteRows`。
2. 删除自定义树节点弹窗方法 → 用 `openTreeNodeDialog / submitTreeNodeForm / deleteTreeNodeWithConfirm`。
3. 删除手写 `dataLoader`（改用基类；仅保留 `postprocessRows` 钩子）。
4. 删除 `init()` 覆写 → 用 `onAfterInit` / `afterTreeLoaded`。
5. 修正所有小写节点读取（P0.1）。
6. `.vue` 改用 `useTreeTable`，删除本地 handlers 与重复 ref。
7. 浏览器逐页回归（见验收清单）。

### 11.3 迁移顺序（风险递增）

`role` → `iso-standard` → `skill-manage` → `dictionary` → `organization`

---

## 十二、实施阶段汇总

| 阶段 | 任务 | 文件 | 工作量 | 风险 |
|------|------|------|--------|------|
| P0 | 契约统一（大小写、按钮双轨、protected） | 5 Logic + 若干 .vue + 基类 | 1.5h | 中（修 bug 会改行为） |
| P1 | CrudPageLogic 增强 | `CrudPageLogic.ts` | 0.5h | 低 |
| P2 | 行 CRUD 泛型化 + 统一 dataLoader | `TreeTableLogic.ts` | 1.5h | 中 |
| P3 | 树节点 CRUD 泛型化 | `TreeTableLogic.ts` | 1h | 低 |
| P4 | 联动与 init 统一 | `TreeTableLogic.ts` | 0.5h | 低 |
| P5 | `useTreeTable` composable | `composables/useTreeTable.ts` + 导出 | 1h | 低 |
| P6 | 大量节点可扩展性 | `TreeTableLogic.ts`、`YzhTree*.vue` | 2h | 中 |
| P7 | 迁移 5 个页面 | 5 × (logic+index.vue) | 2.5h | 中 |
| P8 | 文档更新 | `07-开发流程.md` 等 | 0.5h | 低 |
| **合计** | — | — | **~11h** | — |

---

## 十三、风险与缓解

| 风险 | 等级 | 影响 | 缓解 |
|------|------|------|------|
| 修正大小写读取改变现有行为 | 中 | 过滤/新增关联值从"失效"变为"生效"，可能出现新数据或校验失败 | 每页迁移前记录基线；先单独提交 P0.1 并回归 |
| `initFormData`/`resetObject` 改 protected | 低 | 无（仅放宽可见性） | — |
| `rowButtons`（数组）→ `rowActionButtons`（字典）影响单表页 | 低 | cert-stage 等 4 页 | 保留 `rowButtons` 派生 getter，逐步迁移 |
| 统一 `dataLoader` 后覆盖不了特殊逻辑 | 中 | iso-standard/skill-manage | 提供 `shouldApplyTreeFilter / postprocessRows / buildFilters` 钩子 |
| `useTreeTable` 的 `onMounted` 生命周期 | 低 | 在 setup 中调用即可，行为等同组件内声明 | 明确文档要求 |
| 虚拟滚动引入渲染差异 | 中 | `YzhTreeTable` | 作为开关，先在 organization 试点 |
| 基类 `confirmDelete` 增加实体名展示 | 低 | 删除确认文案变化 | 逐步迁移，保留旧文案兜底 |

---

## 十四、验收清单

### 14.1 功能验收（每页）

| 验收项 | 预期 |
|--------|------|
| 树加载 | 根节点正确展示；开启懒加载后展开才请求 children |
| 节点选中 | 右侧表格按关联字段过滤；分页重置为 1 |
| 虚拟/全部节点 | 选中"全部"时右侧加载全量（不注入关联过滤） |
| 节点新增/编辑/删除 | 弹窗默认值正确；删除确认显示节点名；删除后局部更新+表格刷新 |
| 行新增 | 未选节点时提示；选中后 `relateField` 自动填充；默认值生效 |
| 行编辑/删除/批量删除 | 局部更新；确认文案显示行名 |
| 行启用/禁用 | 确认弹窗 + 局部更新 |
| 搜索/排序/分页 | 与改造前一致 |

### 14.2 代码质量验收

| 验收项 | 验证方式 | 预期 |
|--------|---------|------|
| 无小写节点读取 | `grep -rn "node\.\(code\|name\|extra\|children\)\b" src/.../pages` | 无输出 |
| 无手写行弹窗 | `grep -rn "openAdd.*Dialog\|submit.*Form" pages/**/logic.ts` | 仅剩钩子 |
| 业务 Logic 体量 | `wc -l` | 单页 ≤ 90 行 |
| 编译 | `cd cert-admin && npx vue-tsc --noEmit` | 0 error |
| 类型 | `useTreeTable` 返回类型推导正确 | IDE 有完整提示 |

---

## 十五、标准模板（改造后新增左树右表页面）

### 15.1 `logic.ts`

```ts
import { TreeTableLogic, type TreeNode } from '@yzh-core'

export class XxxTreeTableLogic extends TreeTableLogic<any> {
  controllerName = 'Area/XxxTreeTable'

  /** 行新增默认值 */
  protected get defaultValues() {
    return { IsValid: 1, SortOrder: 0 }
  }

  /** 行实体名称字段 */
  get entityNameField() { return 'Name' }

  /** 树节点新增默认值 */
  protected get defaultTreeValues() {
    return { IsValid: 1, Color: '#409EFF' }
  }

  /** 业务差异：是否自动选中首个节点 */
  protected get autoSelectFirstNode() { return false }

  /** 业务差异：行数据后处理（可选） */
  protected postprocessRows(rows: any[]) { return rows }

  /** 业务差异：树加载后注入虚拟节点（可选） */
  // protected async afterTreeLoaded() { ... }
}
```

### 15.2 `index.vue`

```vue
<script setup lang="ts">
import { YzhForm, YzhTable, YzhTreeTable, useTreeTable } from '@yzh-core'
import { XxxTreeTableLogic } from './logic'

const t = useTreeTable(XxxTreeTableLogic)
</script>

<template>
  <div class="xxx-page">
    <YzhTreeTable
      ref="t.treeTableRef"
      :tree-data="t.logic.treeData.value"
      :tree-lazy="t.logic.treeLazy"
      :node-actions="t.nodeActions.value"
      :get-action-label="t.getNodeActionLabel"
      @tree-node-click="t.handleNodeClick"
      @tree-node-action="t.handleNodeAction"
    >
      <template #treeFooter>
        <el-button type="primary" @click="t.logic.openTreeNodeDialog('add')">新增分类</el-button>
      </template>

      <template #default>
        <YzhTable
          ref="t.tableRef"
          :columns="t.logic.columns as any"
          :data-loader="t.dataLoader"
          :search-fields="t.logic.searchFields as any"
          :selectable="true"
          :row-action-buttons="t.rowActionButtons"
          row-key="Code"
          @selection-change="t.logic.onSelectionChange($event)"
          @row-action="t.handleRowAction"
        >
          <template #toolbar-left>
            <el-button type="primary" @click="t.handleAddRow">新增</el-button>
            <el-button type="danger" @click="t.handleBatchDeleteRows">批量删除</el-button>
          </template>
        </YzhTable>
      </template>
    </YzhTreeTable>

    <!-- 行弹窗 -->
    <el-dialog v-model="t.logic.dialogVisible.value" width="640px" :close-on-click-modal="false">
      <YzhForm v-model="t.logic.formData" :fields="t.logic.formFields as any"
        :loading="t.logic.submitting.value" @submit="t.handleRowSubmit"
        @reset="t.logic.dialogVisible.value = false" />
    </el-dialog>

    <!-- 树节点弹窗 -->
    <el-dialog v-model="t.logic.treeDialogVisible.value" width="560px" :close-on-click-modal="false">
      <YzhForm v-model="t.logic.treeFormData" :fields="t.logic.treeFormFields as any"
        :loading="t.logic.treeSubmitting.value" @submit="() => t.logic.submitTreeNodeForm()"
        @reset="t.logic.treeDialogVisible.value = false" />
    </el-dialog>
  </div>
</template>
```

---

## 十六、不在本次范围

| 场景 | 原因 | 后续 |
|------|------|------|
| 左右两侧均为独立 CRUD 的"混合模式" | 语义复杂 | 视情况用组合而非继承 |
| 多选树 + 批量授权（role-menu/role-api/role-user） | 继承 `BaseRoleTreeLogic`，非 CRUD 形态 | 单独评估 |
| 拖拽排序 / 跨节点移动 | 后端接口尚无统一约定 | 后续 |
| 树节点级权限差异 | 权限体系独立演进 | 后续 |
| 虚拟滚动默认全量开启 | 渲染行为变化大 | P6 试点后决定 |

---

## 十七、建议的执行顺序（最小可验证步）

1. **P0.1 单独提交**（仅修大小写 bug），浏览器回归 5 页 → 建立基线。
2. P1 + P2 + P3 + P4 一次性在基类完成，用 `role` 页面先行接入验证。
3. P5 接入 composable，`role` 改写为模板。
4. P6 在 `organization` 试点懒加载 + 节点索引；虚拟滚动按需。
5. P7 依次迁移其余 4 页，每页一个提交。
6. P8 更新开发流程文档与模板。
