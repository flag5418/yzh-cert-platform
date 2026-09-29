<script setup lang="ts">
/**
 * 技能管理 — 左树右表（YZH 标准 TreeTable 架构）
 *
 * 布局：
 * - 左侧：分类树（**只读**，数据源 = 字典 skill_category；分类维护入口在字典管理页）
 * - 右侧：技能表格（选中分类后加载，分页、搜索、增删改、启用/禁用）
 *
 * 配置驱动：
 * - 表格列从 logic.columns 自动获取（Workflow/Skill.json）
 * - 表单字段从 logic.formFields 自动获取（分类下拉由 DictCode=skill_category 驱动）
 * - 行按钮由 logic.rowActions 按 IsValid 二选一（内核 onRowAction 分发）
 * - 节点动作为空（树只读，后端树写钩子兜底拦截）
 */
import { Delete, Plus, RefreshRight } from '@element-plus/icons-vue'
import { YzhFormDialog, YzhTable, YzhTreeTableLayout, useTreeTable, type TreeNode } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed } from 'vue'
import { SkillTreeTableLogic } from './logic'

// 实例化 Logic（useTreeTable 统一注入 tableRef/treeTableRef 与初始化流程）
const { logic, tableRef, treeTableRef } = useTreeTable(SkillTreeTableLogic)

// 行操作按钮（内核 rowActions：按行 IsValid 解析出 编辑/删除/启用|禁用）
const rowActions = computed(() => logic.rowActions)

// ========================================================
// 树节点操作（树只读：仅点击过滤，无增删改）
// ========================================================

/** 树节点点击 → 加载该分类下技能 */
async function handleNodeClick(node: TreeNode) {
  await logic.onNodeClick(node)
}

// ========================================================
// 技能操作
// ========================================================

/** 新增技能（"全部"虚拟节点不可新增，须选中具体分类） */
function handleAddSkill() {
  if (!logic.selectedNode || logic.selectedNode.Code === '__all__') {
    ElMessage.warning('请先选择分类')
    return
  }
  logic.openAddDialog()
}

/** 批量删除技能（内核确认框：逐行名称 + 删除后移行） */
async function handleBatchDelete() {
  await logic.confirmDelete()
}
</script>

<template>
  <div class="skill-page">
    <YzhTreeTableLayout
      ref="treeTableRef"
      :tree-data="logic.treeData"
      :tree-width="280"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :node-actions="logic.nodeActions"
      :get-action-label="(action: string, node: TreeNode) => logic.getNodeActionLabel(action, node)"
      @tree-node-click="handleNodeClick"
    >
      <template #default>
        <div class="skill-page__content">
          <!-- 技能表格 -->
          <YzhTable
            ref="tableRef"
            :columns="logic.columns as any"
            :data-loader="logic.dataLoader.bind(logic)"
            :search-fields="logic.searchFields as any"
            :selectable="true"
            :row-action-buttons="rowActions"
            row-key="Code"
            @selection-change="logic.onSelectionChange($event)"
            @row-action="logic.onRowAction"
          >
            <!-- 启用状态列：el-tag（EnableField 列由 toTableColumns 自动走 slot） -->
            <template #column-IsValid="{ row }">
              <el-tag :type="row.IsValid === 1 ? 'success' : 'info'" size="small">
                {{ row.IsValid === 1 ? '启用' : '禁用' }}
              </el-tag>
            </template>

            <!-- 工具栏左侧：操作按钮 -->
            <template #toolbar-left>
              <el-button type="primary" :icon="Plus" @click="handleAddSkill">新增技能</el-button>
              <el-button type="danger" :icon="Delete" @click="handleBatchDelete">批量删除</el-button>
              <el-button :icon="RefreshRight" @click="logic.refresh()">刷新</el-button>
            </template>

            <!-- 工具栏右侧：显示已禁用开关（默认只看 IsValid=1） -->
            <template #toolbar-right>
              <div class="skill-toolbar-switch">
                <span class="skill-toolbar-switch__label">显示已禁用</span>
                <el-switch
                  :model-value="logic.showDisabled.value"
                  @change="logic.toggleShowDisabled()"
                />
              </div>
            </template>
          </YzhTable>
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- 技能新增/编辑弹窗 -->
    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      entity-name="技能"
      :fields="logic.formFields as any"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      width="640px"
      @submit="logic.submitForm()"
    />
  </div>
</template>

<style scoped>
.skill-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.skill-page__content {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: #fff;
  overflow: hidden;
}

.skill-toolbar-switch {
  display: flex;
  align-items: center;
  gap: 8px;
}

.skill-toolbar-switch__label {
  font-size: 13px;
  color: var(--el-text-color-regular);
}
</style>
