/**
 * 取值来源链（`DocTemplateAnchor.SourceSpec`）的**唯一前端模型**
 *
 * 【契约出处】`22-填写单元属性分类与取值来源模型-V1.md` §三 / §八
 *
 * ```jsonc
 * {
 *   "combine": "firstHit",     // firstHit | concat | template
 *   "separator": "、",         // 仅 concat 用
 *   "expr": "{{Name}}（{{CreditCode}}）",  // 仅 template 用
 *   "sources": [               // ★ 有序：顺序 = 回退优先级
 *     { "kind": "global",  "ref": "ENT_CREDIT_CODE", "onMissing": "next" },
 *     { "kind": "profile", "ref": "营业执照", "field": "统一社会信用代码", "minConfidence": 0.85 },
 *     { "kind": "manual",  "onMissing": "todo" }
 *   ]
 * }
 * ```
 *
 * 【为什么单独一个文件】
 *   ① 解析/生成必须**成对**（一处放宽一处收紧 = 静默丢字段）；
 *   ② 「人话预览」是防错的关键（`37` 号 §3.5 底部），文案逻辑要能单测；
 *   ③ ⛔ 不引第三方拖拽库 —— 原生 HTML5 `draggable` 足够，少一个依赖。
 */

/** 一个来源条目（**字段名与后端 JSON 逐字一致**，⛔ 不做驼峰转换） */
export interface SourceSpecEntry {
  /** `global` / `compute` / `manual` / `profile` / `self` / `sibling` / `ai` */
  kind: string
  /** `global` → 参数编码；`profile` → 文档名；`self`/`sibling` → 锚点/文档引用；`ai` → 提示词编码 */
  ref?: string
  /** `profile` 专用：画像里的字段名 */
  field?: string
  /** `profile` 专用：最低置信度（低于此值视为未命中） */
  minConfidence?: number
  /** `ai` 专用（41 号原型 V6）：提示词组（用于批量提取分组） */
  promptGroup?: string
  /** 缺失时行为：`block` / `next`（默认）/ `todo` / `empty` */
  onMissing?: string
}

/** 整条来源链 */
export interface SourceSpecModel {
  combine: CombineMode
  /** 仅 `concat` 用 */
  separator?: string
  /** 仅 `template` 用 */
  expr?: string
  /** ★ 有序 */
  sources: SourceSpecEntry[]
}

export type CombineMode = 'firstHit' | 'concat' | 'template'

/** 组合方式（22 号 §三） */
export const COMBINE_MODES: {
  value: CombineMode
  label: string
  hint: string
}[] = [
  {
    value: 'firstHit',
    label: '顺序回退',
    hint: '按顺序取第一个非空，取到即停（覆盖 90% 场景）',
  },
  { value: 'concat', label: '拼接', hint: '多个来源拼成一句，全部都要' },
  { value: 'template', label: '模板套用', hint: '用小模板组合多个来源' },
]

/**
 * 来源类别。
 *
 * ⚠️ `implemented=false` 的项**不是设计遗漏**，而是尚未落地的 Skill：
 * 已注册可用的只有 `global`（`src_global_param`）/ `manual`（`src_manual`）
 * 与 `ai`（`src_ai_field` / `src_ai_table` / `src_semantic` 三个）。
 * ⛔ `compute` **没有对应 Skill**（后端执行器命中即 `Fail`）⇒ 必须标未实现。
 * 页面把 `implemented=false` 的项**灰显 + 标注「未实现」**，
 * ⛔ 不静默让用户配一个运行期必然不生效的来源。
 */
