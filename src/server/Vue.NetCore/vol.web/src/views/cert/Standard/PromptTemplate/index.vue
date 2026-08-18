<template>
  <div class="prompt-template-page">
    <!-- 顶部筛选 -->
    <el-card shadow="never" class="filter-card">
      <el-form :inline="true" :model="filterForm" class="filter-form">
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
      <template #header>
        <div class="card-header">
          <span class="card-title"><el-icon class="card-title-icon"><IconPrompt /></el-icon> 提示词模板管理</span>
          <el-button type="primary" size="small" @click="openEdit(null)">
            <el-icon><IconAdd /></el-icon> 新建提示词
          </el-button>
        </div>
      </template>

      <el-table :data="tableData" stripe border v-loading="loading" style="width:100%">
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
        <el-table-column prop="version" label="版本" width="70" align="center" />
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
            <div class="row-actions">
              <el-button link type="primary" size="small" @click="viewPrompt(row)">查看</el-button>
              <el-button link type="primary" size="small" @click="openEdit(row)">编辑</el-button>
              <el-button v-if="!row.isActive" link type="warning" size="small" @click="doActivate(row)">激活</el-button>
              <el-button link type="danger" size="small" @click="doDelete(row)">删除</el-button>
            </div>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 编辑弹窗 -->
    <el-dialog
      v-model="editVisible"
      :title="editForm.id ? '编辑提示词' : '新建提示词'"
      width="700px"
      destroy-on-close
      @close="resetForm"
    >
      <el-form ref="editFormRef" :model="editForm" :rules="editRules" label-width="100px">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="编码" prop="promptCode">
              <el-input v-model="editForm.promptCode" placeholder="如：analyze_word_v1" :disabled="!!editForm.id" />
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
              <el-select v-model="editForm.promptType" placeholder="选择类型" style="width:100%">
                <el-option label="文档分析 (document_analysis)" value="document_analysis" />
                <el-option label="字段提取 (extract)" value="extract" />
                <el-option label="验证提取 (verify)" value="verify" />
                <el-option label="校验规则 (validate)" value="validate" />
                <el-option label="报告生成 (report)" value="report" />
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
          <el-input v-model="editForm.description" type="textarea" :rows="2" placeholder="使用场景说明" />
        </el-form-item>
        <el-form-item label="模板内容" prop="template">
          <el-input
            v-model="editForm.template"
            type="textarea"
            :rows="14"
            placeholder="提示词模板内容，支持占位符：{document_content} {fields_json} {tables_json} {prompt}"
            class="prompt-editor"
          />
          <div class="form-hint">
            <el-icon><IconInfo /></el-icon>
            <span>常用占位符：{document_content} / {fields_json} / {tables_json} / {prompt}</span>
          </div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="editVisible = false">取消</el-button>
        <el-button type="primary" @click="doSave" :loading="saving">保存</el-button>
      </template>
    </el-dialog>

    <!-- 查看弹窗 -->
    <el-dialog v-model="viewVisible" title="提示词详情" width="650px" destroy-on-close>
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
        <el-descriptions-item label="版本">{{ viewData.version }}</el-descriptions-item>
        <el-descriptions-item label="生效状态">
          <el-tag :type="viewData.isActive ? 'success' : 'info'" size="small">
            {{ viewData.isActive ? '生效中' : '历史版本' }}
          </el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="说明" :span="2">{{ viewData.description || '-' }}</el-descriptions-item>
      </el-descriptions>
      <div class="view-section" v-if="viewData?.template">
        <div class="view-section-title">模板内容</div>
        <pre class="view-template">{{ viewData.template }}</pre>
      </div>
      <template #footer>
        <el-button @click="viewVisible = false">关闭</el-button>
        <el-button v-if="viewData && !viewData.isActive" type="warning" @click="doActivate(viewData); viewVisible=false">激活此版本</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { IconAdd, IconInfo, IconPrompt } from '@/yzh'
import { getPromptList, savePrompt, deletePrompt, activatePrompt } from './api.js'

const filterForm = reactive({ promptType: '', skillTarget: '' })
const tableData = ref([])
const loading = ref(false)
const saving = ref(false)
const editVisible = ref(false)
const viewVisible = ref(false)
const editFormRef = ref(null)
const viewData = ref(null)

const editForm = reactive({
  id: null,
  promptCode: '',
  promptName: '',
  promptType: 'document_analysis',
  skillTarget: null,
  template: '',
  description: ''
})

const editRules = {
  promptCode: [{ required: true, message: '请输入编码', trigger: 'blur' }],
  promptName: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  promptType: [{ required: true, message: '请选择类型', trigger: 'change' }],
  template: [{ required: true, message: '请输入模板内容', trigger: 'blur' }]
}

const typeMap = { document_analysis: 'primary', extract: 'warning', verify: 'success', validate: 'danger', report: 'info' }
const labelMap = { document_analysis: '文档分析', extract: '字段提取', verify: '验证', validate: '校验', report: '报告' }

