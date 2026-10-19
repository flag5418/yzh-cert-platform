/**
 * 企业原始资料管理 · 页面逻辑
 *
 * 菜单 `MENU_AUD_11`｜路由 `/enterprise-original`｜后端 `/api/Auditor/EnterpriseOriginal`
 * 规格：docs/20-体系认证/03-详细设计/05-企业资料规范化/36-企业原始资料管理设计-V1.md
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 面向专家的设计原则（2026-10-03 老板反馈后重做）
 *
 * 专家**不需要**知道 Markdown / 转换链 / 队列 / Sha256 / 策略这些技术细节。
 * 专家只关心三件事：
 *   ① 文件处理好了吗？          → 「文件状态」（已就绪 / 处理中 / 处理失败 / 需人工处理）
 *   ② 内容对不对？              → 「预览」（直接看到文件本身）
 *   ③ 系统认出来的东西准不准？  → 「标签」「作用」（可随时人工修正，语义分析必然会错）
 *
 * ⇒ 页面主体是**卡片列表**而不是表格：每张卡 = 一份文件 + 它的标签 + 它的作用，
 *   三个操作（预览 / 改标签 / 改作用）直接摊在卡面上，不需要点进详情。
 * ════════════════════════════════════════════════════════════════════════
 */

import { confirmOrFalse, SingleTableCore } from '@yzh-core'
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { AnalyzePolicyKey, DocProfile, OriginalFile, QueueDetail, StandardRef, StatusBar } from '@share/api/ent/enterprise-original'
import { partitionUploadable } from '@share/constants/upload-file-policy'
import { saveBlob } from '@share/utils/download'
import {
  batchRemoveFile,
  batchSetPolicy,
  fetchMarkdownContent,
  regenerate,
  correctProfile,
  fetchFilesFiltered,
  fetchProfile,
  fetchQueueDetail,
  fetchStatusBar,
  fetchStageTree,
  fetchTags,
  originalDownloadBlob,
  originalPreviewBlob,
  originalUploadConfirm,
  originalUploadFile,
  originalUploadInit,
  originalPlanUpload as planUpload,
  removeFile,
  restoreVersion,
  setPolicy,
} from '@share/api/ent/enterprise-original'
import type { PlanItem, PlanRow } from '@share/api/ent/enterprise-original'
import { fetchVersions } from '@share/api/ent/enterprise-original'
import type { FileVersion } from '@share/api/ent/enterprise-original'

export { formatSize } from '@share/api/ent/enterprise-original'

export const STATUS_TEXT: Record<string, { text: string; type: 'success' | 'info' | 'warning' | 'primary' | 'danger' }> = {
  done: { text: '已提取', type: 'success' },
  processing: { text: '提取中', type: 'primary' },
  failed: { text: '提取失败', type: 'danger' },
  /** ★ 原「需人工填写」—— 改成陈述事实（系统读不了），⛔ 不再下指令 */
  manual: { text: '无法读取', type: 'warning' },
  /** ★ 原「已排除提取」—— 与用户语言「不参与数据提取」对齐 */
  excluded: { text: '不参与提取', type: 'info' },
  pending: { text: '待提取', type: 'info' },
  /** ★ 原「不建议提取」—— 同上，陈述而非建议 */
  notSuggested: { text: '不参与提取', type: 'info' },
}

export type StatusKey = 'done' | 'processing' | 'failed' | 'manual' | 'excluded' | 'pending' | 'notSuggested'

export interface OriginalFolderNode {
  Path: string
  Name: string
  Depth: number
  Files: OriginalFile[]
  Children: OriginalFolderNode[]
  Total: number
  Done: number
  Failed: number
}

export type UploadTarget =
  | { kind: 'stage' }
  | { kind: 'folder'; path: string; name: string }
  | { kind: 'replace'; path: string; file: OriginalFile }

export function describeQueueType(t?: string | null): string {
  if (t === 'enterprise_original_file') return '处理资料内容'
  if (t === 'enterprise_original_analyze') return '识别资料内容'
  return '读取文件内容'
}

function findStageNode(list: any[], code: string): any | null {
  for (const n of list ?? []) {
    if (n.Code === code) return n
    const hit = findStageNode(n.Children ?? [], code)
    if (hit) return hit
  }
  return null
}

export function statusKeyOf(row: OriginalFile): StatusKey {
  if (row.IsUsableForFilling) return 'done'
  if (row.IsNotSuggested) return 'notSuggested'
  const cv = row.ConvertStatus
  const md = row.MarkdownStatus
  const an = row.AnalyzeStatus
  if (cv === 'converting' || md === 'converting' || an === 'analyzing') return 'processing'
  if (cv === 'unsupported' || md === 'unsupported') return 'manual'
  if (cv === 'failed' || md === 'failed' || an === 'failed') return 'failed'
  if (an === 'skipped' || row.AnalyzePolicy === 'ignore' || row.AnalyzePolicy === 'skip') return 'excluded'
  return 'pending'
}

export function statusOf(row: OriginalFile): { text: string; type: 'success' | 'info' | 'warning' | 'primary' | 'danger' } {
  return STATUS_TEXT[statusKeyOf(row)]
}

