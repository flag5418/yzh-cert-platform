<script setup lang="ts">
/**
 * 认证机构管理（配置驱动 CRUD 页面）
 *
 * 唯一差异在后端：新增/修改/删除/启停时会同步 Sys_Organization 机构记录
 */
import { YzhFormDialog, YzhTable, useSingleTable } from '@yzh-core'
import { CertificationBodyLogic } from './logic'

const { logic, tableRef } = useSingleTable(CertificationBodyLogic)
</script>

<template>
  <div class="cert-body-page">
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
      entity-name="认证机构"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      width="760px"
      @submit="logic.submitForm()"
    />
  </div>
</template>

<style scoped>
.cert-body-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}
</style>
