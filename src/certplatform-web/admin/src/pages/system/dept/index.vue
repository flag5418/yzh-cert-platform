<script setup lang="ts">
/**
 * SysDept 页面 - 左树右表布局
 *
 * 布局结构：
 * - 左侧：树搜索 + 树节点
 * - 右侧：工具栏（新建/删除/刷新 + 列设置）+ 表格 + 分页
 */
import { onMounted, ref } from 'vue'
import { YzhTreeTable } from '@yzh-core/components/layout'
import { YzhTable } from '@yzh-core/components/table'
import type { YzhTableColumn, SearchField } from '@yzh-core/components/table/types'
import { ElButton, ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Delete, RefreshRight, Upload, Download } from '@element-plus/icons-vue'
import DeptTreeLogic from './logic'
import DeptFormDialog from './DeptFormDialog.vue'
import type { TreeNode } from '@share/types/tree'
import type { SysDept } from '@share/api/system-dept'

// 实例化 Logic
const logic = new DeptTreeLogic()

// 表格列定义
const columns: YzhTableColumn[] = [
  { prop: 'departmentName', label: '部门名称', minWidth: 180 },
  { prop: 'departmentCode', label: '部门编码', width: 150 },
  { prop: 'departmentType', label: '部门类型', width: 120 },
  {
    prop: 'enable',
    label: '状态',
    width: 80,
    slot: true
  },
  { prop: 'remark', label: '备注', minWidth: 200 },
  { prop: 'actions', label: '操作', width: 140, fixed: 'right', slot: true }
]

// 搜索字段
const searchFields: SearchField[] = [
  { prop: 'departmentName', label: '部门名称', type: 'text' }
]

// 选中的表格行
const selectedRows = ref<SysDept[]>([])

// 表格数据加载器
async function loadTableData(_params: any) {
  if (logic.selectedNode.value) {
    await logic.loadDataWithTreeCondition(logic.selectedNode.value)
  } else {
    await logic.loadData()
  }
  return { rows: logic.rows.value, total: logic.pagination.total }
}

// 树节点点击
async function handleNodeClick(node: TreeNode) {
  await logic.onNodeClick(node)
}

// 表格选择变更
function handleSelectionChange(rows: SysDept[]) {
  selectedRows.value = rows
}

// ──── 行操作 ────

// 行编辑
function handleRowEdit(row: SysDept) {
  const node = logic.findNodeByCode(row.departmentId)
  if (node) {
    logic.openEditDialog(node)
  }
}

// 行删除
async function handleRowDelete(row: SysDept) {
  const node = logic.findNodeByCode(row.departmentId)
  if (node) {
    await logic.handleDelete(node)
    ElMessage.success('删除成功')
  }
}

// ──── 树操作 ────

// 新建（统一入口：根节点或子节点）
function handleAdd() {
  const parentNode = logic.selectedNode.value || null
  logic.openAddDialog(parentNode)
}

// 刷新树+表格
async function handleRefresh() {
  await logic.initTree()
  await logic.refreshTable()
  ElMessage.success('刷新成功')
}

// ──── 表格批量操作 ────

// 批量删除（选中行）
async function handleBatchDelete() {
  if (selectedRows.value.length === 0) {
    ElMessage.warning('请先选择要删除的行')
    return
  }
  const nodes = selectedRows.value
    .map(row => logic.findNodeByCode(row.departmentId))
    .filter(Boolean) as TreeNode<SysDept>[]

  if (nodes.length === 0) {
    selectedRows.value = []
    return
  }

  await ElMessageBox.confirm(`确定删除选中的 ${nodes.length} 个部门吗？`, '批量删除', {
    type: 'warning'
  })
  await logic.handleBatchDelete(nodes)
  selectedRows.value = []
  ElMessage.success('批量删除成功')
}

// ──── 弹窗提交 ────

// 提交新增
async function handleFormAdd() {
  await logic.handleAdd()
  ElMessage.success('新增成功')
}

// 提交编辑
async function handleFormEdit() {
  await logic.handleEdit()
  ElMessage.success('修改成功')
}

// ──── 导入导出 ────

// 导入
function handleImport() {
  // TODO: 实现导入逻辑
  ElMessage.info('导入功能开发中')
}

// 导出
function handleExport() {
  // TODO: 实现导出逻辑
  ElMessage.info('导出功能开发中')
}

// 初始化
onMounted(async () => {
  await logic.init()
})
</script>

<template>
  <div class="dept-page">
    <!-- 左树右表主体 -->
    <YzhTreeTable
      :tree-data="logic.treeData.value"
      :tree-width="260"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="true"
      :tree-load-data="logic.loadChildren.bind(logic)"
      @tree-node-click="handleNodeClick"
    >
      <template #default>
        <!-- 右侧：完整单页面布局 -->
        <div class="dept-page__content">
          <!-- 数据表格 -->
          <YzhTable
            :columns="columns"
            :data-loader="loadTableData"
            :search-fields="searchFields"
            :selectable="true"
            :show-pagination="!logic.selectedNode.value"
            :no-padding="true"
            @selection-change="handleSelectionChange"
          >
            <!-- 状态列自定义渲染 -->
            <template #column-enable="{ value }">
              <el-tag :type="value === 1 ? 'success' : 'info'" size="small">
                {{ value === 1 ? '启用' : '禁用' }}
              </el-tag>
            </template>

            <!-- 操作列 -->
            <template #column-actions="{ row }">
              <el-button type="primary" link size="small" @click="handleRowEdit(row)">
                编辑
              </el-button>
              <el-button type="danger" link size="small" @click="handleRowDelete(row)">
                删除
              </el-button>
            </template>

            <!-- 工具栏左侧：操作按钮 -->
            <template #toolbar-left>
              <el-button type="primary" :icon="Plus" @click="handleAdd">新建</el-button>
              <el-button type="danger" :icon="Delete" @click="handleBatchDelete">批量删除</el-button>
              <el-button :icon="RefreshRight" @click="handleRefresh">刷新</el-button>
              <el-button :icon="Upload" @click="handleImport">导入</el-button>
              <el-button :icon="Download" @click="handleExport">导出</el-button>
            </template>
          </YzhTable>
        </div>
      </template>
    </YzhTreeTable>

    <!-- 新增弹窗 -->
    <DeptFormDialog
      v-model="logic.showAddDialog.value"
      mode="add"
      :form-data="logic.addForm"
      :parent-node="logic.parentNodeForAdd.value"
      @submit="handleFormAdd"
    />

    <!-- 编辑弹窗 -->
    <DeptFormDialog
      v-model="logic.showEditDialog.value"
      mode="edit"
      :form-data="logic.editForm"
      :parent-node="logic.editingNode.value"
      @submit="handleFormEdit"
    />
  </div>
</template>

<style scoped>
.dept-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

/* 右侧内容区 */
.dept-page__content {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: #fff;
  overflow: hidden;
}
</style>
