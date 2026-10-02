<script setup lang="ts">
/**
 * PromptTestPanel —— 提示词试跑面板（2026-10-02）
 *
 * 用户口径（逐字）：
 *   「我们还可以在该界面上传文件进行测试（上传的时候，自动转 markdown），
 *     调用提示词得到结果，后直接删除该文件，因为该功能非常重要，我们需要不停的尝试完善提示词」
 *
 * ★ 文件生命周期：浏览器 → 后端内存 → 转换容器临时目录 → **后端 finally 清理**。
 *   不落 MinIO、不落 DB、不留痕。本组件只负责「选文件 → 提交 → 展示」。
 *
 * ★ 用「当前编辑器里未保存的内容」试跑（template 传参）—— 这是「边改边试」的关键：
 *   不必先保存才能试。
 */
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { Delete, UploadFilled, VideoPlay } from '@element-plus/icons-vue'
import type { UploadUserFile } from 'element-plus'
import { PROMPT_TYPE, testPrompt, type PromptTestResultDto } from '@share/api/workflow/prompt-workbench'

const props = defineProps<{
  /** 当前提示词类型：doc_group / doc_content */
  promptType: string
  /** 当前适用标准（GUID，可空） */
  standardCode?: string | null
  /** ★ 编辑器里**未保存**的提示词正文（为空则后端回落库里的生效版本） */
  template?: string | null
}>()

const fileList = ref<UploadUserFile[]>([])
const running = ref(false)
const result = ref<PromptTestResultDto | null>(null)
const activeResultTab = ref('parsed')

/** 分类提示词可多文件（逐个取标题+开头片段）；作用提示词只用第一个文件 */
const isGroup = computed(() => props.promptType === PROMPT_TYPE.Group)

const canRun = computed(() => fileList.value.length > 0 && !running.value)

const typeHint = computed(() =>
  isGroup.value
    ? '分类提示词：可一次上传多份资料，系统取每份的「标题 + 开头片段」组成文件清单，让 AI 逐个归类。'
    : '作用提示词：只取**第一份**文件，转换 Markdown 全文后交给 AI 判断其作用。'
)

/** 解析后的 JSON（美化）；解析失败返回 null */
const prettyJson = computed(() => {
  const raw = result.value?.jsonOutput
  if (!raw) return null
  try {
    return JSON.stringify(JSON.parse(raw), null, 2)
  } catch {
    return null
  }
})

const conclusionType = computed(() => {
  if (!result.value) return 'info'
  return result.value.success ? 'success' : 'warning'
})

const conclusionText = computed(() => {
  if (!result.value) return ''
  return result.value.success ? '试跑成功' : `试跑未通过：${result.value.message}`
})

function onExceed() {
  ElMessage.warning('一次最多 20 个文件，请分批测试')
}

function clearFiles() {
  fileList.value = []
  result.value = null
}

async function onRun() {
  const files = fileList.value
    .map((f) => f.raw)
    .filter(Boolean) as File[]
  if (!files.length) {
    ElMessage.warning('请先选择要测试的文件')
    return
  }

  running.value = true
  result.value = null
  try {
    const r = await testPrompt({
      files,
      promptType: props.promptType,
      template: props.template || null,
      standardCode: props.standardCode || null
    })
    result.value = r
    activeResultTab.value = r.jsonOutput ? 'parsed' : 'raw'
    if (r.success) {
      ElMessage.success(`试跑完成（${r.durationMs} ms）`)
    } else {
      ElMessage.warning(r.message || '试跑未通过')
    }
  } catch (e: any) {
    ElMessage.error(e?.message || '试跑失败')
  } finally {
    running.value = false
  }
}

async function copyText(text?: string | null) {
  if (!text) return
  try {
    await navigator.clipboard.writeText(text)
    ElMessage.success('已复制')
  } catch {
    ElMessage.error('复制失败')
  }
}
</script>

