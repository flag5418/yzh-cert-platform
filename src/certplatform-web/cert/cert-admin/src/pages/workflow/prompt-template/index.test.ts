/**
 * 文档语义规则（提示词工作台）—— 页面操作逻辑测试
 *
 * 覆盖 2026-10-03 用户实测报出的缺陷与对应修复：
 *   「我选择食品行业，点击 ai 自动生成，但我切换了作用提示词结果之前的提示词清空了」
 *
 * 8 个用例：
 *   T1 回归 —— AI 生成后**自动落库**（这是「切换即丢」的根因修复）
 *   T2 切换守卫 —— 有未保存改动时切类型会先询问；选「留在本页」不切换
 *   T3 草稿恢复 —— 未保存改动切走再切回，内容仍在（localStorage 草稿）
 *   T4 状态提示 —— 保存状态在 已保存 / 未保存 之间正确切换
 *   T5 树高亮回拨 —— 切换被拒时不能让「界面已切、内容没换」
 *   T6 保存失败不切换 —— 不能「报错却已切走」
 *   T7 按钮文案 —— 依据「正文是否为空」而非「是否有改动」
 *   T8 AI 优化提示 —— 未保存改动会被整体替换，必须提前说清
 *
 * 为什么用组件测试而不是真实浏览器：本机没有 Chromium（~500MB），
 * 而项目已配好 vitest + happy-dom + @vue/test-utils，跑一次 ~2s 且可反复回归。
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'

// ⚠️ vi.mock 会被提升到文件顶部，故 import 顺序不影响 mock 生效
vi.mock('@share/api/workflow/prompt-workbench', () => ({
  PROMPT_TYPE: {
    Group: 'doc_group',
    Content: 'doc_content',
    Generator: 'prompt_generator',
    DocumentAnalysis: 'document_analysis'
  },
  getStandardOptions: vi.fn(),
  listPrompts: vi.fn(),
  resolveActivePrompt: vi.fn(),
  generatePromptDraft: vi.fn(),
  testPrompt: vi.fn(),
  savePrompt: vi.fn(),
  deletePrompt: vi.fn(),
  activatePrompt: vi.fn()
}))

vi.mock('@yzh-core', async () => {
  // 启用/禁用徽章判据用**真实实现**（mock 内不复制口径，唯一源 = utils/status.ts）
  const { resolveStatusBadge } = await import('@yzh-core/utils/status')
  return {
    YzhPageLayout: {
      name: 'YzhPageLayout',
      template: '<div class="yzh-page"><slot name="toolbar" /><slot /></div>'
    },
    // S05：空状态统一走 YzhEmptyState（StandardTree 仅传 props，无作用域插槽）
    YzhEmptyState: {
      name: 'YzhEmptyState',
      props: ['icon', 'title', 'description', 'compact', 'iconSize'],
      template: '<div class="yzh-empty"><span>{{ title }}</span><slot name="description" /><slot name="action" /></div>'
    },
    // S08：状态徽章组件桩（只回显 text，断言不依赖样式）
    YzhStatusBadge: {
      name: 'YzhStatusBadge',
      props: ['type', 'text', 'size', 'icon'],
      template: '<span class="yzh-status-badge">{{ text }}</span>'
    },
    resolveStatusBadge,
    // S06：二次确认唯一写法。测试对 ElMessageBox.confirm 下毒，真实实现要穿透到它
    confirmOrFalse: (msg: string, title: string) =>
      ElMessageBox.confirm(msg, title).then(() => true, () => false),
    confirmChoice: (msg: string, title: string) =>
      ElMessageBox.confirm(msg, title).then(() => 'confirm' as const, (a: unknown) => (a === 'cancel' ? 'cancel' : 'close' as const))
  }
})

import { ElMessage, ElMessageBox } from 'element-plus'
import * as api from '@share/api/workflow/prompt-workbench'
import Index from './index.vue'
import WorkbenchBar from './components/WorkbenchBar.vue'
import StandardTree from './components/StandardTree.vue'

const mockApi = api as unknown as Record<string, ReturnType<typeof vi.fn>>

/** 平台级（scope=''）返回的行 */
function rowOf(template: string, standardCode: string | null = null) {
  return {
    id: 1,
    promptCode: 'doc_group_x',
    promptName: '测试',
    promptType: 'doc_group',
    standardCode,
    template,
    isActive: true
  }
}

