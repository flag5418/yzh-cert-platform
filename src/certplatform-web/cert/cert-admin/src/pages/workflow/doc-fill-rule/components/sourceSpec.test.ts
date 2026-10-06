/**
 * 取值来源链（`SourceSpec`）前端模型 —— 单测
 *
 * 【为什么这个文件最该被单测】
 *   头注释自己写明了三条契约，每一条错了都是**静默**的：
 *   ① 「解析 / 生成必须成对」—— 一处放宽一处收紧 = 保存时**悄悄丢字段**；
 *   ② 「人话预览是防错的关键」—— 文案错 = 实施人员按错的理解配规则；
 *   ③ 「未实现的来源必须显式标注」—— 标错 = 用户配了一个运行期必然失败的来源。
 *   三条都**不会报错**，只会让人配错。所以往返一致性 + 文案 + 实现标记都要钉住。
 */
import { describe, it, expect } from 'vitest'
import {
  COMBINE_MODES,
  DEFAULT_ON_MISSING,
  ON_MISSING_OPTIONS,
  SOURCE_KINDS,
  emptySourceSpec,
  entrySummary,
  humanPreview,
  isImplementedKind,
  kindLabel,
  parseSourceSpec,
  stringifySourceSpec,
  summarizeSourceSpec,
  type SourceSpecModel,
} from './sourceSpec'

// ────────────────────────────────────────────────
// 受控值表
// ────────────────────────────────────────────────

describe('受控值表', () => {
  it('组合方式恰好三种', () => {
    expect(COMBINE_MODES.map((m) => m.value)).toEqual([
      'firstHit',
      'concat',
      'template',
    ])
  })

  it('缺失行为四档，默认 next', () => {
    expect(ON_MISSING_OPTIONS.map((o) => o.value)).toEqual([
      'next',
      'todo',
      'block',
      'empty',
    ])
    expect(DEFAULT_ON_MISSING).toBe('next')
  })

  it('★ compute 必须标「未实现」—— 没有任何 src_compute 技能，执行器命中即 Fail', () => {
    expect(isImplementedKind('compute')).toBe(false)
    const compute = SOURCE_KINDS.find((k) => k.value === 'compute')
    expect(compute?.implemented).toBe(false)
  })

  it('已落地 Skill 的来源标「已实现」：global / manual / ai', () => {
    expect(isImplementedKind('global')).toBe(true)
    expect(isImplementedKind('manual')).toBe(true)
    expect(isImplementedKind('ai')).toBe(true)
  })

  it('未实现：profile / self / sibling / compute', () => {
    for (const k of ['profile', 'self', 'sibling', 'compute']) {
      expect(isImplementedKind(k)).toBe(false)
    }
  })

  it('未知 kind ⇒ 一律按未实现处理（⛔ 不默认放行）', () => {
    expect(isImplementedKind('whatever')).toBe(false)
  })

  it('kindLabel 有中文名则用中文名，否则回落原值', () => {
    expect(kindLabel('global')).toBe('全局参数')
    expect(kindLabel('zzz')).toBe('zzz')
  })
})

// ────────────────────────────────────────────────
// parseSourceSpec —— 绝不抛异常
// ────────────────────────────────────────────────

