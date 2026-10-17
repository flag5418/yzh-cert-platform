/**
 * 发布校验（ValidateTab）—— **自动校验**接线（组件测试）
 *
 * 【为什么测它 —— 2026-10-16 的一类静默失败】
 *   此前 `canPublish` 只在用户**手动**点「开始校验」后才回填 ⇒ 底部「保存并发布」恒灰，
 *   而保存条 tooltip 又写「请先完成必配项」⇒ 用户误以为「缺 AI 语义分析 / 类型确认」
 *   挡住了发布（实测把完成度与发布门混为一谈），可**点一下就能发**。
 *   若哪天有人把这条 `watch(..., { immediate:true })` 删掉，表现是**按钮又变灰且不报错** ——
 *   正是本测试要钉住的。
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
  // 信封判定桩：本测试只关心「有没有去问后端、问了哪个模板」
  unwrapOk: (r: any) => r.data ?? r,
}))

vi.mock('@share/api/workflow/doc-fill-rule', () => ({
  validateTemplate: vi.fn(),
}))

import * as api from '@share/api/workflow/doc-fill-rule'
import ValidateTab from './ValidateTab.vue'

const mockApi = api as unknown as Record<string, ReturnType<typeof vi.fn>>

const OK = {
  data: {
    TemplateCode: 'TPL-1',
    ErrorCount: 0,
    WarningCount: 0,
    Violations: [],
    AnchorCount: 2,
    PublishStatus: 'ready',
    CanPublish: true,
    BlockReason: '',
  },
}

const stubs = {
  'el-button': {
    template:
      '<button class="eb" :disabled="disabled" @click="$emit(\'click\')"><slot /></button>',
    props: ['disabled', 'loading', 'type', 'size', 'icon'],
  },
  'el-icon': { template: '<i class="ei"><slot /></i>' },
}

function mountTab(templateCode = 'TPL-1', hasTemplate = true) {
  return mount(ValidateTab, {
    props: { templateCode, hasTemplate },
    global: { stubs, directives: { loading: {} } },
  })
}

function lastValidated(w: any) {
  const ev = w.emitted('validated')
  return ev?.[ev.length - 1]?.[0]
}

beforeEach(() => {
  vi.clearAllMocks()
  mockApi.validateTemplate.mockResolvedValue(OK)
})

describe('ValidateTab — 选中模板即自动校验', () => {
  it('★ 挂载即调后端校验一次（⛔ 不需要用户先点「开始校验」）', async () => {
    const w = mountTab('TPL-1')
    await flushPromises()

    expect(mockApi.validateTemplate).toHaveBeenCalledTimes(1)
    expect(mockApi.validateTemplate).toHaveBeenCalledWith('TPL-1')
    expect(lastValidated(w)).toMatchObject({ canPublish: true, anchorCount: 2 })
    // 已校验 ⇒ 按钮文案变「重新校验」，证明内部 `validated` 已置位
    expect(w.find('.eb').text()).toContain('重新校验')
  })

  it('★ 换模板 ⇒ 自动重跑（按钮跟着新模板的结论走）', async () => {
    const w = mountTab('TPL-1')
    await flushPromises()

    await w.setProps({ templateCode: 'TPL-2' })
    await flushPromises()

    expect(mockApi.validateTemplate).toHaveBeenCalledTimes(2)
    expect(mockApi.validateTemplate).toHaveBeenLastCalledWith('TPL-2')
  })

  it('无模板（templateCode 空）⇒ 不去问后端，且回吐「不可发布」', async () => {
    const w = mountTab('')
    await flushPromises()

    expect(mockApi.validateTemplate).not.toHaveBeenCalled()
    expect(lastValidated(w)).toMatchObject({ canPublish: false })
  })

  it('校验失败 ⇒ 不误报「可发布」（保持 false）', async () => {
    mockApi.validateTemplate.mockRejectedValueOnce(new Error('后端挂了'))
    const w = mountTab('TPL-1')
    await flushPromises()

    expect(lastValidated(w)).toMatchObject({ canPublish: false })
  })
})
