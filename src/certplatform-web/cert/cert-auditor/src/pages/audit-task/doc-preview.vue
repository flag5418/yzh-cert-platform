<script setup lang="ts">
/**
 * 证据原文预览页（专家端 · 路由 /audit-task/doc-preview）
 *
 * 用途：
 *   - 从认证工作台侧边栏「原始证据」点击跳转而来
 *   - 独立新窗口展示文档全文，支持锚点定位（高亮引用片段）
 *   - 只读预览，不可编辑
 *
 * 入参（route.query）：
 *   fileCode       企业文档 Code（审核发现「查看原文」跳转；优先于下面的证据模式）
 *   enterpriseCode 所属企业 Code（fileCode 模式必填）
 *   sourceCode     证据源文档 Code（cert_evidence.SourceCode）
 *   evidenceCode   当前证据 Code（用于高亮）
 *   anchor         锚点定位（可选）
 *
 * 文档全文来源：
 *   - fileCode 模式 → 与 /enterprise-doc 同款 PDF 预览（企业文档原件）
 *   - 证据模式     → /api/Auditor/CertEvidence/detail
 */
import { ref, onMounted, onBeforeUnmount, computed } from 'vue'
import { useRoute } from 'vue-router'
import { ElButton, ElDescriptions, ElDescriptionsItem } from 'element-plus'
import { YzhStatusBadge, YzhEmptyState } from '@yzh-core'
import { yzhApi } from '@yzh-core/api/client'
import { entDocPreviewBlob } from '@share/api/ent/enterprise-doc'

const route = useRoute()

/** 企业文档全文模式（审核发现「查看原文」） */
const fileCode = ref<string>((route.query.fileCode as string) || '')
const enterpriseCode = ref<string>((route.query.enterpriseCode as string) || '')
const isFileMode = computed(() => !!fileCode.value && !!enterpriseCode.value)

const sourceCode = ref<string>((route.query.sourceCode as string) || '')
const evidenceCode = ref<string>((route.query.evidenceCode as string) || '')
const anchor = ref<string>((route.query.anchor as string) || '')

/**
 * 文档定位编码：优先 sourceCode（源文档），
 * 缺失时退回 evidenceCode —— 工作台侧边栏「原始证据」只传 evidenceCode，
 * 而 CertEvidence/detail 同样支持按证据 Code 直接查询。
 */
const docCode = computed(() => sourceCode.value || evidenceCode.value)

const loading = ref(false)
const doc = ref<any>(null)
/** 企业文档 PDF 预览对象 URL（fileCode 模式，与 /enterprise-doc 同款） */
const previewUrl = ref('')
const error = ref('')

const fullText = computed(
  () => doc.value?.Content || doc.value?.TranscriptText || doc.value?.Markdown || '',
)
const hasContent = computed(() => !!fullText.value)
const canCloseWindow = computed(() => typeof window !== 'undefined' && !!window.opener)

function closeWindow() {
  window.close()
}

/** 释放 PDF 预览对象 URL，避免内存泄漏 */
function releasePreview(): void {
  if (previewUrl.value) {
    URL.revokeObjectURL(previewUrl.value)
    previewUrl.value = ''
  }
}

async function loadDoc() {
  // fileCode 模式：企业文档 PDF 原件预览（审核发现「查看原文」）
  if (isFileMode.value) {
    loading.value = true
    error.value = ''
    releasePreview()
    try {
      const blob = await entDocPreviewBlob(fileCode.value, enterpriseCode.value)
      previewUrl.value = URL.createObjectURL(blob)
    } catch (e: any) {
      error.value = e?.message || '文档预览加载失败'
    } finally {
      loading.value = false
    }
    return
  }

  if (!docCode.value) {
    error.value = '缺少文档编码（sourceCode / evidenceCode）'
    return
  }
  loading.value = true
  error.value = ''
  try {
    const res = await yzhApi.get('/api/Auditor/CertEvidence/detail', { params: { code: docCode.value } })
    if (res?.success && res.data) {
      doc.value = res.data
    } else {
      error.value = res?.message || '文档不存在或已删除'
    }
  } catch (e: any) {
    error.value = e?.message || '加载失败'
  } finally {
    loading.value = false
  }
}

onMounted(loadDoc)
onBeforeUnmount(releasePreview)
</script>