export const SOURCE_KINDS: {
  value: string
  label: string
  implemented: boolean
  hint: string
}[] = [
  {
    value: 'global',
    label: '全局参数',
    implemented: true,
    hint: '取自「全局填写参数」（企业基础信息 / 认证项目信息）',
  },
  {
    value: 'manual',
    label: '人工录入',
    implemented: true,
    hint: '运行期挂人工待办，由专家填写',
  },
  {
    value: 'compute',
    label: '计算',
    implemented: false,
    hint: '由表达式算出来（如合计、计数）—— 一期无对应 Skill，运行期会直接失败',
  },
  {
    value: 'profile',
    label: '企业资料画像',
    implemented: false,
    hint: '从已上传的企业资料里提取的值（画像链尚未接通本页）',
  },
  {
    value: 'self',
    label: '本模板其他锚点',
    implemented: false,
    hint: '引用同一模板内另一个锚点的值',
  },
  {
    value: 'sibling',
    label: '兄弟文档',
    implemented: false,
    hint: '引用同一批次内另一份文档的值',
  },
  {
    value: 'ai',
    label: 'AI 生成',
    implemented: true,
    hint: '由模型按上下文生成（支持批量提示词组）',
  },
]

/** 缺失时行为（22 号 §八） */
export const ON_MISSING_OPTIONS: {
  value: string
  label: string
  hint: string
}[] = [
  { value: 'next', label: '取下一个', hint: '跳过本来源，继续下一个（默认）' },
  { value: 'todo', label: '挂待办', hint: '挂人工待办，继续填其他锚点' },
  { value: 'block', label: '阻断', hint: '直接报错，整份文档不生成' },
  { value: 'empty', label: '留空', hint: '留空，不告警' },
]

export const DEFAULT_ON_MISSING = 'next'

export function kindLabel(kind: string): string {
  return SOURCE_KINDS.find((k) => k.value === kind)?.label ?? kind
}

export function isImplementedKind(kind: string): boolean {
  return SOURCE_KINDS.find((k) => k.value === kind)?.implemented ?? false
}

/** 空模型（新增来源链时的起点） */
export function emptySourceSpec(): SourceSpecModel {
  return { combine: 'firstHit', sources: [] }
}

/**
 * 解析 `SourceSpec` 字符串。
 *
 * ⚠️ **绝不抛异常**：这一列是人工/历史数据，可能是空串、可能是半截 JSON。
 * 抛异常会让整个侧边栏打不开，用户连「改回来」的机会都没有。
 * 解析失败 ⇒ 返回空模型 + `parseError=true`，由界面提示「原值无法解析，保存将覆盖」。
 */
export function parseSourceSpec(raw?: string | null): {
  model: SourceSpecModel
  parseError: boolean
} {
  const text = (raw ?? '').trim()
  if (!text) return { model: emptySourceSpec(), parseError: false }

  try {
    const obj = JSON.parse(text)
    if (obj == null || typeof obj !== 'object')
      return { model: emptySourceSpec(), parseError: true }

    const combine = (['firstHit', 'concat', 'template'] as const).includes(
      obj.combine,
    )
      ? (obj.combine as CombineMode)
      : 'firstHit'

    const sources: SourceSpecEntry[] = Array.isArray(obj.sources)
      ? obj.sources
          .filter((s: any) => s && typeof s === 'object')
          .map((s: any) => ({
            kind: String(s.kind ?? 'global'),
            ref: s.ref == null ? undefined : String(s.ref),
            field: s.field == null ? undefined : String(s.field),
            minConfidence:
              typeof s.minConfidence === 'number' ? s.minConfidence : undefined,
            promptGroup:
              s.promptGroup == null ? undefined : String(s.promptGroup),
            onMissing: s.onMissing == null ? undefined : String(s.onMissing),
          }))
      : []

    return {
      model: {
        combine,
        separator: obj.separator == null ? undefined : String(obj.separator),
        expr: obj.expr == null ? undefined : String(obj.expr),
        sources,
      },
      parseError: false,
    }
  } catch {
    return { model: emptySourceSpec(), parseError: true }
  }
}

/**
 * 生成 `SourceSpec` 字符串。
 *
 * 规则：
 * - 无来源 ⇒ 返回 `null`（**清空该列**，而不是存 `{"sources":[]}`）——
 *   校验页把「未配来源」判为黄牌，靠的就是这一列是空。
 * - 只为 `concat` 写 `separator`、只为 `template` 写 `expr`（⛔ 不写无关字段，避免两处口径）。
 * - 每个来源统一补 `onMissing` 默认值，让 JSON 自解释。
 */
