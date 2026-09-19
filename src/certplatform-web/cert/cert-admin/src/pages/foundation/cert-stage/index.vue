<script setup lang="ts">
/**
 * 认证阶段管理（配置驱动 CRUD 页面）
 *
 * 基于 CrudPageLogic 实现标准 CRUD
 * 后端：CertStageController (YzhControllerBase<CertStage>)
 */
import { YzhForm, YzhTable } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, onMounted, nextTick, ref } from 'vue'
import { CertStageLogic } from './logic'

// 实例化 Logic
const logic = new CertStageLogic()

// 本地状态
const tableRef = ref()

// 行操作按钮（从 logic 派生）
const rowActionButtons = computed(() => logic.rowActionButtons)

// 工具栏配置
const toolbarConfig = computed(() => (logic.config.value as any)?.Toolbar || {})

// ── 分类字典选项 ──
const categoryOptions = [
  { value: 'process', label: '流程阶段' },
  { value: 'audit', label: '审核阶段' },
  { value: 'post-cert', label: '证后阶段' }
]

// ========================================================
// 表格数据加载
// ========================================================

function loadTableData(params: any) {
  return logic.dataLoader(params)
}

// ========================================================
// 操作事件
// ========================================================

/** 新增 */
function handleAdd() {
  logic.openAddDialog()
}

/** 批量删除 */
async function handleBatchDelete() {
  await logic.confirmDelete()
}

/** 行操作 */
async function handleRowAction(action: string, row: any) {
  if (action === 'edit') {
    logic.openEditDialog(row)
  } else if (action === 'toggleValid') {
    // 启用/禁用切换
    const newIsValid = row.IsValid === 1 ? 0 : 1
    logic.updateRow({ ...row, IsValid: newIsValid })
  } else if (action === 'delete') {
    await logic.confirmDelete([row])
  }
}

/** 提交表单 */
async function handleSubmit() {
  try {
    await logic.submitForm()
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
  logic.setTableRef(tableRef.value)
})
</script>

<template>
  <div class="cert-stage-page">
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
      <!-- 工具栏左侧 -->
      <template #toolbar-left>
        <el-button
          v-if="toolbarConfig.Add !== false"
          type="primary"
          @click="handleAdd"
          >新增</el-button
        >
        <el-button
          v-if="toolbarConfig.Delete !== false"
          type="danger"
          @click="handleBatchDelete"
          >删除</el-button
        >
      </template>
    </YzhTable>

    <!-- 新增/编辑弹窗 -->
    <el-dialog
      v-model="logic.dialogVisible.value"
      :title="logic.dialogMode.value === 'add' ? '新增认证阶段' : '编辑认证阶段'"
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
.cert-stage-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}
</style>
