<script setup lang="ts">
import { YzhForm, type YzhFormField } from '@yzh-core/components/form'
import YzhTable from '@yzh-core/components/table/YzhTable.vue'
import type { PageParams, SearchField, YzhTableColumn } from '@yzh-core/components/table/types'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import { getUserPage, saveUser, deleteUser } from '@share/api/system-user'
import type { SysUser } from '@share/api/system-user'

const tableRef = ref()
const selectedRows = ref<SysUser[]>([])
const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
let formData: Partial<SysUser> = reactive<Partial<SysUser>>({})
const formRef = ref()
const submitting = ref(false)

const columns: YzhTableColumn<SysUser>[] = [
  { prop: 'userName', label: '用户名', width: 140, sortable: true, fixed: 'left' },
  { prop: 'userTrueName', label: '真实姓名', width: 120 },
  { prop: 'enable', label: '状态', width: 80, align: 'center', formatter: (v) => v === 1 ? '启用' : '禁用' },
  { prop: 'phoneNo', label: '手机号', width: 140 },
  { prop: 'email', label: '邮箱', width: 200 },
  { prop: 'createDate', label: '创建时间', width: 180 },
  { prop: 'actions', label: '操作', width: 180, fixed: 'right', slot: true }
]

const searchFields: SearchField[] = [
  { prop: 'userName', label: '用户名', type: 'text' },
  { prop: 'userTrueName', label: '姓名', type: 'text' }
]

async function loadUsers(params: PageParams) { return getUserPage(params) }

function onAdd() {
  dialogMode.value = 'add'
  formData = reactive<Partial<SysUser>>({ user_Id: undefined, userName: '', userTrueName: '', enable: 1, remark: '' })
  dialogVisible.value = true
}

function onEdit(row: SysUser) {
  dialogMode.value = 'edit'
  formData = reactive<Partial<SysUser>>({ ...row })
  dialogVisible.value = true
}

async function onDelete(row: SysUser) {
  try { await ElMessageBox.confirm(`确定删除用户「${row.userTrueName}」吗？`, '删除确认', { type: 'warning' }) } catch { return }
  await deleteUser(row.user_Id)
  ElMessage.success('删除成功')
  tableRef.value?.refresh()
}

const formFields: YzhFormField[] = [
  { prop: 'userName', label: '用户名', type: 'text', required: true, span: 12 },
  { prop: 'userTrueName', label: '真实姓名', type: 'text', required: true, span: 12 },
  { prop: 'enable', label: '状态', type: 'switch', span: 12 },
  { prop: 'remark', label: '备注', type: 'textarea', span: 24 }
]

async function onSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  submitting.value = true
  try { await saveUser(formData as SysUser); ElMessage.success('保存成功'); dialogVisible.value = false; tableRef.value?.refresh() }
  catch (e: any) { ElMessage.error(e?.message || '保存失败') }
  finally { submitting.value = false }
}
</script>

<template>
  <div class="user-page">
    <YzhTable ref="tableRef" :columns="columns" :data-loader="loadUsers" :search-fields="searchFields" selectable @selection-change="selectedRows = $event">
      <template #toolbar-left>
        <el-button type="primary" @click="onAdd"><i class="bi bi-plus"></i> 新增</el-button>
        <el-button type="danger" plain :disabled="selectedRows.length === 0" @click="onDelete(selectedRows[0])"><i class="bi bi-trash"></i> 批量删除</el-button>
      </template>
      <template #column-actions="{ row }">
        <el-button text type="primary" @click="onEdit(row)">编辑</el-button>
        <el-button text type="danger" @click="onDelete(row)">删除</el-button>
      </template>
    </YzhTable>
    <el-dialog v-model="dialogVisible" :title="dialogMode === 'add' ? '新增用户' : '编辑用户'" width="600px">
      <YzhForm ref="formRef" v-model="formData" :fields="formFields" :loading="submitting" @submit="onSubmit" @reset="dialogVisible = false" />
    </el-dialog>
  </div>
</template>

<style scoped>
.user-page { display: flex; flex-direction: column; height: 100%; }
</style>
