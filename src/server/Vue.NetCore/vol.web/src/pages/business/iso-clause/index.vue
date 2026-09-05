<script setup lang="ts">
/**
 * ISO 条款 - V4 新架构
 */
import { Delete, Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import { deleteISOClause, getISOClausePage, saveISOClause, type ISOClause } from '@/yzh/api/iso-clause'
import { YzhForm, type YzhFormFieldV4 } from '@/yzh/components/form'
import YzhTable from '@/yzh/components/table/YzhTable.vue'
import type { PageParams, SearchField, YzhTableColumnV4 } from '@/yzh/components/table/types'

const tableRef = ref()
const selectedRows = ref<ISOClause[]>([])

const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const formData = reactive<Partial<ISOClause>>({})
const formRef = ref()
const submitting = ref(false)

const columns: YzhTableColumnV4<ISOClause>[] = [
  { prop: 'StandardCode', label: '所属标准', width: 160, fixed: 'left' },
  { prop: 'ClauseCode', label: '条款编号', width: 140 },
  { prop: 'ClauseTitle', label: '条款标题', width: 240, showOverflowTooltip: true, className: 'yzh-cell-ellipsis' },
  { prop: 'OrderNo', label: '排序', width: 80, align: 'center' },
  {
    prop: 'Enable',
    label: '状态',
    width: 80,
    align: 'center',
    formatter: (v: unknown) => (v === 1 ? '启用' : '禁用')
  },
  {
    prop: 'CreateDate',
    label: '创建时间',
    width: 180,
    formatter: (v: unknown) => (v ? String(v).replace('T', ' ').slice(0, 19) : '-')
  },
  { prop: 'actions', label: '操作', width: 160, fixed: 'right', slot: 'actions' }
]

const searchFields: SearchField[] = [
  { prop: 'StandardId', label: '标准 ID', type: 'number' },
  { prop: 'keyword', label: '关键词', type: 'text', placeholder: '条款编号 / 标题' }
]

async function loadData(params: PageParams) {
  return getISOClausePage(params)
}

function onAdd() {
  dialogMode.value = 'add'
  Object.assign(formData, {
    Id: undefined,
    StandardId: undefined,
    ClauseCode: '',
    ClauseTitle: '',
    ClauseContent: '',
    OrderNo: 0,
    Enable: 1
  })
  dialogVisible.value = true
}

function onEdit(row: ISOClause) {
  dialogMode.value = 'edit'
  Object.assign(formData, { ...row })
  dialogVisible.value = true
}

async function onDelete(row: ISOClause) {
  try {
    await ElMessageBox.confirm(`确定删除条款「${row.ClauseCode} ${row.ClauseTitle}」吗？`, '删除确认', { type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteISOClause(row.Id)
    ElMessage.success('删除成功')
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

async function onBatchDelete() {
  if (!selectedRows.value.length) {
    ElMessage.warning('请先选择要删除的条款')
    return
  }
  try {
    await ElMessageBox.confirm(`确定删除选中的 ${selectedRows.value.length} 项条款吗？`, '批量删除', { type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteISOClause(selectedRows.value.map((r: ISOClause) => r.Id))
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
    await saveISOClause(formData as ISOClause)
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
  { prop: 'StandardId', label: '所属标准 ID', type: 'number', required: true, span: 12 },
  { prop: 'ClauseCode', label: '条款编号', type: 'text', required: true, span: 12, placeholder: '如 4.1 / 8.5.1' },
  { prop: 'ClauseTitle', label: '条款标题', type: 'text', required: true, span: 24 },
  { prop: 'ClauseContent', label: '条款内容', type: 'textarea', span: 24, fieldProps: { rows: 5 } },
  { prop: 'OrderNo', label: '排序', type: 'number', span: 12 },
  {
    prop: 'Enable',
    label: '状态',
    type: 'switch',
    span: 12,
    fieldProps: { activeText: '启用', inactiveText: '禁用' }
  }
]
</script>

<template>
  <div class="yzh-iso-clause-page">
    <YzhTable
      ref="tableRef"
      :columns="columns"
      :data-loader="loadData"
      :search-fields="searchFields"
      :page-size="20"
      selectable
      :toolbar="{ refresh: true, columnSetting: true, density: true }"
      @selection-change="(rows: ISOClause[]) => (selectedRows = rows)"
    >
      <template #toolbar-left>
        <el-button type="primary" :icon="Plus" @click="onAdd">新增条款</el-button>
        <el-button type="danger" plain :icon="Delete" :disabled="!selectedRows.length" @click="onBatchDelete">
          批量删除
          <span v-if="selectedRows.length" style="margin-left: 4px; opacity: 0.8">({{ selectedRows.length }})</span>
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
      :title="dialogMode === 'add' ? '新增 ISO 条款' : '编辑 ISO 条款'"
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
