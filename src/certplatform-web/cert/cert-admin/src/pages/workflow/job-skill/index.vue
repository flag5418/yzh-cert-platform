<script setup lang="ts">
import { YzhTable, YzhForm, type YzhTableColumn, type YzhFormField, type PageParams, type SearchField } from '@yzh-core'
import { ref, reactive, computed, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { Skill, SkillCategory } from '@share/api/workflow/job-skill'
import {
  getSkillPage,
  addSkill,
  updateSkill,
  deleteSkill,
  toggleSkillValid,
  getSkillCategoryPage,
  addSkillCategory,
  updateSkillCategory,
  deleteSkillCategory
} from '@share/api/workflow/job-skill'

// ── 技能表格 ──
const tableRef = ref()
const formRef = ref()
const selectedRows = ref<Skill[]>([])
const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
let formData = reactive<Partial<Skill>>({})
const submitting = ref(false)

const columns: YzhTableColumn<Skill>[] = [
  { prop: 'Code', label: '编码', width: 160 },
  { prop: 'Name', label: '名称', width: 160 },
  { prop: 'CategoryCode', label: '分类', width: 120, formatter: (v: any) => getCategoryName(v) },
  { prop: 'SkillType', label: '类型', width: 80 },
  { prop: 'Description', label: '说明', minWidth: 200 },
  { prop: 'IsValid', label: '状态', width: 80, formatter: (v: any) => v === 1 ? '启用' : '停用' },
  { prop: 'CreateTime', label: '创建时间', width: 180 },
  { prop: 'actions', label: '操作', width: 200, fixed: 'right', slot: true }
]

const searchFields: SearchField[] = [
  { prop: 'Name', label: '名称', type: 'text' }
]

const formFields = computed<YzhFormField[]>(() => [
  { prop: 'Code', label: '编码', type: 'text', required: true, span: 12, disabled: dialogMode.value === 'edit' },
  { prop: 'Name', label: '名称', type: 'text', required: true, span: 12 },
  { prop: 'CategoryCode', label: '分类', type: 'select', span: 12, options: categoryOptions.value },
  { prop: 'SkillType', label: '类型', type: 'select', span: 12, options: [{ label: '手动', value: 'manual' }, { label: 'API', value: 'api' }] },
  { prop: 'Description', label: '说明', type: 'textarea', span: 24 },
  { prop: 'PromptTemplate', label: 'Prompt模板', type: 'textarea', span: 24 },
  { prop: 'SortOrder', label: '排序', type: 'number', span: 12 },
  { prop: 'IsValid', label: '状态', type: 'switch', span: 12, defaultValue: 1 }
])

async function loadData(params: PageParams) {
  const filters: any[] = []
  if (params.Name) {
    filters.push({ Field: 'name', Operator: 'like', Value: params.Name })
  }
  if (currentCategory.value) {
    filters.push({ Field: 'category_code', Operator: 'eq', Value: currentCategory.value })
  }
  const res = await getSkillPage({
    Page: params.page,
    PageSize: params.rows,
    Filters: filters
  })
  if (res.success && res.data) {
    return { rows: res.data.Items ?? [], total: res.data.TotalCount ?? 0 }
  }
  return { rows: [], total: 0 }
}

function onAdd() {
  dialogMode.value = 'add'
  formData = reactive<Partial<Skill>>({ SkillType: 'manual', SortOrder: 0, IsValid: 1 })
  dialogVisible.value = true
}

function onEdit(row: Skill) {
  dialogMode.value = 'edit'
  formData = reactive<Partial<Skill>>({ ...row })
  dialogVisible.value = true
}

async function onSubmit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  submitting.value = true
  try {
    const data = formData as Skill
    if (dialogMode.value === 'add') {
      await addSkill(data)
    } else {
      await updateSkill(data)
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

async function onDelete(row: Skill) {
  try { await ElMessageBox.confirm(`确定删除技能「${row.Name}」吗？`, '删除确认', { type: 'warning' }) } catch { return }
  await deleteSkill([row.Code!])
  ElMessage.success('删除成功')
  tableRef.value?.refresh()
}

async function onToggle(row: Skill) {
  if (!row.Code) return
  await toggleSkillValid(row.Code)
  ElMessage.success('状态已更新')
  tableRef.value?.refresh()
}

// ── 分类管理 ──
const categories = ref<SkillCategory[]>([])
const categoryOptions = computed(() =>
  categories.value.filter(c => c.IsValid === 1).map(c => ({ label: c.Name, value: c.Code }))
)
const currentCategory = ref('')
const categoryDialogVisible = ref(false)
const categoryFormRef = ref()
const categoryDialogMode = ref<'add' | 'edit'>('add')
const categoryEditVisible = ref(false)
let categoryFormData = reactive<Partial<SkillCategory>>({})

const categoryFormFields = computed<YzhFormField[]>(() => [
  { prop: 'Code', label: '编码', type: 'text', required: true, span: 12, disabled: categoryDialogMode.value === 'edit' },
  { prop: 'Name', label: '名称', type: 'text', required: true, span: 12 },
  { prop: 'SortOrder', label: '排序', type: 'number', span: 12 },
  { prop: 'IsValid', label: '状态', type: 'switch', span: 12, defaultValue: 1 }
])

function getCategoryName(code: string) {
  return categories.value.find(c => c.Code === code)?.Name || code || '-'
}

async function loadCategories() {
  const res = await getSkillCategoryPage({ Page: 1, PageSize: 100 })
  if (res.success && res.data) {
    categories.value = res.data.Items ?? []
  }
}

function selectCategory(code: string) {
  currentCategory.value = code
  tableRef.value?.refresh()
}

function onAddCategory() {
  categoryDialogMode.value = 'add'
  categoryFormData = reactive<Partial<SkillCategory>>({ SortOrder: 0, IsValid: 1 })
  categoryEditVisible.value = true
}

function onEditCategory(row: SkillCategory) {
  categoryDialogMode.value = 'edit'
  categoryFormData = reactive<Partial<SkillCategory>>({ ...row })
  categoryEditVisible.value = true
}

async function onSubmitCategory() {
  const valid = await categoryFormRef.value?.validate().catch(() => false)
  if (!valid) return
  try {
    const data = categoryFormData as SkillCategory
    if (categoryDialogMode.value === 'add') {
      await addSkillCategory(data)
    } else {
      await updateSkillCategory(data)
    }
    ElMessage.success('保存成功')
    categoryEditVisible.value = false
    loadCategories()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  }
}

async function onDeleteCategory(row: SkillCategory) {
  try { await ElMessageBox.confirm(`确定删除分类「${row.Name}」吗？`, '删除确认', { type: 'warning' }) } catch { return }
  await deleteSkillCategory([row.Code!])
  ElMessage.success('删除成功')
  loadCategories()
}

onMounted(() => { loadCategories() })
</script>

<template>
  <div class="skill-page">
    <!-- 左侧分类 -->
    <el-card shadow="never" class="category-card">
      <template #header>
        <div class="category-header">
          <span>分类</span>
          <el-button text type="primary" size="small" @click="categoryDialogVisible = true">管理</el-button>
        </div>
      </template>
      <div class="category-list">
        <div class="category-item" :class="{ active: currentCategory === '' }" @click="selectCategory('')">
          全部
        </div>
        <div v-for="cat in categories" :key="cat.Code" class="category-item" :class="{ active: currentCategory === cat.Code }" @click="selectCategory(cat.Code!)">
          {{ cat.Name }}
        </div>
      </div>
    </el-card>

    <!-- 右侧表格 -->
    <div class="table-area">
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
    </div>

    <!-- 技能编辑弹窗 -->
    <el-dialog v-model="dialogVisible" :title="dialogMode === 'add' ? '新增技能' : '编辑技能'" width="700px">
      <YzhForm ref="formRef" v-model="formData" :fields="formFields" :loading="submitting" @submit="onSubmit" @reset="dialogVisible = false" />
    </el-dialog>

    <!-- 分类管理弹窗 -->
    <el-dialog v-model="categoryDialogVisible" title="分类管理" width="600px">
      <el-table :data="categories" border size="small">
        <el-table-column label="编码" width="140">
          <template #default="{ row }">{{ row.Code }}</template>
        </el-table-column>
        <el-table-column label="名称" width="130">
          <template #default="{ row }">{{ row.Name }}</template>
        </el-table-column>
        <el-table-column label="排序" width="80">
          <template #default="{ row }">{{ row.SortOrder }}</template>
        </el-table-column>
        <el-table-column label="操作" width="130" align="center">
          <template #default="{ row }">
            <el-button type="primary" link size="small" @click="onEditCategory(row)">编辑</el-button>
            <el-button type="danger" link size="small" @click="onDeleteCategory(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
      <el-button type="primary" plain size="small" style="margin-top: 8px" @click="onAddCategory">+ 新增分类</el-button>
    </el-dialog>

    <!-- 分类编辑弹窗 -->
    <el-dialog v-model="categoryEditVisible" :title="categoryDialogMode === 'add' ? '新增分类' : '编辑分类'" width="500px" destroy-on-close>
      <YzhForm ref="categoryFormRef" v-model="categoryFormData" :fields="categoryFormFields" @submit="onSubmitCategory" @reset="categoryEditVisible = false" />
    </el-dialog>
  </div>
</template>

<style scoped>
.skill-page { display: flex; gap: 16px; height: 100%; }
.category-card { width: 200px; min-width: 200px; }
.category-header { display: flex; align-items: center; justify-content: space-between; }
.category-list { overflow-y: auto; }
.category-item { padding: 8px 12px; margin-bottom: 4px; border-radius: 6px; cursor: pointer; font-size: 13px; }
.category-item:hover { background: #f5f7fa; }
.category-item.active { background: #ecf5ff; color: #409EFF; font-weight: 600; }
.table-area { flex: 1; min-width: 0; }
.action-cell { display: flex; flex-wrap: nowrap; gap: 2px; }
</style>