export function statusTip(row: OriginalFile): string {
  const k = statusKeyOf(row)
  if (k === 'manual') {
    return '这个格式系统自动读不了内容。如果需要系统读取，换 PDF 或 Word（docx）重传即可；不需要读取就保持原样。'
  }
  if (k === 'failed') {
    return row.AnalyzeMessage || row.ConvertMessage || row.MarkdownMessage || '处理时出错，可点「重新生成」，或删除后重新上传'
  }
  if (k === 'notSuggested') {
    return row.NotSuggestedByFormat
      ? '图片类文件（营业执照、身份证、资质证书等证照扫描件），没有正文可提取'
      : '这份文件当前不参与提取（仍作为证据保留）'
  }
  if (k === 'pending') return row.UnusableReason || '还没开始处理，稍等片刻刷新看看'
  if (k === 'excluded') return '这份文件当前不参与提取（仍作为证据保留）'
  return ''
}

export function isExtractionUnsupported(row: OriginalFile): boolean {
  return String(row.MarkdownStatus || '').toLowerCase() === 'unsupported'
}

export function unsupportedExtractionTip(row: OriginalFile): string {
  return (
    row.MarkdownMessage ||
    row.ConvertMessage ||
    '该文件的类型无法被自动识别，不能自动提取内容。可手工定义字段与表格，由人工填写。'
  )
}

export function onFileRowClick(row: OriginalFile): void {
  if (!isExtractionUnsupported(row)) return
  ElMessage.warning(`该文件不支持提取规则：${unsupportedExtractionTip(row)}`)
}

export function canPreview(row: OriginalFile): boolean {
  return row.ConvertStatus === 'completed' || !!row.PreviewPdfPath || !!row.StoragePath
}

export function displayName(row: OriginalFile): string {
  const n = row.FileName || ''
  const i = n.lastIndexOf('.')
  return i > 0 ? n.slice(0, i) : n
}

export function buildTree(files: OriginalFile[]): {
  root: OriginalFile[]
  nodes: OriginalFolderNode[]
} {
  const root = files.filter((f) => !f.RelFolderPath)
  const map = new Map<string, OriginalFolderNode>()

  // 先把出现过的所有目录路径登记成节点
  for (const f of files) {
    const p = (f.RelFolderPath || '').replace(/^\/+|\/+$/g, '')
    if (!p) continue
    const segs = p.split('/').filter(Boolean)
    let acc = ''
    for (let i = 0; i < segs.length; i++) {
      acc = acc ? `${acc}/${segs[i]}` : segs[i]
      if (!map.has(acc)) {
        map.set(acc, {
          Path: acc,
          Name: segs[i],
          Depth: i + 1,
          Files: [],
          Children: [],
          Total: 0, Done: 0, Failed: 0,
        })
      }
    }
  }

  // 文件挂到最深目录
  for (const f of files) {
    const p = (f.RelFolderPath || '').replace(/^\/+|\/+$/g, '')
    if (!p) continue
    map.get(p)?.Files.push(f)
  }

  // 组树：短路径是长路径的父
  const nodes: OriginalFolderNode[] = []
  for (const node of map.values()) {
    const idx = node.Path.lastIndexOf('/')
    const parentPath = idx > 0 ? node.Path.slice(0, idx) : ''
    const parent = parentPath ? map.get(parentPath) : undefined
    if (parent) parent.Children.push(node)
    else nodes.push(node)
  }

  const sortRec = (list: OriginalFolderNode[], depth: number): number => {
    list.sort((a, b) => a.Name.localeCompare(b.Name, 'zh-CN'))
    for (const n of list) {
      n.Total = n.Files.length + sortRec(n.Children, depth + 1)
      n.Done = n.Files.filter((f) => f.IsUsableForFilling).length
        + n.Children.reduce((s, c) => s + c.Done, 0)
      n.Failed = n.Files.filter((f) => f.ConvertStatus === 'failed' || f.AnalyzeStatus === 'failed').length
        + n.Children.reduce((s, c) => s + c.Failed, 0)
    }
    return list.reduce((s, n) => s + n.Total, 0)
  }
  sortRec(nodes, 0)
  return { root, nodes }
}

export function folderFileCodes(node: OriginalFolderNode): string[] {
  const out: string[] = []
  const walk = (n: OriginalFolderNode): void => {
    for (const f of n.Files) out.push(f.Code)
    for (const c of n.Children) walk(c)
  }
  walk(node)
  return out
}

function reportBatch(prefix: string, r: { Total: number; SuccessCount: number; FailedCount: number }): void {
  const msg = `${prefix}：成功 ${r.SuccessCount} / ${r.Total} 份${r.FailedCount ? `，失败 ${r.FailedCount} 份` : ''}`
  ElMessage[r.FailedCount > 0 ? 'warning' : 'success'](msg)
}

export function previewKindOf(row: OriginalFile): 'pdf' | 'image' | 'text' | 'none' {
  const t = (row.FileType || '').toLowerCase()
  if (['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp'].includes(t)) return 'image'
  if (t === '.pdf') return 'pdf'
  if (['.txt', '.md', '.csv'].includes(t)) return 'text'
  // Word/Excel/PPT 一律走服务端转出的 PDF
  return 'pdf'
}

