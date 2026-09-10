<script setup lang="ts">
/**
 * OrgPage - 组织机构-人员管理（左树右表，配置驱动）
 *
 * 布局结构：
 * - 左侧：机构树（懒加载，含搜索、增删改、启用/禁用）
 * - 右侧：人员表格（分页、搜索、增删改、启用/禁用、显示已禁用开关）
 *
 * 配置驱动：
 * - 表格列从 logic.columns 自动获取
 * - 表单字段从 logic.formFields 自动获取
 * - toolbar 按钮根据配置动态渲染
 */
import { Delete, Plus, RefreshRight } from '@element-plus/icons-vue'
import { YzhForm, YzhTreeTable, YzhTable, type TreeNode } from '@yzh-core'
import {
  ElButton,
  ElMessage,
  ElMessageBox,
  ElSwitch,
  ElTag,
} from 'element-plus'
import { computed, onMounted, nextTick, ref } from 'vue'
import OrgPageLogic from './logic'

// 实例化 Logic
const logic = new OrgPageLogic()

// 本地状态
const treeTableRef = ref()
const tableRef = ref()
const selectedRows = ref<any[]>([])

// 合并行操作按钮：标准 edit/delete（后端 RowButtons 控制）+ 自定义按钮
const mergedRowActionButtons = computed(() => {
  const buttons: Record<string, string> = {}
  // 标准按钮（受后端 RowButtons.Edit / RowButtons.Delete 控制）
  const rb = logic.config.value?.RowButtons
  if (rb?.Edit !== false) buttons['edit'] = '编辑'
  if (rb?.Delete !== false) buttons['delete'] = '删除'
  // 自定义按钮（toggle-valid 等）
  Object.assign(buttons, logic.rowCustomButtons)
  return buttons
})

// ========================================================
// 树节点操作
// ========================================================

/** 树节点点击 → 加载该机构下人员 */
async function handleNodeClick(node: TreeNode) {
  await logic.onNodeClick(node)
  tableRef.value?.refresh()
}

/** 树节点自定义操作（新增下级/编辑/删除/禁用/启用） */
async function handleTreeNodeAction(action: string, node: TreeNode) {
  if (action === 'add-child') {
    handleAddOrg(node)
  } else if (action === 'edit') {
    handleEditOrg(node)
  } else if (action === 'delete') {
    await handleDeleteOrg(node)
  } else if (action === 'toggle-valid') {
    await handleToggleOrgIsValid(node)
  }
}

/** 新增机构（可指定父节点） */
function handleAddOrg(parentNode?: TreeNode) {
  logic.openAddOrgDialog(parentNode ?? logic.selectedNode.value)
}

/** 编辑机构 */
function handleEditOrg(node: TreeNode) {
  logic.openEditOrgDialog(node)
}

/** 删除机构 */
async function handleDeleteOrg(node: TreeNode) {
  await ElMessageBox.confirm(
    `确定禁用机构【${node.name}】？将递归禁用所有子机构和人员！`,
    '禁用确认',
    {
      type: 'warning',
      confirmButtonText: '确定禁用',
      cancelButtonText: '取消',
    },
  )
  await logic.deleteOrg(node)
  ElMessage.success('已删除机构')
}

/** 禁用机构 */
async function handleToggleOrgIsValid(node: TreeNode) {
  const extra = (node.extra as any) || {}
  const isValid = extra.IsValid ?? 1
  const action = isValid === 1 ? '禁用' : '启用'
  await ElMessageBox.confirm(
    `确定${action}机构【${node.name}】？${isValid === 1 ? '将递归禁用所有子机构和人员！' : ''}`,
    `${action}确认`,
    {
      type: 'warning',
      confirmButtonText: `确定${action}`,
      cancelButtonText: '取消',
    },
  )
  await logic.toggleOrgIsValid(node)
}

// ========================================================
// 人员操作
// ========================================================

/** 新增人员 */
function handleAddUser() {
  const ok = logic.openAddUserDialog()
  if (!ok) {
    ElMessage.warning('请先选择机构')
  }
}

