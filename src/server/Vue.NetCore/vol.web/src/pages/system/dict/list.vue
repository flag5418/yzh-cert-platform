<script setup lang="ts">
/**
 * 字典详情管理 - 新架构实现
 */
import { Delete, Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import {
  deleteDictListItem,
  getDictListPage,
  saveDictListItem,
  type SysDictionaryList
} from '@/yzh/api/system-dict'
import { YzhForm, type YzhFormFieldV4 } from '@/yzh/components/form'
import YzhTable from '@/yzh/components/table/YzhTable.vue'
import type { PageParams, SearchField, YzhTableColumnV4 } from '@/yzh/components/table/types'

const route = useRoute()
const dicId = Number(route.query.dicId)
const dicName = String(route.query.dicName || '字典详情')

const tableRef = ref()
const selectedRows = ref<SysDictionaryList[]>([])

const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const formData = reactive<Partial<SysDictionaryList>>({})
const formRef = ref()
const submitting = ref(false)

const columns: YzhTableColumnV4<SysDictionaryList>[] = [
  { prop: 'dicName', label: '字典项名称', width: 200, fixed: 'left' },
  { prop: 'dicValue', label: '字典项值', width: 200 },
  { prop: 'orderNo', label: '排序', width: 80, align: 'center' },
  {
    prop: 'enable',
    label: '状态',
    width: 80,
    align: 'center',
    formatter: (v: unknown) => (v === 1 ? '启用' : '禁用')
  },
  { prop: 'remark', label: '备注', minWidth: 200, className: 'yzh-cell-ellipsis' },
  { prop: 'actions', label: '操作', width: 160, fixed: 'right', slot: 'actions' }
]

const searchFields: SearchField[] = [
  { prop: 'dicName', label: '字典项名称', type: 'text', placeholder: '请输入字典项名称' }
]

async function loadItems(params: PageParams) {
  return getDictListPage(dicId, params)
}

function onAdd() {
  dialogMode.value = 'add'
  Object.assign(formData, {
    dicList_Id: undefined,
    dic_Id: dicId,
    dicName: '',
    dicValue: '',
    orderNo: 0,
    enable: 1,
    remark: ''
  })
  dialogVisible.value = true
}

function onEdit(row: SysDictionaryList) {
  dialogMode.value = 'edit'
  Object.assign(formData, { ...row })
  dialogVisible.value = true
}

async function onDelete(row: SysDictionaryList) {
  try {
    await ElMessageBox.confirm(`确定删除字典项「${row.dicName}」吗？`, '删除确认', { type: 'warning' })
  } catch {
    return
  }
  try {
    await deleteDictListItem(row.dicList_Id)
    ElMessage.success('删除成功')
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

async function onSubmit() {
  const valid = await formRef.value?.validate()
  if (!valid) return
  submitting.value = true
  try {
    await saveDictListItem(formData as SysDictionaryList)
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
  { prop: 'dicName', label: '字典项名称', type: 'text', required: true, span: 12 },
  { prop: 'dicValue', label: '字典项值', type: 'text', required: true, span: 12 },
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
  <div class="yzh-dict-list-page">
    <div class="page-header">
      <h3>{{ dicName }} - 字典项</h3>
    </div>
    <YzhTable
      ref="tableRef"
      :columns="columns"
      :data-loader="loadItems"
      :search-fields="searchFields"
      :page-size="10"
      selectable
      :toolbar="{ refresh: true, columnSetting: true, density: true }"
    >
      <template #toolbar-left>
        <el-button type="primary" :icon="Plus" @click="onAdd">新增字典项</el-button>
        <el-button :icon="Refresh" @click="tableRef?.refresh()">刷新</el-button>
      </template>
      <template #column-actions="{ row }">
        <el-button text type="primary" :icon="Edit" @click="onEdit(row)">编辑</el-button>
        <el-button text type="danger" :icon="Delete" @click="onDelete(row)">删除</el-button>
      </template>
    </YzhTable>

    <el-dialog
      v-model="dialogVisible"
      :title="dialogMode === 'add' ? '新增字典项' : '编辑字典项'"
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

<style scoped>
.page-header {
  padding: 12px 16px 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.page-header h3 {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
  color: #1f2329;
}
</style>
