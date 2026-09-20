<script setup lang="ts">
/**
 * 阶段定义管理（配置驱动 CRUD 页面）
 */
import { YzhForm, YzhTable } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, onMounted, nextTick, ref } from 'vue'
import { PhaseDefinitionLogic } from './logic'

const logic = new PhaseDefinitionLogic()
const tableRef = ref()

// 行操作按钮（基类自动注入 toggle-valid）
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

async function handleBatchDelete() {
  await logic.confirmDelete()
}

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

onMounted(async () => {
  await logic.init()
  await nextTick()
  logic.setTableRef(tableRef.value)
})
</script>

<template>
  <div class="phase-definition-page">
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

      <template #toolbar-left>
        <el-button v-if="toolbarConfig.Add !== false" type="primary" @click="handleAdd">新增</el-button>
        <el-button v-if="toolbarConfig.Delete !== false" type="danger" @click="handleBatchDelete">删除</el-button>
      </template>
    </YzhTable>

    <el-dialog
      v-model="logic.dialogVisible.value"
      :title="logic.dialogMode.value === 'add' ? '新增阶段定义' : '编辑阶段定义'"
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
.phase-definition-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}
</style>
