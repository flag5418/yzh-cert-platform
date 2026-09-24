<template>
  <div class="role-api-page">
    <!-- 左侧：角色树 -->
    <div class="role-api-page__tree-panel">
      <div class="role-api-page__tree-header">
        <span class="role-api-page__tree-title">角色列表</span>
      </div>
      <div class="role-api-page__tree-content">
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

    <!-- 右侧：接口勾选树 -->
    <div class="role-api-page__table-panel">
      <div class="role-api-page__table-header">
        <span class="role-api-page__table-title">
          {{ logic.selectedNode.value ? `${logic.selectedNode.value.Name} - 接口授权` : '请先选择左侧角色' }}
        </span>
        <span v-if="logic.saving.value" class="role-api-page__saving">保存中...</span>
      </div>

      <div class="role-api-page__table-content">
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
          :check-all-exclude-types="['group']"
          :default-expand-all="false"
          cascade
          searchable
          :search-fields="['Name', 'Path', 'Method', 'ApiName']"
          search-placeholder="搜索接口名称 / 路径 / 方法"
          count-type="api"
          @check-change="handleCheckChange"
        >
          <!-- 分组行：分组名 + 接口数量；接口行：接口名称 -->
          <template #column-Name="{ row }">
            <template v-if="row.NodeType === 'group'">
              <el-tag size="small" type="info" effect="plain">{{ row.Name }}</el-tag>
              <span class="role-api-page__group-count">{{ row.ApiCount ?? 0 }}</span>
            </template>
            <span v-else>{{ row.ApiName || row.Name }}</span>
          </template>
        </YzhTreeTableCheckSelector>
        <div v-else class="role-api-page__empty">
          <el-empty description="请先选择左侧角色" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 角色-接口管理页面
 *
 * - 左侧：角色树（懒加载）+ 已授权接口数量 badge（内核内建局部刷新）
 * - 右侧：接口分组树 + 接口勾选（cascade / 搜索 / count-type）
 * - 勾选/取消立即保存（auto-save）；分组节点跟随子级（afterAssociationsLoaded）
 */
import { YzhTree, YzhTreeTableCheckSelector, useCheckTree } from '@yzh-core'
import { RoleApiLogic } from './logic'

const { logic } = useCheckTree(RoleApiLogic)

const TYPE_LABELS = { group: '分组', api: '接口' }
const TYPE_TAG_TYPES = { group: 'primary' as const, api: 'success' as const }

function handleNodeClick(data: any) {
  logic.handleNodeSelect(data)
}

function handleCheckChange(payload: { added: string[]; removed: string[] }) {
  logic.handleCheckChange(payload)
}
</script>

<style scoped>
.role-api-page {
  height: 100%;
  display: flex;
}

.role-api-page__tree-panel {
  width: 260px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--el-border-color-lighter);
  overflow: hidden;
}

.role-api-page__tree-header {
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fff;
  flex-shrink: 0;
}

.role-api-page__tree-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.role-api-page__tree-content {
  flex: 1;
  overflow: auto;
}

.role-api-page__table-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  overflow: hidden;
}

.role-api-page__table-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fff;
  flex-shrink: 0;
}

.role-api-page__table-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.role-api-page__group-count {
  margin-left: 6px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.role-api-page__saving {
  font-size: 12px;
  color: var(--el-color-warning);
}

.role-api-page__table-content {
  flex: 1;
  overflow: auto;
}

.role-api-page__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
