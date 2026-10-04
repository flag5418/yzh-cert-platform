/**
 * ★★ 上传文件类型契约（**全平台唯��事实源**，2026-10-03 用户裁决）
 *
 * ════════════════════════════════════════════════════════════════════════
 * 为什么要有这个文件
 *
 * 用户原话：「上传的原则其实只有一点，就是限制上传的文件名，可以设置在 vue_share 中，
 * 形成一个文件的后缀名进行过滤，**而不是遇到问题解决问题**，因为除了标准的文档、
 * 图片、pdf，其他的文件名格式太多，不能发现一个解决一个。」
 *
 * 修之前的现状（逐个打补丁）：
 *   · `enterprise-original/index.vue`  自己一份 ACCEPT 常量
 *   · `PromptTestPanel.vue`            硬编码 '.doc,.docx,.xls,.xlsx,.pdf,.txt,.md,.csv'
 *   · 后端 `EnterpriseOriginalService` 私有一份 AllowedExtensions
 *   · 标准目录 / 资料库的上传端点       **根本不校验后缀**（谁都能传 .exe）
 *   · `.DS_Store` / `Thumbs.db` / `~$xx.doc` 被当「不支持的格式」报给用户，
 *     而前端「有一个不合规就整体中止」⇒ 整个文件夹传不上去
 *
 * ════════════════════════════════════════════════════════════════════════
 * 规则（改规则只改这里，两端同步）
 *
 * 1. **系统噪声**：操作系统/编辑器垃圾文件（`.DS_Store` / `Thumbs.db` / `~$xx.doc` /
 *    `._xx` / `desktop.ini` / 一切 `.` 开头）。⇒ **静默剔除**，不报给用户、不阻塞上传。
 *    用户根本没打算上传它们，报「格式不支持」是错的。
 *
 * 2. **白名单**：只允许下列类别。⛔ 采**白名单**而非黑名单 —— 格式数不清，
 *    黑名单必然漏（用户原话）。压缩包 / 可执行文件 / 音视频 / 工程文件一律拒。
 *
 * 3. **分级**：不同业务可用子集（标准目录只要文档+图片；提示词试跑只要文档+文本）。
 *    用 `UPLOAD_CATEGORIES` 按类别组合，避免每个页面各写一串。
 *
 * ⚠️ **前端过滤只是体验，后端 `UploadFilePolicy.cs` 才是权威** ——
 *    前端可绕过（改 accept / 直接调 API），后端必须硬拦。改规则**两边都要改**。
 */

export type UploadCategory =
  | 'document'   // 办公文档（Word/Excel/PPT/文本）
  | 'image'      // 图片（预览透传 + 证件识别）
  | 'pdf'        // PDF（文本层直解，无文本层才走 AI）
  | 'archive'    // 压缩包（默认**不允许**；确需时按业务显式开启）

export interface UploadPolicyVerdict {
  /** 是否允许上传（系统噪声与白名单外都 false） */
  ok: boolean
  /** 被剔除的原因分类 —— 前端据此选提示文案 */
  reason?: 'system_noise' | 'bad_extension'
  /** 人类可读的说明（前端直接展示，不自行拼文案） */
  message?: string
}

/** 办公文档类 */
export const EXT_DOCUMENT = [
  '.doc', '.docx',
  '.xls', '.xlsx',
  '.ppt', '.pptx',
  '.rtf',
  '.odt', '.ods', '.odp',
  '.txt', '.md', '.csv',
] as const

/** 图片类（证件 / 现场照片主要走这一类） */
export const EXT_IMAGE = [
  '.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp',
] as const

/** PDF 类（单独成类：判定顺序是「先文本层直解 → 失败才 AI」，与其它格式不同） */
export const EXT_PDF = ['.pdf'] as const

/** 压缩包类（默认**不开放**） */
export const EXT_ARCHIVE = ['.zip', '.rar', '.7z', '.tar', '.gz'] as const

/** 分类 → 后缀表 */
export const UPLOAD_CATEGORIES: Record<UploadCategory, readonly string[]> = {
  document: EXT_DOCUMENT,
  image: EXT_IMAGE,
  pdf: EXT_PDF,
  archive: EXT_ARCHIVE,
}

/**
 * 默认允许类别 —— **不含 archive**。
 * ⛔ 为什么不给压缩包开口子：压缩包无法做内容抽取/语义分析/预览，
 * 传上来只会占 MinIO 与数据库，而后续所有处理链都不支持它。
 * （将来若要支持「批量打包上传并自动解压」，应新增**解压任务**而不是放宽白名单。）
 */
export const DEFAULT_UPLOAD_CATEGORIES: readonly UploadCategory[] = ['document', 'image', 'pdf']

/** 默认允许的后缀集合（小写，含点） */
export const UPLOAD_ALLOWED_EXTENSIONS: readonly string[] = [
  ...EXT_DOCUMENT,
  ...EXT_IMAGE,
  ...EXT_PDF,
]

/** 可执行文件 / 系统二进制 —— 明确黑名单（即使将来白名单写错，这些也永远拒） */
export const UPLOAD_NEVER_EXTENSIONS: readonly string[] = [
  '.exe', '.dll', '.so', '.dylib', '.bat', '.cmd', '.sh', '.bash',
  '.msi', '.apk', '.deb', '.rpm',
]

