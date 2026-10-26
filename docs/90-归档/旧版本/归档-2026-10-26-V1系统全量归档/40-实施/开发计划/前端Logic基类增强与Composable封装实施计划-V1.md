# 前端 Logic 基类增强与 Composable 封装实施计划

> **文档版本**: V1
> **创建时间**: 2026-09-20
> **状态**: 待审批
> **核心目标**: 将业务页面散落的重复事件处理逻辑下沉到基类与 Composable，新增页面代码量从 185 行降至 38 行（减少 80%）
> **关联架构**: `docs/10-YZH架构/02-后端架构.md`、`docs/10-YZH架构/07-开发流程.md`

---

## 一、问题现状

### 1.1 重复代码全景

| 页面 | `.vue` 行数 | `logic.ts` 行数 | 重复占比 |
|------|-----------|---------------|---------|
| `cert-stage/index.vue` | 155 | 29 | ~77% |
| `certification-body/index.vue` | 113 | 31 | ~77% |
| `phase-definition/index.vue` | 111 | 28 | ~77% |

### 1.2 三层重复分布

| 重复层 | 具体表现 | 重复度 | 根因 |
|--------|---------|--------|------|
| **Logic 层** | `init()` + `openAddDialog()` 在子类中 100% 重复 | 100% | 基类缺少 `defaultValues` getter 机制 |
| **Vue 事件层** | `handleAdd/handleBatchDelete/handleRowAction/handleSubmit` 四个函数逻辑完全一致 | 100% | 缺少标准 Composable 封装 |
| **Vue 模板层** | YzhTable props/events 绑定、IsValid 插槽、toolbar-left 按钮、dialog+表单 | 90% | UI 与 Logic 天然耦合（Vue SFC 限制） |

### 1.3 典型重复代码（`cert-stage/index.vue`）

```typescript
// 这 4 个 handle* 函数在 4 个页面中 100% 重复
async function handleRowAction(action: string, row: any) {
  if (action === 'edit') {
    logic.openEditDialog(row)
  } else if (action === 'toggle-valid') {
    await logic.toggleRowIsValidWithConfirm(row, { entityName: row.Name })
  } else if (action === 'delete') {
    await logic.confirmDelete([row])
  }
}

async function handleSubmit() {
  try {
    await logic.submitForm()
  } catch (e: any) {
    ElMessage.error(e.message || '保存失败')
  }
}
```

### 1.4 Logic 层典型重复（`cert-stage/logic.ts`）

```typescript
// 以下模式在所有 4 个单表页面重复
async init(): Promise<void> {
  await this.loadConfig()  // 唯一的初始化逻辑
}

openAddDialog(): void {
  super.openAddDialog()
  if (this.formData.IsValid === undefined) this.formData.IsValid = 1  // 默认值各不相同
  if (this.formData.SortOrder === undefined) this.formData.SortOrder = 0
}
```

---

## 二、方案总览

### 2.1 三层下沉策略

| 层级 | 下沉目标 | 下沉方式 |
|------|---------|---------|
| **Logic 基类** | `init()` 统一流程 + 默认值配置 | 新增 `defaultValues` getter + `entityNameField` 属性 + `onAfterInit()` 钩子 |
| **Composable 函数** | 4 个 handle 函数 + 响应式状态组装 | 新建 `useCrudPage(LogicClass)` |
| **业务页面** | 仅保留 UI 模板 + 业务特制逻辑 | `.vue` 通过 composable 获取所有 handlers |

### 2.2 架构关系图

