<template>
  <div class="yzh-tree-table-check-selector">
    <!-- 工具栏 -->
    <div class="yzh-tree-table-check-selector__toolbar">
      <div class="yzh-tree-table-check-selector__selection-info">
        已选择 <strong>{{ checkedCount }}</strong> 条记录
      </div>
      <div v-if="searchable" class="yzh-tree-table-check-selector__search">
        <el-input
          v-model="searchKeyword"
          :placeholder="searchPlaceholder"
          clearable
          size="small"
          prefix-icon="Search"
        />
      </div>
      <div class="yzh-tree-table-check-selector__toolbar-actions">
        <el-button size="small" @click="handleExpandAll">展开全部</el-button>
        <el-button size="small" @click="handleCollapseAll">折叠全部</el-button>
        <el-button size="small" @click="handleCheckAll">全选</el-button>
        <el-button size="small" @click="handleUncheckAll">取消全选</el-button>
      </div>
    </div>

    <!--
      树形表格
      ⚠️ tree-props 必须带 checkStrictly: true（父子勾选独立）
      Element Plus 的 treeProps.checkStrictly 默认为 false：此时 toggleRowStatus()
      会把「勾选某行」级联到 row.children（见 element-plus util.mjs）。而本组件
      在 cascade 模式下也会自己调用 toggleRowSelection(父节点, false) 回填祖先 ——
      两者叠加会把该父节点下的所有子行从 el-table 内部 selection 里一并清掉，
      使 store.selection 与本组件 checkedKeys 长期错位：下一次点击被误判为
      「新增勾选」（added 而非 removed），导致取消勾选不触发后端、状态无法保存。
      置为 true 后 el-table 不再隐式级联，级联语义完全由本组件 owning。
    -->
    <el-table
      ref="tableRef"
      :data="treeData"
      :row-key="nodeKey"
      :tree-props="{ children: 'children', checkStrictly: true }"
      @selection-change="handleSelectionChange"
      :default-expand-all="defaultExpandAll"
      style="width: 100%"
      class="yzh-tree-table-check-selector__table"
    >
      <!-- Checkbox 列 -->
      <el-table-column type="selection" width="50" />

      <!-- 动态列 -->
      <el-table-column
        v-for="col in columns"
        :key="col.prop"
        :prop="col.prop"
        :label="col.label"
        :width="col.width"
        :min-width="col.minWidth"
        :fixed="col.fixed"
        :show-overflow-tooltip="col.showOverflowTooltip !== false"
      >
        <template #default="{ row }">
          <slot :name="`column-${col.prop}`" :row="row" :column="col">
            <!-- 节点类型标签（可通过 typeLabels / typeTagTypes 定制） -->
            <template v-if="col.prop === nodeTypeField">
              <el-tag :type="typeTagType(row[nodeTypeField])" size="small">
                {{ typeLabel(row[nodeTypeField]) }}
              </el-tag>
            </template>
            <!-- 默认显示 -->
            <template v-else>
              {{ row[col.prop] }}
            </template>
          </slot>
        </template>
      </el-table-column>
    </el-table>
  </div>
</template>

<script setup lang="ts">
/**
 * YzhTreeTableCheckSelector - 树形表格勾选选择器
 *
 * 功能：
 * - 接收扁平数据，自动转换为嵌套树（el-table tree 模式）
 * - 支持 checkbox 勾选（treeProps.checkStrictly = true，父子独立）
 * - 勾选/取消立即触发 check-change 事件（auto-save 模式）
 *
 * 事件契约：以 el-table 的 selection-change 为准，用「变更前快照」做差集：
 *   added   = 本次新增勾选的 key
 *   removed = 本次取消勾选的 key
 * 二者互斥，恒有 added ∩ removed = ∅。
 * - 支持展开/折叠、全选/取消全选
 *
 * 典型场景：
 * - 角色-用户关联（机构+用户混合树，勾选用户）
 * - 角色-菜单关联（菜单树，勾选菜单）
 */
import { ref, computed, watch, nextTick } from 'vue'
import { ElInput } from 'element-plus'
import type { ElTable } from 'element-plus'