/** HTML/SVG —— ⛔ 拒绝：可内嵌脚本，放行等于给存储开了 XSS 面 */
export const UPLOAD_SCRIPT_EXTENSIONS: readonly string[] = [
  '.html', '.htm', '.xhtml', '.svg', '.xml',
]

/**
 * 操作系统 / 编辑器垃圾文件名（**小写**）。
 * ⛔ 不要用黑名单穷举：`.DS_Store` 在每个目录都可能生成，还有 `.Spotlight-V100`、
 * `.Trashes`、`.fseventsd` 等 ⇒ 永远列不全。改用**前缀规则**兜底（见 `isSystemNoise`）。
 */
export const UPLOAD_SYSTEM_NOISE_NAMES: readonly string[] = [
  'thumbs.db', 'ehthumb.db', 'desktop.ini', 'icon\r',
]

/** 扩展名（小写，含点）；无扩展名时返回空串 */
export function extOf(fileName?: string | null): string {
  const n = fileName ?? ''
  const i = n.lastIndexOf('.')
  return i > 0 ? n.slice(i).toLowerCase() : ''
}

/**
 * ★ 是否为系统噪声（操作系统 / 编辑器垃圾）。
 *
 * <para><b>用前缀规则而不是穷举黑名单</b>（用户原话：「不能发现一个解决一个」）：
 * 一切 `.` 开头（`.DS_Store` / `.Spotlight-V100` / `.Trashes` / `._AppleDouble.x`）
 * 与 `~$` 开头（Office/WPS 锁文件）都是垃圾。</para>
 */
export function isSystemNoise(fileName?: string | null): boolean {
  const n = (fileName ?? '').trim()
  if (!n) return true
  if (n.startsWith('.') || n.startsWith('~$')) return true
  return UPLOAD_SYSTEM_NOISE_NAMES.includes(n.toLowerCase())
}

/** 取某类别组合的后缀白名单 */
export function allowedExtensionsOf(
  categories: readonly UploadCategory[] = DEFAULT_UPLOAD_CATEGORIES,
): readonly string[] {
  return categories.flatMap((c) => UPLOAD_CATEGORIES[c] ?? [])
}

/** 生成 `<input accept>` 属性值（直接绑定 `YzhFolderUpload` 的 `accept` prop） */
export function buildAcceptAttribute(
  categories: readonly UploadCategory[] = DEFAULT_UPLOAD_CATEGORIES,
): string {
  return allowedExtensionsOf(categories).join(',')
}

/**
 * ★ 上传前判定（**单一入口**，页面不要自己写 if/else）。
 *
 * <p>用法（`onFilesPicked` 里）：</p>
 * <pre>{@code
 * const { accepted, rejected } = partitionUploadable(pickedFiles)
 * if (!accepted.length) ElMessage.warning(rejected[0]?.message ?? '没有可上传的文件')
 * else {
 *   if (rejected.length) ElMessage.info(`已忽略 ${rejected.length} 个文件：${rejected[0].message}`)
 *   const plan = await originalPlanUpload(ent, stage, accepted.map(toPlanItem))
 * }
 * }</pre>
 */
export function checkUploadable(
  fileName: string,
  categories: readonly UploadCategory[] = DEFAULT_UPLOAD_CATEGORIES,
): UploadPolicyVerdict {
  if (isSystemNoise(fileName)) {
    return { ok: false, reason: 'system_noise', message: `「${fileName}」是系统文件，已自动忽略` }
  }

  const ext = extOf(fileName)
  if (!ext) {
    return { ok: false, reason: 'bad_extension', message: `「${fileName}」没有扩展名，无法判断类型` }
  }
  if (UPLOAD_NEVER_EXTENSIONS.includes(ext) || UPLOAD_SCRIPT_EXTENSIONS.includes(ext)) {
    return { ok: false, reason: 'bad_extension', message: `「${fileName}」是程序或脚本文件，不允许上传` }
  }
  const allowed = allowedExtensionsOf(categories)
  if (!allowed.includes(ext)) {
    return {
      ok: false,
      reason: 'bad_extension',
      message: `「${fileName}」不是支持的资料格式（支持：${allowed.join(' ')}）`,
    }
  }
  return { ok: true }
}

/** 批量判定后分区（系统噪声与格式不符都会被剔除，但**分属两类原因**） */
export function partitionUploadable(
  files: Array<{ name: string }>,
  categories: readonly UploadCategory[] = DEFAULT_UPLOAD_CATEGORIES,
): {
  accepted: Array<{ name: string }>
  noise: Array<{ name: string; message: string }>
  rejected: Array<{ name: string; message: string }>
} {
  const accepted: Array<{ name: string }> = []
  const noise: Array<{ name: string; message: string }> = []
  const rejected: Array<{ name: string; message: string }> = []

  for (const f of files) {
    const v = checkUploadable(f.name, categories)
    if (v.ok) accepted.push(f)
    else if (v.reason === 'system_noise') noise.push({ name: f.name, message: v.message ?? '' })
    else rejected.push({ name: f.name, message: v.message ?? '' })
  }
  return { accepted, noise, rejected }
}

/** 人类可读的「支持哪些格式」说明（UI 直接展示，避免各处文案漂移） */
export function describeAllowed(categories: readonly UploadCategory[] = DEFAULT_UPLOAD_CATEGORIES): string {
  const label: Record<UploadCategory, string> = {
    document: 'Word / Excel / PPT / 文本',
    image: '图片',
    pdf: 'PDF',
    archive: '压缩包',
  }
  return categories.map((c) => label[c]).join(' / ')
}