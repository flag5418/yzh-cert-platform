<template>
  <div class="role-user-page">
    <!-- 左侧：角色树 -->
    <div class="role-user-page__tree-panel">
      <div class="role-user-page__tree-header">
        <span class="role-user-page__tree-title">角色列表</span>
      </div>
      <div class="role-user-page__tree-content">
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

    <!-- 右侧：机构+用户混合树形表格 -->
    <div class="role-user-page__table-panel">
      <div v-if="logic.selectedRole.value" class="role-user-page__table-header">
        <span class="role-user-page__table-title">
          {{ logic.selectedRole.value.Name }} - 用户关联
        </span>
        <span v-if="logic.saving.value" class="role-user-page__saving">保存中...</span>
      </div>
      <div v-else class="role-user-page__table-header">
        <span class="role-user-page__table-title">请先选择左侧角色</span>
      </div>

      <div class="role-user-page__table-content">
        <YzhTreeTableCheckSelector
          v-if="logic.selectedRole.value"
          ref="checkSelectorRef"
          :flat-data="logic.checkTreeData.value"
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
 * 功能：
 * - 左侧：角色树（懒加载）+ 用户数量 badge
 * - 右侧：机构+用户混合树形表格（el-table tree 模式，带 checkbox）
 * - 勾选/取消立即保存（auto-save）
 * - 本地缓存：页面加载获取全部关联，切换角色无需 API
 */
import { ref, onMounted } from 'vue'
import { YzhTree, YzhTreeTableCheckSelector } from '@yzh-core'
import { RoleUserLogic } from './logic'

// ========================================================
// Logic 实例
// ========================================================

const logic = new RoleUserLogic()

// ========================================================
// Refs
// ========================================================

const roleTreeRef = ref<InstanceType<typeof YzhTree>>()
const checkSelectorRef = ref<InstanceType<typeof YzhTreeTableCheckSelector>>()

// ========================================================
// 带 badge 的角色树数据（深拷贝 + 注入 badge，确保 Vue 响应式）
// ========================================================

const roleTreeWithBadges = ref<any[]>([])

/** 深拷贝树并注入 badge（每次返回新引用，触发 Vue 重渲染） */
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

/** 递归注入 badge 到树节点（同时写入 Extra 和 extra，兼容不同访问方式） */
function injectBadgesRecursive(nodes: any[]): void {
  for (const node of nodes) {
    const count = logic.getUserCount(node.Code)
    const badge = count > 0 ? String(count) : undefined
    node.Extra = {
      ...node.Extra,
      badge,
    }
    node.extra = {
      ...node.extra,
      badge,
    }
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
    // 勾选/取消后刷新 badge
    refreshTreeBadges()
  })
}

// ========================================================
// 初始化
// ========================================================

onMounted(async () => {
  // 1. 先加载本地缓存（所有角色-用户关联）
  await logic.initCache()
  // 2. 再加载角色树（此时缓存已就绪，badge 可以正确计算）
  await logic.loadRoleTreeRoot()
  // 3. 注入 badge 到树数据（深拷贝 + 新引用，触发 Vue 响应式）
  refreshTreeBadges()
})
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
  padding: 8px 0;
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
