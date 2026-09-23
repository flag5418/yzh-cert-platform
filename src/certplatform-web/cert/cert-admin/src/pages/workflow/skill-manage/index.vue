<script setup lang="ts">
/**
 * 技能管理 — 左树右表（YZH 标准 TreeTable 架构）
 *
 * 布局：
 * - 左侧：分类树（扁平，所有分类为根节点，含搜索、增删改）
 * - 右侧：技能表格（选中分类后加载，分页、搜索、增删改）
 *
 * 配置驱动：
 * - 表格列从 logic.columns 自动获取（Workflow/Skill.json）
 * - 表单字段从 logic.formFields 自动获取
 * - 树节点表单从 logic.treeFormFields 自动获取（Workflow/SkillCategoryForm.json）
 */
import { Delete, Plus, RefreshRight } from '@element-plus/icons-vue'
import { YzhForm, YzhTreeTableLayout, YzhTable, type TreeNode } from '@yzh-core'
import {
  ElButton,
  ElMessage,
  ElMessageBox,
} from 'element-plus'
import { computed, onMounted, nextTick, ref } from 'vue'
import { SkillTreeTableLogic } from './logic'

// 实例化 Logic
const logic = new SkillTreeTableLogic()

// 本地状态
const treeTableRef = ref()
const tableRef = ref()
const selectedRows = ref<any[]>([])

// 行操作按钮（从后端 config 派生）
const rowActionButtons = computed(() => logic.rowActionButtons)

// ========================================================
// 树节点操作
// ========================================================

/** 树节点点击 → 加载该分类下技能 */
async function handleNodeClick(node: TreeNode) {
  await logic.onNodeClick(node)
}

/** 树节点自定义操作（编辑/删除） */
async function handleTreeNodeAction(action: string, node: TreeNode) {
  if (action === 'edit') {
    logic.openEditCategoryDialog(node)
  } else if (action === 'delete') {
    await handleDeleteCategory(node)
  }
}

/** 新增分类 */
function handleAddCategory() {
  logic.openAddCategoryDialog()
}

/** 删除分类 */
async function handleDeleteCategory(node: TreeNode) {
  await ElMessageBox.confirm(
    `确定删除分类【${node.Name}】？`,
    '删除确认',
    {
      type: 'warning',
      confirmButtonText: '确定删除',
      cancelButtonText: '取消',
    },
  )
  await logic.deleteCategory(node)
  ElMessage.success('已删除分类')
}

// ========================================================
// 技能操作
// ========================================================

/** 新增技能 */
function handleAddSkill() {
  const ok = logic.openAddSkillDialog()
  if (!ok) {
    ElMessage.warning('请先选择分类')
  }
}

/** 编辑技能 */
function handleEditSkill(row: any) {
  logic.openEditSkillDialog(row)
}

/** 删除技能 */
async function handleDeleteSkill(row: any) {
  await ElMessageBox.confirm(
    `确定删除技能【${row.Name}】？`,
    '删除确认',
    { type: 'warning' },
  )
  await logic.deleteSkill(row)
  ElMessage.success('删除成功')
}

/** 表格行自定义操作 */
async function handleRowAction(action: string, row: any) {
  if (action === 'edit') {
    handleEditSkill(row)
  } else if (action === 'delete') {
    await handleDeleteSkill(row)
  }
}

/** 批量删除技能 */
async function handleBatchDelete() {
  if (selectedRows.value.length === 0) {
    ElMessage.warning('请先选择要删除的技能')
    return
  }
  await ElMessageBox.confirm(
    `确定删除选中的 ${selectedRows.value.length} 个技能？`,
    '批量删除',
    { type: 'warning' },
  )
  await logic.batchDeleteSkills(selectedRows.value)
  selectedRows.value = []
  ElMessage.success('批量删除成功')
}

/** 切换技能有效标志（预留，暂不使用行内 toggle） */
// async function handleToggleSkillIsValid(row: any) {
//   await (logic as any).toggleRowIsValidWithConfirm(row, { entityName: row.Name })
// }

// ========================================================
// 提交弹窗
// ========================================================

async function handleSkillSubmit() {
  try {
    await logic.submitSkillForm()
    ElMessage.success(
      logic.dialogMode.value === 'add' ? '新增成功' : '修改成功',
    )
  } catch (e: any) {
    ElMessage.error(e.message || '保存失败')
  }
}

async function handleCategorySubmit() {
  try {
    await logic.submitCategoryForm()
  } catch (e: any) {
    ElMessage.error(e.message || '保存失败')
  }
}

// ========================================================
// 初始化
// ========================================================

onMounted(async () => {
  await logic.init()
  // （官方示例写法：nextTick 传回调，确保 DOM 更新完成后再取 ref）
  nextTick(() => {
    logic.setTreeTableRef(treeTableRef.value)
    logic.setTableRef(tableRef.value)
  })
})
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
      @tree-node-action="handleTreeNodeAction"
    >
      <!-- 树底部：新增分类按钮 -->
      <template #treeFooter>
        <el-button type="primary" :icon="Plus" @click="handleAddCategory" style="width: 100%;">
          新增分类
        </el-button>
      </template>

      <template #default>
        <div class="skill-page__content">
          <!-- 技能表格 -->
          <YzhTable
            ref="tableRef"
            :columns="logic.columns as any"
            :data-loader="logic.dataLoader.bind(logic)"
            :search-fields="logic.searchFields as any"
            :selectable="true"
            :row-action-buttons="rowActionButtons"
            row-key="Code"
            @selection-change="selectedRows = $event"
            @row-action="handleRowAction"
          >
            <!-- 工具栏左侧：操作按钮 -->
            <template #toolbar-left>
              <el-button type="primary" :icon="Plus" @click="handleAddSkill">新增技能</el-button>
              <el-button type="danger" :icon="Delete" @click="handleBatchDelete">批量删除</el-button>
              <el-button :icon="RefreshRight" @click="logic.refresh()">刷新</el-button>
            </template>
          </YzhTable>
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- 技能新增/编辑弹窗 -->
    <el-dialog
      v-model="logic.dialogVisible.value"
      :title="logic.dialogMode.value === 'add' ? '新增技能' : '编辑技能'"
      width="640px"
      :close-on-click-modal="false"
      :destroy-on-close="false"
    >
      <YzhForm
        v-model="logic.formData"
        :fields="logic.formFields as any"
        :loading="logic.submitting.value"
        :cols="logic.formLayoutCols as any"
        @submit="handleSkillSubmit"
        @reset="logic.dialogVisible.value = false"
      />
    </el-dialog>

    <!-- 分类新增/编辑弹窗 -->
    <el-dialog
      v-model="logic.categoryDialogVisible.value"
      :title="logic.categoryDialogMode.value === 'add' ? '新增分类' : '编辑分类'"
      width="560px"
      :close-on-click-modal="false"
      :destroy-on-close="false"
    >
      <YzhForm
        v-model="logic.categoryFormData"
        :fields="logic.treeFormFields as any"
        :loading="logic.categorySubmitting.value"
        :cols="2"
        label-width="80px"
        @submit="handleCategorySubmit"
        @reset="logic.categoryDialogVisible.value = false"
      />
    </el-dialog>
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
</style>
