/**
 * certcore 业务图标映射（认证业务域）
 * 基础图标经 YzhIcon，本文件只做业务语义扩展
 * 换图标只改本文件
 */
import { OfficeBuilding, Calendar, Picture, VideoCamera } from '@element-plus/icons-vue'
import { IconFolder, IconFile, IconFileChecked, IconCircleSuccess, IconWarning, IconPending } from '@/yzh'

/** 目录树层级图标 */
export const CertTreeIcon = {
  organization: OfficeBuilding,
  standard: IconFile,
  stage: Calendar,
  folder: IconFolder,
  file: IconFile,
  fileConfigured: IconFileChecked
}

/** 文件扩展名 → 图标（业务扩展用） */
export const CertFileIcon = {
  pdf: IconFile,
  doc: IconFile,
  docx: IconFile,
  xls: IconFile,
  xlsx: IconFile,
  png: Picture,
  jpg: Picture,
  jpeg: Picture,
  gif: Picture,
  bmp: Picture,
  webp: Picture,
  mp4: VideoCamera,
  default: IconFile
}

/** 文件扩展名 → 类型色（CSS 变量） */
export const CERT_FILE_TYPE_COLOR = {
  pdf: 'var(--cert-color-file-pdf)',
  doc: 'var(--cert-color-file-doc)',
  docx: 'var(--cert-color-file-doc)',
  xls: 'var(--cert-color-file-xls)',
  xlsx: 'var(--cert-color-file-xls)',
  jpg: 'var(--cert-color-file-image)',
  jpeg: 'var(--cert-color-file-image)',
  png: 'var(--cert-color-file-image)',
  gif: 'var(--cert-color-file-image)',
  bmp: 'var(--cert-color-file-image)',
  webp: 'var(--cert-color-file-image)',
  default: 'var(--cert-color-file-default)'
}

/** 转换状态图标（供 CertConvertBadge 使用） */
export const CERT_CONVERT_ICON = {
  pending: IconPending,
  converting: null, // 用 Loading（旋转），由组件内处理
  converted: IconCircleSuccess,
  failed: IconWarning
}

/** 规则状态图标 */
export const CERT_RULE_ICON = {
  none: null,
  configured: IconFileChecked,
  failed: IconWarning
}
