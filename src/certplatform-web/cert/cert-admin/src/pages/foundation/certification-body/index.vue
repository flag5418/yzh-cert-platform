<script setup lang="ts">
/**
 * 认证机构管理（配置驱动 CRUD 页面）
 *
 * 唯一差异在后端：新增/修改/删除/启停时会同步 Sys_Organization 机构记录
 */
import { YzhForm, YzhTable } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, onMounted, nextTick, ref } from 'vue'
import { CertificationBodyLogic } from './logic'

const logic = new CertificationBodyLogic()
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
  } else if (action === 'toggleValid') {
    const newIsValid = row.IsValid === 1 ? 0 : 1
    logic.updateRow({ ...row, IsValid: newIsValid })
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
  <div class="cert-body-page">
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
      <template #toolbar-left>
        <el-button v-if="toolbarConfig.Add !== false" type="primary" @click="handleAdd">新增</el-button>
        <el-button v-if="toolbarConfig.Delete !== false" type="danger" @click="handleBatchDelete">删除</el-button>
      </template>
    </YzhTable>

    <el-dialog
      v-model="logic.dialogVisible.value"
      :title="logic.dialogMode.value === 'add' ? '新增认证机构' : '编辑认证机构'"
      width="760px"
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
.cert-body-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}
</style>
