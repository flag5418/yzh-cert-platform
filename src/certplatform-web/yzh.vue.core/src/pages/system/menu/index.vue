<script setup lang="ts">
/**
 * MenuPage - 菜单管理（左树右表，配置驱动）
 *
 * - 左树：菜单树（懒加载 + 增删改 + 启停），底部「新增根菜单」
 * - 右表：选中节点子级（未选中 = 根级）
 * - 表单：YzhFormDialog + Icon slot（IconPicker）；节点动作由内核 onNodeAction 派发
 */
import { Plus } from '@element-plus/icons-vue'
import { YzhFormDialog, YzhTable, YzhTreeTableLayout, useTreeTable } from '@yzh-core'
import { formatMenuIcon } from '../../../utils/menu'
import IconPicker from './IconPicker.vue'
import MenuPageLogic from './logic'

const { logic, tableRef, treeTableRef } = useTreeTable(MenuPageLogic)
</script>

<template>
  <div class="menu-page">
    <YzhTreeTableLayout
      ref="treeTableRef"
      :tree-data="logic.treeData"
      :tree-width="300"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="true"
      :tree-load-data="logic.loadChildren.bind(logic)"
      :node-actions="logic.nodeActions"
      :get-action-label="(action: string, node: any) => logic.getNodeActionLabel(action, node)"
      @tree-node-click="logic.onNodeClick"
      @tree-node-action="logic.onNodeAction"
    >
      <template #treeFooter>
        <el-button
          type="primary"
          :icon="Plus"
          style="width: 100%"
          @click="logic.openRootMenuDialog()"
        >
          新增根菜单
        </el-button>
      </template>

      <template #default>
        <div class="menu-page__table">
          <YzhTable
            ref="tableRef"
            :columns="logic.columns"
            :data-loader="logic.dataLoader.bind(logic)"
            :search-fields="logic.searchFields"
            :row-action-buttons="logic.rowActions"
            row-key="Code"
            @selection-change="logic.onSelectionChange($event)"
            @row-action="logic.onRowAction"
          >
            <!-- 图标列：渲染真实 Element Plus 图标（Icon 为空显示 -） -->
            <template #column-Icon="{ row }">
              <el-icon v-if="row.Icon">
                <component :is="formatMenuIcon(row.Icon)" />
              </el-icon>
              <span v-else>-</span>
            </template>

            <!-- 状态列：IsValid 被内核标记为 slot，不给插槽会渲原值 1/0 -->
            <template #column-IsValid="{ row }">
              <el-tag :type="row.IsValid === 1 ? 'success' : 'info'" size="small">
                {{ row.IsValid === 1 ? '启用' : '禁用' }}
              </el-tag>
            </template>
          </YzhTable>
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- 树节点新增/编辑弹窗（Icon 走 custom slot） -->
    <YzhFormDialog
      v-model:visible="logic.treeDialogVisible.value"
      v-model="logic.treeFormData"
      :mode="logic.treeDialogMode.value"
      entity-name="菜单"
      :fields="logic.treeFormFields"
      :loading="logic.treeSubmitting.value"
      :cols="logic.treeFormLayoutCols as any"
      width="520px"
      @submit="logic.submitTreeNodeForm()"
    >
      <template #prepend>
        <div class="menu-form-header">
          <span class="menu-form-header__label">上级菜单：</span>
          <span class="menu-form-header__value">
            {{ logic.treeParentNode.value?.Name ?? '根级' }}
          </span>
        </div>
      </template>
      <template #Icon="{ value }">
        <IconPicker
          :model-value="value ?? ''"
          @update:model-value="logic.treeFormData.Icon = $event"
        />
      </template>
    </YzhFormDialog>

    <!-- 右表行编辑弹窗 -->
    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      entity-name="菜单"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      width="520px"
      @submit="logic.submitForm()"
    >
      <template #Icon="{ value }">
        <IconPicker
          :model-value="value ?? ''"
          @update:model-value="logic.formData.Icon = $event"
        />
      </template>
    </YzhFormDialog>
  </div>
</template>

<style scoped>
.menu-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.menu-page__table {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: #fff;
  overflow: hidden;
}

.menu-form-header {
  display: flex;
  align-items: center;
  margin-bottom: 12px;
}

.menu-form-header__label {
  font-size: 14px;
  color: var(--el-text-color-regular);
  font-weight: 500;
}

.menu-form-header__value {
  font-size: 14px;
  color: var(--el-text-color-primary);
  font-weight: 500;
}
</style>