export function stringifySourceSpec(model: SourceSpecModel): string | null {
  if (!model.sources || model.sources.length === 0) return null

  const out: Record<string, any> = { combine: model.combine }
  if (model.combine === 'concat') out.separator = model.separator ?? '、'
  if (model.combine === 'template') out.expr = model.expr ?? ''

  out.sources = model.sources.map((s) => {
    const e: Record<string, any> = { kind: s.kind }
    if (s.ref) e.ref = s.ref
    if (s.kind === 'profile' && s.field) e.field = s.field
    if (s.kind === 'profile' && typeof s.minConfidence === 'number')
      e.minConfidence = s.minConfidence
    if (s.kind === 'ai' && s.promptGroup) e.promptGroup = s.promptGroup
    e.onMissing = s.onMissing || DEFAULT_ON_MISSING
    return e
  })

  return JSON.stringify(out)
}

/** 单条来源的一行摘要（表格「取值来源」列用） */
export function entrySummary(e: SourceSpecEntry): string {
  const base = (() => {
    switch (e.kind) {
      case 'global':
        return `全局参数:${e.ref || '?'}`
      case 'profile':
        return `画像:${e.ref || '?'}${e.field ? `.${e.field}` : ''}`
      case 'compute':
        return `计算:${e.ref || '?'}`
      case 'manual':
        return '人工录入'
      case 'self':
        return `本模板:${e.ref || '?'}`
      case 'sibling':
        return `兄弟文档:${e.ref || '?'}`
      case 'ai':
        return `AI:${e.ref || '?'}${e.promptGroup ? `[组:${e.promptGroup}]` : ''}`
      default:
        return `${e.kind}:${e.ref || ''}`
    }
  })()

  // 未实现的来源必须显式标注 —— 否则实施人员会以为配了就能跑
  return isImplementedKind(e.kind) ? base : `${base}（未实现）`
}

/** 整条链的一行摘要（表格列 + 侧边栏标题用） */
export function summarizeSourceSpec(model: SourceSpecModel): string {
  if (!model.sources.length) return ''
  const parts = model.sources.map(entrySummary)
  if (model.combine === 'concat')
    return `拼接(${model.separator ?? '、'}): ${parts.join(' + ')}`
  if (model.combine === 'template')
    return `模板套用: ${model.expr || '(未填模板)'} ← ${parts.join(' + ')}`
  return parts.join(' → ')
}

/**
 * ★ 底部实时人话预览（`37` 号 §3.5 —— 「这是防错的关键」）。
 *
 * 目的不是复述配置，而是让实施人员**用业务语言确认逻辑对不对**：
 * 「先取全局参数 → 没有则去企业资料画像里找 → 都没有就挂人工待办」
 */
export function humanPreview(model: SourceSpecModel): string {
  if (!model.sources.length) return '尚未配置取值来源 —— 运行期该锚点将留空。'

  const labels = model.sources.map(
    (s, i) => `${i + 1}. ${kindLabel(s.kind)}${s.ref ? `（${s.ref}）` : ''}`,
  )

  if (model.combine === 'concat') {
    return `把 ${labels.join(' 与 ')} 用「${model.separator || '、'}」拼成一段文字。`
  }
  if (model.combine === 'template') {
    return `按模板「${model.expr || '(未填)'}」套用 ${labels.join('、')}。`
  }

  // firstHit：把每个来源的 onMissing 翻成人话，串成一条回退链
  const sentences: string[] = []
  model.sources.forEach((s, i) => {
    const tail = (() => {
      switch (s.onMissing || DEFAULT_ON_MISSING) {
        case 'block':
          return '取不到就阻断（整份文档不生成）'
        case 'todo':
          return '取不到就挂人工待办'
        case 'empty':
          return '取不到就留空'
        default:
          return i === model.sources.length - 1
            ? '取不到就留空'
            : '取不到就继续往下找'
      }
    })()
    sentences.push(`先取 ${labels[i]}，${tail}`)
  })

  return sentences.join('；') + '。'
}
