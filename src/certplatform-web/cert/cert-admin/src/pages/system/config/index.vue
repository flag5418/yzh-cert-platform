<script setup lang="ts">
/**
 * 系统参数配置（配置驱动 CRUD 页面）
 *
 * 设计原则：
 * - 前端不硬编码任何按钮/列/字段，全部由后端 EntityConfig 配置驱动
 * - 分页/排序/搜索条件由 YzhTable 传入 → SingleTableCore.dataLoader
 * - 编辑/删除/启停由内核 dispatch 统一派发（删除/启停自带二次确认）
 */
import { YzhFormDialog, YzhTable, useSingleTable } from '@yzh-core'
import { ConfigLogic } from './logic'

// 实例化 Logic（useSingleTable 统一注入 tableRef）
const { logic, tableRef } = useSingleTable(ConfigLogic)
</script>

<template>
  <div class="config-page">
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
      entity-name="参数"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      @submit="logic.submitForm()"
    />
  </div>
</template>

<style scoped>
.config-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}
</style>
