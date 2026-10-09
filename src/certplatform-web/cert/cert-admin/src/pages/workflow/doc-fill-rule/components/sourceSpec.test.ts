/**
 * 取值来源（`SourceSpec`）前端模型 —— 单测
 *
 * 【★ 2026-10-09 全量重写】旧断言测的是 `22` 号时代的**来源链**模型
 * （`combine` 三种组合方式 / `onMissing` 四档 / 7 种来源），
 * 而用户已裁定「**一个锚点 = 一个来源**」⇒ 那一整层被删除。
 * 本文件随之重写为**新模型**的断言，并**显式钉住「旧概念不许回来」**。
 *
 * 【为什么这个文件最该被单测】
 *   头注释自己写明了三条契约，每一条错了都是**静默**的：
 *   ① 「解析 / 生成必须成对」—— 一处放宽一处收紧 = 保存时**悄悄丢字段**；
 *   ② 「人话预览是防错的关键」—— 文案错 = 实施人员按错的理解配规则；
 *   ③ 「校验顺序 = 用户体验」—— 先报「为什么」= 用户不知道该做什么。
 *   三条都**不会报错**，只会让人配错。所以往返一致性 + 文案 + 校验顺序都要钉住。
 */
import { describe, it, expect } from 'vitest'
import {
  DEFAULT_WRITE_MODE,
  DOC_INFO_ITEMS,
  ENTERPRISE_ATTRS,
  ORG_SYS_ATTRS,
  SOURCE_KINDS,
  WRITE_MODES,
  aiParamRefs,
  cardOf,
  emptySourceSpec,
  entrySummary,
  findParam,
  humanPreview,
  isAiKind,
  isBlankSource,
  kindLabel,
  kindOfRef,
  labelOfRef,
  newEntry,
  paramCapLabel,
  parseSourceSpec,
  stringifySourceSpec,
  summarizeSourceSpec,
  validateSource,
  writeModeLabel,
} from './sourceSpec'

// ────────────────────────────────────────────────
// 受控值表
// ────────────────────────────────────────────────

describe('受控值表', () => {
  it('★ 来源恰好 6 种，顺序即 UI 顺序', () => {
    // ★ 2026-10-09 用户裁决「可以」⇒ 「文档信息」（`headerFooter`）为第 6 种。
    //   没有它，`{{阶段名称}}` / `{{标准号}}` 这类锚点在本页根本配不出来。
    expect(SOURCE_KINDS.map((k) => k.value)).toEqual([
      'global',
      'headerFooter',
      'ai_semantic',
      'ai_field',
      'ai_table',
      'manual',
    ])
  })

  it('★ 每条来源都有中文名与非空说明（卡片要渲染两行）', () => {
    for (const k of SOURCE_KINDS) {
      expect(k.label.length).toBeGreaterThan(0)
      expect(k.hint.length).toBeGreaterThan(0)
    }
  })

  it('★ 写入方式恰好 2 种：覆盖 / 填充', () => {
    expect(WRITE_MODES.map((m) => m.value)).toEqual(['overwrite', 'replace'])
    expect(WRITE_MODES.map((m) => m.label)).toEqual(['覆盖', '填充'])
    expect(DEFAULT_WRITE_MODE).toBe('overwrite')
  })

  it('writeModeLabel：认识的值出中文，未知值回落原值，空值出「覆盖」', () => {
    expect(writeModeLabel('overwrite')).toBe('覆盖')
    expect(writeModeLabel('replace')).toBe('填充')
    expect(writeModeLabel('zzz')).toBe('zzz')
    expect(writeModeLabel(null)).toBe('覆盖')
  })

  it('kindLabel 有中文名则用中文名，否则回落原值', () => {
    expect(kindLabel('global')).toBe('全局参数')
    expect(kindLabel('ai_field')).toBe('AI 字段')
    expect(kindLabel('zzz')).toBe('zzz')
  })

  it('isAiKind 只认三类 AI', () => {
    expect(isAiKind('ai_semantic')).toBe(true)
    expect(isAiKind('ai_field')).toBe(true)
    expect(isAiKind('ai_table')).toBe(true)
    expect(isAiKind('global')).toBe(false)
    expect(isAiKind('manual')).toBe(false)
    expect(isAiKind('ai')).toBe(false) // 旧值不算「新三类」
    expect(isAiKind('')).toBe(false)
  })
})