<template>
  <div class="doc-preview">
    <!-- 头部 -->
    <div class="doc-preview__header">
      <div class="doc-preview__title">{{ isFileMode ? '企业文档原文' : '证据原文预览' }}</div>
      <div class="doc-preview__meta" v-if="isFileMode">
        <YzhStatusBadge type="info" size="small">{{ anchor ? `条款 ${anchor}` : '全文' }}</YzhStatusBadge>
      </div>
      <div class="doc-preview__meta" v-else-if="doc">
        <YzhStatusBadge type="info" size="small">{{ doc.SourceName || doc.Code }}</YzhStatusBadge>
        <YzhStatusBadge v-if="doc.EvidenceKind" type="info" size="small">{{ doc.EvidenceKind }}</YzhStatusBadge>
        <YzhStatusBadge v-if="doc.CapturedIn" type="success" size="small">{{ doc.CapturedIn }}</YzhStatusBadge>
      </div>
      <el-button type="default" size="small" @click="closeWindow" v-if="canCloseWindow">关闭窗口</el-button>
    </div>

    <!-- 文档元信息 -->
    <div class="doc-preview__info" v-if="doc">
      <el-descriptions :column="3" border size="small">
        <el-descriptions-item label="来源">{{ doc.SourceName || '-' }}</el-descriptions-item>
        <el-descriptions-item label="类型">{{ doc.EvidenceKind || '-' }}</el-descriptions-item>
        <el-descriptions-item label="采集阶段">{{ doc.CapturedIn || '-' }}</el-descriptions-item>
        <el-descriptions-item label="内容指纹">{{ (doc.ContentMD5 || '').slice(0, 12) }}...</el-descriptions-item>
        <el-descriptions-item label="采集时间">{{ doc.CapturedAt || '-' }}</el-descriptions-item>
      </el-descriptions>
    </div>

    <!-- 文档全文 -->
    <div class="doc-preview__content" v-loading="loading">
      <template v-if="isFileMode">
        <iframe v-if="previewUrl" :src="previewUrl" class="doc-preview__frame" title="PDF 预览" />
        <YzhEmptyState v-else-if="error" :title="error" :image-size="80" />
        <YzhEmptyState v-else-if="!loading" title="PDF 预览还在生成" :image-size="80" />
      </template>
      <template v-else-if="hasContent">
        <div class="doc-preview__text">
          {{ fullText }}
        </div>
      </template>
      <YzhEmptyState v-else-if="error" :title="error" :image-size="80" />
      <YzhEmptyState v-else-if="!loading" title="文档暂无全文内容" :image-size="80" />
      <div v-else class="doc-preview__placeholder">加载文档中...</div>
    </div>

    <!-- 片段锚点提示 -->
    <div class="doc-preview__anchor-bar" v-if="anchor">
      <YzhStatusBadge type="warning" size="small">锚点：{{ anchor }}</YzhStatusBadge>
      <span class="doc-preview__anchor-tip">该片段在文档中对应位置已通过 LLM 关联</span>
    </div>
  </div>
</template>

<style scoped>
.doc-preview {
  display: flex;
  flex-direction: column;
  height: 100vh;
  background: var(--el-bg-color);
  overflow: hidden;
}
.doc-preview__header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: var(--yzh-space-2, 8px) var(--yzh-space-4, 16px);
  border-bottom: 1px solid var(--el-border-color-lighter);
  flex-shrink: 0;
}
.doc-preview__title {
  font-size: var(--yzh-font-size-lg, 16px);
  font-weight: 600;
  color: var(--el-text-color-primary);
}
.doc-preview__meta {
  display: flex;
  gap: 6px;
  flex: 1;
}
.doc-preview__info {
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  flex-shrink: 0;
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.doc-preview__content {
  flex: 1;
  overflow-y: auto;
  padding: var(--yzh-space-4, 16px);
  background: var(--el-bg-color-page);
}
.doc-preview__frame {
  display: block;
  width: 100%;
  height: 100%;
  min-height: 100%;
  border: 0;
  border-radius: 4px;
  background: var(--el-bg-color);
}
.doc-preview__text {
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: 1.8;
  color: var(--el-text-color-regular);
  white-space: pre-wrap;
  word-break: break-word;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  padding: var(--yzh-space-4, 16px);
}
.doc-preview__placeholder {
  padding: var(--yzh-space-8, 32px);
  text-align: center;
  color: var(--el-text-color-placeholder);
  font-size: var(--yzh-font-size-sm, 13px);
}
.doc-preview__anchor-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: var(--yzh-space-2, 8px) var(--yzh-space-4, 16px);
  border-top: 1px solid var(--el-border-color-lighter);
  background: var(--el-color-warning-light-9);
  flex-shrink: 0;
}
.doc-preview__anchor-tip {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-secondary);
}
</style>