function remapFolderPath(rawDir: string, t: { path: string; name: string }): string {
  const dir = (rawDir || '').replace(/^\/+|\/+$/g, '')
  const tgt = (t.path || '').replace(/^\/+|\/+$/g, '')
  if (!tgt) return dir
  if (!dir) return tgt
  const segs = dir.split('/')
  if (segs[0] === t.name) return [tgt, ...segs.slice(1)].filter(Boolean).join('/')
  return [tgt, dir].filter(Boolean).join('/')
}

export async function onDownload(row: OriginalFile): Promise<void> {
  try {
    // 保存统一走 `saveBlob`（全平台唯一实现，P1.3）
    saveBlob(await originalDownloadBlob(row.StoragePath, row.EnterpriseCode), row.FileName)
  } catch (e) {
    ElMessage.error((e as Error).message)
  }
}

/**
 * 页面 Logic（R24：必须 extends 内核基类）
 *
 * ⛔ 覆写说明：
 *   · `loadConfig()` 置空 —— 本页没有 EntityConfig，不置空则 `init()` 会请求不存在的 `/config`
 *   · `refresh()` 被页面级「重拉树 + 标准 + 文件」覆盖（基类版本只刷 tableRef，本页不适用）
 */
export class EnterpriseOriginalLogic extends SingleTableCore<any> {
  controllerName = 'Auditor/EnterpriseOriginal'

  /** 本页无 /config 端点 ⇒ 空实现，避免 init() 打 404 */
  protected async loadConfig(): Promise<void> {}

  protected async onAfterInit(): Promise<void> {
    await this.loadTree()
  }

  readonly treeNodes = ref<any[]>([])

  readonly treeHint = ref('')

  readonly enterpriseCode = ref('')

  readonly stageCode = ref('')

  readonly scopeLabel = ref('')

  readonly scopeLoading = ref(false)

  readonly standards = ref<StandardRef[]>([])

  readonly activeStandard = ref('')

  readonly activeStandardName = computed(() => {
    const hit = this.standards.value.find((s) => s.Code === this.activeStandard.value)
    return hit ? (hit.StandardName || hit.StandardCode) : ''
  })

  readonly files = ref<OriginalFile[]>([])

  readonly statusBar = ref<StatusBar | null>(null)

  readonly selected = ref<OriginalFile[]>([])

  readonly filterTags = ref<string[]>([])

  readonly onlyUsable = ref(false)

  readonly tagOptions = ref<Array<{ TagCode: string; TagName: string; TagGroup?: string | null }>>([])

  readonly isBusy = computed(() => this.statusBar.value?.IsBusy === true)

  readonly queueProgress = computed(() => {
    const sb = this.statusBar.value
    if (!sb) return 0
    if (typeof sb.QueueProgress === 'number' && sb.QueueProgress > 0) return sb.QueueProgress
    if ((sb.QueueTotal ?? 0) > 0) return Math.round(((sb.QueueCompleted ?? 0) / (sb.QueueTotal ?? 1)) * 100)
    return 0
  })

  readonly queueLabel = computed(() => `正在${describeQueueType(this.statusBar.value?.QueueType)}`)

  pollTimer: any = null

  readonly tagNameMap = computed<Record<string, string>>(() =>
    Object.fromEntries(this.tagOptions.value.map((t) => [t.TagCode, t.TagName])))

  readonly filterActive = computed(() => this.filterTags.value.length > 0 || this.onlyUsable.value)

  readonly filterSummary = computed(() => {
    const parts: string[] = []
    if (this.filterTags.value.length)
      parts.push(this.filterTags.value.map((c) => this.tagNameMap.value[c] ?? c).join('、'))
    if (this.onlyUsable.value) parts.push('只看已就绪的')
    return parts.join(' ＋ ')
  })

  readonly scopeTotal = computed(() => this.statusBar.value?.Total ?? 0)

  readonly filteredCount = computed(() => this.files.value.length)

  readonly tableRef = ref<any>(null)

  readonly dataRevision = ref(0)

  readonly rootFiles = ref<OriginalFile[]>([])

  readonly folderNodes = ref<OriginalFolderNode[]>([])

  readonly detailVisible = ref(false)

  readonly detailRow = ref<OriginalFile | null>(null)

  readonly detailProfile = ref<DocProfile | null>(null)

  readonly detailLoading = ref(false)

  readonly infoTab = ref('group')

  readonly detailPurposeText = ref('')

  readonly contentText = ref('')

  readonly contentMessage = ref('')

  readonly contentLoading = ref(false)

  readonly regenerating = ref('')

  readonly previewVisible = ref(false)

  readonly previewRow = ref<OriginalFile | null>(null)

  readonly previewUrl = ref('')

  readonly previewKind = ref<'pdf' | 'image' | 'text' | 'none'>('none')

  readonly previewText = ref('')

  readonly previewLoading = ref(false)

  readonly tagDraft = ref<string[]>([])

  readonly purposeSaving = ref(false)

  readonly uploadVisible = ref(false)

  readonly uploadStage = ref<'idle' | 'uploading' | 'done'>('idle')

  readonly planRows = ref<PlanRow[]>([])

  readonly planSummary = ref<any>({})

