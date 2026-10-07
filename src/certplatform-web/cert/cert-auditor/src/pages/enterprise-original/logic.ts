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
import type { AnalyzePolicyKey, DocProfile, OriginalFile, QueueDetail, StandardRef, StatusBar } from '@share/api/ent/enterprise-original'
import { partitionUploadable } from '@share/constants/upload-file-policy'
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

// ═══════════════════════ 一、左右树 + 作用域 ═══════════════════════

export const treeNodes = ref<any[]>([])
export const treeHint = ref('')
export const enterpriseCode = ref('')
export const stageCode = ref('')
export const scopeLabel = ref('')
export const scopeLoading = ref(false)

/**
 * ★ **当前阶段绑定的标准清单**（2026-10-07 接入）。
 *
 * <para>来源：`cert_enterprise_stage`（一行 = 一个「企业 × 阶段 × 标准」）
 * ⇒ `cert_iso_standard`，由 `stage-tree` 随阶段节点下发。</para>
 *
 * <para><b>为什么必须有</b>：原始资料表 <c>cert_enterprise_original_file</c> <b>没有标准列</b>
 * （只有 <c>StageCode</c>），标准维度只存在于「关联表」与「画像表」。
 * 一个文件在 N 个标准下各有<b>一行</b>画像 ⇒ 前端不把标准传下去，后端只能取「某一行」
 * ⇒ <b>同一份文件在 A 标准下显示 B 标准的标签与作用</b>（用户点名痛点：
 * 「点击分组的时候，显示的信息不正确」）。</para>
 */
export const standards = ref<StandardRef[]>([])

/** 当前选中的标准 Code —— 传回 `list` / `profile` / `profile/correct` 三个端点 */
export const activeStandard = ref('')

/** 当前标准的中文名（页头、抽屉里显示「标准：xxx」；⛔ 不给用户看 GUID） */
export const activeStandardName = computed(() => {
  const hit = standards.value.find((s) => s.Code === activeStandard.value)
  return hit ? (hit.StandardName || hit.StandardCode) : ''
})

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
        // ★ 标准清单随阶段节点透传（`...c` 已带上，这里显式写出以防后人误删）
        Standards: c.Standards ?? [],
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
    standards.value = []
    activeStandard.value = ''
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
  // ★ 标准 tab：该阶段绑定的标准清单（多标准 ⇒ 出 tab；单标准 ⇒ 只显示标准名）
  standards.value = (node.Standards ?? []) as StandardRef[]
  activeStandard.value = standards.value[0]?.Code ?? ''
  // ⚠️ 换阶段必须清标签筛选：标签是**标准相关**的，上一个标准的 TagCode 在新标准下
  //    可能根本不存在 ⇒ 筛出 0 命中，页面看着像「文件丢了」（2026-10-06 同类投诉）。
  filterTags.value = []
  await loadFiles()
  startPolling()      // ★ 队列在跑时自动刷新进度
}

/**
 * 切换标准 tab。
 *
 * ★ 切换后必须**重取列表**：标签与作用都来自「该标准那一行画像」。
 * ⛔ 不清 `filterTags` 会留下上一个标准的标签 ⇒ 新标准下筛不到任何文件。
 */
export async function onStandardChange(code: string): Promise<void> {
  if (!code || code === activeStandard.value) return
  activeStandard.value = code
  filterTags.value = []
  await loadFiles()
}

// ═══════════════════════ 二、文件列表（卡片）═══════════════════════

export const files = ref<OriginalFile[]>([])
export const statusBar = ref<StatusBar | null>(null)
export const selected = ref<OriginalFile[]>([])
export const filterTags = ref<string[]>([])
export const onlyUsable = ref(false)
export const tagOptions = ref<Array<{ TagCode: string; TagName: string; TagGroup?: string | null }>>([])

/**
 * ★ 忙碌态：该阶段有队列排队中/执行中（2026-10-06 文件级重构后可能同时有多个）。
 * ⇒ 显示进度横幅 + 页面自动轮询刷新进度（用户要求「显示进度的详情，否则会造成误判」）。
 * ⛔ **不再禁用上传按钮**：一个文件一个队列互不冲突，阶段级拒绝是 2026-10-03 的旧口径；
 * 同文件重传由后端「取消旧队列重建」兜底。
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

/**
 * 队列类型 → 专家语言（⛔ 不暴露 task_type 字面量）。
 * `enterprise_original_file` = 当前唯一写入口（转换+识别串行）；后两个是历史批次队列。
 */
