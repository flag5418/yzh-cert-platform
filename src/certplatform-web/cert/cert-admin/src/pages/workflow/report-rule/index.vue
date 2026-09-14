<script setup lang="ts">
/**
 * 报告章节定义 — YzhTable 配置驱动（模板列表）
 * 右侧章节子表保留 el-table（从属数据，非标准 CRUD 页面）
 */
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus } from '@element-plus/icons-vue'
import {
  YzhPageLayout,
  YzhTable,
  YzhForm,
  type YzhTableColumn,
  type YzhFormField,
  type PageParams
} from '@yzh-core'
import {
  getReportTemplatePage,
  saveReportTemplate,
  deleteReportTemplate,
  getReportSectionList,
  saveReportSection,
  deleteReportSection,
  type ReportTemplate,
  type ReportSection
} from '@share/api/workflow/report-rule'

const tableRef = ref()
const formRef = ref()

// 当前选中的模板
const currentTemplate = ref<ReportTemplate | null>(null)
const sectionLoading = ref(false)
const sectionData = ref<ReportSection[]>([])

// 编辑弹窗
const dialogVisible = ref(false)
const sectionDialogVisible = ref(false)
const editForm = reactive<Partial<ReportTemplate>>({ templateName: '', isDefault: false, remark: '' })
const sectionForm = reactive<Partial<ReportSection>>({ sectionName: '', sectionNameEn: '', sortOrder: 0, isActive: true, remark: '' })
const submitting = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')

// ── 模板表格列定义 ──
const columns: YzhTableColumn<ReportTemplate>[] = [
  { prop: 'templateName', label: '模板名称', minWidth: 180 },
  { prop: 'isDefault', label: '默认', width: 60, align: 'center', formatter: (v: any) => v ? '是' : '否' },
  { prop: 'chapterCount', label: '章节数', width: 80, align: 'center' },
  { prop: 'remark', label: '备注', minWidth: 150 },
  { prop: 'actions', label: '操作', width: 120, fixed: 'right', slot: true }
]

// ── 模板表单字段 ──
const templateFormFields: YzhFormField[] = [
  { prop: 'templateName', label: '模板名称', type: 'text', required: true, span: 24, placeholder: '如：ISO9001第一阶段审核报告' },
  { prop: 'isDefault', label: '是否默认', type: 'switch', span: 24 },
  { prop: 'remark', label: '备注', type: 'textarea', span: 24 }
]

// ── 章节表单字段 ──
const sectionFormFields: YzhFormField[] = [
  { prop: 'sectionName', label: '章节名称', type: 'text', required: true, span: 24, placeholder: '如：审核发现' },
  { prop: 'sectionNameEn', label: '英文名称', type: 'text', span: 24, placeholder: '如：Audit Findings' },
  { prop: 'sortOrder', label: '排序', type: 'number', span: 24 },
  { prop: 'isActive', label: '是否启用', type: 'switch', span: 24 },
  { prop: 'remark', label: '备注', type: 'textarea', span: 24 }
]

// ── 数据加载（YzhTable 期望返回 Page<T> = { rows, total }）──
async function loadTemplateData(params: PageParams) {
  const res = await getReportTemplatePage(params)
  return { rows: res?.rows ?? [], total: res?.total ?? 0 }
}

// ── 章节加载 ──
async function loadSections(template: ReportTemplate) {
  currentTemplate.value = template
  sectionLoading.value = true
  try {
    sectionData.value = await getReportSectionList(template.code!)
  } catch {
    ElMessage.error('加载章节失败')
  } finally {
    sectionLoading.value = false
  }
}

// ── 模板操作 ──
function onAddTemplate() {
  dialogMode.value = 'add'
  Object.assign(editForm, { id: undefined, templateName: '', isDefault: false, remark: '' })
  dialogVisible.value = true
}

function onEditTemplate(row: ReportTemplate) {
  dialogMode.value = 'edit'
  Object.assign(editForm, { ...row })
  dialogVisible.value = true
}

async function doSaveTemplate() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  submitting.value = true
  try {
    await saveReportTemplate(editForm as ReportTemplate)
    ElMessage.success('保存成功')
    dialogVisible.value = false
    tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    submitting.value = false
  }
}

async function doDeleteTemplate(row: ReportTemplate) {
  try { await ElMessageBox.confirm(`确定删除模板「${row.templateName}」？`, '确认删除', { type: 'warning' }) } catch { return }
  await deleteReportTemplate(row.id!)
  ElMessage.success('删除成功')
  tableRef.value?.refresh()
}

