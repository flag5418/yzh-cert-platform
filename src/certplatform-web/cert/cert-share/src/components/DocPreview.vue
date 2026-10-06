<script setup lang="ts">
/**
 * DocPreview —— 文档在线预览
 *
 * 架构要点（移植自历史项目 DocPreview，按新架构改造）：
 *  1. 本平台 JWT 走 Authorization 头，`<iframe src>` / `<img src>` 无法携带凭据
 *     → 直接渲染受保护的文件流必然 401（历史「不能预览」根因之一），
 *       必须先带 Token 取回字节，再用 ObjectURL 交给渲染器。
 *  2. Office（doc/docx/xls/xlsx/ppt/pptx）统一走后端预览产物链，端点返回 PDF 字节
 *     （已有产物优先，缺失时实时转换）。**两条链**：
 *     - 有 `fileCode` → `file-preview`（按标准目录行查，产物路径回写 DB 列）；
 *     - 只有 `storagePath` → `preview-by-path`（裸路径，用于 `_template/` 下的空白模板
 *       —— 它在标准目录表里没有行，第一条链查不到）。
 *  3. 魔数校验：JSON 错误体由 getBlob 提前拦截；此处再校验二进制魔数，
 *     避免「返回的不是文件却被当文件渲染」的静默失败。
 */
import { YzhEmptyState, YzhStatusBadge } from '@yzh-core'
import { ref, watch, computed, onBeforeUnmount } from 'vue'
import { ElMessage } from 'element-plus'
import { Download, Refresh, WarningFilled, Loading, Document } from '@element-plus/icons-vue'
import VueOfficePdf from '@vue-office/pdf'
import { getFilePreviewBlob, getPreviewBlobByPath } from '@share/api/workflow/doc-extraction-rule'
import { downloadRawBlob } from '@share/composables/useDirectoryApi'

const props = defineProps<{
  file: any
  /**
   * 「下载」按钮的文案。
   *
   * ⛔ 默认值刻意是「下载原始件」而不是「下载」：本组件取的是**原始字节**
   *   （`fetchRawBlob` 优先 `storagePath`），而填写规则页操作条的「下载可编辑版」
   *   取的是归一产物 —— 两个按钮下的是**不同文件**，同名会让用户以为下重了。
   *   预览空白模板时由调用方传「下载模板」，语义才准确。
   */
  downloadLabel?: string
}>()

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
 *
 * ★★ 提取规则：**取最后一个「像扩展名」的 token**，⛔ 不是「最后一个点之后的所有字符」。
 *
 * 【为什么（2026-10-04 实测缺陷）】
 *   上游可能把**显示装饰**拼进文件名（本页左树就把模板状态徽标拼进了节点 `Name`），
 *   于是传来 `附录一 质量管理体系过程识别图.doc  ⬜未上传模板`。
 *   用 `split('.').pop()` ⇒ `doc  ⬜未上传模板` ⇒ 不在白名单
 *   ⇒ 报「暂不支持在线预览 .doc ⬜未上传模板 格式」，**而标题栏看起来完全正常**
 *   （契约接口随后把标题修干净了）—— 典型的「标题对、内容错」，最难查的一类。
 *
 *   正则 `\.([A-Za-z0-9]{1,8})(?![A-Za-z0-9])` 取**最后一个**匹配：
 *   - `x.doc`                → `doc`   ✅
 *   - `x.doc.docx`           → `docx`  ✅（双重扩展名的归一产物）
 *   - `x.doc  ⬜未上传模板`   → `doc`   ✅（中文/空白不是 `[A-Za-z0-9]`，不会吞进去）
 *   - `XASL-QR-014 计划.doc` → `doc`   ✅（连字符不在字符类里，不会被误判）
 *
 * ⚠️ 这是**防御性**修正，不是主要修复：主修复是上游别把徽标拼进文件名
 *   （`logic.fileName` 读 `Extra.rawName`）。两层都要有 —— 少任何一层，
 *   下一个往文件名里塞装饰的调用方又会把这里打回原形。
 */
const ext = computed(() => {
  const name = fileName.value
  const matches = name.match(/\.([A-Za-z0-9]{1,8})(?![A-Za-z0-9])/g)
  if (!matches || matches.length === 0) return ''
  return matches[matches.length - 1].slice(1).toLowerCase()
})

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

/**
 * 「下载」按钮文案（模板可覆盖）。
 * ⛔ 不要在模板里直接写死 —— 错误提示里也要引用同一份文案，两处不一致会让用户找不到按钮。
 */
const downloadText = computed(() => props.downloadLabel || '下载原始件')

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