describe('parseSourceSpec', () => {
  it('空 / null / 空白 ⇒ 空模型且 parseError=false（「没配」不是「坏了」）', () => {
    for (const raw of [null, undefined, '', '   ']) {
      const r = parseSourceSpec(raw as any)
      expect(r.parseError).toBe(false)
      expect(r.model).toEqual({ combine: 'firstHit', sources: [] })
    }
  })

  it('半截 JSON ⇒ 空模型 + parseError=true（⛔ 不抛，否则整个侧边栏打不开）', () => {
    const r = parseSourceSpec('{"combine": "firstHit", "sources": [')
    expect(r.parseError).toBe(true)
    expect(r.model.sources).toEqual([])
  })

  it('非对象字面量（数字 / 字符串 / null）⇒ parseError=true', () => {
    expect(parseSourceSpec('123').parseError).toBe(true)
    expect(parseSourceSpec('"abc"').parseError).toBe(true)
    expect(parseSourceSpec('null').parseError).toBe(true)
  })

  it('完整 JSON 逐字段还原（字段名与后端逐字一致，⛔ 不做驼峰转换）', () => {
    const { model, parseError } = parseSourceSpec(
      JSON.stringify({
        combine: 'concat',
        separator: '；',
        sources: [
          { kind: 'global', ref: 'ENT_NAME', onMissing: 'next' },
          {
            kind: 'profile',
            ref: '营业执照',
            field: '统一社会信用代码',
            minConfidence: 0.85,
          },
          { kind: 'manual', onMissing: 'todo' },
        ],
      }),
    )
    expect(parseError).toBe(false)
    expect(model.combine).toBe('concat')
    expect(model.separator).toBe('；')
    expect(model.sources).toHaveLength(3)
    expect(model.sources[1]).toEqual({
      kind: 'profile',
      ref: '营业执照',
      field: '统一社会信用代码',
      minConfidence: 0.85,
      promptGroup: undefined,
      onMissing: undefined,
    })
  })

  it('未知 combine ⇒ 回落 firstHit（⛔ 不把非法值透传给后端）', () => {
    expect(parseSourceSpec('{"combine":"nonsense"}').model.combine).toBe(
      'firstHit',
    )
  })

  it('sources 非数组 ⇒ 空数组；数组里的非对象项被丢弃', () => {
    expect(parseSourceSpec('{"sources":"x"}').model.sources).toEqual([])
    const { model } = parseSourceSpec(
      '{"sources":[null,1,"a",{"kind":"global","ref":"P"}]}',
    )
    expect(model.sources).toEqual([
      {
        kind: 'global',
        ref: 'P',
        field: undefined,
        minConfidence: undefined,
        promptGroup: undefined,
        onMissing: undefined,
      },
    ])
  })

  it('条目缺 kind ⇒ 回落 global', () => {
    expect(parseSourceSpec('{"sources":[{}]}').model.sources[0].kind).toBe(
      'global',
    )
  })

  it('minConfidence 非数字 ⇒ 丢弃（⛔ 不把字符串塞进数值位）', () => {
    const { model } = parseSourceSpec(
      '{"sources":[{"kind":"profile","minConfidence":"0.9"}]}',
    )
    expect(model.sources[0].minConfidence).toBeUndefined()
  })
})

// ────────────────────────────────────────────────
// stringifySourceSpec
// ────────────────────────────────────────────────

describe('stringifySourceSpec', () => {
  it('无来源 ⇒ null（清空该列，而不是存 {"sources":[]}）', () => {
    expect(stringifySourceSpec(emptySourceSpec())).toBeNull()
    expect(
      stringifySourceSpec({ combine: 'firstHit', sources: [] }),
    ).toBeNull()
  })

  it('只为 concat 写 separator、只为 template 写 expr（⛔ 不写无关字段）', () => {
    const s1 = JSON.parse(
      stringifySourceSpec({
        combine: 'firstHit',
        separator: '；',
        expr: '{{1}}',
        sources: [{ kind: 'global', ref: 'P' }],
      })!,
    )
    expect(s1).not.toHaveProperty('separator')
    expect(s1).not.toHaveProperty('expr')

    const s2 = JSON.parse(
      stringifySourceSpec({
        combine: 'concat',
        sources: [{ kind: 'global', ref: 'P' }],
      })!,
    )
    expect(s2.separator).toBe('、') // 缺省顿号

    const s3 = JSON.parse(
      stringifySourceSpec({
        combine: 'template',
        sources: [{ kind: 'global', ref: 'P' }],
      })!,
    )
    expect(s3.expr).toBe('')
  })

  it('每条来源统一补 onMissing 默认值（让 JSON 自解释）', () => {
    const out = JSON.parse(
      stringifySourceSpec({
        combine: 'firstHit',
        sources: [{ kind: 'global', ref: 'P' }],
      })!,
    )
    expect(out.sources[0].onMissing).toBe(DEFAULT_ON_MISSING)
  })

  it('field / minConfidence 只写 profile；promptGroup 只写 ai', () => {
    const out = JSON.parse(
      stringifySourceSpec({
        combine: 'firstHit',
        sources: [
          { kind: 'global', ref: 'P', field: 'F', minConfidence: 0.5, promptGroup: 'G' },
          { kind: 'ai', ref: 'PC', promptGroup: 'G1' },
        ],
      })!,
    )
    expect(out.sources[0]).not.toHaveProperty('field')
    expect(out.sources[0]).not.toHaveProperty('minConfidence')
    expect(out.sources[0]).not.toHaveProperty('promptGroup')
    expect(out.sources[1].promptGroup).toBe('G1')
  })
})