// ========================================================
// 类型定义
// ========================================================

interface FlatNode {
  [key: string]: any
  Code: string
  ParentCode?: string | null
  NodeType?: string
  CheckFlag?: boolean
  children?: FlatNode[]
}

interface TreeNode extends FlatNode {
  children: TreeNode[]
}

interface ColumnConfig {
  prop: string
  label: string
  width?: number
  minWidth?: number
  fixed?: boolean | 'left' | 'right'
  showOverflowTooltip?: boolean
}

// ========================================================
// Props
// ========================================================

type TagType = 'primary' | 'success' | 'info' | 'warning' | 'danger'

interface Props {
  /** 扁平数据（后端返回） */
  flatData: FlatNode[]
  /** 节点唯一 key 字段 */
  nodeKey?: string
  /** 父节点编码字段 */
  parentKey?: string
  /** 节点类型字段 */
  nodeTypeField?: string
  /** 勾选状态字段 */
  checkField?: string
  /** 列配置 */
  columns?: ColumnConfig[]
  /** 是否显示类型列（默认 true） */
  showTypeColumn?: boolean
  /** 默认展开所有节点 */
  defaultExpandAll?: boolean
  /** 节点类型显示文案映射（如 { menu: '菜单' }） */
  typeLabels?: Record<string, string>
  /** 节点类型标签颜色映射（如 { menu: 'warning' }） */
  typeTagTypes?: Record<string, TagType>
  /** 「全选」时排除的节点类型（默认排除机构 org） */
  checkAllExcludeTypes?: string[]
  /** 父子级联勾选：勾选父节点自动勾选全部子孙，反之回填父节点（默认 false，保持独立勾选） */
  cascade?: boolean
  /** 是否显示搜索框（命中节点保留其祖先层级） */
  searchable?: boolean
  /** 搜索字段（默认 Name） */
  searchFields?: string[]
  /** 「已选择」只统计该类型节点（如 api）；不传则统计全部 */
  countType?: string
  /** 搜索框占位文字 */
  searchPlaceholder?: string
}

const props = withDefaults(defineProps<Props>(), {
  nodeKey: 'Code',
  parentKey: 'ParentCode',
  nodeTypeField: 'NodeType',
  checkField: 'CheckFlag',
  columns: () => [],
  showTypeColumn: true,
  defaultExpandAll: false,
  typeLabels: undefined,
  typeTagTypes: undefined,
  checkAllExcludeTypes: () => ['org'],
  cascade: false,
  searchable: false,
  searchFields: () => ['Name'],
  countType: undefined,
  searchPlaceholder: '搜索接口名称 / 路径',
})

// 默认类型文案 / 颜色（保持 role-user 原有行为）
const DEFAULT_TYPE_LABELS: Record<string, string> = { org: '机构', user: '用户' }
const DEFAULT_TYPE_TAG_TYPES: Record<string, TagType> = { org: 'primary', user: 'success' }

/** 节点类型显示文案 */
function typeLabel(type: string): string {
  return props.typeLabels?.[type] ?? DEFAULT_TYPE_LABELS[type] ?? type
}

/** 节点类型标签颜色 */
function typeTagType(type: string): TagType {
  return props.typeTagTypes?.[type] ?? DEFAULT_TYPE_TAG_TYPES[type] ?? 'info'
}

// ========================================================
// Emits
// ========================================================

const emit = defineEmits<{
  (e: 'check-change', payload: { added: string[]; removed: string[] }): void
}>()

// ========================================================
// 内部状态
// ========================================================

const tableRef = ref<InstanceType<typeof ElTable>>()
const treeData = ref<TreeNode[]>([])
const searchKeyword = ref('')
const checkedKeys = ref<Set<string>>(new Set())
const allNodeMap = ref<Map<string, TreeNode>>(new Map())
const expandedKeys = ref<Set<string>>(new Set())
const isBulkUpdating = ref(false)
/**
 * 上一次 selection 快照（必须与 el-table store.selection 恒等）
 * handleSelectionChange 用它与本次 selection 做差集得出 added/removed。
 * 每次程序化改动 selection（syncTableCheckState / 级联）后都必须同步刷新，
 * 否则差集会失准 → 页面收不到变更 → 后端接口不被调用。
 */
