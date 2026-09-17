<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { Download, Refresh, WarningFilled, Loading } from '@element-plus/icons-vue'
import { getFilePreviewUrl, getFileMarkdown } from '@share/api/workflow/doc-extraction-rule'

const props = defineProps<{ file: any }>()

const loading = ref(false)
const error = ref('')
const previewUrl = ref('')
const markdownContent = ref('')
const imageFallback = ref(false)

const ext = computed(() => {
  const name = props.file?.name || props.file?.fileName || ''
  const match = name.match(/\.(\w+)$/)
  return match?.[1]?.toLowerCase() || ''
})

const isImage = computed(() => ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp'].includes(ext.value))
const isPdf = computed(() => ext.value === 'pdf')
const isMarkdown = computed(() => ['md', 'markdown'].includes(ext.value))
const isText = computed(() => ['txt', 'json', 'xml', 'csv'].includes(ext.value))
const isOffice = computed(() => ['doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx'].includes(ext.value))

watch(() => props.file, () => loadPreview(), { immediate: true })

async function loadPreview() {
  if (!props.file?.code) return
  loading.value = true
  error.value = ''
  previewUrl.value = ''
  markdownContent.value = ''
  imageFallback.value = false

  try {
    if (isImage.value) {
      previewUrl.value = `/api/standard-directory/download?path=${props.file.storagePath || props.file.code}`
    } else if (isPdf.value) {
      previewUrl.value = getFilePreviewUrl(props.file.code)
    } else if (isMarkdown.value) {
      const res = await getFileMarkdown(props.file.code)
      markdownContent.value = res?.data || ''
    } else if (isOffice.value) {
      previewUrl.value = getFilePreviewUrl(props.file.code)
    } else if (isText.value) {
      const res = await getFileMarkdown(props.file.code)
      markdownContent.value = res?.data || ''
    } else {
      error.value = '不支持预览此文件类型'
    }
  } catch (e: any) {
    error.value = e?.message || '预览加载失败'
  } finally {
    loading.value = false
  }
}

function download() {
  if (!previewUrl.value) return
  window.open(previewUrl.value, '_blank')
}
</script>

<template>
  <div class="doc-preview">
    <div class="preview-toolbar">
      <span class="file-name">{{ file?.name || file?.fileName }}</span>
      <el-tag size="small" type="info">{{ ext || '未知' }}</el-tag>
      <div class="toolbar-actions">
        <el-button link :icon="Refresh" @click="loadPreview" :loading="loading" />
        <el-button link :icon="Download" @click="download">下载</el-button>
      </div>
    </div>

    <div v-if="loading" class="preview-loading">
      <el-icon class="is-loading" :size="32"><Loading /></el-icon>
      <span>加载中...</span>
    </div>

    <div v-else-if="error" class="preview-error">
      <el-icon :size="48" color="#e6a23c"><WarningFilled /></el-icon>
      <p>{{ error }}</p>
      <el-button v-if="previewUrl" type="primary" size="small" @click="download">下载查看</el-button>
    </div>

    <div v-else-if="isImage" class="preview-content">
      <img
        v-if="!imageFallback"
        :src="previewUrl"
        @error="imageFallback = true"
        class="image-preview"
      />
      <img v-else :src="previewUrl" class="image-preview" />
    </div>

    <div v-else-if="isPdf || isOffice" class="preview-content">
      <iframe :src="previewUrl" class="pdf-frame" />
    </div>

    <div v-else-if="markdownContent" class="preview-content">
      <pre class="text-content">{{ markdownContent }}</pre>
    </div>

    <div v-else class="preview-empty">
      <el-empty description="暂无预览内容" />
    </div>
  </div>
</template>

<style scoped>
.doc-preview {
  height: 100%;
  display: flex;
  flex-direction: column;
}
.preview-toolbar {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 0;
  border-bottom: 1px solid #ebeef5;
}
.file-name {
  font-weight: 500;
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.toolbar-actions {
  display: flex;
  gap: 4px;
}
.preview-loading,
.preview-error,
.preview-empty {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  color: #909399;
}
.preview-content {
  flex: 1;
  overflow: hidden;
}
.image-preview {
  max-width: 100%;
  max-height: 100%;
  object-fit: contain;
}
.pdf-frame {
  width: 100%;
  height: 100%;
  border: none;
}
.text-content {
  height: 100%;
  overflow: auto;
  padding: 12px;
  margin: 0;
  font-family: 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.6;
  background: #f5f7fa;
  border-radius: 4px;
}
</style>