// ────────────────────────────────────────────────
// ★ 成对性（本文件最该测的一条）
// ────────────────────────────────────────────────

describe('parse ↔ stringify 往返一致性', () => {
  const cases: SourceSpecModel[] = [
    {
      combine: 'firstHit',
      sources: [
        { kind: 'global', ref: 'ENT_NAME', onMissing: 'next' },
        { kind: 'ai', ref: 'PC', onMissing: 'next' },
        { kind: 'manual', onMissing: 'todo' },
      ],
    },
    {
      combine: 'concat',
      separator: '；',
      sources: [
        { kind: 'global', ref: 'A', onMissing: 'next' },
        { kind: 'global', ref: 'B', onMissing: 'next' },
      ],
    },
    {
      combine: 'template',
      expr: '{{1}}（{{2}}）',
      sources: [
        { kind: 'global', ref: 'A', onMissing: 'block' },
        { kind: 'global', ref: 'B', onMissing: 'empty' },
      ],
    },
    {
      combine: 'firstHit',
      sources: [
        {
          kind: 'profile',
          ref: '营业执照',
          field: '统一社会信用代码',
          minConfidence: 0.85,
          onMissing: 'next',
        },
      ],
    },
  ]

  it.each(cases.map((c) => [c.combine, c] as const))(
    'stringify → parse 不丢字段（%s）',
    (_name, model) => {
      const text = stringifySourceSpec(model)
      expect(text).not.toBeNull()
      const { model: back, parseError } = parseSourceSpec(text)
      expect(parseError).toBe(false)
      expect(back).toEqual(model)
    },
  )

  it('parse → stringify → parse 是幂等的（第二次不再变化）', () => {
    const raw = JSON.stringify({
      combine: 'firstHit',
      sources: [
        { kind: 'global', ref: 'ENT_NAME' },
        { kind: 'manual', onMissing: 'todo' },
      ],
    })
    const once = stringifySourceSpec(parseSourceSpec(raw).model)!
    const twice = stringifySourceSpec(parseSourceSpec(once).model)!
    expect(twice).toBe(once)
  })

  it('⚠️ 已知不对称：combine 非 concat 时 separator 会被丢弃（刻意的规范化）', () => {
    const model: SourceSpecModel = {
      combine: 'firstHit',
      separator: '；',
      sources: [{ kind: 'global', ref: 'P', onMissing: 'next' }],
    }
    const back = parseSourceSpec(stringifySourceSpec(model)!).model
    expect(back.separator).toBeUndefined()
  })

  it('⚠️ 已知不对称：kind 非 ai 时 promptGroup 会被丢弃', () => {
    const model: SourceSpecModel = {
      combine: 'firstHit',
      sources: [{ kind: 'global', ref: 'P', promptGroup: 'G' }],
    }
    const back = parseSourceSpec(stringifySourceSpec(model)!).model
    expect(back.sources[0].promptGroup).toBeUndefined()
  })
})

// ────────────────────────────────────────────────
// 摘要与人话预览
// ────────────────────────────────────────────────

describe('entrySummary', () => {
  it('各类来源的一行摘要', () => {
    expect(entrySummary({ kind: 'global', ref: 'ENT_NAME' })).toBe(
      '全局参数:ENT_NAME',
    )
    expect(entrySummary({ kind: 'manual' })).toBe('人工录入')
    expect(
      entrySummary({ kind: 'profile', ref: '营业执照', field: '代码' }),
    ).toBe('画像:营业执照.代码（未实现）')
    expect(entrySummary({ kind: 'ai', ref: 'PC', promptGroup: 'G' })).toBe(
      'AI:PC[组:G]',
    )
    expect(entrySummary({ kind: 'global' })).toBe('全局参数:?')
  })

  it('★ 未实现的来源必须显式标注「（未实现）」', () => {
    expect(entrySummary({ kind: 'self', ref: 'A' })).toBe('本模板:A（未实现）')
    expect(entrySummary({ kind: 'sibling', ref: 'B' })).toBe(
      '兄弟文档:B（未实现）',
    )
    expect(entrySummary({ kind: 'compute', ref: 'C' })).toBe(
      '计算:C（未实现）',
    )
  })

  it('已实现的来源不加标注', () => {
    expect(entrySummary({ kind: 'global', ref: 'P' })).not.toContain('未实现')
  })
})

