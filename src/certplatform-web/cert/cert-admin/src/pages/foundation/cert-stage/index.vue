<script setup lang="ts">
import { YzhTable, YzhForm, type YzhTableColumn, type YzhFormField, type PageParams, type SearchField } from '@yzh-core'
import { ref, reactive, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { CertStage } from '@share/types/cert'
import {
  getCertStagePage,
  addCertStage,
  updateCertStage,
  deleteCertStage,
  toggleCertStageValid
} from '@share/api/cert/cert-stage'

const tableRef = ref()
const formRef = ref()
const selectedRows = ref<CertStage[]>([])
const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
let formData = reactive<Partial<CertStage>>({})
const submitting = ref(false)

// ── 分类字典选项 ──
const categoryOptions = [
  { value: 'process', label: '流程阶段' },
  { value: 'audit', label: '审核阶段' },
  { value: 'post-cert', label: '证后阶段' }
]

// ── 表格列定义 ──
const columns: YzhTableColumn<CertStage>[] = [
  { prop: 'StageCode', label: '阶段编码', width: 120 },
  { prop: 'StageName', label: '阶段名称', width: 150 },
  { prop: 'Category', label: '分类', width: 110, formatter: (v: any) => ({ process: '流程阶段', audit: '审核阶段', 'post-cert': '证后阶段' }[v] ?? v) },
  { prop: 'SortOrder', label: '排序', width: 80 },
  { prop: 'Description', label: '说明', minWidth: 200, showOverflowTooltip: true },
  { prop: 'IsValid', label: '状态', width: 80, formatter: (v: any) => v === 1 ? '启用' : '停用' },
  { prop: 'CreateTime', label: '创建时间', width: 170 },
  { prop: 'actions', label: '操作', width: 200, fixed: 'right', slot: true }
]

// ── 搜索字段 ──
const searchFields: SearchField[] = [
  { prop: 'StageName', label: '阶段名称', type: 'text' },
  { prop: 'Category', label: '分类', type: 'select', options: categoryOptions }
]

// ── 表单字段定义 ──
const formFields = computed<YzhFormField[]>(() => [
  { prop: 'StageCode', label: '阶段编码', type: 'text', required: true, span: 12, placeholder: '如：AP/CR/S1/S2', disabled: dialogMode.value === 'edit' },
  { prop: 'StageName', label: '阶段名称', type: 'text', required: true, span: 12, placeholder: '如：申请受理' },
  { prop: 'Category', label: '分类', type: 'select', span: 12, options: categoryOptions },
  { prop: 'SortOrder', label: '排序', type: 'number', span: 12 },
  { prop: 'Description', label: '说明', type: 'textarea', span: 24 },
  { prop: 'Remark', label: '备注', type: 'textarea', span: 24 },
  { prop: 'IsValid', label: '状态', type: 'switch', span: 12, defaultValue: 1 }
])

// ── 数据加载 ──
async function loadData(params: PageParams) {
  const res = await getCertStagePage({
    Page: params.page,
    PageSize: params.rows,
    Filters: params.StageName ? [{ Field: 'StageName', Operator: 'like', Value: params.StageName }] : []
  })
  if (res.success && res.data) {
    return { rows: res.data.Items ?? [], total: res.data.TotalCount ?? 0 }
  }
  return { rows: [], total: 0 }
}

// ── 新增 ──
function onAdd() {
  dialogMode.value = 'add'
  formData = reactive<Partial<CertStage>>({ IsValid: 1, SortOrder: 0, Category: 'process' })
  dialogVisible.value = true
}

// ── 编辑 ──
function onEdit(row: CertStage) {
  dialogMode.value = 'edit'
  formData = reactive<Partial<CertStage>>({ ...row })
  dialogVisible.value = true
}

// ── 提交 ──
async function onSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  submitting.value = true
  try {
    const data = formData as CertStage
    if (dialogMode.value === 'add') {
      await addCertStage(data)
    } else {
      await updateCertStage(data)
    }
    ElMessage.success('保存成功')
    dialogVisible.value = false
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    submitting.value = false
  }
}

// ── 删除 ──
async function onDelete(row: CertStage) {
  try { await ElMessageBox.confirm(`确定删除阶段「${row.StageName}」吗？`, '删除确认', { type: 'warning' }) } catch { return }
  await deleteCertStage([row.Code!])
  ElMessage.success('删除成功')
  tableRef.value?.refresh()
}

// ── 切换启用/停用 ──
async function onToggle(row: CertStage) {
  if (!row.Code) return
  await toggleCertStageValid(row.Code)
  ElMessage.success('状态已更新')
  tableRef.value?.refresh()
}
</script>

<template>
  <div class="cert-stage-page">
    <YzhTable ref="tableRef" :columns="columns" :data-loader="loadData" :search-fields="searchFields" selectable @selection-change="selectedRows = $event">
      <template #toolbar-left>
        <el-button type="primary" @click="onAdd"><i class="bi bi-plus"></i> 新增</el-button>
        <el-button type="danger" plain :disabled="selectedRows.length === 0" @click="onDelete(selectedRows[0])"><i class="bi bi-trash"></i> 批量删除</el-button>
      </template>
      <template #column-actions="{ row }">
        <div class="action-cell">
          <el-button text type="primary" @click="onEdit(row)">编辑</el-button>
          <el-button text :type="row.IsValid === 1 ? 'warning' : 'success'" @click="onToggle(row)">
            {{ row.IsValid === 1 ? '禁用' : '启用' }}
          </el-button>
          <el-button text type="danger" @click="onDelete(row)">删除</el-button>
        </div>
      </template>
    </YzhTable>
    <el-dialog v-model="dialogVisible" :title="dialogMode === 'add' ? '新增认证阶段' : '编辑认证阶段'" width="600px">
      <YzhForm ref="formRef" v-model="formData" :fields="formFields" :loading="submitting" @submit="onSubmit" @reset="dialogVisible = false" />
    </el-dialog>
  </div>
</template>

<style scoped>
.cert-stage-page { display: flex; flex-direction: column; height: 100%; }
.action-cell { display: flex; flex-wrap: nowrap; gap: 2px; }
</style>