// ────────────────────────────────────────────────
// ★ 负断言：旧概念不许回来
// ────────────────────────────────────────────────

describe('★ 旧概念已被删除（⛔ 不许回归）', () => {
  it('来源里没有 profile / compute / self / sibling / ai（旧值）', () => {
    const kinds = SOURCE_KINDS.map((k) => k.value)
    for (const dead of ['profile', 'compute', 'self', 'sibling', 'ai']) {
      expect(kinds).not.toContain(dead)
    }
  })

  it('导出面上没有「组合方式 / 缺失行为 / 实现标记」这三套旧 API', async () => {
    const mod: any = await import('./sourceSpec')
    for (const dead of [
      'COMBINE_MODES',
      'ON_MISSING_OPTIONS',
      'DEFAULT_ON_MISSING',
      'isImplementedKind',
    ]) {
      expect(mod[dead]).toBeUndefined()
    }
  })

  it('★ 生成的 JSON 里没有 combine / onMissing / separator / expr', () => {
    const json = stringifySourceSpec({
      source: { kind: 'global', ref: 'ENT_NAME' },
    })!
    const obj = JSON.parse(json)
    for (const dead of ['combine', 'onMissing', 'separator', 'expr']) {
      expect(obj).not.toHaveProperty(dead)
    }
  })
})

// ────────────────────────────────────────────────
// ★ 卡片 ↔ 存储 kind 的映射（本轮「合并」的枢轴）
// ────────────────────────────────────────────────

describe('★ kindOfRef / cardOf —— 卡片与存储 kind 的双向映射', () => {
  it('kindOfRef：`@` ⇒ headerFooter；三个点号命名空间 ⇒ replace；其余 ⇒ global', () => {
    expect(kindOfRef('@doc_no')).toBe('headerFooter')
    expect(kindOfRef('enterprise.Name')).toBe('replace')
    expect(kindOfRef('org.Name')).toBe('replace')
    expect(kindOfRef('system.date')).toBe('replace')
    expect(kindOfRef('ENT_NAME')).toBe('global')
    expect(kindOfRef('')).toBe('global')
    expect(kindOfRef(null)).toBe('global')
  })

  it('★ cardOf：`replace` 归到「全局参数」卡片下（用户裁决「需要合并」）', () => {
    expect(cardOf('replace')).toBe('global')
    expect(cardOf('global')).toBe('global')
    expect(cardOf('headerFooter')).toBe('headerFooter')
    expect(cardOf('manual')).toBe('manual')
    expect(cardOf('')).toBe('')
  })

  it('★ 已配 `enterprise.Name` 的锚点，卡片判据必须仍是「全局参数」', () => {
    // ⚠️ 回归点：`pickKind` 若拿 `src.kind`（= `replace`）去比卡片名（= `global`），
    //    会**误判成「换了来源」而把用户已配的 ref 清空**。所以必须走 `cardOf`。
    const stored = kindOfRef('enterprise.Name')
    expect(stored).toBe('replace')
    expect(cardOf(stored)).toBe('global')
  })
})