export function describeQueueType(t?: string | null): string {
  if (t === 'enterprise_original_file') return '处理资料内容'
  if (t === 'enterprise_original_analyze') return '识别资料内容'
  return '读取文件内容'
}

/** 队列正在做什么（专家语言，横幅用：「正在…」） */
export const queueLabel = computed(() => `正在${describeQueueType(statusBar.value?.QueueType)}`)

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

/**
 * ★ 是否处于「筛选态」（标签筛选 / 只看已就绪）。
 *
 * <para><b>为什么必须有这个 computed</b>：`list` 端点的 `Total` 是<b>过滤前</b>的行数
 * （`Total = rows.Count`），而 `Rows` 是<b>过滤后</b>的行数 —— 两者口径不同。
 * 于是筛选命中 0 行时，页头仍显示「共 101 个文件」，列表却空 ⇒ 页面<b>自相矛盾</b>，
 * 用户观感就是「<b>明明有资料，点一下却说没有</b>」（2026-10-06 实测投诉）。</para>
 *
 * <para>⇒ 页面必须区分「这个阶段真的没有文件」与「筛选没命中」，两者文案与动作都不同。</para>
 */
export const filterActive = computed(() => filterTags.value.length > 0 || onlyUsable.value)

/** 当前筛选条件的「人话」摘要（供空态说明，⛔ 不给用户看 TagCode） */
export const filterSummary = computed(() => {
  const parts: string[] = []
  if (filterTags.value.length)
    parts.push(filterTags.value.map((c) => tagNameMap.value[c] ?? c).join('、'))
  if (onlyUsable.value) parts.push('只看已就绪的')
  return parts.join(' ＋ ')
})

/** 本阶段真实文件总数（`status-bar` 的口径，⛔ 不受筛选影响） */
export const scopeTotal = computed(() => statusBar.value?.Total ?? 0)

/** 当前筛选命中的文件数（= 列表实际渲染的行数） */
export const filteredCount = computed(() => files.value.length)

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
    // ★ 必须带上 activeStandard：标签/作用是按「标准那一行画像」来的（见 standards 注释）
    const r = await fetchFilesFiltered(
      enterpriseCode.value, stageCode.value, filterTags.value, onlyUsable.value, false,
      activeStandard.value)
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
  // ★ 树刷新后标准清单可能变（管理员改了该阶段的关联标准）⇒ 从新树里重新取一次；
  //   当前选中的标准若已不在清单里，回落到第一个（否则会带着一个失效的 GUID 去查画像）
  const cur = findStageNode(treeNodes.value, stageCode.value)
  if (cur) {
    standards.value = (cur.Standards ?? []) as StandardRef[]
    if (!standards.value.some((s) => s.Code === activeStandard.value))
      activeStandard.value = standards.value[0]?.Code ?? ''
  }
  await loadFiles()
}

/** 在树里按 Code 找节点（阶段节点在第二层，但递归写更耐改） */
function findStageNode(list: any[], code: string): any | null {
  for (const n of list ?? []) {
    if (n.Code === code) return n
    const hit = findStageNode(n.Children ?? [], code)
    if (hit) return hit
  }
  return null
}

// ═══════════════════════ 三、★ 术语去技术化（专家语言）══════════════

/**
 * ★★ **「提取状态」的唯一口径**（2026-10-07 从 `OriginalFolderTree.vue` 提到这里）。
 *
 * <para><b>为什么必须收成一份</b>：此前根目录表只判两态（已提取 / 其余），
 * 文件夹树判七态 ⇒ <b>同一份文件在页面上下两处显示不同的状态</b>，
 * 用户无法判断哪个是真的（这正是「显示的信息不正确」的一种）。</para>
 *
 * <para><b>为什么压在「一个」状态上</b>：专家不该看到
 * 「转换 completed ∧ Markdown completed ∧ 分析 analyzed」这种内部状态机，
 * 它在 UI 上只该表现为一句话：已提取 / 提取中 / 提取失败 / 需人工填写 / 已排除提取 / 待提取。</para>
 */
