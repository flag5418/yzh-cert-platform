<template>
  <div class="yzh-tree-table-selector">
    <!-- 左侧：树面板 -->
    <div class="yzh-tree-table-selector__tree-panel" :style="{ width: treeWidth + 'px' }">
      <!-- 树搜索 -->
      <div v-if="treeSearchable" class="yzh-tree-table-selector__tree-search">
        <el-input
          v-model="treeSearchKeyword"
          placeholder="搜索节点"
          clearable
          prefix-icon="Search"
          size="small"
        />
      </div>

      <!-- 树操作栏 -->
      <div class="yzh-tree-table-selector__tree-actions">
        <el-button size="small" @click="handleExpandAll">展开全部</el-button>
        <el-button size="small" @click="handleCollapseAll">折叠全部</el-button>
        <el-button size="small" @click="handleCheckAll">全选</el-button>
        <el-button size="small" @click="handleUncheckAll">取消全选</el-button>
      </div>

      <!-- 树组件 -->
      <YzhTree
        ref="treeRef"
        :data="filteredTreeData"
        :show-checkbox="true"
        :check-strictly="checkStrictly"
        :lazy="treeLazy"
        :load-data="treeLoadData"
        :default-expand-all="treeDefaultExpandAll"
        :node-key="nodeKey"
        @check-change="handleTreeCheckChange"
        @node-click="handleTreeNodeClick"
      />

      <!-- 树底部插槽 -->
      <div v-if="$slots.treeFooter" class="yzh-tree-table-selector__tree-footer">
        <slot name="treeFooter" />
      </div>
    </div>

    <!-- 右侧：表格面板 -->
    <div class="yzh-tree-table-selector__table-panel">
      <!-- 工具栏 -->
      <div class="yzh-tree-table-selector__table-toolbar">
        <div class="yzh-tree-table-selector__selection-info">
          已选择 <strong>{{ checkedTableRows.length }}</strong> 条记录
        </div>
        <el-button size="small" type="danger" @click="handleClearSelection" :disabled="checkedTableRows.length === 0">
          清空选择
        </el-button>
      </div>

      <!-- 表格 -->
      <YzhTable
        ref="tableRef"
        :columns="tableColumns"
        :data-loader="tableDataLoader"
        :selectable="true"
        :show-pagination="showPagination"
        :page-size="pageSize"
        :row-key="rowKey"
        @selection-change="handleTableSelectionChange"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * YzhTreeTableSelector - 树表选择器组件
 *
 * 功能：
 * - 左侧树（带 checkbox）+ 右侧表格（带 checkbox）
 * - 勾选树节点 → 自动加载关联表格数据并勾选
 * - 支持递归选择（勾选父节点时递归加载所有子孙节点的关联数据）
 * - 支持树搜索、展开/折叠、全选/取消全选
 *
 * 典型场景：
 * - 角色-用户关联（左=角色树，右=用户表格）
 * - 部门-员工关联（左=部门树，右=员工表格）
 * - 权限-菜单关联（左=权限树，右=菜单表格）
 */
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import YzhTree from './YzhTree.vue'
import YzhTable from '../table/YzhTable.vue'
import type { TreeNode } from '../../types/tree'
import type { YzhTableColumn } from '../table/types'

// ========================================================
// Props
// ========================================================

interface Props {
  /** 树数据 */
  treeData: TreeNode[]
  /** 树面板宽度 */
  treeWidth?: number
  /** 树可搜索 */
  treeSearchable?: boolean
  /** 树默认展开 */
  treeDefaultExpandAll?: boolean
  /** 树懒加载 */
  treeLazy?: boolean
  /** 树懒加载函数 */
  treeLoadData?: (node: any, resolve: (data: TreeNode[]) => void) => void
  /** 树节点 key 字段 */
  nodeKey?: string
  /** 树节点严格模式（父子不关联） */
  checkStrictly?: boolean
  /** 表格列配置 */
  tableColumns: YzhTableColumn[]
  /** 表格数据加载函数（参数：treeCode） */
  loadTableData: (treeCode: string) => Promise<{ rows: any[]; total: number }>
  /** 是否启用分页 */
  showPagination?: boolean
  /** 每页条数 */
  pageSize?: number
  /** 表格行 key 字段 */
  rowKey?: string
}

const props = withDefaults(defineProps<Props>(), {
  treeWidth: 260,
  treeSearchable: true,
  treeDefaultExpandAll: false,
  treeLazy: false,
  checkStrictly: true,
  showPagination: true,
  pageSize: 20,
  nodeKey: 'code',
  rowKey: 'Code',
})

// ========================================================
// Emits
// ========================================================

const emit = defineEmits<{
  (e: 'update:checkedTreeNodes', nodes: TreeNode[]): void
  (e: 'update:checkedTableRows', rows: any[]): void
  (e: 'tree-check-change', checkedNodes: TreeNode[]): void
  (e: 'selection-change', rows: any[]): void
}>()

// ========================================================
// 内部状态
// ========================================================

const treeRef = ref<InstanceType<typeof YzhTree>>()
const tableRef = ref<any>()
const treeSearchKeyword = ref('')
const checkedTreeNodes = ref<TreeNode[]>([])
const checkedTableRows = ref<any[]>([])
const loadedTableData = ref<Map<string, any[]>>(new Map())

// ========================================================
// 计算属性
// ========================================================

const filteredTreeData = computed(() => {
  if (!treeSearchKeyword.value) return props.treeData
  return filterTreeData(props.treeData, treeSearchKeyword.value)
})

// ========================================================
// 树操作
// ========================================================

function handleExpandAll() {
  treeRef.value?.expandAll()
}

