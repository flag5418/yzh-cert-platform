<script setup lang="ts">
/**
 * 系统角色管理 - 新架构实现
 */
import { deleteRole, getRolePage, saveRole, type SysRole } from '@/yzh/api/system-role'
import { YzhForm, type YzhFormFieldV4 } from '@/yzh/components/form'
import YzhTable from '@/yzh/components/table/YzhTable.vue'
import type { PageParams, SearchField, YzhTableColumnV4 } from '@/yzh/components/table/types'
import { Delete, Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'

const tableRef = ref()
const selectedRows = ref<SysRole[]>([])

const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const formData = reactive<Partial<SysRole>>({})
const formRef = ref()
const submitting = ref(false)

const columns: YzhTableColumnV4<SysRole>[] = [
  { prop: 'roleName', label: '角色名称', width: 200, fixed: 'left' },
  { prop: 'roleCode', label: '角色编码', width: 200 },
  {
    prop: 'enable',
    label: '状态',
    width: 80,
    align: 'center',
    formatter: (v: unknown) => (v === 1 ? '启用' : '禁用')
  },
  { prop: 'creator', label: '创建人', width: 120 },
  {
    prop: 'createDate',
    label: '创建时间',
    width: 180,
    formatter: (v: unknown) => (v ? String(v).replace('T', ' ').slice(0, 19) : '-')
  },
  { prop: 'remark', label: '备注', minWidth: 200, className: 'yzh-cell-ellipsis' },
  { prop: 'actions', label: '操作', width: 160, fixed: 'right', slot: 'actions' }
]

const searchFields: SearchField[] = [
  { prop: 'roleName', label: '角色名称', type: 'text', placeholder: '请输入角色名称' },
  { prop: 'roleCode', label: '角色编码', type: 'text', placeholder: '请输入角色编码' },
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

async function loadRoles(params: PageParams) {
  return getRolePage(params)
}

function onAdd() {
  dialogMode.value = 'add'
  Object.assign(formData, { role_Id: undefined, roleName: '', roleCode: '', enable: 1, remark: '' })
  dialogVisible.value = true
}

function onEdit(row: SysRole) {
  dialogMode.value = 'edit'
  Object.assign(formData, { ...row })
  dialogVisible.value = true
}

async function onDelete(row: SysRole) {
  try {
    await ElMessageBox.confirm(`确定删除角色「${row.roleName}」吗？`, '删除确认', {
      type: 'warning'
    })
  } catch {
    return
  }
  try {
    await deleteRole(row.role_Id)
    ElMessage.success('删除成功')
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

async function onBatchDelete() {
  if (!selectedRows.value.length) {
    ElMessage.warning('请先选择要删除的角色')
    return
  }
  try {
    await ElMessageBox.confirm(
      `确定删除选中的 ${selectedRows.value.length} 个角色吗？`,
      '批量删除',
      {
        type: 'warning'
      }
    )
  } catch {
    return
  }
  try {
    await deleteRole(selectedRows.value.map((r: SysRole) => r.role_Id))
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
    await saveRole(formData as SysRole)
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
  { prop: 'roleName', label: '角色名称', type: 'text', required: true, span: 12 },
  { prop: 'roleCode', label: '角色编码', type: 'text', required: true, span: 12 },
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
  <div class="yzh-role-page">
    <YzhTable
      ref="tableRef"
      :columns="columns"
      :data-loader="loadRoles"
      :search-fields="searchFields"
      :page-size="10"
      selectable
      :toolbar="{ refresh: true, columnSetting: true, density: true }"
      @selection-change="(rows: SysRole[]) => (selectedRows = rows)"
    >
      <template #toolbar-left>
        <el-button type="primary" :icon="Plus" @click="onAdd">新增角色</el-button>
        <el-button
          type="danger"
          plain
          :icon="Delete"
          :disabled="!selectedRows.length"
          @click="onBatchDelete"
        >
          批量删除
          <span v-if="selectedRows.length" style="margin-left: 4px; opacity: 0.8"
            >({{ selectedRows.length }})</span
          >
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
      :title="dialogMode === 'add' ? '新增角色' : '编辑角色'"
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