<template>
  <div class="test-panel">
    <!-- ── 上传区 ───────────────────────────────────────────── -->
    <div class="panel-section">
      <div class="section-head">
        <h4>上传试跑</h4>
        <span class="head-hint">文件即用即弃，不落库</span>
      </div>

      <el-upload
        v-model:file-list="fileList"
        drag
        multiple
        :auto-upload="false"
        :limit="20"
        :on-exceed="onExceed"
        accept=".doc,.docx,.xls,.xlsx,.pdf,.txt,.md,.csv,.wps,.et,.ppt,.pptx"
        class="test-upload"
      >
        <el-icon class="el-icon--upload"><UploadFilled /></el-icon>
        <div class="el-upload__text">拖拽文件到此处，或<em>点击选择</em></div>
        <template #tip>
          <div class="el-upload__tip">
            支持 Word / Excel / PDF / 文本；上传后自动转 Markdown。已选 {{ fileList.length }} 个文件。
          </div>
        </template>
      </el-upload>

      <el-alert :title="typeHint" type="info" :closable="false" show-icon class="type-hint" />

      <div class="run-bar">
        <el-button
          type="primary"
          :icon="VideoPlay"
          :loading="running"
          :disabled="!canRun"
          @click="onRun"
        >
          {{ running ? '试跑中…' : '用当前提示词试跑' }}
        </el-button>
        <el-button :icon="Delete" :disabled="running || !fileList.length" @click="clearFiles">
          清空
        </el-button>
        <span class="run-hint">
          {{ template ? '使用编辑器中的当前内容' : '编辑器为空 → 使用库里的生效版本' }}
        </span>
      </div>
    </div>

    <!-- ── 结果区 ───────────────────────────────────────────── -->
    <div v-if="result" class="panel-section result-section">
      <el-alert :type="conclusionType" :title="conclusionText" :closable="false" show-icon />

      <div class="meta-bar">
        <el-tag size="small" type="info" effect="plain">模型 {{ result.model || '—' }}</el-tag>
        <el-tag size="small" type="info" effect="plain">max_tokens {{ result.maxTokens ?? '—' }}</el-tag>
        <el-tag size="small" type="info" effect="plain">温度 {{ result.temperature ?? '—' }}</el-tag>
        <el-tag size="small" type="info" effect="plain">{{ result.durationMs }} ms</el-tag>
        <el-tag v-if="result.promptTokens != null" size="small" type="info" effect="plain">
          in {{ result.promptTokens }} / out {{ result.completionTokens ?? 0 }} tokens
        </el-tag>
      </div>

      <!-- 转换日志：确认「文件确实被读到了」 -->
      <!-- ⚠️ 此处刻意不用 el-table 内联表格：守卫 R6 要求页面统一用 YzhTable，
           而这段只有 4 个字段、数据来自一次响应、无分页/排序需求 ——
           硬套 YzhTable 的 dataLoader 契约反而更绕。用纯布局渲染。 -->
      <div class="block">
        <h5>① 转换结果（上传 → Markdown）</h5>
        <div class="convert-list">
          <div v-for="(f, i) in result.files" :key="i" class="convert-row">
            <el-tag :type="f.success ? 'success' : 'danger'" size="small" effect="plain">
              {{ f.success ? '成功' : '失败' }}
            </el-tag>
            <span class="convert-name" :title="f.fileName">{{ f.fileName }}</span>
            <span class="convert-len">{{ f.markdownLength }} 字</span>
            <span class="convert-msg">
              <span v-if="f.message" class="err-text">{{ f.message }}</span>
              <span v-else class="ok-text">已转 Markdown</span>
            </span>
          </div>
        </div>
      </div>

      <!-- 结果本体 -->
      <div class="block">
        <h5>
          ② AI 返回结果
          <el-button link size="small" @click="copyText(result.rawOutput)">复制原文</el-button>
        </h5>
        <el-tabs v-model="activeResultTab" class="result-tabs">
          <el-tab-pane name="parsed" :disabled="!prettyJson">
            <template #label>
              <span>解析后 JSON<span v-if="!prettyJson" class="tab-note">（无）</span></span>
            </template>
            <pre v-if="prettyJson" class="code-block">{{ prettyJson }}</pre>
            <el-empty v-else description="模型未返回可解析的 JSON（见「原始输出」）" :image-size="60" />
          </el-tab-pane>
          <el-tab-pane label="原始输出" name="raw">
            <pre v-if="result.rawOutput" class="code-block">{{ result.rawOutput }}</pre>
            <el-empty v-else description="无输出" :image-size="60" />
          </el-tab-pane>
          <el-tab-pane label="实际送出的提示词" name="prompt">
            <div class="prompt-pre-head">
              <span class="hint">用于确认占位符（{{ '{{file_list}}' }} / {{ '{{document_content}}' }}）替换是否正确</span>
              <el-button link size="small" @click="copyText(result.promptText)">复制</el-button>
            </div>
            <pre v-if="result.promptText" class="code-block">{{ result.promptText }}</pre>
          </el-tab-pane>
        </el-tabs>
      </div>
    </div>

    <el-empty v-else description="选择文件后点「试跑」，这里显示结果" :image-size="70" />
  </div>
</template>

<style scoped>
.test-panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: 100%;
  overflow: auto;
  padding: 16px;
  box-sizing: border-box;
}

.panel-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.section-head {
  display: flex;
  align-items: baseline;
  gap: 8px;
}
.section-head h4 {
  margin: 0;
  font-size: 14px;
  font-weight: 600;
  color: #303133;
}
.head-hint {
  font-size: 12px;
  color: #909399;
}

.test-upload :deep(.el-upload-dragger) {
  padding: 20px 12px;
}

.type-hint :deep(.el-alert__title) {
  font-size: 12px;
  line-height: 1.6;
}

.run-bar {
  display: flex;
  align-items: center;
  gap: 8px;
}
.run-hint {
  font-size: 12px;
  color: #909399;
  margin-left: auto;
}

.result-section {
  border-top: 1px solid #ebeef5;
  padding-top: 14px;
}

.meta-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.block h5 {
  margin: 0 0 8px 0;
  font-size: 13px;
  font-weight: 600;
  color: #303133;
  display: flex;
  align-items: center;
  gap: 8px;
}

.err-text {
  color: #f56c6c;
}
.ok-text {
  color: #67c23a;
}

.convert-list {
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 4px;
  overflow: hidden;
}
.convert-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 10px;
  font-size: 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.convert-row:last-child {
  border-bottom: none;
}
.convert-row:nth-child(odd) {
  background: #fafcff;
}
.convert-name {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: #303133;
}
.convert-len {
  flex-shrink: 0;
  color: #909399;
  font-variant-numeric: tabular-nums;
}
.convert-msg {
  flex-shrink: 0;
  max-width: 160px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.result-tabs {
  margin-top: 4px;
}
.tab-note {
  color: #c0c4cc;
}

.code-block {
  margin: 0;
  padding: 12px;
  background: #1e1e1e;
  color: #d4d4d4;
  border-radius: 4px;
  font-family: 'Courier New', monospace;
  font-size: 12px;
  line-height: 1.65;
  max-height: 420px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-all;
}

.prompt-pre-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 6px;
}
.prompt-pre-head .hint {
  font-size: 12px;
  color: #909399;
}
</style>
