<script setup lang="ts">
/**
 * 认证阶段管理（后台管理 · 左树右表，配置驱动）
 *
 * - 左树：字典 stage_category 三个分类（流程/审核/证后，只读单层，跨控制器数据源，无节点按钮）
 * - 右表：选中分类下阶段（`Category eq node.Code`），列含 StatusName 中文（读 v_cert_stage）
 * - 新增/编辑弹窗：YzhFormDialog，字段来自 `/config`（分类由树节点注入，不在表单内）
 */
import { YzhFormDialog, YzhTable, YzhTreeTableLayout, useTreeTable } from '@yzh-core'
import { ElSwitch, ElTag } from 'element-plus'
import CertStageLogic from './logic'

const { logic, tableRef, treeTableRef } = useTreeTable(CertStageLogic)
</script>

<template>
  <div class="cert-stage-page">
    <YzhTreeTableLayout
      ref="treeTableRef"
      :tree-data="logic.treeData"
      :tree-width="240"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :node-actions="logic.nodeActions"
      @tree-node-click="logic.onNodeClick"
    >
      <template #default>
        <div class="cert-stage-page__table">
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
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- 阶段新增/编辑弹窗 -->
    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      entity-name="认证阶段"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      width="700px"
      @submit="logic.submitForm()"
    >
      <template #prepend>
        <div class="cert-stage-form-header">
          <span class="cert-stage-form-header__label">归属分类：</span>
          <span class="cert-stage-form-header__value">
            {{ logic.selectedNode?.Name ?? '未选择' }}
          </span>
        </div>
      </template>
    </YzhFormDialog>
  </div>
</template>

<style scoped>
.cert-stage-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.cert-stage-page__table {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: var(--yzh-color-bg-container, #fff);
  overflow: hidden;
}

.cert-stage-toolbar-switch {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.cert-stage-toolbar-switch__label {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-regular);
}

.cert-stage-form-header {
  display: flex;
  align-items: center;
  margin-bottom: var(--yzh-space-3, 12px);
}

.cert-stage-form-header__label {
  font-size: var(--yzh-font-size-md, 14px);
  color: var(--el-text-color-regular);
  font-weight: 500;
}

.cert-stage-form-header__value {
  font-size: var(--yzh-font-size-md, 14px);
  color: var(--el-text-color-primary);
  font-weight: 500;
}
</style>
