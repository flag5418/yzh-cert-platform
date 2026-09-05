<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { YzhPageLayout } from '@yzh-core/components/layout'
import {
  getReportTemplatePage,
  saveReportTemplate,
  deleteReportTemplate,
  getReportSectionList,
  saveReportSection,
  deleteReportSection,
  type ReportTemplate,
  type ReportSection
} from '@share/api/report-rule'

const loading = ref(false)
const tableData = ref<ReportTemplate[]>([])
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const keyword = ref('')

// 当前选中的模板
const currentTemplate = ref<ReportTemplate | null>(null)
const sectionLoading = ref(false)
const sectionData = ref<ReportSection[]>([])

// 编辑弹窗
const dialogVisible = ref(false)
const sectionDialogVisible = ref(false)
const editFormRef = ref()
const sectionFormRef = ref()
const editForm = reactive<Partial<ReportTemplate>>({
  templateName: '',
  isDefault: false,
  remark: ''
})
const sectionForm = reactive<Partial<ReportSection>>({
  sectionName: '',
  sectionNameEn: '',
  sortOrder: 0,
  isActive: true,
  remark: ''
})
const submitting = ref(false)

const columns = [
  { prop: 'templateName', label: '模板名称', width: 250 },
  { prop: 'isDefault', label: '默认', width: 80, align: 'center' },
  { prop: 'chapterCount', label: '章节数', width: 80, align: 'center' },
  { prop: 'remark', label: '备注', minWidth: 150, showOverflowTooltip: true },
  { prop: 'actions', label: '操作', width: 180, fixed: 'right' }
]

async function loadData() {
  loading.value = true
  try {
    const res = await getReportTemplatePage({ page: page.value, rows: pageSize.value }, { keyword: keyword.value || null })
    tableData.value = res?.rows || []
    total.value = res?.total || 0
  } catch (e: any) {
    ElMessage.error(e?.message || '加载失败')
  } finally {
    loading.value = false
  }
}

async function loadSections(template: ReportTemplate) {
  currentTemplate.value = template
  sectionLoading.value = true
  try {
    sectionData.value = await getReportSectionList(template.code!)
  } catch (e: any) {
    ElMessage.error('加载章节失败')
  } finally {
    sectionLoading.value = false
  }
}

function openEditTemplate(row: ReportTemplate | null) {
  if (row) {
    Object.assign(editForm, { ...row })
  } else {
    Object.assign(editForm, {
      id: undefined,
      templateName: '',
      isDefault: false,
      remark: ''
    })
  }
  dialogVisible.value = true
}

async function doSaveTemplate() {
  if (!editForm.templateName) {
    ElMessage.warning('请输入模板名称')
    return
  }
  submitting.value = true
  try {
    await saveReportTemplate(editForm as ReportTemplate)
    ElMessage.success('保存成功')
    dialogVisible.value = false
    loadData()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    submitting.value = false
  }
}

