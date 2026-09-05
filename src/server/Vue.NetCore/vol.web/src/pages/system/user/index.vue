<script setup lang="ts">
/**
 * 系统用户管理 - 新架构试点页面
 *
 * 演示：
 * - YzhTable（分页/搜索/排序/选择）
 * - YzhForm（新增/编辑）
 * - YzhSearchBar
 * - YzhApiClient（vol 格式兼容）
 *
 * 目的：作为"彻底抛弃 view-grid"的可复制模板
 */
import { getRoleOptions } from '@/yzh/api/system-role'
import {
  deleteUser,
  getUserPage,
  saveUser,
  toggleUserEnable,
  type SysUser
} from '@/yzh/api/system-user'
import { YzhForm, type YzhFormFieldV4 } from '@/yzh/components/form'
import YzhTable from '@/yzh/components/table/YzhTable.vue'
import type { PageParams, SearchField, YzhTableColumnV4 } from '@/yzh/components/table/types'

import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'

// ============== 状态 ==============
const tableRef = ref()
const selectedRows = ref<SysUser[]>([])

// 编辑弹窗
const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const formData = reactive<Partial<SysUser>>({})
const formRef = ref()
const submitting = ref(false)

// ============== 列定义 ==============
const columns: YzhTableColumnV4<SysUser>[] = [
  { prop: 'userName', label: '用户名', width: 140, sortable: true, fixed: 'left' },
  { prop: 'userTrueName', label: '真实姓名', width: 120 },
  { prop: 'roleName', label: '角色', width: 120 },
  {
    prop: 'enable',
    label: '状态',
    width: 80,
    align: 'center',
    formatter: (v: unknown) => (v === 1 ? '启用' : '禁用')
  },
  { prop: 'phoneNo', label: '手机号', width: 140 },
  { prop: 'email', label: '邮箱', width: 200 },
  {
    prop: 'createDate',
    label: '创建时间',
    width: 180,
    sortable: true,
    formatter: (v: unknown) => (v ? String(v).replace('T', ' ').slice(0, 19) : '-')
  },
  {
    prop: 'lastLoginDate',
    label: '最后登录',
    width: 180,
    formatter: (v: unknown) => (v ? String(v).slice(0, 19) : '从未')
  },
  { prop: 'remark', label: '备注', minWidth: 180, className: 'yzh-cell-ellipsis' },
  { prop: 'actions', label: '操作', width: 200, fixed: 'right', slot: 'actions' }
]

