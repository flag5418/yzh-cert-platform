<script setup lang="ts">
/**
 * 认证机构管理（机构-Attach 试点）
 *
 * 形态：标准单表页面（与 foundation/cert-stage 同构）
 * - 列表 / 搜索 / 新增 / 编辑 / 删除 / 启用禁用
 * - 唯一差异在后端：新增/修改/删除/启停时会同步 Sys_Organization 机构记录
 */
import { YzhTable, YzhForm, type YzhTableColumn, type YzhFormField, type PageParams, type SearchField } from '@yzh-core'
import { ref, reactive, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { CertificationBody } from '@share/types/cert'
import {
  getCertificationBodyPage,
  addCertificationBody,
  updateCertificationBody,
  deleteCertificationBody,
  toggleCertificationBodyValid
} from '@share/api/cert/certification-body'

const tableRef = ref()
const formRef = ref()
const selectedRows = ref<CertificationBody[]>([])
const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
let formData = reactive<Partial<CertificationBody>>({})
const submitting = ref(false)

// ── 运营状态选项 ──
const statusOptions = [
  { value: 'active', label: '正常' },
  { value: 'suspended', label: '暂停' },
  { value: 'inactive', label: '停用' }
]

// ── 表格列定义 ──
const columns: YzhTableColumn<CertificationBody>[] = [
  { prop: 'Name', label: '机构名称', minWidth: 220, showOverflowTooltip: true },
  { prop: 'ShortName', label: '简称', width: 140 },
  { prop: 'CbCode', label: '机构编号', width: 140 },
  { prop: 'LegalPerson', label: '法人代表', width: 120 },
  { prop: 'ContactName', label: '联系人', width: 120 },
  { prop: 'ContactPhone', label: '联系电话', width: 140 },
  { prop: 'MaxUsers', label: '最大用户数', width: 110 },
  { prop: 'MaxEnterprises', label: '最大企业数', width: 110 },
  { prop: 'IsValid', label: '状态', width: 90, formatter: (v: any) => (v === 0 ? '已禁用' : '启用') },
  { prop: 'CreateTime', label: '创建时间', width: 170 },
  { prop: 'actions', label: '操作', width: 200, fixed: 'right', slot: true }
]

// ── 搜索字段 ──
const searchFields: SearchField[] = [
  { prop: 'Name', label: '机构名称', type: 'text' },
  { prop: 'CbCode', label: '机构编号', type: 'text' }
]

// ── 表单字段定义 ──
const formFields = computed<YzhFormField[]>(() => [
  { prop: 'Name', label: '机构名称', type: 'text', required: true, span: 12, placeholder: '请输入机构全称' },
  { prop: 'ShortName', label: '简称', type: 'text', span: 12, placeholder: '请输入简称' },
  { prop: 'CbCode', label: '机构编号', type: 'text', span: 12, placeholder: '如：CB001（CNAS 认可编号）' },
  { prop: 'LegalPerson', label: '法人代表', type: 'text', span: 12 },
  { prop: 'ContactName', label: '联系人', type: 'text', span: 12 },
  { prop: 'ContactPhone', label: '联系电话', type: 'text', span: 12 },
  { prop: 'ContactEmail', label: '联系邮箱', type: 'text', span: 24 },
  { prop: 'Address', label: '地址', type: 'text', span: 24 },
  { prop: 'ScopeText', label: '认证范围', type: 'textarea', span: 24 },
  { prop: 'MaxUsers', label: '最大用户数', type: 'number', span: 12 },
  { prop: 'MaxEnterprises', label: '最大企业数', type: 'number', span: 12 },
  { prop: 'ExpireDate', label: '到期日期', type: 'date', span: 12 },
  { prop: 'Status', label: '运营状态', type: 'select', span: 12, options: statusOptions, defaultValue: 'active' },
  { prop: 'Sort', label: '排序', type: 'number', span: 12 },
  { prop: 'Remark', label: '备注', type: 'textarea', span: 24 }
])

// ── 数据加载 ──
async function loadData(params: PageParams) {
  const filters: Array<{ Field: string; Operator: string; Value: any }> = []
  if (params.Name) filters.push({ Field: 'Name', Operator: 'like', Value: params.Name })
  if (params.CbCode) filters.push({ Field: 'CbCode', Operator: 'like', Value: params.CbCode })

  const res = await getCertificationBodyPage({
    Page: params.page,
    PageSize: params.rows,
    Filters: filters
  })
  if (res.success && res.data) {
    return { rows: res.data.Items ?? [], total: res.data.TotalCount ?? 0 }
  }
  return { rows: [], total: 0 }
}

// ── 新增 ──
function onAdd() {
  dialogMode.value = 'add'
  formData = reactive<Partial<CertificationBody>>({
    IsValid: 1,
    Status: 'active',
    Sort: 0,
    MaxUsers: 100,
    MaxEnterprises: 1000
  })
  dialogVisible.value = true
}

// ── 编辑 ──
function onEdit(row: CertificationBody) {
  dialogMode.value = 'edit'
  formData = reactive<Partial<CertificationBody>>({ ...row })
  dialogVisible.value = true
}

// ── 提交 ──
async function onSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  submitting.value = true
  try {
    const data = formData as CertificationBody
    if (dialogMode.value === 'add') {
      await addCertificationBody(data)
    } else {
      await updateCertificationBody(data)
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
async function onDelete(row: CertificationBody) {
  try {
    await ElMessageBox.confirm(`确定删除认证机构「${row.Name}」吗？同时会删除对应的系统机构记录。`, '删除确认', { type: 'warning' })
  } catch {
    return
  }
  await deleteCertificationBody([row.Code!])
  ElMessage.success('删除成功')
  tableRef.value?.refresh()
}

// ── 启用 / 禁用（同步 Sys_Organization.Enable）──
async function onToggle(row: CertificationBody) {
  if (!row.Code) return
  await toggleCertificationBodyValid(row.Code)
  ElMessage.success(row.IsValid === 1 ? '已禁用' : '已启用')
  tableRef.value?.refresh()
}
</script>

<template>
  <div class="cert-body-page">
    <YzhTable
      ref="tableRef"
      :columns="columns"
      :data-loader="loadData"
      :search-fields="searchFields"
      selectable
      @selection-change="selectedRows = $event"
    >
      <template #toolbar-left>
        <el-button type="primary" @click="onAdd"><i class="bi bi-plus"></i> 新增</el-button>
        <el-button
          type="danger"
          plain
          :disabled="selectedRows.length === 0"
          @click="onDelete(selectedRows[0])"
        >
          <i class="bi bi-trash"></i> 批量删除
        </el-button>
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

    <el-dialog
      v-model="dialogVisible"
      :title="dialogMode === 'add' ? '新增认证机构' : '编辑认证机构'"
      width="760px"
    >
      <YzhForm
        ref="formRef"
        v-model="formData"
        :fields="formFields"
        :loading="submitting"
        @submit="onSubmit"
        @reset="dialogVisible = false"
      />
    </el-dialog>
  </div>
</template>

<style scoped>
.cert-body-page {
  padding: 20px;
  display: flex;
  flex-direction: column;
  height: 100%;
}
.action-cell {
  display: flex;
  flex-wrap: nowrap;
  gap: 2px;
}
</style>
