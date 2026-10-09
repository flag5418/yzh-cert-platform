<script setup lang="ts">
/**
 * ISO 标准 → 条款管理（左树右表，配置驱动 TreeTable 架构）
 *
 * ★ 三层体系裁决（2026-10-08）：
 * - 左树：**全只读**（类别/标准均不可增删改）
 * - 右表：仅当选中「标准」节点时加载条款树；否则显示空提示
 * - 条款操作（增/删/改）仍可用 —— 但只作用于已选中的标准
 *
 * 注：标准本身的增删改在「标准管理」/cert/standard-manage 独立页面完成。
 *
 * ★ 选择判定唯一入口 = `logic.isSelectableNode()`（点击守卫 + 空态 v-if 共用）。
 *   2026-10-09 事故：两处曾各写一遍 `Extra._level !== 2`，而数据源根本没有 `_level`
 *   键（只有 `level`，值 0/1）⇒ 守卫恒拦、右表永不挂载、条款接口一次不发。
 */
import { Delete, Plus, RefreshRight } from '@element-plus/icons-vue'
import { confirmOrFalse, YzhForm, YzhEmptyState, YzhStatusBadge, resolveStatusBadge, YzhTreeTableLayout, YzhTreeTable, type TreeNode } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, onMounted, ref } from 'vue'
import { ISOStandardTreeTableLogic } from './logic'

// 静默第三方库 ResizeObserver polyfill 的 startTime 错误（VM809:2）
window.addEventListener('error', (event) => {
  if (event.message?.includes("Cannot read properties of undefined (reading 'startTime')")) {
    event.preventDefault()
    return true
  }
})

const logic = new ISOStandardTreeTableLogic()

const treeTableRef = ref()
const tableRef = ref()
const selectedRows = ref<any[]>([])

/**
 * 函数式 ref：挂载/卸载时同步给内核。
 *
 * ★ 必要性：右表在 `v-if` 分支里 —— 首次选中标准前**根本不渲染**，
 *   `onMounted + nextTick` 一次性注册拿到的恒是 `undefined`，
 *   此后 `refresh()` / 启停 / 显示已禁用 / 提交后刷新全走 `_tableRef?.refresh()`
 *   静默 no-op（2026-10-09 与守卫 bug 一并修复）。范式同 `useTreeTable` / standard-manage。
 */
function setTreeTableRef(el: any): void {
  treeTableRef.value = el
  logic.setTreeTableRef(el)
}

function setTableRef(el: any): void {
  tableRef.value = el
  logic.setTableRef(el)
}

const rowActionButtons = computed(() => logic.rowActions)

const dialogTitle = computed(() =>
  logic.dialogMode.value === 'add' ? '新增条款' : '编辑条款',
)

// ========================================================
// 树节点点击：仅标准节点触发条款加载（判定唯一入口 = isSelectableNode）
// ========================================================

async function handleNodeClick(node: TreeNode) {
  if (!logic.isSelectableNode(node)) {
    // 点击类别（虚拟）节点：清空选择，右侧回到空提示
    logic.selectedNode = null
    return
  }
  await logic.onNodeClick(node)
}

// ========================================================
// 条款操作
// ========================================================

async function handleAddClause() {
  const ok = await logic.openAddClauseDialog()
  if (!ok) ElMessage.warning('请先选择标准')
}

async function handleEditClause(row: any) {
  await logic.openEditClauseDialog(row)
}

async function handleDeleteClause(row: any) {
  const ok = await confirmOrFalse(
    `确定删除条款【${row.ClauseNumber} ${row.Title}】？`,
    '删除确认',
    { type: 'warning' },
  )
  if (!ok) return
  await logic.deleteClause(row)
  ElMessage.success('删除成功')
}

async function handleRowAction(action: string, row: any) {
  if (action === 'add-child') {
    const ok = await logic.openAddClauseChild(row)
    if (!ok) ElMessage.warning('请先选择标准')
  } else if (action === 'edit') {
    await handleEditClause(row)
  } else if (action === 'delete') {
    await handleDeleteClause(row)
  } else if (action === 'toggle-valid') {
    await handleToggleClauseIsValid(row)
  }
}