// ============== 搜索字段 ==============
const searchFields: SearchField[] = [
  { prop: 'userName', label: '用户名', type: 'text', placeholder: '请输入用户名' },
  { prop: 'userTrueName', label: '姓名', type: 'text', placeholder: '请输入真实姓名' },
  {
    prop: 'role_Id',
    label: '角色',
    type: 'select',
    options: [
      { label: '无', value: 10 },
      { label: '普通审核员', value: 202 },
      { label: '企业账号', value: 300 }
    ]
  },
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

// ============== 数据加载器 ==============
async function loadUsers(params: PageParams) {
  return getUserPage(params)
}

// ============== 操作 ==============
function onAdd() {
  dialogMode.value = 'add'
  Object.assign(formData, {
    user_Id: undefined,
    userName: '',
    userTrueName: '',
    role_Id: undefined,
    enable: 1,
    email: '',
    phoneNo: '',
    address: '',
    gender: 1,
    remark: ''
  })
  dialogVisible.value = true
}

function onEdit(row: SysUser) {
  dialogMode.value = 'edit'
  Object.assign(formData, { ...row })
  dialogVisible.value = true
}

async function onDelete(row: SysUser) {
  try {
    await ElMessageBox.confirm(
      `确定删除用户「${row.userTrueName || row.userName}」吗？该操作不可恢复。`,
      '删除确认',
      { type: 'warning', confirmButtonText: '确定删除', cancelButtonText: '取消' }
    )
  } catch {
    return
  }
  try {
    await deleteUser(row.user_Id)
    ElMessage.success('删除成功')
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

async function onBatchDelete() {
  if (selectedRows.value.length === 0) {
    ElMessage.warning('请先选择要删除的用户')
    return
  }
  try {
    await ElMessageBox.confirm(
      `确定删除选中的 ${selectedRows.value.length} 个用户吗？`,
      '批量删除',
      { type: 'warning', confirmButtonText: '确定', cancelButtonText: '取消' }
    )
  } catch {
    return
  }
  try {
    const ids = selectedRows.value.map((r: SysUser) => r.user_Id)
    await deleteUser(ids)
    ElMessage.success(`已删除 ${ids.length} 个用户`)
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '批量删除失败')
  }
}

async function onToggleEnable(row: SysUser) {
  const next = row.enable === 1 ? 0 : 1
  try {
    await toggleUserEnable([row.user_Id], next === 1)
    ElMessage.success(next === 1 ? '已启用' : '已禁用')
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '操作失败')
  }
}

async function onSubmit() {
  const valid = await formRef.value?.validate()
  if (!valid) return
  submitting.value = true
  try {
    await saveUser(formData as SysUser)
    ElMessage.success(dialogMode.value === 'add' ? '新增成功' : '保存成功')
    dialogVisible.value = false
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    submitting.value = false
  }
}

function onSelectionChange(rows: SysUser[]) {
  selectedRows.value = rows
}

// ============== 表单字段 ==============
const formFields: YzhFormFieldV4[] = [
  { prop: 'userName', label: '用户名', type: 'text', required: true, span: 12 },
  { prop: 'userTrueName', label: '真实姓名', type: 'text', required: true, span: 12 },
  {
    prop: 'role_Id',
    label: '角色',
    type: 'select',
    required: true,
    span: 12,
    loadOptions: getRoleOptions
  },
  {
    prop: 'enable',
    label: '状态',
    type: 'switch',
    span: 12,
    fieldProps: { activeText: '启用', inactiveText: '禁用' }
  },
  { prop: 'email', label: '邮箱', type: 'text', span: 12 },
  { prop: 'phoneNo', label: '手机号', type: 'text', span: 12 },
  {
    prop: 'gender',
    label: '性别',
    type: 'radio',
    span: 12,
    options: [
      { label: '男', value: 1 },
      { label: '女', value: 0 }
    ]
  },
  { prop: 'remark', label: '备注', type: 'textarea', span: 24, fieldProps: { rows: 3 } }
]
</script>

<template>
  <div class="yzh-user-page">
    <YzhTable
      ref="tableRef"
      :columns="columns"
      :data-loader="loadUsers"
      :search-fields="searchFields"
      :page-size="10"
      :search-max-fields="2"
      selectable
      :toolbar="{ columnSetting: true }"
      @selection-change="onSelectionChange"
    >
      <template #toolbar-left>
        <el-button type="primary" @click="onAdd">
          <i class="bi bi-plus-lg"></i> 新增用户
        </el-button>
        <el-button
          type="danger"
          plain
          :disabled="selectedRows.length === 0"
          @click="onBatchDelete"
        >
          <i class="bi bi-trash"></i> 批量删除
          <span v-if="selectedRows.length" style="margin-left: 4px; opacity: 0.8">
            ({{ selectedRows.length }})
          </span>
        </el-button>
        <el-button @click="tableRef?.refresh()">
          <i class="bi bi-arrow-clockwise"></i> 刷新
        </el-button>
      </template>

      <template #column-actions="{ row }">
        <el-button text type="primary" @click="onEdit(row)">
          <i class="bi bi-pencil"></i> 编辑
        </el-button>
        <el-button
          text
          :type="row.enable === 1 ? 'warning' : 'success'"
          @click="onToggleEnable(row)"
        >
          <i class="bi bi-toggle-on"></i> {{ row.enable === 1 ? '禁用' : '启用' }}
        </el-button>
        <el-button text type="danger" @click="onDelete(row)">
          <i class="bi bi-trash"></i> 删除
        </el-button>
      </template>
    </YzhTable>

    <!-- 新增/编辑弹窗 -->
    <el-dialog
      v-model="dialogVisible"
      :title="dialogMode === 'add' ? '新增用户' : '编辑用户'"
      width="720px"
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
        label-width="100px"
        submit-text="保存"
        reset-text="取消"
        @submit="onSubmit"
        @reset="dialogVisible = false"
      />
    </el-dialog>
  </div>
</template>

<style scoped>
.yzh-user-page {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--yzh-color-bg-page, #f5f7fa);
  padding: 0;
}
</style>
