<script setup lang="ts">
import { YzhTable } from '@yzh-core/components/table'
import { YzhForm } from '@yzh-core/components/form'
import { onMounted, ref } from 'vue'
import type { PhaseDefinition } from '@share/types/cert'
import {
  getPhaseDefinitionPage,
  addPhaseDefinition,
  updatePhaseDefinition,
  deletePhaseDefinition,
  togglePhaseDefinitionValid
} from '@share/api/cert/phase-definition'

// ============================================================
// 状态
// ============================================================
const loading = ref(false)
const submitting = ref(false)
const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const formData = ref<Partial<PhaseDefinition>>({})
const selectedRows = ref<PhaseDefinition[]>([])
const tableData = ref<PhaseDefinition[]>([])
const total = ref(0)
const searchParams = ref({ keyword: '' })
const pagination = ref({ page: 1, pageSize: 20 })

// ============================================================
// 表格列定义
// ============================================================
const columns = [
  { prop: 'PhaseCode', label: '阶段编码', width: 120 },
  { prop: 'PhaseName', label: '阶段名称', width: 150 },
  { prop: 'SequenceOrder', label: '顺序', width: 80 },
  { prop: 'Description', label: '说明', minWidth: 200 },
  { prop: 'StatusName', label: '状态', width: 80 },
  { prop: 'CreateTime', label: '创建时间', width: 180 }
]

// ============================================================
// 表单字段定义（与 EntityConfig 对齐）
// ============================================================
const formFields = [
  { field: 'PhaseCode', label: '阶段编码', type: 'input', required: true, placeholder: '如：S1、S2、Surv1' },
  { field: 'PhaseName', label: '阶段名称', type: 'input', required: true, placeholder: '如：一阶段审核' },
  { field: 'SequenceOrder', label: '顺序', type: 'number', placeholder: '如：1' },
  { field: 'Description', label: '说明', type: 'textarea', placeholder: '阶段说明' },
  { field: 'IsValid', label: '状态', type: 'switch', defaultValue: 1 }
]

// ============================================================
// 数据加载
// ============================================================
async function loadData() {
  loading.value = true
  try {
    const params = {
      page: pagination.value.page,
      pageSize: pagination.value.pageSize,
      filters: searchParams.value.keyword
        ? [{ field: 'PhaseName', operator: 'like', value: searchParams.value.keyword }]
        : []
    }
    const res = await getPhaseDefinitionPage(params)
    if (res.success && res.data) {
      tableData.value = res.data.items ?? []
      total.value = res.data.totalCount ?? 0
    }
  } catch (e) {
    console.error('[PhaseDefinition] ❌ 加载数据失败:', e)
  } finally {
    loading.value = false
  }
}

// ============================================================
// 新增
// ============================================================
function openAddDialog() {
  dialogMode.value = 'add'
  formData.value = { IsValid: 1 }
  dialogVisible.value = true
}

// ============================================================
// 编辑
// ============================================================
function openEditDialog(row: PhaseDefinition) {
  dialogMode.value = 'edit'
  formData.value = { ...row }
  dialogVisible.value = true
}

// ============================================================
// 提交表单
// ============================================================
async function submitForm() {
  submitting.value = true
  try {
    const data = formData.value as PhaseDefinition
    let res: any
    if (dialogMode.value === 'add') {
      res = await addPhaseDefinition(data)
    } else {
      res = await updatePhaseDefinition(data)
    }
    if (res.success) {
      dialogVisible.value = false
      await loadData()
    } else {
      console.error('[PhaseDefinition] ❌ 操作失败:', res.message)
    }
  } catch (e) {
    console.error('[PhaseDefinition] ❌ 提交失败:', e)
  } finally {
    submitting.value = false
  }
}

// ============================================================
// 删除
// ============================================================
async function handleDelete(rows: PhaseDefinition[]) {
  const codes = rows.map(r => r.Code!).filter(Boolean) as string[]
  if (codes.length === 0) return
  try {
    const res = await deletePhaseDefinition(codes)
    if (res.success) {
      await loadData()
    }
  } catch (e) {
    console.error('[PhaseDefinition] ❌ 删除失败:', e)
  }
}

