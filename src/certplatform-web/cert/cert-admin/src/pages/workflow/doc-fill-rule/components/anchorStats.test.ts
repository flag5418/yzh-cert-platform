import { describe, expect, it } from 'vitest'
import {
  ANCHOR_QUICK_FILTERS,
  anchorBadge,
  buildAnchorViews,
  computeAnchorStats,
  filterAnchorViews,
  filterCount,
  isDomainAutoRow,
  toAnchorView,
} from './anchorStats'

/* ============================================================
   夹具
   ============================================================ */

/** 一行锚点（PascalCase，与后端整行一致） */
function row(over: Record<string, any> = {}): any {
  return {
    Code: 'A1',
    AnchorRef: '企业名称',
    AnchorType: 'scalar',
    SourceSpec: null,
    Required: false,
    IsOrphan: false,
    ...over,
  }
}

/** 一条 `SourceSpec` JSON 串 */
function spec(
  sources: any[],
  combine = 'firstHit',
): string {
  return JSON.stringify({ combine, sources })
}

/* ============================================================
   isDomainAutoRow —— 域自动值判据
   ============================================================ */
describe('isDomainAutoRow', () => {
  it('domain + auto 才算域自动值', () => {
    expect(isDomainAutoRow({ AnchorType: 'domain', DomainKind: 'auto' })).toBe(
      true,
    )
  })

  it('domain 但 DomainKind 不是 auto ⇒ 不是域自动值', () => {
    expect(isDomainAutoRow({ AnchorType: 'domain', DomainKind: 'manual' })).toBe(
      false,
    )
  })

  it('⛔ 只判 AnchorType 是不够的：非 domain 一律 false', () => {
    expect(isDomainAutoRow({ AnchorType: 'scalar', DomainKind: 'auto' })).toBe(
      false,
    )
  })

  it('DomainKind 缺省 ⇒ false（不能把「没写」当成 auto）', () => {
    expect(isDomainAutoRow({ AnchorType: 'domain' })).toBe(false)
  })

  it('null / undefined / 空对象都不炸', () => {
    expect(isDomainAutoRow(null)).toBe(false)
    expect(isDomainAutoRow(undefined)).toBe(false)
    expect(isDomainAutoRow({})).toBe(false)
  })

  it('大小写敏感 —— 后端给的是小写字面量', () => {
    expect(isDomainAutoRow({ AnchorType: 'Domain', DomainKind: 'Auto' })).toBe(
      false,
    )
  })
})

/* ============================================================
   toAnchorView —— 唯一解析入口
   ============================================================ */
describe('toAnchorView', () => {
  it('SourceSpec 为空 ⇒ 未配来源，摘要为空串', () => {
    const v = toAnchorView(row())
    expect(v.unconfigured).toBe(true)
    expect(v.summary).toBe('')
    expect(v.parseError).toBe(false)
    expect(v.model.sources).toEqual([])
  })

  it('有来源 ⇒ 未配为 false，且摘要非空', () => {
    const v = toAnchorView(
      row({
        SourceSpec: spec([{ kind: 'global', ref: 'ENT_NAME' }]),
      }),
    )
    expect(v.unconfigured).toBe(false)
    expect(v.summary).toContain('ENT_NAME')
  })

  it('★ 域自动值没有来源也**不算未配**（页码/日期由扫描器给）', () => {
    const v = toAnchorView(
      row({ AnchorType: 'domain', DomainKind: 'auto', SourceSpec: null }),
    )
    expect(v.isDomainAuto).toBe(true)
    expect(v.unconfigured).toBe(false)
  })

  it('域自动值**配了**来源也不算未配（是否该配由 combos 校验管，不在这里判）', () => {
    const v = toAnchorView(
      row({
        AnchorType: 'domain',
        DomainKind: 'auto',
        SourceSpec: spec([{ kind: 'global', ref: 'X' }]),
      }),
    )
    expect(v.unconfigured).toBe(false)
    expect(v.model.sources).toHaveLength(1)
  })

  it('IsOrphan ⇒ orphan', () => {
    expect(toAnchorView(row({ IsOrphan: true })).orphan).toBe(true)
    expect(toAnchorView(row({ IsOrphan: 0 })).orphan).toBe(false)
  })

  it('★ 半截 JSON ⇒ parseError 且回落成未配（界面须提示原值会被覆盖）', () => {
    const v = toAnchorView(row({ SourceSpec: '{"combine":"first' }))
    expect(v.parseError).toBe(true)
    expect(v.unconfigured).toBe(true)
    expect(v.model.sources).toEqual([])
  })

  it('JSON 合法但不是对象（如 "abc"）⇒ parseError', () => {
    expect(toAnchorView(row({ SourceSpec: '"abc"' })).parseError).toBe(true)
  })

  it('⛔ 解析失败**绝不抛异常**（抛了整张清单就打不开）', () => {
    expect(() => toAnchorView(row({ SourceSpec: '{{{' }))).not.toThrow()
  })

  it('保留 row 的**原引用** —— 保存时按 uk_tpl_anchor 整行 upsert，必须能拿到完整行', () => {
    const r = row({ SourceSpec: spec([{ kind: 'manual' }]), Remark: '备注' })
    const v = toAnchorView(r)
    expect(v.row).toBe(r)
    expect(v.row.Remark).toBe('备注')
  })
})