/**
 * 原始文件字节：优先原始路径，其次转换产物，最后退回预览链。
 *
 * ⚠️ 这里的「原始」是相对**归一产物**而言的 —— 它取的是 `storagePath` 指向的那份字节。
 *   标准目录文件 → `.doc`/`.xls` 原始件；空白模板 → `_template/` 下的 `.docx`/`.xlsx`。
 *   两种情况都**不该**去要归一产物：那是「下载可编辑版」按钮的职责。
 */
async function fetchRawBlob(): Promise<Blob> {
  // ⚠️ 这里要的是**字节**（拿去渲染 / 另存），不是「触发浏览器下载」——
  //   `downloadFile` 已改为 Promise<void>（内部直接 downloadGet），
  //   预览渲染必须走返回 Blob 的二进制端点。
  if (storagePath.value) return downloadRawBlob(storagePath.value)
  if (convertedPath.value) return downloadRawBlob(convertedPath.value)
  return getFilePreviewBlob(fileCode.value)
}

/**
 * ★ PDF 字节：**两条链，按有没有 `fileCode` 分流**（2026-10-04）。
 *
 * | 场景 | 判据 | 端点 | 为什么 |
 * |---|---|---|---|
 * | 标准资料清单文件 | 有 `fileCode` | `file-preview` | 产物路径要回写 `PreviewPdfPath` 列 |
 * | 空白模板 / 企业文档 | 只有 `storagePath` | `preview-by-path` | 它在标准目录表里**没有行**，按 code 查不到 |
 *
 * ⛔ 不要合并成一条：`file-preview` 内部是 `GetFileInfoAsync(fileCode)`，
 *   模板传空 code 会得到「未找到文件」，而用户看到的是「预览失败」——
 *   症状会指向存储，实际是路由选错了。
 */
async function fetchPreviewBlob(): Promise<Blob> {
  if (fileCode.value) return getFilePreviewBlob(fileCode.value)
  return getPreviewBlobByPath(storagePath.value, fileName.value)
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
      const blob = await fetchPreviewBlob()
      const magic = await detectMagic(blob)
      if (magic !== 'pdf') {
        // ⚠️ 必须区分「0 字节」与「内容不对」——两者根因完全不同：
        //   0 字节 = **对象存储读不到内容**（源文件或 PDF 缓存是空的），
        //           重新扫描 / 重转 PDF 都无济于事，得先查存储层；
        //   有内容但不是 PDF = 转换链返回了别的东西（多半是 JSON 错误体被当文件）。
        if (!blob || blob.size === 0) {
          error.value = '预览产物为空（0 字节）'
          errorHint.value =
            '服务端未取到文件内容。通常是对象存储读取异常或该文件的 PDF 产物是空文件，' +
            '请重试上传 / 重新生成 PDF；仍失败请联系管理员核查存储层。'
        } else {
          error.value = '预览服务返回的不是 PDF 内容'
          errorHint.value = `可点击「${props.downloadLabel || '下载原始件'}」后用本地 Office / WPS 打开查看`
        }
        return
      }
      previewUrl.value = URL.createObjectURL(new Blob([blob], { type: 'application/pdf' }))
      return
    }

    error.value = `暂不支持在线预览 .${ext.value || '?'} 格式`
    errorHint.value = `可点击「${downloadText.value}」后用本地软件打开查看`
  } catch (e: any) {
    error.value = e?.message || '预览加载失败'
    errorHint.value =
      convertStatus.value === 'failed'
        ? `预览转换失败：${convertMessage.value}`
        : `可点击「${downloadText.value}」后用本地 Office / WPS 打开查看`
  } finally {
    clearTimeout(slowTimer)
    loading.value = false
  }
}

