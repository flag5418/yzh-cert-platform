<template>
  <div class="doc-extraction-rule">
    <!-- 三栏布局：目录树 / 预览 / 操作区 -->
    <div class="main-container">
      <!-- 左侧：文件目录树（certcore 通用件，全局复用） -->
      <div class="left-panel" :style="{ width: leftPanelWidth + 'px' }">
        <CertDirectoryTree
          ref="treeRef"
          :selected-file="currentFile"
          mode="file"
          :show-convert-badge="true"
          :show-rule-status="true"
          filterable
          @select="onFileSelect"
          @lock-warning="onFileLockWarning"
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
              <el-icon style="margin-right: 4px"><IconRefresh /></el-icon>重试失败转换
            </el-button>
          </template>
        </CertDirectoryTree>
      </div>

      <!-- 左侧拖拽调整宽度 -->
      <div class="resize-handle resize-handle-left" @mousedown.prevent="startResizeLeft">
        <div class="resize-bar"></div>
      </div>

      <!-- 中间：文档预览 -->
      <div class="center-panel">
        <DocPreview v-if="currentFile && !lockReason" :file="currentFile" />
        <div v-else-if="lockReason" class="lock-state-panel">
          <YzhLockStatus
            title="文件处理中，暂不可操作"
            :description="lockReason"
            tag-text="队列执行期间锁定"
          />
        </div>
        <div v-else class="empty-preview">
          <YzhEmptyState :icon="IconFile" title="请选择左侧文档进行预览" description="从目录树中选择一份文档查看内容" />
        </div>
      </div>

      <!-- 右侧：操作区 -->
      <div class="right-panel">
        <!-- 状态栏 -->
        <div class="status-bar" v-if="currentFile">
          <div class="status-item">
            <span class="label">规则状态：</span>
            <YzhStatusBadge :type="ruleStatusType" :text="ruleStatusText" />
          </div>
          <div class="status-item">
            <span class="label">字段数：</span>
            <span class="value">{{ fieldCount }}个</span>
          </div>
          <div class="status-item">
            <span class="label">表格数：</span>
            <span class="value">{{ tableCount }}个</span>
          </div>
        </div>

        <!-- Tab 切换 -->
        <el-tabs v-model="activeTab" class="right-tabs">
          <el-tab-pane name="analysis">
            <template #label>
              <span class="tab-label"><el-icon><IconAnalyze /></el-icon>自动分析</span>
            </template>
            <AIAnalysisTab
              :fields="analysisFields"
              :tables="analysisTables"
              :raw-json="rawJsonDisplay"
              @analyze="onAIAnalyze"
              @update:fields="onFieldsUpdate"
              @update:tables="onTablesUpdate"
            />
          </el-tab-pane>
          <el-tab-pane name="prompt">
            <template #label>
              <span class="tab-label"><el-icon><IconPrompt /></el-icon>提示词与验证</span>
            </template>
            <PromptVerifyTab
              :prompt="generatedPrompt"
              :verify-result="verifyResult"
              @generate="onGeneratePrompt"
              @verify="onVerifyPrompt"
              @update:prompt="onPromptUpdate"
            />
          </el-tab-pane>
        </el-tabs>

        <!-- 底部操作（仅在提示词Tab显示） -->
        <div class="bottom-actions" v-if="activeTab === 'prompt'">
          <el-button @click="cancel">取消</el-button>
          <el-button type="primary" @click="saveRule" :loading="saving">保存规则</el-button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { CertDirectoryTree } from '@/certcore'
import { YzhEmptyState, YzhStatusBadge, YzhLockStatus } from '@/yzh'
import { IconFile, IconAnalyze, IconPrompt, IconRefresh, IconLoading } from '@/yzh'
import { useYzhQueue } from '@/yzh/composables/useYzhQueue'
import { aiAnalyzeDocument } from './api'
import AIAnalysisTab from './components/AIAnalysisTab.vue'
import DocPreview from './components/DocPreview.vue'
import PromptVerifyTab from './components/PromptVerifyTab.vue'