/* ============================================================
   buildAnchorViews
   ============================================================ */
describe('buildAnchorViews', () => {
  it('null / undefined / [] ⇒ 空数组', () => {
    expect(buildAnchorViews(null)).toEqual([])
    expect(buildAnchorViews(undefined)).toEqual([])
    expect(buildAnchorViews([])).toEqual([])
  })

  it('长度与顺序保持不变', () => {
    const rows = [row({ Code: 'A' }), row({ Code: 'B' }), row({ Code: 'C' })]
    expect(buildAnchorViews(rows).map((v) => v.row.Code)).toEqual([
      'A',
      'B',
      'C',
    ])
  })
})

/* ============================================================
   computeAnchorStats
   ============================================================ */
describe('computeAnchorStats', () => {
  it('空清单 ⇒ 全 0', () => {
    expect(computeAnchorStats([])).toEqual({
      total: 0,
      auto: 0,
      manual: 0,
      compute: 0,
      unconfigured: 0,
      orphan: 0,
      required: 0,
    })
  })

  it('total / required 直接来自行', () => {
    const s = computeAnchorStats(
      buildAnchorViews([
        row({ Required: true }),
        row({ Required: false }),
        row({ Required: 1 }),
      ]),
    )
    expect(s.total).toBe(3)
    expect(s.required).toBe(2)
  })

  it('来源按 kind 三分类：auto（其余）/ manual / compute', () => {
    const s = computeAnchorStats(
      buildAnchorViews([
        row({
          SourceSpec: spec([
            { kind: 'global', ref: 'A' },
            { kind: 'manual' },
            { kind: 'compute', ref: 'B' },
            { kind: 'ai', ref: 'C' },
          ]),
        }),
      ]),
    )
    expect(s.auto).toBe(2) // global + ai
    expect(s.manual).toBe(1)
    expect(s.compute).toBe(1)
  })

  it('★ 一行多来源**逐个计数**（不是「有来源就算 1」）', () => {
    const s = computeAnchorStats(
      buildAnchorViews([
        row({ SourceSpec: spec([{ kind: 'global' }, { kind: 'global' }]) }),
        row({ SourceSpec: spec([{ kind: 'global' }]) }),
      ]),
    )
    expect(s.auto).toBe(3)
  })

  it('unconfigured / orphan 分别计数', () => {
    const s = computeAnchorStats(
      buildAnchorViews([
        row({ Code: 'A' }), // 未配
        row({ Code: 'B', IsOrphan: true }), // 孤儿 + 未配
        row({ Code: 'C', SourceSpec: spec([{ kind: 'global' }]) }),
      ]),
    )
    expect(s.unconfigured).toBe(2)
    expect(s.orphan).toBe(1)
  })

  it('★ 域自动值不进 unconfigured', () => {
    const s = computeAnchorStats(
      buildAnchorViews([
        row({ AnchorType: 'domain', DomainKind: 'auto' }),
        row({ AnchorType: 'domain', DomainKind: 'manual' }), // 需人工 ⇒ 未配
      ]),
    )
    expect(s.unconfigured).toBe(1)
  })

  it('半截 JSON 的行也算未配（且计入 total）', () => {
    const s = computeAnchorStats(
      buildAnchorViews([row({ SourceSpec: '{bad' })]),
    )
    expect(s.total).toBe(1)
    expect(s.unconfigured).toBe(1)
  })
})

/* ============================================================
   filterAnchorViews / filterCount
   ============================================================ */
describe('filterAnchorViews', () => {
  const views = buildAnchorViews([
    row({ Code: 'OK', SourceSpec: spec([{ kind: 'global' }]), Required: true }),
    row({ Code: 'NOSRC' }), // 未配
    row({ Code: 'ORPHAN', IsOrphan: true }), // 孤儿 + 未配
    row({
      Code: 'OPT',
      SourceSpec: spec([{ kind: 'manual' }]),
      Required: false,
    }),
  ])
  const codes = (v: any[]) => v.map((x) => x.row.Code)

  it("'all' 原样返回（连数组引用都不换）", () => {
    expect(filterAnchorViews(views, 'all')).toBe(views)
  })

  it("'unconfigured' 只留未配", () => {
    expect(codes(filterAnchorViews(views, 'unconfigured'))).toEqual([
      'NOSRC',
      'ORPHAN',
    ])
  })

  it("'required' 只留必填", () => {
    expect(codes(filterAnchorViews(views, 'required'))).toEqual(['OK'])
  })

  it("'orphan' 只留孤儿", () => {
    expect(codes(filterAnchorViews(views, 'orphan'))).toEqual(['ORPHAN'])
  })

  it('筛选不改动原数组', () => {
    filterAnchorViews(views, 'required')
    expect(views).toHaveLength(4)
  })
})