const prevSelectionKeys = ref<Set<string>>(new Set())

// ========================================================
// 计算属性
// ========================================================

const checkedCount = computed(() => {
  if (!props.countType) return checkedKeys.value.size

  let count = 0
  for (const key of checkedKeys.value) {
    if (allNodeMap.value.get(key)?.[props.nodeTypeField] === props.countType) count++
  }
  return count
})

/** 搜索过滤后的扁平数据（命中节点保留祖先） */
const visibleFlatData = computed<FlatNode[]>(() => {
  const keyword = searchKeyword.value.trim().toLowerCase()
  if (!keyword) return props.flatData

  const matched = new Set<string>()
  for (const item of props.flatData) {
    const hit = props.searchFields.some((field) =>
      String(item[field] ?? '').toLowerCase().includes(keyword),
    )
    if (hit) matched.add(String(item[props.nodeKey]))
  }

  // 向上收集祖先
  const byCode = new Map(props.flatData.map((i) => [String(i[props.nodeKey]), i]))
  const keep = new Set(matched)
  for (const code of matched) {
    let parent = byCode.get(code)?.[props.parentKey]
    while (parent && !keep.has(String(parent))) {
      keep.add(String(parent))
      parent = byCode.get(String(parent))?.[props.parentKey]
    }
  }

  return props.flatData.filter((item) => keep.has(String(item[props.nodeKey])))
})

// ========================================================
// 扁平数据 → 嵌套树转换
// ========================================================

function flatToTree(flat: FlatNode[]): TreeNode[] {
  const nodeMap = new Map<string, TreeNode>()
  const roots: TreeNode[] = []

  // 第一遍：创建所有节点
  for (const item of flat) {
    const node: TreeNode = {
      ...item,
      children: [],
    }
    nodeMap.set(item[props.nodeKey], node)
    allNodeMap.value.set(item[props.nodeKey], node)
  }

  // 第二遍：构建父子关系
  for (const item of flat) {
    const node = nodeMap.get(item[props.nodeKey])!
    const parentCode = item[props.parentKey]

    if (parentCode && nodeMap.has(parentCode)) {
      const parent = nodeMap.get(parentCode)!
      parent.children.push(node)
    } else {
      roots.push(node)
    }
  }

  return roots
}

// ========================================================
// 初始化默认勾选状态
// ========================================================

function initCheckedState(nodes: TreeNode[]) {
  const newSet = new Set<string>()

  function walk(items: TreeNode[]) {
    for (const node of items) {
      if (node[props.checkField]) {
        newSet.add(node[props.nodeKey])
      }
      if (node.children && node.children.length > 0) {
        walk(node.children)
      }
    }
  }
  walk(nodes)

  checkedKeys.value = newSet
}

// ========================================================
// 设置表格勾选状态
// ========================================================

function syncTableCheckState() {
  if (!tableRef.value) return

  // checkStrictly: true 下 toggleRowSelection 只影响单行，
  // 因此可以安全地「先 clearSelection 再逐行勾选」，store 与 checkedKeys 恒等。
  // 期间触发的 selection-change 由 isBulkUpdating 拦截（nextTick 解除）。
  isBulkUpdating.value = true

  // 清空所有勾选
  tableRef.value.clearSelection()

  // 只勾选「当前可见」的节点：搜索过滤后 allNodeMap 仅含可见节点
  const visibleKeys = new Set<string>()
  for (const key of checkedKeys.value) {
    const node = allNodeMap.value.get(key)
    if (node) {
      tableRef.value.toggleRowSelection(node, true)
      visibleKeys.add(key)
    }
  }

  // ⚠️ 快照必须与 el-table store.selection 「逐行一致」（含可见性）。
  // 若这里直接取全部 checkedKeys，搜索态下快照会多出「已勾选但被过滤掉」的项；
  // 此时用户再点任意一行，diff 会把这些不可见项算成 removed → 误删授权。
  prevSelectionKeys.value = visibleKeys

  nextTick(() => {
    isBulkUpdating.value = false
  })
}

