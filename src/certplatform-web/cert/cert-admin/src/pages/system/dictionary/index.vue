<script setup lang="ts">
/**
 * DictionaryPage - 数据字典管理（左树右表，配置驱动）
 *
 * 布局结构：
 * - 左侧：字典/分类树（懒加载，含搜索、增删改、启用/禁用）
 * - 右侧：字典项表格（分页、搜索、增删改、启用/禁用）
 */
import { Delete, Plus, RefreshRight } from '@element-plus/icons-vue'
import { YzhForm, YzhTreeTable, YzhTable, type TreeNode } from '@yzh-core'
import {
  ElButton,
  ElMessage,
  ElMessageBox,
  ElTag,
} from 'element-plus'
import { computed, onMounted, ref, watch, nextTick } from 'vue'
import DictionaryPageLogic from './logic'

// 实例化 Logic
const logic = new DictionaryPageLogic()

// 本地状态
const treeTableRef = ref()
const tableRef = ref()
const selectedRows = ref<any[]>([])

// 监听 tableRef 变化（含 :key 重挂载后重新注入）
watch(tableRef, (el) => {
  if (el) logic.setTableRef(el)
})

// 合并行操作按钮：标准 edit/delete + toggle-valid
const mergedRowActionButtons = computed(() => {
  const buttons: Record<string, string> = {}
  const rb = logic.config.value?.RowButtons
  if (rb?.Edit !== false) buttons['edit'] = '编辑'
  if (rb?.Delete !== false) buttons['delete'] = '删除'
  if (logic.enableField) {
    buttons['toggle-valid'] = '禁用/启用'
  }
  // 合入其他自定义按钮（过滤旧 disable/enable）
  const customButtons = logic.rowCustomButtons
  for (const [key, text] of Object.entries(customButtons)) {
    if (key === 'disable' || key === 'enable') continue
    buttons[key] = text
  }
  return buttons
})

// ========================================================
// 树节点操作
// ========================================================

/** 树节点点击 → 加载该字典下的字典项 */
async function handleNodeClick(node: TreeNode) {
  await logic.onNodeClick(node)
  await nextTick()
  tableRef.value?.refresh()
}

/** 树节点自定义操作（新增下级/编辑/删除/禁用/启用） */
async function handleTreeNodeAction(action: string, node: TreeNode) {
  if (action === 'add-child') {
    logic.openTreeDialog('add', null, node)
  } else if (action === 'edit') {
    logic.openTreeDialog('edit', node)
  } else if (action === 'delete') {
    await handleDeleteDict(node)
  } else if (action === 'toggle-valid') {
    await handleToggleDictValid(node)
  }
}

/** 新增字典分类（根级） */
function handleAddDictCategory() {
  logic.openTreeDialog('add', null, null)
}

/** 删除字典/分类 */
async function handleDeleteDict(node: TreeNode) {
  await ElMessageBox.confirm(
    `确定删除字典/分类【${node.name}】？\n（子节点将一并被软删除）`,
    '删除确认',
    {
      type: 'warning',
      confirmButtonText: '确定删除',
      cancelButtonText: '取消',
    },
  )
  await logic.deleteTree(node)
  ElMessage.success('已删除')
}

/** 启用/禁用字典/分类 */
async function handleToggleDictValid(node: TreeNode) {
  await logic.toggleTreeValid(node)
}

// ========================================================
// 字典项操作
// ========================================================

/** 新增字典项 */
function handleAddItem() {
  const ok = logic.openItemDialog()
  if (!ok) {
    ElMessage.warning('请先在左侧选择字典')
  }
}

/** 编辑字典项 */
function handleEditItem(row: any) {
  logic.openItemDialog(row)
}

/** 删除字典项 */
async function handleDeleteItem(row: any) {
  await ElMessageBox.confirm(
    `确定删除字典项【${row.DicName}】？`,
    '删除确认',
    { type: 'warning' },
  )
  await logic.deleteItem(row)
  ElMessage.success('删除成功')
}

/** 批量删除字典项 */
async function handleBatchDeleteItems() {
  if (selectedRows.value.length === 0) {
    ElMessage.warning('请先选择要删除的字典项')
    return
  }
  await ElMessageBox.confirm(
    `确定删除选中的 ${selectedRows.value.length} 个字典项？`,
    '批量删除',
    { type: 'warning' },
  )
  await logic.batchDeleteItems(selectedRows.value)
  selectedRows.value = []
  ElMessage.success('批量删除成功')
}

/** 切换字典项有效标志 */
async function handleToggleItemValid(row: any) {
  await logic.toggleItemValid(row)
}

/** 表格行自定义操作（edit / delete / toggle-valid） */
async function handleRowAction(action: string, row: any) {
  if (action === 'edit') {
    handleEditItem(row)
  } else if (action === 'delete') {
    await handleDeleteItem(row)
  } else if (action === 'toggle-valid') {
    await handleToggleItemValid(row)
  }
}

