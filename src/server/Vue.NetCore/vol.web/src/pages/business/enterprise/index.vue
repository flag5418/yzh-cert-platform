<script setup lang="ts">
/**
 * 企业管理 - V4 新架构
 */
import { Delete, Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import { deleteEnterprise, getEnterprisePage, saveEnterprise, type Enterprise } from '@/yzh/api/enterprise'
import { YzhForm, type YzhFormFieldV4 } from '@/yzh/components/form'
import YzhTable from '@/yzh/components/table/YzhTable.vue'
import type { PageParams, SearchField, YzhTableColumnV4 } from '@/yzh/components/table/types'

const tableRef = ref()
const selectedRows = ref<Enterprise[]>([])

const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const formData = reactive<Partial<Enterprise>>({})
const formRef = ref()
const submitting = ref(false)

const columns: YzhTableColumnV4<Enterprise>[] = [
  { prop: 'EntCode', label: '企业代码', width: 140, fixed: 'left' },
  { prop: 'EntName', label: '企业名称', width: 240, showOverflowTooltip: true, className: 'yzh-cell-ellipsis' },
  { prop: 'EntShortName', label: '简称', width: 120 },
  { prop: 'IndustryName', label: '行业', width: 120, align: 'center' },
  { prop: 'ScaleName', label: '规模', width: 100, align: 'center' },
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
  { prop: 'keyword', label: '关键词', type: 'text', placeholder: '企业代码 / 名称' },
  { prop: 'Industry', label: '行业', type: 'text' },
  { prop: 'Scale', label: '规模', type: 'text' },
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
  return getEnterprisePage(params)
}

function onAdd() {
  dialogMode.value = 'add'
  Object.assign(formData, {
    Id: undefined,
    EntCode: '',
    EntName: '',
    EntShortName: '',
    Industry: '',
    Scale: '',
    Country: '中国',
    Province: '',
    City: '',
    Address: '',
    ContactName: '',
    ContactPhone: '',
    ContactEmail: '',
    LegalPerson: '',
    EstablishDate: '',
    Enable: 1,
    Remark: ''
  })
  dialogVisible.value = true
}

function onEdit(row: Enterprise) {
  dialogMode.value = 'edit'
  Object.assign(formData, { ...row })
  dialogVisible.value = true
}

async function onDelete(row: Enterprise) {
  try {
    await ElMessageBox.confirm(`确定删除企业「${row.EntName}」吗？`, '删除确认', { type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteEnterprise(row.Id)
    ElMessage.success('删除成功')
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

async function onBatchDelete() {
  if (!selectedRows.value.length) {
    ElMessage.warning('请先选择要删除的企业')
    return
  }
  try {
    await ElMessageBox.confirm(`确定删除选中的 ${selectedRows.value.length} 家企业吗？`, '批量删除', { type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteEnterprise(selectedRows.value.map((r: Enterprise) => r.Id))
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
    await saveEnterprise(formData as Enterprise)
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
  { prop: 'EntCode', label: '企业代码', type: 'text', required: true, span: 12 },
  { prop: 'EntName', label: '企业名称', type: 'text', required: true, span: 12 },
  { prop: 'EntShortName', label: '简称', type: 'text', span: 12 },
  { prop: 'Industry', label: '行业', type: 'text', span: 12 },
  { prop: 'Scale', label: '规模', type: 'text', span: 12 },
  { prop: 'Country', label: '国家', type: 'text', span: 12 },
  { prop: 'Province', label: '省份', type: 'text', span: 8 },
  { prop: 'City', label: '城市', type: 'text', span: 8 },
  { prop: 'Address', label: '地址', type: 'text', span: 8 },
  { prop: 'ContactName', label: '联系人', type: 'text', span: 12 },
  { prop: 'ContactPhone', label: '联系电话', type: 'text', span: 12 },
  { prop: 'ContactEmail', label: '邮箱', type: 'text', span: 12 },
  { prop: 'LegalPerson', label: '法人', type: 'text', span: 12 },
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
  <div class="yzh-enterprise-page">
    <YzhTable
      ref="tableRef"
      :columns="columns"
      :data-loader="loadData"
      :search-fields="searchFields"
      :page-size="20"
      selectable
      :toolbar="{ refresh: true, columnSetting: true, density: true }"
      @selection-change="(rows: Enterprise[]) => (selectedRows = rows)"
    >
      <template #toolbar-left>
        <el-button type="primary" :icon="Plus" @click="onAdd">新增企业</el-button>
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
      :title="dialogMode === 'add' ? '新增企业' : '编辑企业'"
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
  </div>
</template>
