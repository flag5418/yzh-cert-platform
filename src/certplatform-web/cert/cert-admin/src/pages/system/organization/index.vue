<script setup lang="ts">
/**
 * OrgPage - 组织机构-人员管理（左树右表，配置驱动）
 *
 * - 左树：机构树（懒加载 + 增删改 + 级联启停），底部「新增机构」
 * - 右表：选中机构下人员（新增仅限末端机构）
 * - 节点动作由内核 onNodeAction 派发；行按钮由 logic.rowActions 函数驱动
 */
import { Plus } from '@element-plus/icons-vue'
import { YzhFormDialog, YzhTable, YzhTreeTableLayout, useTreeTable } from '@yzh-core'
import { ElSwitch, ElTag } from 'element-plus'
import OrgPageLogic from './logic'

const { logic, tableRef, treeTableRef } = useTreeTable(OrgPageLogic)
</script>

<template>
  <div class="org-page">
    <YzhTreeTableLayout
      ref="treeTableRef"
      :tree-data="logic.treeData"
      :tree-width="260"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="true"
      :tree-load-data="logic.loadChildren.bind(logic)"
      :node-actions="logic.nodeActions"
      :get-action-label="(action: string, node: any) => logic.getNodeActionLabel(action, node)"
      @tree-node-click="logic.onNodeClick"
      @tree-node-action="logic.onNodeAction"
    >
      <template #treeFooter>
        <el-button
          type="primary"
          :icon="Plus"
          style="width: 100%"
          @click="logic.openOrgAddFromFooter()"
        >
          新增机构
        </el-button>
      </template>

      <template #default>
        <div class="org-page__table">
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
            <template #column-IsValid="{ row }">
              <el-tag :type="row.IsValid === 1 ? 'success' : 'info'" size="small">
                {{ row.IsValid === 1 ? '启用' : '禁用' }}
              </el-tag>
            </template>

            <template #toolbar-right>
              <div class="org-toolbar-switch">
                <span class="org-toolbar-switch__label">显示已禁用</span>
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
        <div class="org-form-header">
          <span class="org-form-header__label">所属机构：</span>
          <span class="org-form-header__value">
            {{ logic.selectedNode?.Name ?? '未选择' }}
          </span>
        </div>
      </template>
    </YzhFormDialog>

    <!-- 机构新增/编辑弹窗 -->
    <YzhFormDialog
      v-model:visible="logic.treeDialogVisible.value"
      v-model="logic.treeFormData"
      :mode="logic.treeDialogMode.value"
      entity-name="机构"
      :fields="logic.treeFormFields"
      :loading="logic.treeSubmitting.value"
      :cols="logic.treeFormLayoutCols as any"
      width="700px"
      @submit="logic.submitTreeNodeForm()"
    >
      <template #prepend>
        <div class="org-form-header">
          <span class="org-form-header__label">上级机构：</span>
          <span class="org-form-header__value">
            {{ logic.treeParentNode.value?.Name ?? '根级' }}
          </span>
        </div>
      </template>
    </YzhFormDialog>
  </div>
</template>

<style scoped>
.org-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.org-page__table {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: #fff;
  overflow: hidden;
}

.org-toolbar-switch {
  display: flex;
  align-items: center;
  gap: 8px;
}

.org-toolbar-switch__label {
  font-size: 13px;
  color: var(--el-text-color-regular);
}

.org-form-header {
  display: flex;
  align-items: center;
  margin-bottom: 12px;
}

.org-form-header__label {
  font-size: 14px;
  color: var(--el-text-color-regular);
  font-weight: 500;
}

.org-form-header__value {
  font-size: 14px;
  color: var(--el-text-color-primary);
  font-weight: 500;
}
</style>
