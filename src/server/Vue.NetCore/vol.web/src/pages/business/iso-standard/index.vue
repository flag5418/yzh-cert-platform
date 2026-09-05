<script setup lang="ts">
/**
 * ISO 标准注册 - V4 新架构
 *
 * 字段说明：后端视图已包含字典翻译（CategoryName/StatusName）
 */
import {
  deleteISOStandard,
  getISOStandardPage,
  saveISOStandard,
  type ISOStandard
} from '@/yzh/api/iso-standard'
import { YzhForm, type YzhFormFieldV4 } from '@/yzh/components/form'
import YzhTable from '@/yzh/components/table/YzhTable.vue'
import type { PageParams, SearchField, YzhTableColumnV4 } from '@/yzh/components/table/types'
import { dictStore } from '@/yzh/store/dict'
import { Delete, Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { onMounted, reactive, ref } from 'vue'

const tableRef = ref()
const selectedRows = ref<ISOStandard[]>([])

const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const formData = reactive<Partial<ISOStandard>>({})
const formRef = ref()
const submitting = ref(false)

// 字典下拉（iso_category / standard_status 由后端维护）
const categoryOptions = [
  { label: '质量管理体系', value: 'quality' },
  { label: '环境管理体系', value: 'environment' },
  { label: '职业健康安全', value: 'ohsas' },
  { label: '信息安全管理', value: 'isms' },
  { label: '能源管理', value: 'energy' }
]
const statusOptions = [
  { label: '草稿', value: 'draft' },
  { label: '已发布', value: 'published' },
  { label: '已废止', value: 'obsolete' }
]

const columns: YzhTableColumnV4<ISOStandard>[] = [
  { prop: 'StandardCode', label: '标准编号', width: 180, sortable: true, fixed: 'left' },
  {
    prop: 'StandardName',
    label: '标准名称',
    width: 320,
    showOverflowTooltip: true,
    className: 'yzh-cell-ellipsis'
  },
  { prop: 'VersionYear', label: '版本', width: 80, align: 'center' },
  { prop: 'Category', label: '分类', width: 140, align: 'center', dictCode: 'iso_category' },
  {
    prop: 'Status',
    label: '状态',
    width: 100,
    align: 'center',
    dictCode: 'standard_status',
    tagType: 'primary'
  },
  {
    prop: 'CreateDate',
    label: '创建时间',
    width: 180,
    formatter: (v: unknown) => (v ? String(v).replace('T', ' ').slice(0, 19) : '-')
  },
  { prop: 'Remark', label: '备注', minWidth: 180, className: 'yzh-cell-ellipsis' },
  { prop: 'actions', label: '操作', width: 160, fixed: 'right', slot: 'actions' }
]

const searchFields: SearchField[] = [
  { prop: 'keyword', label: '关键词', type: 'text', placeholder: '标准编号 / 名称' },
  { prop: 'Category', label: '分类', type: 'select', options: categoryOptions },
  { prop: 'Status', label: '状态', type: 'select', options: statusOptions }
]

async function loadData(params: PageParams) {
  return getISOStandardPage(params)
}

function onAdd() {
  dialogMode.value = 'add'
  Object.assign(formData, {
    Id: undefined,
    StandardCode: '',
    StandardName: '',
    VersionYear: new Date().getFullYear(),
    Category: 'quality',
    Status: 'draft',
    Description: '',
    Remark: ''
  })
  dialogVisible.value = true
}

function onEdit(row: ISOStandard) {
  dialogMode.value = 'edit'
  Object.assign(formData, { ...row })
  dialogVisible.value = true
}

async function onDelete(row: ISOStandard) {
  try {
    await ElMessageBox.confirm(`确定删除标准「${row.StandardName}」吗？`, '删除确认', {
      type: 'warning'
    })
  } catch {
    return
  }
  try {
    await deleteISOStandard(row.Id)
    ElMessage.success('删除成功')
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

async function onBatchDelete() {
  if (!selectedRows.value.length) {
    ElMessage.warning('请先选择要删除的标准')
    return
  }
  try {
    await ElMessageBox.confirm(
      `确定删除选中的 ${selectedRows.value.length} 项标准吗？`,
      '批量删除',
      { type: 'warning' }
    )
  } catch {
    return
  }
  try {
    await deleteISOStandard(selectedRows.value.map((r: ISOStandard) => r.Id))
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
    await saveISOStandard(formData as ISOStandard)
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
  {
    prop: 'StandardCode',
    label: '标准编号',
    type: 'text',
    required: true,
    span: 12,
    placeholder: '如 ISO 9001:2015'
  },
  { prop: 'StandardName', label: '标准名称', type: 'text', required: true, span: 12 },
  { prop: 'VersionYear', label: '版本年份', type: 'number', span: 12 },
  {
    prop: 'Category',
    label: '分类',
    type: 'select',
    required: true,
    span: 12,
    options: dictStore.options('iso_category')
  },
  {
    prop: 'Status',
    label: '状态',
    type: 'select',
    required: true,
    span: 12,
    options: dictStore.options('standard_status')
  },
  { prop: 'Description', label: '描述', type: 'textarea', span: 24, fieldProps: { rows: 3 } },
  { prop: 'Remark', label: '备注', type: 'textarea', span: 24, fieldProps: { rows: 2 } }
]

onMounted(() => {
  dictStore.preload(['iso_category', 'standard_status'])
})
</script>

<template>
  <div class="yzh-iso-standard-page">
    <YzhTable
      ref="tableRef"
      :columns="columns"
      :data-loader="loadData"
      :search-fields="searchFields"
      :page-size="20"
      selectable
      :toolbar="{ refresh: true, columnSetting: true, density: true }"
      @selection-change="(rows: ISOStandard[]) => (selectedRows = rows)"
    >
      <template #toolbar-left>
        <el-button type="primary" :icon="Plus" @click="onAdd">新增标准</el-button>
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
        <el-button :icon="Refresh" @click="tableRef?.refresh()">刷新</el-button>
      </template>
      <template #column-actions="{ row }">
        <el-button text type="primary" :icon="Edit" @click="onEdit(row)">编辑</el-button>
        <el-button text type="danger" :icon="Delete" @click="onDelete(row)">删除</el-button>
      </template>
    </YzhTable>

    <el-dialog
      v-model="dialogVisible"
      :title="dialogMode === 'add' ? '新增 ISO 标准' : '编辑 ISO 标准'"
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
  </div>
</template>
