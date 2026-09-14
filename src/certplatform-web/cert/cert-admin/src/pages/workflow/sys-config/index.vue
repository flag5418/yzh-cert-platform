<script setup lang="ts">
/**
 * 系统参数配置 — YzhTable + YzhForm 配置驱动
 */
import { ref, reactive } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus } from '@element-plus/icons-vue'
import {
  YzhTable,
  YzhForm,
  YzhPageLayout,
  type YzhTableColumn,
  type YzhFormField,
  type PageParams,
  type SearchField
} from '@yzh-core'
import {
  getParamList,
  getParamPage,
  saveParam,
  deleteParam,
  type SysParam
} from '@/api/system/param'

const tableRef = ref()
const formRef = ref()
const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
let formData = reactive<Partial<SysParam>>({})
const submitting = ref(false)

// ── 表格列定义 ──
const columns: YzhTableColumn<SysParam>[] = [
  { prop: 'paramCode', label: '参数编码', width: 200 },
  { prop: 'paramName', label: '参数名称', width: 180 },
  {
    prop: 'paramType', label: '类型', width: 100, align: 'center',
    formatter: (v: any) => ({ string: '字符串', number: '数字', boolean: '布尔', json: 'JSON' }[v] ?? v)
  },
  { prop: 'paramValue', label: '参数值', minWidth: 200 },
  { prop: 'description', label: '说明', minWidth: 200 },
  { prop: 'actions', label: '操作', width: 150, fixed: 'right', slot: true }
]

// ── 搜索字段 ──
const searchFields: SearchField[] = [
  { prop: 'paramCode', label: '参数编码', type: 'text' },
  { prop: 'paramName', label: '参数名称', type: 'text' }
]

// ── 表单字段定义 ──
const formFields: YzhFormField[] = [
  { prop: 'paramCode', label: '参数编码', type: 'text', required: true, span: 12, placeholder: '如：AI_Qwen_Model' },
  { prop: 'paramName', label: '参数名称', type: 'text', required: true, span: 12, placeholder: '如：千问AI模型' },
  { prop: 'paramType', label: '参数类型', type: 'select', required: true, span: 12, options: [
    { label: '字符串', value: 'string' },
    { label: '数字', value: 'number' },
    { label: '布尔', value: 'boolean' },
    { label: 'JSON', value: 'json' }
  ] },
  { prop: 'paramValue', label: '参数值', type: 'text', span: 12 },
  { prop: 'description', label: '说明', type: 'textarea', span: 24 }
]

// ── 数据加载（YzhTable 期望返回 Page<T> = { rows, total }）──
async function loadData(params: PageParams) {
  const res = await getParamPage(params)
  return { rows: res?.rows ?? [], total: res?.total ?? 0 }
}

// ── 新增 ──
function onAdd() {
  dialogMode.value = 'add'
  formData = reactive<Partial<SysParam>>({ paramType: 'string' })
  dialogVisible.value = true
}

// ── 编辑 ──
function onEdit(row: SysParam) {
  dialogMode.value = 'edit'
  formData = reactive<Partial<SysParam>>({ ...row })
  dialogVisible.value = true
}

// ── 提交 ──
async function onSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  submitting.value = true
  try {
    await saveParam(formData as SysParam)
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
async function onDelete(row: SysParam) {
  try { await ElMessageBox.confirm(`确定删除参数「${row.paramName}」？`, '确认删除', { type: 'warning' }) } catch { return }
  await deleteParam(row.id!)
  ElMessage.success('删除成功')
  tableRef.value?.refresh()
}
</script>

<template>
  <YzhPageLayout title="系统参数配置">
    <YzhTable ref="tableRef" :columns="columns" :data-loader="loadData" :search-fields="searchFields">
      <template #toolbar-left>
        <el-button type="primary" :icon="Plus" @click="onAdd">新建参数</el-button>
      </template>
      <template #column-actions="{ row }">
        <el-button link type="primary" size="small" @click="onEdit(row)">编辑</el-button>
        <el-button link type="danger" size="small" @click="onDelete(row)">删除</el-button>
      </template>
    </YzhTable>

    <el-dialog v-model="dialogVisible" :title="dialogMode === 'add' ? '新建参数' : '编辑参数'" width="600px">
      <YzhForm ref="formRef" v-model="formData" :fields="formFields" :loading="submitting" @submit="onSubmit" @reset="dialogVisible = false" />
    </el-dialog>
  </YzhPageLayout>
</template>

<style scoped>
</style>
