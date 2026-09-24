<template>
  <div class="role-menu-page">
    <!-- 左侧：角色树 -->
    <div class="role-menu-page__tree-panel">
      <div class="role-menu-page__tree-header">
        <span class="role-menu-page__tree-title">角色列表</span>
      </div>
      <div class="role-menu-page__tree-content">
        <YzhTree
          :data="logic.treeData.value"
          :lazy="true"
          :load-data="logic.loadChildren.bind(logic)"
          :default-expand-all="false"
          node-key="Code"
          @node-click="handleNodeClick"
        />
      </div>
    </div>

    <!-- 右侧：菜单勾选树 -->
    <div class="role-menu-page__table-panel">
      <div class="role-menu-page__table-header">
        <span class="role-menu-page__table-title">
          {{ logic.selectedNode.value ? `${logic.selectedNode.value.Name} - 菜单授权` : '请先选择左侧角色' }}
        </span>
        <span v-if="logic.saving.value" class="role-menu-page__saving">保存中...</span>
      </div>

      <div class="role-menu-page__table-content">
        <YzhTreeTableCheckSelector
          v-if="logic.selectedNode.value"
          :flat-data="logic.associationData.value"
          :columns="logic.columns"
          node-key="Code"
          parent-key="ParentCode"
          node-type-field="NodeType"
          check-field="CheckFlag"
          :type-labels="TYPE_LABELS"
          :type-tag-types="TYPE_TAG_TYPES"
          :check-all-exclude-types="[]"
          :default-expand-all="true"
          @check-change="handleCheckChange"
        />
        <div v-else class="role-menu-page__empty">
          <el-empty description="请先选择左侧角色" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 角色-菜单管理页面
 *
 * - 左侧：角色树（懒加载）+ 已授权菜单数量 badge（内核内建局部刷新）
 * - 右侧：菜单勾选树
 * - 勾选/取消立即保存（auto-save）；勾选子菜单后端自动补全祖先菜单（Applied 回传）
 */
import { YzhTree, YzhTreeTableCheckSelector, useCheckTree } from '@yzh-core'
import { RoleMenuLogic } from './logic'

const { logic } = useCheckTree(RoleMenuLogic)

const TYPE_LABELS = { menu: '菜单' }
const TYPE_TAG_TYPES = { menu: 'warning' as const }

function handleNodeClick(data: any) {
  logic.handleNodeSelect(data)
}

function handleCheckChange(payload: { added: string[]; removed: string[] }) {
  logic.handleCheckChange(payload)
}
</script>

<style scoped>
.role-menu-page {
  height: 100%;
  display: flex;
}

.role-menu-page__tree-panel {
  width: 260px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--el-border-color-lighter);
  overflow: hidden;
}

.role-menu-page__tree-header {
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fff;
  flex-shrink: 0;
}

.role-menu-page__tree-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.role-menu-page__tree-content {
  flex: 1;
  overflow: auto;
}

.role-menu-page__table-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  overflow: hidden;
}

.role-menu-page__table-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fff;
  flex-shrink: 0;
}

.role-menu-page__table-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.role-menu-page__saving {
  font-size: 12px;
  color: var(--el-color-warning);
}

.role-menu-page__table-content {
  flex: 1;
  overflow: auto;
}

.role-menu-page__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
