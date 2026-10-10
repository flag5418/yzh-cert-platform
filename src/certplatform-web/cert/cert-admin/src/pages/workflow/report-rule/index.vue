<script setup lang="ts">
/**
 * ★ 报告章节定义 — 左树右表（★2026-09-29 去主表化）
 *
 * ★ 本页与「NC 规则定义」（nc-config）结构完全一致：
 * - 左侧：组织 → 标准 → 阶段 树（OrgStandardStageTree + OrgStageTableLayout）
 * - 右侧：报告章节表格（YzhTable + YzhFormDialog + TreeTableCore）
 *
 * ★ 去主表化（D34）：
 *   不再有「报告名称」「报表模板」层，章节直接按三元组挂在阶段下。
 *   新增章节时归属三编码由树节点自动注入，无需手填。
 */
import { computed } from 'vue'
import { Plus, RefreshRight } from '@element-plus/icons-vue'
import { YzhTable, YzhFormDialog, useTreeTable } from '@yzh-core'
import { OrgStageTableLayout } from '@share/components'
import { ReportRuleLogic } from './logic'

const { logic, tableRef } = useTreeTable(ReportRuleLogic)

const rowActions = computed(() => logic.rowActions)

// 右表空态文案：未选中阶段时给出操作提示
const emptyText = computed(() => (logic.anySelected ? '暂无数据' : '请在左侧选择阶段'))

async function handleNodeClick(node: any) {
  await logic.onNodeClick(node)
}
</script>

<template>
  <div class="report-rule-page">
    <OrgStageTableLayout
      :data="logic.treeData"
      title="组织 → 标准 → 阶段"
      :tree-width="280"
      :filterable="true"
      :default-expand-level="2"
      :max-level="3"
:leaf-types="['stage']"
:count-field="'SectionCount'"
@select="handleNodeClick"
    >
      <template #default>
        <div class="report-rule-page__content">
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
            <!-- IsValid 是 int（0/1），内核会渲成裸数字 —— 必须给插槽 -->
            <template #column-IsValid="{ row }">
              <el-tag :type="row.IsValid === 1 ? 'success' : 'info'" size="small">
                {{ row.IsValid === 1 ? '启用' : '禁用' }}
              </el-tag>
            </template>

            <!-- 工具栏左侧：新建章节 + 刷新 -->
            <template #toolbar-left>
              <el-button type="primary" :icon="Plus" @click="logic.onToolbarAction('add')">新建章节</el-button>
              <el-button :icon="RefreshRight" @click="tableRef?.refresh()">刷新</el-button>
            </template>
          </YzhTable>
        </div>
      </template>
    </OrgStageTableLayout>

    <!-- 编辑弹窗（★布局列数由 EntityConfig.FormCols 决定，勿硬编码） -->
    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      entity-name="章节"
      :title="logic.dialogMode.value === 'add' ? '新建章节' : '编辑章节'"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      label-width="130px"
      width="640px"
      @submit="logic.submitForm()"
    >
      <template #prepend>
        <div class="report-rule-form-header">
          <span class="report-rule-form-header__label">所属阶段：</span>
          <span class="report-rule-form-header__value">
            {{ logic.selectedNode?.Name ?? '未选择' }}
          </span>
        </div>
      </template>
    </YzhFormDialog>
  </div>
</template>

<style scoped>
.report-rule-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.report-rule-page__content {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  background: var(--yzh-color-bg-container, #fff);
  overflow: hidden;
}

.report-rule-form-header {
  display: flex;
  align-items: center;
  margin-bottom: 12px;
}

.report-rule-form-header__label {
  font-size: 14px;
  color: var(--el-text-color-regular);
  font-weight: 500;
}

.report-rule-form-header__value {
  font-size: 14px;
  color: var(--el-text-color-primary);
  font-weight: 500;
}
</style>