// ========================================================
// 监听数据变化
// ========================================================

/**
 * 重建树结构
 * @param data       要渲染的扁平数据（可能是搜索过滤后的）
 * @param resetChecks 是否以 CheckFlag 重置勾选（数据源变化时重置；搜索时保留）
 */
function rebuildTree(data: FlatNode[], resetChecks: boolean): void {
  //
  // 🔴 必须在替换 treeData 之前就举起闸门。
  //
  // 数据集合一变（切换角色 / 搜索过滤 / 折叠展开），el-table 自身的 data watcher
  // 会调用 store.cleanSelection()，把「不在新数据里」的旧行从内部 selection 剔除，
  // 并同步派发 selection-change。该事件发生在本函数的 nextTick(syncTableCheckState)
  // 之前，若此刻闸门未举起，handleSelectionChange 会以
  //     prevSelectionKeys = 上一个角色的勾选集合
  //     currKeys          = 已被 cleanSelection 清空的 selection
  // 算出 removed = 上一个角色的全部 code；而页面在切换角色时已经先更新了
  // selectedRole，于是拿【新角色】的 Code 去调 check/remove ——
  // 新角色中同名的菜单授权被真实删除（静默数据丢失）。
  //
  // 实测复现：总管理员(已勾 MENU_00002/MENU_00106) → 切到运维人员，
  //   发出 checkRemove(ROLE_000102, [MENU_00106, MENU_00002])，运维人员授权被清空。
  //
  isBulkUpdating.value = true

  allNodeMap.value.clear()

  if (!data || data.length === 0) {
    treeData.value = []
    if (resetChecks) checkedKeys.value = new Set()
    nextTick(() => {
      syncTableCheckState()
      releaseBulkGuard()
    })
    return
  }

  // 转换为嵌套树
  treeData.value = flatToTree(data)

  // 初始化勾选状态
  if (resetChecks) {
    initCheckedState(treeData.value)
  }

  // 展开所有节点（如果需要）
  if (props.defaultExpandAll) {
    expandedKeys.value.clear()
    expandAllNodes(treeData.value)
  }

  // 同步表格勾选状态（渲染完成后），再释放闸门
  nextTick(() => {
    syncTableCheckState()
    releaseBulkGuard()
  })
}

/**
 * 释放闸门
 *
 * 再等一个 tick 才落下，确保 el-table 因数据变化产生的 selection-change 已全部派发。
 * syncTableCheckState 内部也会释放一次，此处是双保险。
 */
function releaseBulkGuard(): void {
  nextTick(() => {
    isBulkUpdating.value = false
  })
}

// 数据源变化（切换角色 / 重新拉取）：以 CheckFlag 为准重置勾选
watch(
  () => props.flatData,
  (data) => {
    rebuildTree(data, true)
  },
  { immediate: true },
)

// 搜索过滤变化：只重建可见树，保留已有勾选（含级联产生的勾选）
watch(searchKeyword, () => {
  rebuildTree(visibleFlatData.value, false)
})

// ========================================================
// 展开/折叠
// ========================================================

function expandAllNodes(nodes: TreeNode[]) {
  for (const node of nodes) {
    if (node.children && node.children.length > 0) {
      expandedKeys.value.add(node[props.nodeKey])
      expandAllNodes(node.children)
    }
  }
}

function handleExpandAll() {
  expandAllNodes(treeData.value)
}

function handleCollapseAll() {
  expandedKeys.value.clear()
  // el-table 没有直接的 collapseAll 方法，需要重新渲染
  // 通过设置 data 触发重新渲染 —— 这一清一还同样会触发 cleanSelection，需举闸门
  isBulkUpdating.value = true
  const data = treeData.value
  treeData.value = []
  nextTick(() => {
    treeData.value = data
    releaseBulkGuard()
  })
}

// ========================================================
// 全选/取消全选
// ========================================================