function handleCollapseAll() {
  treeRef.value?.collapseAll()
}

function handleCheckAll() {
  // 递归勾选所有节点
  const checkAllNodes = (nodes: TreeNode[]) => {
    for (const node of nodes) {
      treeRef.value?.setChecked(node.code, true)
      if (node.children && node.children.length > 0) {
        checkAllNodes(node.children)
      }
    }
  }
  checkAllNodes(props.treeData)
}

function handleUncheckAll() {
  treeRef.value?.setCheckedNodes([])
}

function handleTreeNodeClick(node: TreeNode) {
  // 点击节点时加载该节点的关联表格数据
  loadTableDataForNode(node.code)
}

// ========================================================
// 树勾选事件
// ========================================================

async function handleTreeCheckChange() {
  if (!treeRef.value) return

  const checked = treeRef.value.getCheckedNodes() as unknown as TreeNode[]
  checkedTreeNodes.value = checked

  // 收集所有勾选节点的 code
  const checkedCodes = checked.map((n) => n.code)

  // 加载所有勾选节点的关联表格数据
  const allTableRows: any[] = []
  for (const code of checkedCodes) {
    const data = await loadTableDataForNode(code)
    if (data) {
      allTableRows.push(...data)
    }
  }

  // 去重（按 rowKey）
  const seen = new Set<string>()
  const uniqueRows = allTableRows.filter((row) => {
    const key = row[props.rowKey]
    if (seen.has(key)) return false
    seen.add(key)
    return true
  })

  checkedTableRows.value = uniqueRows

  // 设置表格勾选状态
  if (tableRef.value) {
    tableRef.value.setCheckedRows(
      (row: any) => uniqueRows.some((r) => r[props.rowKey] === row[props.rowKey]),
      true,
    )
  }

  emit('update:checkedTreeNodes', checked)
  emit('update:checkedTableRows', uniqueRows)
  emit('tree-check-change', checked)
}

// ========================================================
// 表格数据加载
// ========================================================

async function loadTableDataForNode(treeCode: string): Promise<any[] | null> {
  // 检查缓存
  if (loadedTableData.value.has(treeCode)) {
    return loadedTableData.value.get(treeCode)!
  }

  try {
    const result = await props.loadTableData(treeCode)
    const rows = result.rows ?? []
    loadedTableData.value.set(treeCode, rows)
    return rows
  } catch (e: any) {
    ElMessage.error(e.message || '加载表格数据失败')
    return null
  }
}

async function tableDataLoader(params: any) {
  // 如果没有勾选树节点，返回空数据
  if (checkedTreeNodes.value.length === 0) {
    return { rows: [], total: 0 }
  }

  // 加载所有勾选节点的关联数据
  const allRows: any[] = []
  for (const node of checkedTreeNodes.value) {
    const data = await loadTableDataForNode(node.code)
    if (data) {
      allRows.push(...data)
    }
  }

  // 去重
  const seen = new Set<string>()
  const uniqueRows = allRows.filter((row) => {
    const key = row[props.rowKey]
    if (seen.has(key)) return false
    seen.add(key)
    return true
  })

  // 手动分页
  const start = (params.page - 1) * params.rows
  const end = start + params.rows
  const pagedRows = uniqueRows.slice(start, end)

  return { rows: pagedRows, total: uniqueRows.length }
}

// ========================================================
// 表格选择事件
// ========================================================

function handleTableSelectionChange(rows: any[]) {
  checkedTableRows.value = rows
  emit('update:checkedTableRows', rows)
  emit('selection-change', rows)
}

function handleClearSelection() {
  // 清空表格选择
  tableRef.value?.clearSelection()
  // 清空树选择
  handleUncheckAll()
  checkedTreeNodes.value = []
  checkedTableRows.value = []
  loadedTableData.value.clear()
  emit('update:checkedTreeNodes', [])
  emit('update:checkedTableRows', [])
}

// ========================================================
// 辅助函数
// ========================================================

function filterTreeData(nodes: TreeNode[], keyword: string): TreeNode[] {
  const lower = keyword.toLowerCase()
  const result: TreeNode[] = []

  for (const node of nodes) {
    const matched = node.name.toLowerCase().includes(lower)
    const filteredChildren = filterTreeData(node.children, keyword)

    if (matched || filteredChildren.length > 0) {
      result.push({ ...node, children: filteredChildren })
    }
  }

  return result
}

// ========================================================
// 公开方法
// ========================================================

defineExpose({
  getCheckedTreeNodes: () => checkedTreeNodes.value,
  getCheckedTableRows: () => checkedTableRows.value,
  clearSelection: handleClearSelection,
  refreshTable: () => tableRef.value?.refresh(),
})
</script>

<style scoped>
.yzh-tree-table-selector {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

.yzh-tree-table-selector__tree-panel {
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--el-border-color-lighter);
  overflow: hidden;
}

.yzh-tree-table-selector__tree-search {
  padding: 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fff;
  flex-shrink: 0;
}

.yzh-tree-table-selector__tree-actions {
  padding: 8px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fff;
  flex-shrink: 0;
  display: flex;
  gap: 4px;
}

.yzh-tree-table-selector__tree-footer {
  padding: 12px;
  border-top: 1px solid var(--el-border-color-lighter);
  background: #fff;
  flex-shrink: 0;
}

.yzh-tree-table-selector__table-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  overflow: hidden;
}

.yzh-tree-table-selector__table-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fff;
  flex-shrink: 0;
}

.yzh-tree-table-selector__selection-info {
  font-size: 14px;
  color: var(--el-text-color-regular);
}
</style>
