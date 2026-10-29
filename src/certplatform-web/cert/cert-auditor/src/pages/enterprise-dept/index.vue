<script setup lang="ts">
/**
 * 企业机构管理（左树右表，配置驱动，路由 /enterprise-dept）
 *
 * - 左树：本工作区企业（根，只读）+ 企业部门（可增删改启停），底部「新增部门」
 * - 右表：选中部门下的人员（新增需先选部门）
 *
 * 企业节点只读（无编辑/删除/启停），仅可「新增下级」建顶层部门 —— 见 logic.resolveTreeActions。
 */
import { Plus } from '@element-plus/icons-vue'
import { YzhFormDialog, YzhStatusBadge, YzhTable, YzhTreeTableLayout, useTreeTable } from '@yzh-core'
import { ElSwitch } from 'element-plus'
import EnterpriseDeptLogic from './logic'

const { logic, tableRef, treeTableRef } = useTreeTable(EnterpriseDeptLogic)
</script>

<template>
  <div class="enterprise-dept-page">
    <YzhTreeTableLayout
      ref="treeTableRef"
      :tree-data="logic.treeData"
      :tree-width="260"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="true"
      :tree-load-data="logic.loadChildren.bind(logic)"
      :status-field="logic.treeStatusField"
      :node-actions="logic.nodeActions"
      :get-action-label="(action: string, node: any) => logic.getNodeActionLabel(action, node)"
      @tree-node-click="logic.onNodeClick"
      @tree-node-action="logic.onNodeAction"
    >
      <template #treeFooter>
        <el-button
          type="primary"
          :icon="Plus"
          class="enterprise-dept-page__add-dept"
          :disabled="logic.treeFooterAddDisabled"
          @click="logic.openDeptAddFromFooter()"
        >
          新增部门
        </el-button>
      </template>

      <template #default>
        <div class="enterprise-dept-page__table">
          <YzhTable
            ref="tableRef"
            :columns="logic.columns"
            :data-loader="logic.dataLoader.bind(logic)"
            :search-fields="logic.searchFields"
            :toolbar-actions="logic.toolbarActions"
            :row-action-buttons="logic.rowActions"
            select-mode="multiple"
            row-key="Code"
            @selection-change="logic.onSelectionChange($event)"
            @row-action="logic.onRowAction"
            @toolbar-action="logic.onToolbarAction"
          >
            <!-- 启用状态列（S08：状态标签一律 YzhStatusBadge） -->
            <template #column-IsValid="{ row }">
              <YzhStatusBadge
                :type="row.IsValid === 1 ? 'success' : 'info'"
                :text="row.IsValid === 1 ? '启用' : '禁用'"
              />
            </template>

            <template #toolbar-right>
              <div class="enterprise-dept-switch">
                <span class="enterprise-dept-switch__label">显示已禁用</span>
                <el-switch
                  :model-value="logic.showDisabled.value"
                  @change="logic.toggleShowDisabled()"
                />
              </div>
            </template>
          </YzhTable>
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- 人员新增/编辑弹窗 -->
    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      entity-name="人员"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      width="600px"
      @submit="logic.submitForm()"
    >
      <template #prepend>
        <div class="enterprise-dept-form-header">
          <span class="enterprise-dept-form-header__label">所属部门：</span>
          <span class="enterprise-dept-form-header__value">
            {{ logic.selectedNode?.Name ?? '未选择' }}
          </span>
        </div>
      </template>
    </YzhFormDialog>

    <!-- 部门新增/编辑弹窗（上级节点由树的 add-child / 底部新增按钮注入） -->
    <YzhFormDialog
      v-model:visible="logic.treeDialogVisible.value"
      v-model="logic.treeFormData"
      :mode="logic.treeDialogMode.value"
      entity-name="部门"
      :fields="logic.treeFormFields"
      :loading="logic.treeSubmitting.value"
      :cols="logic.treeFormLayoutCols as any"
      width="700px"
      @submit="logic.submitTreeNodeForm()"
    />
  </div>
</template>

<style scoped>
.enterprise-dept-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.enterprise-dept-page__add-dept {
  width: 100%;
}

.enterprise-dept-page__table {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: var(--yzh-color-bg-container, #fff);
  overflow: hidden;
}

.enterprise-dept-switch {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.enterprise-dept-switch__label {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-regular);
}

.enterprise-dept-form-header {
  display: flex;
  align-items: center;
  margin-bottom: var(--yzh-space-3, 12px);
}

.enterprise-dept-form-header__label {
  font-size: var(--yzh-font-size-base, 14px);
  color: var(--el-text-color-regular);
  font-weight: 500;
}

.enterprise-dept-form-header__value {
  font-size: var(--yzh-font-size-base, 14px);
  color: var(--el-text-color-primary);
  font-weight: 500;
}
</style>