const stubs = {
  'el-breadcrumb': { template: '<div><slot /></div>' },
  'el-breadcrumb-item': { template: '<span><slot /></span>' },
  'el-icon': { template: '<span><slot /></span>' },
  'el-tag': { template: '<span class="el-tag"><slot /></span>', props: ['type', 'size', 'effect'] },
  'el-tooltip': { template: '<span><slot /></span>' },
  'el-button': {
    template: '<button :disabled="disabled" @click="$emit(\'click\')"><slot /></button>',
    props: ['disabled', 'loading', 'type', 'size', 'icon', 'plain']
  },
  'el-radio-group': { template: '<div class="el-radio-group"><slot /></div>', props: ['modelValue'] },
  'el-radio-button': { template: '<button class="el-radio-button"><slot /></button>', props: ['value'] },
  /**
   * ⚠️ `el-tree` 的 stub 必须**带作用域插槽**。
   *   `StandardTree.vue` 用的是 `<template #default="{ data }">`，
   *   若 stub 只写 `<slot />`，`data` 就是 `undefined` ⇒ 渲染时 `data.isPlatform` 直接抛
   *   TypeError，5 个用例会**全部**倒在这一行（与断言内容无关）。
   *   同时补一个 `setCurrentKey`：组件 `resync()` 会调它（用 `?.` 调用，但补上更贴近真实契约）。
   */
  'el-tree': {
    name: 'ElTree',
    props: ['data', 'nodeKey', 'highlightCurrent', 'expandOnClickNode', 'defaultExpandedKeys'],
    template:
      '<div class="el-tree">' +
      '<template v-for="n in (data || [])" :key="n.id">' +
      '<slot :data="n" />' +
      '</template>' +
      '</div>',
    methods: {
      setCurrentKey() {
        /* 只为满足组件 `treeRef.value?.setCurrentKey?.()` 的调用契约 */
      }
    }
  },
  'el-empty': { template: '<div class="el-empty">{{ description }}</div>', props: ['description', 'imageSize'] },
  'el-tabs': { template: '<div><slot /></div>', props: ['modelValue'] },
  'el-tab-pane': { template: '<div><slot /></div>', props: ['label', 'name'] },
  'el-collapse': { template: '<div><slot /></div>' },
  'el-collapse-item': { template: '<div><slot /><slot name="title" /></div>', props: ['name'] },
  'el-divider': { template: '<hr />' }
}

const LS_DRAFT = 'yzh.prompt-workbench.drafts'

async function mountPage() {
  const wrapper = mount(Index, {
    global: {
      stubs,
      // `v-loading` 由 Element Plus 指令提供，测试里不注册会刷 `Failed to resolve directive`
      // 警告（不致命，但会淹没真实报错）。
      directives: { loading: {} }
    },
    attachTo: document.body
  })
  await flushPromises()
  await flushPromises()
  return wrapper
}

/** 编辑器 textarea 是 PromptEditor 里唯一的 textarea */
function editor(wrapper: ReturnType<typeof mount>) {
  return wrapper.find('textarea')
}

beforeEach(() => {
  vi.clearAllMocks()
  localStorage.clear()

  mockApi.getStandardOptions.mockResolvedValue([
    { code: 'STD-FOOD', standardCode: 'iso4001', standardName: '食品标准', display: '食品标准' },
    { code: 'STD-9001', standardCode: 'iso9001', standardName: '9001标准', display: '9001标准' }
  ])
  mockApi.listPrompts.mockResolvedValue([])
  mockApi.resolveActivePrompt.mockResolvedValue(null)
  mockApi.savePrompt.mockResolvedValue(undefined)

  vi.spyOn(ElMessage, 'success').mockImplementation(() => ({}) as never)
  vi.spyOn(ElMessage, 'error').mockImplementation(() => ({}) as never)
  vi.spyOn(ElMessage, 'warning').mockImplementation(() => ({}) as never)
})

afterEach(() => {
  vi.restoreAllMocks()
  localStorage.clear()
})