/** 编辑人员 */
function handleEditUser(row: any) {
  logic.openEditUserDialog(row)
}

/** 删除人员 */
async function handleDeleteUser(row: any) {
  await ElMessageBox.confirm(
    `确定删除人员【${row.UserTrueName}】？`,
    '删除确认',
    {
      type: 'warning',
    },
  )
  await logic.deleteUser(row)
  ElMessage.success('删除成功')
}

/** 批量删除 */
async function handleBatchDelete() {
  if (selectedRows.value.length === 0) {
    ElMessage.warning('请先选择要删除的人员')
    return
  }
  await ElMessageBox.confirm(
    `确定删除选中的 ${selectedRows.value.length} 个人员？`,
    '批量删除',
    {
      type: 'warning',
    },
  )
  await logic.batchDeleteUsers(selectedRows.value)
  selectedRows.value = []
  ElMessage.success('批量删除成功')
}

/** 切换人员有效标志 */
async function handleToggleUserIsValid(row: any) {
  await logic.toggleUserIsValid(row)
}

/** 表格行自定义操作（edit / delete / toggle-valid） */
async function handleRowAction(action: string, row: any) {
  if (action === 'edit') {
    handleEditUser(row)
  } else if (action === 'delete') {
    await handleDeleteUser(row)
  } else if (action === 'toggle-valid') {
    await handleToggleUserIsValid(row)
  }
}

// ========================================================
// 表格数据加载
// ========================================================

async function loadTableData(params: any) {
  const filters: Array<{ Field: string; Operator: string; Value: any }> = []
  if (logic.selectedNode.value) {
    filters.push({
      Field: 'OrgCode',
      Operator: 'eq',
      Value: logic.selectedNode.value.code,
    })
  }
  if (logic.showDisabled.value) {
    filters.push({ Field: 'ShowDisabled', Value: 'true', Operator: 'eq' })
  }
  try {
    const res = await logic.apiPostPublic('/filter', {
      Page: params.page,
      PageSize: params.rows,
      SortField: params.sort,
      SortOrder: params.order,
      Filters: filters,
    })
    if (res.data) {
      return { rows: res.data.Items ?? [], total: res.data.TotalCount ?? 0 }
    }
  } catch (e: any) {
    ElMessage.error(e.message || '数据加载失败')
  }
  return { rows: [], total: 0 }
}

// ========================================================
// 提交弹窗
// ========================================================

async function handleUserSubmit() {
  try {
    await logic.submitUserForm()
    ElMessage.success(
      logic.dialogMode.value === 'add' ? '新增成功' : '修改成功',
    )
  } catch (e: any) {
    ElMessage.error(e.message || '保存失败')
  }
}

async function handleOrgSubmit() {
  try {
    await logic.submitOrgForm()
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
  // 注入组件引用，使基类 addTreeNode/updateTreeNode/deleteTreeNode 等可局部刷新
  logic.setTreeTableRef(treeTableRef.value)
  logic.setTableRef(tableRef.value)
})
</script>