  readonly uploadProgress = ref('')

  readonly pendingFiles = ref<File[]>([])

  readonly uploadTarget = ref<UploadTarget>({ kind: 'stage' })

  readonly uploadTargetLabel = computed(() => {
    const t = this.uploadTarget.value
    if (t.kind === 'folder') return `更新文件夹「${t.name}」下的内容（${t.path}）`
    if (t.kind === 'replace') return `替换「${t.file.FileName}」（${t.path || '根目录'}）· 请选择同名文件`
    return ''
  })

  readonly uploadTitle = computed(() => {
    const t = this.uploadTarget.value
    if (t.kind === 'replace') return '替换文件'
    if (t.kind === 'folder') return '更新文件夹内容'
    return '上传文件'
  })

  readonly uploadTargetAlertType = computed<'warning' | 'info'>(() =>
    this.uploadTarget.value.kind === 'replace' ? 'warning' : 'info')

  readonly versionVisible = ref(false)

  readonly versions = ref<FileVersion[]>([])

  readonly currentVersion = ref(1)

  readonly versionTarget = ref<OriginalFile | null>(null)

  readonly policyVisible = ref(false)

  readonly policyTarget = ref<'single' | 'batch'>('single')

  readonly policyValue = ref<AnalyzePolicyKey>('analyze')

  readonly policyReason = ref<string>('')

  readonly policyRows = computed<OriginalFile[]>(() =>
    this.policyTarget.value === 'batch' ? this.selected.value : (this.policyEditing.value ? [this.policyEditing.value] : []))

  readonly policyEditing = ref<OriginalFile | null>(null)

  readonly queueVisible = ref(false)

  readonly queueDetail = ref<QueueDetail | null>(null)

  readonly queueLoading = ref(false)

  async loadTree(): Promise<void> {
    try {
      const nodes = await fetchStageTree()
      this.treeNodes.value = nodes.map((n: any) => ({
        Code: n.Code,
        Label: n.Label,
        FileCount: n.FileCount,
        EnterpriseCode: n.EnterpriseCode,
        Children: (n.Children ?? []).map((c: any) => ({
          ...c,
          EnterpriseCode: c.EnterpriseCode,
          // ★ 标准清单随阶段节点透传（`...c` 已带上，这里显式写出以防后人误删）
          Standards: c.Standards ?? [],
          Children: undefined,
        })),
      }))
      this.treeHint.value = nodes.length === 0 ? '当前工作区下没有企业' : ''
    } catch (e) {
      this.treeHint.value = (e as Error).message
    }
  }

  async onNodeClick(node: any): Promise<void> {
    const isLeaf = !node?.Children || node.Children.length === 0
    if (!isLeaf) {
      this.scopeLabel.value = `${node.Label}（请选择具体阶段）`
      this.stageCode.value = ''
      this.standards.value = []
      this.activeStandard.value = ''
      this.files.value = []
      this.statusBar.value = null
      return
    }
    const entCode = node.EnterpriseCode ?? node.Code
    if (!entCode || !node.Code) {
      ElMessage.warning('树节点缺少企业/阶段标识，请刷新后重试')
      return
    }
    this.enterpriseCode.value = entCode
    this.stageCode.value = node.Code
    this.scopeLabel.value = node.Label
    // ★ 标准 tab：该阶段绑定的标准清单（多标准 ⇒ 出 tab；单标准 ⇒ 只显示标准名）
    this.standards.value = (node.Standards ?? []) as StandardRef[]
    this.activeStandard.value = this.standards.value[0]?.Code ?? ''
    // ⚠️ 换阶段必须清标签筛选：标签是**标准相关**的，上一个标准的 TagCode 在新标准下
    //    可能根本不存在 ⇒ 筛出 0 命中，页面看着像「文件丢了」（2026-10-06 同类投诉）。
    this.filterTags.value = []
    await this.loadFiles()
    this.startPolling()      // ★ 队列在跑时自动刷新进度
  }

  async onStandardChange(code: string): Promise<void> {
    if (!code || code === this.activeStandard.value) return
    this.activeStandard.value = code
    this.filterTags.value = []
    await this.loadFiles()
  }

  startPolling(): void {
    this.stopPolling()
    this.pollTimer = setInterval(async () => {
      if (!this.enterpriseCode.value || !this.stageCode.value) return
      try {
        const sb = await fetchStatusBar(this.enterpriseCode.value, this.stageCode.value)
        const wasBusy = this.statusBar.value?.IsBusy === true
        this.statusBar.value = sb
        // 队列刚跑完 ⇒ 拉一次文件列表（标签/作用此时才有值）
        if (wasBusy && sb?.IsBusy !== true) {
          await this.loadFiles()
          ElMessage.success('资料处理完成')
        } else if (sb?.IsBusy) {
          await this.loadFiles()
        }
      } catch { /* 轮询失败静默，不打断用户 */ }
    }, 3000)
  }

  stopPolling(): void {
    if (this.pollTimer) { clearInterval(this.pollTimer); this.pollTimer = null }
  }

