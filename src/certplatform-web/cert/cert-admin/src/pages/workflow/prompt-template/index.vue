<script setup lang="ts">
/**
 * 提示词模板管理（配置驱动 CRUD 页面）
 */
import { YzhForm, YzhTable } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, onMounted, nextTick, ref } from 'vue'
import { PromptTemplateLogic } from './logic'

const logic = new PromptTemplateLogic()
const tableRef = ref()

const rowActionButtons = computed(() => logic.rowActionButtons)
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
  <div class="prompt-template-page">
    <YzhTable
      ref="tableRef"
      :columns="logic.columns as any"
      :data-loader="loadTableData"
      :search-fields="logic.searchFields as any"
      :selectable="true"
      :row-action-buttons="rowActionButtons"
      row-key="Id"
      @selection-change="logic.onSelectionChange($event)"
      @row-action="handleRowAction"
    >
      <template #toolbar-left>
        <el-button v-if="toolbarConfig.Add !== false" type="primary" @click="handleAdd">新增提示词</el-button>
        <el-button v-if="toolbarConfig.Delete !== false" type="danger" @click="handleBatchDelete">删除</el-button>
      </template>
    </YzhTable>

    <el-dialog
      v-model="logic.dialogVisible.value"
      :title="logic.dialogMode.value === 'add' ? '新增提示词模板' : '编辑提示词模板'"
      width="750px"
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
.prompt-template-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}
</style>
