<template>
  <div class="role-api-page">
    <!-- 左侧：角色树 -->
    <div class="role-api-page__tree-panel">
      <div class="role-api-page__tree-header">
        <span class="role-api-page__tree-title">角色列表</span>
      </div>
      <div class="role-api-page__tree-content">
        <YzhTree
          ref="roleTreeRef"
          :data="roleTreeWithBadges"
          :lazy="true"
          :load-data="logic.loadRoleTreeChildren.bind(logic)"
          :default-expand-all="false"
          node-key="Code"
          @node-click="handleRoleClick"
        />
      </div>
    </div>

    <!-- 右侧：接口勾选树 -->
    <div class="role-api-page__table-panel">
      <div class="role-api-page__table-header">
        <span class="role-api-page__table-title">
          {{ logic.selectedRole.value ? `${logic.selectedRole.value.Name} - 接口授权` : '请先选择左侧角色' }}
        </span>
        <span v-if="logic.saving.value" class="role-api-page__saving">保存中...</span>
      </div>

      <div class="role-api-page__table-content">
        <YzhTreeTableCheckSelector
          v-if="logic.selectedRole.value"
          :flat-data="logic.checkTreeData.value"
          :columns="logic.columns"
          node-key="Code"
          parent-key="ParentCode"
          node-type-field="NodeType"
          check-field="CheckFlag"
          :type-labels="TYPE_LABELS"
          :type-tag-types="TYPE_TAG_TYPES"
          :check-all-exclude-types="['group']"
          :default-expand-all="true"
          @check-change="handleCheckChange"
        />
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
 * 功能：
 * - 左侧：角色树（懒加载）+ 已授权接口数量 badge
 * - 右侧：接口分组树 + 接口勾选（el-table tree 模式，带 checkbox）
 * - 勾选/取消立即保存（auto-save）
 * - 本地缓存：页面加载获取全部关联，切换角色无需 API
 */
import { ref, onMounted } from 'vue'
import { YzhTree, YzhTreeTableCheckSelector } from '@yzh-core'
import { RoleApiLogic } from './logic'

// ========================================================
// Logic 实例
// ========================================================

const logic = new RoleApiLogic()

// ========================================================
// 节点类型展示配置
// ========================================================

const TYPE_LABELS = { group: '分组', api: '接口' }
const TYPE_TAG_TYPES = { group: 'primary' as const, api: 'success' as const }

// ========================================================
// Refs
// ========================================================

const roleTreeRef = ref<InstanceType<typeof YzhTree>>()

// ========================================================
// 带 badge 的角色树数据
// ========================================================

const roleTreeWithBadges = ref<any[]>([])

function refreshTreeBadges(): void {
  const source = logic.roleTreeData.value
  if (!source || source.length === 0) {
    roleTreeWithBadges.value = []
    return
  }
  const cloned = JSON.parse(JSON.stringify(source))
  injectBadgesRecursive(cloned)
  roleTreeWithBadges.value = cloned
}

function injectBadgesRecursive(nodes: any[]): void {
  for (const node of nodes) {
    const count = logic.getApiCount(node.Code)
    const badge = count > 0 ? String(count) : undefined
    node.Extra = { ...node.Extra, badge }
    node.extra = { ...node.extra, badge }
    if (node.children && node.children.length > 0) {
      injectBadgesRecursive(node.children)
    }
  }
}

// ========================================================
// 事件处理
// ========================================================

function handleRoleClick(data: any) {
  logic.handleRoleSelect(data)
}

function handleCheckChange(payload: { added: string[]; removed: string[] }) {
  logic.handleCheckChange(payload).then(() => {
    refreshTreeBadges()
  })
}

// ========================================================
// 初始化
// ========================================================

onMounted(async () => {
  await logic.initCache()
  await logic.loadRoleTreeRoot()
  refreshTreeBadges()
})
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
  padding: 8px 0;
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
