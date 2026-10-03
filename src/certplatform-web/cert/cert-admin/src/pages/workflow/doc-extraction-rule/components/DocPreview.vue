<script setup lang="ts">
/**
 * DocPreview —— 文档在线预览
 *
 * 架构要点（移植自历史项目 DocPreview，按新架构改造）：
 *  1. 本平台 JWT 走 Authorization 头，`<iframe src>` / `<img src>` 无法携带凭据
 *     → 直接渲染受保护的文件流必然 401（历史「不能预览」根因之一），
 *       必须先带 Token 取回字节，再用 ObjectURL 交给渲染器。
 *  2. Office（doc/docx/xls/xlsx/ppt/pptx）统一走后端 D-6 预览产物链：
 *     file-preview 端点返回 PDF 字节（已有产物优先，缺失时实时转换）。
 *  3. 魔数校验：JSON 错误体由 getBlob 提前拦截；此处再校验二进制魔数，
 *     避免「返回的不是文件却被当文件渲染」的静默失败。
 */
import { ref, watch, computed, onBeforeUnmount } from 'vue'
import { ElMessage } from 'element-plus'
import { Download, Refresh, WarningFilled, Loading, Document } from '@element-plus/icons-vue'
import VueOfficePdf from '@vue-office/pdf'
import { getFilePreviewBlob } from '@share/api/workflow/doc-extraction-rule'
import { downloadFile } from '@share/composables/useDirectoryApi'

const props = defineProps<{ file: any }>()

const loading = ref(false)
const error = ref('')
const errorHint = ref('')
const previewUrl = ref('')
const textContent = ref('')
const imageFallback = ref(false)
/** Office 文件首次预览需服务端转换（秒级，高峰期可能更久），超过 2.5s 时给出说明 */
const slowHint = ref(false)
let slowTimer: any = null
/** 兼容树节点与原始行两种数据形态（PascalCase / camelCase 双写，树节点为 FileCode/Name/Raw） */
const fileName = computed(
  () =>
    props.file?.fileName ||
    props.file?.FileName ||
    props.file?.Name ||
    props.file?.name ||
    props.file?.Raw?.FileName ||
    '未命名文件'
)
const fileCode = computed(
  () => props.file?.fileCode || props.file?.FileCode || props.file?.Raw?.FileCode || props.file?.raw?.FileCode || ''
)
const storagePath = computed(
  () =>
    props.file?.storagePath ||
    props.file?.StoragePath ||
    props.file?.Raw?.StoragePath ||
    props.file?.raw?.StoragePath ||
    ''
)
const convertedPath = computed(
  () =>
    props.file?.convertedStoragePath ||
    props.file?.ConvertedStoragePath ||
    props.file?.Raw?.ConvertedStoragePath ||
    props.file?.raw?.ConvertedStoragePath ||
    ''
)
const convertStatus = computed(() =>
  String(
    props.file?.convertStatus ||
      props.file?.ConvertStatus ||
      props.file?.Raw?.ConvertStatus ||
      props.file?.raw?.ConvertStatus ||
      ''
  ).toLowerCase()
)
const convertMessage = computed(
  () =>
    props.file?.convertMessage ||
    props.file?.ConvertMessage ||
    props.file?.Raw?.ConvertMessage ||
    props.file?.raw?.ConvertMessage ||
    '预览转换失败，可在左侧点击「重试失败转换」后重试'
)

/**
 * 提取链状态（★ 2026-09-26 双产物链新增）
 * - unsupported = 图片/扫描件等能力边界（不是故障）→ 引导用户手工定义字段、人工填写
 */