/** 表格数据加载 */
async function loadTableData(params: any) {
  try {
    return await logic.fetchItems(params)
  } catch (e: any) {
    ElMessage.error(e.message || '数据加载失败')
    return { rows: [], total: 0 }
  }
}

// ========================================================
// 提交弹窗
// ========================================================

async function handleTreeSubmit() {
  try {
    await logic.submitTreeForm()
    ElMessage.success(
      logic.treeDialogMode.value === 'add' ? '新增成功' : '修改成功',
    )
  } catch (e: any) {
    ElMessage.error(e.message || '保存失败')
  }
}

async function handleItemSubmit() {
  try {
    await logic.submitItemForm()
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
  if (tableRef.value) logic.setTableRef(tableRef.value)
})
</script>

<template>
  <div class="dict-page">
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
      <!-- 树底部：新增字典分类按钮 -->
      <template #treeFooter>
        <el-button type="primary" :icon="Plus" @click="handleAddDictCategory" style="width: 100%;">
          新增字典分类
        </el-button>
      </template>

      <template #default>
        <div class="dict-page__content">
          <!-- 字典项表格 -->
          <YzhTable
            ref="tableRef"
            :key="logic.selectedNode.value?.code ?? 'none'"
            :columns="logic.columnsWithActions as any"
            :data-loader="loadTableData"
            :search-fields="logic.searchFields as any"
            :selectable="true"
            :row-action-buttons="mergedRowActionButtons"
            row-key="Code"
            @selection-change="selectedRows = $event"
            @row-action="handleRowAction"
          >
            <!-- 状态列 -->
            <template #column-IsValid="{ row }">
              <el-tag
                :type="row.IsValid === 1 ? 'success' : 'info'"
                size="small"
              >
                {{ row.IsValid === 1 ? '启用' : '禁用' }}
              </el-tag>
            </template>

            <!-- 工具栏左侧 -->
            <template #toolbar-left>
              <el-button type="primary" :icon="Plus" @click="handleAddItem">
                新增字典项
              </el-button>
              <el-button type="danger" :icon="Delete" @click="handleBatchDeleteItems">
                批量删除
              </el-button>
              <el-button :icon="RefreshRight" @click="tableRef?.refresh()">
                刷新
              </el-button>
            </template>
          </YzhTable>
        </div>
      </template>
    </YzhTreeTable>

    <!-- 字典/分类 新增/编辑弹窗 -->
    <el-dialog
      v-model="logic.treeDialogVisible.value"
      :title="logic.treeDialogMode.value === 'add' ? '新增字典分类' : '编辑字典分类'"
      width="700px"
      :close-on-click-modal="false"
      destroy-on-close
    >
      <div class="dict-form-header">
        <span class="dict-form-header__label">上级节点：</span>
        <el-tag v-if="logic.treeParentNode.value" type="info">
          {{ logic.treeParentNode.value.name }}
        </el-tag>
        <el-tag v-else type="info">根级</el-tag>
      </div>
      <YzhForm
        v-model="logic.treeFormData"
        :fields="logic.treeFormFields as any"
        :loading="logic.treeSubmitting.value"
        :cols="2"
        label-width="100px"
        @submit="handleTreeSubmit"
        @reset="logic.treeDialogVisible.value = false"
      />
    </el-dialog>

    <!-- 字典项 新增/编辑弹窗 -->
    <el-dialog
      v-model="logic.dialogVisible.value"
      :title="logic.dialogMode.value === 'add' ? '新增字典项' : '编辑字典项'"
      width="700px"
      :close-on-click-modal="false"
      destroy-on-close
    >
      <div class="dict-form-header">
        <span class="dict-form-header__label">所属字典：</span>
        <el-tag v-if="logic.selectedNode.value" type="info">
          {{ logic.selectedNode.value.name }}
        </el-tag>
        <el-tag v-else type="info">未选择</el-tag>
      </div>
      <YzhForm
        v-model="logic.formData"
        :fields="logic.formFields as any"
        :loading="logic.submitting.value"
        :cols="logic.formLayoutCols as any"
        label-width="100px"
        @submit="handleItemSubmit"
        @reset="logic.dialogVisible.value = false"
      />
    </el-dialog>
  </div>
</template>

<style scoped>
.dict-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.dict-page__content {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: #fff;
  overflow: hidden;
}

.dict-form-header {
  display: flex;
  align-items: center;
  margin-bottom: 16px;
  padding: 8px 12px;
  background: var(--el-fill-color-light);
  border-radius: 4px;
}

.dict-form-header__label {
  font-size: 14px;
  color: var(--el-text-color-regular);
  font-weight: 500;
}
</style>