describe('summarizeSourceSpec', () => {
  it('无来源 ⇒ 空串', () => {
    expect(summarizeSourceSpec(emptySourceSpec())).toBe('')
  })

  it('firstHit 用 → 串成回退链', () => {
    expect(
      summarizeSourceSpec({
        combine: 'firstHit',
        sources: [
          { kind: 'global', ref: 'A' },
          { kind: 'manual' },
        ],
      }),
    ).toBe('全局参数:A → 人工录入')
  })

  it('concat 显示分隔符', () => {
    expect(
      summarizeSourceSpec({
        combine: 'concat',
        separator: '；',
        sources: [
          { kind: 'global', ref: 'A' },
          { kind: 'global', ref: 'B' },
        ],
      }),
    ).toBe('拼接(；): 全局参数:A + 全局参数:B')
  })

  it('template 显示表达式，未填时给出占位提示', () => {
    expect(
      summarizeSourceSpec({
        combine: 'template',
        sources: [{ kind: 'global', ref: 'A' }],
      }),
    ).toBe('模板套用: (未填模板) ← 全局参数:A')
  })
})

describe('humanPreview', () => {
  it('无来源 ⇒ 明说运行期会留空', () => {
    expect(humanPreview(emptySourceSpec())).toBe(
      '尚未配置取值来源 —— 运行期该锚点将留空。',
    )
  })

  it('firstHit：逐条翻成「先取 X，取不到就…」，末条默认「留空」', () => {
    const text = humanPreview({
      combine: 'firstHit',
      sources: [
        { kind: 'global', ref: 'ENT_NAME', onMissing: 'next' },
        { kind: 'manual', onMissing: 'todo' },
      ],
    })
    expect(text).toBe(
      '先取 1. 全局参数（ENT_NAME），取不到就继续往下找；' +
        '先取 2. 人工录入，取不到就挂人工待办。',
    )
  })

  it('firstHit：末条 onMissing 缺省 ⇒ 说「留空」（⛔ 不说「继续往下找」，那会误导）', () => {
    const text = humanPreview({
      combine: 'firstHit',
      sources: [{ kind: 'global', ref: 'A' }],
    })
    expect(text).toBe('先取 1. 全局参数（A），取不到就留空。')
  })

  it('firstHit：block / empty 各自成句', () => {
    expect(
      humanPreview({
        combine: 'firstHit',
        sources: [{ kind: 'global', ref: 'A', onMissing: 'block' }],
      }),
    ).toContain('取不到就阻断（整份文档不生成）')
    expect(
      humanPreview({
        combine: 'firstHit',
        sources: [{ kind: 'global', ref: 'A', onMissing: 'empty' }],
      }),
    ).toContain('取不到就留空')
  })

  it('concat：说清用什么分隔符拼几段', () => {
    expect(
      humanPreview({
        combine: 'concat',
        separator: '；',
        sources: [
          { kind: 'global', ref: 'A' },
          { kind: 'manual' },
        ],
      }),
    ).toBe('把 1. 全局参数（A） 与 2. 人工录入 用「；」拼成一段文字。')
  })

  it('template：显示表达式，未填时给出 (未填)', () => {
    expect(
      humanPreview({
        combine: 'template',
        sources: [{ kind: 'global', ref: 'A' }],
      }),
    ).toBe('按模板「(未填)」套用 1. 全局参数（A）。')
  })

  it('没有 ref 的来源不显示空括号', () => {
    expect(
      humanPreview({
        combine: 'firstHit',
        sources: [{ kind: 'manual' }],
      }),
    ).toBe('先取 1. 人工录入，取不到就留空。')
  })
})