async function handleBatchDelete() {
  if (selectedRows.value.length === 0) {
    ElMessage.warning('请先选择要删除的条款')
    return
  }
  const ok = await confirmOrFalse(
    `确定删除选中的 ${selectedRows.value.length} 个条款？`,
    '批量删除',
    { type: 'warning' },
  )
  if (!ok) return
  await logic.batchDeleteClauses(selectedRows.value)
  selectedRows.value = []
  tableRef.value?.clearSelection?.()
  ElMessage.success('批量删除成功')
}

async function handleToggleClauseIsValid(row: any) {
  await (logic as any).toggleRowIsValidWithConfirm(row, {
    entityName: `${row.ClauseNumber} ${row.Title}`,
  })
  await logic.refresh()
}

async function handleClauseSubmit() {
  try {
    await logic.submitClauseForm()
    ElMessage.success(logic.dialogMode.value === 'add' ? '新增成功' : '修改成功')
  } catch (e: any) {
    ElMessage.error(e.message || '保存失败')
  }
}

function onClauseDialogOpened() { /* 弹窗完全打开后初始化 */ }
function onClauseDialogClosed() { /* 弹窗完全关闭后清理 */ }

onMounted(async () => {
  // refs 由模板函数式 ref 在子组件挂载时注入（早于本 onMounted），
  // 这里只做 init —— init 末尾若自动选中节点，right table refresh 才拿得到引用。
  await logic.init()
})
</script>

<template>
  <div class="iso-page">
    <YzhTreeTableLayout
      :ref="setTreeTableRef"
      :tree-data="logic.treeData"
      :tree-width="320"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :tree-default-expand-all="true"
      :node-actions="logic.nodeActions"
      :get-action-label="(action: string, node: TreeNode) => logic.getNodeActionLabel(action, node)"
      @tree-node-click="handleNodeClick"
    >
      <!-- 树底部：无操作按钮（左树全只读） -->

      <template #default>
        <div class="iso-page__content">
          <!-- 未选中标准时：空提示（判定与点击守卫共用 isSelectableNode） -->
          <div v-if="!logic.isSelectableNode(logic.selectedNode)" class="iso-page__empty">
            <YzhEmptyState title="未选择标准" description="请从左侧选择标准节点以查看和编辑条款" />
          </div>

          <!-- 已选中标准：条款树形表格 -->
          <YzhTreeTable
            v-else
            :ref="setTableRef"
            :columns="logic.columns as any"
            :data-loader="logic.dataLoader.bind(logic)"
            :search-fields="logic.searchFields as any"
            :selectable="true"
            :row-action-buttons="rowActionButtons"
            select-mode="multiple"
            row-key="Code"
            :allow-add-child="true"
            @selection-change="selectedRows = $event"
            @row-action="handleRowAction"
          >
            <!-- 状态列 -->
            <template #column-IsValid="{ row }">
              <YzhStatusBadge
                v-if="resolveStatusBadge(row, 'IsValid')"
                :type="resolveStatusBadge(row, 'IsValid')?.type"
                :text="resolveStatusBadge(row, 'IsValid')?.text"
                size="small"
              />
            </template>

            <!-- 工具栏左侧 -->
            <template #toolbar-left>
              <el-button type="primary" :icon="Plus" @click="handleAddClause">
                新增顶级条款
              </el-button>
              <el-button type="danger" :icon="Delete" @click="handleBatchDelete">
                批量删除
              </el-button>
              <el-button :icon="RefreshRight" @click="logic.refresh()">刷新</el-button>
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
          </YzhTreeTable>
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- 条款新增/编辑弹窗 -->
    <el-dialog
      v-model="logic.dialogVisible.value"
      :title="dialogTitle"
      width="600px"
      :close-on-click-modal="false"
      :destroy-on-close="false"
      @opened="onClauseDialogOpened"
      @closed="onClauseDialogClosed"
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
  background: var(--yzh-color-bg-container, #fff);
  overflow: hidden;
}

.iso-page__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  flex: 1;
  min-height: 200px;
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
