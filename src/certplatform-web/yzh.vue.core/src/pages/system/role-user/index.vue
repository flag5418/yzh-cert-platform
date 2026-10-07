<template>
  <div class="role-user-page">
    <YzhTreeTableLayout
      ref="treeTableRef"
      :tree-data="logic.treeData.value"
      :tree-width="260"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="true"
      :tree-load-data="logic.loadChildren.bind(logic)"
      :status-field="logic.treeStatusField"
      :node-actions="logic.nodeActions"
      @tree-node-click="handleNodeClick"
      @tree-node-action="logic.onNodeAction"
    >
      <!-- 树底部「新增角色」：有选中加子级、无选中建根（与 organization 同位同构） -->
      <template #treeFooter>
        <el-button
          type="primary"
          :icon="Plus"
          class="role-user-page__add-role"
          @click="logic.openRoleAddFromFooter()"
        >
          新增角色
        </el-button>
      </template>

      <template #default>
        <div class="role-user-page__panel">
          <div class="role-user-page__panel-header">
            <span class="role-user-page__panel-title">
              {{
                logic.selectedNode.value
                  ? `${logic.selectedNode.value.Name} - 用户关联`
                  : '请先选择左侧角色'
              }}
            </span>
            <span v-if="logic.saving.value" class="role-user-page__saving">保存中...</span>
          </div>

          <div class="role-user-page__panel-content">
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
              <YzhEmptyState :icon="Pointer" title="请先选择左侧角色" />
            </div>
          </div>
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- 角色新增/编辑弹窗（字段由 /api/Role/treepconfig 的 TreeFormConfig 驱动） -->
    <YzhFormDialog
      v-model:visible="logic.treeDialogVisible.value"
      v-model="logic.treeFormData"
      :mode="logic.treeDialogMode.value"
      entity-name="角色"
      :fields="logic.treeFormFields"
      :loading="logic.treeSubmitting.value"
      :cols="logic.treeFormLayoutCols as any"
      width="500px"
      @submit="logic.submitTreeNodeForm()"
    >
      <template #prepend>
        <div v-if="logic.treeDialogMode.value === 'add'" class="role-user-form-meta">
          <span class="role-user-form-meta__label">上级角色：</span>
          <span class="role-user-form-meta__value">{{ logic.treeParentName }}</span>
        </div>
        <div v-else class="role-user-form-meta">
          <span class="role-user-form-meta__label">角色编码：</span>
          <span class="role-user-form-meta__value">
            {{ logic.treeEditingNode.value?.Code }}
          </span>
        </div>
      </template>
    </YzhFormDialog>
  </div>
</template>

<script setup lang="ts">
/**
 * 角色-用户管理页面（左树 + 右侧勾选，YzhTreeTableLayout 骨架）
 *
 * - 左侧：角色树（搜索 + 状态徽章 + 节点增删改），**树底「新增角色」**
 *   （与 `pages/system/organization` 同位：`#treeFooter` 插槽，⛔ 不自造树头）
 * - 右侧：机构+用户混合勾选树（勾选/取消立即保存，本地缓存切换角色无请求）
 * - 节点动作与弹窗字段零硬编码：全部来自 /api/Role/treepconfig
 */
import { onMounted, ref } from 'vue'
import { Plus, Pointer } from '@element-plus/icons-vue'
import {
  YzhTreeTableLayout,
  YzhTreeTableCheckSelector,
  YzhFormDialog,
  YzhEmptyState,
  useCheckTree,
} from '@yzh-core'
import { RoleUserLogic } from './logic'

const { logic } = useCheckTree(RoleUserLogic)

// 布局内 YzhTree 实例（暴露 appendNode/removeNode）→ 内核做本地增量（不整树 reload）
const treeTableRef = ref<any>(null)

onMounted(() => {
  logic.setTreeRef(treeTableRef.value?.treeRef ?? null)
})

function handleCheckChange(payload: { added: string[]; removed: string[] }) {
  logic.handleCheckChange(payload)
}

/**
 * 节点点击 → 选中角色并加载右侧关联态。
 * ⚠️ 必须走包装函数：Vue 编译器对 `@tree-node-click="logic.handleNodeSelect"`
 * 只透传函数引用（不绑 receiver），而 `handleNodeSelect` 是普通方法 ⇒ `this` 丢失。
 */
function handleNodeClick(node: any) {
  logic.handleNodeSelect(node)
}
</script>

<style scoped>
.role-user-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.role-user-page__add-role {
  width: 100%;
}

.role-user-page__panel {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: var(--yzh-color-bg-container, #fff);
  overflow: hidden;
}

.role-user-page__panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  border-bottom: 1px solid var(--el-border-color-lighter);
  flex-shrink: 0;
}

.role-user-page__panel-title {
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.role-user-page__saving {
  font-size: var(--yzh-font-size-sm, 12px);
  color: var(--yzh-color-warning);
}

.role-user-page__panel-content {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

.role-user-page__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}

.role-user-form-meta {
  display: flex;
  align-items: center;
  margin-bottom: var(--yzh-space-3, 12px);
}

.role-user-form-meta__label {
  font-size: var(--yzh-font-size-md, 14px);
  color: var(--el-text-color-regular);
  font-weight: 500;
}

.role-user-form-meta__value {
  font-size: var(--yzh-font-size-md, 14px);
  color: var(--el-text-color-primary);
  font-weight: 500;
  word-break: break-all;
}
</style>