async function doDeleteTemplate(row: ReportTemplate) {
  try {
    await ElMessageBox.confirm(`确定删除模板「${row.templateName}」？`, '确认删除', { type: 'warning' })
  } catch { return }
  try {
    await deleteReportTemplate(row.id!)
    ElMessage.success('删除成功')
    loadData()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

function openEditSection(row: ReportSection | null) {
  if (row) {
    Object.assign(sectionForm, { ...row })
  } else {
    Object.assign(sectionForm, {
      id: undefined,
      sectionName: '',
      sectionNameEn: '',
      sortOrder: 0,
      isActive: true,
      remark: ''
    })
  }
  sectionDialogVisible.value = true
}

async function doSaveSection() {
  if (!sectionForm.sectionName) {
    ElMessage.warning('请输入章节名称')
    return
  }
  if (!currentTemplate.value?.code) {
    ElMessage.warning('请先选择模板')
    return
  }
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
  try {
    await ElMessageBox.confirm(`确定删除章节「${row.sectionName}」？`, '确认删除', { type: 'warning' })
  } catch { return }
  try {
    await deleteReportSection(row.id!)
    ElMessage.success('删除成功')
    if (currentTemplate.value) loadSections(currentTemplate.value)
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

function toggleSectionActive(row: ReportSection) {
  // TODO: 后端接口待实现
  ElMessage.info('切换状态功能待实现')
}

onMounted(loadData)
</script>

<template>
  <YzhPageLayout title="报告章节定义">
    <template #toolbar>
      <el-button type="primary" @click="openEditTemplate(null)">
        <el-icon><Plus /></el-icon> 新建模板
      </el-button>
    </template>

    <el-row :gutter="16">
      <!-- 左侧：模板列表 -->
      <el-col :span="10">
        <el-card shadow="never">
          <template #header>
            <div class="card-header">
              <span>报告模板</span>
              <el-input v-model="keyword" placeholder="搜索模板" clearable size="small" style="width: 160px" @keyup.enter="loadData" />
            </div>
          </template>
          <el-table :data="tableData" stripe border v-loading="loading" @row-click="loadSections" highlight-current-row>
            <el-table-column prop="templateName" label="模板名称" min-width="180" />
            <el-table-column label="默认" width="60" align="center">
              <template #default="{ row }">
                <el-tag v-if="row.isDefault" type="success" size="small">是</el-tag>
                <el-tag v-else size="small">否</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="章节数" width="80" align="center">
              <template #default="{ row }">{{ row.chapterCount || 0 }}</template>
            </el-table-column>
            <el-table-column label="操作" width="120" fixed="right">
              <template #default="{ row }">
                <el-button link type="primary" size="small" @click.stop="openEditTemplate(row)">编辑</el-button>
                <el-button link type="danger" size="small" @click.stop="doDeleteTemplate(row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
          <el-pagination
            v-model:current-page="page"
            :page-size="pageSize"
            :total="total"
            layout="prev, pager, next"
            style="margin-top: 12px; justify-content: center"
            @current-change="loadData"
          />
        </el-card>
      </el-col>

      <!-- 右侧：章节列表 -->
      <el-col :span="14">
        <el-card shadow="never">
          <template #header>
            <div class="card-header">
              <span v-if="currentTemplate">章节 — {{ currentTemplate.templateName }}</span>
              <span v-else>章节（请先选择模板）</span>
              <el-button v-if="currentTemplate" type="primary" size="small" @click="openEditSection(null)">
                <el-icon><Plus /></el-icon> 新建章节
              </el-button>
            </div>
          </template>
          <el-table :data="sectionData" stripe border v-loading="sectionLoading">
            <el-table-column prop="sectionName" label="章节名称" width="200" />
            <el-table-column prop="sectionNameEn" label="英文名称" width="150" show-overflow-tooltip />
            <el-table-column prop="sortOrder" label="排序" width="80" align="center" />
            <el-table-column label="启用" width="80" align="center">
              <template #default="{ row }">
                <el-switch :model-value="row.isActive" @change="toggleSectionActive(row)" />
              </template>
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

    <!-- 模板编辑弹窗 -->
    <el-dialog v-model="dialogVisible" :title="editForm.id ? '编辑模板' : '新建模板'" width="500px">
      <el-form ref="editFormRef" :model="editForm" label-width="100px">
        <el-form-item label="模板名称" required>
          <el-input v-model="editForm.templateName" placeholder="如：ISO9001第一阶段审核报告" />
        </el-form-item>
        <el-form-item label="是否默认">
          <el-switch v-model="editForm.isDefault" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="editForm.remark" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="doSaveTemplate" :loading="submitting">保存</el-button>
      </template>
    </el-dialog>

    <!-- 章节编辑弹窗 -->
    <el-dialog v-model="sectionDialogVisible" :title="sectionForm.id ? '编辑章节' : '新建章节'" width="500px">
      <el-form ref="sectionFormRef" :model="sectionForm" label-width="100px">
        <el-form-item label="章节名称" required>
          <el-input v-model="sectionForm.sectionName" placeholder="如：审核发现" />
        </el-form-item>
        <el-form-item label="英文名称">
          <el-input v-model="sectionForm.sectionNameEn" placeholder="如：Audit Findings" />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="sectionForm.sortOrder" :min="0" :max="999" style="width: 100%" />
        </el-form-item>
        <el-form-item label="是否启用">
          <el-switch v-model="sectionForm.isActive" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="sectionForm.remark" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="sectionDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="doSaveSection" :loading="submitting">保存</el-button>
      </template>
    </el-dialog>
  </YzhPageLayout>
</template>

<style scoped>
.card-header { display: flex; align-items: center; justify-content: space-between; }
</style>