const router = useRouter()
const treeRef = ref(null)
const { retryFailedConversions } = useYzhQueue()

// 状态
const activeTab = ref('analysis')
const leftPanelWidth = ref(280) // 左侧面板默认宽度
const currentFile = ref(null)
const saving = ref(false)
const retrying = ref(false)
const lockReason = ref('') // 当前文件锁定原因（队列处理中 / 上传中）

// AI分析结果
const analysisFields = ref([])
const analysisTables = ref([])

// Prompt和验证结果
const generatedPrompt = ref('')
const verifyResult = ref(null)
const rawJsonDisplay = ref('')

// 规则状态
const ruleStatus = ref('none') // none, configured, failed

// 计算属性
const ruleStatusType = computed(() => {
  const map = { none: 'info', configured: 'success', failed: 'danger' }
  return map[ruleStatus.value] || 'info'
})

const ruleStatusText = computed(() => {
  const map = { none: '未配置', configured: '已配置', failed: '配置失败' }
  return map[ruleStatus.value] || '未知'
})

const fieldCount = computed(() => analysisFields.value.length)
const tableCount = computed(() => analysisTables.value.length)

// 方法
const onFileSelect = (file) => {
  console.log('[DocExtractionRule] ✅ onFileSelect 触发:',
    { id: file?.id, name: file?.name, type: file?.type, storagePath: file?.storagePath, mimeType: file?.mimeType })
  currentFile.value = file
  lockReason.value = ''
  // 重置状态
  analysisFields.value = []
  analysisTables.value = []
  generatedPrompt.value = ''
  verifyResult.value = null
  activeTab.value = 'analysis'
}

const onFileLockWarning = (info) => {
  const reason = info.queueCode === 'uploading'
    ? `文件「${info.fileName}」正在上传处理中，请稍后再试`
    : `文件「${info.fileName}」正被队列 ${info.queueCode} 处理中，请稍后再试`
  lockReason.value = reason
  ElMessage.warning(reason)
}

const onAIAnalyze = async () => {
  if (!currentFile.value?.fileCode) {
    ElMessage.warning('请先选择一个文件')
    return
  }
  const fileCode = currentFile.value.fileCode
  // 根据 mimeType 推断 skill
  const mimeType = currentFile.value.mimeType || ''
  let skill = 'word'
  if (mimeType.includes('excel') || fileCode.toLowerCase().endsWith('.xlsx') || fileCode.toLowerCase().endsWith('.xls'))
    skill = 'excel'
  else if (mimeType.includes('pdf') || fileCode.toLowerCase().endsWith('.pdf'))
    skill = 'pdf'

  console.log('[DocExtractionRule] 🔍 开始分析:', { fileCode, skill })
  ElMessage.info('AI分析中...')

  try {
    const res = await aiAnalyzeDocument({ fileCode, skill })
    console.log('[DocExtractionRule] 📦 analyze 响应:', JSON.stringify(res, null, 2))

    // 解析响应数据（兼容 Fields/Tables 大写与 fields/tables 小写）
    const data = res?.Data ?? res?.data ?? res
    if (data?.fields || data?.Fields) {
      analysisFields.value = (data.fields || data.Fields || []).map(f => ({
        name: f.fieldName ?? f.name ?? f.field_code ?? '',
        dataType: f.dataType ?? 'string',
        description: f.description ?? '',
        isManual: f.isManual ?? false
      }))
    }
    if (data?.tables || data?.Tables) {
      analysisTables.value = (data.tables || data.Tables || []).map(t => ({
        name: t.tableName ?? t.name ?? t.table_code ?? '',
        description: t.description ?? '',
        columns: (t.columns ?? t.Columns ?? []).map(c => ({
          name: c.columnName ?? c.name ?? c.column_code ?? '',
          dataType: c.dataType ?? 'string'
        }))
      }))
    }
    // 原始JSON展示
    rawJsonDisplay.value = JSON.stringify(data ?? res, null, 2)
    // 后端返回的 Message 可能包含明确原因（如文件转换中/转换失败/不支持的类型），必须展示给用户
    const message = data?.Message ?? data?.message
    if (message && message !== 'AI分析完成') {
      ElMessage.warning(message)
    } else {
      ElMessage.success('分析完成')
    }
  } catch (err) {
    console.error('[DocExtractionRule] ❌ 分析失败:', err)
    ElMessage.error('AI分析失败: ' + (err?.message ?? '未知错误'))
  }
}