  async loadFiles(): Promise<void> {
    if (!this.enterpriseCode.value || !this.stageCode.value) return
    this.scopeLoading.value = true
    try {
      // ★ 必须带上 activeStandard：标签/作用是按「标准那一行画像」来的（见 standards 注释）
      const r = await fetchFilesFiltered(
        this.enterpriseCode.value, this.stageCode.value, this.filterTags.value, this.onlyUsable.value, false,
        this.activeStandard.value)
      this.files.value = r.rows
      const t = buildTree(r.rows)
      this.rootFiles.value = t.root
      this.folderNodes.value = t.nodes
      this.statusBar.value = await fetchStatusBar(this.enterpriseCode.value, this.stageCode.value)
      this.dataRevision.value++
    } catch (e) {
      this.reportError(e, '加载文件列表失败')
    } finally {
      this.scopeLoading.value = false
    }
  }

  async loadTags(): Promise<void> {
    if (this.tagOptions.value.length > 0) return
    try {
      this.tagOptions.value = await fetchTags('enterprise')
    } catch (e) {
      ElMessage.warning(`标签清单加载失败：${(e as Error).message}`)
    }
  }

  async toggleFilterTag(code: string): Promise<void> {
    const i = this.filterTags.value.indexOf(code)
    if (i >= 0) this.filterTags.value.splice(i, 1)
    else this.filterTags.value.push(code)
    await this.loadFiles()
  }

  async clearFilters(): Promise<void> {
    this.filterTags.value = []
    this.onlyUsable.value = false
    await this.loadFiles()
  }

  async refresh(): Promise<void> {
    await this.loadTree()
    // ★ 树刷新后标准清单可能变（管理员改了该阶段的关联标准）⇒ 从新树里重新取一次；
    //   当前选中的标准若已不在清单里，回落到第一个（否则会带着一个失效的 GUID 去查画像）
    const cur = findStageNode(this.treeNodes.value, this.stageCode.value)
    if (cur) {
      this.standards.value = (cur.Standards ?? []) as StandardRef[]
      if (!this.standards.value.some((s) => s.Code === this.activeStandard.value))
        this.activeStandard.value = this.standards.value[0]?.Code ?? ''
    }
    await this.loadFiles()
  }

  async openDetail(row: OriginalFile): Promise<void> {
    this.detailRow.value = row
    this.detailVisible.value = true
    this.detailLoading.value = true
    this.detailProfile.value = null
    this.infoTab.value = 'group'
    // 先用列表行的值垫底（列表已按当前标准取画像）⇒ 即使画像接口失败也能编辑
    this.detailPurposeText.value = row.DocPurpose ?? ''
    this.tagDraft.value = [...(row.Tags ?? [])]
    try {
      // ★ 必须带 activeStandard：不带就只能拿到「某一行」画像
      //   ⇒ 显示的标签/作用可能属于另一个标准（用户点名痛点）
      const p = await fetchProfile(row.Code, row.EnterpriseCode, this.activeStandard.value)
      this.detailProfile.value = p
      if (p) this.detailPurposeText.value = p.DocPurpose ?? ''
    } catch {
      // 该标准下无画像（策略跳过 / 分析未完成）⇒ 保留列表行的值，仍可人工编辑
    } finally {
      this.detailLoading.value = false
    }
    // ★ 第二个 Tab「提取内容」一并取回（原独立的「提取内容」抽屉已并入本抽屉）
    await this.loadInfoContent(row)
  }

  async onDetailStandardChange(code: string): Promise<void> {
    if (!code || code === this.activeStandard.value) return
    this.activeStandard.value = code
    const row = this.detailRow.value
    if (row) await this.openDetail(row)
  }

  closeDetail(): void {
    this.detailVisible.value = false
    this.detailRow.value = null
    this.detailProfile.value = null
    this.detailPurposeText.value = ''
    this.infoTab.value = 'group'
    this.contentText.value = ''
    this.contentMessage.value = ''
  }

  async saveDetail(): Promise<void> {
    const row = this.detailRow.value
    if (!row) return
    this.purposeSaving.value = true
    try {
      await correctProfile({
        FileCode: row.Code,
        EnterpriseCode: row.EnterpriseCode,
        // ★ 必须带标准，否则后端改的是「版本号最大那一行」= 可能不是用户正在看的标准
        StandardCode: this.activeStandard.value || null,
        TagsJson: JSON.stringify(this.tagDraft.value),
        DocPurpose: this.detailPurposeText.value,
        InfoItemsJson: this.detailProfile.value?.InfoItemsJson ?? undefined,
      })
      ElMessage.success('已保存')
      this.detailVisible.value = false
      await this.loadFiles()
    } catch (e) {
      this.reportError(e, '保存失败')
    } finally {
      this.purposeSaving.value = false
    }
  }

  async excludeExtract(row: OriginalFile): Promise<void> {
    try {
      await setPolicy(row.Code, row.EnterpriseCode, 'ignore', 'manual', true)
      ElMessage.success('已排除提取（文件仍作为证据保留）')
      await this.loadFiles()
    } catch (e) {
      this.reportError(e, '排除提取失败')
    }
  }

  async allowExtract(row: OriginalFile): Promise<void> {
    try {
      await setPolicy(row.Code, row.EnterpriseCode, 'analyze', null, true)
      ElMessage.success('已允许提取')
      await this.loadFiles()
    } catch (e) {
      this.reportError(e, '恢复提取失败')
    }
  }