describe('★ 参数清单与取名（findParam vs labelOfRef 的分工）', () => {
  it('静态清单条数 = 引擎硬编码条数（改这里必须同步改 ReplaceResolver）', () => {
    expect(ENTERPRISE_ATTRS.length).toBe(16)
    expect(ORG_SYS_ATTRS.length).toBe(12)
    expect(DOC_INFO_ITEMS.length).toBe(6)
  })

  it('清单里每项都是 [ref, 中文名] 且无重复 ref', () => {
    for (const list of [ENTERPRISE_ATTRS, ORG_SYS_ATTRS, DOC_INFO_ITEMS]) {
      const refs = list.map((p) => p[0])
      expect(new Set(refs).size).toBe(refs.length)
      for (const [ref, label] of list) {
        expect(ref.length).toBeGreaterThan(0)
        expect(label.length).toBeGreaterThan(0)
      }
    }
  })

  it('findParam：命中静态 ⇒ 只读；命中动态 ⇒ 可覆盖；都没有 ⇒ 标「库里没有」', () => {
    expect(findParam('enterprise.Name')).toMatchObject({ kind: 'replace', ro: true })
    expect(findParam('ENT_NAME', [{ value: 'ENT_NAME', label: '企业名称' }])).toMatchObject(
      { kind: 'global', ro: false, label: '企业名称' },
    )
    expect(findParam('ENT_NAME')).toMatchObject({ kind: 'global', ro: false })
    expect(findParam('ENT_NAME')!.label).toContain('库里没有')
  })

  it('★ labelOfRef：⛔ 不判「库里没有」—— 列表/预览拿不到动态清单，判了就是假警报', () => {
    expect(labelOfRef('enterprise.Name')).toBe('企业全称')
    expect(labelOfRef('@doc_no')).toBe('文档编号')
    // 库里的参数（无动态清单）⇒ 原样回显编码，⛔ 不追加「库里没有」
    expect(labelOfRef('ENT_NAME')).toBe('ENT_NAME')
    expect(labelOfRef('')).toBe('')
  })

  it('paramCapLabel：只读 / 可覆盖 两档文案', () => {
    expect(paramCapLabel('enterprise.Name')).toBe('只读 · replace')
    expect(paramCapLabel('ENT_NAME')).toBe('可覆盖 · global')
  })
})

// ────────────────────────────────────────────────
// newEntry
// ────────────────────────────────────────────────

describe('newEntry', () => {
  it('global / manual 不带 AI 属性', () => {
    expect(newEntry('global')).toEqual({ kind: 'global' })
    expect(newEntry('manual')).toEqual({ kind: 'manual' })
  })

  it('★ AI 三类都预置「参数=无 / 依赖企业资料=是 / 提示词空 / 参数引用空数组」', () => {
    for (const k of ['ai_semantic', 'ai_field', 'ai_table']) {
      expect(newEntry(k)).toEqual({
        kind: k,
        hasParam: false,
        params: [],
        dependsOnEnterprise: true,
        prompt: '',
      })
    }
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
      expect(r.legacyMultiSource).toBe(false)
      expect(r.droppedCount).toBe(0)
      expect(r.model).toEqual({ source: { kind: '' } })
    }
  })

  it('半截 JSON ⇒ 空模型 + parseError=true（⛔ 不抛，否则整个侧边栏打不开）', () => {
    const r = parseSourceSpec('{"sources": [')
    expect(r.parseError).toBe(true)
    expect(r.model).toEqual({ source: { kind: '' } })
  })

  it('非对象字面量（数字 / 字符串 / null）⇒ parseError=true', () => {
    expect(parseSourceSpec('123').parseError).toBe(true)
    expect(parseSourceSpec('"abc"').parseError).toBe(true)
    expect(parseSourceSpec('null').parseError).toBe(true)
  })

  it('sources 不是数组 / 数组里没有对象 ⇒ 空模型，⛔ 不算 parseError', () => {
    expect(parseSourceSpec('{"sources":"x"}').model).toEqual({
      source: { kind: '' },
    })
    expect(parseSourceSpec('{"sources":[null,1]}').model).toEqual({
      source: { kind: '' },
    })
  })

  it('★ 单来源：原样取出 kind / ref', () => {
    const r = parseSourceSpec('{"sources":[{"kind":"global","ref":"ENT_NAME"}]}')
    expect(r.parseError).toBe(false)
    expect(r.legacyMultiSource).toBe(false)
    expect(r.model.source).toEqual({ kind: 'global', ref: 'ENT_NAME' })
  })

  it('★ AI 属性原样取出（含多参数 params）', () => {
    const r = parseSourceSpec(
      '{"sources":[{"kind":"ai_field","hasParam":true,"params":["P1","P2"],"dependsOnEnterprise":false,"prompt":"写一段"}]}',
    )
    expect(r.model.source).toEqual({
      kind: 'ai_field',
      hasParam: true,
      params: ['P1', 'P2'],
      dependsOnEnterprise: false,
      prompt: '写一段',
    })
  })

  it('★ 老数据（单 ref）解析时迁移成 params[0]', () => {
    const r = parseSourceSpec(
      '{"sources":[{"kind":"ai_field","hasParam":true,"ref":"P1","dependsOnEnterprise":false,"prompt":"写一段"}]}',
    )
    expect(r.model.source).toMatchObject({ kind: 'ai_field', hasParam: true, params: ['P1'] })
  })

  it('缺 kind ⇒ 回落 global（⛔ 不产生空 kind 的「幽灵条目」）', () => {
    expect(parseSourceSpec('{"sources":[{}]}').model.source.kind).toBe('global')
  })

  it('★ 旧数据（多来源链）⇒ 保留第 1 个 + 报 legacyMultiSource + droppedCount', () => {
    const r = parseSourceSpec(
      '{"combine":"firstHit","sources":[{"kind":"global","ref":"A"},{"kind":"manual"},{"kind":"profile","ref":"X"}]}',
    )
    expect(r.legacyMultiSource).toBe(true)
    expect(r.droppedCount).toBe(2)
    expect(r.model.source).toEqual({ kind: 'global', ref: 'A' })
  })

  it('旧数据里被丢弃的 profile 不会污染新模型', () => {
    const r = parseSourceSpec(
      '{"sources":[{"kind":"manual"},{"kind":"profile","ref":"营业执照","field":"统一社会信用代码"}]}',
    )
    expect(r.model.source).toEqual({ kind: 'manual' })
    expect(r.model.source).not.toHaveProperty('field')
  })
})

