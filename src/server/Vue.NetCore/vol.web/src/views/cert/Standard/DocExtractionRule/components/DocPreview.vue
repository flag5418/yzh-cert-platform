<template>
  <div class="doc-preview">
    <div class="preview-header">
      <div class="file-info">
        <el-icon class="file-icon"><Document /></el-icon>
        <span class="file-name">{{ file.name }}</span>
        <el-tag size="small" type="info">{{ fileTypeText }}</el-tag>
      </div>
      <el-button size="small" @click="refresh">
        <el-icon><Refresh /></el-icon>刷新
      </el-button>
    </div>

    <div class="preview-content">
      <!-- 前置校验失败：统一降级页 -->
      <template v-if="previewError">
        <div class="unsupported">
          <el-icon :size="56" color="#f56c6c"><Warning /></el-icon>
          <p style="font-weight: 600; color: #f56c6c">文档预览失败</p>
          <p>{{ previewError }}</p>
          <p class="tip">提示：可下载后用 WPS / Microsoft Office 打开查看</p>
          <el-button type="primary" @click="download">下载查看</el-button>
        </div>
      </template>

      <!-- 图片预览 -->
      <template v-else-if="isImage">
        <el-image
          :src="previewUrl"
          :preview-src-list="[previewUrl]"
          fit="contain"
          class="image-preview"
        />
      </template>

      <!-- Word 文档：.docx（OOXML，已通过 ZIP 结构双重校验） -->
      <template v-else-if="isDocx && previewBuffer">
        <vue-office-docx
          :src="previewBuffer"
          style="height: 100%; min-height: 0"
          @rendered="onRendered"
          @error="onError"
        />
      </template>

      <!-- PPT 文档：.pptx（OOXML，zip+ppt/ structure，已通过结构校验） -->
      <template v-else-if="isPptx && previewBuffer">
        <vue-office-docx
          :src="previewBuffer"
          style="height: 100%; min-height: 0"
          @rendered="onRendered"
          @error="onError"
        />
      </template>

      <!-- 旧版二进制 Office 不支持：.doc / .ppt（OLE2） -->
      <template v-else-if="isOldFormat">
        <div class="unsupported">
          <el-icon :size="48"><Document /></el-icon>
          <p>
            {{
              isOldDoc
                ? '旧版 Word (.doc) 格式暂不支持在线预览'
                : '旧版 PPT (.ppt) 格式暂不支持在线预览'
            }}
          </p>
          <p class="tip">
            提示：可下载后用 WPS 或 Microsoft Office 打开；或将文件另存为 .docx / .pptx 后上传
          </p>
          <el-button type="primary" @click="download">下载查看</el-button>
        </div>
      </template>

      <!-- Excel：.xlsx + .xls（官方均支持，xlsx 做 ZIP 校验；xls 走 exceljs BIFF 兼容解析） -->
      <template v-else-if="isExcel && previewBuffer">
        <vue-office-excel
          :src="previewBuffer"
          style="height: 100%; min-height: 0"
          @rendered="onRendered"
          @error="onError"
        />
      </template>

      <!-- PDF 文档 -->
      <template v-else-if="isPdf && previewUrl">
        <vue-office-pdf
          :src="previewUrl"
          style="height: 100%; min-height: 0"
          @rendered="onRendered"
          @error="onError"
        />
      </template>

      <!-- 文本文件 -->
      <template v-else-if="isText">
        <pre class="text-content">{{ textContent }}</pre>
      </template>

      <!-- 不支持的格式 -->
      <template v-else>
        <div class="unsupported">
          <el-icon :size="48"><Document /></el-icon>
          <p>该文件格式暂不支持预览</p>
          <el-button type="primary" @click="download">下载查看</el-button>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup>
import http from '@/api/http'
import { Document, Refresh, Warning } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, onBeforeUnmount, ref, watch } from 'vue'

/* ============ 0. 锚点日志（最高优先级 console.log，级别过滤不会拦截） ============ */
console.log(
  '[DocPreview] 🔵 script setup 已初始化 ✅（这条没出现 = 组件根本没挂载 / import 抛异常）'
)