function typeTag(t) { return typeMap[t] || 'info' }
function typeLabel(t) { return labelMap[t] || t }

async function loadList() {
  loading.value = true
  try {
    const res = await getPromptList(filterForm)
    // 后端返回 PascalCase 字段名，需要转换为 camelCase
    const rawData = res.Data || res.data || []
    tableData.value = rawData.map(item => ({
      id: item.Id || item.id,
      promptCode: item.PromptCode || item.promptCode,
      promptName: item.PromptName || item.promptName,
      promptType: item.PromptType || item.promptType,
      skillTarget: item.SkillTarget || item.skillTarget,
      template: item.Template || item.template,
      description: item.Description || item.description,
      version: item.Version || item.version,
      isActive: item.IsActive ?? item.isActive,
      enable: item.Enable ?? item.enable,
      code: item.Code || item.code,
      createDate: item.CreateDate || item.createDate,
      creator: item.Creator || item.creator
    }))
  } catch (error) {
    console.error('加载提示词列表失败:', error)
    tableData.value = []
  } finally {
    loading.value = false
  }
}

function resetFilter() {
  filterForm.promptType = ''
  filterForm.skillTarget = ''
  loadList()
}

function openEdit(row) {
  if (row) {
    Object.assign(editForm, {
      id: row.id,
      promptCode: row.promptCode,
      promptName: row.promptName,
      promptType: row.promptType,
      skillTarget: row.skillTarget,
      template: row.template || '',
      description: row.description || ''
    })
  } else {
    resetForm()
  }
  editVisible.value = true
}

function resetForm() {
  editForm.id = null
  editForm.promptCode = ''
  editForm.promptName = ''
  editForm.promptType = 'document_analysis'
  editForm.skillTarget = null
  editForm.template = ''
  editForm.description = ''
  editFormRef.value?.clearValidate()
}

async function doSave() {
  await editFormRef.value.validate()
  saving.value = true
  try {
    const res = await savePrompt(editForm)
    if (res.data?.success !== false) {
      ElMessage.success('保存成功')
      editVisible.value = false
      loadList()
    } else {
      ElMessage.error(res.data?.message || '保存失败')
    }
  } finally {
    saving.value = false
  }
}

async function doDelete(row) {
  await ElMessageBox.confirm(`确定删除提示词「${row.promptName}」？`, '确认删除', { type: 'warning' })
  const res = await deletePrompt(row.promptCode)
  if (res.data?.success !== false) {
    ElMessage.success('删除成功')
    loadList()
  } else {
    ElMessage.error(res.data?.message || '删除失败')
  }
}

async function doActivate(row) {
  const res = await activatePrompt(row.promptCode)
  if (res.data?.success !== false) {
    ElMessage.success('已激活')
    loadList()
  } else {
    ElMessage.error(res.data?.message || '激活失败')
  }
}

function viewPrompt(row) {
  viewData.value = row
  viewVisible.value = true
}

onMounted(loadList)
</script>

<style scoped>
.prompt-template-page { padding: 16px; }
.prompt-template-page :deep(.el-dialog__body) { padding: var(--yzh-space-5, 20px); }
.prompt-template-page :deep(.el-dialog) { border-radius: var(--yzh-radius-lg, 8px); }
.filter-card { margin-bottom: var(--yzh-space-4, 16px); }
.filter-form { display: flex; flex-wrap: wrap; align-items: center; }
.card-header { display: flex; justify-content: space-between; align-items: center; }
.card-title { font-size: 15px; font-weight: var(--yzh-font-weight-bold, 600); display: flex; align-items: center; gap: var(--yzh-space-2, 8px); }
.card-title-icon { color: var(--yzh-color-primary, #409eff); font-size: 16px; }
.prompt-editor :deep(textarea) { font-family: 'Consolas', monospace; font-size: var(--yzh-font-size-sm, 13px); }
.form-hint { color: var(--yzh-color-text-secondary, #909399); font-size: var(--yzh-font-size-xs, 12px); margin-top: 4px; display: flex; align-items: center; gap: 4px; }
.view-section { margin-top: var(--yzh-space-4, 16px); }
.view-section-title { font-weight: var(--yzh-font-weight-bold, 600); margin-bottom: var(--yzh-space-2, 8px); font-size: var(--yzh-font-size-md, 14px); }
.view-template { background: var(--yzh-color-bg-page, #f5f7fa); border: 1px solid var(--yzh-color-border, #e4e7ed); border-radius: var(--yzh-radius-sm, 4px); padding: var(--yzh-space-3, 12px); font-size: var(--yzh-font-size-sm, 13px); line-height: var(--yzh-line-height-base, 1.6); white-space: pre-wrap; word-break: break-all; max-height: 400px; overflow-y: auto; }
.row-actions { display: flex; align-items: center; gap: 4px; white-space: nowrap; }
.row-actions .el-button { margin: 0; padding: 0 4px; }
</style>
