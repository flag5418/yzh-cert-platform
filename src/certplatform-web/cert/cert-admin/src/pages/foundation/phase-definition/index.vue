<script setup lang="ts">
/**
 * 阶段定义管理（配置驱动 CRUD 页面）
 */
import { YzhFormDialog, YzhTable, useSingleTable } from '@yzh-core'
import { PhaseDefinitionLogic } from './logic'

const { logic, tableRef } = useSingleTable(PhaseDefinitionLogic)
</script>

<template>
  <div class="phase-definition-page">
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
      entity-name="阶段定义"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      @submit="logic.submitForm()"
    />
  </div>
</template>

<style scoped>
.phase-definition-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}
</style>
