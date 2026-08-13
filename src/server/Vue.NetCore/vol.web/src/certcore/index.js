/**
 * certcore —— 认证平台项目级通用层（业务域）
 *
 * 分层：
 *   yzh/      框架级（多项目可剥离）：设计令牌 / 统一图标 / 基础组件
 *   certcore/ 项目级（认证业务域）：目录树 / 文档预览 / 标准目录 API / 转换状态
 *   views/cert/  业务页面：只写差异
 *
 * 样式引用：先 @import '@/yzh/styles/yzh.css'，再 @import '@/certcore/styles/cert-tokens.css'
 */

/* 组件 */
export { default as CertDirectoryTree } from './components/CertDirectoryTree.vue'
export { default as CertConvertBadge } from './components/CertConvertBadge.vue'
export { default as CertStatusBar } from './components/CertStatusBar.vue'
export { default as CertPageHeader } from './components/CertPageHeader.vue'

/* composables */
export { useFileTree } from './composables/useFileTree'
export { useDirectoryApi } from './composables/useDirectoryApi'
export { usePolling } from './composables/usePolling'

/* utils */
export { formatFileSize, formatDate, formatDateTime } from './utils/format'
export { isOk, getData, getMessage, unwrap, pickCamel } from './utils/api'
export { downloadBlob, downloadBlobPost, parseFileNameFromDisposition, fileNameOf } from './utils/download'
export {
  CONVERT_STATUS_MAP,
  convertStatusInfo,
  convertStatusBadgeType,
  convertStatusLabel
} from './utils/convertStatus'

/* icons */
export { CertTreeIcon, CertFileIcon, CERT_FILE_TYPE_COLOR } from './icons'
