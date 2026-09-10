<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { YzhPageLayout } from '@yzh-core'
import {
  getPromptList,
  savePrompt,
  deletePrompt,
  activatePrompt,
  type PromptTemplate
} from '@share/api/workflow/prompt-template'

const loading = ref(false)
const saving = ref(false)
const tableData = ref<PromptTemplate[]>([])
const filterForm = reactive({ promptType: '', skillTarget: '' })

const editVisible = ref(false)
const viewVisible = ref(false)
const editFormRef = ref()
const viewData = ref<PromptTemplate | null>(null)

const editForm = reactive<Partial<PromptTemplate>>({
  promptCode: '',
  promptName: '',
  promptType: 'document_analysis',
  skillTarget: '',
  template: '',
  description: ''
})

const editRules = {
  promptCode: [{ required: true, message: '请输入编码', trigger: 'blur' }],
  promptName: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  promptType: [{ required: true, message: '请选择类型', trigger: 'change' }],
  template: [{ required: true, message: '请输入模板内容', trigger: 'blur' }]
}

const typeMap: Record<string, string> = {
  document_analysis: 'primary',
  extract: 'warning',
  verify: 'success',
  validate: 'danger',
  report: 'info'
}

const labelMap: Record<string, string> = {
  document_analysis: '文档分析',
  extract: '字段提取',
  verify: '验证',
  validate: '校验',
  report: '报告'
}

async function loadList() {
  loading.value = true
  try {
    const res = await getPromptList(filterForm)
    tableData.value = res || []
  } catch (e: any) {
    ElMessage.error(e?.message || '加载失败')
  } finally {
    loading.value = false
  }
}

function resetFilter() {
  filterForm.promptType = ''
  filterForm.skillTarget = ''
  loadList()
}

function openEdit(row: PromptTemplate | null) {
  if (row) {
    Object.assign(editForm, { ...row })
  } else {
    Object.assign(editForm, {
      id: undefined,
      promptCode: '',
      promptName: '',
      promptType: 'document_analysis',
      skillTarget: '',
      template: '',
      description: ''
    })
  }
  editVisible.value = true
}