```
┌─────────────────────────────────────────────────────┐
│                  业务 .vue 页面                       │
│  ┌─────────────────────────────────────────────┐    │
│  │  <script setup>                              │    │
│  │    const f = useCrudPage(CertStageLogic)     │    │
│  │    // f 包含所有响应式状态 + handlers        │    │
│  │  </script>                                   │    │
│  │  <template>                                  │    │
│  │    <YzhTable :columns="f.logic.columns" ...> │    │
│  │    <el-dialog v-model="f.logic.dialogVisible">│    │
│  │  </template>                                 │    │
│  └─────────────────────────────────────────────┘    │
│                         │ 使用                        │
│                         ▼                            │
│  ┌─────────────────────────────────────────────┐    │
│  │         useCrudPage Composable                │    │
│  │  - 实例化 Logic                               │    │
│  │  - 提供 rowActionButtons / toolbarConfig      │    │
│  │  - 提供 handleAdd / handleBatchDelete         │    │
│  │  - 提供 handleRowAction / handleSubmit        │    │
│  │  - onMounted 初始化                           │    │
│  └─────────────────────────────────────────────┘    │
│                         │ 操作                        │
│                         ▼                            │
│  ┌─────────────────────────────────────────────┐    │
│  │      CrudPageLogic 基类（增强后）              │    │
│  │  + defaultValues getter（自动应用默认值）      │    │
│  │  + entityNameField（确认弹窗名称字段）        │    │
│  │  + onAfterInit() 钩子                        │    │
│  │  + init() 统一流程                           │    │
│  │  保留原有的 dataLoader / submitForm /         │    │
│  │  confirmDelete / toggleRowIsValidWithConfirm  │    │
│  └─────────────────────────────────────────────┘    │
│                         │ 继承                        │
│                         ▼                            │
│  ┌─────────────────────────────────────────────┐    │
│  │    CertStageLogic（业务子类，仅 8 行）         │    │
│  │  controllerName = 'Foundation/CertStage'      │    │
│  │  defaultValues = { IsValid:1, SortOrder:0 }   │    │
│  └─────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────┘
```

---

## 三、详细改动说明

### 3.1 Phase 1：`CrudPageLogic` 基类增强

**文件**: `src/certplatform-web/yzh.vue.core/src/logic/CrudPageLogic.ts`

#### 3.1.1 新增属性/方法

| 成员 | 类型 | 说明 |
|------|------|------|
| `defaultValues` | `protected get(): Partial<V>` | 子类覆盖，声明新增时的默认值。返回空对象则不自动填充 |
| `entityNameField` | `get(): string` | 确认弹窗中显示的实体名称字段名，默认 `'Name'` |
| `init()` | `async(): Promise<void>` | **增强**：调用 `loadConfig()` → `onAfterInit()` 后结束 |
| `onAfterInit()` | `protected async(): Promise<void>` | **新增钩子**：子类可在此执行额外初始化（如加载字典） |
| `openAddDialog()` | 无返回值 | **增强**：调用 `super` 逻辑后自动 `Object.assign(this.formData, this.defaultValues)` |

#### 3.1.2 关键代码变更

```typescript
// ===== CrudPageLogic.ts 新增/修改部分 =====

export abstract class CrudPageLogic<V extends Record<string, any> = any> {
  // ... 现有属性保持不变 ...

  // ──── 新增：子类可覆盖的默认值配置 ────

  /**
   * 新增时的默认值
   *
   * 子类覆盖此 getter，返回需要预填充的字段值。
   * 替代旧的 "override openAddDialog + 手动设默认值" 写法。
   *
   * @example
   * protected get defaultValues(): Partial<V> {
   *   return { IsValid: 1, SortOrder: 0, Category: 'process' }
   * }
   */
  protected get defaultValues(): Partial<V> {
    return {}
  }

  /**
   * 实体名称字段名（用于确认弹窗显示）
   *
   * 子类覆盖以指定业务实体的"名称"字段。
   * 例：ISOStandard 覆盖为 'StandardName'，CertificationBody 覆盖为 'OrgName'。
   *
   * @default 'Name'
   */
  get entityNameField(): string {
    return 'Name'
  }

  // ──── 修改：init 统一流程 ────

  /**
   * 初始化页面（增强版）
   *
   * 流程：loadConfig → onAfterInit
   *
   * 子类如需额外初始化（如加载字典选项），覆盖 onAfterInit()，而非覆盖 init()。
   */
  async init(): Promise<void> {
    await this.loadConfig()
    await this.onAfterInit()
  }

  /**
   * 初始化后钩子（子类可覆盖）
   *
   * 在 loadConfig 完成后调用，用于执行额外的初始化逻辑。
   */
  protected async onAfterInit(): Promise<void> {}

  // ──── 修改：openAddDialog 自动应用默认值 ────

  openAddDialog() {
    this.dialogMode.value = 'add'
    this.formGroupIndex.value = '0'
    this.initFormData()
    // 自动应用默认值（替代子类 override + 手动 Object.assign）
    Object.assign(this.formData, this.defaultValues)
    this.onPrepareAdd(this.formData)
    this.dialogVisible.value = true
  }
}
```