// vue-office 组件（严格按官方 2025-05 README / NPM 描述）
//   支持：docx / xls+xlsx / pptx / pdf
//   不支持：doc OLE2 / ppt OLE2
import VueOfficeDocx from '@vue-office/docx'
import '@vue-office/docx/lib/index.css'
import VueOfficeExcel from '@vue-office/excel'
import '@vue-office/excel/lib/index.css'
import VueOfficePdf from '@vue-office/pdf'

const props = defineProps({
  file: { type: Object, required: true }
})

const previewUrl = ref('') /* 图片/PDF 的 blob URL / 原始路径兜底 */
const previewBuffer = ref(null) /* 传给 vue-office-docx/excel 的 ArrayBuffer（通过校验才赋值） */
const previewError = ref(null) /* 降级页提示 */
const textContent = ref('') /* 文本预览 */

/* ============ 1. 文件类型分类（速查.md 官方支持矩阵） ============ */
const ext = computed(() => {
  // 优先从转换后的路径提取扩展名（.doc→.docx 转换后实际是 docx）
  const convertedPath = props.file?.convertedStoragePath || props.file?.ConvertedStoragePath || ''
  if (convertedPath) {
    const convertedExt = convertedPath.split('.').pop().toLowerCase()
    if (convertedExt && convertedExt !== 'converted') return convertedExt
  }
  // 回退到原始文件名扩展名
  return (props.file?.name || '').split('.').pop().toLowerCase()
})