// ────────────────────────────────────────────────
// stringifySourceSpec
// ────────────────────────────────────────────────

describe('stringifySourceSpec', () => {
  it('未选来源 ⇒ null（清空该列，⛔ 不存 {"sources":[]}）', () => {
    expect(stringifySourceSpec(emptySourceSpec())).toBeNull()
    expect(stringifySourceSpec({ source: { kind: '' } })).toBeNull()
  })

  it('★ global 只写 kind + ref', () => {
    const out = JSON.parse(
      stringifySourceSpec({ source: { kind: 'global', ref: 'ENT_NAME' } })!,
    )
    expect(out).toEqual({ sources: [{ kind: 'global', ref: 'ENT_NAME' }] })
    expect(out.sources).toHaveLength(1)
  })

  it('★ global 没选参数时不写 ref（⛔ 不写 ref:""）', () => {
    const out = JSON.parse(
      stringifySourceSpec({ source: { kind: 'global' } })!,
    )
    expect(out.sources[0]).toEqual({ kind: 'global' })
    expect(out.sources[0]).not.toHaveProperty('ref')
  })

  it('★ AI 写全属性（参数用 params 多选数组，⛔ 不再写单值 ref）', () => {
    const out = JSON.parse(
      stringifySourceSpec({
        source: {
          kind: 'ai_field',
          hasParam: true,
          params: ['P1', 'P2'],
          dependsOnEnterprise: true,
          prompt: '写一段企业概况',
        },
      })!,
    )
    expect(out.sources[0]).toEqual({
      kind: 'ai_field',
      hasParam: true,
      params: ['P1', 'P2'],
      dependsOnEnterprise: true,
      prompt: '写一段企业概况',
    })
    expect(out.sources[0]).not.toHaveProperty('ref')
  })

  it('★ AI 参数选「无」时 ⛔ 不写 ref（哪怕残留了 ref 值）', () => {
    const out = JSON.parse(
      stringifySourceSpec({
        source: {
          kind: 'ai_semantic',
          hasParam: false,
          ref: 'LEFTOVER',
          dependsOnEnterprise: true,
          prompt: 'x',
        },
      })!,
    )
    expect(out.sources[0]).not.toHaveProperty('ref')
  })

  it('manual 只写 kind', () => {
    const out = JSON.parse(
      stringifySourceSpec({ source: { kind: 'manual' } })!,
    )
    expect(out.sources[0]).toEqual({ kind: 'manual' })
  })
})

// ────────────────────────────────────────────────
// 往返一致（parse ∘ stringify = id）
// ────────────────────────────────────────────────