  async excludeFolder(node: OriginalFolderNode): Promise<void> {
    const codes = folderFileCodes(node)
    if (codes.length === 0) { ElMessage.warning('这个文件夹里没有文件'); return }
    try {
      const r = await batchSetPolicy(codes, this.enterpriseCode.value, 'ignore', 'manual', true)
      reportBatch(`「${node.Name}」已设为不参与提取`, r)
      await this.loadFiles()
    } catch (e) {
      this.reportError(e, '文件夹排除提取失败')
    }
  }

  async allowFolder(node: OriginalFolderNode): Promise<void> {
    const codes = folderFileCodes(node)
    if (codes.length === 0) { ElMessage.warning('这个文件夹里没有文件'); return }
    try {
      const r = await batchSetPolicy(codes, this.enterpriseCode.value, 'analyze', null, true)
      reportBatch(`「${node.Name}」已恢复参与提取`, r)
      await this.loadFiles()
    } catch (e) {
      this.reportError(e, '文件夹恢复提取失败')
    }
  }

  async deleteFolder(node: OriginalFolderNode): Promise<void> {
    const codes = folderFileCodes(node)
    if (codes.length === 0) { ElMessage.warning('这个文件夹里没有文件'); return }
    const ok = await confirmOrFalse(
      `删除整个「${node.Name}」文件夹？该文件夹（含子文件夹）下共 ${codes.length} 份文件会被一并删除，` +
      `历史版本也会移除；识别结果（标签 / 作用）会保留以备审计。`,
      '删除整个文件夹', { type: 'warning', confirmButtonText: '确认删除', cancelButtonText: '取消' })
    if (!ok) return
    try {
      const r = await batchRemoveFile(codes, this.enterpriseCode.value, `删除文件夹：${node.Path}`)
      reportBatch(`已删除「${node.Name}」`, r)
      await this.loadFiles()
    } catch (e) {
      this.reportError(e, '删除文件夹失败')
    }
  }

  async loadInfoContent(row: OriginalFile): Promise<void> {
    this.contentLoading.value = true
    this.contentText.value = ''
    this.contentMessage.value = ''
    try {
      const r = await fetchMarkdownContent(row.Code, row.EnterpriseCode)
      if (r?.Markdown) this.contentText.value = r.Markdown
      // ⛔ 不弹 toast：抽屉自己会显示空态 + 原因，弹两个提示只会让人以为出了两次错
      else this.contentMessage.value = r?.Message ?? '尚未生成内容'
    } catch (e) {
      this.contentMessage.value = `读取内容失败：${(e as Error).message}`
    } finally {
      this.contentLoading.value = false
    }
  }

  async onRegenerate(row: OriginalFile, reanalyze = true): Promise<void> {
    this.regenerating.value = row.Code
    try {
      const r = await regenerate(row.Code, row.EnterpriseCode, reanalyze)
      ElMessage.success(r?.Message ?? '已重新入队')
      await this.loadFiles()
    } catch (e) {
      this.reportError(e, '重新提取失败')
    } finally {
      this.regenerating.value = ''
    }
  }

  async openPreview(row: OriginalFile): Promise<void> {
    this.previewRow.value = row
    this.previewVisible.value = true
    this.previewLoading.value = true
    this.previewText.value = ''
    this.previewUrl.value = ''
    try {
      if (previewKindOf(row) === 'image') {
        const blob = await originalPreviewBlob(row.Code, row.EnterpriseCode)
        this.previewKind.value = 'image'
        this.previewUrl.value = URL.createObjectURL(blob)
      } else if (previewKindOf(row) === 'text') {
        const blob = await originalPreviewBlob(row.Code, row.EnterpriseCode)
        this.previewKind.value = 'text'
        this.previewText.value = await blob.text()
      } else {
        const blob = await originalPreviewBlob(row.Code, row.EnterpriseCode)
        this.previewKind.value = 'pdf'
        this.previewUrl.value = URL.createObjectURL(blob)
      }
    } catch (e) {
      this.previewKind.value = 'none'
      ElMessage.warning(`预览失败：${(e as Error).message}`)
    } finally {
      this.previewLoading.value = false
    }
  }

  closePreview(): void {
    if (this.previewUrl.value) URL.revokeObjectURL(this.previewUrl.value)
    this.previewUrl.value = ''
    this.previewRow.value = null
  }

  openUpload(): void { this.openUploadWith({ kind: 'stage' }) }

  openFolderUpload(node: OriginalFolderNode): void {
    this.openUploadWith({ kind: 'folder', path: node.Path, name: node.Name })
  }

  openReplace(row: OriginalFile): void {
    this.openUploadWith({ kind: 'replace', path: row.RelFolderPath || '', file: row })
  }

  openUploadWith(target: UploadTarget): void {
    if (!this.enterpriseCode.value || !this.stageCode.value) {
      ElMessage.warning('请先在左侧选择「企业 › 阶段」')
      return
    }
    this.uploadStage.value = 'idle'
    this.planRows.value = []
    this.planSummary.value = {}
    this.pendingFiles.value = []
    this.uploadProgress.value = ''
    this.uploadTarget.value = target
    this.uploadVisible.value = true
  }

