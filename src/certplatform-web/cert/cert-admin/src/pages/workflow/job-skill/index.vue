<script setup lang="ts">
/**
 * 技能管理（组合页面：左侧分类 + 右侧表格）
 *
 * 表格走 SingleTableCore（dataLoader / dispatch / YzhFormDialog）；
 * 分类管理为页面级组合 UI（@share api），不属于单表 CRUD 内核范围。
 */
import { YzhTable, YzhForm, YzhFormDialog, useSingleTable, type PageParams, type YzhFormField } from '@yzh-core'
import { ref, reactive, computed, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { SkillCategory } from '@share/api/workflow/job-skill'
import {
  getSkillCategoryPage,
  addSkillCategory,
  updateSkillCategory,
  deleteSkillCategory
} from '@share/api/workflow/job-skill'
import { JobSkillLogic } from './logic'

const { logic, tableRef } = useSingleTable(JobSkillLogic)

// ── 技能表格（内核 dataLoader + 分类过滤） ──
async function loadTableData(params: PageParams) {
  return logic.dataLoader({
    ...params,
    CategoryCode: logic.currentCategory.value || '',
  } as PageParams)
}

// ── 分类管理 ──
const categories = ref<SkillCategory[]>([])

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

async function loadCategories() {
  const res = await getSkillCategoryPage({ Page: 1, PageSize: 100 })
  if (res.success && res.data) {
    categories.value = res.data.Items ?? []
  }
}

function selectCategory(code: string) {
  logic.currentCategory.value = code
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

onMounted(async () => {
  await loadCategories()
})
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
        <div class="category-item" :class="{ active: logic.currentCategory.value === '' }" @click="selectCategory('')">
          全部
        </div>
        <div v-for="cat in categories" :key="cat.Code" class="category-item" :class="{ active: logic.currentCategory.value === cat.Code }" @click="selectCategory(cat.Code!)">
          {{ cat.Name }}
        </div>
      </div>
    </el-card>

    <!-- 右侧表格 -->
    <div class="table-area">
      <YzhTable
        ref="tableRef"
        :columns="logic.columns"
        :data-loader="loadTableData"
        :search-fields="logic.searchFields"
        :toolbar-actions="logic.toolbarActions"
        :row-action-buttons="logic.rowActions"
        select-mode="multiple"
        @row-action="logic.onRowAction"
        @toolbar-action="logic.onToolbarAction"
      />
    </div>

    <!-- 技能编辑弹窗 -->
    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      entity-name="技能"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      width="700px"
      @submit="logic.submitForm()"
    />

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
</style>
