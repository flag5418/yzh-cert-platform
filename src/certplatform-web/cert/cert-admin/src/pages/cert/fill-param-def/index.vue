<script setup lang="ts">
/**
 * 企业资料参数页（后台管理 · 左树右表，配置驱动）
 *
 * - 左树：[通用参数] + 体系(系统) → 族 → 标准 三层（只读，跨控制器数据源，无节点按钮）
 *       - 体系/族 = virtual 节点，只展开/折叠，不触发过滤
 *       - 通用参数/标准 = 真实节点，点击触发右表过滤（StandardCode='' 或 = ISO GUID）
 * - 右表：选中节点下参数（`StandardCode eq node.Code`），行内 编辑/删除/禁用启用 由配置驱动
 * - 新增/编辑弹窗：YzhFormDialog，字段来自 `/config`（Schema 自动流）
 */
import { YzhFormDialog, YzhTable, YzhTreeTableLayout, useTreeTable } from '@yzh-core'
import { ElSwitch, ElTag } from 'element-plus'
import FillParamDefLogic from './logic'

const { logic, tableRef, treeTableRef } = useTreeTable(FillParamDefLogic)
</script>

<template>
  <div class="param-def-page">
    <YzhTreeTableLayout
      ref="treeTableRef"
      :tree-data="logic.treeData"
      :tree-width="260"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :node-actions="logic.nodeActions"
      @tree-node-click="logic.onNodeClick"
    >
      <template #default>
        <div class="param-def-page__table">
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
              <div class="param-def-toolbar-switch">
                <span class="param-def-toolbar-switch__label">显示已禁用</span>
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

    <!-- 参数新增/编辑弹窗 -->
    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      entity-name="参数"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      width="700px"
      @submit="logic.submitForm()"
    >
      <template #prepend>
        <div class="param-def-form-header">
          <span class="param-def-form-header__label">归属标准：</span>
          <span class="param-def-form-header__value">
            {{ logic.selectedNode?.Name ?? '未选择' }}
          </span>
        </div>
      </template>
    </YzhFormDialog>
  </div>
</template>

<style scoped>
.param-def-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.param-def-page__table {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: var(--yzh-color-bg-container, #fff);
  overflow: hidden;
}

.param-def-toolbar-switch {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.param-def-toolbar-switch__label {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-regular);
}

.param-def-form-header {
  display: flex;
  align-items: center;
  margin-bottom: var(--yzh-space-3, 12px);
}

.param-def-form-header__label {
  font-size: var(--yzh-font-size-md, 14px);
  color: var(--el-text-color-regular);
  font-weight: 500;
}

.param-def-form-header__value {
  font-size: var(--yzh-font-size-md, 14px);
  color: var(--el-text-color-primary);
  font-weight: 500;
}
</style>