function handleCheckAll() {
  if (!tableRef.value) return

  isBulkUpdating.value = true

  // 收集所有用户节点（只勾选用户，不勾选机构）
  const userNodes = collectUserNodes(treeData.value)
  const newSet = new Set(checkedKeys.value)

  for (const node of userNodes) {
    newSet.add(node[props.nodeKey])
    tableRef.value.toggleRowSelection(node, true)
  }
  checkedKeys.value = newSet
  prevSelectionKeys.value = new Set(newSet)

  isBulkUpdating.value = false

  // 一次性触发批量事件
  emitCheckChange([], userNodes.map((n) => n[props.nodeKey]))
}

function handleUncheckAll() {
  if (!tableRef.value) return

  isBulkUpdating.value = true

  const removedKeys = Array.from(checkedKeys.value)
  checkedKeys.value = new Set()
  prevSelectionKeys.value = new Set()
  tableRef.value.clearSelection()

  isBulkUpdating.value = false

  // 一次性触发批量事件
  emitCheckChange(removedKeys, [])
}

function collectUserNodes(nodes: TreeNode[]): TreeNode[] {
  const excluded = props.checkAllExcludeTypes ?? []
  const result: TreeNode[] = []
  for (const node of nodes) {
    if (!excluded.includes(node[props.nodeTypeField])) {
      result.push(node)
    }
    if (node.children && node.children.length > 0) {
      result.push(...collectUserNodes(node.children))
    }
  }
  return result
}

// ========================================================
// 勾选事件处理
// ========================================================

/** 收集全部子孙节点（不含自身） */
function collectSubtree(node: TreeNode): TreeNode[] {
  const result: TreeNode[] = []
  const walk = (items: TreeNode[]) => {
    for (const item of items) {
      result.push(item)
      if (item.children?.length) walk(item.children)
    }
  }
  walk(node.children ?? [])
  return result
}

/** 集合差集：to - from */
function diffKeys(from: Set<string>, to: Set<string>): string[] {
  const out: string[] = []
  for (const k of to) if (!from.has(k)) out.push(k)
  return out
}

/**
 * 级联勾选：父节点 → 全部子孙，叶子节点 → 回填父节点状态
 *
 * ⚠️ 两点必须成立，否则「取消勾选」会静默失效：
 *
 * 1) before 必须是「本次变更前的快照」（由 handleSelectionChange 传入）。
 *    若以 checkedKeys 为基准重算，被点的那一项永远算不进差异，
 *    最终发出 added=[]/removed=[] → 页面收不到变更 → 后端接口不被调用。
 *
 * 2) el-table 的 tree-props 必须带 checkStrictly: true。
 *    本函数会用 toggleRowSelection(父节点, false) 回填祖先；若 el-table 同时
 *    开启隐式级联，这一次调用会把该父节点下所有子行一并从 store.selection 中
 *    移除，使 store 与本组件 checkedKeys 错位，下一次点击即被误判为「新增勾选」。
 *
 * 函数内部所有 toggleRowSelection 都会同步触发 el-table 的 selection-change，
 * 因此全程由 isBulkUpdating 拦截，避免递归。
 */
function emitCascadeChange(row: TreeNode, checked: boolean, before: Set<string>) {
  const next = new Set(before)

  const apply = (node: TreeNode, value: boolean) => {
    const key = String(node[props.nodeKey])
    if (value) next.add(key)
    else next.delete(key)
    tableRef.value?.toggleRowSelection(node, value)
  }

  isBulkUpdating.value = true

  apply(row, checked)
  for (const child of collectSubtree(row)) apply(child, checked)

  // 回填祖先：子级（排除分组/机构类节点）全选 → 勾上父，否则取消父
  const excluded = props.checkAllExcludeTypes ?? []
  let parentCode = row[props.parentKey]
  while (parentCode) {
    const parent = allNodeMap.value.get(String(parentCode))
    if (!parent) break

    const children = parent.children.filter(
      (c) => !excluded.includes(String(c[props.nodeTypeField])),
    )
    apply(parent, children.length > 0 && children.every((c) => next.has(String(c[props.nodeKey]))))
    parentCode = parent[props.parentKey]
  }

  isBulkUpdating.value = false

  // 与实际生效状态（含级联产生的子孙/祖先）对齐后再计算差异
  // diffKeys(from, to) 返回「在 to 中但不在 from 中」的项
  const removed = diffKeys(next, before)
  const added = diffKeys(before, next)

  // 只对差集做增删，保留搜索过滤期间不可见但仍处于已勾选逻辑中的项
  const nextChecked = new Set(checkedKeys.value)
  for (const k of added) nextChecked.add(k)
  for (const k of removed) nextChecked.delete(k)

  checkedKeys.value = nextChecked
  // 同步快照（= 本次生效后的 store 集合），避免下次 diff 误判级联变更
  prevSelectionKeys.value = new Set(next)

  emitCheckChange(removed, added)
}