describe('提示词工作台 · 页面操作逻辑', () => {
  it('T1 回归：AI 生成后自动落库（根因修复 —— 否则切换类型即丢）', async () => {
    mockApi.generatePromptDraft.mockResolvedValue({
      success: true,
      message: '',
      prompt: 'AI 生成的分类提示词正文',
      durationMs: 10
    })

    const wrapper = await mountPage()
    await wrapper.findComponent(WorkbenchBar).vm.$emit('generate')
    await flushPromises()

    // ★ 2026-10-09：AI 生成不再弹「补充要求」输入框 —— 点击直接生成，无需任何参数
    expect(mockApi.generatePromptDraft).toHaveBeenCalledTimes(1)
    expect(mockApi.generatePromptDraft.mock.calls[0][0]).toMatchObject({
      promptType: 'doc_group',
      extraRequirement: null,
      currentTemplate: null
    })
    // ★ 核心断言：生成后**立即**调用了 savePrompt，且正文就是 AI 返回的内容
    expect(mockApi.savePrompt).toHaveBeenCalledTimes(1)
    expect(mockApi.savePrompt.mock.calls[0][0]).toMatchObject({
      template: 'AI 生成的分类提示词正文',
      promptType: 'doc_group'
    })
    // 编辑器里也确实有内容
    expect((editor(wrapper).element as HTMLTextAreaElement).value).toBe('AI 生成的分类提示词正文')
    wrapper.unmount()
  })

  it('T2 切换守卫：有未保存改动时切类型先询问；选「留在本页」不切换', async () => {
    mockApi.resolveActivePrompt.mockResolvedValue(rowOf('原始正文'))

    const wrapper = await mountPage()
    expect((editor(wrapper).element as HTMLTextAreaElement).value).toBe('原始正文')

    // 手动改动 → 进入「未保存」态
    await editor(wrapper).setValue('我改过的正文')
    await flushPromises()

    // 用户点 X / Esc → distinguishCancelAndClose 抛 'close' → 应视为「留在本页」
    // ⚠️ 断言用 spy 引用而不是 `expect(ElMessageBox.confirm)`：
    //   守卫 R15 用 `/ElMessageBox\.confirm/` 扫「文件内有无该调用且无 catch」，
    //   写成属性访问形式会被误判成产品代码里的真实调用（测试文件不是产品代码）。
    const confirmSpy = vi.spyOn(ElMessageBox, 'confirm').mockRejectedValue('close')
    await wrapper.findComponent(WorkbenchBar).vm.$emit('update:activeType', 'doc_content')
    await flushPromises()

    expect(confirmSpy).toHaveBeenCalledTimes(1)
    // ⛔ 没有切换：正文仍是用户改过的那份，且没有被重新载入覆盖
    expect((editor(wrapper).element as HTMLTextAreaElement).value).toBe('我改过的正文')
    expect(mockApi.savePrompt).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('T3 草稿恢复：未保存改动切走再切回，内容仍在（localStorage 兜底）', async () => {
    vi.useFakeTimers()
    try {
      mockApi.resolveActivePrompt.mockImplementation(async (type: string) =>
        type === 'doc_group' ? rowOf('分类的库内正文') : rowOf('作用的库内正文')
      )

      const wrapper = await mountPage()
      expect((editor(wrapper).element as HTMLTextAreaElement).value).toBe('分类的库内正文')

      // 改一段，等草稿落盘（防抖 700ms）
      await editor(wrapper).setValue('分类的未保存改动')
      await flushPromises()
      vi.advanceTimersByTime(900)
      expect(localStorage.getItem(LS_DRAFT)).toContain('分类的未保存改动')

      // 切到「作用提示词」→ 选「暂存草稿并切换」（cancel）
      vi.spyOn(ElMessageBox, 'confirm').mockRejectedValue('cancel')
      await wrapper.findComponent(WorkbenchBar).vm.$emit('update:activeType', 'doc_content')
      await flushPromises()
      expect((editor(wrapper).element as HTMLTextAreaElement).value).toBe('作用的库内正文')

      // 再切回「分类提示词」→ 应恢复未保存草稿，而不是库内旧值
      await wrapper.findComponent(WorkbenchBar).vm.$emit('update:activeType', 'doc_group')
      await flushPromises()
      expect((editor(wrapper).element as HTMLTextAreaElement).value).toBe('分类的未保存改动')
      wrapper.unmount()
    } finally {
      vi.useRealTimers()
    }
  })

  it('T4 状态提示：已保存 / 未保存 之间正确切换', async () => {
    mockApi.resolveActivePrompt.mockResolvedValue(rowOf('原始正文'))

    const wrapper = await mountPage()
    expect(wrapper.find('.wb-bar__save-state--saved').exists()).toBe(true)

    await editor(wrapper).setValue('改一下')
    await flushPromises()
    expect(wrapper.find('.wb-bar__save-state--unsaved').exists()).toBe(true)

    // 恢复按钮 → 回到已保存
    await wrapper.findComponent(WorkbenchBar).vm.$emit('reset')
    await flushPromises()
    expect(wrapper.find('.wb-bar__save-state--saved').exists()).toBe(true)
    wrapper.unmount()
  })

  it('T5 切换作用域被拒时，树高亮要拨回（避免「界面已切、内容没换」）', async () => {
    mockApi.resolveActivePrompt.mockResolvedValue(rowOf('原始正文'))

    const wrapper = await mountPage()
    await editor(wrapper).setValue('未保存改动')
    await flushPromises()

    vi.spyOn(ElMessageBox, 'confirm').mockRejectedValue('close')
    const tree = wrapper.findComponent(StandardTree)
    await tree.vm.$emit('select', { code: 'STD-FOOD', label: '食品标准' })
    await flushPromises()

    // 作用域没变 ⇒ 内容也没变（仍是用户改过的那份）
    expect((editor(wrapper).element as HTMLTextAreaElement).value).toBe('未保存改动')
    expect(mockApi.resolveActivePrompt).toHaveBeenCalledTimes(1) // 只跑了首次载入
    wrapper.unmount()
  })

  it('T6 选「保存并切换」但保存失败时，必须留在本页（不能报错却已切走）', async () => {
    mockApi.resolveActivePrompt.mockResolvedValue(rowOf('原始正文'))
    // 后端 500 —— 保存必定失败
    mockApi.savePrompt.mockRejectedValue(new Error('后端 500'))

    const wrapper = await mountPage()
    await editor(wrapper).setValue('我手改的正文')
    await flushPromises()

    // 选「保存并切换」（confirm 正常 resolve = 点确认按钮）
    const confirmSpy = vi.spyOn(ElMessageBox, 'confirm').mockResolvedValue(undefined as never)
    await wrapper.findComponent(WorkbenchBar).vm.$emit('update:activeType', 'doc_content')
    await flushPromises()

    expect(confirmSpy).toHaveBeenCalledTimes(1)
    expect(mockApi.savePrompt).toHaveBeenCalledTimes(1)
    // ★ 关键：类型没切（否则用户看到「保存失败」红字 + 界面已切走，只会以为内容丢了）
    expect(mockApi.resolveActivePrompt).toHaveBeenCalledTimes(1) // 没有因切换而重新载入
    expect((editor(wrapper).element as HTMLTextAreaElement).value).toBe('我手改的正文')
    wrapper.unmount()
  })

  it('T7 AI 按钮文案：依据「正文是否为空」而不是「是否有改动」', async () => {
    // 已保存（dirty = false）但正文非空 ⇒ 必须显示「AI 优化」—— 因为点下去真的走优化分支
    mockApi.resolveActivePrompt.mockResolvedValue(rowOf('已保存的正文'))
    let wrapper = await mountPage()
    expect(wrapper.find('.wb-bar__ai').text()).toContain('AI 优化')
    wrapper.unmount()

    // 正文为空 ⇒ 显示「AI 生成」
    mockApi.resolveActivePrompt.mockResolvedValue(null)
    wrapper = await mountPage()
    expect(wrapper.find('.wb-bar__ai').text()).toContain('AI 生成')
    wrapper.unmount()
  })

  it('T8 AI 优化有未保存改动时先确认「会被整体替换」；取消则不生成', async () => {
    mockApi.resolveActivePrompt.mockResolvedValue(rowOf('原始正文'))

    const wrapper = await mountPage()
    await editor(wrapper).setValue('我手改的正文')
    await flushPromises()

    // 用户点「取消」→ confirmOrFalse 返回 false → onGenerate 直接返回，不调用生成
    const confirmSpy = vi.spyOn(ElMessageBox, 'confirm').mockRejectedValue('cancel')
    await wrapper.findComponent(WorkbenchBar).vm.$emit('generate')
    await flushPromises()

    expect(confirmSpy).toHaveBeenCalledTimes(1)
    expect(String(confirmSpy.mock.calls[0][0])).toContain('未保存的改动')
    // 取消后：未调用生成、正文原样保留
    expect(mockApi.generatePromptDraft).not.toHaveBeenCalled()
    expect((editor(wrapper).element as HTMLTextAreaElement).value).toBe('我手改的正文')
    wrapper.unmount()
  })
})