/**
 * ★★ **状态词表（2026-10-07 按用户裁决改口径）**
 *
 * <para><b>为什么改词</b>：用户逐字指出「审核员不需要确认任何信息……针对我们提取的 markdown、
 * 标签、文档作用<b>不用做强制的审批</b>」。旧词表里
 * 「<b>需人工填写</b>」「<b>不建议提取</b>」都带着「等你来处理」的指令口气，
 * 会把系统内部准备工作的负担转嫁给审核员。</para>
 *
 * <para><b>新口径 = 状态只回答两件事</b>：① <b>系统能不能读到</b>（已提取 / 提取中 / 无法读取 / 提取失败）
 * ② <b>要不要参与提取</b>（待提取 / 不参与提取）。⛔ 不含任何「请你去确认/填写」的措辞。</para>
 */
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

/** 状态键（⛔ 不要用 `statusOf(row).text` 做判断 —— 文案会变，键不会） */
export type StatusKey = 'done' | 'processing' | 'failed' | 'manual' | 'excluded' | 'pending' | 'notSuggested'

/** 三态（转换 / Markdown / 分析）压成**一个**专家能懂的状态 —— 返回状态键 */
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

/**
 * tooltip：说清「为什么是这个状态」，用专家看得懂的话。
 *
 * ⚠️ 判据一律用 {@link statusKeyOf} 的**键**，⛔ 不用 `statusOf(row).text` ——
 * 文案随时会被改（本轮就改了三处），拿文案当判据会在改词时静默失效。
 */
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

/**
 * ★ **详情抽屉**（2026-10-07 按用户裁决重做）。
 *
 * <para><b>它只有一个职责</b>：看/改**当前标准下**的「标签」与「文档作用」。
 * 表格已经不再显示这两列 ⇒ 这里成了它们唯一的编辑入口（行操作「详情」打开的就是它）。</para>
 *
 * <para>⛔ 不再有「基本信息 / 预览 / 提取内容 / 版本」这些区块 ——
 * 它们各自是行操作里的一个动作（预览 / 提取内容 / 历史版本 / 下载原件），
 * 塞进抽屉只会让专家在无关信息里找按钮。</para>
 */
export const detailVisible = ref(false)
export const detailRow = ref<OriginalFile | null>(null)
export const detailProfile = ref<DocProfile | null>(null)
export const detailLoading = ref(false)

/**
 * 抽屉内的 Tab：`group` = 分组与文档作用（可改）/ `content` = 提取内容（只读）。
 * ★ 默认落在「分组与文档作用」—— 想看 Markdown 的人自己切过去，⛔ 不把长文本糊在首屏。
 * ⚠️ 声明成 `string`（⛔ 不用字面量联合）：`el-tabs` 的 `v-model` 是 `string | number`，
 *    用联合类型会在 `vue-tsc` 下报赋值不兼容。
 */
export const infoTab = ref('group')

/**
 * ★ 文档作用草稿 —— **一个文本框**，不再拆四段。
 *
 * <para><b>为什么去掉「四段式」</b>：原表单拆成
 * 【是什么】【审核关注点】【来源口径】【包含信息】四格让专家逐格填写，
 * 但「审核关注点 / 审核内容」这两段系统根本不使用，专家也不需要填（用户明确裁决）。</para>
 *
 * <para>⚠️ 后端 `DocPurpose` 只作**自由文本**存储（无任何代码解析【】分段；
 * 结构化要素另存 `InfoItemsJson`）⇒ 整段编辑不会破坏任何下游。</para>
 */
export const detailPurposeText = ref('')

