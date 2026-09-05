<script setup lang="ts">
/**
 * 系统字典管理 - 新架构实现
 */
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import { deleteDict, getDictPage, saveDict, type SysDictionary } from '@/yzh/api/system-dict'
import { YzhForm, type YzhFormFieldV4 } from '@/yzh/components/form'
import YzhTable from '@/yzh/components/table/YzhTable.vue'
import type { PageParams, SearchField, YzhTableColumnV4 } from '@/yzh/components/table/types'

const tableRef = ref()
const selectedRows = ref<SysDictionary[]>([])

const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const formData = reactive<Partial<SysDictionary>>({})
const formRef = ref()
const submitting = ref(false)

const columns: YzhTableColumnV4<SysDictionary>[] = [
  { prop: 'dicCode', label: '字典编码', width: 200, fixed: 'left' },
  { prop: 'dicName', label: '字典名称', width: 200 },
  {
    prop: 'enable',
    label: '状态',
    width: 80,
    align: 'center',
    formatter: (v: unknown) => (v === 1 ? '启用' : '禁用')
  },
  { prop: 'orderNo', label: '排序', width: 80, align: 'center' },
  { prop: 'creator', label: '创建人', width: 120 },
  {
    prop: 'createDate',
    label: '创建时间',
    width: 180,
    formatter: (v: unknown) => (v ? String(v).replace('T', ' ').slice(0, 19) : '-')
  },
  { prop: 'remark', label: '备注', minWidth: 200, className: 'yzh-cell-ellipsis' },
  { prop: 'actions', label: '操作', width: 200, fixed: 'right', slot: 'actions' }
]

const searchFields: SearchField[] = [
  { prop: 'dicCode', label: '字典编码', type: 'text', placeholder: '请输入字典编码' },
  { prop: 'dicName', label: '字典名称', type: 'text', placeholder: '请输入字典名称' },
  {
    prop: 'enable',
    label: '状态',
    type: 'select',
    options: [
      { label: '启用', value: 1 },
      { label: '禁用', value: 0 }
    ]
  }
]

async function loadDicts(params: PageParams) {
  return getDictPage(params)
}

function onAdd() {
  dialogMode.value = 'add'
  Object.assign(formData, {
    dic_Id: undefined,
    dicCode: '',
    dicName: '',
    enable: 1,
    orderNo: 0,
    remark: ''
  })
  dialogVisible.value = true
}

function onEdit(row: SysDictionary) {
  dialogMode.value = 'edit'
  Object.assign(formData, { ...row })
  dialogVisible.value = true
}

async function onDelete(row: SysDictionary) {
  try {
    await ElMessageBox.confirm(`确定删除字典「${row.dicName}」吗？`, '删除确认', { type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteDict(row.dic_Id)
    ElMessage.success('删除成功')
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

async function onBatchDelete() {
  if (!selectedRows.value.length) {
    ElMessage.warning('请先选择要删除的字典')
    return
  }
  try {
    await ElMessageBox.confirm(`确定删除选中的 ${selectedRows.value.length} 个字典吗？`, '批量删除', { type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteDict(selectedRows.value.map((r: SysDictionary) => r.dic_Id))
    ElMessage.success('批量删除成功')
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '批量删除失败')
  }
}

function onManageItems(row: SysDictionary) {
  ElMessage.info(`字典项管理：${row.dicName}（${row.dicCode}）— 请到字典详情页维护`)
}

async function onSubmit() {
  const valid = await formRef.value?.validate()
  if (!valid) return
  submitting.value = true
  try {
    await saveDict(formData as SysDictionary)
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
  { prop: 'dicCode', label: '字典编码', type: 'text', required: true, span: 12 },
  { prop: 'dicName', label: '字典名称', type: 'text', required: true, span: 12 },
  { prop: 'orderNo', label: '排序', type: 'number', span: 12 },
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
  <YzhTable
    ref="tableRef"
    :columns="columns"
    :data-loader="loadDicts"
    :search-fields="searchFields"
    :page-size="10"
    :search-max-fields="2"
    selectable
    :toolbar="{ columnSetting: true }"
    @selection-change="(rows: SysDictionary[]) => (selectedRows = rows)"
  >
    <template #toolbar-left>
      <el-button type="primary" @click="onAdd">
        <i class="bi bi-plus-lg"></i> 新增字典
      </el-button>
      <el-button type="danger" plain :disabled="!selectedRows.length" @click="onBatchDelete">
        <i class="bi bi-trash"></i> 批量删除
        <span v-if="selectedRows.length" style="margin-left: 4px; opacity: 0.8">({{ selectedRows.length }})</span>
      </el-button>
      <el-button @click="tableRef?.refresh()">
        <i class="bi bi-arrow-clockwise"></i> 刷新
      </el-button>
    </template>
    <template #column-actions="{ row }">
      <el-button text type="primary" @click="onEdit(row)">
        <i class="bi bi-pencil"></i> 编辑
      </el-button>
      <el-button text type="success" @click="onManageItems(row)">
        <i class="bi bi-collection"></i> 字典项
      </el-button>
      <el-button text type="danger" @click="onDelete(row)">
        <i class="bi bi-trash"></i> 删除
      </el-button>
    </template>
  </YzhTable>

  <el-dialog
    v-model="dialogVisible"
    :title="dialogMode === 'add' ? '新增字典' : '编辑字典'"
    width="640px"
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
</template>

<style scoped>
.yzh-dict-page {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--yzh-color-bg-page, #f5f7fa);
}
</style>