describe('filterCount', () => {
  const stats = computeAnchorStats(
    buildAnchorViews([row(), row({ Required: true }), row({ IsOrphan: true })]),
  )

  it('每个筛选档取对应计数', () => {
    expect(filterCount(stats, 'all')).toBe(3)
    expect(filterCount(stats, 'unconfigured')).toBe(3)
    expect(filterCount(stats, 'required')).toBe(1)
    expect(filterCount(stats, 'orphan')).toBe(1)
  })

  it('计数口径与「筛出来的条数」严格相等（chip 上的数字必须能对上列表）', () => {
    const views = buildAnchorViews([
      row(),
      row({ Required: true }),
      row({ IsOrphan: true }),
    ])
    const s = computeAnchorStats(views)
    for (const f of ANCHOR_QUICK_FILTERS) {
      expect(filterCount(s, f.value)).toBe(
        filterAnchorViews(views, f.value).length,
      )
    }
  })
})

/* ============================================================
   ★ 口径唯一性回归
   ============================================================ */
describe('★ 未配判据只有一份实现（回归）', () => {
  it('视图 / 统计 / 筛选三处对「未配」的结论必须完全一致', () => {
    const views = buildAnchorViews([
      row({ Code: 'A' }), // 未配
      row({ Code: 'B', SourceSpec: spec([{ kind: 'global' }]) }),
      row({ Code: 'C', AnchorType: 'domain', DomainKind: 'auto' }),
      row({ Code: 'D', SourceSpec: '{"broken"' }), // 坏 JSON ⇒ 未配
    ])
    const stats = computeAnchorStats(views)
    const viaFilter = filterAnchorViews(views, 'unconfigured')
    const viaFlag = views.filter((v) => v.unconfigured)

    expect(stats.unconfigured).toBe(2)
    expect(viaFilter.length).toBe(stats.unconfigured)
    expect(viaFlag.length).toBe(stats.unconfigured)
    expect(viaFilter.every((v) => v.unconfigured)).toBe(true)
  })
})

/* ============================================================
   anchorBadge
   ============================================================ */
describe('anchorBadge', () => {
  it('孤儿优先于未配（模板里已无此标签，再配来源是白费功夫）', () => {
    const v = toAnchorView(row({ IsOrphan: true }))
    expect(v.unconfigured).toBe(true) // 两个条件同时成立
    expect(anchorBadge(v)).toEqual({ text: '孤儿', tone: 'warning' })
  })

  it('未配 ⇒ danger', () => {
    expect(anchorBadge(toAnchorView(row()))).toEqual({
      text: '未配来源',
      tone: 'danger',
    })
  })

  it('已配 + 必填 ⇒ success', () => {
    const v = toAnchorView(
      row({ SourceSpec: spec([{ kind: 'global' }]), Required: true }),
    )
    expect(anchorBadge(v)).toEqual({ text: '已配 · 必填', tone: 'success' })
  })

  it('已配 + 非必填 ⇒ info', () => {
    const v = toAnchorView(row({ SourceSpec: spec([{ kind: 'global' }]) }))
    expect(anchorBadge(v)).toEqual({ text: '已配', tone: 'info' })
  })

  it('tone 只会落在 YzhStatusBadge 的 4 档里（S08 契约）', () => {
    const tones = [
      anchorBadge(toAnchorView(row({ IsOrphan: true }))).tone,
      anchorBadge(toAnchorView(row())).tone,
      anchorBadge(
        toAnchorView(row({ SourceSpec: spec([{ kind: 'global' }]), Required: 1 })),
      ).tone,
      anchorBadge(toAnchorView(row({ SourceSpec: spec([{ kind: 'global' }]) })))
        .tone,
    ]
    expect(tones.every((t) => ['success', 'warning', 'danger', 'info'].includes(t))).toBe(
      true,
    )
  })
})

/* ============================================================
   ANCHOR_QUICK_FILTERS
   ============================================================ */
describe('ANCHOR_QUICK_FILTERS', () => {
  it('4 档且顺序固定（UI 上不允许悄悄改顺序/漏档）', () => {
    expect(ANCHOR_QUICK_FILTERS.map((f) => f.value)).toEqual([
      'all',
      'unconfigured',
      'required',
      'orphan',
    ])
  })

  it('每档都有中文标签', () => {
    expect(ANCHOR_QUICK_FILTERS.every((f) => f.label.length > 0)).toBe(true)
  })
})
