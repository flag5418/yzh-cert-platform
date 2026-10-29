<script setup lang="ts">
/**
 * 专家组设置（配置驱动 CRUD 页面，路由 /audit-member）
 *
 * 审核人员基本档案管理（姓名/职称/专业/资质/联系方式）。
 * 入场计划指派审核员时从本池子挑选，不关联登录账号。
 * 工作区隔离由后端 OnBuildingFilter 强制。
 */
import { ElSwitch } from 'element-plus'
import { YzhFormDialog, YzhStatusBadge, YzhTable, useSingleTable } from '@yzh-core'
import { AuditMemberLogic } from './logic'

const { logic, tableRef } = useSingleTable(AuditMemberLogic)
</script>

<template>
  <div class="audit-member-page">
    <YzhTable
      ref="tableRef"
      :columns="logic.columns"
      :data-loader="logic.dataLoader.bind(logic)"
      :search-fields="logic.searchFields"
      :toolbar-actions="logic.toolbarActions"
      :row-action-buttons="logic.rowActions"
      :search-max-fields="3"
      select-mode="multiple"
      @selection-change="logic.onSelectionChange($event)"
      @row-action="logic.onRowAction"
      @toolbar-action="logic.onToolbarAction"
    >
      <!-- 启用状态列（S08：状态标签一律 YzhStatusBadge） -->
      <template #column-IsValid="{ row }">
        <YzhStatusBadge :type="row.IsValid === 1 ? 'success' : 'info'" :text="row.IsValid === 1 ? '启用' : '禁用'" />
      </template>

      <!-- 性别列 -->
      <template #column-Gender="{ row }">
        <span>{{ row.Gender === 1 ? '男' : row.Gender === 2 ? '女' : '未知' }}</span>
      </template>

      <!-- 「显示已禁用」开关 -->
      <template #toolbar-right>
        <div class="audit-member-toolbar-switch">
          <span class="audit-member-toolbar-switch__label">显示已禁用</span>
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
      entity-name="审核人员"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      width="760px"
      label-width="130px"
      @submit="logic.submitForm()"
    />
  </div>
</template>

<style scoped>
.audit-member-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}

.audit-member-toolbar-switch {
  display: flex;
  align-items: center;
  gap: 8px;
}

.audit-member-toolbar-switch__label {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-regular);
}
</style>