export async function openDetail(row: OriginalFile): Promise<void> {
  detailRow.value = row
  detailVisible.value = true
  detailLoading.value = true
  detailProfile.value = null
  infoTab.value = 'group'
  // 先用列表行的值垫底（列表已按当前标准取画像）⇒ 即使画像接口失败也能编辑
  detailPurposeText.value = row.DocPurpose ?? ''
  tagDraft.value = [...(row.Tags ?? [])]
  try {
    // ★ 必须带 activeStandard：不带就只能拿到「某一行」画像
    //   ⇒ 显示的标签/作用可能属于另一个标准（用户点名痛点）
    const p = await fetchProfile(row.Code, row.EnterpriseCode, activeStandard.value)
    detailProfile.value = p
    if (p) detailPurposeText.value = p.DocPurpose ?? ''
  } catch {
    // 该标准下无画像（策略跳过 / 分析未完成）⇒ 保留列表行的值，仍可人工编辑
  } finally {
    detailLoading.value = false
  }
  // ★ 第二个 Tab「提取内容」一并取回（原独立的「提取内容」抽屉已并入本抽屉）
  await loadInfoContent(row)
}

/**
 * ★ 抽屉内切标准 —— 重新按新标准取该文件的画像。
 *
 * <para><b>为什么必须有</b>：主区已不再按标准分 tab（用户裁决去掉），
 * 但「一个文件 × 一个标准 = 一行画像」这个事实没变。切标准若不重取，
 * 抽屉里还是旧标准那行的标签/作用 —— 正是用户点名的「显示的信息不正确」。</para>
 */
export async function onDetailStandardChange(code: string): Promise<void> {
  if (!code || code === activeStandard.value) return
  activeStandard.value = code
  const row = detailRow.value
  if (row) await openDetail(row)
}