#### 3.1.3 向后兼容说明

| 现有模式 | 兼容性 | 说明 |
|---------|--------|------|
| 子类 override `init()` | ✅ 完全兼容 | 派生类 override 优先级高于基类；已有子类继续生效 |
| 子类 override `openAddDialog()` | ✅ 完全兼容 | 已有子类的 override 继续覆盖基类逻辑 |
| 子类无 `defaultValues` | ✅ 基类返回空对象 | 不影响现有行为 |

---

### 3.2 Phase 2：新建 `useCrudPage` Composable

**文件**: `src/certplatform-web/yzh.vue.core/src/composables/useCrudPage.ts`（新建）

#### 3.2.1 函数签名

```typescript
function useCrudPage<L extends CrudPageLogic<any>>(
  LogicClass: new () => L
): UseCrudPageReturn<L>
```

#### 3.2.2 返回对象

| 字段 | 类型 | 说明 |
|------|------|------|
| `logic` | `L` | Logic 实例，用于模板中访问 columns / formFields 等 |
| `tableRef` | `Ref` | 模板中 ref 绑定的表格引用 |
| `rowActionButtons` | `ComputedRef<Record<string,string>>` | 行操作按钮字典（直接传给 YzhTable） |
| `toolbarConfig` | `ComputedRef<ToolbarConfig>` | 工具栏配置 |
| `loadTableData` | `(params) => Promise<Page<V>>` | 表格数据加载器 |
| `handleAdd` | `() => void` | 工具栏"新增"点击事件 |
| `handleBatchDelete` | `() => void` | 工具栏"删除"点击事件 |
| `handleRowAction` | `(action, row) => Promise<void>` | 行操作按钮点击事件 |
| `handleSubmit` | `() => Promise<void>` | 表单提交事件 |

#### 3.2.3 关键实现

```typescript
// composables/useCrudPage.ts

import { ref, computed, onMounted, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import type { CrudPageLogic } from '../logic/CrudPageLogic'

export interface UseCrudPageReturn<L> {
  logic: L
  tableRef: any
  rowActionButtons: any
  toolbarConfig: any
  loadTableData: (params: any) => Promise<any>
  handleAdd: () => void
  handleBatchDelete: () => void
  handleRowAction: (action: string, row: any) => Promise<void>
  handleSubmit: () => Promise<void>
}

export function useCrudPage<L extends CrudPageLogic<any>>(
  LogicClass: new () => L,
): UseCrudPageReturn<L> {
  const logic = new LogicClass()
  const tableRef = ref()

  // 组装行操作按钮字典
  const rowActionButtons = computed(() => {
    return logic.rowButtons.reduce((acc, btn) => {
      acc[btn.key] = btn.text
      return acc
    }, {} as Record<string, string>)
  })

  const toolbarConfig = computed(() => (logic.config.value as any)?.Toolbar || {})

  function loadTableData(params: any) {
    return logic.dataLoader(params)
  }

  function handleAdd() {
    logic.openAddDialog()
  }

  function handleBatchDelete() {
    logic.confirmDelete()
  }

  async function handleRowAction(action: string, row: any) {
    if (action === 'edit') {
      logic.openEditDialog(row)
    } else if (action === 'toggle-valid') {
      await logic.toggleRowIsValidWithConfirm(row, {
        entityName: (row as any)[logic.entityNameField],
      })
    } else if (action === 'delete') {
      await logic.confirmDelete([row as any])
    }
  }

  async function handleSubmit() {
    try {
      await logic.submitForm()
    } catch (e: any) {
      ElMessage.error(e.message || '保存失败')
    }
  }

  onMounted(async () => {
    await logic.init()
    await nextTick()
    logic.setTableRef(tableRef.value)
  })

  return {
    logic,
    tableRef,
    rowActionButtons,
    toolbarConfig,
    loadTableData,
    handleAdd,
    handleBatchDelete,
    handleRowAction,
    handleSubmit,
  }
}
```

