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
import { onMounted, ref } from 'vue'
import { YzhTreeTable } from '@yzh-core/components/layout'
import { YzhTable } from '@yzh-core/components/table'
import { YzhForm } from '@yzh-core/components/form'
import { ElButton, ElMessage, ElMessageBox, ElTag, ElSwitch } from 'element-plus'
import { Plus, Delete, RefreshRight } from '@element-plus/icons-vue'
import OrgPageLogic from './logic'
import type { TreeNode } from '@share/types/tree'
import type { OrgUserRow } from './logic'

// 实例化 Logic
const logic = new OrgPageLogic()

// 本地状态
const tableRef = ref()
const selectedRows = ref<OrgUserRow[]>([])

// ========================================================
// 树节点操作
// ========================================================

/** 树节点点击 → 加载该机构下人员 */
async function handleNodeClick(node: TreeNode) {
  await logic.onNodeClick(node)
  tableRef.value?.refresh()
}

/** 新增机构 */
function handleAddOrg() {
  logic.openAddOrgDialog(logic.selectedNode.value)
}

/** 编辑机构 */
function handleEditOrg(node: TreeNode) {
  logic.openEditOrgDialog(node)
}

/** 删除机构 */
async function handleDeleteOrg(node: TreeNode) {
  await ElMessageBox.confirm(`确定禁用机构【${node.name}】？将递归禁用所有子机构和人员！`, '禁用确认', {
    type: 'warning',
    confirmButtonText: '确定禁用',
    cancelButtonText: '取消',
  })
  await logic.deleteOrg(node)
  ElMessage.success('已删除机构')
}

/** 禁用机构 */
async function handleDisableOrg(node: TreeNode) {
  await ElMessageBox.confirm(`确定禁用机构【${node.name}】？将递归禁用所有子机构和人员！`, '禁用确认', {
    type: 'warning',
    confirmButtonText: '确定禁用',
    cancelButtonText: '取消',
  })
  await logic.disableOrg(node)
  ElMessage.success('已禁用机构及其子机构/人员')
}

/** 启用机构 */
async function handleEnableOrg(node: TreeNode) {
  await logic.enableOrg(node)
  ElMessage.success('已启用机构')
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
function handleEditUser(row: OrgUserRow) {
  logic.openEditUserDialog(row)
}

/** 删除人员 */
async function handleDeleteUser(row: OrgUserRow) {
  await ElMessageBox.confirm(`确定删除人员【${row.userTrueName}】？`, '删除确认', {
    type: 'warning',
  })
  await logic.deleteUser(row)
  ElMessage.success('删除成功')
}

/** 批量删除 */
async function handleBatchDelete() {
  if (selectedRows.value.length === 0) {
    ElMessage.warning('请先选择要删除的人员')
    return
  }
  await ElMessageBox.confirm(`确定删除选中的 ${selectedRows.value.length} 个人员？`, '批量删除', {
    type: 'warning',
  })
  await logic.batchDeleteUsers(selectedRows.value)
  selectedRows.value = []
  ElMessage.success('批量删除成功')
}

/** 禁用人员 */
async function handleDisableUser(row: OrgUserRow) {
  await logic.disableUser(row)
  ElMessage.success('已禁用该人员')
}

/** 启用人员 */
async function handleEnableUser(row: OrgUserRow) {
  await logic.enableUser(row)
  ElMessage.success('已启用该人员')
}

// ========================================================
// 表格数据加载
// ========================================================

async function loadTableData(params: any) {
  const filters: Array<{ field: string; operator: string; value: any }> = []
  if (logic.selectedNode.value) {
    filters.push({ field: 'org_code', operator: 'eq', value: logic.selectedNode.value.code })
  }
  if (logic.showDisabled.value) {
    filters.push({ field: 'ShowDisabled', value: 'true', operator: 'eq' })
  }
  const res = await logic.apiPostPublic('/filter', {
    page: params.page,
    pageSize: params.rows,
    sortField: params.sortField,
    sortOrder: params.sortOrder,
    filters,
  })
  return { rows: res.data.items, total: res.data.total }
}

// ========================================================
// 提交弹窗
// ========================================================

async function handleUserSubmit() {
  try {
    await logic.submitUserForm()
    ElMessage.success(logic.dialogMode.value === 'add' ? '新增成功' : '修改成功')
  } catch (e: any) {
    ElMessage.error(e.message || '保存失败')
  }
}

async function handleOrgSubmit() {
  try {
    await logic.submitOrgForm()
    ElMessage.success(logic.orgDialogMode.value === 'add' ? '新增成功' : '修改成功')
  } catch (e: any) {
    ElMessage.error(e.message || '保存失败')
  }
}

// ========================================================
// 初始化
// ========================================================

onMounted(async () => {
  await logic.init()
})
</script>

<template>
  <div class="org-page">
    <YzhTreeTable
      :tree-data="logic.treeData.value"
      :tree-width="260"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="true"
      :tree-load-data="logic.loadChildren.bind(logic)"
      @tree-node-click="handleNodeClick"
    >
      <template #default>
        <div class="org-page__content">
          <!-- 人员表格 -->
          <YzhTable
            ref="tableRef"
            :columns="logic.columnsWithActions as any"
            :data-loader="loadTableData"
            :search-fields="logic.searchFields as any"
            :selectable="true"
            @selection-change="selectedRows = $event"
          >
            <!-- 状态列 -->
            <template #column-enable="{ row }">
              <el-tag :type="row.enable === 1 ? 'success' : 'info'" size="small">
                {{ row.enable === 1 ? '启用' : '禁用' }}
              </el-tag>
            </template>

            <!-- 操作列 -->
            <template #column-actions="{ row }">
              <el-button type="primary" link size="small" @click="handleEditUser(row)">编辑</el-button>
              <el-button type="danger" link size="small" @click="handleDeleteUser(row)">删除</el-button>
              <el-button
                v-if="row.enable === 1"
                type="warning"
                link
                size="small"
                @click="handleDisableUser(row)"
              >禁用</el-button>
              <el-button
                v-else
                type="success"
                link
                size="small"
                @click="handleEnableUser(row)"
              >启用</el-button>
            </template>

            <!-- 工具栏左侧：操作按钮 -->
            <template #toolbar-left>
              <el-button type="primary" :icon="Plus" @click="handleAddUser">新增人员</el-button>
              <el-button type="primary" :icon="Plus" plain @click="handleAddOrg">新增机构</el-button>
              <el-button type="danger" :icon="Delete" @click="handleBatchDelete">批量删除</el-button>
              <el-button :icon="RefreshRight" @click="logic.refreshTable()">刷新</el-button>
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
    >
      <YzhForm
        v-model="logic.formData"
        :fields="logic.formFieldsWithHidden as any"
        :loading="logic.submitting.value"
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