/** el-table selection 变化 → 快照 diff 计算 added/removed */
function handleSelectionChange(selection: TreeNode[]) {
  if (isBulkUpdating.value) return

  const currKeys = new Set(selection.map((r) => String(r[props.nodeKey])))
  const prevKeys = prevSelectionKeys.value

  const added = diffKeys(prevKeys, currKeys)
  const removed = diffKeys(currKeys, prevKeys)
  if (added.length === 0 && removed.length === 0) return

  if (props.cascade) {
    //
    // 级联：由触发节点算出最终勾选集合，再与变更前快照比对（含触发节点自身）
    // checkStrictly=true 下 el-table 一次只会翻转「被点击的那一行」，
    // 因此 added/removed 中恰好有且仅有一个 key —— 它就是触发节点。
    // 若同一次事件里出现多个变更（异常/批量 API），则不做级联，直接按 diff 落库，
    // 避免「猜错触发节点」导致整棵子树被错误勾选/取消。
    //
    const diffCount = added.length + removed.length
    const triggerKey = diffCount === 1 ? (added[0] ?? removed[0]) : undefined
    const triggerNode = triggerKey ? allNodeMap.value.get(triggerKey) : undefined
    if (triggerNode) {
      emitCascadeChange(triggerNode, added.length > 0, prevKeys)
      return
    }
  }

  // 非级联：以本次 diff 为准。
  // ⚠️ 只对差集做增删，不能整体替换为 currKeys ——
  // 搜索过滤期间不可见的已勾选项不在 store 里，整体替换会把它们丢掉。
  prevSelectionKeys.value = currKeys

  const nextChecked = new Set(checkedKeys.value)
  for (const k of added) nextChecked.add(k)
  for (const k of removed) nextChecked.delete(k)
  checkedKeys.value = nextChecked

  if (added.length > 0) emitCheckChange([], added)
  if (removed.length > 0) emitCheckChange(removed, [])
}

function emitCheckChange(removed: string[], added: string[]) {
  emit('check-change', { added, removed })
}

// ========================================================
// 公开方法
// ========================================================

function getCheckedKeys(): string[] {
  return Array.from(checkedKeys.value)
}

function setCheckedKeys(keys: string[]) {
  checkedKeys.value = new Set(keys)
  nextTick(() => {
    syncTableCheckState()
  })
}

function getCheckedNodes(): TreeNode[] {
  const result: TreeNode[] = []
  for (const key of checkedKeys.value) {
    const node = allNodeMap.value.get(key)
    if (node) {
      result.push(node)
    }
  }
  return result
}

defineExpose({
  getCheckedKeys,
  setCheckedKeys,
  getCheckedNodes,
  expandAll: handleExpandAll,
  collapseAll: handleCollapseAll,
  checkAll: handleCheckAll,
  uncheckAll: handleUncheckAll,
})
</script>

<style scoped>
.yzh-tree-table-check-selector {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

.yzh-tree-table-check-selector__toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fff;
  flex-shrink: 0;
}

.yzh-tree-table-check-selector__selection-info {
  font-size: 14px;
  color: var(--el-text-color-regular);
}

.yzh-tree-table-check-selector__toolbar-actions {
  display: flex;
  gap: 8px;
}

.yzh-tree-table-check-selector__search {
  width: 240px;
}

.yzh-tree-table-check-selector__table {
  flex: 1;
  overflow: auto;
}
</style>
