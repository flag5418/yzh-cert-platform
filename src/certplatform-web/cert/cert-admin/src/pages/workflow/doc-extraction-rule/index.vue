<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import { YzhPageLayout } from '@yzh-core'
import { CertDirectoryTree } from '@share/components'
import {
  getRuleDetail,
  saveRule,
  analyzeDoc,
  generatePrompt,
  verifyPrompt,
  type FieldDefDto,
  type TableDefDto,
  type ExtractionData
} from '@share/api/workflow/doc-extraction-rule'
import AIAnalysisTab from './components/AIAnalysisTab.vue'
import PromptVerifyTab from './components/PromptVerifyTab.vue'
import DocPreview from './components/DocPreview.vue'

const selectedFile = ref<any>(null)
const activeTab = ref('analysis')

const skill = ref('')
const fields = ref<FieldDefDto[]>([])
const tables = ref<TableDefDto[]>([])
const prompt = ref('')
const isValid = ref<boolean | null>(null)
const extractionData = ref<ExtractionData | null>(null)

const loading = ref(false)
const saving = ref(false)
const analyzing = ref(false)
const generatingPrompt = ref(false)
const verifying = ref(false)

const leftWidth = ref(280)
const isResizing = ref(false)

async function onFileSelect(file: any) {
  selectedFile.value = file
  fields.value = []
  tables.value = []
  prompt.value = ''
  isValid.value = null
  extractionData.value = null
  activeTab.value = 'analysis'

  await loadExistingRule(file.code)
}

async function loadExistingRule(fileCode: string) {
  loading.value = true
  try {
    const res = await getRuleDetail(fileCode)
    if (!res || res.code === 404) return

    const data = res.data
    if (!data) return

    if (selectedFile.value?.code !== fileCode) return

    skill.value = data.skill || ''
    fields.value = data.fields || []
    tables.value = data.tables || []
    prompt.value = data.prompt || ''
    isValid.value = data.isValid
    extractionData.value = null

    if (prompt.value) {
      activeTab.value = 'prompt'
    }
  } catch {
    // 静默：文件可能无规则
  } finally {
    loading.value = false
  }
}

async function onAIAnalyze() {
  if (!selectedFile.value?.code) {
    ElMessage.warning('请先选择文件')
    return
  }
  analyzing.value = true
  try {
    const res = await analyzeDoc(selectedFile.value.code, skill.value)
    if (!res) return

    const data = res.data
    if (data?.message && data.message !== 'AI分析完成') {
      ElMessage.info(data.message)
    }

    fields.value = data?.fields || []
    tables.value = data?.tables || []

    prompt.value = ''
    isValid.value = null
    extractionData.value = null

    ElMessage.success('AI 分析完成')
  } catch (e: any) {
    ElMessage.error(e?.message || 'AI 分析失败')
  } finally {
    analyzing.value = false
  }
}

async function onGeneratePrompt() {
  if (!selectedFile.value?.code) return
  if (!fields.value.length && !tables.value.length) {
    ElMessage.warning('请先添加字段或表格定义')
    return
  }

  generatingPrompt.value = true
  try {
    const res = await generatePrompt({
      fileCode: selectedFile.value.code,
      fields: fields.value,
      tables: tables.value
    })
    prompt.value = res?.data || ''
    ElMessage.success('Prompt 已生成')
  } catch (e: any) {
    ElMessage.error(e?.message || '生成失败')
  } finally {
    generatingPrompt.value = false
  }
}

async function onVerifyPrompt() {
  if (!selectedFile.value?.code || !prompt.value) return

  verifying.value = true
  try {
    const res = await verifyPrompt({
      fileCode: selectedFile.value.code,
      prompt: prompt.value
    })
    const data = res?.data
    if (data?.success) {
      isValid.value = true
      extractionData.value = data.data || null
      ElMessage.success('验证通过')
    } else {
      isValid.value = false
      extractionData.value = null
      ElMessage.warning(data?.message || '验证失败')
    }
  } catch (e: any) {
    isValid.value = false
    ElMessage.error(e?.message || '验证异常')
  } finally {
    verifying.value = false
  }
}