function onOfficePdfError(e: any) {
  error.value = e?.message || 'PDF 渲染失败'
  errorHint.value = `可点击「${downloadText.value}」后用本地软件打开查看`
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

/**
 * 重载触发键。
 *
 * ⛔ **必须带上 `fileName`**：`ext` / `isOffice` / `fileTypeText` 全部由文件名推出，
 *   而文件名可能**后到**（本页的契约接口是异步的，先渲染的是树节点名）。
 *   只 watch 路径的话，名字改对了也**不会重载** —— 用户看到的是
 *   「标题已经变正常、内容还停在旧错误」的错位状态。
 */
watch(() => [fileCode.value, storagePath.value, fileName.value].join('|'), () => loadPreview(), {
  immediate: true,
})

/**
 * ★ 显式重载出口（2026-10-04）。
 *
 * 【为什么 watch 不够】
 *   换版（重新上传**同名**空白模板）后，`storagePath` 与 `fileCode` **完全没变**
 *   ⇒ 上面的 watch 不触发 ⇒ 用户会一直看着**上一版**的 PDF。
 *   字节变了但路径没变，只能由调用方**显式**通知（上传成功后调 `reload()`）。
 *
 * 调用方：`doc-fill-rule/components/PreviewPane.vue`。
 */
defineExpose({ reload: loadPreview })
</script>

<template>
  <div class="doc-preview">
    <div class="preview-header">
      <div class="file-info">
        <el-icon class="file-icon"><Document /></el-icon>
        <span class="file-name" :title="fileName">{{ fileName }}</span>
        <YzhStatusBadge type="info" size="small" :text="fileTypeText" />
      </div>
      <div class="preview-actions">
        <!--
          ★ `#actions` 插槽（2026-10-05 新增，C9）—— 让调用方把**本页专有**的动作
            放在「下载」左边，与它**同一行**。

          【为什么用插槽而不是加 prop】
            「上传空白模板」是 `doc-fill-rule` 页独有的动作（依赖标准资料清单行 +
            模板登记链），塞进本组件会把领域概念带进共享渲染器。
            插槽让本组件保持「哑渲染器」，同时满足「上传 / 下载同一行」的排布要求。
          【向后兼容】插槽可选，未提供时渲染结果与改动前**逐字一致**。
        -->
        <slot name="actions" />
        <el-button type="default" size="small" :icon="Download" @click="download">
          {{ downloadText }}
        </el-button>
        <el-button type="default" size="small" :icon="Refresh" :loading="loading" @click="loadPreview">
          刷新
        </el-button>
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
        <YzhEmptyState :icon="Document" title="暂无预览内容" />
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
  padding: var(--yzh-space-2, 8px) var(--yzh-space-4, 16px);
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
  font-size: var(--yzh-font-size-xl, 18px);
}
.file-name {
  font-weight: var(--yzh-font-weight-medium, 500);
  font-size: var(--yzh-font-size-md, 14px);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.preview-actions {
  display: flex;
  gap: var(--yzh-space-2, 8px);
  flex-shrink: 0;
}
.convert-bar {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-1, 4px) var(--yzh-space-4, 16px);
  background: var(--yzh-color-bg-subtle, #f9fafb);
  border-bottom: 1px solid var(--yzh-color-warning, #d97706);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-warning, #d97706);
}
/* 能力边界提示（图片/扫描件需人工填写）：用中性信息色，与「失败」的橙黄区分开 */
.manual-bar {
  background: var(--yzh-color-bg-active, #eff6ff);
  border-bottom-color: var(--yzh-color-primary-lighter, #3b82f6);
  color: var(--yzh-color-primary, #1e3a8a);
}
.preview-content {
  flex: 1;
  min-height: 0;
  overflow: auto;
  display: flex;
  flex-direction: column;
  background: var(--yzh-color-bg-page, #f8fafc);
}
.state-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: var(--yzh-space-3, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
  padding: var(--yzh-space-6, 24px);
  text-align: center;
}
.state-title {
  margin: 0;
  font-size: var(--yzh-font-size-lg, 16px);
  font-weight: var(--yzh-font-weight-medium, 500);
  color: var(--yzh-color-text-regular, #606266);
}
.state-desc {
  margin: 0;
  font-size: var(--yzh-font-size-sm, 13px);
}
.state-tip {
  margin: 0;
  font-size: var(--yzh-font-size-xs, 12px);
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
/*
 * ★ `@vue-office/pdf` 在**运行时给画布容器写内联 `background: gray`**，
 *   在浅色页面里表现为「预览区中间一条深灰带」，与整页底色割裂。
 *   改为跟随页面底色 —— 只动**画布背板**，渲染出来的 PDF 页面白底不动。
 *
 *   ⚠️ 必须 `:deep()` + `!important`（S11 允许 `!important` 仅出现在 `:deep()` 内）：
 *      内联样式的优先级高于任何选择器，不加 `!important` 一定压不住。
 *      这是 S11 设立该例外的**原意场景** —— 覆盖第三方组件的运行时内联样式。
 */
.pdf-viewer :deep(.vue-office-pdf-wrapper) {
  background: var(--yzh-color-bg-page, #f8fafc) !important;
}
.text-content {
  flex: 1;
  overflow: auto;
  padding: var(--yzh-space-4, 16px);
  margin: 0;
  font-family: var(--yzh-font-family-mono, 'Courier New', monospace);
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: var(--yzh-line-height-base, 1.6);
  background: var(--yzh-color-bg-container, #fff);
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}
</style>