---

### 3.3 Phase 3：`TreeTableLogic` 基类增强（与 P1 对称）

**文件**: `src/certplatform-web/yzh.vue.core/src/logic/TreeTableLogic.ts`

#### 3.3.1 新增/修改成员

| 成员 | 说明 |
|------|------|
| `defaultTreeValues` | 树节点新增时的默认值 getter |
| `treeEntityNameField` | 树节点名称字段，默认 `'Name'` |
| `init()` | 覆盖基类：`loadConfig` → `loadTreeRoot` → `onAfterInit` |
| `onAfterInit()` | 新增钩子 |
| `openAddTreeDialog()` | 可选：封装树节点新增弹窗逻辑（如果多个 TreeTable 页面重复） |

#### 3.3.2 新建 `useTreeTable` Composable

**文件**: `src/certplatform-web/yzh.vue.core/src/composables/useTreeTable.ts`（新建）

**注意**：ISOStandard 的 TreeTable 布局有一定特殊性（右侧条款表格是独立 CRUD，左侧标准是树），需要验证 composable 对这类"混合模式"的兼容能力。如果 TreeTable 业务页面差异较大，此阶段可降级为**仅增强基类，不做 composable 封装**。

---

## 四、业务页面重构效果

### 4.1 `CertStageLogic` 重构对比

```typescript
// ============ Before（29行）============
export class CertStageLogic extends CrudPageLogic<any> {
  controllerName = 'Foundation/CertStage'

  async init(): Promise<void> {
    await this.loadConfig()
  }

  openAddDialog(): void {
    super.openAddDialog()
    if (this.formData.IsValid === undefined) this.formData.IsValid = 1
    if (this.formData.SortOrder === undefined) this.formData.SortOrder = 0
    if (this.formData.Category === undefined) this.formData.Category = 'process'
  }
}

// ============ After（8行）============
export class CertStageLogic extends CrudPageLogic<any> {
  controllerName = 'Foundation/CertStage'

  protected get defaultValues(): Partial<any> {
    return { IsValid: 1, SortOrder: 0, Category: 'process' }
  }
}
```

### 4.2 `cert-stage/index.vue` 重构对比