  toItems(files: File[]): PlanItem[] {
    const t = this.uploadTarget.value
    return files.map((f) => {
      const rel = (f as any).webkitRelativePath || f.name
      const slash = rel.lastIndexOf('/')
      const name = slash >= 0 ? rel.slice(slash + 1) : rel
      const dir = slash > 0 ? rel.slice(0, slash) : ''
      // ★ 替换：强制沿用登记名与登记目录（后端会校验文件名一致，⛔ 不允许改名）
      if (t.kind === 'replace') {
        return { FileName: t.file.FileName, RelFolderPath: t.path, FileSize: f.size }
      }
      if (t.kind === 'folder') {
        return { FileName: name, RelFolderPath: remapFolderPath(dir, t), FileSize: f.size }
      }
      return { FileName: name, RelFolderPath: dir, FileSize: f.size }
    })
  }

  removePlanRow(index: number): void {
    if (index < 0 || index >= this.planRows.value.length) return
    this.planRows.value.splice(index, 1)
    const sum = this.planSummary.value ?? {}
    this.planSummary.value = {
      ...sum,
      Total: this.planRows.value.length,
      BlockedCount: this.planRows.value.filter((r) => r.Blocked).length,
    }
  }

  async onFilesPicked(list: File[]): Promise<void> {
    // ★ 先用共享契约本地过滤（36 号：改规则只改 upload-file-policy.ts，页面不写 if/else）
    //   好处：① 立刻反馈，不必等后端往返 ② 系统垃圾文件根本不会出现在 UI 里
    const part = partitionUploadable(list.map((f) => ({ name: f.name })))
    let accepted = list.filter((f) => part.accepted.some((a) => a.name === f.name))
  
    // ★ 替换模式：只接受 1 个文件，且必须**同名**
    //   ⚠️ 后端 UploadFileStepAsync 会硬校验「上传文件名 == 登记名」，不一致直接拒；
    //      在前端先拦一道，用户看到的是「要选同名文件」而不是一个后端错误码。
    const t = this.uploadTarget.value
    if (t.kind === 'replace') {
      const hit = accepted.find((f) => f.name === t.file.FileName)
      if (!hit) {
        this.pendingFiles.value = []
        this.planRows.value = []
        this.planSummary.value = {} as any
        ElMessage.warning(`「替换」要求同名文件：请选择名为「${t.file.FileName}」的文件（改名等于换一个文件）`)
        return
      }
      accepted = [hit]
    }
    this.pendingFiles.value = accepted
  
    if (part.noise.length > 0) {
      ElMessage.info(
        `已自动忽略 ${part.noise.length} 个系统文件（${part.noise.slice(0, 3).map((nf) => nf.name).join('、')}${part.noise.length > 3 ? ' 等' : ''}）`)
    }
  
    if (this.pendingFiles.value.length === 0) {
      this.planRows.value = []
      this.planSummary.value = {} as any
      if (part.rejected.length > 0) {
        ElMessage.warning(part.rejected[0].message + '（压缩包 / 可执行文件 / 网页脚本不支持）')
      }
      return
    }
  
    try {
      const r = await planUpload(this.enterpriseCode.value, this.stageCode.value, this.toItems(this.pendingFiles.value))
      this.planRows.value = r.rows
      this.planSummary.value = r.summary
      if (part.rejected.length > 0) {
        ElMessage.warning(`已跳过 ${part.rejected.length} 个不支持的文件：${part.rejected.slice(0, 3).map((r2) => r2.name).join('、')}`)
      }
    } catch (e) {
      this.reportError(e, '读取文件失败')
    }
  }

