/**
 * 预览 Tab（PreviewTab）—— **试填闸只看「能否试填」，⛔ 不看来源配齐**
 *
 * 【为什么测它 —— 2026-10-16 用户裁决引入的结构性变化】
 *  用户逐字：「*预览的时候，无论来源是什么，都可以自动生成填写内容，查看替换后的预览
 *  效果是否准确 —— 提前发现我们更新文档后是否正确*」。
 *  门槛从 `anchorReadiness.ready`（配置完成度：要求来源全配齐）换成
 *  `previewReadiness.ready`（只看 有模板 ∧ 扫描完成 ∧ 有锚点）。
 *  ⛔ 若哪天有人把门槛改回 `anchorReadiness`，表现是「有 1 个锚点没配来源就进不了预览」
 *     —— 正是本测试要钉住的（组件不报错，只是把内容区整个换成门槛空态）。
 */
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'

vi.mock('@yzh-core', () => ({
  YzhEmptyState: {
    name: 'YzhEmptyState',
    props: ['icon', 'title', 'description', 'compact', 'iconSize'],
    template: '<div class="yzh-empty">{{ title }}</div>',
  },
  YzhStatusBadge: {
    name: 'YzhStatusBadge',
    props: ['type', 'text'],
    template: '<span class="yzh-badge">{{ text }}</span>',
  },
  unwrapOk: (r: any) => r.data ?? r,
}))

vi.mock('@share/api/workflow/doc-fill-rule', () => ({
  runDocFillPreview: vi.fn(),
}))

import * as api from '@share/api/workflow/doc-fill-rule'
import PreviewTab from './PreviewTab.vue'

const mockApi = api as unknown as Record<string, ReturnType<typeof vi.fn>>

const PREVIEW_OK = {
  data: {
    Status: 'filled',
    AnchorCount: 1,
    PendingCount: 0,
    Values: [
      { AnchorRef: 'ENT_NAME', Key: 'ENT_NAME', Source: '试填占位值', Value: '【试填】ENT_NAME' },
    ],
  },
}

const stubs = {
  'el-button': {
    template:
      '<button class="eb" :disabled="disabled" @click="$emit(\'click\')"><slot /></button>',
    props: ['disabled', 'loading', 'type', 'size', 'icon'],
  },
  'el-input': {
    template: '<textarea class="ein" :value="modelValue" />',
    props: ['modelValue', 'type', 'rows', 'size', 'ariaLabel'],
  },
}

/**
 * 造一份 logic 桩：**只桩门槛两块**（其余组件不读），用来暴露「门槛读的是哪一个」。
 */
function makeLogic(over: {
  previewReady?: boolean
  anchorReady?: boolean
}) {
  const previewReady = over.previewReady ?? true
  const anchorReady = over.anchorReady ?? false
  return {
    previewReadiness: {
      total: 2,
      unconfigured: 0,
      staleRef: 0,
      orphan: 0,
      ready: previewReady,
    },
    anchorReadiness: {
      total: 2,
      unconfigured: 0,
      staleRef: 0,
      orphan: 0,
      ready: anchorReady,
    },
    ensureAnchors: vi.fn(async () => {}),
  }
}

function mountTab(logic: any, extra: Record<string, any> = {}) {
  return mount(PreviewTab, {
    props: {
      templateCode: 'TPL-1',
      blockReason: '',
      ...extra,
    },
    global: {
      provide: { logic },
      stubs,
      directives: { loading: {} },
    },
  })
}

beforeEach(() => {
  vi.clearAllMocks()
  mockApi.runDocFillPreview.mockResolvedValue(PREVIEW_OK)
})

describe('PreviewTab · 试填闸不因「来源未配齐」而关闭', () => {
  it('★ previewReadiness.ready=true / anchorReadiness.ready=false ⇒ 照常自动取值（进得去）', async () => {
    // `anchorReadiness.ready=false` 模拟「有锚点没配来源」：旧门槛会因此把整个 Tab 关掉
    const w = mountTab(makeLogic({ previewReady: true, anchorReady: false }))
    await flushPromises()

    expect(w.find('.yzh-empty').exists()).toBe(false) // ⛔ 未落门槛空态
    expect(mockApi.runDocFillPreview).toHaveBeenCalledTimes(1)
    expect(w.findAll('.pvrow').length).toBe(1)
  })

  it('★ previewReadiness.ready=false ⇒ 落门槛空态，且不去试填', async () => {
    const w = mountTab(makeLogic({ previewReady: false, anchorReady: true }), {
      blockReason: '还没有空白模板',
    })
    await flushPromises()

    expect(w.find('.yzh-empty').text()).toContain('暂不能预览')
    expect(mockApi.runDocFillPreview).not.toHaveBeenCalled()
  })

  it('★ sourceHint 非空 ⇒ 顶部如实提示「用占位值」，⛔ 不阻断', async () => {
    const w = mountTab(makeLogic({ previewReady: true }), {
      sourceHint: '2 个未配来源 ⇒ 这些锚点用【试填】占位值（不影响验证替换效果）',
    })
    await flushPromises()

    expect(w.find('.pvtab__srcbar').text()).toContain('【试填】占位值')
    expect(w.findAll('.pvrow').length).toBe(1) // 提示归提示，值照样出
  })

  it('sourceHint 空 ⇒ 不渲染提示条', async () => {
    const w = mountTab(makeLogic({ previewReady: true }))
    await flushPromises()
    expect(w.find('.pvtab__srcbar').exists()).toBe(false)
  })
})
