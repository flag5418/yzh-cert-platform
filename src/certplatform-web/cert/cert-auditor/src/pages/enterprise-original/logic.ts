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
import { confirmOrFalse } from '@yzh-core'
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { AnalyzePolicyKey, DocProfile, OriginalFile, QueueDetail, StatusBar } from '@share/api/ent/enterprise-original'
import { partitionUploadable } from '@share/constants/upload-file-policy'
import {
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

// ═══════════════════════ 一、左右树 + 作用域 ═══════════════════════

export const treeNodes = ref<any[]>([])
export const treeHint = ref('')
export const enterpriseCode = ref('')
export const stageCode = ref('')
export const scopeLabel = ref('')
export const scopeLoading = ref(false)

export async function loadTree(): Promise<void> {
  try {
    const nodes = await fetchStageTree()
    treeNodes.value = nodes.map((n: any) => ({
      Code: n.Code,
      Label: n.Label,
      FileCount: n.FileCount,
      EnterpriseCode: n.EnterpriseCode,
      Children: (n.Children ?? []).map((c: any) => ({
        ...c,
        EnterpriseCode: c.EnterpriseCode,
        Children: undefined,
      })),
    }))
    treeHint.value = nodes.length === 0 ? '当前工作区下没有企业' : ''
  } catch (e) {
    treeHint.value = (e as Error).message
  }
}

/** 点树节点：企业（有子节点）只提示，阶段（叶子）才加载 */
export async function onNodeClick(node: any): Promise<void> {
  const isLeaf = !node?.Children || node.Children.length === 0
  if (!isLeaf) {
    scopeLabel.value = `${node.Label}（请选择具体阶段）`
    stageCode.value = ''
    files.value = []
    statusBar.value = null
    return
  }
  const entCode = node.EnterpriseCode ?? node.Code
  if (!entCode || !node.Code) {
    ElMessage.warning('树节点缺少企业/阶段标识，请刷新后重试')
    return
  }
  enterpriseCode.value = entCode
  stageCode.value = node.Code
  scopeLabel.value = node.Label
  await loadFiles()
  startPolling()      // ★ 队列在跑时自动刷新进度
}

// ═══════════════════════ 二、文件列表（卡片）═══════════════════════

export const files = ref<OriginalFile[]>([])
export const statusBar = ref<StatusBar | null>(null)
export const selected = ref<OriginalFile[]>([])
export const filterTags = ref<string[]>([])
export const onlyUsable = ref(false)
export const tagOptions = ref<Array<{ TagCode: string; TagName: string; TagGroup?: string | null }>>([])

/**
 * ★ 忙碌态：该阶段有队列排队中/执行中。
 * ⇒ 上传按钮**禁用**；页面自动轮询刷新进度（用户要求「显示进度的详情，否则会造成误判」）。
 */
export const isBusy = computed(() => statusBar.value?.IsBusy === true)

/** 队列进度（0-100） */
export const queueProgress = computed(() => {
  const sb = statusBar.value
  if (!sb) return 0
  if (typeof sb.QueueProgress === 'number' && sb.QueueProgress > 0) return sb.QueueProgress
  if ((sb.QueueTotal ?? 0) > 0) return Math.round(((sb.QueueCompleted ?? 0) / (sb.QueueTotal ?? 1)) * 100)
  return 0
})

/** 队列正在做什么（专家语言） */
export const queueLabel = computed(() => {
  const t = statusBar.value?.QueueType
  return t === 'enterprise_original_analyze' ? '正在识别资料内容' : '正在读取文件内容'
})

let pollTimer: any = null

/**
 * ★ 有队列在跑时自动轮询（3s），空闲时停。
 * <para>为什么必须有：一次几十份文件的语义分析要跑一两分钟，没有轮询用户只能手动点刷新，
 * 看不到进度 ⇒ 误判成「没触发队列」（2026-10-03 实测投诉）。</para>
 */
export function startPolling(): void {
  stopPolling()
  pollTimer = setInterval(async () => {
    if (!enterpriseCode.value || !stageCode.value) return
    try {
      const sb = await fetchStatusBar(enterpriseCode.value, stageCode.value)
      const wasBusy = statusBar.value?.IsBusy === true
      statusBar.value = sb
      // 队列刚跑完 ⇒ 拉一次文件列表（标签/作用此时才有值）
      if (wasBusy && sb?.IsBusy !== true) {
        await loadFiles()
        ElMessage.success('资料处理完成')
      } else if (sb?.IsBusy) {
        await loadFiles()
      }
    } catch { /* 轮询失败静默，不打断用户 */ }
  }, 3000)
}

export function stopPolling(): void {
  if (pollTimer) { clearInterval(pollTimer); pollTimer = null }
}

export const tagNameMap = computed<Record<string, string>>(() =>
  Object.fromEntries(tagOptions.value.map((t) => [t.TagCode, t.TagName])))

export const tableRef = ref<any>(null)

/**
 * 数据修订号：每次 loadFiles 自增。
 * ★ 用途：`YzhTable` 只在挂载时跑 dataLoader，而本模块的 dataLoader 是**内存版**
 *   （读 files.value）⇒ 数据变了必须靠换 :key 重挂载才会重取。资料库页面用同一手法
 *   （`resources/logic.ts` 的 dataRevision）。
 */
export const dataRevision = ref(0)

export async function loadFiles(): Promise<void> {
  if (!enterpriseCode.value || !stageCode.value) return
  scopeLoading.value = true
  try {
    const r = await fetchFilesFiltered(enterpriseCode.value, stageCode.value, filterTags.value, onlyUsable.value, false)
    files.value = r.rows
    const t = buildTree(r.rows)
    rootFiles.value = t.root
    folderNodes.value = t.nodes
    statusBar.value = await fetchStatusBar(enterpriseCode.value, stageCode.value)
    dataRevision.value++
  } catch (e) {
    ElMessage.error((e as Error).message)
  } finally {
    scopeLoading.value = false
  }
}

export async function loadTags(): Promise<void> {
  if (tagOptions.value.length > 0) return
  try {
    tagOptions.value = await fetchTags('enterprise')
  } catch (e) {
    ElMessage.warning(`标签清单加载失败：${(e as Error).message}`)
  }
}

export async function toggleFilterTag(code: string): Promise<void> {
  const i = filterTags.value.indexOf(code)
  if (i >= 0) filterTags.value.splice(i, 1)
  else filterTags.value.push(code)
  await loadFiles()
}

export async function clearFilters(): Promise<void> {
  filterTags.value = []
  onlyUsable.value = false
  await loadFiles()
}

export async function refresh(): Promise<void> {
  await loadTree()
  await loadFiles()
}

// ═══════════════════════ 三、★ 术语去技术化（专家语言）══════════════

/** 文件状态：技术态 → 专家看得懂的文案 */
export const FILE_STATUS_TEXT: Record<string, string> = {
  done: '已就绪',
  processing: '处理中',
  failed: '处理失败',
  manual: '需人工处理',
  skipped: '已跳过',
  pending: '等待处理',
}

/** 文件状态：技术态 → 徽标色 */
export const FILE_STATUS_TAG: Record<string, string> = {
  done: 'success',
  processing: 'warning',
  failed: 'danger',
  manual: 'warning',
  skipped: 'info',
  pending: 'info',
}

/**
 * ★ 把技术三态（转换 / Markdown / 分析）压成**一个**专家能懂的状态。
 *
 * <para>为什么要压：专家不该看到「转换 completed ∧ Markdown completed ∧ 分析 analyzed」这种
 * 内部状态机。它在 UI 上只该表现为一句话：
 * <list type="bullet">
 *   <item><b>已就绪</b>：内容能看、标签和作用都有 ⇒ 可以往下走</item>
 *   <item><b>处理中</b>：正在识别，等一下</item>
 *   <item><b>需人工处理</b>：这个格式系统读不了（扫描件 / 图片），需要人工填写</item>
 *   <item><b>处理失败</b>：出错了，可重试</item>
 * </list>
 * </para>
 */
export function fileStatus(row: OriginalFile): { key: string; text: string; type: string; hint?: string } {
  if (row.IsUsableForFilling) return { key: 'done', text: FILE_STATUS_TEXT.done, type: FILE_STATUS_TAG.done }

  const cv = row.ConvertStatus
  const md = row.MarkdownStatus
  const an = row.AnalyzeStatus

  if (cv === 'converting' || md === 'converting' || an === 'analyzing')
    return { key: 'processing', text: FILE_STATUS_TEXT.processing, type: FILE_STATUS_TAG.processing }
  if (cv === 'unsupported' || md === 'unsupported')
    return { key: 'manual', text: FILE_STATUS_TEXT.manual, type: FILE_STATUS_TAG.manual, hint: '这个格式系统自动读不了，需要人工填写下面的标签和作用' }
  if (cv === 'failed' || md === 'failed' || an === 'failed')
    return { key: 'failed', text: FILE_STATUS_TEXT.failed, type: FILE_STATUS_TAG.failed, hint: row.AnalyzeMessage || row.ConvertMessage || row.MarkdownMessage || undefined }
  if (an === 'skipped')
    return { key: 'skipped', text: FILE_STATUS_TEXT.skipped, type: FILE_STATUS_TAG.skipped, hint: '这份文件已设置为不参与识别' }
  if (cv === 'pending' || md === 'pending' || an === 'pending')
    return { key: 'pending', text: FILE_STATUS_TEXT.pending, type: FILE_STATUS_TAG.pending }
  return { key: 'manual', text: FILE_STATUS_TEXT.manual, type: FILE_STATUS_TAG.manual, hint: row.UnusableReason || undefined }
}

/** 是否处于「已就绪」（可预览全文 + 有标签/作用） */
export function canPreview(row: OriginalFile): boolean {
  return row.ConvertStatus === 'completed' || !!row.PreviewPdfPath || !!row.StoragePath
}

/** 文件名（去掉扩展名）—— 用作标题更易读 */
export function displayName(row: OriginalFile): string {
  const n = row.FileName || ''
  const i = n.lastIndexOf('.')
  return i > 0 ? n.slice(0, i) : n
}


// ═══════════════════════ 三·b、★ 文件夹树（照抄资料库 StandardFolderTree 结构）═══

/**
 * ★ 把扁平的 `RelFolderPath` 列表构建成**文件夹树**。
 *
 * <para>为什么必须有树：企业交上来的资料是<b>有层级</b>的（`4记录文件/技术类/…`），
 * 而且文件很多 —— 摊平成一张长表或卡片列表都看不清「哪些文件属于哪个目录」。
 * 老板 2026-10-03 明确要求「按资料库一样的显示方式，文件夹下套文件」。</para>
 *
 * <para>根目录（<c>RelFolderPath=''</c>）用 <b>空 Path</b> 表示，由页面单独渲染成「根目录」区，
 * 不混进树里 —— 否则会出现一个名字为空的文件夹节点。</para>
 */
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

/** 根目录（阶段根下直接放的文件） */
export const rootFiles = ref<OriginalFile[]>([])
/** 文件夹树（一级节点数组） */
export const folderNodes = ref<OriginalFolderNode[]>([])

function buildTree(files: OriginalFile[]): {
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

/** ★ 详情抽屉（老板要的「点详情 → 侧边操作」） */
export const detailVisible = ref(false)
export const detailRow = ref<OriginalFile | null>(null)
export const detailProfile = ref<DocProfile | null>(null)
export const detailLoading = ref(false)
export const detailPurpose = ref<Record<string, string>>({})

export async function openDetail(row: OriginalFile): Promise<void> {
  detailRow.value = row
  detailVisible.value = true
  detailLoading.value = true
  detailProfile.value = null
  detailPurpose.value = {}
  try {
    const p = await fetchProfile(row.Code, row.EnterpriseCode)
    detailProfile.value = p
    detailPurpose.value = splitPurpose(p?.DocPurpose ?? row.DocPurpose ?? '')
  } catch {
    detailPurpose.value = splitPurpose(row.DocPurpose ?? '')
  } finally {
    detailLoading.value = false
  }
}

export function closeDetail(): void {
  detailVisible.value = false
  detailRow.value = null
}

/** 详情里保存标签 + 作用（一次提交，两者都带上） */
export async function saveDetail(): Promise<void> {
  const row = detailRow.value
  if (!row) return
  purposeSaving.value = true
  try {
    await correctProfile({
      FileCode: row.Code,
      EnterpriseCode: row.EnterpriseCode,
      TagsJson: JSON.stringify(tagDraft.value),
      DocPurpose: PURPOSE_SEGMENTS.map((seg) => `${seg}${detailPurpose.value[seg] ?? ''}`).join('\n'),
      InfoItemsJson: detailProfile.value?.InfoItemsJson ?? undefined,
    })
    ElMessage.success('已保存')
    detailVisible.value = false
    await loadFiles()
  } catch (e) {
    ElMessage.error((e as Error).message)
  } finally {
    purposeSaving.value = false
  }
}

/** 打开详情时把当前标签同步进编辑草稿 */
export function initDetailDraft(): void {
  tagDraft.value = [...(detailRow.value?.Tags ?? [])]
}

// ═══════════════════════ 七·b、忽略 / 恢复提取（一键）═══════════

export async function quickIgnore(row: OriginalFile): Promise<void> {
  try {
    await setPolicy(row.Code, row.EnterpriseCode, 'ignore', 'manual', true)
    ElMessage.success('已忽略，不再参与提取（文件仍作为证据保留）')
    await loadFiles()
  } catch (e) {
    ElMessage.error((e as Error).message)
  }
}

export async function quickParticipate(row: OriginalFile): Promise<void> {
  try {
    await setPolicy(row.Code, row.EnterpriseCode, 'analyze', null, true)
    ElMessage.success('已恢复提取')
    await loadFiles()
  } catch (e) {
    ElMessage.error((e as Error).message)
  }
}

// ═══════════════════════ 三·c、★ 内容查看 / 重新生成 ═══════════════════════

/** ★「查看提取内容」抽屉：让专家在页面内直接看到 AI 提取出的 Markdown 原文 */
export const contentVisible = ref(false)
export const contentRow = ref<OriginalFile | null>(null)
export const contentText = ref('')
export const contentLoading = ref(false)

export async function openContent(row: OriginalFile): Promise<void> {
  contentRow.value = row
  contentVisible.value = true
  contentLoading.value = true
  contentText.value = ''
  try {
    const r = await fetchMarkdownContent(row.Code, row.EnterpriseCode)
    if (r?.Markdown) contentText.value = r.Markdown
    else ElMessage.warning(r?.Message ?? '尚未生成内容')
  } catch (e) {
    ElMessage.warning(`读取内容失败：(e as Error).message`.replace('(e as Error).message', (e as Error).message))
  } finally {
    contentLoading.value = false
  }
}

export function closeContent(): void {
  contentVisible.value = false
  contentRow.value = null
  contentText.value = ''
}

/** ★ 重新生成：重跑转换链（此前只能重新上传整个文件夹） */
export const regenerating = ref('')

export async function onRegenerate(row: OriginalFile, reanalyze = true): Promise<void> {
  regenerating.value = row.Code
  try {
    const r = await regenerate(row.Code, row.EnterpriseCode, reanalyze)
    ElMessage.success(r?.Message ?? '已重新入队')
    await loadFiles()
  } catch (e) {
    ElMessage.error((e as Error).message)
  } finally {
    regenerating.value = ''
  }
}

// ═══════════════════════ 四、★ 预览（专家直接看文件）══════════════

export const previewVisible = ref(false)
export const previewRow = ref<OriginalFile | null>(null)
export const previewUrl = ref('')
export const previewKind = ref<'pdf' | 'image' | 'text' | 'none'>('none')
export const previewText = ref('')
export const previewLoading = ref(false)

/** 按扩展名判定预览方式：图片直出、PDF 内嵌、文本读内容、其余转 PDF 后内嵌 */
export function previewKindOf(row: OriginalFile): 'pdf' | 'image' | 'text' | 'none' {
  const t = (row.FileType || '').toLowerCase()
  if (['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp'].includes(t)) return 'image'
  if (t === '.pdf') return 'pdf'
  if (['.txt', '.md', '.csv'].includes(t)) return 'text'
  // Word/Excel/PPT 一律走服务端转出的 PDF
  return 'pdf'
}

export async function openPreview(row: OriginalFile): Promise<void> {
  previewRow.value = row
  previewVisible.value = true
  previewLoading.value = true
  previewText.value = ''
  previewUrl.value = ''
  try {
    if (previewKindOf(row) === 'image') {
      const blob = await originalPreviewBlob(row.Code, row.EnterpriseCode)
      previewKind.value = 'image'
      previewUrl.value = URL.createObjectURL(blob)
    } else if (previewKindOf(row) === 'text') {
      const blob = await originalPreviewBlob(row.Code, row.EnterpriseCode)
      previewKind.value = 'text'
      previewText.value = await blob.text()
    } else {
      const blob = await originalPreviewBlob(row.Code, row.EnterpriseCode)
      previewKind.value = 'pdf'
      previewUrl.value = URL.createObjectURL(blob)
    }
  } catch (e) {
    previewKind.value = 'none'
    ElMessage.warning(`预览失败：${(e as Error).message}`)
  } finally {
    previewLoading.value = false
  }
}

export function closePreview(): void {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = ''
  previewRow.value = null
}

// ═══════════════════════ 五、★ 改标签（卡片内联，受控下拉）══════════

export const tagEditing = ref<OriginalFile | null>(null)
export const tagDraft = ref<string[]>([])
export const tagSaving = ref(false)

export function openTagEditor(row: OriginalFile): void {
  tagEditing.value = row
  tagDraft.value = [...(row.Tags ?? [])]
}

export async function saveTags(): Promise<void> {
  const row = tagEditing.value
  if (!row) return
  tagSaving.value = true
  try {
    const p = await fetchProfile(row.Code, row.EnterpriseCode)
    await correctProfile({
      FileCode: row.Code,
      EnterpriseCode: row.EnterpriseCode,
      TagsJson: JSON.stringify(tagDraft.value),
      DocPurpose: p?.DocPurpose ?? undefined,
      InfoItemsJson: p?.InfoItemsJson ?? undefined,
    })
    ElMessage.success('标签已保存')
    tagEditing.value = null
    await loadFiles()
  } catch (e) {
    ElMessage.error((e as Error).message)
  } finally {
    tagSaving.value = false
  }
}

// ═══════════════════════ 六、★ 改作用（四段式，卡片内联）══════════

export const PURPOSE_SEGMENTS = ['【是什么】', '【审核关注点】', '【来源口径】', '【包含信息】']

export const purposeEditing = ref<OriginalFile | null>(null)
export const purposeDraft = ref<Record<string, string>>({})
export const purposeSaving = ref(false)

/** 把后端的四段文本拆成 4 个可独立编辑的字段 */
export function splitPurpose(text?: string | null): Record<string, string> {
  const map: Record<string, string> = {}
  for (const seg of PURPOSE_SEGMENTS) map[seg] = ''
  if (!text) return map
  for (const line of text.split('\n')) {
    const t = line.trim()
    const seg = PURPOSE_SEGMENTS.find((s) => t.startsWith(s))
    if (seg) map[seg] = t.slice(seg.length).trim()
  }
  return map
}

/** 卡片上显示的作用摘要（取第一段「是什么」，没有就显示第一行） */
export function purposeBrief(row: OriginalFile): string {
  const p = row.DocPurpose || ''
  if (!p) return ''
  const first = p.split('\n').map((s) => s.trim()).find(Boolean) || ''
  return first.replace(/^【[^】]*】/, '').slice(0, 60)
}

export async function openPurposeEditor(row: OriginalFile): Promise<void> {
  purposeEditing.value = row
  purposeSaving.value = false
  try {
    const p = await fetchProfile(row.Code, row.EnterpriseCode)
    purposeDraft.value = splitPurpose(p?.DocPurpose ?? row.DocPurpose ?? '')
  } catch {
    purposeDraft.value = splitPurpose(row.DocPurpose ?? '')
  }
}

export async function savePurpose(): Promise<void> {
  const row = purposeEditing.value
  if (!row) return
  purposeSaving.value = true
  try {
    const p = await fetchProfile(row.Code, row.EnterpriseCode)
    await correctProfile({
      FileCode: row.Code,
      EnterpriseCode: row.EnterpriseCode,
      DocPurpose: PURPOSE_SEGMENTS.map((s) => `${s}${purposeDraft.value[s] ?? ''}`).join('\n'),
      TagsJson: p?.TagsJson ?? JSON.stringify(row.Tags ?? []),
      InfoItemsJson: p?.InfoItemsJson ?? undefined,
    })
    ElMessage.success('作用已保存')
    purposeEditing.value = null
    await loadFiles()
  } catch (e) {
    ElMessage.error((e as Error).message)
  } finally {
    purposeSaving.value = false
  }
}

// ═══════════════════════ 七、上传（保留，不改交互）════════════════════

export const uploadVisible = ref(false)
export const uploadStage = ref<'idle' | 'uploading' | 'done'>('idle')
export const planRows = ref<PlanRow[]>([])
export const planSummary = ref<any>({})
export const uploadProgress = ref('')
export const pendingFiles = ref<File[]>([])

export function openUpload(): void {
  if (!enterpriseCode.value || !stageCode.value) {
    ElMessage.warning('请先在左侧选择「企业 › 阶段」')
    return
  }
  uploadStage.value = 'idle'
  planRows.value = []
  planSummary.value = {}
  pendingFiles.value = []
  uploadProgress.value = ''
  uploadVisible.value = true
}

function toItems(files: File[]): PlanItem[] {
  return files.map((f) => {
    const rel = (f as any).webkitRelativePath || f.name
    const slash = rel.lastIndexOf('/')
    return {
      FileName: slash >= 0 ? rel.slice(slash + 1) : rel,
      RelFolderPath: slash > 0 ? rel.slice(0, slash) : '',
      FileSize: f.size,
    }
  })
}

/**
 * 从 plan 清单里移除一行（只影响本次上传的清单，⛔ 不改已选中的原始 File 列表）。
 * ★ 解决用户报障：「不支持的文件在弹窗里删不掉，只能干等」。
 */
export function removePlanRow(index: number): void {
  if (index < 0 || index >= planRows.value.length) return
  planRows.value.splice(index, 1)
  const sum = planSummary.value ?? {}
  planSummary.value = {
    ...sum,
    Total: planRows.value.length,
    BlockedCount: planRows.value.filter((r) => r.Blocked).length,
  }
}

export async function onFilesPicked(list: File[]): Promise<void> {
  // ★ 先用共享契约本地过滤（36 号：改规则只改 upload-file-policy.ts，页面不写 if/else）
  //   好处：① 立刻反馈，不必等后端往返 ② 系统垃圾文件根本不会出现在 UI 里
  const part = partitionUploadable(list.map((f) => ({ name: f.name })))
  pendingFiles.value = list.filter((f) => part.accepted.some((a) => a.name === f.name))

  if (part.noise.length > 0) {
    ElMessage.info(
      `已自动忽略 ${part.noise.length} 个系统文件（${part.noise.slice(0, 3).map((nf) => nf.name).join('、')}${part.noise.length > 3 ? ' 等' : ''}）`)
  }

  if (pendingFiles.value.length === 0) {
    planRows.value = []
    planSummary.value = {} as any
    if (part.rejected.length > 0) {
      ElMessage.warning(part.rejected[0].message + '（压缩包 / 可执行文件 / 网页脚本不支持）')
    }
    return
  }

  try {
    const r = await planUpload(enterpriseCode.value, stageCode.value, toItems(pendingFiles.value))
    planRows.value = r.rows
    planSummary.value = r.summary
    if (part.rejected.length > 0) {
      ElMessage.warning(`已跳过 ${part.rejected.length} 个不支持的文件：${part.rejected.slice(0, 3).map((r2) => r2.name).join('、')}`)
    }
  } catch (e) {
    ElMessage.error((e as Error).message)
  }
}

/**
 * 开始上传。
 *
 * ★ 2026-10-03 修掉的卡死：原逻辑是「**只要有 1 个不合规文件就整体 return**」，
 *   而 macOS 选文件夹上传时每个目录都带 `.DS_Store` ⇒ 用户点了上传**毫无反应**。
 *   现改为：**不合规的跳过，合规的照常上传**，只有「一个都不合规」才中止。
 */
export async function startUpload(): Promise<void> {
  const all = planRows.value
  const blocked = all.filter((r) => r.Blocked)
  const uploadable = all.filter((r) => !r.Blocked)

  if (all.length === 0) {
    ElMessage.warning(
      planSummary.value?.FilteredCount
        ? `选中的 ${planSummary.value.FilteredCount} 个文件都是系统文件（.DS_Store 等），没有可上传的资料`
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
  const replaces = planSummary.value?.ReplaceCount ?? 0
  const skips = planSummary.value?.SkipCount ?? 0
  if (replaces > 0) {
    const ok = await confirmOrFalse(
      `本次上传 ${planRows.value.length} 份，其中 ${replaces} 份会替换旧文件（旧版本会保留），` +
      `${skips} 份内容没变化会跳过。是否继续？`,
      '确认上传', { type: 'warning', confirmButtonText: '继续', cancelButtonText: '取消' })
    if (!ok) return
  }
  uploadStage.value = 'uploading'
  const items = toItems(pendingFiles.value)
  let taskId = ''
  const codeByName = new Map<string, string>()
  try {
    const init = await originalUploadInit(enterpriseCode.value, stageCode.value, items)
    taskId = init.TaskId
    for (const it of init.Items) if (it.Action !== 'skip') codeByName.set(it.FileName, it.FileCode)
  } catch (e) {
    uploadStage.value = 'idle'
    ElMessage.error((e as Error).message)
    return
  }
  let done = 0
  for (const f of pendingFiles.value) {
    const rel = (f as any).webkitRelativePath || f.name
    const slash = rel.lastIndexOf('/')
    const name = slash >= 0 ? rel.slice(slash + 1) : rel
    const code = codeByName.get(name)
    if (code) {
      uploadProgress.value = `正在上传 ${done + 1}/${pendingFiles.value.length}：${name}`
      try { await originalUploadFile(taskId, code, f) }
      catch (e) { ElMessage.warning(`${name} 上传失败：${(e as Error).message}`) }
    }
    done++
  }
  try {
    const r: any = await originalUploadConfirm(taskId, enterpriseCode.value)
    uploadProgress.value = `已完成：${r.ActivatedCount} 份文件已入库，正在识别`
    uploadStage.value = 'done'
    await loadFiles()
  } catch (e) {
    uploadStage.value = 'idle'
    ElMessage.error((e as Error).message)
  }
}

export function closeUpload(): void {
  uploadVisible.value = false
  uploadStage.value = 'idle'
  pendingFiles.value = []
}

// ═══════════════════════ 八、其他操作 ═══════════════════════

export async function onDownload(row: OriginalFile): Promise<void> {
  try {
    const blob = await originalDownloadBlob(row.StoragePath, row.EnterpriseCode)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = row.FileName
    document.body.appendChild(a)
    a.click()
    a.remove()
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  } catch (e) {
    ElMessage.error((e as Error).message)
  }
}

export const versionVisible = ref(false)
export const versions = ref<FileVersion[]>([])
export const currentVersion = ref(1)
export const versionTarget = ref<OriginalFile | null>(null)

export async function onVersions(row: OriginalFile): Promise<void> {
  versionTarget.value = row
  versionVisible.value = true
  try {
    const r = await fetchVersions(row.Code, row.EnterpriseCode)
    versions.value = r.Rows
    currentVersion.value = r.CurrentVersion
  } catch (e) {
    versions.value = []
  }
}

export async function onRestore(v: FileVersion): Promise<void> {
  const row = versionTarget.value
  if (!row) return
  const ok = await confirmOrFalse(
    `把「${v.FileName}」恢复为当前版本？当前版本会被保留为历史版本。`,
    '恢复旧版本', { type: 'warning', confirmButtonText: '确认恢复', cancelButtonText: '取消' })
  if (!ok) return
  try {
    await restoreVersion(row.Code, row.EnterpriseCode, v.VersionNumber, '页面手动恢复')
    ElMessage.success('已恢复')
    versionVisible.value = false
    await loadFiles()
  } catch (e) {
    ElMessage.error((e as Error).message)
  }
}

export const policyVisible = ref(false)
export const policyTarget = ref<'single' | 'batch'>('single')
export const policyValue = ref<AnalyzePolicyKey>('analyze')
export const policyReason = ref<string>('')
export const policyRows = computed<OriginalFile[]>(() =>
  policyTarget.value === 'batch' ? selected.value : (policyEditing.value ? [policyEditing.value] : []))

export const policyEditing = ref<OriginalFile | null>(null)

export function openPolicy(mode: 'single' | 'batch', row?: OriginalFile): void {
  if (mode === 'batch' && selected.value.length === 0) {
    ElMessage.warning('请先勾选文件')
    return
  }
  policyTarget.value = mode
  policyEditing.value = row ?? null
  policyValue.value = 'analyze'
  policyReason.value = ''
  policyVisible.value = true
}

export async function submitPolicy(): Promise<void> {
  const rows = policyRows.value
  if (rows.length === 0) return
  try {
    if (rows.length === 1) {
      await setPolicy(rows[0].Code, rows[0].EnterpriseCode, policyValue.value, (policyReason.value || null) as any, true)
      ElMessage.success('已保存')
    } else {
      const r = await batchSetPolicy(rows.map((x) => x.Code), rows[0].EnterpriseCode,
        policyValue.value, (policyReason.value || null) as any, true)
      ElMessage[r.FailedCount > 0 ? 'warning' : 'success'](`成功 ${r.SuccessCount} / 失败 ${r.FailedCount}`)
    }
    policyVisible.value = false
    await loadFiles()
  } catch (e) {
    ElMessage.error((e as Error).message)
  }
}

export async function onDelete(row: OriginalFile): Promise<void> {
  const ok = await confirmOrFalse(
    `删除「${row.FileName}」？文件与历史版本会一并移除；识别结果（标签/作用）会保留以备审计。`,
    '删除确认', { type: 'warning', confirmButtonText: '确认删除', cancelButtonText: '取消' })
  if (!ok) return
  try {
    await removeFile(row.Code, row.EnterpriseCode, '页面删除')
    ElMessage.success('已删除')
    await loadFiles()
  } catch (e) {
    ElMessage.error((e as Error).message)
  }
}

// ═══════════════════════ 九、队列（折叠进「处理进度」）════════════════

export const queueVisible = ref(false)
export const queueDetail = ref<QueueDetail | null>(null)
export const queueLoading = ref(false)

export async function openQueueDetail(): Promise<void> {
  if (!enterpriseCode.value || !stageCode.value) return
  queueVisible.value = true
  queueLoading.value = true
  try {
    queueDetail.value = await fetchQueueDetail(enterpriseCode.value, stageCode.value)
  } catch (e) {
    queueDetail.value = null
  } finally {
    queueLoading.value = false
  }
}
