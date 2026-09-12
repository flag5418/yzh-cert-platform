<script setup lang="ts">
/**
 * ISO 标准 → 条款管理（左树右表，配置驱动 TreeTable 架构）
 *
 * 布局结构：
 * - 左侧：标准树（扁平，所有标准为根节点，含搜索、增删改）
 * - 右侧：条款表格（分页、搜索、增删改、显示已禁用开关）
 *
 * 配置驱动：
 * - 表格列从 logic.columns 自动获取（Foundation/ISOClause.json）
 * - 表单字段从 logic.formFields 自动获取
 * - 树节点表单从 logic.treeFormFields 自动获取
 * - toolbar 按钮根据配置动态渲染
 */
import { Delete, Plus, RefreshRight } from '@element-plus/icons-vue'
import { YzhForm, YzhTreeTable, YzhTable, type TreeNode } from '@yzh-core'
import {
  ElButton,
  ElMessage,
  ElMessageBox,
  ElSwitch,
} from 'element-plus'
import { computed, onMounted, nextTick, ref } from 'vue'
import { ISOStandardTreeTableLogic } from './logic'

// 实例化 Logic
const logic = new ISOStandardTreeTableLogic()

// 本地状态
const treeTableRef = ref()
const tableRef = ref()
const selectedRows = ref<any[]>([])

// 行操作按钮
const rowActionButtons = computed(() => {
  return logic.rowActionButtons
})

// ========================================================
// 树节点操作
// ========================================================

/** 树节点点击 → 加载该标准下条款 */
async function handleNodeClick(node: TreeNode) {
  await logic.onNodeClick(node)
  tableRef.value?.refresh()
}

/** 树节点自定义操作（编辑/删除） */
async function handleTreeNodeAction(action: string, node: TreeNode) {
  if (action === 'edit') {
    handleEditStd(node)
  } else if (action === 'delete') {
    await handleDeleteStd(node)
  }
}

/** 新增标准 */
function handleAddStd() {
  logic.openAddStdDialog()
}

/** 编辑标准 */
function handleEditStd(node: TreeNode) {
  logic.openEditStdDialog(node)
}

/** 删除标准 */
async function handleDeleteStd(node: TreeNode) {
  await ElMessageBox.confirm(
    `确定删除标准【${node.name}】？`,
    '删除确认',
    {
      type: 'warning',
      confirmButtonText: '确定删除',
      cancelButtonText: '取消',
    },
  )
  await logic.deleteStd(node)
  ElMessage.success('已删除标准')
}

// ========================================================
// 条款操作
// ========================================================

/** 新增条款 */
function handleAddClause() {
  const ok = logic.openAddClauseDialog()
  if (!ok) {
    ElMessage.warning('请先选择标准')
  }
}

/** 编辑条款 */
function handleEditClause(row: any) {
  logic.openEditClauseDialog(row)
}

/** 删除条款 */
async function handleDeleteClause(row: any) {
  await ElMessageBox.confirm(
    `确定删除条款【${row.ClauseNumber} ${row.Title}】？`,
    '删除确认',
    { type: 'warning' },
  )
  await logic.deleteClause(row)
  ElMessage.success('删除成功')
}

/** 批量删除条款 */
async function handleBatchDelete() {
  if (selectedRows.value.length === 0) {
    ElMessage.warning('请先选择要删除的条款')
    return
  }
  await ElMessageBox.confirm(
    `确定删除选中的 ${selectedRows.value.length} 个条款？`,
    '批量删除',
    { type: 'warning' },
  )
  await logic.batchDeleteClauses(selectedRows.value)
  selectedRows.value = []
  ElMessage.success('批量删除成功')
}

/** 切换条款有效标志 */
async function handleToggleClauseIsValid(row: any) {
  await (logic as any).toggleRowIsValidWithConfirm(row, { entityName: `${row.ClauseNumber} ${row.Title}` })
}

// ========================================================
// 表格数据加载
// ========================================================

async function loadTableData(params: any) {
  const filters: Array<{ Field: string; Operator: string; Value: any }> = []
  if (logic.selectedNode.value) {
    filters.push({
      Field: logic.relateField,
      Operator: 'eq',
      Value: logic.selectedNode.value.code,
    })
  }
  if (logic.showDisabled.value) {
    filters.push({ Field: 'ShowDisabled', Value: 'true', Operator: 'eq' })
  }
  try {
    const res = await logic.apiPost('/filter', {
      Page: params.page,
      PageSize: params.rows,
      SortField: params.sort,
      SortOrder: params.order,
      Filters: filters,
    })
    if (res.data) {
      return { rows: res.data.Items ?? [], total: res.data.TotalCount ?? 0 }
    }
  } catch (e: any) {
    ElMessage.error(e.message || '数据加载失败')
  }
  return { rows: [], total: 0 }
}

