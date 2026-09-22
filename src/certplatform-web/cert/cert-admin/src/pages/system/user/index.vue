<script setup lang="ts">
/**
 * 用户管理（配置驱动 CRUD 页面）
 *
 * useSingleTable 统一注入 logic/tableRef；按钮与动作全部由内核派发，
 * 页面无手写 handleAdd/handleBatchDelete/handleRowAction/handleSubmit。
 */
import { YzhFormDialog, YzhTable, useSingleTable } from '@yzh-core'
import { UserLogic } from './logic'

const { logic, tableRef } = useSingleTable(UserLogic)
</script>

<template>
  <div class="user-page">
    <YzhTable
      ref="tableRef"
      :columns="logic.columns"
      :data-loader="logic.dataLoader.bind(logic)"
      :search-fields="logic.searchFields"
      :toolbar-actions="logic.toolbarActions"
      :row-action-buttons="logic.rowActions"
      select-mode="multiple"
      @selection-change="logic.onSelectionChange($event)"
      @row-action="logic.onRowAction"
      @toolbar-action="logic.onToolbarAction"
    />

    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      entity-name="用户"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      @submit="logic.submitForm()"
    />
  </div>
</template>

<style scoped>
.user-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}
</style>