const isImage = computed(() => ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp'].includes(ext.value))
const isText = computed(() => ['txt', 'md', 'json', 'xml', 'csv'].includes(ext.value))
const isPdf = computed(() => ext.value === 'pdf')

const isDocx = computed(() => ext.value === 'docx') /* ✅ 官方支持 */
const isPptx = computed(() => ext.value === 'pptx') /* ✅ 官方支持 */
const isExcel = computed(() => ['xlsx', 'xls'].includes(ext.value)) /* ✅ 官方二者都支持 */
const isOldDoc = computed(() => ext.value === 'doc') /* ❌ OLE2 不支持 */
const isOldPpt = computed(() => ext.value === 'ppt') /* ❌ OLE2 不支持 */
const isOldFormat = computed(() => isOldDoc.value || isOldPpt.value)

const fileTypeText = computed(() => {
  const map = {
    doc: 'Word 文档(旧版 .doc · 不支持预览)',
    docx: 'Word 文档',
    ppt: 'PPT 演示(旧版 .ppt · 不支持预览)',
    pptx: 'PPT 演示',
    xlsx: 'Excel 表格',
    xls: 'Excel 表格(兼容版)',
    csv: 'CSV 文件',
    pdf: 'PDF 文档',
    txt: '文本文件',
    md: 'Markdown',
    jpg: '图片',
    jpeg: '图片',
    png: '图片',
    gif: '图片',
    bmp: '图片',
    webp: '图片'
  }
  return map[ext.value] || ext.value.toUpperCase() + ' 文件'
})

/* ============ 2. 后端下载 URL 构建 ============
 * StandardDirectoryController.cs L238-L257
 *   [Route("api/standard-directory")] + [HttpGet("download")]
 *   [JWTAuthorize] + public async Task<IActionResult> DownloadFile([FromQuery] string path)
 * 
 * 优先使用 ConvertedStoragePath（转换后的 .docx/.xlsx），其次使用原始 StoragePath
 */
const buildFileUrl = () => {
  if (!props.file) return ''
  // 优先使用转换后的路径（如果转换成功）
  const convertedPath = props.file.convertedStoragePath || props.file.ConvertedStoragePath || ''
  if (convertedPath) return `/api/standard-directory/download?path=${encodeURIComponent(convertedPath)}`
  // 回退到原始路径
  const path = props.file.storagePath || props.file.StoragePath || ''
  if (path) return `/api/standard-directory/download?path=${encodeURIComponent(path)}`
  const fileCode = props.file.fileCode || props.file.FileCode || ''
  if (fileCode) return `/api/standard-directory/download?path=${encodeURIComponent(fileCode)}`
  return ''
}

/* ============ 3. 辅助：blob URL 回收 ============ */
const _revoke = () => {
  if (previewUrl.value?.startsWith('blob:')) {
    try {
      URL.revokeObjectURL(previewUrl.value)
    } catch (_) {}
  }
}

/* ============ 4. Office 文件结构校验（ZIP + Central Directory） ============ */
function _isZipBuffer(buf) {
  if (!buf || buf.byteLength < 4) return false
  return new DataView(buf).getUint32(0, false) === 0x504b0304 /* "PK.." */
}

/** 扫 ZIP Central Directory，判断 OOXML 子类型：docx / xlsx / pptx / zip / null */
function _detectOfficeKindFromZip(buf) {
  if (!_isZipBuffer(buf)) return null
  const u8 = new Uint8Array(buf)
  const len = u8.length
  let eocdStart = -1
  for (let i = len - 22; i >= 0; i--) {
    if (u8[i] === 0x50 && u8[i + 1] === 0x4b && u8[i + 2] === 0x05 && u8[i + 3] === 0x06) {
      eocdStart = i
      break
    }
  }
  if (eocdStart === -1 || eocdStart + 22 > len) return null
  const dv = new DataView(buf)
  const cdSize = dv.getUint32(eocdStart + 12, true)
  const cdOffset = dv.getUint32(eocdStart + 16, true)
  if (!cdSize || cdOffset + cdSize > len) return null

  const names = new Set()
  let p = cdOffset
  while (p + 46 <= cdOffset + cdSize) {
    if (u8[p] !== 0x50 || u8[p + 1] !== 0x4b || u8[p + 2] !== 0x01 || u8[p + 3] !== 0x02) break
    const nameLen = dv.getUint16(p + 28, true)
    const extraLen = dv.getUint16(p + 30, true)
    const commentLen = dv.getUint16(p + 32, true)
    const nameStart = p + 46
    if (nameStart + nameLen > len) break
    const name = new TextDecoder('utf-8').decode(u8.subarray(nameStart, nameStart + nameLen))
    if (name) names.add(name.toLowerCase())
    p = nameStart + nameLen + extraLen + commentLen
    if (p > cdOffset + cdSize) break
  }
  if (!names.size) return null
  if (!names.has('[content_types].xml')) return 'zip' /* 普通 ZIP，无 [Content_Types].xml 非 OOXML */
  /* 注意：_rels/.rels 是 docx / xlsx / pptx 三者共有的通用根关系文件，不能作为子类型判断依据
   *      子类型必须用各自目录下的专有文件：word/ → docx；xl/ → xlsx；ppt/ → pptx
   */
  if (names.has('ppt/presentation.xml') || names.has('ppt/slides/_rels/slide1.xml.rels')) return 'pptx'
  if (names.has('xl/workbook.xml')        || names.has('xl/_rels/workbook.xml.rels'))         return 'xlsx'
  if (names.has('word/document.xml')      || names.has('word/_rels/document.xml.rels'))       return 'docx'
  return 'zip'
}

/* ============ 5. 带 JWT 鉴权的下载（复用 http.js Authorization / lang / baseURL）
 * ⚠️ 重要：http.js get/post 内部 resolve 的是 response.data（整个 axios.response 只在拦截器内可见）
 *    → const buf  = await _httpGetArrayBuffer(url)     // buf 本身就是 ArrayBuffer
 *    → const txt  = await _httpGetText(url)            // txt 本身就是字符串
 *    → const blob = await _httpGetBlob(url)            // blob 本身就是 Blob
 * 另外 headers 没法从 http.js 拿到，改用「魔数 + 试读头 64 bytes」替代 Content-Type 做登录重定向识别
 * ============================================================ */
const _httpGetArrayBuffer = (url) => http.get(url, null, false, { responseType: 'arraybuffer' })
const _httpGetText = (url) => http.get(url, null, false, { responseType: 'text' })
const _httpGetBlob = (url) => http.get(url, null, false, { responseType: 'blob' })

/**
 * 从 ArrayBuffer 开头判断是不是登录重定向吐的 HTML / JSON（http.js 不返回 headers，所以退而求其次读 payload）
 *   HTML   : 64 bytes 内出现 "<!doctype" / "<html" / "<head"（大小写不敏感）
 *   JSON   : 首字节 0x7b = '{' 且 64 bytes 内有 "login" / "\"code\":" / "unauthorized"
 *   合法二进制 : PDF 魔数 %PDF- / ZIP 魔数 PK.. / PNG 魔数 / JPG 魔数 / GIF 魔数 / BMP 魔数
 */
function _looksLikeAuthRedirect(buf) {
  if (!buf || buf.byteLength < 4) return { redirect: false }
  const len = Math.min(buf.byteLength, 256)
  const u8 = new Uint8Array(buf, 0, len)

  /* --- 1. 常见合法二进制魔数 → 直接不是重定向 --- */
  const magic32 = (u8[0] << 24) | (u8[1] << 16) | (u8[2] << 8) | u8[3]
  const magic16 = (u8[0] << 8) | u8[1]
  if (magic32 === 0x504b0304)
    return { redirect: false, kind: 'zip-or-ooxml' } /* docx / xlsx / pptx */
  if (magic32 === 0x25504446) return { redirect: false, kind: 'pdf' } /* "%PDF" */
  if (magic16 === 0xffd8) return { redirect: false, kind: 'jpg' }
  if (magic32 === 0x47494638) return { redirect: false, kind: 'gif' } /* "GIF8" */
  if (magic32 === 0x89504e47) return { redirect: false, kind: 'png' }
  if (magic16 === 0x424d) return { redirect: false, kind: 'bmp' }
  if (u8[0] === 0x52 && u8[1] === 0x49 && u8[2] === 0x46 && u8[3] === 0x46)
    return { redirect: false, kind: 'webp' } /* "RIFF" */

  /* --- 2. 尝试以 utf-8 解码读前 256 bytes --- */
  let head = ''
  try {
    head = new TextDecoder('utf-8', { fatal: false }).decode(u8).toLowerCase()
  } catch (_) {
    head = ''
  }

  if (
    head &&
    (head.includes('<!doctype ') ||
      head.includes('<html') ||
      head.includes('<head') ||
      head.includes('登录') ||
      head.includes('<body'))
  )
    return { redirect: true, kind: 'html' }
  if (
    u8[0] === 0x7b /* '{' */ &&
    head &&
    (head.includes('"code"') ||
      head.includes('login') ||
      head.includes('unauthorized') ||
      head.includes('登录') ||
      head.includes('token'))
  )
    return { redirect: true, kind: 'json' }
  return { redirect: false }
}

/* ============ 6. 主流程：加载 & 四层校验（全链路 console 可追踪） ============ */
const loadPreview = async () => {
  _revoke()
  previewBuffer.value = null
  previewError.value = null
  textContent.value = ''
  if (!props.file) {
    console.log('[DocPreview] 无 props.file，返回')
    return
  }

  const url = buildFileUrl()
  previewUrl.value = url
  if (!url) {
    previewError.value = '未获取到文件存储路径'
    return
  }
  const fileName = props.file?.name || '(unknown)'
  console.log(`[DocPreview] 开始预览 ${fileName}  URL=${url}`)

  /* ---- 6.1 文本：直接读 text ---- */
  if (isText.value) {
    try {
      textContent.value = (await _httpGetText(url)) ?? ''
      console.log(`[DocPreview] 文本预览加载完成, 字节=${textContent.value.length}`)
    } catch (e) {
      const msg = typeof e === 'string' ? e : e?.message || e || '加载失败'
      textContent.value = '加载失败: ' + msg
      console.warn('[DocPreview] 文本加载异常:', e)
    }
    return
  }

  /* ---- 6.2 其余全走 arraybuffer ---- */
  let buf = null
  try {
    buf = await _httpGetArrayBuffer(url)
    console.log(`[DocPreview] 二进制下载完成, byteLength=${buf?.byteLength}`)
  } catch (e) {
    const msg = typeof e === 'string' ? e : e?.message || e || '未知错误'
    const is401 = /401|unauthorized|登录|token/i.test(msg)
    previewError.value = is401 ? '登录已失效，请重新登录后再预览' : '下载接口请求失败: ' + msg
    console.warn('[DocPreview] 二进制下载异常:', { e, msg, is401 })
    return
  }
  if (!buf || buf.byteLength === 0) {
    previewError.value = '服务器返回空文件（请联系管理员检查 MinIO / StoragePath 配置）'
    return
  }

  const pre = _looksLikeAuthRedirect(buf)
  if (pre.redirect) {
    previewError.value = '下载接口返回的不是文件内容（当前登录状态失效，请重新登录后再试）'
    console.warn('[DocPreview] payload 检测到登录重定向：', pre)
    return
  }
  console.log('[DocPreview] 魔数检测：', pre)

  /* Layer 3：图片 / PDF → ObjectURL */
  if (isImage.value || isPdf.value) {
    try {
      let mime = ''
      if (pre.kind === 'png') mime = 'image/png'
      else if (pre.kind === 'jpg') mime = 'image/jpeg'
      else if (pre.kind === 'gif') mime = 'image/gif'
      else if (pre.kind === 'bmp') mime = 'image/bmp'
      else if (pre.kind === 'webp') mime = 'image/webp'
      else if (pre.kind === 'pdf') mime = 'application/pdf'
      const blob = new Blob([buf], { type: mime || 'application/octet-stream' })
      previewUrl.value = URL.createObjectURL(blob)
      console.log(
        `[DocPreview] 图片/PDF 预览构建完成，mime=${mime || 'application/octet-stream'}, size=${buf.byteLength}`
      )
      return
    } catch (e) {
      previewError.value = '文件读取失败: ' + (e?.message || e)
      console.warn('[DocPreview] 图片/PDF Blob 构建失败：', e)
      return
    }
  }

  /* Layer 4：Office（docx/pptx/xls/xlsx）
   *   xls 旧版官方 exceljs BIFF 兼容解析，跳过 ZIP 校验
   *   docx/xlsx/pptx 是 ZIP(OOXML)，做 ZIP + Central Directory 双重校验
   */
  if (isDocx.value || isPptx.value || isExcel.value) {
    let detected = null
    if (ext.value !== 'xls') {
      detected = _detectOfficeKindFromZip(buf)
      console.log(
        `[DocPreview] ZIP Central Directory 子类型检测：ext=${ext.value} detected=${detected}`
      )
    } else {
      console.log(`[DocPreview] ext=.xls 跳过 ZIP Central Directory 检测，走 exceljs BIFF 兼容解析`)
    }

    if (ext.value !== 'xls') {
      if (isDocx.value && detected !== 'docx') {
        previewError.value =
          detected === 'xlsx'
            ? '实际为 Excel 文件，请确认文件扩展名与内容是否一致'
            : detected === 'pptx'
              ? '实际为 PPT 文件，请确认文件扩展名与内容是否一致'
              : detected === 'zip'
                ? '该文件是普通 ZIP，不是合法的 Word (.docx) 文件'
                : '文件内容不是合法的 .docx 格式（已损坏或扩展名被修改）'
        return
      }
      if (isPptx.value && detected !== 'pptx') {
        previewError.value =
          detected === 'docx'
            ? '实际为 Word 文件，请确认文件扩展名与内容是否一致'
            : detected === 'xlsx'
              ? '实际为 Excel 文件，请确认文件扩展名与内容是否一致'
              : detected === 'zip'
                ? '该文件是普通 ZIP，不是合法的 Office (.pptx) 文件'
                : '文件内容不是合法的 .pptx 格式（已损坏或扩展名被修改）'
        return
      }
      if (ext.value === 'xlsx' && detected !== 'xlsx') {
        previewError.value =
          detected === 'docx'
            ? '实际为 Word 文件，请确认文件扩展名与内容是否一致'
            : detected === 'pptx'
              ? '实际为 PPT 文件，请确认文件扩展名与内容是否一致'
              : detected === 'zip'
                ? '该文件是普通 ZIP，不是合法的 Excel (.xlsx) 文件'
                : '文件内容不是合法的 .xlsx 格式（已损坏或扩展名被修改）'
        return
      }
    }

    previewBuffer.value = buf.slice(0) /* vue-office 内部可能会 detach，给独立副本 */
    console.log(
      `[DocPreview] ✅ 已交付给 vue-office：ext=${ext.value} byteLength=${buf.byteLength} kind=${pre?.kind ?? detected ?? 'xls-biff'}`
    )
    return
  }
  /* 其它格式 → template 走兜底 unsupported */
  console.log('[DocPreview] 命中 template 兜底 unsupported 分支：ext=', ext.value)
}

/* ============ 7. 事件 & 生命周期 ============ */
const onRendered = () => {
  console.log('[DocPreview] rendered:', props.file?.name)
}
const onError = (e) => {
  previewError.value = '文档渲染失败: ' + (e?.message || e || '未知错误')
  ElMessage.error(previewError.value)
}
const refresh = () => loadPreview()

/* 下载：同样走带鉴权的 http（http.js resolve response.data = 直接就是 Blob 对象） */
const download = async () => {
  const raw = buildFileUrl()
  if (!raw) return ElMessage.warning('无可用的下载地址')
  try {
    const blob = await _httpGetBlob(raw)
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob instanceof Blob ? blob : new Blob([blob]))
    a.download = props.file?.name || 'file'
    document.body.appendChild(a)
    a.click()
    setTimeout(() => {
      document.body.removeChild(a)
      URL.revokeObjectURL(a.href)
    }, 1200)
  } catch (_) {
    window.open(raw, '_blank')
  }
}

