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
import { useRoleTreeBadges } from '../_shared/useRoleTreeBadges'

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
// 带 badge 的角色树数据
//
// 初始化时注入一次徽标；勾选/取消后只局部更新「当前角色」那一个节点。
// 之前是每次勾选都深拷贝整棵树并整体替换 :data，会把 el-tree 的展开状态
// 和懒加载出来的子角色一起重置（详见 useRoleTreeBadges 注释）。
// ========================================================

const {
  treeNodes: roleTreeWithBadges,
  init: initTreeBadges,
  updateBadge: updateRoleBadge,
} = useRoleTreeBadges((code) => logic.getUserCount(code))

// ========================================================
// 事件处理
// ========================================================

function handleRoleClick(data: any) {
  logic.handleRoleSelect(data)
}

function handleCheckChange(payload: { added: string[]; removed: string[] }) {
  logic.handleCheckChange(payload).then(() => {
    // 只刷新当前角色节点的徽标（局部更新，不重建整棵树）
    updateRoleBadge(logic.selectedRole.value?.Code)
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
  // 3. 注入 badge 到树数据（仅初始化一次）
  initTreeBadges(logic.roleTreeData.value)
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