const onFieldsUpdate = (fields) => {
  analysisFields.value = fields
}

const onTablesUpdate = (tables) => {
  analysisTables.value = tables
}

const onGeneratePrompt = async () => {
  // TODO: 调用后端生成Prompt接口（功能待开发）
  ElMessage.info('生成Prompt中...')
}

const onVerifyPrompt = async () => {
  // TODO: 调用后端验证接口（功能待开发）
  ElMessage.info('验证中...')
}

const onPromptUpdate = (prompt) => {
  generatedPrompt.value = prompt
}

const saveRule = async () => {
  saving.value = true
  try {
    // TODO: 调用后端保存接口（功能待开发）
    ElMessage.success('规则保存成功')
    ruleStatus.value = 'configured'
  } finally {
    saving.value = false
  }
}

const cancel = () => {
  router.back()
}

/* 重试失败的文档转换：把转换失败/未转换的 doc、xls 重新入队，队列完成后树自动刷新 */
const onRetryFailed = async () => {
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
    const { ok, message, data } = await retryFailedConversions()
    if (!ok) {
      ElMessage.error(message || '重试失败')
      return
    }
    const enqueued = data?.enqueued ?? 0
    if (enqueued > 0) {
      ElMessage.success(`${message}（${enqueued} 个文件）`)
      // 文件已置为隐藏，立即刷新树
      treeRef.value?.refresh?.()
    } else {
      ElMessage.info(message || '没有需要重试的文件')
    }
  } catch (err) {
    console.error('[DocExtractionRule] 重试失败转换出错:', err)
    ElMessage.error('重试失败：' + (err?.message ?? '未知错误'))
  } finally {
    retrying.value = false
  }
}

// ====== 左侧面板拖拽调整宽度 ======
const startResizeLeft = (e) => {
  const startX = e.clientX
  const startWidth = leftPanelWidth.value

  const onMouseMove = (e) => {
    const delta = e.clientX - startX
    // 限制最小200px，最大600px
    leftPanelWidth.value = Math.max(200, Math.min(600, startWidth + delta))
  }

  const onMouseUp = () => {
    document.removeEventListener('mousemove', onMouseMove)
    document.removeEventListener('mouseup', onMouseUp)
    document.body.style.cursor = ''
    document.body.style.userSelect = ''
  }

  document.addEventListener('mousemove', onMouseMove)
  document.addEventListener('mouseup', onMouseUp)
  document.body.style.cursor = 'col-resize'
  document.body.style.userSelect = 'none'
}
</script>

<style scoped>
/* 令牌引用：yzh 设计令牌 + certcore 业务令牌 */
@import '@/yzh/styles/yzh.css';
@import '@/certcore/styles/cert-tokens.css';

.doc-extraction-rule {
  /* 认证平台统一页面容器：16/24/16/24 留白 + 灰底 */
  position: absolute;
  top: 16px;
  left: 24px;
  right: 24px;
  bottom: 16px;
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-4, 16px);
  background: var(--yzh-color-bg-page, #f5f7fa);
  border-radius: var(--yzh-radius-sm, 4px);
  overflow: hidden;
}