```vue
<!-- ============ Before（155行）============ -->
<script setup lang="ts">
import { YzhForm, YzhTable } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, onMounted, nextTick, ref } from 'vue'
import { CertStageLogic } from './logic'

const logic = new CertStageLogic()
const tableRef = ref()
const rowActionButtons = computed(() => {
  return logic.rowButtons.reduce((acc, btn) => {
    acc[btn.key] = btn.text
    return acc
  }, {} as Record<string, string>)
})
const toolbarConfig = computed(() => (logic.config.value as any)?.Toolbar || {})

function loadTableData(params: any) { return logic.dataLoader(params) }
function handleAdd() { logic.openAddDialog() }
async function handleBatchDelete() { await logic.confirmDelete() }
async function handleRowAction(action: string, row: any) {
  if (action === 'edit') { logic.openEditDialog(row) }
  else if (action === 'toggle-valid') { await logic.toggleRowIsValidWithConfirm(row, { entityName: row.Name }) }
  else if (action === 'delete') { await logic.confirmDelete([row]) }
}
async function handleSubmit() {
  try { await logic.submitForm() }
  catch (e: any) { ElMessage.error(e.message || '保存失败') }
}
onMounted(async () => {
  await logic.init()
  await nextTick()
  logic.setTableRef(tableRef.value)
})
</script>

<!-- ============ After（约 30 行）============ -->
<script setup lang="ts">
import { YzhForm, YzhTable, useCrudPage } from '@yzh-core'
import { CertStageLogic } from './logic'

const {
  logic, tableRef, rowActionButtons, toolbarConfig,
  loadTableData, handleAdd, handleBatchDelete,
  handleRowAction, handleSubmit,
} = useCrudPage(CertStageLogic)
</script>
```

### 4.3 量化收益

| 指标 | Before | After | 降幅 |
|------|--------|-------|------|
| `logic.ts` 行数 | 29 | 8 | -72% |
| `index.vue` 行数 | 155 | 30（模板 25 + script 5） | -81% |
| **新增页面总代码** | **184** | **38** | **-80%** |
| 重复代码（4页面合计） | ~600 行 | ~150 行（4处独立声明） | -75% |

---

## 五、实施计划

### 5.1 阶段划分

| 阶段 | 任务 | 修改文件 | 工作量 | 风险 |
|------|------|----------|--------|------|
| **P1** | 增强 `CrudPageLogic` 基类 | `CrudPageLogic.ts` | 0.5h | 低 |
| **P2** | 新建 `useCrudPage` composable | `composables/useCrudPage.ts` | 1h | 低 |
| **P3** | 重构 3 个单表试点页面 | `cert-stage`、`certification-body`、`phase-definition` | 1h | 低 |
| **P4** | TreeTable 基类增强（按需） | `TreeTableLogic.ts` | 0.5h | 低 |
| **P5** | 评估 TreeTable composable 可行性 | 分析 `iso-standard` 差异 | 0.5h | — |
| **P6** | 文档更新 | `docs/10-YZH架构/07-开发流程.md` | 0.5h | 低 |
| **合计** | — | — | **4h** | — |

### 5.2 阶段 P1 详细步骤

| 步骤 | 操作 | 验收标准 |
|------|------|---------|
| P1.1 | 在 `CrudPageLogic` 中添加 `defaultValues` getter（返回空对象） | 编译通过 |
| P1.2 | 添加 `entityNameField` getter（返回 `'Name'`） | 编译通过 |
| P1.3 | 修改 `openAddDialog()`：在 `initFormData()` 后插入 `Object.assign(this.formData, this.defaultValues)` | 单元测试/手动验证新增弹窗默认值正确 |
| P1.4 | 添加 `onAfterInit()` 空钩子 | 编译通过 |
| P1.5 | 修改 `init()` 为 `await this.loadConfig(); await this.onAfterInit()` | 页面加载行为不变 |

### 5.3 阶段 P2 详细步骤

| 步骤 | 操作 | 验收标准 |
|------|------|---------|
| P2.1 | 新建 `composables/` 目录（如不存在） | 目录存在 |
| P2.2 | 创建 `composables/useCrudPage.ts`，实现上述接口 | 编译通过，IDE 类型推导正确 |
| P2.3 | 在 `composables/index.ts` 或 `yzh.vue.core/index.ts` 中导出 | 可通过 `import { useCrudPage } from '@yzh-core'` 导入 |

### 5.4 阶段 P3 详细步骤

