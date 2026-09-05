<script setup lang="ts">
/**
 * 认证机构 - V4 标准布局
 */
import { Delete, Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import {
  deleteCertBody,
  getCertBodyPage,
  saveCertBody,
  type CertificationBody
} from '@/yzh/api/certification-body'
import { YzhForm, type YzhFormFieldV4 } from '@/yzh/components/form'
import YzhPageLayout from '@/yzh/components/layout/YzhPageLayout.vue'
import YzhTable from '@/yzh/components/table/YzhTable.vue'
import type { PageParams, SearchField, YzhTableColumnV4 } from '@/yzh/components/table/types'

const tableRef = ref()
const selectedRows = ref<CertificationBody[]>([])

const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const formData = reactive<Partial<CertificationBody>>({})
const formRef = ref()
const submitting = ref(false)

const cbTypeOptions = [
  { label: '国家机构', value: 'national' },
  { label: '国际机构', value: 'international' },
  { label: '行业机构', value: 'industry' }
]

const columns: YzhTableColumnV4<CertificationBody>[] = [
  { prop: 'CbCode', label: '机构代码', width: 140, fixed: 'left' },
  { prop: 'CbName', label: '机构名称', width: 240, showOverflowTooltip: true, className: 'yzh-cell-ellipsis' },
  { prop: 'CbShortName', label: '简称', width: 120 },
  { prop: 'CbTypeName', label: '机构类型', width: 120, align: 'center' },
  { prop: 'Country', label: '国家', width: 100 },
  { prop: 'Province', label: '省份', width: 100 },
  { prop: 'City', label: '城市', width: 100 },
  { prop: 'ContactName', label: '联系人', width: 100 },
  { prop: 'ContactPhone', label: '联系电话', width: 140 },
  {
    prop: 'Enable',
    label: '状态',
    width: 80,
    align: 'center',
    formatter: (v: unknown) => (v === 1 ? '启用' : '禁用')
  },
  { prop: 'actions', label: '操作', width: 160, fixed: 'right', slot: 'actions' }
]

const searchFields: SearchField[] = [
  { prop: 'keyword', label: '关键词', type: 'text', placeholder: '机构代码 / 名称' },
  { prop: 'CbType', label: '机构类型', type: 'select', options: cbTypeOptions },
  {
    prop: 'Enable',
    label: '状态',
    type: 'select',
    options: [
      { label: '启用', value: 1 },
      { label: '禁用', value: 0 }
    ]
  }
]

async function loadData(params: PageParams) {
  return getCertBodyPage(params)
}

function onAdd() {
  dialogMode.value = 'add'
  Object.assign(formData, {
    Id: undefined,
    CbCode: '',
    CbName: '',
    CbShortName: '',
    CbType: 'national',
    Country: '',
    Province: '',
    City: '',
    Address: '',
    ContactName: '',
    ContactPhone: '',
    ContactEmail: '',
    Enable: 1,
    Remark: ''
  })
  dialogVisible.value = true
}

function onEdit(row: CertificationBody) {
  dialogMode.value = 'edit'
  Object.assign(formData, { ...row })
  dialogVisible.value = true
}

async function onDelete(row: CertificationBody) {
  try {
    await ElMessageBox.confirm(`确定删除机构「${row.CbName}」吗？`, '删除确认', { type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteCertBody(row.Id)
    ElMessage.success('删除成功')
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

async function onBatchDelete() {
  if (!selectedRows.value.length) {
    ElMessage.warning('请先选择要删除的机构')
    return
  }
  try {
    await ElMessageBox.confirm(`确定删除选中的 ${selectedRows.value.length} 项机构吗？`, '批量删除', { type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteCertBody(selectedRows.value.map((r: CertificationBody) => r.Id))
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
    await saveCertBody(formData as CertificationBody)
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
  { prop: 'CbCode', label: '机构代码', type: 'text', required: true, span: 12 },
  { prop: 'CbName', label: '机构名称', type: 'text', required: true, span: 12 },
  { prop: 'CbShortName', label: '简称', type: 'text', span: 12 },
  { prop: 'CbType', label: '机构类型', type: 'select', required: true, span: 12, options: cbTypeOptions },
  { prop: 'Country', label: '国家', type: 'text', span: 8 },
  { prop: 'Province', label: '省份', type: 'text', span: 8 },
  { prop: 'City', label: '城市', type: 'text', span: 8 },
  { prop: 'Address', label: '地址', type: 'text', span: 24 },
  { prop: 'ContactName', label: '联系人', type: 'text', span: 12 },
  { prop: 'ContactPhone', label: '联系电话', type: 'text', span: 12 },
  { prop: 'ContactEmail', label: '邮箱', type: 'text', span: 12 },
  {
    prop: 'Enable',
    label: '状态',
    type: 'switch',
    span: 12,
    fieldProps: { activeText: '启用', inactiveText: '禁用' }
  },
  { prop: 'Remark', label: '备注', type: 'textarea', span: 24, fieldProps: { rows: 3 } }
]
</script>

<template>
  <YzhPageLayout pageTitle="认证机构" helpText="维护所有ISO认证机构基本信息">
    <!-- 控制栏 -->
    <template #toolbar-left>
      <el-button type="primary" :icon="Plus" @click="onAdd">新增机构</el-button>
      <el-button type="danger" plain :icon="Delete" :disabled="!selectedRows.length" @click="onBatchDelete">
        批量删除
        <span v-if="selectedRows.length" style="margin-left: 4px; opacity: 0.8">({{ selectedRows.length }})</span>
      </el-button>
    </template>
    <template #toolbar-right>
      <el-button :icon="Refresh" @click="tableRef?.refresh()">刷新</el-button>
    </template>

    <!-- 表格 -->
    <YzhTable
      ref="tableRef"
      :columns="columns"
      :data-loader="loadData"
      :search-fields="searchFields"
      :page-size="20"
      selectable
      row-key="Id"
      :toolbar="{ refresh: false, columnSetting: true, density: true }"
      @selection-change="(rows: CertificationBody[]) => (selectedRows = rows)"
    >
      <template #column-actions="{ row }">
        <el-button text type="primary" :icon="Edit" @click="onEdit(row)">编辑</el-button>
        <el-button text type="danger" :icon="Delete" @click="onDelete(row)">删除</el-button>
      </template>
    </YzhTable>

    <!-- 编辑对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="dialogMode === 'add' ? '新增认证机构' : '编辑认证机构'"
      width="800px"
      align-center
      destroy-on-close
      :close-on-click-modal="false"
    >
      <YzhForm
        ref="formRef"
        v-model="formData"
        :fields="formFields"
        :loading="submitting"
        :cols="3"
        @submit="onSubmit"
        @reset="dialogVisible = false"
      />
    </el-dialog>
  </YzhPageLayout>
</template>
