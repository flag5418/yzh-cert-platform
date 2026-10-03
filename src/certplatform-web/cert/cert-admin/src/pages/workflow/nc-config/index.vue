<script setup lang="ts">
/**
 * NC 规则管理 — 左树右表（YZH 标准架构：YzhTreeTableLayout + useTreeTable）
 *
 * 布局：
 * - 左侧：组织 → 标准 → 阶段 树（只读，数据源 = StandardDirectory 组织树）
 * - 右侧：NC 检查规则表格（YzhTable + YzhFormDialog + TreeTableCore）
 *
 * 架构（样板页面指南-V1 §四）：
 * - Logic 继承 TreeTableLogic，由 useTreeTable 注入 tableRef/treeTableRef 与初始化流程
 * - 表格列/表单字段/搜索字段/行按钮全部由后端 EntityConfig（Cert/ValidationRule.json）驱动
 * - 选中阶段节点后，内核自动注入 OrgCode + StandardCode + PhaseCode 三字段联动过滤
 * - 数据加载走内核 dataLoader：未选中阶段 → 右表空态（不发请求）
 * - 行动作（编辑/删除/启停/复制）由内核 dispatch 派发，页面无手写 handler
 */
import { computed } from 'vue'
import { Plus, RefreshRight } from '@element-plus/icons-vue'
import { YzhTable, YzhFormDialog, YzhTreeTableLayout, useTreeTable, type TreeNode } from '@yzh-core'
import { NCConfigLogic } from './logic'

// 实例化 Logic（useTreeTable 统一注入 tableRef/treeTableRef 与初始化流程）
const { logic, tableRef, treeTableRef } = useTreeTable(NCConfigLogic)

// 行操作按钮（内核 rowActions：Edit/Delete + 按行状态二选一的 启用|禁用|复制）
const rowActions = computed(() => logic.rowActions)

// 右表空态文案：未选中阶段时给出操作提示，避免只显示「暂无数据」
const emptyText = computed(() => (logic.anySelected ? '暂无数据' : '请在左侧选择阶段'))

// 树节点点击 → 内核选中节点并触发右表联动刷新
async function handleNodeClick(node: TreeNode) {
  await logic.onNodeClick(node)
}
</script>

<template>
  <div class="nc-config-page">
    <YzhTreeTableLayout
      ref="treeTableRef"
      :tree-data="logic.treeData"
      :tree-width="280"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :tree-default-expand-all="true"
      :node-actions="logic.nodeActions"
      @tree-node-click="handleNodeClick"
      @tree-node-action="logic.onNodeAction"
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
            <!-- 启用状态列：IsActive（bool）被内核标记为 slot，不给插槽会渲原值 true/false -->
            <template #column-IsActive="{ row }">
              <el-tag :type="row.IsActive ? 'success' : 'info'" size="small">
                {{ row.IsActive ? '启用' : '禁用' }}
              </el-tag>
            </template>

            <!-- 工具栏左侧：新建检查项 + 刷新 -->
            <template #toolbar-left>
              <el-button type="primary" :icon="Plus" @click="logic.onToolbarAction('add')">新建检查项</el-button>
              <el-button :icon="RefreshRight" @click="tableRef?.refresh()">刷新</el-button>
            </template>
          </YzhTable>
        </div>
      </template>
    </YzhTreeTableLayout>

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
</style>