export function closeDetail(): void {
  detailVisible.value = false
  detailRow.value = null
  detailProfile.value = null
  detailPurposeText.value = ''
  infoTab.value = 'group'
  contentText.value = ''
  contentMessage.value = ''
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
      // ★ 必须带标准，否则后端改的是「版本号最大那一行」= 可能不是用户正在看的标准
      StandardCode: activeStandard.value || null,
      TagsJson: JSON.stringify(tagDraft.value),
      DocPurpose: detailPurposeText.value,
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

// ═══════════════════════ 七·b、排除提取 / 允许提取（一键）═══════════

/**
 * ★ **排除提取**（原按钮文案「忽略」，2026-10-07 用户裁决改名）。
 *
 * <para>「忽略」的问题是：它同时可以是「忽略这个提示」「忽略这行数据」「忽略这个文件」，
 * 专家无法预判点下去会发生什么。改成「排除提取」后，动作与后果（<b>不再提取内容</b>）
 * 一一对应，且与列表里的「提取」状态列同一套词。</para>
 *
 * <para>语义：<c>AnalyzePolicy=ignore</c> —— 文件<b>仍作为证据保留</b>，只是不再自动提取内容。</para>
 */
export async function excludeExtract(row: OriginalFile): Promise<void> {
  try {
    await setPolicy(row.Code, row.EnterpriseCode, 'ignore', 'manual', true)
    ElMessage.success('已排除提取（文件仍作为证据保留）')
    await loadFiles()
  } catch (e) {
    ElMessage.error((e as Error).message)
  }
}

/** ★ **允许提取**（原按钮文案「恢复提取」）—— 与「排除提取」成对 */
export async function allowExtract(row: OriginalFile): Promise<void> {
  try {
    await setPolicy(row.Code, row.EnterpriseCode, 'analyze', null, true)
    ElMessage.success('已允许提取')
    await loadFiles()
  } catch (e) {
    ElMessage.error((e as Error).message)
  }
}

// ═══════════════════════ 七·c、★ 文件夹级操作（2026-10-07 新增）═══════════

/**
 * ★ 收集文件夹**子树**下的全部文件 Code —— 文件夹级操作的作用域。
 *
 * <para><b>为什么在前端展开、而不是给后端一个「文件夹路径」</b>：
 * 后端<b>不持有「文件夹」这个概念</b>（只有 <c>RelFolderPath</c> 这个字符串列），
 * 前端这棵树本来就是用同一份 <c>files</c> 构建的 ⇒ 由前端展开，不存在
 * 「前后端各判一套子树归属、两处不一致」的可能。</para>
 *
 * <para>⚠️ 必须含子文件夹：用户点「删除整个文件夹」时，期望的是<b>这个文件夹连同里面的一切</b>，
 * 只删本层会留下一个半空的目录，比不删更让人困惑。</para>
 */
export function folderFileCodes(node: OriginalFolderNode): string[] {
  const out: string[] = []
  const walk = (n: OriginalFolderNode): void => {
    for (const f of n.Files) out.push(f.Code)
    for (const c of n.Children) walk(c)
  }
  walk(node)
  return out
}

/** 文件夹级操作的统一收尾：按成败给不同级别的提示（⛔ 不用 success 掩盖部分失败） */
function reportBatch(prefix: string, r: { Total: number; SuccessCount: number; FailedCount: number }): void {
  const msg = `${prefix}：成功 ${r.SuccessCount} / ${r.Total} 份${r.FailedCount ? `，失败 ${r.FailedCount} 份` : ''}`
  ElMessage[r.FailedCount > 0 ? 'warning' : 'success'](msg)
}

/**
 * ★ 文件夹级「不参与提取」—— 复用 `policy/batch`，<b>零新表、零新端点</b>
 * （用户裁决：文件 + 文件夹都存成文件级）。
 *
 * <para>⚠️ 代价已知并接受：日后往该文件夹新上传的文件默认是「参与」，
 * 需要重新点一次 —— 换来的是一张表都不用建。</para>
 */
export async function excludeFolder(node: OriginalFolderNode): Promise<void> {
  const codes = folderFileCodes(node)
  if (codes.length === 0) { ElMessage.warning('这个文件夹里没有文件'); return }
  try {
    const r = await batchSetPolicy(codes, enterpriseCode.value, 'ignore', 'manual', true)
    reportBatch(`「${node.Name}」已设为不参与提取`, r)
    await loadFiles()
  } catch (e) {
    ElMessage.error((e as Error).message)
  }
}

/** ★ 文件夹级「恢复参与提取」—— 与 {@link excludeFolder} 成对 */
export async function allowFolder(node: OriginalFolderNode): Promise<void> {
  const codes = folderFileCodes(node)
  if (codes.length === 0) { ElMessage.warning('这个文件夹里没有文件'); return }
  try {
    const r = await batchSetPolicy(codes, enterpriseCode.value, 'analyze', null, true)
    reportBatch(`「${node.Name}」已恢复参与提取`, r)
    await loadFiles()
  } catch (e) {
    ElMessage.error((e as Error).message)
  }
}

/**
 * ★ 文件夹级「删除整个文件夹」（含子文件夹）。
 *
 * <para>走新增的 `delete/batch`：一次往返删完，⛔ 不让前端循环调单个 delete ——
 * 那样中途失败会留下「删了一半」而用户界面上看不出来。</para>
 */
export async function deleteFolder(node: OriginalFolderNode): Promise<void> {
  const codes = folderFileCodes(node)
  if (codes.length === 0) { ElMessage.warning('这个文件夹里没有文件'); return }
  const ok = await confirmOrFalse(
    `删除整个「${node.Name}」文件夹？该文件夹（含子文件夹）下共 ${codes.length} 份文件会被一并删除，` +
    `历史版本也会移除；识别结果（标签 / 作用）会保留以备审计。`,
    '删除整个文件夹', { type: 'warning', confirmButtonText: '确认删除', cancelButtonText: '取消' })
  if (!ok) return
  try {
    const r = await batchRemoveFile(codes, enterpriseCode.value, `删除文件夹：${node.Path}`)
    reportBatch(`已删除「${node.Name}」`, r)
    await loadFiles()
  } catch (e) {
    ElMessage.error((e as Error).message)
  }
}

// ═══════════════════════ 三·c、★ 内容查看 / 重新生成 ═══════════════════════

/**
 * ★ 「提取信息」抽屉的第二个 Tab：**提取内容**（AI 读出来的 Markdown 原文，只读）。
 *
 * <para>⚠️ 2026-10-07 从独立抽屉并入「提取信息」—— 用户要的是「<b>一个</b>提取信息的展示」，
 * 而不是页面上一堆并列的入口；行操作里原来那条「提取内容」也随之取消。</para>
 *
 * <para>⛔ <b>只读，且不是给人逐字核对的</b>。用户逐字：「不能依赖这么多的文档，
 * 靠审核员一个一个的去核对，那整套智能系统就失去意义了」—— 留在这里是为了
 * <b>出问题时能查证</b>，不是为了让人审一遍。</para>
 */
export const contentText = ref('')
/** 取不到内容时的原因（Tab 空态里显示） */
export const contentMessage = ref('')
export const contentLoading = ref(false)

export async function loadInfoContent(row: OriginalFile): Promise<void> {
  contentLoading.value = true
  contentText.value = ''
  contentMessage.value = ''
  try {
    const r = await fetchMarkdownContent(row.Code, row.EnterpriseCode)
    if (r?.Markdown) contentText.value = r.Markdown
    // ⛔ 不弹 toast：抽屉自己会显示空态 + 原因，弹两个提示只会让人以为出了两次错
    else contentMessage.value = r?.Message ?? '尚未生成内容'
  } catch (e) {
    contentMessage.value = `读取内容失败：${(e as Error).message}`
  } finally {
    contentLoading.value = false
  }
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

// ═══════════════════════ 五、详情抽屉的编辑草稿 ═══════════════════════

/**
 * ★ 标签编辑草稿（受控多选）。
 *
 * <para>值必须 ∈ `cert_tag_dict.TagCode`，否则后端拒绝（标签漂移 → 召回退化）。</para>
 * <para>⛔ 只有详情抽屉在用 —— 页面上的「内联改标签 / 内联改作用」两套编辑器已于 2026-10-07 删除：
 * 表格不再显示「标签」「作用」两列，用户裁决「这个抽屉只有一个标签和文档作用的信息，
 * 不需要其他的详情设计」。</para>
 */
export const tagDraft = ref<string[]>([])

/** 详情抽屉「保存」按钮的 loading 态 */
export const purposeSaving = ref(false)

// ═══════════════════════ 七、上传（保留，不改交互）════════════════════

export const uploadVisible = ref(false)
export const uploadStage = ref<'idle' | 'uploading' | 'done'>('idle')
export const planRows = ref<PlanRow[]>([])
export const planSummary = ref<any>({})
export const uploadProgress = ref('')
export const pendingFiles = ref<File[]>([])

/**
 * ★ **本次上传的目标**（2026-10-07 新增）—— 决定「选进来的文件放到哪个相对路径下」。
 *
 * <para>三种模式：
 * <list type="bullet">
 *   <item>`stage` —— 常规上传：保持所选文件夹的相对结构（原行为，⛔ 不变）。</item>
 *   <item>`folder` —— <b>更新本文件夹内容</b>：把选进来的东西放到该文件夹路径下。</item>
 *   <item>`replace` —— <b>替换单个文件</b>：强制沿用登记名 + 登记目录。
 *     ⚠️ 后端 <c>UploadFileStepAsync</c> 会硬校验「上传文件名 == 登记名」，
 *     ⛔ 不允许借「替换」改名（改名等于换逻辑文件，存储路径不同）。</item>
 * </list></para>
 */
export type UploadTarget =
  | { kind: 'stage' }
  | { kind: 'folder'; path: string; name: string }
  | { kind: 'replace'; path: string; file: OriginalFile }

export const uploadTarget = ref<UploadTarget>({ kind: 'stage' })

/** 当前上传目标的人话说明（抽屉顶部提示用；常规上传时为空串） */
export const uploadTargetLabel = computed(() => {
  const t = uploadTarget.value
  if (t.kind === 'folder') return `更新文件夹「${t.name}」下的内容（${t.path}）`
  if (t.kind === 'replace') return `替换「${t.file.FileName}」（${t.path || '根目录'}）· 请选择同名文件`
  return ''
})

/**
 * ★ 上传抽屉的标题（2026-10-07 四轮）。
 *
 * <para>同一个抽屉承载三种入口，标题却恒为「上传文件」⇒ 从「替换」点进来的人会以为
 * 自己走错了地方。标题跟着目标走，是最低成本的自证。</para>
 */
export const uploadTitle = computed(() => {
  const t = uploadTarget.value
  if (t.kind === 'replace') return '替换文件'
  if (t.kind === 'folder') return '更新文件夹内容'
  return '上传文件'
})

/**
 * ★ 目标提示条的级别：「替换」有**同名**硬约束（后端会拒），用 warning 让它显眼；
 * 「更新文件夹」只是说明落点，info 即可。⛔ 常规上传不显示提示条。
 */
export const uploadTargetAlertType = computed<'warning' | 'info'>(() =>
  uploadTarget.value.kind === 'replace' ? 'warning' : 'info')

export function openUpload(): void { openUploadWith({ kind: 'stage' }) }

/** ★ 文件夹级「更新本文件夹内容」—— 预置目标路径后打开上传抽屉 */
export function openFolderUpload(node: OriginalFolderNode): void {
  openUploadWith({ kind: 'folder', path: node.Path, name: node.Name })
}

/** ★ 文件级「替换」—— 预置登记名 + 登记目录后打开上传抽屉 */
export function openReplace(row: OriginalFile): void {
  openUploadWith({ kind: 'replace', path: row.RelFolderPath || '', file: row })
}

function openUploadWith(target: UploadTarget): void {
  if (!enterpriseCode.value || !stageCode.value) {
    ElMessage.warning('请先在左侧选择「企业 › 阶段」')
    return
  }
  uploadStage.value = 'idle'
  planRows.value = []
  planSummary.value = {}
  pendingFiles.value = []
  uploadProgress.value = ''
  uploadTarget.value = target
  uploadVisible.value = true
}

/**
 * ★ 把浏览器选中的 `File` 列表翻译成后端的 `PlanItem` 列表。
 *
 * <para>⚠️ <b>相对路径的改写规则全部收在这里</b>：`plan` 与 `init` 两次调用必须用同一份映射，
 * 否则会出现「预检清单说会替换、实际却新建了另一个文件」这种清单与结果对不上的怪象。</para>
 */
function toItems(files: File[]): PlanItem[] {
  const t = uploadTarget.value
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

/**
 * ★ 文件夹级「更新」时的相对路径改写 —— 只有两条规则，必须可预期。
 *
 * <list type="number">
 *   <item>用户选的正是目标文件夹本身（首段名 == 目标文件夹名）⇒ 用目标路径替换首段，其余层级保持。
 *     例：更新「4记录文件/技术类」时选了「技术类」文件夹 ⇒ `技术类/a.doc` 落到 `4记录文件/技术类/a.doc`。</item>
 *   <item>其它情况（选了散文件、或选了别的文件夹）⇒ 整体挂到目标路径下。</item>
 * </list>
 *
 * <para>⛔ 不做模糊匹配、不做大小写归一 —— 猜错了会把文件放到用户没预期的地方，
 * 而这类错误在界面上很难被发现。</para>
 */
function remapFolderPath(rawDir: string, t: { path: string; name: string }): string {
  const dir = (rawDir || '').replace(/^\/+|\/+$/g, '')
  const tgt = (t.path || '').replace(/^\/+|\/+$/g, '')
  if (!tgt) return dir
  if (!dir) return tgt
  const segs = dir.split('/')
  if (segs[0] === t.name) return [tgt, ...segs.slice(1)].filter(Boolean).join('/')
  return [tgt, dir].filter(Boolean).join('/')
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
  let accepted = list.filter((f) => part.accepted.some((a) => a.name === f.name))

  // ★ 替换模式：只接受 1 个文件，且必须**同名**
  //   ⚠️ 后端 UploadFileStepAsync 会硬校验「上传文件名 == 登记名」，不一致直接拒；
  //      在前端先拦一道，用户看到的是「要选同名文件」而不是一个后端错误码。
  const t = uploadTarget.value
  if (t.kind === 'replace') {
    const hit = accepted.find((f) => f.name === t.file.FileName)
    if (!hit) {
      pendingFiles.value = []
      planRows.value = []
      planSummary.value = {} as any
      ElMessage.warning(`「替换」要求同名文件：请选择名为「${t.file.FileName}」的文件（改名等于换一个文件）`)
      return
    }
    accepted = [hit]
  }
  pendingFiles.value = accepted

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
  // ★ 目标必须一起清掉 —— 否则下一次点「上传文件」会带着上一次的「替换 xxx」目标
  uploadTarget.value = { kind: 'stage' }
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