describe('往返一致性', () => {
  it('★ 六种来源逐个往返都不丢字段', () => {
    const cases = [
      // 可覆盖的全局参数（③④ 组）
      { kind: 'global', ref: 'ENT_NAME' },
      // ★ 只读引用（①② 组）—— 存的是 `replace`，⛔ 不是 `global`
      { kind: 'replace', ref: 'enterprise.Name' },
      { kind: 'replace', ref: 'system.date' },
      // ★ 文档信息（走 `@` token）
      { kind: 'headerFooter', ref: '@doc_no' },
      { kind: 'manual' },
      { kind: 'ai_semantic', hasParam: false, dependsOnEnterprise: true, prompt: 'A' },
      { kind: 'ai_field', hasParam: true, params: ['P1'], dependsOnEnterprise: false, prompt: 'B' },
      { kind: 'ai_table', hasParam: false, dependsOnEnterprise: true, prompt: 'C' },
    ]
    for (const source of cases) {
      const json = stringifySourceSpec({ source })!
      const back = parseSourceSpec(json).model.source
      expect(back).toEqual(source)
    }
  })

  it('★ 「全局参数」卡片按 ref 反推后端 kind —— 只读项存 `replace`，可覆盖项存 `global`', () => {
    // ①②（enterprise./org./system.）⇒ `replace`（只读，直读企业档案）
    expect(
      JSON.parse(
        stringifySourceSpec({ source: { kind: 'global', ref: 'enterprise.Name' } })!,
      ).sources[0],
    ).toEqual({ kind: 'replace', ref: 'enterprise.Name' })
    // ③④（库里的参数编码）⇒ `global`（可覆盖，读企业填的值）
    expect(
      JSON.parse(
        stringifySourceSpec({ source: { kind: 'global', ref: 'ENT_NAME' } })!,
      ).sources[0],
    ).toEqual({ kind: 'global', ref: 'ENT_NAME' })
  })

  it('AI 提示词为空串 ⇒ 往返后仍然存在（⛔ 不因 falsy 被吃掉）', () => {
    const json = stringifySourceSpec({
      source: { kind: 'ai_field', hasParam: false, dependsOnEnterprise: true, prompt: '' },
    })!
    const back = parseSourceSpec(json).model.source
    // 空串在 stringify 时被省略 ⇒ 解析回来 prompt 为 undefined，语义等价（都是「没填」）
    expect(back.prompt ?? '').toBe('')
  })
})

// ────────────────────────────────────────────────
// 摘要
// ────────────────────────────────────────────────

describe('摘要', () => {
  it('未选来源 ⇒ 空串（列表列不显示「未配置」字样，那是徽标的活）', () => {
    expect(entrySummary({ kind: '' })).toBe('')
    expect(summarizeSourceSpec(emptySourceSpec())).toBe('')
  })

  it('global 显示参数编码；未选参数时显式说明', () => {
    expect(entrySummary({ kind: 'global', ref: 'ENT_NAME' })).toBe(
      '全局参数：ENT_NAME',
    )
    expect(entrySummary({ kind: 'global' })).toContain('未选参数')
  })

  it('★ replace（只读项）摘要带「· 只读」，且用中文名而非 `enterprise.Name`', () => {
    expect(entrySummary({ kind: 'replace', ref: 'enterprise.Name' })).toBe(
      '全局参数：企业全称 · 只读',
    )
    expect(entrySummary({ kind: 'replace', ref: 'system.date' })).toBe(
      '全局参数：制表日期 yyyy-MM-dd · 只读',
    )
  })

  it('★ headerFooter 摘要出「文档信息：<中文名>」', () => {
    expect(entrySummary({ kind: 'headerFooter', ref: '@doc_no' })).toBe(
      '文档信息：文档编号',
    )
    expect(entrySummary({ kind: 'headerFooter' })).toContain('未选项')
  })

  it('manual 显示「人工填写」', () => {
    expect(entrySummary({ kind: 'manual' })).toBe('人工填写')
  })

  it('★ AI 摘要带上「参数 / 依赖企业资料」，让人一眼看出输入是什么', () => {
    expect(
      entrySummary({
        kind: 'ai_field',
        hasParam: true,
        ref: 'P1',
        dependsOnEnterprise: true,
      }),
    ).toBe('AI 字段（参数 P1 · 依赖企业资料）')
    expect(
      entrySummary({ kind: 'ai_semantic', hasParam: false, dependsOnEnterprise: false }),
    ).toBe('AI 语义生成')
  })
})

// ────────────────────────────────────────────────
// humanPreview
// ────────────────────────────────────────────────