// ========================================================
// 提交弹窗
// ========================================================

async function handleClauseSubmit() {
  try {
    await logic.submitClauseForm()
    ElMessage.success(
      logic.dialogMode.value === 'add' ? '新增成功' : '修改成功',
    )
  } catch (e: any) {
    ElMessage.error(e.message || '保存失败')
  }
}

async function handleStdSubmit() {
  try {
    await logic.submitStdForm()
  } catch (e: any) {
    ElMessage.error(e.message || '保存失败')
  }
}

// ========================================================
// 初始化
// ========================================================

onMounted(async () => {
  await logic.init()
  await nextTick()
  // 注入组件引用，使基类 addTreeNode/updateTreeNode/deleteTreeNode 等可局部刷新
  logic.setTreeTableRef(treeTableRef.value)
  logic.setTableRef(tableRef.value)
})
</script>

<template>
  <div class="iso-page">
    <YzhTreeTable
      ref="treeTableRef"
      :tree-data="logic.treeData.value"
      :tree-width="320"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :node-actions="logic.nodeActions"
      :get-action-label="(action: string, node: TreeNode) => logic.getNodeActionLabel(action, node)"
      @tree-node-click="handleNodeClick"
      @tree-node-action="handleTreeNodeAction"
    >
      <!-- 树底部：新增标准按钮 -->
      <template #treeFooter>
        <el-button type="primary" :icon="Plus" @click="handleAddStd" style="width: 100%;">
          新增标准
        </el-button>
      </template>

      <template #default>
        <div class="iso-page__content">
          <!-- 条款表格 -->
          <YzhTable
            ref="tableRef"
            :columns="logic.columns as any"
            :data-loader="loadTableData"
            :search-fields="logic.searchFields as any"
            :selectable="true"
            :row-action-buttons="rowActionButtons"
            @selection-change="selectedRows = $event"
          >
            <!-- 工具栏左侧：操作按钮 -->
            <template #toolbar-left>
              <el-button type="primary" :icon="Plus" @click="handleAddClause"
                >新增条款</el-button
              >
              <el-button type="danger" :icon="Delete" @click="handleBatchDelete"
                >批量删除</el-button
              >
              <el-button :icon="RefreshRight" @click="logic.refreshTable()"
                >刷新</el-button
              >
            </template>

            <!-- 工具栏右侧：显示已禁用开关 -->
            <template #toolbar-right>
              <div class="toolbar-switch">
                <span class="toolbar-switch__label">显示已禁用</span>
                <el-switch
                  :model-value="logic.showDisabled.value"
                  @change="logic.toggleShowDisabled()"
                />
              </div>
            </template>
          </YzhTable>
        </div>
      </template>
    </YzhTreeTable>

    <!-- 条款新增/编辑弹窗 -->
    <el-dialog
      v-model="logic.dialogVisible.value"
      :title="logic.dialogMode.value === 'add' ? '新增条款' : '编辑条款'"
      width="600px"
      :close-on-click-modal="false"
      destroy-on-close
    >
      <YzhForm
        v-model="logic.formData"
        :fields="logic.formFields as any"
        :loading="logic.submitting.value"
        :cols="logic.formLayoutCols as any"
        @submit="handleClauseSubmit"
        @reset="logic.dialogVisible.value = false"
      />
    </el-dialog>

    <!-- 标准新增/编辑弹窗 -->
    <el-dialog
      v-model="logic.stdDialogVisible.value"
      :title="logic.stdDialogMode.value === 'add' ? '新增标准' : '编辑标准'"
      width="700px"
      :close-on-click-modal="false"
      destroy-on-close
    >
      <!-- 配置驱动的表单（字段从 ISOStandardForm.json 自动派生） -->
      <YzhForm
        v-model="logic.stdFormData"
        :fields="logic.treeFormFields as any"
        :loading="logic.stdSubmitting.value"
        :cols="2"
        label-width="100px"
        @submit="handleStdSubmit"
        @reset="logic.stdDialogVisible.value = false"
      />
    </el-dialog>
  </div>
</template>

<style scoped>
.iso-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.iso-page__content {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: #fff;
  overflow: hidden;
}

.toolbar-switch {
  display: flex;
  align-items: center;
  gap: 8px;
}

.toolbar-switch__label {
  font-size: 13px;
  color: var(--el-text-color-regular);
}
</style>