watch(() => props.file, loadPreview, { immediate: true, deep: true })
console.log(
  '[DocPreview] 🟢 watcher 已注册 ✅（这条没出现 = watch 之前代码抛异常） immediate=true → loadPreview 已同步执行'
)
onBeforeUnmount(() => _revoke())
</script>

<style scoped>
.doc-preview {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.06);
  border: 1px solid #e4e7ed;
  overflow: hidden;
}
.preview-header {
  flex-shrink: 0;
  padding: 14px 20px;
  background: linear-gradient(to right, #fff, #f5f7fa);
  border-bottom: 1px solid #e4e7ed;
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.preview-content {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: 20px;
  background: #fafafa;
  position: relative;
}
.preview-content > :deep(.docx-wrapper),
.preview-content > :deep(.excel-table),
.preview-content > :deep(.vue-office-pdf),
.preview-content > :deep(.vue-office-docx),
.preview-content > :deep(.vue-office-excel) {
  height: 100%;
  min-height: 0;
}
.file-icon {
  font-size: 22px;
  color: #409eff;
}
.file-name {
  font-weight: 600;
  font-size: 14px;
  color: #303133;
}
.image-preview {
  width: 100%;
  height: 100%;
  border-radius: 6px;
  overflow: hidden;
}
.text-content {
  background: #fff;
  padding: 24px;
  border-radius: 8px;
  white-space: pre-wrap;
  word-wrap: break-word;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  line-height: 1.8;
  margin: 0;
  color: #303133;
  font-size: 14px;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.04);
  border: 1px solid #ebeef5;
}
.unsupported {
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: #909399;
  gap: 16px;
  background: #fff;
  border-radius: 8px;
}
.unsupported :deep(.el-icon) {
  color: #c0c4cc;
}
.unsupported .tip {
  font-size: 12px;
  color: #b0b0b0;
  margin: -8px 0 0 0;
}
</style>
