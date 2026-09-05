<script setup lang="ts">
/**
 * 系统部门管理 - V4 标准布局
 *
 * 结构：
 * - 标题栏
 * - 搜索栏（查询条件 + 按钮 grid）
 * - 控制栏（新增/批量删除/刷新）
 * - 表格
 * - 分页
 */
import { deleteDept, getDeptPage, saveDept, type SysDepartment } from '@/yzh/api/system-dept'
import { YzhForm, type YzhFormFieldV4 } from '@/yzh/components/form'
import YzhPageLayout from '@/yzh/components/layout/YzhPageLayout.vue'
import YzhTable from '@/yzh/components/table/YzhTable.vue'
import type { PageParams, YzhTableColumnV4 } from '@/yzh/components/table/types'
import { Delete, Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'

const tableRef = ref()
const selectedRows = ref<SysDepartment[]>([])

const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const formData = reactive<Partial<SysDepartment>>({})
const formRef = ref()
const submitting = ref(false)

const columns: YzhTableColumnV4<SysDepartment>[] = [
  { prop: 'departmentName', label: '部门名称', width: 240, fixed: 'left' },
  { prop: 'departmentCode', label: '部门编码', width: 200 },
  {
    prop: 'enable',
    label: '状态',
    width: 80,
    align: 'center',
    formatter: (v: unknown) => (v === 1 ? '启用' : '禁用')
  },
  { prop: 'creator', label: '创建人', width: 120 },
  {
    prop: 'createDate',
    label: '创建时间',
    width: 180,
    formatter: (v: unknown) => (v ? String(v).replace('T', ' ').slice(0, 19) : '-')
  },
  { prop: 'remark', label: '备注', minWidth: 200, className: 'yzh-cell-ellipsis' },
  { prop: 'actions', label: '操作', width: 160, fixed: 'right', slot: 'actions' }
]

/** 外部搜索表单（置入 YzhPageLayout 的 #search slot） */
const searchForm = reactive<{ keyword: string; enable: number | undefined }>({
  keyword: '',
  enable: undefined
})

/** 数据加载器：合并外部搜索条件 + 分页排序参数 */
async function loadDepts(params: PageParams) {
  return getDeptPage({
    ...params,
    departmentName: searchForm.keyword || undefined,
    enable: searchForm.enable
  })
}

function handleSearch() {
  tableRef.value?.loadData()
}

function handleSearchReset() {
  searchForm.keyword = ''
  searchForm.enable = undefined
  tableRef.value?.loadData()
}

function onAdd() {
  dialogMode.value = 'add'
    Object.assign(formData, {
    departmentId: undefined,
    departmentName: '',
    departmentCode: '',
    parentId: null,
    enable: 1,
    sortOrder: 0,
    remark: ''
  })
  dialogVisible.value = true
}

function onEdit(row: SysDepartment) {
  dialogMode.value = 'edit'
  Object.assign(formData, { ...row })
  dialogVisible.value = true
}

async function onDelete(row: SysDepartment) {
  try {
    await ElMessageBox.confirm(`确定删除部门「${row.departmentName}」吗？`, '删除确认', {
      type: 'warning'
    })
  } catch {
    return
  }
  try {
    await deleteDept(row.departmentId)
    ElMessage.success('删除成功')
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

async function onBatchDelete() {
  if (!selectedRows.value.length) {
    ElMessage.warning('请先选择要删除的部门')
    return
  }
  try {
    await ElMessageBox.confirm(
      `确定删除选中的 ${selectedRows.value.length} 个部门吗？`,
      '批量删除',
      { type: 'warning' }
    )
  } catch {
    return
  }
  try {
    await deleteDept(selectedRows.value.map((r: SysDepartment) => r.departmentId))
    ElMessage.success('批量删除成功')
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '批量删除失败')
  }
}

async function onSubmit() {
  const valid = await formRef.value?.validate()
  if (!valid) return
  submitting.value = true
  try {
    await saveDept(formData as SysDepartment)
    ElMessage.success(dialogMode.value === 'add' ? '新增成功' : '保存成功')
    dialogVisible.value = false
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    submitting.value = false
  }
}

const formFields: YzhFormFieldV4[] = [
  { prop: 'departmentName', label: '部门名称', type: 'text', required: true, span: 12 },
  { prop: 'departmentCode', label: '部门编码', type: 'text', required: true, span: 12 },
  { prop: 'parentId', label: '父级部门', type: 'number', span: 12 },
  { prop: 'sortOrder', label: '排序', type: 'number', span: 12 },
  {
    prop: 'enable',
    label: '状态',
    type: 'switch',
    span: 12,
    fieldProps: { activeText: '启用', inactiveText: '禁用' }
  },
  { prop: 'remark', label: '备注', type: 'textarea', span: 24, fieldProps: { rows: 3 } }
]
</script>

<template>
  <YzhPageLayout pageTitle="部门管理" helpText="管理组织架构，支持树形层级关系">
    <!-- 搜索栏 -->
    <template #search>
      <el-form :inline="true" class="dept-search-form">
        <el-form-item label="部门名称">
          <el-input
            v-model="searchForm.keyword"
            placeholder="请输入部门名称"
            clearable
            style="width: 200px"
          />
        </el-form-item>
        <el-form-item label="状态">
          <el-select
            v-model="searchForm.enable"
            placeholder="请选择"
            clearable
            style="width: 140px"
          >
            <el-option label="启用" :value="1" />
            <el-option label="禁用" :value="0" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="handleSearch">查询</el-button>
          <el-button @click="handleSearchReset">重置</el-button>
        </el-form-item>
      </el-form>
    </template>

    <!-- 控制栏 -->
    <template #toolbar-left>
      <el-button type="primary" :icon="Plus" @click="onAdd">新增部门</el-button>
      <el-button
        type="danger"
        plain
        :icon="Delete"
        :disabled="!selectedRows.length"
        @click="onBatchDelete"
      >
        批量删除
        <span v-if="selectedRows.length" style="margin-left: 4px; opacity: 0.8"
          >({{ selectedRows.length }})</span
        >
      </el-button>
    </template>
    <template #toolbar-right>
      <el-button :icon="Refresh" @click="tableRef?.refresh()">刷新</el-button>
    </template>

    <!-- 表格 -->
    <YzhTable
      ref="tableRef"
      :columns="columns"
      :data-loader="loadDepts"
      :search-fields="[]"
      :page-size="10"
      selectable
      row-key="departmentId"
      :toolbar="{ refresh: false, columnSetting: true, density: true }"
      @selection-change="(rows: SysDepartment[]) => (selectedRows = rows)"
    >
      <template #column-actions="{ row }">
        <el-button text type="primary" :icon="Edit" @click="onEdit(row)">编辑</el-button>
        <el-button text type="danger" :icon="Delete" @click="onDelete(row)">删除</el-button>
      </template>
    </YzhTable>

    <!-- 编辑对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="dialogMode === 'add' ? '新增部门' : '编辑部门'"
      width="720px"
      align-center
      destroy-on-close
      :close-on-click-modal="false"
    >
      <YzhForm
        ref="formRef"
        v-model="formData"
        :fields="formFields"
        :loading="submitting"
        :cols="2"
        @submit="onSubmit"
        @reset="dialogVisible = false"
      />
    </el-dialog>
    </YzhPageLayout>
</template>

<style scoped>
/* 搜索表单 */
.dept-search-form {
  display: flex;
  align-items: center;
  gap: 16px;
}

.dept-search-form :deep(.el-form-item) {
  margin-bottom: 0;
  margin-right: 0;
}

.dept-search-form :deep(.el-form-item__label) {
  font-weight: 500;
  color: var(--yzh-color-text-regular, #606266);
}
</style>