| 步骤 | 操作 | 验收标准 |
|------|------|---------|
| P3.1 | 改造 `cert-stage/logic.ts`：删除 `init()` 和 `openAddDialog()`，添加 `defaultValues` | 行数降至 8 行 |
| P3.2 | 改造 `cert-stage/index.vue`：使用 `useCrudPage`，删除本地声明的状态和 handlers | 功能行为与改造前一致 |
| P3.3 | 重复 P3.1-P3.2 到 `certification-body` 和 `phase-definition` | 编译通过，浏览器功能验证 |
| P3.4 | 全量 grep 验证：业务 `.vue` 中不再出现 `handleAdd`、`handleBatchDelete`、`handleRowAction`、`handleSubmit` 的手写实现 | 仅 ISOStandard TreeTable 页面可保留（有特殊逻辑） |

### 5.5 阶段 P4-P5 详细步骤

| 步骤 | 操作 | 验收标准 |
|------|------|---------|
| P4.1 | 在 `TreeTableLogic` 中添加 `defaultTreeValues` getter | 编译通过 |
| P4.2 | 在 `TreeTableLogic` 中添加 `treeEntityNameField` getter | 编译通过 |
| P4.3 | 在 `TreeTableLogic.init()` 中确认流程 `loadConfig → loadTreeRoot → onAfterInit`（查看是否已在当前代码中自然实现） | 流程正确 |
| P5.1 | 分析 ISOStandard TreeTable 左右两侧的 CRUD 逻辑，判断能否抽取 composable | 输出分析报告 |
| P5.2 | 如果可行，实现 `useTreeTable`；否则标记为"保持现状" | — |

---

## 六、风险评估与缓解

| 风险 | 等级 | 影响范围 | 缓解措施 |
|------|------|---------|---------|
| 现有子类重写的 `init()` 与基类新逻辑冲突 | 低 | 所有 `CrudPageLogic` 子类 | 派生类 override 优先级最高；P1 完成后全量测试现有页面 |
| Composable 中 `onMounted` 生命周期绑定问题 | 低 | 使用 `useCrudPage` 的页面 | Composable 内 `onMounted` 注册在调用者 setup 上下文中，行为等同于组件内直接声明 |
| ISOStandard 等 TreeTable 页面有右侧独立 CRUD 逻辑，不完全符合单表 composable | 中 | TreeTable 业务页面 | P5 阶段评估，不强求统一；差异部分通过子类方法保留 |
| 某些子类 `openAddDialog()` 有复杂逻辑（不只是设默认值） | 低 | 个别业务子类 | 仍可选择 override `openAddDialog()`，基类逻辑自动跳过 |
| 业务 `init()` 需要加载字典等额外数据 | 低 | 有额外初始化需求的子类 | 覆盖 `onAfterInit()` 钩子，而非覆盖 `init()` |

---

## 七、验收清单

### 7.1 功能验收

| 验收项 | 验证方式 | 预期结果 |
|--------|---------|---------|
| 单表页面加载 | 浏览器访问 3 个试点页面 | 数据正常展示 |
| 新增操作 | 点击新增按钮，检查默认值 | 表单自动填充 `defaultValues` 中声明的值 |
| 编辑操作 | 点击行编辑，修改后提交 | 数据更新成功，表格行替换 |
| 删除操作 | 选中行删除，确认弹窗显示实体名称 | 弹窗显示正确的 `entityNameField` 字段值 |
| 批量删除 | 选中多行批量删除 | 数量正确的确认弹窗 |
| 行 toggle-valid | 点击启用/禁用 | 状态切换，localRow 更新 |

### 7.2 代码质量验收

| 验收项 | 验证方式 | 预期结果 |
|--------|---------|---------|
| Logic 层代码量 | `wc -l */*.logic.ts` | 单表 Logic ≤ 10 行 |
| Vue 层代码量 | `wc -l */index.vue` | 单表 `.vue` ≤ 40 行 |
| 无手写 handle 函数 | `grep -rn "function handle" pages/foundation/` | 无输出（除 TreeTable 特殊页面） |
| 编译通过 | `cd cert-admin && npx vue-tsc --noEmit` | 0 error |

