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
    >
      <!-- 状态列：IsValid 启用/禁用标签（列被内核标记为 slot，不给插槽会渲原值 1/0） -->
      <template #column-IsValid="{ row }">
        <el-tag :type="row.IsValid === 1 ? 'success' : 'info'" size="small">
          {{ row.IsValid === 1 ? '启用' : '禁用' }}
        </el-tag>
      </template>

      <!-- 显示已禁用：后端默认过滤 IsValid=0，不开这个开关禁用即不可逆 -->
      <template #toolbar-right>
        <div class="cert-stage-toolbar-switch">
          <span class="cert-stage-toolbar-switch__label">显示已禁用</span>
          <el-switch
            :model-value="logic.showDisabled.value"
            @change="logic.toggleShowDisabled()"
          />
        </div>
      </template>
    </YzhTable>

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

.cert-stage-toolbar-switch {
  display: flex;
  align-items: center;
  gap: 8px;
}

.cert-stage-toolbar-switch__label {
  font-size: 13px;
  color: var(--el-text-color-regular);
}
</style>