// ============================================================
// 切换启用/停用
// ============================================================
async function handleToggleValid(row: PhaseDefinition) {
  if (!row.Code) return
  try {
    const res = await togglePhaseDefinitionValid(row.Code)
    if (res.success) {
      await loadData()
    }
  } catch (e) {
    console.error('[PhaseDefinition] ❌ 切换状态失败:', e)
  }
}

// ============================================================
// 搜索
// ============================================================
function handleSearch() {
  pagination.value.page = 1
  loadData()
}

function handleReset() {
  searchParams.value = { keyword: '' }
  pagination.value.page = 1
  loadData()
}

// ============================================================
// 初始化
// ============================================================
onMounted(() => {
  loadData()
})
</script>

<template>
  <div class="phase-definition-page">
    <!-- 工具栏 -->
    <div class="toolbar">
      <el-input
        v-model="searchParams.keyword"
        placeholder="搜索阶段名称"
        clearable
        style="width: 200px"
        @keyup.enter="handleSearch"
      />
      <el-button type="primary" @click="handleSearch">搜索</el-button>
      <el-button @click="handleReset">重置</el-button>
      <el-button type="primary" @click="openAddDialog">新增</el-button>
    </div>

    <!-- 表格 -->
    <el-table
      v-loading="loading"
      :data="tableData"
      border
      stripe
      @selection-change="selectedRows = $event"
    >
      <el-table-column type="selection" width="50" />
      <el-table-column prop="PhaseCode" label="阶段编码" width="120" />
      <el-table-column prop="PhaseName" label="阶段名称" width="150" />
      <el-table-column prop="SequenceOrder" label="顺序" width="80" />
      <el-table-column prop="Description" label="说明" min-width="200" show-overflow-tooltip />
      <el-table-column prop="StatusName" label="状态" width="80">
        <template #default="{ row }">
          <el-tag :type="row.IsValid === 1 ? 'success' : 'danger'">
            {{ row.StatusName ?? (row.IsValid === 1 ? '启用' : '停用') }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="CreateTime" label="创建时间" width="180" />
      <el-table-column label="操作" width="200" fixed="right">
        <template #default="{ row }">
          <el-button text type="primary" @click="openEditDialog(row)">编辑</el-button>
          <el-button
            v-if="row.IsValid === 1"
            text type="warning"
            @click="handleToggleValid(row)"
          >禁用</el-button>
          <el-button
            v-else
            text type="success"
            @click="handleToggleValid(row)"
          >启用</el-button>
          <el-button text type="danger" @click="handleDelete([row])">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 分页 -->
    <el-pagination
      v-model:current-page="pagination.page"
      v-model:page-size="pagination.pageSize"
      :total="total"
      :page-sizes="[10, 20, 50, 100]"
      layout="total, sizes, prev, pager, next"
      @current-change="loadData"
      @size-change="loadData"
    />

    <!-- 新增/编辑弹窗 -->
    <el-dialog
      v-model="dialogVisible"
      :title="dialogMode === 'add' ? '新增认证阶段' : '编辑认证阶段'"
      width="500px"
      destroy-on-close
    >
      <el-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        label-width="100px"
      >
        <el-form-item label="阶段编码" prop="PhaseCode">
          <el-input v-model="formData.PhaseCode" placeholder="如：S1、S2、Surv1" :disabled="dialogMode === 'edit'" />
        </el-form-item>
        <el-form-item label="阶段名称" prop="PhaseName">
          <el-input v-model="formData.PhaseName" placeholder="如：一阶段审核" />
        </el-form-item>
        <el-form-item label="顺序" prop="SequenceOrder">
          <el-input-number v-model="formData.SequenceOrder" :min="0" />
        </el-form-item>
        <el-form-item label="说明" prop="Description">
          <el-input v-model="formData.Description" type="textarea" :rows="3" placeholder="阶段说明" />
        </el-form-item>
        <el-form-item label="状态" prop="IsValid">
          <el-switch v-model="formData.IsValid" :active-value="1" :inactive-value="0" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitForm">
          {{ dialogMode === 'add' ? '新增' : '保存' }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.phase-definition-page {
  padding: 20px;
}
.toolbar {
  margin-bottom: 16px;
  display: flex;
  gap: 8px;
}
</style>