<template>
  <div class="org-page">
    <YzhTreeTable
      ref="treeTableRef"
      :tree-data="logic.treeData.value"
      :tree-width="260"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="true"
      :tree-load-data="logic.loadChildren.bind(logic)"
      :node-actions="logic.nodeActions"
      :get-action-label="(action: string, node: TreeNode) => logic.getNodeActionLabel(action, node)"
      @tree-node-click="handleNodeClick"
      @tree-node-action="handleTreeNodeAction"
    >
      <!-- 树底部：新增机构按钮 -->
      <template #treeFooter>
        <el-button type="primary" :icon="Plus" @click="handleAddOrg()" style="width: 100%;">
          新增机构
        </el-button>
      </template>

      <template #default>
        <div class="org-page__content">
          <!-- 人员表格 -->
          <YzhTable
            ref="tableRef"
            :columns="logic.columnsWithActions as any"
            :data-loader="loadTableData"
            :search-fields="logic.searchFields as any"
            :selectable="true"
            :row-action-buttons="mergedRowActionButtons"
            @selection-change="selectedRows = $event"
            @row-action="handleRowAction"
          >
            <!-- 状态列 -->
            <template #column-Enable="{ row }">
              <el-tag
                :type="row.IsValid === 1 ? 'success' : 'info'"
                size="small"
              >
                {{ row.IsValid === 1 ? '启用' : '禁用' }}
              </el-tag>
            </template>

            <!-- 工具栏左侧：操作按钮 -->
            <template #toolbar-left>
              <el-button type="primary" :icon="Plus" @click="handleAddUser"
                >新增人员</el-button
              >
              <el-button type="danger" :icon="Delete" @click="handleBatchDelete"
                >批量删除</el-button
              >
              <el-button :icon="RefreshRight" @click="logic.refreshTable()"
                >刷新</el-button
              >
            </template>

            <!-- 工具栏右侧：显示已禁用开关 -->
            <template #toolbar-right>
              <div class="toolbar-switch">
                <span class="toolbar-switch__label">显示已禁用</span>
                <el-switch
                  :model-value="logic.showDisabled.value"
                  @change="logic.toggleShowDisabled()"
                />
              </div>
            </template>
          </YzhTable>
        </div>
      </template>
    </YzhTreeTable>

    <!-- 人员新增/编辑弹窗 -->
    <el-dialog
      v-model="logic.dialogVisible.value"
      :title="logic.dialogMode.value === 'add' ? '新增人员' : '编辑人员'"
      width="600px"
      :close-on-click-modal="false"
      destroy-on-close
    >
      <YzhForm
        v-model="logic.formData"
        :fields="logic.formFieldsWithHidden as any"
        :loading="logic.submitting.value"
        :cols="logic.formLayoutCols as any"
        @submit="handleUserSubmit"
        @reset="logic.dialogVisible.value = false"
      >
        <!-- 所属机构只读展示 -->
        <template #orgCode>
          <el-tag v-if="logic.selectedNode.value" type="info">
            {{ logic.selectedNode.value.name }}
          </el-tag>
          <el-tag v-else type="info">未选择</el-tag>
        </template>
      </YzhForm>
    </el-dialog>

    <!-- 机构新增/编辑弹窗 -->
    <el-dialog
      v-model="logic.orgDialogVisible.value"
      :title="logic.orgDialogMode.value === 'add' ? '新增机构' : '编辑机构'"
      width="700px"
      :close-on-click-modal="false"
      destroy-on-close
    >
      <!-- 上级机构只读展示 -->
      <div class="org-form-header">
        <span class="org-form-header__label">上级机构：</span>
        <el-tag v-if="logic.orgParentNode.value" type="info">
          {{ logic.orgParentNode.value.name }}
        </el-tag>
        <el-tag v-else type="info">根级</el-tag>
      </div>

      <!-- 配置驱动的表单（字段从后端 sys_organization_form.json 自动派生） -->
      <YzhForm
        v-model="logic.orgFormData"
        :fields="logic.treeFormFields as any"
        :loading="logic.orgSubmitting.value"
        :cols="2"
        label-width="100px"
        @submit="handleOrgSubmit"
        @reset="logic.orgDialogVisible.value = false"
      />
    </el-dialog>
  </div>
</template>

<style scoped>
.org-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.org-page__content {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: #fff;
  overflow: hidden;
}

.toolbar-switch {
  display: flex;
  align-items: center;
  gap: 8px;
}

.toolbar-switch__label {
  font-size: 13px;
  color: var(--el-text-color-regular);
}

.org-form-header {
  display: flex;
  align-items: center;
  margin-bottom: 16px;
  padding: 8px 12px;
  background: var(--el-fill-color-light);
  border-radius: 4px;
}

.org-form-header__label {
  font-size: 14px;
  color: var(--el-text-color-regular);
  font-weight: 500;
}
</style>
