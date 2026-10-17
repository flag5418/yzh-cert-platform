/**
 * 特殊节点「编辑说明」完备性回归测试（2026-10-15）
 *
 * ── 缺陷现场（两个独立问题，缺一不可）──────────────────────────────────
 * ① 数据侧：节点属性面板顶部的「!」读 `SpecialNodeMeta.description`，每个参数下方的
 *    常驻小字读 `PanelField.description`。此前 9 个特殊节点里 8 个只有一句泛泛的话
 *    （如「从文档中提取指定字段的值」）—— 配置者看完仍然不知道「文档」下拉从哪来、
 *    为什么「字段」是空的、留空会怎样。
 * ② 渲染侧：`PanelField.description` 在 `NodePropertyForm.vue` 里**从未被渲染** ——
 *    即使把说明写全，界面上也一个字都看不到（真·死数据）。
 *
 * ── 本测试锁两条线 ────────────────────────────────────────────────────
 *   数据侧：每个节点 / 每个参数 / 每个端口都必须有说明（防「新增节点时忘了写」）。
 *   渲染侧：`NodePropertyForm.vue` 必须真的把 `field.description` 渲染出来
 *           （防「又变回死数据」—— 这是本次修复的**唯一目的**）。
 *
 * ── ★ 负向对照（必配，否则测试可能恒绿）──────────────────────────────
 *   `collectMissing*` 系列对**故意造出的坏数据**必须报出来。
 *   若哪天有人把校验写错成「恒返回空数组」，下面的「坏数据必须被检出」会先失败。
 */
import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { SPECIAL_NODES, getSpecialNode } from '@share/composables/workflow/specialNodes'

// ══════════════════════════════════════════════════════════════
// 校验器（纯函数 ⇒ 可对真数据与坏数据双向断言）
// ══════════════════════════════════════════════════════════════

type Node = (typeof SPECIAL_NODES)[number]

/** 节点级说明缺哪一段（【作用】/【怎么配】/【坑】） */
function missingNodeSections(nodes: readonly any[]): string[] {
  const bad: string[] = []
  for (const n of nodes) {
    const d = (n?.description ?? '') as string
    const lacks = (['【作用】', '【怎么配】', '【坑】'] as const).filter((k) => !d.includes(k))
    if (lacks.length) bad.push(`${n?.classCode ?? '?'} 缺 ${lacks.join('/')}`)
  }
  return bad
}

/** 参数级说明缺失或与 label 重复（等于没写） */
function missingFieldDescriptions(nodes: readonly any[]): string[] {
  const bad: string[] = []
  for (const n of nodes) {
    for (const f of (n?.panelSchema ?? []) as any[]) {
      const d = (f?.description ?? '') as string
      if (!d.trim()) bad.push(`${n?.classCode}.${f?.field} 说明为空`)
      else if (d.trim() === (f?.label ?? '').trim()) bad.push(`${n?.classCode}.${f?.field} 说明只是重复了 label`)
    }
  }
  return bad
}

/** 端口级说明为空 */
function missingPortDescriptions(nodes: readonly any[]): string[] {
  const bad: string[] = []
  for (const n of nodes) {
    for (const p of [...(n?.inputPorts ?? []), ...(n?.outputPorts ?? [])] as any[]) {
      if (!((p?.Description ?? '') as string).trim()) bad.push(`${n?.classCode}.${p?.Name} 端口说明为空`)
    }
  }
  return bad
}

// ══════════════════════════════════════════════════════════════
// ① 数据侧：真数据必须全绿
// ══════════════════════════════════════════════════════════════

describe('specialNodes — 编辑说明完备性', () => {
  it('每个节点说明都含【作用】【怎么配】【坑】三段', () => {
    expect(missingNodeSections(SPECIAL_NODES)).toEqual([])
  })

  it('每个参数都有说明，且不是简单重复 label', () => {
    expect(missingFieldDescriptions(SPECIAL_NODES)).toEqual([])
  })

  it('每个端口都有说明', () => {
    expect(missingPortDescriptions(SPECIAL_NODES)).toEqual([])
  })

  it('节点清单未被意外增删（新增节点必须同时补说明）', () => {
    expect(SPECIAL_NODES.map((n: Node) => n.classCode).sort()).toEqual(
      [
        'ai_node', 'branch', 'constant', 'docField', 'docMarkdown', 'docTable',
        'end', 'is_std_file_missing', 'loop', 'start'
      ]
    )
  })
})

// ══════════════════════════════════════════════════════════════
// ② ★ 负向对照：坏数据必须被检出（证明校验器不是恒绿）
// ══════════════════════════════════════════════════════════════