// ── 章节操作 ──
function openEditSection(row: ReportSection | null) {
  if (row) Object.assign(sectionForm, { ...row })
  else Object.assign(sectionForm, { id: undefined, sectionName: '', sectionNameEn: '', sortOrder: 0, isActive: true, remark: '' })
  sectionDialogVisible.value = true
}

async function doSaveSection() {
  if (!sectionForm.sectionName) { ElMessage.warning('请输入章节名称'); return }
  if (!currentTemplate.value?.code) { ElMessage.warning('请先选择模板'); return }
  submitting.value = true
  try {
    await saveReportSection({ ...sectionForm, reportCode: currentTemplate.value.code } as ReportSection)
    ElMessage.success('保存成功')
    sectionDialogVisible.value = false
    loadSections(currentTemplate.value!)
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    submitting.value = false
  }
}

async function doDeleteSection(row: ReportSection) {
  try { await ElMessageBox.confirm(`确定删除章节「${row.sectionName}」？`, '确认删除', { type: 'warning' }) } catch { return }
  await deleteReportSection(row.id!)
  ElMessage.success('删除成功')
  if (currentTemplate.value) loadSections(currentTemplate.value)
}
</script>

<template>
  <YzhPageLayout title="报告章节定义">
    <el-row :gutter="16" class="report-rule-layout">
      <!-- 左侧：模板列表（YzhTable 配置驱动） -->
      <el-col :span="10">
        <YzhTable ref="tableRef" :columns="columns" :data-loader="loadTemplateData" :search-fields="[{ prop: 'templateName', label: '搜索模板', type: 'text' }]">
          <template #toolbar-left>
            <el-button type="primary" :icon="Plus" @click="onAddTemplate">新建模板</el-button>
          </template>
          <template #column-actions="{ row }">
            <el-button link type="primary" size="small" @click="loadSections(row); onEditTemplate(row)">编辑</el-button>
            <el-button link type="danger" size="small" @click="doDeleteTemplate(row)">删除</el-button>
          </template>
        </YzhTable>
      </el-col>

      <!-- 右侧：章节列表（从属数据，保留 el-table） -->
      <el-col :span="14">
        <el-card shadow="never" class="section-card">
          <template #header>
            <div class="card-header">
              <span v-if="currentTemplate">章节 — {{ currentTemplate.templateName }}</span>
              <span v-else>章节（请先选择模板）</span>
              <el-button v-if="currentTemplate" type="primary" size="small" :icon="Plus" @click="openEditSection(null)">新建章节</el-button>
            </div>
          </template>
          <el-table :data="sectionData" stripe border v-loading="sectionLoading">
            <el-table-column prop="sectionName" label="章节名称" width="200" />
            <el-table-column prop="sectionNameEn" label="英文名称" width="150" show-overflow-tooltip />
            <el-table-column prop="sortOrder" label="排序" width="80" align="center" />
            <el-table-column label="启用" width="80" align="center">
              <template #default="{ row }"><el-switch :model-value="row.isActive" disabled /></template>
            </el-table-column>
            <el-table-column label="操作" width="120" fixed="right">
              <template #default="{ row }">
                <el-button link type="primary" size="small" @click="openEditSection(row)">编辑</el-button>
                <el-button link type="danger" size="small" @click="doDeleteSection(row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
          <el-empty v-if="!currentTemplate && !sectionLoading" description="请先在左侧选择报告模板" :image-size="100" />
        </el-card>
      </el-col>
    </el-row>

    <!-- 模板编辑弹窗（YzhForm 配置驱动） -->
    <el-dialog v-model="dialogVisible" :title="dialogMode === 'add' ? '新建模板' : '编辑模板'" width="500px">
      <YzhForm ref="formRef" v-model="editForm" :fields="templateFormFields" :loading="submitting" @submit="doSaveTemplate" @reset="dialogVisible = false" />
    </el-dialog>

    <!-- 章节编辑弹窗（YzhForm 配置驱动） -->
    <el-dialog v-model="sectionDialogVisible" :title="sectionForm.id ? '编辑章节' : '新建章节'" width="500px">
      <YzhForm v-model="sectionForm" :fields="sectionFormFields" :loading="submitting" @submit="doSaveSection" @reset="sectionDialogVisible = false" />
    </el-dialog>
  </YzhPageLayout>
</template>

<style scoped>
.report-rule-layout { height: 100%; }
.section-card { height: 100%; display: flex; flex-direction: column; }
.card-header { display: flex; align-items: center; justify-content: space-between; }
</style>
