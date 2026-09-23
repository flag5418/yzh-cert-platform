<script setup lang="ts">
/**
 * RolePage - 角色管理（树形结构，配置驱动）
 *
 * 布局结构：
 * - 左侧：角色树（懒加载，含搜索、增删改、启用/禁用）
 * - 右侧：选中角色详情面板
 *
 * 后端：RoleController 继承 TreeTableControllerBase<Sys_Role, Sys_Role>
 * 前端：useTreeTable + YzhTreeTableLayout + YzhFormDialog，节点动作由内核 onNodeAction 派发
 */
import { Plus } from '@element-plus/icons-vue'
import { YzhFormDialog, YzhTreeTableLayout, useTreeTable } from '@yzh-core'
import { ElTag } from 'element-plus'
import RolePageLogic from './logic'

// 实例化 Logic（useTreeTable 统一注入 tableRef/treeTableRef 与初始化流程）
const { logic, treeTableRef } = useTreeTable(RolePageLogic)
</script>

<template>
  <div class="role-page">
    <YzhTreeTableLayout
      ref="treeTableRef"
      :tree-data="logic.treeData"
      :tree-width="320"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="true"
      :tree-load-data="logic.loadChildren.bind(logic)"
      :node-actions="logic.nodeActions"
      @tree-node-click="logic.onNodeClick"
      @tree-node-action="logic.onNodeAction"
    >
      <!-- 树底部：新增角色按钮（动作仍走内核派发） -->
      <template #treeFooter>
        <el-button type="primary" :icon="Plus" style="width: 100%;" @click="logic.openTreeNodeDialog(null, logic.selectedNode)">
          新增角色
        </el-button>
      </template>

      <template #default>
        <div class="role-page__detail">
          <!-- 未选中提示 -->
          <div v-if="!logic.selectedNode" class="role-page__empty">
            <el-tag type="info" size="large">请在左侧选择角色查看详情</el-tag>
          </div>

          <!-- 选中角色详情 -->
          <div v-else class="role-page__info">
            <h3 class="role-page__title">角色详情</h3>
            <div class="role-page__fields">
              <div class="role-page__field">
                <span class="role-page__label">角色名称：</span>
                <span class="role-page__value">{{ logic.selectedNode.Name }}</span>
              </div>
              <div class="role-page__field">
                <span class="role-page__label">角色编码：</span>
                <span class="role-page__value">{{ logic.selectedNode.Code }}</span>
              </div>
              <div class="role-page__field">
                <span class="role-page__label">状态：</span>
                <el-tag
                  :type="(logic.selectedNode.Extra as any)?.IsValid === 1 ? 'success' : 'info'"
                  size="small"
                >
                  {{ (logic.selectedNode.Extra as any)?.IsValid === 1 ? '启用' : '禁用' }}
                </el-tag>
              </div>
              <div class="role-page__field">
                <span class="role-page__label">排序：</span>
                <span class="role-page__value">{{ (logic.selectedNode.Extra as any)?.OrderNo ?? '-' }}</span>
              </div>
              <div class="role-page__field">
                <span class="role-page__label">创建时间：</span>
                <span class="role-page__value">{{ (logic.selectedNode.Extra as any)?.CreateDate ?? '-' }}</span>
              </div>
            </div>
          </div>
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- 新增/编辑角色弹窗（树节点泛型流） -->
    <YzhFormDialog
      v-model:visible="logic.treeDialogVisible.value"
      v-model="logic.treeFormData"
      :mode="logic.treeDialogMode.value"
      entity-name="角色"
      :fields="logic.treeFormFields"
      :loading="logic.treeSubmitting.value"
      :cols="1"
      width="500px"
      @submit="logic.submitTreeNodeForm()"
    >
      <!-- 上级角色只读展示 -->
      <template #prepend>
        <div class="role-form-header">
          <span class="role-form-header__label">上级角色：</span>
          <span class="role-form-header__value">
            {{ logic.treeParentNode.value?.Name ?? '根级' }}
          </span>
        </div>
      </template>
    </YzhFormDialog>
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
  margin-bottom: 12px;
}

.role-form-header__label {
  font-size: 14px;
  color: var(--el-text-color-regular);
  font-weight: 500;
}

.role-form-header__value {
  font-size: 14px;
  color: var(--el-text-color-primary);
  font-weight: 500;
}
</style>
