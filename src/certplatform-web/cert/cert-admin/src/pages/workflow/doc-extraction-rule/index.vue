<script setup lang="ts">
import { ref, computed } from 'vue'
import { ElMessage } from 'element-plus'
import { MagicStick, ChatLineSquare, Pointer } from '@element-plus/icons-vue'
import { confirmOrFalse, YzhPageLayout, YzhEmptyState } from '@yzh-core'
import { CertDirectoryTree, DocPreview } from '@share/components'
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

/** 当前选中文件编码：树节点为 PascalCase（FileCode），此处统一收敛为唯一读取点 */
const selectedFileCode = computed(
  () => selectedFile.value?.fileCode || selectedFile.value?.FileCode || selectedFile.value?.Raw?.FileCode || ''
)

/**
 * ★ 该文件是否**不支持提取规则**（2026-10-09）。
 *
 * <para>判据只有一条：后端 Markdown 提取链把它判成了 <c>unsupported</c> —— 这是**能力边界**，
 * 不是故障。典型来源是 anydoc 与视觉模型都不认的文件类型（如 <c>.txt</c>），
 * 见 <c>FileConvertCore.ConvertMarkdownInnerAsync</c> 的分支 B。</para>
 *
 * <para>⛔ 为什么不用扩展名在前端自己猜：那会与后端两套判据，一旦 anydoc 新增支持格式，
 * 前端就会**拦下一个其实能处理的文件**（用户永远发现不了）。以后端落库的状态为唯一权威。</para>
 */
const extractionUnsupported = computed(() => String(selectedFile.value?.markdownStatus || '').toLowerCase() === 'unsupported')

/**
 * 不支持时的原因文案。
 *
 * 主用后端 `MarkdownMessage`（它本身就是写给用户看的一整句，含「可手工定义字段与表格，由人工填写」
 * 这样的行动指引）；历史数据 / 老接口可能为空 ⇒ 兜底句必须**同样含行动指引**，
 * ⛔ 不能只说「不支持」把用户留在原地。
 */
const unsupportedReason = computed(
  () =>
    selectedFile.value?.markdownMessage ||
    '该文件的类型无法被自动识别，不能自动提取内容。可手工定义字段与表格，由人工填写。'
)

async function onFileSelect(file: any) {
  // 树节点是 PascalCase 形态（FileCode/Name/Raw/ConvertStatus…），归一化为 camelCase，
  // 供本页守卫与 DocPreview 统一读取
  selectedFile.value = {
    ...file,
    fileCode: file.fileCode || file.FileCode || file.Raw?.FileCode || file.Code || '',
    fileName: file.fileName || file.FileName || file.Name || '',
    storagePath: file.storagePath || file.StoragePath || file.Raw?.StoragePath || '',
    convertedStoragePath:
      file.convertedStoragePath || file.ConvertedStoragePath || file.Raw?.ConvertedStoragePath || '',
    convertStatus: file.convertStatus || file.ConvertStatus || file.Raw?.ConvertStatus || '',
    convertMessage: file.convertMessage || file.ConvertMessage || file.Raw?.ConvertMessage || '',
    // ★ 提取链状态 + 原因（2026-10-09）：unsupported 的文件点开就要给出明确提示
    markdownStatus: file.markdownStatus || file.MarkdownStatus || file.Raw?.MarkdownStatus || '',
    markdownMessage: file.markdownMessage || file.MarkdownMessage || file.Raw?.MarkdownMessage || '',
    ruleStatus: file.ruleStatus || file.RuleStatus || 'none'
  }
  fields.value = []
  tables.value = []
  prompt.value = ''
  isValid.value = null
  extractionData.value = null
  ruleStatus.value = selectedFile.value.ruleStatus || 'none'
  activeTab.value = 'analysis'

  // ★ 点击即提示（2026-10-09）：不能让用户先点「开始分析」、等一轮往返、再被告知不行。
  //   用 warning 而非 error —— 这是能力边界，不是故障（⛔ 别把「不支持」渲染成系统出错）。
  if (extractionUnsupported.value) {
    ElMessage.warning(`该文件不支持提取规则：${unsupportedReason.value}`)
  }

  await loadExistingRule(selectedFileCode.value)
}

