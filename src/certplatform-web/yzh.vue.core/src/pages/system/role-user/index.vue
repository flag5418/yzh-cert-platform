<template>
  <div class="role-user-page">
    <!-- 左侧：角色树 -->
    <div class="role-user-page__tree-panel">
      <div class="role-user-page__tree-header">
        <span class="role-user-page__tree-title">角色列表</span>
      </div>
      <div class="role-user-page__tree-content">
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

    <!-- 右侧：机构+用户混合勾选树 -->
    <div class="role-user-page__table-panel">
      <div v-if="logic.selectedNode.value" class="role-user-page__table-header">
        <span class="role-user-page__table-title">
          {{ logic.selectedNode.value.Name }} - 用户关联
        </span>
        <span v-if="logic.saving.value" class="role-user-page__saving">保存中...</span>
      </div>
      <div v-else class="role-user-page__table-header">
        <span class="role-user-page__table-title">请先选择左侧角色</span>
      </div>

      <div class="role-user-page__table-content">
        <YzhTreeTableCheckSelector
          v-if="logic.selectedNode.value"
          :flat-data="logic.associationData.value"
          :columns="logic.columns"
          node-key="Code"
          parent-key="ParentCode"
          node-type-field="NodeType"
          check-field="CheckFlag"
          :default-expand-all="true"
          :loading="logic.loading.value"
          @check-change="handleCheckChange"
        />
        <div v-else class="role-user-page__empty">
          <el-empty description="请先选择左侧角色" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 角色-用户管理页面
 *
 * - 左侧：角色树（懒加载）+ 用户数量 badge（内核内建局部刷新）
 * - 右侧：机构+用户混合勾选树
 * - 勾选/取消立即保存（auto-save）；本地缓存初始化，切换角色无请求
 */
import { YzhTree, YzhTreeTableCheckSelector, useCheckTree } from '@yzh-core'
import { RoleUserLogic } from './logic'

const { logic } = useCheckTree(RoleUserLogic)

function handleNodeClick(data: any) {
  logic.handleNodeSelect(data)
}

function handleCheckChange(payload: { added: string[]; removed: string[] }) {
  logic.handleCheckChange(payload)
}
</script>

<style scoped>
.role-user-page {
  height: 100%;
  display: flex;
}

.role-user-page__tree-panel {
  width: 260px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--el-border-color-lighter);
  overflow: hidden;
}

.role-user-page__tree-header {
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fff;
  flex-shrink: 0;
}

.role-user-page__tree-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.role-user-page__tree-content {
  flex: 1;
  overflow: auto;
}

.role-user-page__table-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  overflow: hidden;
}

.role-user-page__table-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fff;
  flex-shrink: 0;
}

.role-user-page__table-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.role-user-page__saving {
  font-size: 12px;
  color: var(--el-color-warning);
}

.role-user-page__table-content {
  flex: 1;
  overflow: auto;
}

.role-user-page__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
