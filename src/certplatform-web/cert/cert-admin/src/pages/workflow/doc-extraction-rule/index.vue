<script setup lang="ts">
import { ref, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { MagicStick, ChatLineSquare } from '@element-plus/icons-vue'
import { YzhPageLayout } from '@yzh-core'
import { CertDirectoryTree } from '@share/components'
import { retryFailedConversions } from '@share/composables/useDirectoryApi'
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

const treeRef = ref<any>(null)
const selectedFile = ref<any>(null)
const activeTab = ref('analysis')

const skill = ref('')
const fields = ref<FieldDefDto[]>([])
const tables = ref<TableDefDto[]>([])
const prompt = ref('')
const isValid = ref<boolean | null>(null)
const extractionData = ref<ExtractionData | null>(null)
const ruleStatus = ref<'none' | 'configured' | 'failed'>('none')

const loading = ref(false)
const saving = ref(false)
const analyzing = ref(false)
const generatingPrompt = ref(false)
const verifying = ref(false)
const retrying = ref(false)

const leftWidth = ref(280)
const isResizing = ref(false)

/* ============ 规则状态（状态栏 + 目录树标签） ============ */
const ruleStatusType = computed(() => {
  const map: Record<string, string> = { none: 'info', configured: 'success', failed: 'danger' }
  return (map[ruleStatus.value] || 'info') as any
})
const ruleStatusText = computed(() => {
  const map: Record<string, string> = { none: '未配置', configured: '已配置', failed: '配置失败' }
  return map[ruleStatus.value] || '未知'
})

async function onFileSelect(file: any) {
  selectedFile.value = file
  fields.value = []
  tables.value = []
  prompt.value = ''
  isValid.value = null
  extractionData.value = null
  ruleStatus.value = file?.ruleStatus || 'none'
  activeTab.value = 'analysis'
  await loadExistingRule(file.fileCode || file.FileCode)
}

async function loadExistingRule(fileCode: string) {
  if (!fileCode) return
  loading.value = true
  try {
    const res: any = await getRuleDetail(fileCode)
    const data = res?.data
    if (!data) return
    // 防止快速切换文件时旧响应覆盖新选择
    if ((selectedFile.value?.fileCode || selectedFile.value?.FileCode) !== fileCode) return

    skill.value = data.skill || ''
    fields.value = data.fields || []
    tables.value = data.tables || []
    prompt.value = data.prompt || ''
    isValid.value = data.isValid ?? null
    extractionData.value = null
    ruleStatus.value = data.isValid === false ? 'failed' : 'configured'

    // 已有 Prompt 的文件直接落到「提示词与验证」页签
    if (prompt.value) activeTab.value = 'prompt'
  } catch {
    // 静默：文件可能尚无规则
    ruleStatus.value = 'none'
  } finally {
    loading.value = false
  }
}

async function onAIAnalyze() {
  if (!selectedFile.value?.fileCode) {
    ElMessage.warning('请先选择一个文件')
    return
  }
  analyzing.value = true
  try {
    const res: any = await analyzeDoc(selectedFile.value.fileCode, skill.value)
    const data = res?.data
    if (data?.message && data.message !== 'AI分析完成') {
      ElMessage.warning(data.message)
    } else {
      ElMessage.success('分析完成')
    }
    fields.value = data?.fields || []
    tables.value = data?.tables || []
    prompt.value = ''
    isValid.value = null
    extractionData.value = null
  } catch (e: any) {
    ElMessage.error('AI分析失败: ' + (e?.message || '未知错误'))
  } finally {
    analyzing.value = false
  }
}

async function onGeneratePrompt() {
  if (!selectedFile.value?.fileCode) return
  if (!fields.value.length && !tables.value.length) {
    ElMessage.warning('请先在「自动分析」页签添加至少一个字段或表格')
    return
  }

  generatingPrompt.value = true
  try {
    const res: any = await generatePrompt({
      fileCode: selectedFile.value.fileCode,
      fields: fields.value,
      tables: tables.value
    })
    const generated = res?.data
    if (generated) {
      prompt.value = generated
      isValid.value = null
      extractionData.value = null
      ElMessage.success('Prompt 生成成功')
    } else {
      ElMessage.warning('生成失败：未返回 Prompt 内容')
    }
  } catch (e: any) {
    ElMessage.error('生成失败: ' + (e?.message || '未知错误'))
  } finally {
    generatingPrompt.value = false
  }
}

async function onVerifyPrompt() {
  if (!selectedFile.value?.fileCode || !prompt.value) {
    ElMessage.warning('请先生成 Prompt')
    return
  }

  verifying.value = true
  try {
    const res: any = await verifyPrompt({
      fileCode: selectedFile.value.fileCode,
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
    ElMessage.error('验证失败: ' + (e?.message || '未知错误'))
  } finally {
    verifying.value = false
  }
}

async function onSave() {
  if (!selectedFile.value?.fileCode) {
    ElMessage.warning('请先选择一个文件')
    return
  }

  saving.value = true
  try {
    await saveRule({
      fileCode: selectedFile.value.fileCode,
      skill: skill.value,
      fields: fields.value,
      tables: tables.value,
      prompt: prompt.value,
      isValid: isValid.value === true,
      extractionData: extractionData.value || undefined
    })
    ruleStatus.value = isValid.value === true ? 'configured' : 'failed'
    ElMessage.success('规则保存成功')
    // 刷新目录树的规则状态标签（已配置 / 未配置 / 配置失败）
    await treeRef.value?.refresh?.()
  } catch (e: any) {
    ElMessage.error('保存失败: ' + (e?.message || '未知错误'))
  } finally {
    saving.value = false
  }
}

function onFieldsUpdate(newFields: FieldDefDto[]) {
  fields.value = newFields
  // 字段变更 → 旧 Prompt 与验证结果失效
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

/* 重试失败的文档转换：把转换失败/未转换的 doc、xls 重新入队，完成后树自动刷新 */
async function onRetryFailed() {
  try {
    await ElMessageBox.confirm(
      '将把转换失败或未转换的 doc、xls 文件重新加入转换队列（文件会在转换期间暂时隐藏，完成后自动恢复）。确定继续吗？',
      '重试失败转换',
      { type: 'warning', confirmButtonText: '开始重试', cancelButtonText: '取消' }
    )
  } catch {
    return
  }

  retrying.value = true
  try {
    const { ok, message, enqueued } = await retryFailedConversions()
    if (!ok) {
      ElMessage.error(message || '重试失败')
      return
    }
    if (enqueued > 0) {
      ElMessage.success(`${message || '已重新入队'}（${enqueued} 个文件）`)
    } else {
      ElMessage.info(message || '没有需要重试的文件')
    }
    await treeRef.value?.refresh?.()
  } catch (e: any) {
    ElMessage.error('重试失败：' + (e?.message || '未知错误'))
  } finally {
    retrying.value = false
  }
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
    document.body.style.cursor = ''
    document.body.style.userSelect = ''
  }
  document.addEventListener('mousemove', onMove)
  document.addEventListener('mouseup', onUp)
  document.body.style.cursor = 'col-resize'
  document.body.style.userSelect = 'none'
}
</script>

<template>
  <YzhPageLayout title="文档提取规则" no-padding hide-toolbar>
    <div class="three-column-layout">
      <!-- 左侧：文件目录（复用证书目录通用件） -->
      <div class="left-panel" :style="{ width: leftWidth + 'px' }">
        <CertDirectoryTree
          ref="treeRef"
          title="文件目录"
          filterable
          show-rule-status
          :show-convert-badge="true"
          @select="onFileSelect"
        >
          <template #header-actions>
            <el-button
              size="small"
              type="primary"
              plain
              :loading="retrying"
              title="将转换失败/未转换的 doc、xls 文件重新加入转换队列"
              @click.stop="onRetryFailed"
            >
              重试失败转换
            </el-button>
          </template>
        </CertDirectoryTree>
      </div>

      <!-- 拖拽调整宽度 -->
      <div class="resize-handle" @mousedown.prevent="startResizeLeft">
        <div class="resize-bar"></div>
      </div>

      <!-- 中间：文档预览 -->
      <div class="center-panel">
        <DocPreview v-if="selectedFile" :file="selectedFile" />
        <div v-else class="empty-preview">
          <el-empty description="请选择左侧文档进行预览" :image-size="90" />
        </div>
      </div>

      <!-- 右侧：规则配置 -->
      <div
        class="right-panel"
        v-loading="analyzing"
        element-loading-text="AI 分析中，请稍候…"
        element-loading-background="rgba(255, 255, 255, 0.7)"
      >
        <div v-if="selectedFile" class="status-bar">
          <div class="status-item">
            <el-tag :type="ruleStatusType" size="small" effect="light">{{ ruleStatusText }}</el-tag>
            <span class="label">规则状态</span>
          </div>
          <div class="status-item">
            <span class="value">{{ fields.length }}</span>
            <span class="label">字段数</span>
          </div>
          <div class="status-item">
            <span class="value">{{ tables.length }}</span>
            <span class="label">表格数</span>
          </div>
        </div>

        <el-tabs v-model="activeTab" class="right-tabs">
          <el-tab-pane name="analysis">
            <template #label>
              <span class="tab-label"><el-icon><MagicStick /></el-icon>自动分析</span>
            </template>
            <AIAnalysisTab
              :fields="fields"
              :tables="tables"
              :analyzing="analyzing"
              @analyze="onAIAnalyze"
              @update:fields="onFieldsUpdate"
              @update:tables="onTablesUpdate"
            />
          </el-tab-pane>
          <el-tab-pane name="prompt">
            <template #label>
              <span class="tab-label"><el-icon><ChatLineSquare /></el-icon>提示词与验证</span>
            </template>
            <PromptVerifyTab
              :fields="fields"
              :tables="tables"
              :prompt="prompt"
              :is-valid="isValid"
              :verifying="verifying"
              :generating="generatingPrompt"
              @update:prompt="onPromptUpdate"
              @generate="onGeneratePrompt"
              @verify="onVerifyPrompt"
            />
          </el-tab-pane>
        </el-tabs>

        <!-- 底部操作（历史项目仅在提示词页签显示） -->
        <div v-if="activeTab === 'prompt'" class="bottom-actions">
          <el-button @click="activeTab = 'analysis'">取消</el-button>
          <el-button type="primary" :loading="saving" @click="onSave">保存规则</el-button>
        </div>
      </div>
    </div>
  </YzhPageLayout>
</template>

<style scoped>
.three-column-layout {
  display: flex;
  flex: 1;
  min-height: 0;
  height: 100%;
  background: #fff;
}

.left-panel {
  min-width: 200px;
  max-width: 600px;
  flex-shrink: 0;
  background: #fff;
  border-right: 1px solid var(--el-border-color-lighter);
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.resize-handle {
  width: 4px;
  flex-shrink: 0;
  cursor: col-resize;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color 0.2s;
}
.resize-handle:hover,
.resize-handle:active {
  background: #c6e2ff;
}
.resize-bar {
  width: 2px;
  height: 40px;
  background: #e4e7ed;
  border-radius: 9999px;
  transition: all 0.2s;
}
.resize-handle:hover .resize-bar {
  background: #409eff;
  height: 50px;
}

.center-panel {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  background: #fff;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
  overflow: hidden;
}
.empty-preview {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

.right-panel {
  width: 360px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  background: #fff;
  border-left: 1px solid var(--el-border-color-lighter);
  overflow: hidden;
}

.status-bar {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.status-item {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 4px;
  border-right: 1px solid #ebeef5;
}
.status-item:last-child {
  border-right: none;
}
.status-item .label {
  font-size: 12px;
  color: #909399;
}
.status-item .value {
  font-size: 18px;
  font-weight: 700;
  color: #303133;
  line-height: 1;
}

.right-tabs {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}
.tab-label {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.right-tabs :deep(.el-tabs__header) {
  margin: 0;
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.right-tabs :deep(.el-tabs__nav-wrap) {
  padding: 0 16px;
}
.right-tabs :deep(.el-tabs__item) {
  height: 44px;
  line-height: 44px;
  font-size: 13px;
}
.right-tabs :deep(.el-tabs__content) {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: 16px;
}

.bottom-actions {
  flex-shrink: 0;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding: 12px 16px;
  border-top: 1px solid var(--el-border-color-lighter);
  background: #fff;
}
</style>
