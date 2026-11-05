<script setup lang="ts">
/**
 * 条款管理（左树右表）
 *
 * - 左树：**体系（分类） → 族 → 标准** 三层，**全只读**（不可增删改）
 * - 右表：仅当选中「标准」节点时加载条款树；否则显示空提示
 * - 条款操作（增 / 删 / 改 / 批量删除 / 行内新增下级）仍可用 —— 但只作用于已选中的标准
 *
 * ★ 布局裁决（2026-11-05）：与 iso-standard（/cert/iso-standard）同构。
 *   注：标准的增删改在「标准管理」/cert/standard-manage 独立页面完成。
 *
 * ★ 选择判定唯一入口 = `logic.isSelectableNode()`（点击守卫 + 空态 v-if 共用）。
 */
import { Delete, Plus, RefreshRight } from '@element-plus/icons-vue'
import {
  YzhFormDialog,
  YzhEmptyState,
  YzhStatusBadge,
  resolveStatusBadge,
  YzhTreeTableLayout,
  YzhTreeTable,
  type TreeNode,
} from '@yzh-core'
import { computed, onMounted, ref } from 'vue'
import { ISOClauseLogic } from './logic'

// 静默第三方库 ResizeObserver polyfill 的 startTime 错误（VM809:2，同 iso-standard）
window.addEventListener('error', (event) => {
  if (event.message?.includes("Cannot read properties of undefined (reading 'startTime')")) {
    event.preventDefault()
    return true
  }
})

const logic = new ISOClauseLogic()

const treeTableRef = ref()
const tableRef = ref()

/**
 * 函数式 ref：挂载/卸载时同步给内核。
 *
 * ★ 必要性：右表在 `v-if` 分支里 —— 首次选中标准前**根本不渲染**，
 *   `onMounted + nextTick` 一次性注册拿到的恒是 `undefined`，
 *   此后 `refresh()` / 启停 / 显示已禁用 / 提交后刷新全走 `_tableRef?.refresh()`
 *   静默 no-op。范式同 `useTreeTable` / standard-manage / iso-standard。
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
    // 点击体系（虚拟）/族节点：清空选择，右侧回到空提示
    logic.selectedNode = null
    return
  }
  await logic.onNodeClick(node)
}

onMounted(async () => {
  // refs 由模板函数式 ref 在子组件挂载时注入（早于本 onMounted），
  // 这里只做 init —— init 末尾若自动选中节点，right table refresh 才拿得到引用。
  await logic.init()
})
</script>

<template>
  <div class="clause-page">
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
        <div class="clause-page__content">
          <!-- 未选中标准时：空提示（判定与点击守卫共用 isSelectableNode） -->
          <div v-if="!logic.isSelectableNode(logic.selectedNode)" class="clause-page__empty">
            <YzhEmptyState
              title="未选择标准"
              description="请从左侧选择标准节点以查看和编辑条款"
            />
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
            :children-field="'Children'"
            :allow-add-child="true"
            @selection-change="logic.onSelectionChange"
            @row-action="logic.onRowAction"
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
              <el-button type="primary" :icon="Plus" @click="logic.addClauseWithToast()">
                新增顶级条款
              </el-button>
              <el-button type="danger" :icon="Delete" @click="logic.batchDeleteClausesWithConfirm()">
                批量删除
              </el-button>
              <el-button type="default" :icon="RefreshRight" @click="logic.refresh()">
                刷新
              </el-button>
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
    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      :title="dialogTitle"
      width="600px"
      :fields="logic.formFields as any"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      :destroy-on-close="false"
      @submit="logic.submitClauseFormWithToast()"
    />
  </div>
</template>

<style scoped>
.clause-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.clause-page__content {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: var(--yzh-color-bg-container, #fff);
  overflow: hidden;
}

.clause-page__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  flex: 1;
  min-height: 200px;
}

.toolbar-switch {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.toolbar-switch__label {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-regular);
}
</style>