  async startUpload(): Promise<void> {
    const all = this.planRows.value
    const blocked = all.filter((r) => r.Blocked)
    const uploadable = all.filter((r) => !r.Blocked)
  
    if (all.length === 0) {
      ElMessage.warning(
        this.planSummary.value?.FilteredCount
          ? `选中的 ${this.planSummary.value.FilteredCount} 个文件都是系统文件（.DS_Store 等），没有可上传的资料`
          : '没有可上传的文件')
      return
    }
    // ⛔ 只有「全部不合规」才中止；有合规的就继续传
    if (uploadable.length === 0) {
      ElMessage.error(`这 ${blocked.length} 个文件都不支持上传：${blocked.slice(0, 3).map((b) => b.FileName).join('、')}`)
      return
    }
    if (blocked.length > 0) {
      ElMessage.warning(`已跳过 ${blocked.length} 个不支持的文件：${blocked.slice(0, 3).map((b) => b.FileName).join('、')}${blocked.length > 3 ? ' 等' : ''}`)
    }
    const replaces = this.planSummary.value?.ReplaceCount ?? 0
    const skips = this.planSummary.value?.SkipCount ?? 0
    if (replaces > 0) {
      const ok = await confirmOrFalse(
        `本次上传 ${this.planRows.value.length} 份，其中 ${replaces} 份会替换旧文件（旧版本会保留），` +
        `${skips} 份内容没变化会跳过。是否继续？`,
        '确认上传', { type: 'warning', confirmButtonText: '继续', cancelButtonText: '取消' })
      if (!ok) return
    }
    this.uploadStage.value = 'uploading'
    const items = this.toItems(this.pendingFiles.value)
    let taskId = ''
    const codeByName = new Map<string, string>()
    try {
      const init = await originalUploadInit(this.enterpriseCode.value, this.stageCode.value, items)
      taskId = init.TaskId
      for (const it of init.Items) if (it.Action !== 'skip') codeByName.set(it.FileName, it.FileCode)
    } catch (e) {
      this.uploadStage.value = 'idle'
      this.reportError(e, '上传失败')
      return
    }
    let done = 0
    for (const f of this.pendingFiles.value) {
      const rel = (f as any).webkitRelativePath || f.name
      const slash = rel.lastIndexOf('/')
      const name = slash >= 0 ? rel.slice(slash + 1) : rel
      const code = codeByName.get(name)
      if (code) {
        this.uploadProgress.value = `正在上传 ${done + 1}/${this.pendingFiles.value.length}：${name}`
        try { await originalUploadFile(taskId, code, f) }
        catch (e) { ElMessage.warning(`${name} 上传失败：${(e as Error).message}`) }
      }
      done++
    }
    try {
      const r: any = await originalUploadConfirm(taskId, this.enterpriseCode.value)
      this.uploadProgress.value = `已完成：${r.ActivatedCount} 份文件已入库，正在识别`
      this.uploadStage.value = 'done'
      await this.loadFiles()
    } catch (e) {
      this.uploadStage.value = 'idle'
      this.reportError(e, '上传失败')
    }
  }

  closeUpload(): void {
    this.uploadVisible.value = false
    this.uploadStage.value = 'idle'
    this.pendingFiles.value = []
    // ★ 目标必须一起清掉 —— 否则下一次点「上传文件」会带着上一次的「替换 xxx」目标
    this.uploadTarget.value = { kind: 'stage' }
  }

  async onVersions(row: OriginalFile): Promise<void> {
    this.versionTarget.value = row
    this.versionVisible.value = true
    try {
      const r = await fetchVersions(row.Code, row.EnterpriseCode)
      this.versions.value = r.Rows
      this.currentVersion.value = r.CurrentVersion
    } catch (e) {
      this.versions.value = []
    }
  }

  async onRestore(v: FileVersion): Promise<void> {
    const row = this.versionTarget.value
    if (!row) return
    const ok = await confirmOrFalse(
      `把「${v.FileName}」恢复为当前版本？当前版本会被保留为历史版本。`,
      '恢复旧版本', { type: 'warning', confirmButtonText: '确认恢复', cancelButtonText: '取消' })
    if (!ok) return
    try {
      await restoreVersion(row.Code, row.EnterpriseCode, v.VersionNumber, '页面手动恢复')
      ElMessage.success('已恢复')
      this.versionVisible.value = false
      await this.loadFiles()
    } catch (e) {
      this.reportError(e, '恢复历史版本失败')
    }
  }

  openPolicy(mode: 'single' | 'batch', row?: OriginalFile): void {
    if (mode === 'batch' && this.selected.value.length === 0) {
      ElMessage.warning('请先勾选文件')
      return
    }
    this.policyTarget.value = mode
    this.policyEditing.value = row ?? null
    this.policyValue.value = 'analyze'
    this.policyReason.value = ''
    this.policyVisible.value = true
  }

  async submitPolicy(): Promise<void> {
    const rows = this.policyRows.value
    if (rows.length === 0) return
    try {
      if (rows.length === 1) {
        await setPolicy(rows[0].Code, rows[0].EnterpriseCode, this.policyValue.value, (this.policyReason.value || null) as any, true)
        ElMessage.success('已保存')
      } else {
        const r = await batchSetPolicy(rows.map((x) => x.Code), rows[0].EnterpriseCode,
          this.policyValue.value, (this.policyReason.value || null) as any, true)
        ElMessage[r.FailedCount > 0 ? 'warning' : 'success'](`成功 ${r.SuccessCount} / 失败 ${r.FailedCount}`)
      }
      this.policyVisible.value = false
      await this.loadFiles()
    } catch (e) {
      this.reportError(e, '保存提取策略失败')
    }
  }

  async deleteFile(row: OriginalFile): Promise<void> {
    const ok = await confirmOrFalse(
      `删除「${row.FileName}」？文件与历史版本会一并移除；识别结果（标签/作用）会保留以备审计。`,
      '删除确认', { type: 'warning', confirmButtonText: '确认删除', cancelButtonText: '取消' })
    if (!ok) return
    try {
      await removeFile(row.Code, row.EnterpriseCode, '页面删除')
      ElMessage.success('已删除')
      await this.loadFiles()
    } catch (e) {
      this.reportError(e, '删除文件失败')
    }
  }

  async openQueueDetail(): Promise<void> {
    if (!this.enterpriseCode.value || !this.stageCode.value) return
    this.queueVisible.value = true
    this.queueLoading.value = true
    try {
      this.queueDetail.value = await fetchQueueDetail(this.enterpriseCode.value, this.stageCode.value)
    } catch (e) {
      this.queueDetail.value = null
    } finally {
      this.queueLoading.value = false
    }
  }

}
