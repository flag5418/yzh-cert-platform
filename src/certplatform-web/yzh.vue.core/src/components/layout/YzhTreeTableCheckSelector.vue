<template>
  <div class="yzh-tree-table-check-selector">
    <!-- 工具栏 -->
    <div class="yzh-tree-table-check-selector__toolbar">
      <div class="yzh-tree-table-check-selector__selection-info">
        已选择 <strong>{{ checkedCount }}</strong> 条记录
      </div>
      <div class="yzh-tree-table-check-selector__toolbar-actions">
        <el-button size="small" @click="handleExpandAll">展开全部</el-button>
        <el-button size="small" @click="handleCollapseAll">折叠全部</el-button>
        <el-button size="small" @click="handleCheckAll">全选</el-button>
        <el-button size="small" @click="handleUncheckAll">取消全选</el-button>
      </div>
    </div>

    <!-- 树形表格 -->
    <el-table
      ref="tableRef"
      :data="treeData"
      :row-key="nodeKey"
      :tree-props="{ children: 'children', hasChildren: 'hasChildren' }"
      :default-expand-all="false"
      :expand-on-click-node="false"
      :check-on-click-node="false"
      style="width: 100%"
      class="yzh-tree-table-check-selector__table"
      @check-change="handleCheckChange"
      @select="handleSelect"
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
            <!-- 节点类型标签 -->
            <template v-if="col.prop === nodeTypeField">
              <el-tag v-if="row[nodeTypeField] === 'org'" type="primary" size="small">机构</el-tag>
              <el-tag v-else-if="row[nodeTypeField] === 'user'" type="success" size="small">用户</el-tag>
              <el-tag v-else size="small">{{ row[nodeTypeField] }}</el-tag>
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
 * - 支持 checkbox 勾选（checkStrictly: true，父子独立）
 * - 勾选/取消立即触发 check-change 事件（auto-save 模式）
 * - 支持展开/折叠、全选/取消全选
 *
 * 典型场景：
 * - 角色-用户关联（机构+用户混合树，勾选用户）
 * - 角色-菜单关联（菜单树，勾选菜单）
 */
import { ref, computed, watch, nextTick } from 'vue'
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
}

const props = withDefaults(defineProps<Props>(), {
  nodeKey: 'Code',
  parentKey: 'ParentCode',
  nodeTypeField: 'NodeType',
  checkField: 'CheckFlag',
  columns: () => [],
  showTypeColumn: true,
  defaultExpandAll: false,
})

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
const checkedKeys = ref<Set<string>>(new Set())
const allNodeMap = ref<Map<string, TreeNode>>(new Map())
const expandedKeys = ref<Set<string>>(new Set())
const isBulkUpdating = ref(false)

// ========================================================
// 计算属性
// ========================================================

const checkedCount = computed(() => checkedKeys.value.size)

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

  // 清空所有勾选
  tableRef.value.clearSelection()

  // 设置勾选状态
  for (const key of checkedKeys.value) {
    const node = allNodeMap.value.get(key)
    if (node) {
      tableRef.value.toggleRowSelection(node, true)
    }
  }
}

// ========================================================
// 监听数据变化
// ========================================================

watch(
  () => props.flatData,
  (newData) => {
    if (!newData || newData.length === 0) {
      treeData.value = []
      checkedKeys.value.clear()
      allNodeMap.value.clear()
      return
    }

    // 转换为嵌套树
    treeData.value = flatToTree(newData)

    // 初始化勾选状态
    initCheckedState(treeData.value)

    // 展开所有节点（如果需要）
    if (props.defaultExpandAll) {
      expandedKeys.value.clear()
      expandAllNodes(treeData.value)
    }

    // 同步表格勾选状态
    nextTick(() => {
      syncTableCheckState()
    })
  },
  { immediate: true },
)

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
  // 通过设置 data 触发重新渲染
  const data = treeData.value
  treeData.value = []
  nextTick(() => {
    treeData.value = data
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

  isBulkUpdating.value = false

  // 一次性触发批量事件
  emitCheckChange([], userNodes.map((n) => n[props.nodeKey]))
}

function handleUncheckAll() {
  if (!tableRef.value) return

  isBulkUpdating.value = true

  const removedKeys = Array.from(checkedKeys.value)
  checkedKeys.value = new Set()
  tableRef.value.clearSelection()

  isBulkUpdating.value = false

  // 一次性触发批量事件
  emitCheckChange(removedKeys, [])
}

function collectUserNodes(nodes: TreeNode[]): TreeNode[] {
  const result: TreeNode[] = []
  for (const node of nodes) {
    if (node[props.nodeTypeField] !== 'org') {
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

function handleCheckChange(row: TreeNode, checked: boolean) {
  if (isBulkUpdating.value) return

  const key = row[props.nodeKey]

  if (checked) {
    const newSet = new Set(checkedKeys.value)
    newSet.add(key)
    checkedKeys.value = newSet
  } else {
    const newSet = new Set(checkedKeys.value)
    newSet.delete(key)
    checkedKeys.value = newSet
  }

  // 触发事件
  if (checked) {
    emitCheckChange([], [key])
  } else {
    emitCheckChange([key], [])
  }
}

function handleSelect(selection: TreeNode[], row: TreeNode) {
  if (isBulkUpdating.value) return

  // 处理 select 事件（el-table 的 checkbox 点击）
  const key = row[props.nodeKey]
  const isSelected = selection.some((s) => s[props.nodeKey] === key)

  if (isSelected) {
    const newSet = new Set(checkedKeys.value)
    newSet.add(key)
    checkedKeys.value = newSet
    emitCheckChange([], [key])
  } else {
    const newSet = new Set(checkedKeys.value)
    newSet.delete(key)
    checkedKeys.value = newSet
    emitCheckChange([key], [])
  }
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

.yzh-tree-table-check-selector__table {
  flex: 1;
  overflow: auto;
}
</style>