.main-container {
  flex: 1;
  min-height: 0;
  display: flex;
  overflow: hidden;
}

/* 左侧面板：独立白色卡片 */
.left-panel {
  min-width: 200px;
  max-width: 600px;
  flex-shrink: 0;
  background: var(--yzh-color-bg-card, #fff);
  border: 1px solid var(--yzh-color-border, #e4e7ed);
  border-radius: var(--yzh-radius-sm, 4px);
  box-shadow: var(--yzh-shadow-sm, 0 1px 4px rgba(0, 0, 0, 0.04));
  overflow: hidden;
  box-sizing: border-box;
}

/* 拖拽调整宽度手柄 */
.resize-handle {
  width: var(--yzh-space-1, 4px);
  flex-shrink: 0;
  cursor: col-resize;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color 0.2s;
}

.resize-handle:hover,
.resize-handle:active {
  background: var(--yzh-color-primary-light-7, #c6e2ff);
}

.resize-bar {
  width: 2px;
  height: 40px;
  background: var(--yzh-color-border, #e4e7ed);
  border-radius: var(--yzh-radius-full, 9999px);
  transition: all 0.2s;
}

.resize-handle:hover .resize-bar {
  background: var(--yzh-color-primary, #409eff);
  height: 50px;
}

/* 中间面板：预览卡片 */
.center-panel {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.empty-preview {
  flex: 1;
  min-height: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--yzh-color-bg-card, #fff);
  border: 1px solid var(--yzh-color-border, #e4e7ed);
  border-radius: var(--yzh-radius-sm, 4px);
  box-shadow: var(--yzh-shadow-sm, 0 1px 4px rgba(0, 0, 0, 0.04));
}

/* 右侧面板：独立白色卡片 */
.right-panel {
  width: 480px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  background: var(--yzh-color-bg-card, #fff);
  border: 1px solid var(--yzh-color-border, #e4e7ed);
  border-radius: var(--yzh-radius-sm, 4px);
  box-shadow: var(--yzh-shadow-sm, 0 1px 4px rgba(0, 0, 0, 0.04));
  box-sizing: border-box;
  overflow: hidden;
}

/* 状态栏 */
.status-bar {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: var(--yzh-space-8, 32px);
  padding: var(--yzh-space-3, 12px) var(--yzh-space-5, 20px);
  background: var(--yzh-color-bg-page, #f5f7fa);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
}

.status-item {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-sm, 13px);
}

.status-item .label {
  color: var(--yzh-color-text-regular, #606266);
}

.status-item .value {
  font-weight: var(--yzh-font-weight-bold, 600);
  color: var(--yzh-color-text-primary, #303133);
}

/* Tab */
.right-tabs {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.tab-label {
  display: inline-flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
}

.right-tabs :deep(.el-tabs__header) {
  margin: 0;
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
}

.right-tabs :deep(.el-tabs__item) {
  height: 44px;
  line-height: 44px;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
  padding: 0 var(--yzh-space-5, 20px);
}

/* EP 内置「首/末 Tab 去内边距」规则特异性更高，直接给 nav-wrap 加内边距更可靠，且与内容区 20px 对齐 */
.right-tabs :deep(.el-tabs__nav-wrap) {
  padding: 0 var(--yzh-space-5, 20px);
}

.right-tabs :deep(.el-tabs__item.is-active) {
  color: var(--yzh-color-primary, #409eff);
  font-weight: var(--yzh-font-weight-medium, 500);
}

.right-tabs :deep(.el-tabs__content) {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: var(--yzh-space-5, 20px);
}

/* 底部操作栏 */
.bottom-actions {
  flex-shrink: 0;
  display: flex;
  justify-content: space-between;
  padding: var(--yzh-space-3, 12px) var(--yzh-space-5, 20px);
  border-top: 1px solid var(--yzh-color-border-light, #ebeef5);
  background: var(--yzh-color-bg-page, #f5f7fa);
}
</style>
