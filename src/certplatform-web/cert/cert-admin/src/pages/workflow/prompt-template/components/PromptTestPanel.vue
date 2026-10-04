<script setup lang="ts">
/**
 * 语义测试面板（33 号 §4.2 / 34 号 §九）
 *
 * ★ 2026-10-02 重构：由「永久第三栏」改为「可折叠的下方面板」。
 *   原因：提示词正文 1500–2500 字是**纵向阅读**的长文，左右分栏会把它压成窄条
 *   （实测中栏仅 474px ≈ 34 字/行）；而测试是偶发动作却永久占掉 410px。
 *   上下分栏同时满足「正文够宽」与「边改边看结果」。
 *
 * 迭代闭环：真实上传 → 分析 → 改提示词 → 复用缓存重测（零转换）。
 * Markdown 缓存 8 小时，键由后端回传。
 * ⛔ 不展示 AI 模型：模型由 `cert_sys_config` 统一固定，UI 不可选也不展示。
 */
import { YzhEmptyState } from '@yzh-core'
import { ref, computed } from 'vue'
import { Upload, RefreshRight, Delete, Close, Files } from '@element-plus/icons-vue'
import type { PromptTestResultDto, ConvertLogDto } from '@share/api/workflow/prompt-workbench'
// ★ accept 来自共享上传契约（⛔ 本页不再硬编码后缀串；改规则只改 constants/upload-file-policy.ts）
import { buildAcceptAttribute } from '@share/constants/upload-file-policy'

/** 提示词试跑：只要文档 + 文本，不需要图片 */
const ACCEPT = buildAcceptAttribute(['document', 'pdf'])
// 2026-10-03：SemanticResult 上移到 @share/components（36 号 §6.6），
//   供 36 号「企业原始资料管理」的分析结果抽屉共用同一渲染器。
import { SemanticResult } from '@share/components'

const props = defineProps<{
  promptType: string
  standardCode: string
  cacheKey?: string | null
  testing: boolean
  result: PromptTestResultDto | null
}>()

const emit = defineEmits<{
  (e: 'update:cacheKey', v: string): void
  (e: 'run', files: File[]): void
}>()

const fileInput = ref<HTMLInputElement>()
const files = ref<File[]>([])
const activeTab = ref('result')

function pickFiles() {
  fileInput.value?.click()
}

function onFilesChange(e: Event) {
  const el = e.target as HTMLInputElement
  if (!el.files?.length) return
  // 同名去重：重复点选不会堆出重复项
  const incoming = Array.from(el.files)
  const names = new Set(files.value.map((f) => f.name))
  files.value = files.value.concat(incoming.filter((f) => !names.has(f.name)))
  el.value = ''
}

function removeFile(i: number) {
  files.value.splice(i, 1)
}

function clearFiles() {
  files.value = []
}

function runWithFiles() {
  if (!files.value.length) return
  emit('run', files.value)
}

function runWithCache() {
  emit('run', [])
}

// ==================== 结果展示 ====================

const prettyJson = computed(() => {
  const raw = props.result?.jsonOutput
  if (!raw) return ''
  try {
    return JSON.stringify(JSON.parse(raw), null, 2)
  } catch {
    return raw
  }
})

const validationMessages = computed(() => props.result?.validation?.messages || [])

const okFiles = computed<ConvertLogDto[]>(() => (props.result?.files || []).filter((f) => f.success))

const hasFiles = computed(() => (props.result?.files || []).length > 0)

/** 单行运行指标（⛔ 不含模型名 —— 模型由系统参数统一固定，UI 不展示） */
const metaLine = computed(() => {
  const r = props.result
  if (!r) return ''
  const parts = [
    `${r.durationMs} ms`,
    `${r.promptTokens ?? '-'} / ${r.completionTokens ?? '-'} tokens`,
    hasFiles.value
      ? `${r.cacheHit ? '缓存文件' : '转换'} ${okFiles.value.length}/${(r.files || []).length}`
      : '',
    props.cacheKey ? `cache ${props.cacheKey.slice(0, 8)}…` : ''
  ].filter(Boolean)
  return parts.join(' · ')
})
</script>