const markdownStatus = computed(() =>
  String(
    props.file?.markdownStatus ||
      props.file?.MarkdownStatus ||
      props.file?.Raw?.MarkdownStatus ||
      props.file?.raw?.MarkdownStatus ||
      ''
  ).toLowerCase()
)
const markdownMessage = computed(
  () =>
    props.file?.markdownMessage ||
    props.file?.MarkdownMessage ||
    props.file?.Raw?.MarkdownMessage ||
    props.file?.raw?.MarkdownMessage ||
    ''
)
/** 需人工填写（图片/扫描件等）：非故障，给正向引导而非报错 */
const needsManualFill = computed(() => markdownStatus.value === 'unsupported')

/**
 * 扩展名：**只从原始文件名推断**。
 *
 * ⚠️ 原实现优先从 ConvertedStoragePath 取扩展名（.doc→.docx 中间产物语义）。
 * 双产物链后 ConvertedStoragePath 停止写入新值，且预览链已统一为「PDF 字节」，
 * 因此扩展名必须回到原始文件名 —— 否则 .doc 会被判成 isLegacyOffice，
 * 错误文案长期停留在「旧版格式需先转 PDF」这种过时说法上。
 */
const ext = computed(() => (fileName.value.split('.').pop() || '').toLowerCase())

