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
 *   sourceCode   证据源文档 Code（cert_evidence.SourceCode）
 *   evidenceCode 当前证据 Code（用于高亮）
 *   anchor       锚点定位（可选）
 *
 * 文档全文来源：/api/Auditor/CertEvidence/detail
 */
import { ref, onMounted, computed } from 'vue'
import { useRoute } from 'vue-router'
import { ElButton, ElTag, ElDescriptions, ElDescriptionsItem, Empty } from 'element-plus'
import { yzhApi } from '@yzh-core/api/client'

const route = useRoute()

const sourceCode = ref<string>((route.query.sourceCode as string) || '')
const evidenceCode = ref<string>((route.query.evidenceCode as string) || '')
const anchor = ref<string>((route.query.anchor as string) || '')

const loading = ref(false)
const doc = ref<any>(null)
const error = ref('')

const hasContent = computed(() => !!(doc.value?.Content || doc.value?.TranscriptText || doc.value?.Markdown))

async function loadDoc() {
  if (!sourceCode.value) {
    error.value = '缺少源文档编码（sourceCode）'
    return
  }
  loading.value = true
  error.value = ''
  try {
    const res = await yzhApi.get('/api/Auditor/CertEvidence/detail', { params: { code: sourceCode.value } })
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
</script>

<template>
  <div class="doc-preview">
    <!-- 头部 -->
    <div class="doc-preview__header">
      <div class="doc-preview__title">证据原文预览</div>
      <div class="doc-preview__meta" v-if="doc">
        <el-tag size="small" effect="plain">{{ doc.SourceName || doc.Code }}</el-tag>
        <el-tag v-if="doc.EvidenceKind" size="small" type="info" effect="plain">{{ doc.EvidenceKind }}</el-tag>
        <el-tag v-if="doc.CapturedIn" size="small" type="success" effect="plain">{{ doc.CapturedIn }}</el-tag>
      </div>
      <el-button size="small" @click="window.close()" v-if="window.opener">关闭窗口</el-button>
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
      <template v-if="doc">
        <div v-if="hasContent" class="doc-preview__text">
          {{ doc.Content || doc.TranscriptText || doc.Markdown }}
        </div>
        <el-empty v-else description="文档暂无全文内容（仅有结构化证据片段）" :image-size="80" />
      </template>
      <el-empty v-else-if="error" :description="error" :image-size="80" />
      <div v-else-if="!loading" class="doc-preview__placeholder">加载文档中...</div>
    </div>

    <!-- 片段锚点提示 -->
    <div class="doc-preview__anchor-bar" v-if="anchor">
      <el-tag type="warning" size="small">锚点：{{ anchor }}</el-tag>
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
  padding: 10px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  flex-shrink: 0;
}
.doc-preview__title {
  font-size: 15px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}
.doc-preview__meta {
  display: flex;
  gap: 6px;
  flex: 1;
}
.doc-preview__info {
  padding: 12px 16px;
  flex-shrink: 0;
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.doc-preview__content {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  background: var(--el-bg-color-page);
}
.doc-preview__text {
  font-size: 13px;
  line-height: 1.8;
  color: var(--el-text-color-regular);
  white-space: pre-wrap;
  word-break: break-word;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  padding: 16px;
}
.doc-preview__placeholder {
  padding: 32px;
  text-align: center;
  color: var(--el-text-color-placeholder);
  font-size: 13px;
}
.doc-preview__anchor-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  border-top: 1px solid var(--el-border-color-lighter);
  background: var(--el-color-warning-light-9);
  flex-shrink: 0;
}
.doc-preview__anchor-tip {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
</style>