<template>
  <div class="test-panel">
    <!-- 上传区 -->
    <div class="test-panel__upload">
      <div class="test-panel__row">
        <input
          ref="fileInput"
          type="file"
          multiple
          style="display: none"
          :accept="ACCEPT"
          @change="onFilesChange"
        />
        <el-button :icon="Upload" @click="pickFiles">选择文件</el-button>
        <el-button :icon="Delete" :disabled="!files.length" @click="clearFiles"> 清空 </el-button>
        <div class="test-panel__spacer" />
        <el-button
          type="primary"
          :loading="testing"
          :disabled="!files.length"
          :icon="RefreshRight"
          @click="runWithFiles"
        >
          分析
        </el-button>
        <el-button
          :loading="testing"
          :disabled="!cacheKey"
          :title="cacheKey ? '复用已转换的 Markdown（零转换）' : '无缓存'"
          @click="runWithCache"
        >
          重测
        </el-button>
      </div>

      <div v-if="files.length" class="test-panel__filelist">
        <div v-for="(f, i) in files" :key="f.name + i" class="test-panel__file">
          <span class="test-panel__fname" :title="f.name">{{ f.name }}</span>
          <span class="test-panel__fsize">{{ (f.size / 1024).toFixed(1) }} KB</span>
          <el-icon class="test-panel__fdel" @click="removeFile(i)"><Close /></el-icon>
        </div>
      </div>
    </div>

    <!-- 结果区 -->
    <div class="test-panel__result">
      <YzhEmptyState :icon="Files" v-if="!result" title="无分析结果" />

      <template v-else>
        <!-- 状态头 -->
        <div class="test-panel__status">
          <el-tag :type="result.success ? 'success' : 'danger'" size="small" effect="dark">
            {{ result.success ? '分析通过' : '分析失败' }}
          </el-tag>
          <el-tag v-if="result.cacheHit" size="small" effect="plain">缓存命中</el-tag>
          <span v-if="!result.success" class="test-panel__msg" :title="result.message">
            {{ result.message }}
          </span>
        </div>
        <div class="test-panel__meta">{{ metaLine }}</div>

        <!-- 后端校正明细（33 号 §3.3） -->
        <div v-if="validationMessages.length" class="test-panel__valid">
          <div class="test-panel__valid-title">校正 {{ validationMessages.length }} 处</div>
          <ul>
            <li v-for="(m, i) in validationMessages" :key="i">{{ m }}</li>
          </ul>
        </div>

        <!-- 明细 -->
        <el-tabs v-model="activeTab" class="test-panel__tabs">
          <el-tab-pane label="结果" name="result">
            <SemanticResult :prompt-type="promptType" :json="result.jsonOutput" />
          </el-tab-pane>

          <el-tab-pane label="JSON" name="json">
            <pre class="test-panel__code">{{ prettyJson || '(无 JSON 输出)' }}</pre>
          </el-tab-pane>

          <el-tab-pane label="转换日志" name="files">
            <div v-if="!hasFiles" class="test-panel__none">复用缓存，未重新转换</div>
            <el-collapse v-else>
              <el-collapse-item
                v-for="(f, i) in result.files"
                :key="i"
                :name="i"
              >
                <template #title>
                  <span class="test-panel__fitem">
                    <el-tag :type="f.success ? 'success' : 'danger'" size="small" effect="plain">
                      {{ f.success ? 'OK' : 'FAIL' }}
                    </el-tag>
                    <span class="test-panel__fname">{{ f.fileName }}</span>
                    <span class="test-panel__fsize">{{ f.markdownLength }} 字符</span>
                  </span>
                </template>
                <div v-if="!f.success" class="test-panel__fail">{{ f.message || '转换失败' }}</div>
                <pre v-else class="test-panel__code test-panel__code--md">{{ f.markdown || f.markdownHead || '' }}</pre>
              </el-collapse-item>
            </el-collapse>
          </el-tab-pane>

          <el-tab-pane label="提示词" name="prompt">
            <pre class="test-panel__code">{{ result.promptText || '(空)' }}</pre>
          </el-tab-pane>

          <el-tab-pane v-if="result.rawOutput" label="原始输出" name="raw">
            <pre class="test-panel__code">{{ result.rawOutput }}</pre>
          </el-tab-pane>
        </el-tabs>
      </template>
    </div>
  </div>
</template>

<style scoped>
.test-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