describe('humanPreview', () => {
  it('未选来源 ⇒ 明确说「会留空并挂人工待办」', () => {
    expect(humanPreview(emptySourceSpec())).toContain('尚未配置来源')
    expect(humanPreview(emptySourceSpec())).toContain('留空')
  })

  it('global 且选了参数 ⇒ 说清取哪个参数', () => {
    const t = humanPreview({ source: { kind: 'global', ref: 'ENT_NAME' } })
    expect(t).toContain('ENT_NAME')
  })

  it('★ replace ⇒ 预览必须点明「只读」与「企业改档案即改文档」', () => {
    const t = humanPreview({ source: { kind: 'replace', ref: 'enterprise.Name' } })
    expect(t).toContain('企业全称')
    expect(t).toContain('只读')
  })

  it('★ headerFooter ⇒ 预览说清取哪个文档信息项', () => {
    const t = humanPreview({ source: { kind: 'headerFooter', ref: '@stage_name' } })
    expect(t).toContain('认证阶段')
  })

  it('★ headerFooter 没选项 ⇒ 指回「来源属性」', () => {
    expect(humanPreview({ source: { kind: 'headerFooter' } })).toContain('来源属性')
  })

  it('global 没选参数 ⇒ 指回「来源属性」，⛔ 不说「取不到就留空」（那会误导）', () => {
    const t = humanPreview({ source: { kind: 'global' } })
    expect(t).toContain('来源属性')
  })

  it('manual ⇒ 说清是专家填', () => {
    expect(humanPreview({ source: { kind: 'manual' } })).toContain('人工待办')
  })

  it('★ AI ⇒ 说清输入是什么 + 结果进建议池待确认', () => {
    const t = humanPreview({
      source: {
        kind: 'ai_field',
        hasParam: true,
        ref: 'P1',
        dependsOnEnterprise: true,
        prompt: 'x',
      },
    })
    expect(t).toContain('P1')
    expect(t).toContain('企业资料')
    expect(t).toContain('建议池')
  })

  it('★ AI 且没有任何输入 ⇒ 明确告警（⛔ 不假装能生成）', () => {
    const t = humanPreview({
      source: { kind: 'ai_semantic', hasParam: false, dependsOnEnterprise: false, prompt: 'x' },
    })
    expect(t).toContain('没有任何输入')
  })
})

// ────────────────────────────────────────────────
// validateSource —— ★ 顺序 = 用户体验
// ────────────────────────────────────────────────

describe('validateSource', () => {
  it('★ 未选来源不阻塞（清空配置是合法操作）', () => {
    expect(validateSource(emptySourceSpec())).toEqual([])
  })

  it('global 没选参数 ⇒ 报一条', () => {
    expect(validateSource({ source: { kind: 'global' } })).toHaveLength(1)
    expect(validateSource({ source: { kind: 'global', ref: 'X' } })).toEqual([])
  })

  it('★ headerFooter 没选项 ⇒ 报一条（文案与「参数」区分开）', () => {
    const issues = validateSource({ source: { kind: 'headerFooter' } })
    expect(issues).toHaveLength(1)
    expect(issues[0]).toContain('文档信息项')
    expect(
      validateSource({ source: { kind: 'headerFooter', ref: '@doc_no' } }),
    ).toEqual([])
  })

  it('★ replace（只读项）选了 ref 即通过，⛔ 不因 kind 是 `replace` 而漏判', () => {
    expect(validateSource({ source: { kind: 'replace', ref: 'enterprise.Name' } })).toEqual([])
  })

  it('manual 永远通过', () => {
    expect(validateSource({ source: { kind: 'manual' } })).toEqual([])
  })

  it('★ 提示词为空时，第一条必须是「请填写提示词」（直接规则在前）', () => {
    const issues = validateSource({
      source: {
        kind: 'ai_field',
        hasParam: false,
        dependsOnEnterprise: false,
        prompt: '',
      },
    })
    expect(issues[0]).toContain('请填写提示词')
  })

  it('提示词填了、但两个输入都没开 ⇒ 报语义规则（解释「为什么」）', () => {
    const issues = validateSource({
      source: {
        kind: 'ai_field',
        hasParam: false,
        dependsOnEnterprise: false,
        prompt: '写一段',
      },
    })
    expect(issues).toHaveLength(1)
    expect(issues[0]).toContain('没有任何输入')
  })

  it('参数选「有」但没选具体参数 ⇒ 报一条', () => {
    const issues = validateSource({
      source: {
        kind: 'ai_field',
        hasParam: true,
        dependsOnEnterprise: false,
        prompt: '写一段',
      },
    })
    expect(issues.some((s) => s.includes('请指定'))).toBe(true)
  })

  it('★ 配全的 AI 节点通过', () => {
    expect(
      validateSource({
        source: {
          kind: 'ai_field',
          hasParam: true,
          ref: 'P1',
          dependsOnEnterprise: true,
          prompt: '写一段',
        },
      }),
    ).toEqual([])
  })
})

