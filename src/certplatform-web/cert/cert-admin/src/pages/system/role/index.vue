<script setup lang="ts">
/**
 * RolePage - 角色管理（树形结构，配置驱动）
 *
 * 布局结构：
 * - 左侧：角色树（懒加载，含搜索、增删改、启用/禁用）
 * - 右侧：选中角色详情面板
 *
 * 后端：RoleController 继承 TreeTableControllerBase<Sys_Role, Sys_Role>
 * 前端：YzhTreeTable + TreeTableLogic
 */
import { Plus } from '@element-plus/icons-vue'
import { YzhForm, YzhTreeTable, type TreeNode } from '@yzh-core'
import {
  ElButton,
  ElMessage,
  ElMessageBox,
  ElTag,
} from 'element-plus'
import { onMounted, nextTick, ref } from 'vue'
import RolePageLogic from './logic'

// 实例化 Logic
const logic = new RolePageLogic()

// 本地状态
const treeTableRef = ref()

// ========================================================
// 树节点操作
// ========================================================

/** 树节点点击 */
async function handleNodeClick(node: TreeNode) {
  await logic.onNodeClick(node)
}

/** 树节点自定义操作 */
async function handleTreeNodeAction(action: string, node: TreeNode) {
  if (action === 'add-child') {
    handleAddRole(node)
  } else if (action === 'edit') {
    handleEditRole(node)
  } else if (action === 'delete') {
    await handleDeleteRole(node)
  } else if (action === 'toggle-valid') {
    await handleToggleRoleIsValid(node)
  }
}

/** 新增角色 */
function handleAddRole(parentNode?: TreeNode) {
  logic.openAddDialog(parentNode ?? logic.selectedNode.value)
}

/** 编辑角色 */
function handleEditRole(node: TreeNode) {
  logic.openEditDialog(node)
}

/** 删除角色 */
async function handleDeleteRole(node: TreeNode) {
  await ElMessageBox.confirm(
    `确定删除角色【${node.name}】？`,
    '删除确认',
    {
      type: 'warning',
      confirmButtonText: '确定删除',
      cancelButtonText: '取消',
    },
  )
  await logic.deleteTreeNode(node, true)
  ElMessage.success('删除成功')
}

/** 启用/禁用角色（基类统一处理：确认弹窗 → API → 本地更新） */
async function handleToggleRoleIsValid(node: TreeNode) {
  await logic.toggleTreeNodeWithConfirm(node)
}

// ========================================================
// 提交弹窗
// ========================================================

async function handleSubmit() {
  try {
    await logic.submitForm()
    ElMessage.success(
      logic.dialogMode.value === 'add' ? '新增成功' : '修改成功',
    )
  } catch (e: any) {
    ElMessage.error(e.message || '保存失败')
  }
}

// ========================================================
// 初始化
// ========================================================

onMounted(async () => {
  await logic.init()
  await nextTick()
  logic.setTreeTableRef(treeTableRef.value)
})
</script>

<template>
  <div class="role-page">
    <YzhTreeTable
      ref="treeTableRef"
      :tree-data="logic.treeData.value"
      :tree-width="320"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="true"
      :tree-load-data="logic.loadChildren.bind(logic)"
      :node-actions="logic.nodeActions"
      :get-action-label="(action: string, node: TreeNode) => logic.getNodeActionLabel(action, node)"
      @tree-node-click="handleNodeClick"
      @tree-node-action="handleTreeNodeAction"
    >
      <!-- 树底部：新增角色按钮 -->
      <template #treeFooter>
        <el-button type="primary" :icon="Plus" @click="handleAddRole()" style="width: 100%;">
          新增角色
        </el-button>
      </template>

      <template #default>
        <div class="role-page__detail">
          <!-- 未选中提示 -->
          <div v-if="!logic.selectedNode.value" class="role-page__empty">
            <el-tag type="info" size="large">请在左侧选择角色查看详情</el-tag>
          </div>

          <!-- 选中角色详情 -->
          <div v-else class="role-page__info">
            <h3 class="role-page__title">角色详情</h3>
            <div class="role-page__fields">
              <div class="role-page__field">
                <span class="role-page__label">角色名称：</span>
                <span class="role-page__value">{{ logic.selectedNode.value.name }}</span>
              </div>
              <div class="role-page__field">
                <span class="role-page__label">角色编码：</span>
                <span class="role-page__value">{{ logic.selectedNode.value.code }}</span>
              </div>
              <div class="role-page__field">
                <span class="role-page__label">状态：</span>
                <el-tag
                  :type="(logic.selectedNode.value.extra as any)?.IsValid === 1 ? 'success' : 'info'"
                  size="small"
                >
                  {{ (logic.selectedNode.value.extra as any)?.IsValid === 1 ? '启用' : '禁用' }}
                </el-tag>
              </div>
              <div class="role-page__field">
                <span class="role-page__label">排序：</span>
                <span class="role-page__value">{{ (logic.selectedNode.value.extra as any)?.OrderNo ?? '-' }}</span>
              </div>
              <div class="role-page__field">
                <span class="role-page__label">创建时间：</span>
                <span class="role-page__value">{{ (logic.selectedNode.value.extra as any)?.CreateDate ?? '-' }}</span>
              </div>
            </div>
          </div>
        </div>
      </template>
    </YzhTreeTable>

    <!-- 新增/编辑弹窗 -->
    <el-dialog
      v-model="logic.dialogVisible.value"
      :title="logic.dialogMode.value === 'add' ? '新增角色' : '编辑角色'"
      width="500px"
      :close-on-click-modal="false"
      destroy-on-close
    >
      <!-- 上级角色只读展示 -->
      <div class="role-form-header">
        <span class="role-form-header__label">上级角色：</span>
        <el-tag v-if="logic.parentNode.value" type="info">
          {{ logic.parentNode.value.name }}
        </el-tag>
        <el-tag v-else type="info">根级</el-tag>
      </div>

      <YzhForm
        v-model="logic.formData"
        :fields="logic.formFields as any"
        :loading="logic.submitting.value"
        :cols="1"
        label-width="100px"
        @submit="handleSubmit"
        @reset="logic.dialogVisible.value = false"
      />
    </el-dialog>
  </div>
</template>

<style scoped>
.role-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.role-page__detail {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: #fff;
  padding: 24px;
}

.role-page__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}

.role-page__title {
  font-size: 18px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 20px 0;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.role-page__fields {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.role-page__field {
  display: flex;
  align-items: center;
}

.role-page__label {
  font-size: 14px;
  color: var(--el-text-color-secondary);
  width: 100px;
  flex-shrink: 0;
}

.role-page__value {
  font-size: 14px;
  color: var(--el-text-color-primary);
}

.role-form-header {
  display: flex;
  align-items: center;
  margin-bottom: 16px;
  padding: 8px 12px;
  background: var(--el-fill-color-light);
  border-radius: 4px;
}

.role-form-header__label {
  font-size: 14px;
  color: var(--el-text-color-regular);
  font-weight: 500;
}
</style>
