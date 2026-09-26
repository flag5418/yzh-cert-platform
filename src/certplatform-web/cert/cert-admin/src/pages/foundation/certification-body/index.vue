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
    >
      <!-- 状态列：IsValid 启用/禁用标签（列被内核标记为 slot，不给插槽会渲原值 1/0） -->
      <template #column-IsValid="{ row }">
        <el-tag :type="row.IsValid === 1 ? 'success' : 'info'" size="small">
          {{ row.IsValid === 1 ? '启用' : '禁用' }}
        </el-tag>
      </template>

      <!-- 显示已禁用：后端默认过滤 IsValid=0，不开这个开关禁用即不可逆 -->
      <template #toolbar-right>
        <div class="cert-body-toolbar-switch">
          <span class="cert-body-toolbar-switch__label">显示已禁用</span>
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

.cert-body-toolbar-switch {
  display: flex;
  align-items: center;
  gap: 8px;
}

.cert-body-toolbar-switch__label {
  font-size: 13px;
  color: var(--el-text-color-regular);
}
</style>