/* ---------- 上传区 ---------- */
.test-panel__upload {
  flex-shrink: 0;
  padding: var(--yzh-space-3) var(--yzh-space-4);
  border-bottom: 1px solid var(--yzh-color-border-light);
  background: var(--yzh-color-bg-subtle);
}
.test-panel__row {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2);
}
.test-panel__spacer {
  flex: 1;
}
.test-panel__filelist {
  margin-top: var(--yzh-space-2);
  max-height: 72px;
  overflow: auto;
  display: flex;
  flex-wrap: wrap;
  gap: var(--yzh-space-1) var(--yzh-space-2);
}
.test-panel__file {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2);
  font-size: var(--yzh-font-size-xs);
  padding: 3px var(--yzh-space-2);
  background: var(--yzh-color-bg-container);
  border: 1px solid var(--yzh-color-border-light);
  border-radius: var(--yzh-radius-sm);
}
.test-panel__fname {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--yzh-color-text-regular);
}
.test-panel__fsize {
  flex-shrink: 0;
  color: var(--yzh-color-text-placeholder);
}
.test-panel__fdel {
  cursor: pointer;
  color: var(--yzh-color-text-placeholder);
}
.test-panel__fdel:hover {
  color: var(--yzh-color-danger);
}
/* ---------- 结果区 ---------- */
.test-panel__result {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: var(--yzh-space-3) var(--yzh-space-4);
}

.test-panel__status {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--yzh-space-1) var(--yzh-space-2);
}
.test-panel__msg {
  flex: 1;
  min-width: 120px;
  font-size: var(--yzh-font-size-xs);
  color: var(--yzh-color-danger);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.test-panel__meta {
  margin-top: var(--yzh-space-1);
  font-size: var(--yzh-font-size-xs);
  line-height: var(--yzh-line-height-base);
  font-variant-numeric: tabular-nums;
  color: var(--yzh-color-text-placeholder);
  word-break: break-all;
}

.test-panel__valid {
  margin-top: var(--yzh-space-3);
  padding: var(--yzh-space-2) var(--yzh-space-3);
  background: var(--yzh-color-warning-light-9);
  border: 1px solid var(--el-color-warning-light-7);
  border-radius: var(--yzh-radius-md);
}
.test-panel__valid-title {
  font-size: var(--yzh-font-size-xs);
  font-weight: var(--yzh-font-weight-semibold);
  color: var(--yzh-color-warning);
  margin-bottom: var(--yzh-space-1);
}
.test-panel__valid ul {
  margin: 0;
  padding-left: 18px;
}
.test-panel__valid li {
  font-size: var(--yzh-font-size-xs);
  line-height: var(--yzh-line-height-base);
  color: var(--yzh-color-text-regular);
  word-break: break-all;
}

.test-panel__tabs {
  margin-top: var(--yzh-space-3);
}
.test-panel__tabs :deep(.el-tabs__header) {
  margin-bottom: var(--yzh-space-2);
}
.test-panel__tabs :deep(.el-tabs__item) {
  height: 32px;
  line-height: 32px;
  font-size: var(--yzh-font-size-xs);
  padding: 0 var(--yzh-space-3);
}
.test-panel__tabs :deep(.el-tabs__content) {
  padding: 0;
}

.test-panel__code {
  margin: 0;
  max-height: 320px;
  overflow: auto;
  padding: var(--yzh-space-3);
  background: #1e1e1e;
  color: #d4d4d4;
  border-radius: var(--yzh-radius-md);
  font-family: 'SFMono-Regular', Consolas, Menlo, monospace;
  font-size: var(--yzh-font-size-xs);
  line-height: var(--yzh-line-height-base);
  white-space: pre-wrap;
  word-break: break-all;
}
.test-panel__code--md {
  background: #f6f8fa;
  color: #24292f;
  max-height: 260px;
}
.test-panel__none {
  font-size: var(--yzh-font-size-xs);
  color: var(--yzh-color-text-tertiary);
  padding: var(--yzh-space-2) 0;
}
.test-panel__fail {
  font-size: var(--yzh-font-size-xs);
  color: var(--yzh-color-danger);
}
.test-panel__fitem {
  display: inline-flex;
  align-items: center;
  gap: var(--yzh-space-2);
  min-width: 0;
  padding-right: var(--yzh-space-2);
}
</style>