async function onSave() {
  if (!selectedFile.value?.code) return

  saving.value = true
  try {
    await saveRule({
      fileCode: selectedFile.value.code,
      skill: skill.value,
      fields: fields.value,
      tables: tables.value,
      prompt: prompt.value,
      isValid: isValid.value === true,
      extractionData: extractionData.value || undefined
    })
    ElMessage.success('保存成功')
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

function onFieldsUpdate(newFields: FieldDefDto[]) {
  fields.value = newFields
  prompt.value = ''
  isValid.value = null
  extractionData.value = null
}

function onTablesUpdate(newTables: TableDefDto[]) {
  tables.value = newTables
  prompt.value = ''
  isValid.value = null
  extractionData.value = null
}

function onPromptUpdate(val: string) {
  prompt.value = val
  isValid.value = null
  extractionData.value = null
}

function startResizeLeft(e: MouseEvent) {
  isResizing.value = true
  const startX = e.clientX
  const startWidth = leftWidth.value

  const onMove = (ev: MouseEvent) => {
    const delta = ev.clientX - startX
    leftWidth.value = Math.min(600, Math.max(200, startWidth + delta))
  }
  const onUp = () => {
    isResizing.value = false
    document.removeEventListener('mousemove', onMove)
    document.removeEventListener('mouseup', onUp)
  }
  document.addEventListener('mousemove', onMove)
  document.addEventListener('mouseup', onUp)
}
</script>

<template>
  <YzhPageLayout title="文档提取规则">
    <template #default>
      <div class="three-column-layout">
        <div class="left-panel" :style="{ width: leftWidth + 'px' }">
          <el-card shadow="never" class="full-height-card">
            <template #header>
              <div class="panel-header">
                <span>文件目录</span>
              </div>
            </template>
            <div class="tree-container">
              <CertDirectoryTree @select="onFileSelect" />
            </div>
          </el-card>
        </div>

        <div class="resize-handle" @mousedown="startResizeLeft" />

        <div class="center-panel">
          <el-card shadow="never" class="full-height-card">
            <template #header>
              <div class="panel-header">
                <span v-if="selectedFile">{{ selectedFile.name || selectedFile.fileName }}</span>
                <span v-else class="placeholder">请选择文件</span>
              </div>
            </template>
            <DocPreview v-if="selectedFile" :file="selectedFile" />
            <el-empty v-else description="请选择左侧文件进行预览" />
          </el-card>
        </div>

        <div class="right-panel">
          <el-card shadow="never" class="full-height-card">
            <template #header>
              <div class="panel-header">
                <span>规则配置</span>
                <el-tag v-if="fields.length || tables.length" size="small" type="info">
                  {{ fields.length }} 字段 / {{ tables.length }} 表格
                </el-tag>
              </div>
            </template>

            <el-tabs v-model="activeTab" class="config-tabs">
              <el-tab-pane label="AI 分析" name="analysis">
                <AIAnalysisTab
                  :fields="fields"
                  :tables="tables"
                  :analyzing="analyzing"
                  @analyze="onAIAnalyze"
                  @update:fields="onFieldsUpdate"
                  @update:tables="onTablesUpdate"
                />
              </el-tab-pane>
              <el-tab-pane label="提示词验证" name="prompt">
                <PromptVerifyTab
                  :fields="fields"
                  :tables="tables"
                  :prompt="prompt"
                  :is-valid="isValid ?? false"
                  :verifying="verifying"
                  :generating="generatingPrompt"
                  @update:prompt="onPromptUpdate"
                  @generate="onGeneratePrompt"
                  @verify="onVerifyPrompt"
                />
              </el-tab-pane>
            </el-tabs>

            <div class="bottom-actions">
              <el-button @click="activeTab = 'analysis'">取消</el-button>
              <el-button type="primary" :loading="saving" @click="onSave">保存规则</el-button>
            </div>
          </el-card>
        </div>
      </div>
    </template>
  </YzhPageLayout>
</template>

<style scoped>
.three-column-layout {
  display: flex;
  height: calc(100vh - 120px);
  gap: 0;
}
.left-panel,
.center-panel,
.right-panel {
  display: flex;
  flex-direction: column;
}
.left-panel { flex-shrink: 0; }
.center-panel { flex: 1; min-width: 0; }
.right-panel { width: 480px; flex-shrink: 0; }

.full-height-card {
  height: 100%;
  display: flex;
  flex-direction: column;
}
.full-height-card :deep(.el-card__body) {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  padding: 16px;
}

.tree-container {
  flex: 1;
  overflow-y: auto;
}

.resize-handle {
  width: 4px;
  cursor: col-resize;
  background: #e4e7ed;
  transition: background 0.2s;
}
.resize-handle:hover {
  background: #409eff;
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.placeholder {
  color: #c0c4cc;
}

.config-tabs {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}
.config-tabs :deep(.el-tabs__header) {
  margin-bottom: 0;
}
.config-tabs :deep(.el-tabs__content) {
  flex: 1;
  overflow: hidden;
}
.config-tabs :deep(.el-tab-pane) {
  height: 100%;
  overflow-y: auto;
}

.bottom-actions {
  padding: 12px 0 0;
  border-top: 1px solid #ebeef5;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  flex-shrink: 0;
}
</style>