---

## 八、附录：改造后新增页面的标准模板

### 8.1 `logic.ts` 模板

```typescript
/**
 * {EntityName}Logic — {业务名称} Logic
 *
 * 基于 CrudPageLogic 实现标准 CRUD 页面
 * 后端 Controller：{ControllerName}Controller
 */

import { CrudPageLogic } from '@yzh-core'

export class {EntityName}Logic extends CrudPageLogic<any> {
  controllerName = '{Area}/{ControllerName}'

  /** 新增默认值 */
  protected get defaultValues(): Partial<any> {
    return {
      IsValid: 1,
      // ... 其他默认值
    }
  }

  /** 实体名称字段（用于确认弹窗显示） */
  get entityNameField(): string {
    return '{NameField}'  // 如 'Name'、'OrgName'、'StandardName'
  }

  /** 初始化后钩子（如有额外初始化需求） */
  // protected async onAfterInit(): Promise<void> { }
}
```

### 8.2 `index.vue` 模板

```vue
<script setup lang="ts">
/**
 * {业务名称}（配置驱动 CRUD 页面）
 */
import { YzhForm, YzhTable, useCrudPage } from '@yzh-core'
import { {EntityName}Logic } from './logic'

const {
  logic, tableRef, rowActionButtons, toolbarConfig,
  loadTableData, handleAdd, handleBatchDelete,
  handleRowAction, handleSubmit,
} = useCrudPage({EntityName}Logic)
</script>

<template>
  <div class="{entity-name}-page">
    <YzhTable
      ref="tableRef"
      :columns="logic.columns as any"
      :data-loader="loadTableData"
      :search-fields="logic.searchFields as any"
      :selectable="true"
      :row-action-buttons="rowActionButtons"
      row-key="Code"
      @selection-change="logic.onSelectionChange($event)"
      @row-action="handleRowAction"
    >
      <!-- 状态列：IsValid 自动渲染为 el-tag -->
      <template #column-IsValid="{ row }">
        <el-tag :type="row.IsValid === 1 ? 'success' : 'info'" size="small">
          {{ row.IsValid === 1 ? '启用' : '禁用' }}
        </el-tag>
      </template>

      <!-- 工具栏左侧 -->
      <template #toolbar-left>
        <el-button v-if="toolbarConfig.Add !== false" type="primary" @click="handleAdd">新增</el-button>
        <el-button v-if="toolbarConfig.Delete !== false" type="danger" @click="handleBatchDelete">删除</el-button>
      </template>
    </YzhTable>

    <!-- 新增/编辑弹窗 -->
    <el-dialog
      v-model="logic.dialogVisible.value"
      :title="logic.dialogMode.value === 'add' ? '新增{业务名称}' : '编辑{业务名称}'"
      width="640px"
      :close-on-click-modal="false"
      destroy-on-close
    >
      <YzhForm
        v-model="logic.formData"
        :fields="logic.formFields as any"
        :loading="logic.submitting.value"
        :cols="logic.formLayoutCols as any"
        @submit="handleSubmit"
        @reset="logic.dialogVisible.value = false"
      />
    </el-dialog>
  </div>
</template>

<style scoped>
.{entity-name}-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}
</style>
```

---

## 九、不在本次范围内

以下场景暂不纳入本次改造，后续按需评估：

| 场景 | 原因 | 后续可能性 |
|------|------|-----------|
| TreeTable 右侧独立 CRUD（ISOStandard） | 左右两侧 Logic 混合，composable 模式复杂 | 待 P5 评估 |
| 多 Tab 页面 | UI 结构差异大 | 长期 |
| 嵌入外部组件/选择器的弹窗 | 业务特异性高 | 不纳入 |
| 嵌套路由/keep-alive 页面 | 生命周期复杂 | 不纳入 |
