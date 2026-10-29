/**
 * 企业文档页 · **纯函数层**（⛔ 无 Vue 状态、无请求、无 ElMessage）
 *
 * 为什么单独一个文件：这些函数是「状态 → 文案 / 数字 → 显示」的唯一换算点，
 * 页面模板里出现 `row.ConvertStatus === 'completed' ? ... : ...` 这种内联判定，
 * 就会出现「同一份文件在表格与抽屉显示不同状态」的历史问题
 * （老页 `enterprise-original` 实测踩过：上表判两态、文件夹树判七态）。
 *
 * ⇒ 一律收口到这里：**页面只调函数，不写判定**。也因此可以纯单测（见 `logic.test.ts`）。
 */
import type {
  EntDocClauseHit,
  EntDocExtractContent,
  EntDocExtractionRow,
  EntDocRow,
} from '@share/api/ent/enterprise-doc'

/** 徽章语义（`YzhStatusBadge` 的 `type`） */
export type BadgeType = 'success' | 'warning' | 'danger' | 'info'

export interface Badge {
  type: BadgeType
  text: string
  /** 悬浮说明（缺省不渲染 tooltip） */
  tip?: string
}

// ═══════════════════════ 一、展示格式 ═══════════════════════

/** 字节数 → 人类可读（`0` / 空 显示 `-`，⛔ 不显示 `0 B` 让人以为文件是空的） */
export function formatSize(bytes?: number | null): string {
  if (bytes == null || bytes <= 0) return '-'
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`
}

/**
 * 相对文件夹路径 → 展示文案。
 *
 * ⚠️ 空串 = **根目录**（后端 `NormalizeFolder` 把空/`/`/`.` 都归一成空串），
 * 直接显示空串会让「文件夹」列看起来是坏的。
 */
export function folderLabel(path?: string | null): string {
  const p = (path ?? '').trim().replace(/^\/+|\/+$/g, '')
  return p.length === 0 ? '根目录' : p
}

/** 文件类型 → 中文名（专家不需要看扩展名；⛔ 不用 emoji 当图标） */
export function fileKindText(fileType?: string | null): string {
  const t = (fileType ?? '').toLowerCase()
  const map: Record<string, string> = {
    '.pdf': 'PDF',
    '.doc': 'Word', '.docx': 'Word', '.wps': 'Word',
    '.xls': 'Excel', '.xlsx': 'Excel', '.et': 'Excel', '.csv': '表格',
    '.ppt': 'PPT', '.pptx': 'PPT', '.dps': 'PPT',
    '.txt': '文本', '.md': '文本',
    '.jpg': '图片', '.jpeg': '图片', '.png': '图片', '.gif': '图片',
    '.bmp': '图片', '.webp': '图片', '.tif': '图片', '.tiff': '图片',
  }
  return map[t] ?? '文件'
}

// ═══════════════════════ 二、状态徽章（唯一口径） ═══════════════════════

/**
 * 转换 / Markdown 状态 → 徽章。
 *
 * ⚠️ 取值必须是后端 `ConvertStatus` 的六个字面量（`completed` 不是 `success`）。
 */
export function convertBadge(status?: string | null): Badge {
  switch ((status ?? '').toLowerCase()) {
    case 'completed': return { type: 'success', text: '已转换' }
    case 'converting': return { type: 'warning', text: '转换中' }
    case 'pending': return { type: 'warning', text: '待转换' }
    case 'failed': return { type: 'danger', text: '转换失败' }
    case 'unsupported': return { type: 'info', text: '不支持' }
    case 'none':
    default: return { type: 'info', text: '未开始' }
  }
}

/**
 * 单个「文档 × 标准」提取行 → 徽章。
 *
 * ★ `IsStale` 优先于状态：源文件已换（替换 / 回滚）⇒ 结果**不可信**，
 * 即使 `ExtractStatus` 还是 `done` 也必须显示「已过期」（⛔ 不能让旧结论冒充新结论）。
 */
export function standardExtractBadge(row?: EntDocExtractionRow | null): Badge {
  if (!row) return { type: 'info', text: '未提取' }
  if (row.IsStale) {
    return {
      type: 'warning',
      text: '已过期',
      tip: '源文件已变更（替换 / 回滚），本结果不可信，请重新提取',
    }
  }
  switch ((row.ExtractStatus ?? '').toLowerCase()) {
    case 'done':
      return { type: 'success', text: `已提取 ${row.ClauseCount ?? 0} 条` }
    case 'running': return { type: 'warning', text: '提取中' }
    case 'pending': return { type: 'warning', text: '待提取' }
    case 'partial': return { type: 'warning', text: `部分 ${row.ClauseCount ?? 0} 条` }
    case 'failed': return { type: 'danger', text: '提取失败' }
    case 'skipped': return { type: 'info', text: '已跳过' }
    case 'none':
    default: return { type: 'info', text: '未提取' }
  }
}

/**
 * 文件行 → 提取聚合徽章（**表格「提取」列的唯一口径**）。
 *
 * 聚合顺序（先说坏消息，再说好消息）：
 *   ① 有失败 ⇒ 失败（即使其他标准成功，也要让人知道有东西没提出来）
 *   ② 有已过期 ⇒ 已过期
 *   ③ 有计划内标准还没产物 ⇒ 待提取（N 个）
 *   ④ 全 done ⇒ 已提取 N 条（N = 各标准条款数之和）
 *   ⑤ 其余 ⇒ 未提取 / 已跳过
 */
export function extractBadge(row?: EntDocRow | null): Badge {
  if (!row) return { type: 'info', text: '未提取' }
  const exts = row.Extractions ?? []
  const missing = row.MissingStandardCodes ?? []

  const failed = exts.filter((e) => (e.ExtractStatus ?? '').toLowerCase() === 'failed')
  if (failed.length > 0) {
    return { type: 'danger', text: `提取失败 ${failed.length} 个标准`, tip: firstMessage(failed) }
  }
  const stale = exts.filter((e) => e.IsStale)
  if (stale.length > 0) {
    return { type: 'warning', text: '已过期', tip: '源文件已变更，提取结果需要重跑' }
  }
  const running = exts.filter((e) => ['running', 'pending'].includes((e.ExtractStatus ?? '').toLowerCase()))
  if (running.length > 0) return { type: 'warning', text: '提取中' }

  if (missing.length > 0) {
    return {
      type: 'info',
      text: `待提取 ${missing.length} 个标准`,
      tip: exts.length > 0 ? '计划内的部分标准还没有提取结果' : '还没有任何标准的提取结果',
    }
  }

  if (exts.length > 0 && exts.every((e) => (e.ExtractStatus ?? '').toLowerCase() === 'done')) {
    const total = exts.reduce((n, e) => n + (e.ClauseCount ?? 0), 0)
    return { type: 'success', text: `已提取 ${total} 条` }
  }

  switch ((row.ExtractStatus ?? '').toLowerCase()) {
    case 'skipped':
      return {
        type: 'info',
        text: '已跳过',
        tip: row.ExtractMessage || '没有可提取的内容（缺 Markdown 或没有有效认证计划）',
      }
    case 'done': {
      const total = exts.reduce((n, e) => n + (e.ClauseCount ?? 0), 0)
      return { type: 'success', text: `已提取 ${total} 条` }
    }
    case 'failed':
      return { type: 'danger', text: '提取失败', tip: row.ExtractMessage || undefined }
    default:
      return { type: 'info', text: '未提取' }
  }
}

function firstMessage(list: EntDocExtractionRow[]): string | undefined {
  const hit = list.find((e) => (e.ExtractMessage ?? '').trim().length > 0)
  return hit?.ExtractMessage ?? undefined
}

// ═══════════════════════ 三、条款号 ═══════════════════════

/** `"1,4.1,4.2.4"` → `['1','4.1','4.2.4']`（去空、去重、保持原顺序） */
export function parseClauseNumbers(csv?: string | null): string[] {
  const raw = (csv ?? '').split(',')
  const seen = new Set<string>()
  const out: string[] = []
  for (const s of raw) {
    const v = s.trim()
    if (v.length === 0 || seen.has(v)) continue
    seen.add(v)
    out.push(v)
  }
  return out
}

/**
 * 条款号自然序（`4.2.4` < `4.2.10`，⛔ 字典序会把 `4.2.10` 排到 `4.2.4` 前面）。
 *
 * 与 `ClauseExtractExecutor.ClauseSortKey` 同口径：按 `.` 拆数字、非数字段记 9999。
 */
export function clauseSortKey(no: string): number[] {
  return (no ?? '')
    .split('.')
    .map((seg) => {
      const n = Number(seg)
      return Number.isFinite(n) && seg.trim() !== '' ? n : 9999
    })
}

/** 条款号比较器（供 `Array.prototype.sort`） */
export function compareClauseNo(a: string, b: string): number {
  const ka = clauseSortKey(a)
  const kb = clauseSortKey(b)
  const len = Math.max(ka.length, kb.length)
  for (let i = 0; i < len; i++) {
    const va = ka[i] ?? -1
    const vb = kb[i] ?? -1
    if (va !== vb) return va - vb
  }
  return a.localeCompare(b)
}

// ═══════════════════════ 四、提取 JSON ═══════════════════════

/**
 * 解析 `ContentJson`（**PascalCase**：`{ DocMeta, Clauses }`）。
 *
 * ⚠️ 坏 JSON 返回 `null` + 不抛：一条脏数据不该把整页打挂
 * （与后端 `DocumentExtractionService.ParseContent` 同口径）。
 */
export function parseExtractContent(json?: string | null): EntDocExtractContent | null {
  const text = (json ?? '').trim()
  if (text.length === 0) return null
  try {
    const obj = JSON.parse(text)
    if (!obj || typeof obj !== 'object') return null
    // ★ 大小写双读：后端序列化是 PascalCase，但历史行可能是 camelCase
    const clauses = obj.Clauses ?? obj.clauses
    return {
      DocMeta: obj.DocMeta ?? obj.docMeta ?? undefined,
      Clauses: Array.isArray(clauses) ? (clauses as EntDocClauseHit[]) : [],
    }
  } catch {
    return null
  }
}

/** 条款命中行排序（按条款号自然序） */
export function sortClauses(list: EntDocClauseHit[]): EntDocClauseHit[] {
  return [...list].sort((a, b) => compareClauseNo(a.ClauseNumber ?? '', b.ClauseNumber ?? ''))
}

/** 置信度 → 百分比文案（`0.85` → `85%`；缺省 `-`） */
export function confidenceText(v?: number | null): string {
  if (v == null || !Number.isFinite(v)) return '-'
  const pct = v <= 1 ? v * 100 : v
  return `${Math.round(pct)}%`
}

/** 置信度 → 徽章（≥80 高 / ≥60 中 / 其余低） */
export function confidenceBadge(v?: number | null): Badge {
  if (v == null || !Number.isFinite(v)) return { type: 'info', text: '-' }
  const pct = v <= 1 ? v * 100 : v
  if (pct >= 80) return { type: 'success', text: `${Math.round(pct)}%` }
  if (pct >= 60) return { type: 'warning', text: `${Math.round(pct)}%` }
  return { type: 'danger', text: `${Math.round(pct)}%` }
}

// ═══════════════════════ 五、锚点定位（P2 遗留 #1 的消费侧修复） ═══════════════════════

/**
 * 标点归一（**逐字符 1:1**，⛔ 不删不增 ⇒ 归一后下标与原串一一对应）。
 *
 * 为什么需要：模型摘录时会把原文的 `"` 输出成中文引号 `“”`、把 `,` 输出成 `，`
 * （P2 实测 iso9001 24 条里有 1 条因此逐字比对 miss）。
 * ⇒ **下游做「原文定位」必须先归一标点再比**，⛔ 不能直接 `IndexOf` / `indexOf`。
 *
 * ⚠️ 只做 1:1 替换：任何「删除空白 / 合并字符」的归一都会让下标错位，
 * 定位出来的片段就会偏移（那比找不到更糟 —— 用户会以为找到了）。
 */
export function normalizePunctuation(s: string): string {
  const map: Record<string, string> = {
    '“': '"', '”': '"', '„': '"', '‟': '"', '「': '"', '」': '"', '『': '"', '』': '"',
    '‘': "'", '’': "'", '‚': "'", '‛': "'",
    '（': '(', '）': ')', '【': '[', '】': ']', '《': '<', '》': '>',
    '，': ',', '。': '.', '；': ';', '：': ':', '！': '!', '？': '?',
    '、': ',', '—': '-', '–': '-', '～': '~',
    '\u00A0': ' ', '\u3000': ' ',
  }
  let out = ''
  for (const ch of s) out += map[ch] ?? ch
  return out
}

/**
 * **剥掉全部空白**并记录「剥离后下标 → 原串下标」映射（用于跨行重排的 Markdown）。
 *
 * ★ 为什么是「剥掉」而不是「折叠成一个空格」：
 *   PDF→Markdown 会在**句子中间**插换行（`程序，\n确保`），而模型摘录给的是连续句子
 *   （`程序，确保`）。折叠成空格后两侧仍差一个空格 ⇒ 永远匹配不上。
 *   中文文本里空白不承载语义，剥掉是安全的。
 *   ⚠️ 代价：`a b` 与 `ab` 会被判为同一串。锚点是 10–40 字的整句，
 *   这个宽松度不会造成实际误命中，而「找不到」才是真问题。
 */
function stripWhitespace(s: string): { text: string; map: number[] } {
  const chars: string[] = []
  const map: number[] = []
  for (let i = 0; i < s.length; i++) {
    const ch = s[i]
    if (ch === ' ' || ch === '\n' || ch === '\r' || ch === '\t') continue
    chars.push(ch)
    map.push(i)
  }
  return { text: chars.join(''), map }
}

export interface AnchorHit {
  /** 在原串中的起始下标 */
  index: number
  /** 在原串中的长度 */
  length: number
  /** 命中所用的比对级别（排障用） */
  mode: 'exact' | 'punctuation' | 'reflow'
}

/**
 * 在原文里定位锚点，返回**原串**区间；找不到返回 `null`。
 *
 * 三级递进（先严后宽，命中即返回，并如实告知用了哪一级）：
 *   ① `exact`        —— 逐字命中（96–100% 的情况走这里）
 *   ② `punctuation`  —— 标点归一后命中（模型改了引号 / 逗号）
 *   ③ `reflow`       —— 标点归一 + 剥掉空白后命中（PDF→Markdown 把句子断成多行）
 *
 * ⚠️ 返回的是**原串**下标（`reflow` 级通过下标映射还原），
 * 调用方可以直接 `slice` 出来做高亮，⛔ 不要拿归一后的串去 slice。
 */
export function locateAnchor(haystack: string, anchor?: string | null): AnchorHit | null {
  const text = haystack ?? ''
  const needle = (anchor ?? '').trim()
  if (text.length === 0 || needle.length === 0) return null

  // ① 逐字
  const exact = text.indexOf(needle)
  if (exact >= 0) return { index: exact, length: needle.length, mode: 'exact' }

  // ② 标点归一（1:1 ⇒ 下标可直接用）
  const normText = normalizePunctuation(text)
  const normNeedle = normalizePunctuation(needle)
  const p = normText.indexOf(normNeedle)
  if (p >= 0) return { index: p, length: normNeedle.length, mode: 'punctuation' }

  // ③ 剥掉空白（需要下标映射还原）
  const folded = stripWhitespace(normText)
  const foldedNeedle = stripWhitespace(normNeedle).text
  if (foldedNeedle.length === 0) return null
  const f = folded.text.indexOf(foldedNeedle)
  if (f < 0) return null
  const start = folded.map[f]
  const end = folded.map[f + foldedNeedle.length - 1]
  if (start == null || end == null) return null
  return { index: start, length: end - start + 1, mode: 'reflow' }
}

/** 把原文切成「前 / 命中 / 后」三段（页面高亮用；找不到时 `hit=null`） */
export function splitByAnchor(
  haystack: string, anchor?: string | null,
): { before: string; hit: string | null; after: string; mode: AnchorHit['mode'] | null } {
  const found = locateAnchor(haystack, anchor)
  if (!found) return { before: haystack ?? '', hit: null, after: '', mode: null }
  const text = haystack ?? ''
  return {
    before: text.slice(0, found.index),
    hit: text.slice(found.index, found.index + found.length),
    after: text.slice(found.index + found.length),
    mode: found.mode,
  }
}

// ═══════════════════════ 六、上传预检文案 ═══════════════════════

/** 预检动作 → 中文（`skip` 是「同一份文件，不重复处理」，⛔ 不是失败） */
export function planActionText(action?: string | null): string {
  switch ((action ?? '').toLowerCase()) {
    case 'create': return '新增'
    case 'replace': return '替换（版本 +1）'
    case 'skip': return '已存在，跳过'
    default: return '未知'
  }
}

/** 预检动作 → 徽章 */
export function planActionBadge(action?: string | null): Badge {
  switch ((action ?? '').toLowerCase()) {
    case 'create': return { type: 'success', text: '新增' }
    case 'replace': return { type: 'warning', text: '替换' }
    case 'skip': return { type: 'info', text: '跳过' }
    default: return { type: 'info', text: '未知' }
  }
}

// ═══════════════════════ 七、汇总 ═══════════════════════

/** 表格上方的一句话汇总（⛔ 数字全部来自入参，不在模板里现算） */
export function summarize(rows: EntDocRow[]): {
  total: number
  ready: number
  converting: number
  failed: number
  extracted: number
} {
  const list = rows ?? []
  return {
    total: list.length,
    ready: list.filter((r) => r.CanExtract).length,
    converting: list.filter(
      (r) => ['converting', 'pending'].includes((r.ConvertStatus ?? '').toLowerCase())
        || ['converting', 'pending'].includes((r.MarkdownStatus ?? '').toLowerCase()),
    ).length,
    failed: list.filter(
      (r) => (r.ConvertStatus ?? '').toLowerCase() === 'failed'
        || (r.MarkdownStatus ?? '').toLowerCase() === 'failed',
    ).length,
    extracted: list.filter((r) => (r.ExtractStatus ?? '').toLowerCase() === 'done').length,
  }
}

// ═══════════════════════ 八、队列 ═══════════════════════

/** 队列 / 任务状态 → 徽章（`yzh_queue` 与 `yzh_queue_task` 共用同一套字面量） */
export function queueStatusBadge(status?: string | null): Badge {
  switch ((status ?? '').toLowerCase()) {
    case 'completed': return { type: 'success', text: '已完成' }
    case 'running':
    case 'processing': return { type: 'warning', text: '处理中' }
    case 'pending': return { type: 'info', text: '排队中' }
    case 'failed': return { type: 'danger', text: '失败' }
    case 'cancelled': return { type: 'info', text: '已取消' }
    default: return { type: 'info', text: status || '未知' }
  }
}

/**
 * 任务类型 → 中文。
 *
 * ⚠️ 这里出现的是**技术词**（转换 / 提取），因为队列明细本来就是给「排查问题」用的；
 *   主界面（表格列）⛔ 不出现这些词，只出现业务语言（已转换 / 已提取）。
 */
export function taskTypeText(taskType?: string | null): string {
  switch ((taskType ?? '').toLowerCase()) {
    case 'enterprise_doc_convert': return '转换（PDF + 原文）'
    case 'enterprise_clause_extract': return '按标准提取条款'
    case 'enterprise_original_file': return '转换（旧版）'
    case 'enterprise_original_analyze': return '语义分析（旧版）'
    default: return taskType || '-'
  }
}

/** 耗时（毫秒）→ `1.2s` / `3m20s` */
export function formatDuration(ms?: number | null): string {
  if (ms == null || !Number.isFinite(ms) || ms < 0) return '-'
  if (ms < 1000) return `${Math.round(ms)}ms`
  const s = ms / 1000
  if (s < 60) return `${s.toFixed(1)}s`
  const m = Math.floor(s / 60)
  return `${m}m${Math.round(s - m * 60)}s`
}
