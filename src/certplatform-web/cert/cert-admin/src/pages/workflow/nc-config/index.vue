<script setup lang="ts">
/**
 * NC 规则管理 — 左树右表（OrgStageTableLayout 统一树）
 *
 * 布局：
 * - 左侧：组织 → 标准 → 阶段 树（OrgStandardStageTree + OrgStageTableLayout）
 * - 右侧：NC 检查规则表格（YzhTable + YzhFormDialog）
 */
import { computed } from 'vue'
import { ElSwitch, ElTag } from 'element-plus'
import { Plus, RefreshRight } from '@element-plus/icons-vue'
import { YzhTable, YzhFormDialog, useTreeTable } from '@yzh-core'
import { OrgStageTableLayout } from '@share/components'
import { NCConfigLogic } from './logic'

const { logic, tableRef } = useTreeTable(NCConfigLogic)

// 行操作按钮
const rowActions = computed(() => logic.rowActions)

// 右表空态文案
const emptyText = computed(() => (logic.anySelected ? '暂无数据' : '请在左侧选择阶段'))

// 树节点点击 → 内核选中节点并触发右表联动刷新
async function handleNodeClick(node: any) {
  await logic.onNodeClick(node)
}
</script>

<template>
  <div class="nc-config-page">
    <OrgStageTableLayout
      :data="logic.treeData"
      title="组织 → 标准 → 阶段"
      :tree-width="280"
      :filterable="true"
      :default-expand-level="2"
      :max-level="3"
:leaf-types="['stage']"
:count-field="'RuleCount'"
@select="handleNodeClick"
    >
      <template #default>
        <div class="nc-config-page__content">
          <YzhTable
            ref="tableRef"
            :columns="logic.columns as any"
            :data-loader="logic.dataLoader.bind(logic)"
            :search-fields="logic.searchFields as any"
            :row-action-buttons="rowActions"
            :empty-text="emptyText"
            row-key="Code"
            @row-action="logic.onRowAction"
          >
            <template #column-IsValid="{ row }">
              <el-tag :type="row.IsValid === 1 ? 'success' : 'info'" size="small">
                {{ row.IsValid === 1 ? '启用' : '禁用' }}
              </el-tag>
            </template>

            <template #toolbar-left>
              <el-button type="primary" :icon="Plus" @click="logic.onToolbarAction('add')">新建检查项</el-button>
              <el-button :icon="RefreshRight" @click="tableRef?.refresh()">刷新</el-button>
            </template>

            <template #toolbar-right>
              <div class="nc-config-page__switch">
                <span class="nc-config-page__switch-label">显示已禁用</span>
                <el-switch
                  :model-value="logic.showDisabled.value"
                  @change="logic.toggleShowDisabled()"
                />
              </div>
            </template>
          </YzhTable>
        </div>
      </template>
    </OrgStageTableLayout>

    <!-- 编辑弹窗 -->
    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      entity-name="检查项"
      :title="logic.dialogMode.value === 'add' ? '新建检查项' : '编辑检查项'"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="2"
      width="640px"
      @submit="logic.submitForm()"
    />
  </div>
</template>

<style scoped>
.nc-config-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.nc-config-page__content {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  background: var(--yzh-color-bg-container, #fff);
  overflow: hidden;
}

.nc-config-page__switch {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.nc-config-page__switch-label {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-regular);
}
</style>
