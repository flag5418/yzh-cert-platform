<script setup lang="ts">
/**
 * 操作日志（只读表格页面）
 *
 * 只读性来自后端：`SysLogController` 覆写了 `GetToolbar()` / `GetRowButtons()`，
 * 关闭 Add / Delete / Edit、仅保留 Export —— 因此本页**不需要** `YzhFormDialog`。
 *
 * 原实现手工 `new LogLogic()` + `logic.dataLoader` / `logic.init()`，
 * 现改用 `useSingleTable()`：统一「onMounted → init → nextTick → setTableRef」样板（CP-5），
 * 与其他页面保持一致。
 */
import { YzhTable, useSingleTable } from '@yzh-core'
import { LogLogic } from './logic'

const { logic, tableRef } = useSingleTable(LogLogic)
</script>

<template>
  <div class="log-page">
    <YzhTable
      ref="tableRef"
      :columns="logic.columns"
      :data-loader="logic.dataLoader.bind(logic)"
      :search-fields="logic.searchFields"
      :toolbar-actions="logic.toolbarActions"
      :row-action-buttons="logic.rowActions"
      row-key="Id"
      @row-action="logic.onRowAction"
      @toolbar-action="logic.onToolbarAction"
    />
  </div>
</template>

<style scoped>
.log-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}
</style>
