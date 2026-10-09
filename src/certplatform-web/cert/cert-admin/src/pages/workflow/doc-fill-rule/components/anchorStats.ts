/**
 * 锚点清单的**派生视图** —— 纯函数，可单测。
 *
 * 【为什么抽出来】
 *   原实现把 `parseSourceSpec(row.SourceSpec)` 散在 4 个地方：
 *     ① `stats` 遍历所有行各解析一次；
 *     ② `filtered` 的「未配来源」分支再解析一次；
 *     ③ 模板里**每张卡片**的 `isUnconfigured(a)`；
 *     ④ 模板里**每张卡片**的 `sourceSummary(a)`。
 *   ③④ 在 `v-for` 里逐行调用 ⇒ N 个锚点**每次渲染要解析 4N 次 JSON**，
 *   而且这 4 处的判据「什么是未配」是**各写各的**（三份实现，改一处必漏两处）。
 *
 *   抽成本模块后：**一行只解析一次**（`buildAnchorViews`），
 *   后续全部读 `AnchorView` 上的缓存字段；判据也只有一份。
 *
 * 【与 `sourceSpec.ts` 的分工】
 *   `sourceSpec.ts` = 「一条 SourceSpec 字符串 ↔ 模型」的**序列化契约**；
 *   本文件 = 「一张锚点清单 → 统计 / 筛选 / 徽标」的**聚合口径**。
 *   ⛔ 聚合口径不得再回到组件里内联 —— 否则又变成多份实现。
 */

import {
  parseSourceSpec,
  summarizeSourceSpec,
  type SourceSpecModel,
} from './sourceSpec'

/** `YzhStatusBadge` 的 4 语义档（S08） */
export type AnchorTone = 'success' | 'warning' | 'danger' | 'info'

/** 一行锚点的「解析后视图」—— `SourceSpec` 只解析一次，结果挂在这里 */
export interface AnchorView {
  /** 原始整行（PascalCase），保存时**必须整行提交**（`uk_tpl_anchor` 是整行 upsert） */
  row: any
  /** 解析后的来源链模型 */
  model: SourceSpecModel
  /** `SourceSpec` 是半截 JSON / 非对象 —— 保存会覆盖，界面须提示 */
  parseError: boolean
  /** 域自动值（页码 / 日期）—— 由扫描器给定，**不需要**配来源 */
  isDomainAuto: boolean
  /** 既非域自动值、又没有任何来源 ⇒ 未配 */
  unconfigured: boolean
  /** 模板里已找不到该锚点（模板被换过 / 标签被删） */
  orphan: boolean
  /** 「取值来源」一行摘要（未配时为空串） */
  summary: string
}

/**
 * 域自动值的判据。
 *
 * ⚠️ 这两个字段必须**同时**成立：`AnchorType==='domain'` 且 `DomainKind==='auto'`。
 *   只判 `AnchorType` 会把「域字段但需人工指定」的锚点也放行，
 *   让它们永远不显示「未配来源」——这正是原来三份实现里最容易写歪的一处。
 */
export function isDomainAutoRow(row: any): boolean {
  return (
    String(row?.AnchorType ?? '') === 'domain' &&
    String(row?.DomainKind ?? '') === 'auto'
  )
}

/** 把一行锚点解析成视图（**唯一**的解析入口） */
export function toAnchorView(row: any): AnchorView {
  const { model, parseError } = parseSourceSpec(row?.SourceSpec)
  const isDomainAuto = isDomainAutoRow(row)
  return {
    row,
    model,
    parseError,
    isDomainAuto,
    unconfigured: !isDomainAuto && !model.source.kind,
    orphan: !!row?.IsOrphan,
    summary: summarizeSourceSpec(model),
  }
}

/** 批量解析（一行一次） */
export function buildAnchorViews(rows: any[] | null | undefined): AnchorView[] {
  return (rows ?? []).map(toAnchorView)
}

/** 统计条的数据（`AnchorRuleTab` 顶部那排 chip） */
export interface AnchorStats {
  total: number
  /** 来源是「自动」的锚点数（全局参数 / AI 三类） */
  auto: number
  /** 来源是「人工填写」的锚点数 */
  manual: number
  unconfigured: number
  orphan: number
  required: number
}

export function computeAnchorStats(views: AnchorView[]): AnchorStats {
  const stats: AnchorStats = {
    total: views.length,
    auto: 0,
    manual: 0,
    unconfigured: 0,
    orphan: 0,
    required: 0,
  }
  for (const v of views) {
    if (v.orphan) stats.orphan++
    if (v.unconfigured) stats.unconfigured++
    if (v.row?.Required) stats.required++
    // ★ 2026-10-09：来源模型收敛为「一个锚点 = 一个来源」⇒ 按锚点计数，⛔ 不再按来源条目累加。
    //   ⛔ 旧口径的 `compute`（计算来源）已随「来源链」整层删除，不再统计。
    const s = v.model.source
    if (!s) continue
    if (s.kind === 'manual') stats.manual++
    else stats.auto++
  }
  return stats
}

/** 快捷筛选（筛选器 chip 的取值域） */
export type AnchorQuickFilter = 'all' | 'unconfigured' | 'required' | 'orphan'

/** 筛选器定义（`count` 由 `stats` 取，⛔ 不在模板里现算） */
export const ANCHOR_QUICK_FILTERS: {
  value: AnchorQuickFilter
  label: string
}[] = [
  { value: 'all', label: '全部' },
  { value: 'unconfigured', label: '未配来源' },
  { value: 'required', label: '必填' },
  { value: 'orphan', label: '孤儿锚点' },
]

export function filterAnchorViews(
  views: AnchorView[],
  filter: AnchorQuickFilter,
): AnchorView[] {
  switch (filter) {
    case 'unconfigured':
      return views.filter((v) => v.unconfigured)
    case 'required':
      return views.filter((v) => !!v.row?.Required)
    case 'orphan':
      return views.filter((v) => v.orphan)
    default:
      return views
  }
}

/** 筛选 chip 上的计数 */
export function filterCount(
  stats: AnchorStats,
  filter: AnchorQuickFilter,
): number {
  switch (filter) {
    case 'unconfigured':
      return stats.unconfigured
    case 'required':
      return stats.required
    case 'orphan':
      return stats.orphan
    default:
      return stats.total
  }
}

/**
 * 卡片右上角的状态徽标。
 *
 * 【为什么有优先级】
 *   一个锚点可能同时「孤儿」且「未配」。此时**孤儿更紧急** ——
 *   它意味着模板里已经没有这个标签了，继续配来源是白费功夫。
 *   顺序：孤儿 > 未配 > 已配(必填) > 已配。
 */
export function anchorBadge(v: AnchorView): { text: string; tone: AnchorTone } {
  if (v.orphan) return { text: '孤儿', tone: 'warning' }
  if (v.unconfigured) return { text: '未配来源', tone: 'danger' }
  if (v.row?.Required) return { text: '已配 · 必填', tone: 'success' }
  return { text: '已配', tone: 'info' }
}