const isImage = computed(() => ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp'].includes(ext.value))
const isPdf = computed(() => ext.value === 'pdf')
const isText = computed(() => ['txt', 'md', 'markdown', 'json', 'xml', 'csv'].includes(ext.value))
const isOffice = computed(() => ['doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx'].includes(ext.value))
const isRenderable = computed(() => isImage.value || isText.value || isPdf.value || isOffice.value)

const fileTypeText = computed(() => {
  const map: Record<string, string> = {
    doc: 'Word 文档(.doc)',
    docx: 'Word 文档',
    xls: 'Excel 表格(.xls)',
    xlsx: 'Excel 表格',
    ppt: 'PPT 演示(.ppt)',
    pptx: 'PPT 演示',
    pdf: 'PDF 文档',
    csv: 'CSV 文件',
    txt: '文本文件',
    md: 'Markdown',
    markdown: 'Markdown',
    jpg: '图片',
    jpeg: '图片',
    png: '图片',
    gif: '图片',
    bmp: '图片',
    webp: '图片'
  }
  return map[ext.value] || (ext.value ? ext.value.toUpperCase() + ' 文件' : '未知类型')
})

/* ============ ObjectURL 生命周期管理 ============ */
function revoke() {
  if (previewUrl.value.startsWith('blob:')) {
    try {
      URL.revokeObjectURL(previewUrl.value)
    } catch {
      /* ignore */
    }
  }
  previewUrl.value = ''
}

onBeforeUnmount(revoke)

/* ============ 魔数校验（二进制内容 → 真实类型） ============ */
async function detectMagic(blob: Blob): Promise<string> {
  const buf = new Uint8Array(await blob.slice(0, 8).arrayBuffer())
  const be32 = (o: number) => ((buf[o] << 24) | (buf[o + 1] << 16) | (buf[o + 2] << 8) | buf[o + 3]) >>> 0
  if (be32(0) === 0x25504446) return 'pdf' // %PDF
  if (be32(0) === 0x504b0304) return 'zip' // PK..（OOXML / 普通 ZIP）
  if (buf[0] === 0xff && buf[1] === 0xd8) return 'jpg'
  if (be32(0) === 0x89504e47) return 'png'
  if (be32(0) === 0x47494638) return 'gif'
  if (buf[0] === 0x42 && buf[1] === 0x4d) return 'bmp'
  if (buf[0] === 0x52 && buf[1] === 0x49 && buf[2] === 0x46 && buf[3] === 0x46) return 'webp'
  return ''
}

/** 原始文件字节：优先原始路径，其次转换产物，最后退回预览链 */
async function fetchRawBlob(): Promise<Blob> {
  if (storagePath.value) return downloadFile(storagePath.value)
  if (convertedPath.value) return downloadFile(convertedPath.value)
  return getFilePreviewBlob(fileCode.value)
}

/* ============ 主流程 ============ */
async function loadPreview() {
  revoke()
  loading.value = true
  error.value = ''
  errorHint.value = ''
  textContent.value = ''
  imageFallback.value = false
  slowHint.value = false
  clearTimeout(slowTimer)
  slowTimer = setTimeout(() => {
    slowHint.value = true
  }, 2500)

  try {
    if (!fileCode.value && !storagePath.value) {
      error.value = '该文件缺少存储路径，无法预览'
      return
    }

    // 图片 / 文本：直接取原始文件字节
    if (isImage.value || isText.value) {
      const blob = await fetchRawBlob()
      if (isImage.value) {
        const magic = await detectMagic(blob)
        if (magic && !['jpg', 'png', 'gif', 'bmp', 'webp'].includes(magic)) {
          errorHint.value = `文件内容与扩展名 .${ext.value} 不一致（实际为 ${magic}）`
        }
        previewUrl.value = URL.createObjectURL(blob)
        return
      }
      textContent.value = await blob.text()
      return
    }

    // PDF 原样透传，Office 走后端转换链，二者都是 PDF 字节
    if (isPdf.value || isOffice.value) {
      const blob = await getFilePreviewBlob(fileCode.value)
      const magic = await detectMagic(blob)
      if (magic !== 'pdf') {
        error.value = '预览服务返回的不是 PDF 内容'
        errorHint.value = '可点击「下载」后用本地 Office / WPS 打开查看'
        return
      }
      previewUrl.value = URL.createObjectURL(new Blob([blob], { type: 'application/pdf' }))
      return
    }

    error.value = `暂不支持在线预览 .${ext.value || '?'} 格式`
    errorHint.value = '可点击「下载」后用本地软件打开查看'
  } catch (e: any) {
    error.value = e?.message || '预览加载失败'
    errorHint.value =
      convertStatus.value === 'failed'
        ? `预览转换失败：${convertMessage.value}`
        : '可点击「下载」后用本地 Office / WPS 打开查看'
  } finally {
    clearTimeout(slowTimer)
    loading.value = false
  }
}

function onOfficePdfError(e: any) {
  error.value = e?.message || 'PDF 渲染失败'
  errorHint.value = '可点击「下载」后用本地软件打开查看'
}

async function download() {
  if (!storagePath.value && !fileCode.value) return
  try {
    const blob = await fetchRawBlob()
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = fileName.value
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  } catch (e: any) {
    ElMessage.error('下载失败：' + (e?.message || ''))
  }
}

watch(() => fileCode.value + '|' + storagePath.value, () => loadPreview(), { immediate: true })
</script>

<template>
  <div class="doc-preview">
    <div class="preview-header">
      <div class="file-info">
        <el-icon class="file-icon"><Document /></el-icon>
        <span class="file-name" :title="fileName">{{ fileName }}</span>
        <el-tag size="small" type="info">{{ fileTypeText }}</el-tag>
      </div>
      <div class="preview-actions">
        <el-button size="small" :icon="Download" @click="download">下载</el-button>
        <el-button size="small" :icon="Refresh" :loading="loading" @click="loadPreview">刷新</el-button>
      </div>
    </div>

    <!--
      仅在「转换失败」时给出说明：
      本平台预览走 on-demand 转换（file-preview），与转换队列状态解耦，
      队列显示「待转换/转换中」并不影响在线预览，提示反而误导用户。
    -->
    <div v-if="convertStatus === 'failed' && !error" class="convert-bar">
      <span class="convert-text">{{ convertMessage }}</span>
    </div>

    <!--
      提取链能力边界（★ 2026-09-26）：图片/扫描件无法自动提取内容。
      这不是故障 —— 按产品设计，用户手工定义字段与表格、由人工填写即可。
      因此用「引导」而非「报错」的语气，且不阻塞预览。
    -->
    <div v-if="needsManualFill" class="convert-bar manual-bar">
      <el-icon><WarningFilled /></el-icon>
      <span class="convert-text">
        {{ markdownMessage || '该文件为图片/扫描件，暂不支持自动提取内容。可手工定义字段与表格，由人工填写' }}
      </span>
    </div>

    <div class="preview-content">
      <div v-if="loading" class="state-panel">
        <el-icon class="is-loading" :size="32"><Loading /></el-icon>
        <span>{{ slowHint ? '文档正在转换为 PDF，请稍候…' : '加载中...' }}</span>
      </div>

      <div v-else-if="error" class="state-panel">
        <el-icon :size="52" color="var(--yzh-color-warning, #e6a23c)"><WarningFilled /></el-icon>
        <p class="state-title">文档预览失败</p>
        <p class="state-desc">{{ error }}</p>
        <p v-if="errorHint" class="state-tip">{{ errorHint }}</p>
        <el-button type="primary" @click="download">下载查看</el-button>
      </div>

      <template v-else-if="isImage">
        <el-image
          v-if="!imageFallback"
          :src="previewUrl"
          :preview-src-list="[previewUrl]"
          fit="contain"
          class="image-preview"
          @error="imageFallback = true"
        />
        <img v-else :src="previewUrl" class="image-fallback-img" alt="图片预览" />
      </template>

      <template v-else-if="isPdf || isOffice">
        <!-- vue-office-pdf：canvas 渲染，无浏览器内置查看器的侧边栏/工具栏 -->
        <VueOfficePdf
          v-if="previewUrl"
          :key="previewUrl"
          :src="previewUrl"
          class="pdf-viewer"
          @error="onOfficePdfError"
        />
        <div v-else class="state-panel">
          <el-icon :size="48"><Document /></el-icon>
          <p class="state-desc">暂无预览内容</p>
          <el-button type="primary" @click="download">下载查看</el-button>
        </div>
      </template>

      <pre v-else-if="textContent" class="text-content">{{ textContent }}</pre>

      <div v-else-if="isRenderable" class="state-panel">
        <el-empty description="暂无预览内容" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.doc-preview {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  background: var(--yzh-color-bg-container, #fff);
}
.preview-header {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 16px;
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
}
.file-info {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}
.file-icon {
  color: var(--yzh-color-primary, #409eff);
  font-size: 18px;
}
.file-name {
  font-weight: 500;
  font-size: 14px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.preview-actions {
  display: flex;
  gap: 8px;
  flex-shrink: 0;
}
.convert-bar {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 16px;
  background: var(--yzh-color-warning-light-9, #fdf6ec);
  border-bottom: 1px solid #f5dab1;
  font-size: 12px;
  color: #b88230;
}
/* 能力边界提示（图片/扫描件需人工填写）：用中性信息色，与「失败」的橙黄区分开 */
.manual-bar {
  background: var(--el-color-primary-light-9, #ecf5ff);
  border-bottom-color: #b3d8ff;
  color: #337ecc;
}
.preview-content {
  flex: 1;
  min-height: 0;
  overflow: auto;
  display: flex;
  flex-direction: column;
  background: var(--yzh-color-bg-page, #f5f7fa);
}
.state-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 10px;
  color: var(--yzh-color-text-tertiary, #909399);
  padding: 24px;
  text-align: center;
}
.state-title {
  margin: 0;
  font-size: 15px;
  font-weight: 500;
  color: var(--yzh-color-text-regular, #606266);
}
.state-desc {
  margin: 0;
  font-size: 13px;
}
.state-tip {
  margin: 0;
  font-size: 12px;
  color: var(--yzh-color-text-disabled, #c0c4cc);
}
.image-preview,
.image-fallback-img {
  max-width: 100%;
  max-height: 100%;
  object-fit: contain;
  margin: auto;
}
.pdf-viewer {
  width: 100%;
  height: 100%;
  flex: 1;
  min-height: 0;
}
.text-content {
  flex: 1;
  overflow: auto;
  padding: 16px;
  margin: 0;
  font-family: 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.6;
  background: var(--yzh-color-bg-container, #fff);
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}
</style>
