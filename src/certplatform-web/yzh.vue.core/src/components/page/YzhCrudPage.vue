<script setup lang="ts">
/**
 * YzhCrudPage - 通用单表 CRUD 页面（框架级组件）
 *
 * 功能：
 * - 根据后端 EntityConfig 自动渲染表格列、表单字段、搜索栏
 * - 提供标准的新增/编辑/删除/搜索/分页功能
 * - 支持自定义工具栏按钮和行操作按钮（通过 slot）
 *
 * 使用方式：
 *   <YzhCrudPage controller-name="SysUser">
 *     <template #toolbar-left>
 *       <el-button>自定义按钮</el-button>
 *     </template>
 *     <template #column-status="{ value }">
 *       <el-tag>{{ value === 1 ? '启用' : '禁用' }}</el-tag>
 *     </template>
 *   </YzhCrudPage>
 */
import { onMounted, reactive, ref } from 'vue'
import { YzhTable } from '../table'
import { YzhForm } from '../form'
import { ElDialog, ElMessage, ElMessageBox } from 'element-plus'
import type { YzhTableColumn, SearchField } from '../table/types'
import type { YzhFormField } from '../form/YzhForm.vue'
import { CrudPageLogic } from '../../logic/CrudPageLogic'
import type { FilterItem } from '@share/types/contracts'
import { addEntity, updateEntity, deleteEntities, getEntityPage } from '@share/api/generic'

const props = defineProps<{
  /** 后端控制器名称 */
  controllerName: string
  /** 页面标题（可选，默认从 config 获取） */
  title?: string
  /** 是否显示工具栏 */
  showToolbar?: boolean
  /** 是否显示搜索栏 */
  showSearch?: boolean
  /** 是否支持多选 */
  selectable?: boolean
  /** 默认每页条数 */
  pageSize?: number
}>()

const emit = defineEmits<{
  (e: 'row-click', row: any): void
  (e: 'selection-change', rows: any[]): void
}>()

// ========================================================
// Logic 实例
// ========================================================

class PageLogic extends CrudPageLogic<Record<string, any>> {
  controllerName = props.controllerName
  pageSize = props.pageSize || 20
}

const logic = new PageLogic()

// ========================================================
// 本地状态
// ========================================================

const tableRef = ref()
const selectedRows = ref<Record<string, any>[]>([])

// 弹窗状态
const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const formData = reactive<Record<string, any>>({})
const submitting = ref(false)

// ========================================================
// 表格数据加载
// ========================================================

async function loadTableData(params: { page: number; pageSize: number; filters?: FilterItem[] }) {
  const res = await getEntityPage(props.controllerName, {
    page: params.page,
    pageSize: params.pageSize,
    filters: params.filters,
  })
  return { rows: res.items, total: res.total }
}

// ========================================================
// CRUD 操作
// ========================================================

/** 新增 */
function handleAdd() {
  dialogMode.value = 'add'
  Object.assign(formData, {})
  dialogVisible.value = true
}

/** 编辑 */
function handleEdit(row: Record<string, any>) {
  dialogMode.value = 'edit'
  Object.assign(formData, { ...row })
  dialogVisible.value = true
}

/** 删除 */
async function handleDelete(row: Record<string, any>) {
  await ElMessageBox.confirm(`确定删除该记录？`, '删除确认', { type: 'warning' })
  await deleteEntities(props.controllerName, [row.code])
  ElMessage.success('删除成功')
  tableRef.value?.refresh()
}

/** 批量删除 */
async function handleBatchDelete() {
  if (selectedRows.value.length === 0) {
    ElMessage.warning('请先选择要删除的记录')
    return
  }
  await ElMessageBox.confirm(`确定删除选中的 ${selectedRows.value.length} 条记录？`, '批量删除', { type: 'warning' })
  const codes = selectedRows.value.map(r => r.code)
  await deleteEntities(props.controllerName, codes)
  selectedRows.value = []
  ElMessage.success('批量删除成功')
  tableRef.value?.refresh()
}

/** 提交表单 */
async function handleSubmit() {
  submitting.value = true
  try {
    if (dialogMode.value === 'add') {
      await addEntity(props.controllerName, formData)
      ElMessage.success('新增成功')
    } else {
      await updateEntity(props.controllerName, formData)
      ElMessage.success('修改成功')
    }
    dialogVisible.value = false
    tableRef.value?.refresh()
  } finally {
    submitting.value = false
  }
}

// ========================================================
// 初始化
// ========================================================

onMounted(async () => {
  await logic.init()
})

// 暴露给父组件
defineExpose({
  refresh: () => tableRef.value?.refresh(),
  logic,
})
</script>

<template>
  <div class="yzh-crud-page">
    <!-- 表格（列从 config 自动获取，零硬编码） -->
    <YzhTable
      ref="tableRef"
      :columns="(logic.columns as any)"
      :data-loader="loadTableData"
      :search-fields="(logic.searchFields as any)"
      :selectable="selectable !== false"
      :page-size="pageSize"
      @selection-change="selectedRows = $event; emit('selection-change', $event)"
      @row-click="emit('row-click', $event)"
    >
      <!-- 状态列自定义渲染 -->
      <template #column-enable="{ value }">
        <el-tag :type="value === 1 ? 'success' : 'info'" size="small">
          {{ value === 1 ? '启用' : '禁用' }}
        </el-tag>
      </template>

      <!-- 操作列 -->
      <template #column-actions="{ row }">
        <el-button type="primary" link size="small" @click="handleEdit(row)">编辑</el-button>
        <el-button type="danger" link size="small" @click="handleDelete(row)">删除</el-button>
      </template>

      <!-- 工具栏 -->
      <template #toolbar-left>
        <el-button type="primary" @click="handleAdd">新增</el-button>
        <el-button type="danger" @click="handleBatchDelete">批量删除</el-button>
        <slot name="toolbar-left" />
      </template>
      <template #toolbar-right>
        <slot name="toolbar-right" />
      </template>
    </YzhTable>

    <!-- 新增/编辑弹窗 -->
    <el-dialog
      v-model="dialogVisible"
      :title="dialogMode === 'add' ? '新增' : '编辑'"
      width="560px"
      align-center
    >
      <YzhForm
        v-model="formData"
        :fields="(logic.formFields as any)"
        :loading="submitting"
        @submit="handleSubmit"
        @reset="dialogVisible = false"
      />
    </el-dialog>
  </div>
</template>

<style scoped>
.yzh-crud-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}
</style>
