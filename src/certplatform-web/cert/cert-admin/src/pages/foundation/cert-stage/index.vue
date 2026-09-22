<script setup lang="ts">
/**
 * 认证阶段管理（配置驱动 CRUD 页面）
 *
 * 后端：CertStageController (YzhControllerBase<CertStage>)
 */
import { YzhFormDialog, YzhTable, useSingleTable } from '@yzh-core'
import { CertStageLogic } from './logic'

const { logic, tableRef } = useSingleTable(CertStageLogic)
</script>

<template>
  <div class="cert-stage-page">
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
      entity-name="认证阶段"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      @submit="logic.submitForm()"
    />
  </div>
</template>

<style scoped>
.cert-stage-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}
</style>
