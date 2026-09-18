<script setup lang="ts">
import { YzhTable, YzhForm, type YzhTableColumn, type YzhFormField, type PageParams, type SearchField } from '@yzh-core'
import { ref, reactive, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { PhaseDefinition } from '@share/types/cert'
import {
  getPhaseDefinitionPage,
  addPhaseDefinition,
  updatePhaseDefinition,
  deletePhaseDefinition,
  togglePhaseDefinitionValid
} from '@share/api/cert/phase-definition'

const tableRef = ref()
const formRef = ref()
const selectedRows = ref<PhaseDefinition[]>([])
const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
let formData = reactive<Partial<PhaseDefinition>>({})
const submitting = ref(false)

// ── 表格列定义 ──
const columns: YzhTableColumn<PhaseDefinition>[] = [
  { prop: 'PhaseCode', label: '阶段编码', width: 120 },
  { prop: 'PhaseName', label: '阶段名称', width: 150 },
  { prop: 'SequenceOrder', label: '顺序', width: 80 },
  { prop: 'Description', label: '说明', minWidth: 200 },
  { prop: 'IsValid', label: '状态', width: 80, formatter: (v: any) => v === 1 ? '启用' : '停用' },
  { prop: 'CreateTime', label: '创建时间', width: 180 },
  { prop: 'actions', label: '操作', width: 200, fixed: 'right', slot: true }
]

// ── 搜索字段 ──
const searchFields: SearchField[] = [
  { prop: 'PhaseName', label: '阶段名称', type: 'text' }
]

// ── 表单字段定义（与 EntityConfig 对齐） ──
const formFields = computed<YzhFormField[]>(() => [
  { prop: 'PhaseCode', label: '阶段编码', type: 'text', required: true, span: 12, disabled: dialogMode.value === 'edit' },
  { prop: 'PhaseName', label: '阶段名称', type: 'text', required: true, span: 12 },
  { prop: 'SequenceOrder', label: '顺序', type: 'number', span: 12 },
  { prop: 'Description', label: '说明', type: 'textarea', span: 24 },
  { prop: 'IsValid', label: '状态', type: 'switch', span: 12, defaultValue: 1 }
])

// ── 数据加载（YzhTable 期望返回 Page<T> = { rows, total }）──
async function loadData(params: PageParams) {
  const res = await getPhaseDefinitionPage({
    Page: params.page,
    PageSize: params.rows,
    Filters: params.PhaseName ? [{ Field: 'PhaseName', Operator: 'like', Value: params.PhaseName }] : []
  })
  if (res.success && res.data) {
    return { rows: res.data.Items ?? [], total: res.data.TotalCount ?? 0 }
  }
  return { rows: [], total: 0 }
}

// ── 新增 ──
function onAdd() {
  dialogMode.value = 'add'
  formData = reactive<Partial<PhaseDefinition>>({ IsValid: 1 })
  dialogVisible.value = true
}

// ── 编辑 ──
function onEdit(row: PhaseDefinition) {
  dialogMode.value = 'edit'
  formData = reactive<Partial<PhaseDefinition>>({ ...row })
  dialogVisible.value = true
}

// ── 提交 ──
async function onSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  submitting.value = true
  try {
    const data = formData as PhaseDefinition
    if (dialogMode.value === 'add') {
      await addPhaseDefinition(data)
    } else {
      await updatePhaseDefinition(data)
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
async function onDelete(row: PhaseDefinition) {
  try { await ElMessageBox.confirm(`确定删除阶段「${row.PhaseName}」吗？`, '删除确认', { type: 'warning' }) } catch { return }
  await deletePhaseDefinition([row.Code!])
  ElMessage.success('删除成功')
  tableRef.value?.refresh()
}

// ── 切换启用/停用 ──
async function onToggle(row: PhaseDefinition) {
  if (!row.Code) return
  await togglePhaseDefinitionValid(row.Code)
  ElMessage.success('状态已更新')
  tableRef.value?.refresh()
}
</script>

<template>
  <div class="phase-definition-page">
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
    <el-dialog v-model="dialogVisible" :title="dialogMode === 'add' ? '新增认证阶段' : '编辑认证阶段'" width="500px">
      <YzhForm ref="formRef" v-model="formData" :fields="formFields" :loading="submitting" @submit="onSubmit" @reset="dialogVisible = false" />
    </el-dialog>
  </div>
</template>

<style scoped>
.phase-definition-page { display: flex; flex-direction: column; height: 100%; }
.action-cell { display: flex; flex-wrap: nowrap; gap: 2px; }
</style>