// ────────────────────────────────────────────────
// isBlankSource
// ────────────────────────────────────────────────

describe('isBlankSource', () => {
  it('kind 为空串 ⇒ true；有 kind ⇒ false', () => {
    expect(isBlankSource(emptySourceSpec())).toBe(true)
    expect(isBlankSource({ source: { kind: 'manual' } })).toBe(false)
  })
})

// ────────────────────────────────────────────────
// AI「带参数」—— 多选引用（48 §2.2 ① / 61 S-1）
// ────────────────────────────────────────────────

describe('aiParamRefs —— AI 参数引用列表', () => {
  it('多参数按顺序返回', () => {
    expect(aiParamRefs({ kind: 'ai_field', hasParam: true, params: ['b', 'a'] })).toEqual([
      'b',
      'a',
    ])
  })

  it('去空去重保序', () => {
    expect(
      aiParamRefs({ kind: 'ai_field', hasParam: true, params: [' a ', '', 'a', 'b'] }),
    ).toEqual(['a', 'b'])
  })

  it('★ 老数据：只有单值 ref 时回落成单元素（新写法优先）', () => {
    expect(aiParamRefs({ kind: 'ai_field', hasParam: true, ref: 'enterprise.Name' })).toEqual([
      'enterprise.Name',
    ])
    expect(
      aiParamRefs({ kind: 'ai_field', hasParam: true, ref: 'old', params: ['new'] }),
    ).toEqual(['new'])
  })

  it('未勾选参数（hasParam=false）⇒ 即使有 ref 也返回空', () => {
    expect(aiParamRefs({ kind: 'ai_semantic', hasParam: false, ref: 'X' })).toEqual([])
  })
})

describe('★ AI 多参数 往返一致（parse ∘ stringify）', () => {
  it('写出的 JSON 含 params 数组，且能解析回来', () => {
    const json = stringifySourceSpec({
      source: {
        kind: 'ai_field',
        hasParam: true,
        params: ['enterprise.Name', '质量方针'],
        dependsOnEnterprise: false,
        prompt: '用 {{enterprise.Name}} 作答',
      },
    })
    expect(json).toContain('"params"')
    const back = parseSourceSpec(json).model
    expect(aiParamRefs(back.source)).toEqual(['enterprise.Name', '质量方针'])
    expect(back.source.prompt).toBe('用 {{enterprise.Name}} 作答')
  })

  it('★ 老数据（单 ref + hasParam）解析后自动迁移成 params[0]', () => {
    const back = parseSourceSpec(
      '{"sources":[{"kind":"ai_field","hasParam":true,"ref":"P1","prompt":"x"}]}',
    ).model
    expect(aiParamRefs(back.source)).toEqual(['P1'])
  })
})

describe('★ AI 多参数 文案', () => {
  it('summary 列出全部引用参数', () => {
    const s = entrySummary({
      kind: 'ai_field',
      hasParam: true,
      params: ['enterprise.Name', '质量方针'],
      dependsOnEnterprise: true,
    })
    expect(s).toContain('enterprise.Name')
    expect(s).toContain('质量方针')
  })

  it('humanPreview 列出全部引用参数', () => {
    const t = humanPreview({
      source: {
        kind: 'ai_field',
        hasParam: true,
        params: ['enterprise.Name'],
        dependsOnEnterprise: true,
        prompt: 'x',
      },
    })
    expect(t).toContain('enterprise.Name')
  })
})