describe('specialNodes — 校验器负向对照（防「恒绿」）', () => {
  it('节点说明只写一句话 ⇒ 必须报出缺段', () => {
    const bad = missingNodeSections([{ classCode: 'demo', description: '从文档中提取字段' }])
    expect(bad.length).toBe(1)
    expect(bad[0]).toContain('【作用】')
    expect(bad[0]).toContain('【坑】')
  })

  it('参数说明为空 / 只重复 label ⇒ 必须报出', () => {
    const bad = missingFieldDescriptions([
      { classCode: 'demo', panelSchema: [{ field: 'a', label: 'A', description: '' }] },
      { classCode: 'demo2', panelSchema: [{ field: 'b', label: 'B', description: 'B' }] }
    ])
    expect(bad.length).toBe(2)
    expect(bad[0]).toContain('说明为空')
    expect(bad[1]).toContain('重复了 label')
  })

  it('端口说明为空 ⇒ 必须报出', () => {
    const bad = missingPortDescriptions([{ classCode: 'demo', inputPorts: [{ Name: 'x', Description: '  ' }] }])
    expect(bad).toEqual(['demo.x 端口说明为空'])
  })
})

// ══════════════════════════════════════════════════════════════
// ③ ★ 渲染侧：说明必须真的出现在属性面板上
//
//    这是本次修复的**唯一目的** —— `PanelField.description` 此前从未被渲染，
//    写多好都没人看得见。用源码断言锁住，成本最低、指向最准。
// ══════════════════════════════════════════════════════════════

describe('NodePropertyForm — 参数说明必须被渲染（防死数据回归）', () => {
  // ⚠️ 用 `resolve(dirname(fileURLToPath(import.meta.url)), …)` 而不是 `new URL(…, import.meta.url)`：
  //   本机沙箱对 `readFileSync` 做了 fs 代理（brokered-fs-shim），它只认**字符串路径**，
  //    传 URL 对象会抛 `TypeError: The URL must be of scheme file`。
  const here = dirname(fileURLToPath(import.meta.url))
  const src = readFileSync(
    resolve(here, '../../../../cert-share/src/components/workflow/NodePropertyForm.vue'),
    'utf8'
  )

  it('模板里渲染了 field.description（普通参数 + 提示词参数两处都要）', () => {
    const hits = src.match(/field\.description/g) ?? []
    // 普通参数分支 + promptWithRef 分支 = 至少 2 处
    expect(hits.length).toBeGreaterThanOrEqual(2)
  })

  it('渲染用的是常驻元素而不是 tooltip（用户不需要悬停才看得到）', () => {
    expect(src).toContain('class="field-desc"')
  })

  it('节点说明走 raw-content（否则多行说明的换行会被折叠成一坨）', () => {
    expect(src).toContain('nodeDescriptionHtml')
    expect(src).toContain('raw-content')
  })
})

// ══════════════════════════════════════════════════════════════
// ④ ★ 跨边界归一化：后端 PascalCase → 前端小写
//
//    功能节点（skill）的「!」说明读的是 `skills[].description`（小写），
//    而后端 `/WfSkill/metadata` 返回的是 `Description`（PascalCase）。
//    `WorkflowDesigner.loadSkills` 此前只补了 skillCode/skillName/category/skillType/inputPorts
//    五个小写别名，**漏了 description** ⇒ `s.description` 恒 undefined
//    ⇒ 功能节点属性面板永远不显示「!」（后端写多好都到不了界面）。
//
//    ⛔ 同类跨边界命名问题：后端响应恒 PascalCase，前端内部模型字段在此显式映射。
//      （端口字段已于 2026-10-16 P3 统一为 PascalCase ⇒ 不再需要 portNormalize 归一化层。）
// ══════════════════════════════════════════════════════════════

describe('WorkflowDesigner — skill 说明必须跨边界归一化（防「!」永不显示）', () => {
  const here = dirname(fileURLToPath(import.meta.url))
  const src = readFileSync(
    resolve(here, '../../../../cert-share/src/components/workflow/WorkflowDesigner.vue'),
    'utf8'
  )

  it('loadSkills 里必须把 Description 归一成小写 description', () => {
    expect(src).toMatch(/description:\s*s\.Description/)
  })

  it('NodePropertyForm 读的是小写 description（与上一条必须成对，否则仍静默失效）', () => {
    const form = readFileSync(
      resolve(here, '../../../../cert-share/src/components/workflow/NodePropertyForm.vue'),
      'utf8'
    )
    expect(form).toMatch(/s\.skillCode\s*===\s*form\.value\.skillCode\)\?\.description/)
  })
})

// ══════════════════════════════════════════════════════════════
// ⑤ 抽查：is_std_file_missing 的说明必须点出「四态」这个关键口径
//    （它最容易被误用 —— not_configured 常被当成「缺失」）
// ══════════════════════════════════════════════════════════════

describe('specialNodes — 关键口径抽查', () => {
  it('is_std_file_missing 说明里必须提醒 not_configured ≠ 缺失', () => {
    const d = getSpecialNode('is_std_file_missing')!.description
    expect(d).toContain('not_configured')
    expect(d).toContain('目录没配')
  })

  it('start 说明里必须说明机构编码由引擎推导、不需要手填', () => {
    const d = getSpecialNode('start')!.description
    expect(d).toContain('自动注入')
    expect(d).toContain('推导')
  })
})
