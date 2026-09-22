<script setup lang="ts">
/**
 * 提示词模板管理（配置驱动 CRUD 页面）
 */
import { YzhFormDialog, YzhTable, useSingleTable } from '@yzh-core'
import { PromptTemplateLogic } from './logic'

const { logic, tableRef } = useSingleTable(PromptTemplateLogic)
</script>

<template>
  <div class="prompt-template-page">
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
      entity-name="提示词模板"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      width="750px"
      @submit="logic.submitForm()"
    />
  </div>
</template>

<style scoped>
.prompt-template-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}
</style>