async function loadExistingRule(fileCode: string) {
  if (!fileCode) return
  loading.value = true
  try {
    const res: any = await getRuleDetail(fileCode)
    const data = res?.data
    if (!data) return
    // 防止快速切换文件时旧响应覆盖新选择
    if (selectedFileCode.value !== fileCode) return

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
  if (!selectedFileCode.value) {
    ElMessage.warning('请先选择一个文件')
    return
  }
  // ★ 前置守卫（2026-10-09）：不支持提取的文件不必再跑一次后端 ——
  //   后端 GetDocumentMarkdownAsync 见到 unsupported 会短路返回同一句话，白跑一轮往返。
  //   按钮已置灰，这里是键盘/程序触发的兜底。
  if (extractionUnsupported.value) {
    ElMessage.warning(`该文件不支持提取规则：${unsupportedReason.value}`)
    return
  }
  analyzing.value = true
  try {
    const res: any = await analyzeDoc(selectedFileCode.value, skill.value)
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
  if (!selectedFileCode.value) return
  if (!fields.value.length && !tables.value.length) {
    ElMessage.warning('请先在「自动分析」页签添加至少一个字段或表格')
    return
  }

  generatingPrompt.value = true
  try {
    const res: any = await generatePrompt({
      fileCode: selectedFileCode.value,
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
  if (!selectedFileCode.value) {
    ElMessage.warning('请先选择一个文件')
    return
  }
  // 提示词可为空：后端会用「固定提示词」（按本文件已配置的字段/表格清单生成）

  verifying.value = true
  try {
    const res: any = await verifyPrompt({
      fileCode: selectedFileCode.value,
      prompt: prompt.value || ''
    })
    const data = res?.data
    if (data?.success) {
      isValid.value = true
      extractionData.value = data.data || null
      ElMessage.success('验证通过')
    } else {
      isValid.value = false
      extractionData.value = null
      ElMessage.warning(res?.err || data?.message || '验证失败')
    }
  } catch (e: any) {
    isValid.value = false
    ElMessage.error('验证失败: ' + (e?.message || '未知错误'))
  } finally {
    verifying.value = false
  }
}

async function onSave() {
  if (!selectedFileCode.value) {
    ElMessage.warning('请先选择一个文件')
    return
  }

  saving.value = true
  try {
    await saveRule({
      fileCode: selectedFileCode.value,
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
  const ok = await confirmOrFalse(
    '将把转换失败或未转换的 doc、xls 文件重新加入转换队列（文件会在转换期间暂时隐藏，完成后自动恢复）。确定继续吗？',
    '重试失败转换',
    { type: 'warning', confirmButtonText: '开始重试', cancelButtonText: '取消' }
  )
  if (!ok) return

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
          <YzhEmptyState :icon="Pointer" title="请选择左侧文档进行预览" />
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

        <!-- ★ 不支持提取规则：常驻说明（2026-10-09）
             为什么要常驻而不只弹一次 toast：toast 会消失，用户回头再看就只剩一个「需人工填写」徽标，
             无从判断是「还没转」还是「转不了」。⛔ 也不禁用整个面板 —— 该文件仍可**手工**定义
             字段与表格并保存规则（后端 MarkdownMessage 明写「可手工定义字段与表格，由人工填写」）。 -->
        <el-alert
          v-if="extractionUnsupported"
          class="unsupported-alert"
          type="warning"
          show-icon
          :closable="false"
          title="该文件不支持提取规则"
          :description="unsupportedReason"
        />

        <el-tabs v-model="activeTab" class="right-tabs">
          <el-tab-pane name="analysis">
            <template #label>
              <span class="tab-label"><el-icon><MagicStick /></el-icon>自动分析</span>
            </template>
            <AIAnalysisTab
              :fields="fields"
              :tables="tables"
              :analyzing="analyzing"
              :unsupported="extractionUnsupported"
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
              :extraction-data="extractionData"
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
  background: var(--yzh-color-bg-container, #fff);
}

.left-panel {
  min-width: 200px;
  max-width: 600px;
  flex-shrink: 0;
  background: var(--yzh-color-bg-container, #fff);
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
  background: var(--yzh-color-border, #e4e7ed);
  border-radius: 9999px;
  transition: all 0.2s;
}
.resize-handle:hover .resize-bar {
  background: var(--yzh-color-primary, #409eff);
  height: 50px;
}

.center-panel {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  background: var(--yzh-color-bg-container, #fff);
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
  background: var(--yzh-color-bg-container, #fff);
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

/* ★ 「不支持提取规则」常驻提示（2026-10-09）
   ⚠️ 必须 flex-shrink:0：.right-panel 是 column flex + overflow:hidden，
      el-alert 自身也带 overflow:hidden ⇒ 不给 flex-shrink:0 会被压成一条线（同族坑见 enterprise-normalize）。 */
.unsupported-alert {
  flex-shrink: 0;
  margin: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px) 0;
}
.status-item {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 4px;
  border-right: 1px solid var(--yzh-color-border-light, #ebeef5);
}
.status-item:last-child {
  border-right: none;
}
.status-item .label {
  font-size: 12px;
  color: var(--yzh-color-text-tertiary, #909399);
}
.status-item .value {
  font-size: 18px;
  font-weight: 700;
  color: var(--yzh-color-text-primary, #303133);
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
  background: var(--yzh-color-bg-container, #fff);
}
</style>
