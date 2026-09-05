<script setup lang="ts">
/**
 * 认证阶段 - V4 新架构
 */
import { Delete, Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import { deleteCertStage, getCertStagePage, saveCertStage, type CertStage } from '@/yzh/api/cert-stage'
import { YzhForm, type YzhFormFieldV4 } from '@/yzh/components/form'
import YzhTable from '@/yzh/components/table/YzhTable.vue'
import type { PageParams, SearchField, YzhTableColumnV4 } from '@/yzh/components/table/types'

const tableRef = ref()
const selectedRows = ref<CertStage[]>([])

const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const formData = reactive<Partial<CertStage>>({})
const formRef = ref()
const submitting = ref(false)

const stageTypeOptions = [
  { label: '申请', value: 'application' },
  { label: '审核', value: 'audit' },
  { label: '复核', value: 'review' },
  { label: '发证', value: 'certification' },
  { label: '监督', value: 'surveillance' }
]

const columns: YzhTableColumnV4<CertStage>[] = [
  { prop: 'StageCode', label: '阶段编码', width: 160, fixed: 'left' },
  { prop: 'StageName', label: '阶段名称', width: 200 },
  { prop: 'StageTypeName', label: '阶段类型', width: 120, align: 'center' },
  { prop: 'OrderNo', label: '排序', width: 80, align: 'center' },
  {
    prop: 'Enable',
    label: '状态',
    width: 80,
    align: 'center',
    formatter: (v: unknown) => (v === 1 ? '启用' : '禁用')
  },
  { prop: 'Description', label: '描述', minWidth: 200, className: 'yzh-cell-ellipsis' },
  { prop: 'actions', label: '操作', width: 160, fixed: 'right', slot: 'actions' }
]

const searchFields: SearchField[] = [
  { prop: 'keyword', label: '关键词', type: 'text', placeholder: '阶段编码 / 名称' },
  { prop: 'StageType', label: '阶段类型', type: 'select', options: stageTypeOptions }
]

async function loadData(params: PageParams) {
  return getCertStagePage(params)
}

function onAdd() {
  dialogMode.value = 'add'
  Object.assign(formData, {
    Id: undefined,
    StageCode: '',
    StageName: '',
    StageType: 'application',
    OrderNo: 0,
    Enable: 1,
    Description: ''
  })
  dialogVisible.value = true
}

function onEdit(row: CertStage) {
  dialogMode.value = 'edit'
  Object.assign(formData, { ...row })
  dialogVisible.value = true
}

async function onDelete(row: CertStage) {
  try {
    await ElMessageBox.confirm(`确定删除阶段「${row.StageName}」吗？`, '删除确认', { type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteCertStage(row.Id)
    ElMessage.success('删除成功')
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

async function onBatchDelete() {
  if (!selectedRows.value.length) {
    ElMessage.warning('请先选择要删除的阶段')
    return
  }
  try {
    await ElMessageBox.confirm(`确定删除选中的 ${selectedRows.value.length} 项吗？`, '批量删除', { type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteCertStage(selectedRows.value.map((r: CertStage) => r.Id))
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
    await saveCertStage(formData as CertStage)
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
  { prop: 'StageCode', label: '阶段编码', type: 'text', required: true, span: 12 },
  { prop: 'StageName', label: '阶段名称', type: 'text', required: true, span: 12 },
  { prop: 'StageType', label: '阶段类型', type: 'select', required: true, span: 12, options: stageTypeOptions },
  { prop: 'OrderNo', label: '排序', type: 'number', span: 12 },
  {
    prop: 'Enable',
    label: '状态',
    type: 'switch',
    span: 12,
    fieldProps: { activeText: '启用', inactiveText: '禁用' }
  },
  { prop: 'Description', label: '描述', type: 'textarea', span: 24, fieldProps: { rows: 3 } }
]
</script>

<template>
  <div class="yzh-cert-stage-page">
    <YzhTable
      ref="tableRef"
      :columns="columns"
      :data-loader="loadData"
      :search-fields="searchFields"
      :page-size="20"
      selectable
      :toolbar="{ refresh: true, columnSetting: true, density: true }"
      @selection-change="(rows: CertStage[]) => (selectedRows = rows)"
    >
      <template #toolbar-left>
        <el-button type="primary" :icon="Plus" @click="onAdd">新增阶段</el-button>
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
      :title="dialogMode === 'add' ? '新增认证阶段' : '编辑认证阶段'"
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
  </div>
</template>