async function doSave() {
  const valid = await editFormRef.value?.validate().catch(() => false)
  if (!valid) return
  saving.value = true
  try {
    await savePrompt(editForm as PromptTemplate)
    ElMessage.success('保存成功')
    editVisible.value = false
    loadList()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

async function doDelete(row: PromptTemplate) {
  try {
    await ElMessageBox.confirm(`确定删除提示词「${row.promptName}」？`, '确认删除', { type: 'warning' })
  } catch { return }
  try {
    await deletePrompt(row.promptCode)
    ElMessage.success('删除成功')
    loadList()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

async function doActivate(row: PromptTemplate) {
  try {
    await activatePrompt(row.promptCode)
    ElMessage.success('已激活')
    loadList()
  } catch (e: any) {
    ElMessage.error(e?.message || '激活失败')
  }
}

function viewPrompt(row: PromptTemplate) {
  viewData.value = row
  viewVisible.value = true
}

function typeTag(t: string) { return typeMap[t] || 'info' }
function typeLabel(t: string) { return labelMap[t] || t }

onMounted(loadList)
</script>

<template>
  <YzhPageLayout title="提示词模板管理">
    <template #toolbar>
      <el-button type="primary" @click="openEdit(null)">
        <el-icon><i class="bi bi-plus"></i></el-icon> 新建提示词
      </el-button>
    </template>

    <!-- 筛选 -->
    <el-card shadow="never" class="filter-card">
      <el-form :inline="true" :model="filterForm">
        <el-form-item label="提示词类型">
          <el-select v-model="filterForm.promptType" placeholder="全部" clearable style="width:140px">
            <el-option label="文档分析" value="document_analysis" />
            <el-option label="字段提取" value="extract" />
            <el-option label="验证提取" value="verify" />
            <el-option label="校验规则" value="validate" />
            <el-option label="报告生成" value="report" />
          </el-select>
        </el-form-item>
        <el-form-item label="适用技能">
          <el-select v-model="filterForm.skillTarget" placeholder="全部" clearable style="width:140px">
            <el-option label="全部通用" value="all" />
            <el-option label="Word 文档" value="word" />
            <el-option label="Excel 表格" value="excel" />
            <el-option label="PDF 文档" value="pdf" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="loadList">查询</el-button>
          <el-button @click="resetFilter">重置</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <!-- 列表 -->
    <el-card shadow="never" class="table-card">
      <el-table :data="tableData" stripe border v-loading="loading">
        <el-table-column prop="promptCode" label="编码" width="180" />
        <el-table-column prop="promptName" label="名称" width="180" />
        <el-table-column prop="promptType" label="类型" width="100">
          <template #default="{ row }">
            <el-tag :type="typeTag(row.promptType)" size="small">{{ typeLabel(row.promptType) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="skillTarget" label="适用技能" width="100">
          <template #default="{ row }">
            <el-tag v-if="!row.skillTarget" size="small" type="info">全部</el-tag>
            <el-tag v-else size="small">{{ row.skillTarget }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="isActive" label="生效" width="70" align="center">
          <template #default="{ row }">
            <el-tag :type="row.isActive ? 'success' : 'info'" size="small">
              {{ row.isActive ? '生效' : '历史' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="description" label="说明" min-width="200" show-overflow-tooltip />
        <el-table-column label="操作" width="260" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="viewPrompt(row)">查看</el-button>
            <el-button link type="primary" size="small" @click="openEdit(row)">编辑</el-button>
            <el-button v-if="!row.isActive" link type="warning" size="small" @click="doActivate(row)">激活</el-button>
            <el-button link type="danger" size="small" @click="doDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 编辑弹窗 -->
    <el-dialog v-model="editVisible" :title="editForm.id ? '编辑提示词' : '新建提示词'" width="700px">
      <el-form ref="editFormRef" :model="editForm" :rules="editRules" label-width="100px">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="编码" prop="promptCode">
              <el-input v-model="editForm.promptCode" placeholder="如：analyze_word" :disabled="!!editForm.id" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="名称" prop="promptName">
              <el-input v-model="editForm.promptName" placeholder="用于界面展示" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="类型" prop="promptType">
              <el-select v-model="editForm.promptType" style="width:100%">
                <el-option label="文档分析" value="document_analysis" />
                <el-option label="字段提取" value="extract" />
                <el-option label="验证提取" value="verify" />
                <el-option label="校验规则" value="validate" />
                <el-option label="报告生成" value="report" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="适用技能">
              <el-select v-model="editForm.skillTarget" placeholder="全部适用" clearable style="width:100%">
                <el-option label="全部通用" value="all" />
                <el-option label="Word 文档" value="word" />
                <el-option label="Excel 表格" value="excel" />
                <el-option label="PDF 文档" value="pdf" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="说明">
          <el-input v-model="editForm.description" type="textarea" :rows="2" />
        </el-form-item>
        <el-form-item label="模板内容" prop="template">
          <el-input v-model="editForm.template" type="textarea" :rows="14" placeholder="支持占位符：{document_content} {fields_json} {tables_json} {prompt}" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="editVisible = false">取消</el-button>
        <el-button type="primary" @click="doSave" :loading="saving">保存</el-button>
      </template>
    </el-dialog>

    <!-- 查看弹窗 -->
    <el-dialog v-model="viewVisible" title="提示词详情" width="650px">
      <el-descriptions :column="2" border v-if="viewData">
        <el-descriptions-item label="编码">{{ viewData.promptCode }}</el-descriptions-item>
        <el-descriptions-item label="名称">{{ viewData.promptName }}</el-descriptions-item>
        <el-descriptions-item label="类型">
          <el-tag :type="typeTag(viewData.promptType)" size="small">{{ typeLabel(viewData.promptType) }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="适用技能">
          <el-tag v-if="!viewData.skillTarget" size="small" type="info">全部</el-tag>
          <el-tag v-else size="small">{{ viewData.skillTarget }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="生效状态">
          <el-tag :type="viewData.isActive ? 'success' : 'info'" size="small">
            {{ viewData.isActive ? '生效中' : '未生效' }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="说明" :span="2">{{ viewData.description || '-' }}</el-descriptions-item>
      </el-descriptions>
      <div v-if="viewData?.template" class="view-section">
        <div class="view-section-title">模板内容</div>
        <pre class="view-template">{{ viewData.template }}</pre>
      </div>
      <template #footer>
        <el-button @click="viewVisible = false">关闭</el-button>
        <el-button v-if="viewData && !viewData.isActive" type="warning" @click="doActivate(viewData!); viewVisible=false">设为生效</el-button>
      </template>
    </el-dialog>
  </YzhPageLayout>
</template>

<style scoped>
.filter-card { margin-bottom: 16px; }
.table-card { margin-bottom: 16px; }
.view-section { margin-top: 16px; }
.view-section-title { font-weight: 600; margin-bottom: 8px; }
.view-template { background: #f5f7fa; border: 1px solid #e4e7ed; border-radius: 4px; padding: 12px; font-size: 13px; line-height: 1.6; white-space: pre-wrap; word-break: break-all; max-height: 400px; overflow-y: auto; }
</style>
