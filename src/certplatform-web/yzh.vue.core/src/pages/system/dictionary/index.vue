<script setup lang="ts">
/**
 * DictionaryPage - 数据字典管理（左树右表，配置驱动）
 *
 * - 左树：字典/分类树（懒加载 + 增删改 + 启停），底部「新增字典分类」
 * - 右表：选中字典下字典项（未选中 = empty）
 * - 节点动作由内核 onNodeAction 派发；行按钮由后端 RowButtons 驱动
 */
import { Plus } from '@element-plus/icons-vue'
import { YzhFormDialog, YzhTable, YzhTreeTableLayout, useTreeTable } from '@yzh-core'
import { ElSwitch, ElTag } from 'element-plus'
import DictionaryPageLogic from './logic'

const { logic, tableRef, treeTableRef } = useTreeTable(DictionaryPageLogic)
</script>

<template>
  <div class="dict-page">
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
          @click="logic.openRootDictDialog()"
        >
          新增字典分类
        </el-button>
      </template>

      <template #default>
        <div class="dict-page__table">
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
              <div class="dict-toolbar-switch">
                <span class="dict-toolbar-switch__label">显示已禁用</span>
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

    <!-- 字典项 新增/编辑弹窗 -->
    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      entity-name="字典项"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      width="700px"
      @submit="logic.submitForm()"
    >
      <template #prepend>
        <div class="dict-form-header">
          <span class="dict-form-header__label">所属字典：</span>
          <span class="dict-form-header__value">
            {{ logic.selectedNode?.Name ?? '未选择' }}
          </span>
        </div>
      </template>
    </YzhFormDialog>

    <!-- 字典/分类 新增/编辑弹窗 -->
    <YzhFormDialog
      v-model:visible="logic.treeDialogVisible.value"
      v-model="logic.treeFormData"
      :mode="logic.treeDialogMode.value"
      entity-name="字典分类"
      :fields="logic.treeFormFields"
      :loading="logic.treeSubmitting.value"
      :cols="logic.treeFormLayoutCols as any"
      width="700px"
      @submit="logic.submitTreeNodeForm()"
    >
      <template #prepend>
        <div class="dict-form-header">
          <span class="dict-form-header__label">上级节点：</span>
          <span class="dict-form-header__value">
            {{ logic.treeParentNode.value?.Name ?? '根级' }}
          </span>
        </div>
      </template>
    </YzhFormDialog>
  </div>
</template>

<style scoped>
.dict-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.dict-page__table {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: #fff;
  overflow: hidden;
}

.dict-toolbar-switch {
  display: flex;
  align-items: center;
  gap: 8px;
}

.dict-toolbar-switch__label {
  font-size: 13px;
  color: var(--el-text-color-regular);
}

.dict-form-header {
  display: flex;
  align-items: center;
  margin-bottom: 12px;
}

.dict-form-header__label {
  font-size: 14px;
  color: var(--el-text-color-regular);
  font-weight: 500;
}

.dict-form-header__value {
  font-size: 14px;
  color: var(--el-text-color-primary);
  font-weight: 500;
}
</style>
