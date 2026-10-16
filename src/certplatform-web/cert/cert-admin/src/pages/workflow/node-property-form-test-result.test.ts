/**
 * 节点测试结果面板 —— 「节点级失败」显示回归测试（2026-10-16）
 *
 * 背景（用户实测上报）：在 NC 配置页点「运行测试」测「聚合多路judge」节点，
 * 接口返回的是**结构化失败**（信封 success=true / `data.Success=false` + `data.Error` + `data.UnresolvedInputs`），
 * 而面板只读信封 `success` ⇒ 渲染成**绿底「执行成功」**，且错误文本与「哪些连线端口没取到值」都不显示。
 *
 * 本套把这条链路钉在**组件接口**上（不是断言内部实现）：
 *   挂载 → 点面板里的「运行测试」按钮 → 拿到它 emit 出的 onSuccess/onError 回调
 *   → 用引擎真实响应的形状回调 → 断言面板文案。
 *
 * 为什么用组件测试而不是真实浏览器：本机没有 Chromium，而项目已配好
 * vitest + happy-dom + @vue/test-utils（范式见 pages/workflow/prompt-template/index.test.ts）。
 *
 * ⚠️ stub 铁律：`el-alert` 的文字在**props**（title/description）上，未解析组件不会渲染 props
 * ⇒ 必须补一个把 title/description 渲染出来的 stub，否则「告警显示出来了」这件事**测不出来**。
 */

import { describe, it, expect, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'

vi.mock('@yzh-core', () => ({
  YzhEmptyState: {
    name: 'YzhEmptyState',
    props: ['icon', 'title', 'description', 'compact', 'iconSize'],
    template: '<div class="yzh-empty"><span>{{ title }}</span></div>'
  }
}))

import NodePropertyForm from '@share/components/workflow/NodePropertyForm.vue'

/** 面板里的「运行测试」按钮（unresolved 的 el-button 会渲染成同名元素，click 仍是原生监听） */
const ElAlertStub = {
  name: 'ElAlert',
  props: ['title', 'description', 'type', 'showIcon', 'closable'],
  template: '<div class="alert"><span class="alert-title">{{ title }}</span><span class="alert-desc">{{ description }}</span></div>'
}

function mountForm() {
  return mount(NodePropertyForm, {
    props: {
      selectedNode: {
        nodeId: 'collect_judges_n1',
        nodeType: 'skill',
        title: '聚合多路judge',
        skillCode: 'collect_judges',
        inputs: { judge_1: 'stamp_judge_level_n1' },
        inputTypes: { judge_1: 'link' },
        inputPorts: [{ name: 'judge_1', type: 'json', bindMode: 'Link' }],
        outputPorts: [],
        config: {}
      },
      skills: [{ skillCode: 'collect_judges', skillName: '聚合多路judge', description: '' }],
      canvasNodes: []
    },
    global: { stubs: { ElAlert: ElAlertStub } }
  })
}

/** 点「运行测试」→ 取回组件 emit 出来的回调（= 测试结果写入面板的唯一入口） */
async function clickTest(wrapper: ReturnType<typeof mountForm>) {
  const btn = wrapper.findAll('el-button').find((b) => b.text().includes('运行测试'))
  expect(btn, '没找到「运行测试」按钮').toBeTruthy()
  await btn!.trigger('click')
  const emitted = wrapper.emitted('test-node') as any[]
  expect(emitted?.length, '点按钮没有 emit test-node').toBeGreaterThan(0)
  return emitted[emitted.length - 1][0] as { onSuccess: (d: any) => void; onError: (m: string, d: any) => void }
}

describe('节点测试结果面板 · 节点级失败必须显示为失败', () => {
  it('N1 回归：data.Success=false（信封 success=true）⇒ 显示「执行失败」+ Error + 未取到值的端口', async () => {
    const w = mountForm()
    const cb = await clickTest(w)

    cb.onSuccess({
      TaskCode: 'T1',
      Success: false,
      Error: "Skill 'collect_judges' 执行失败[PRECONDITION_EMPTY]: 未收到任何 judge 结果（本次单节点测试未执行上游：judge_1 端口没有值）",
      Output: {},
      UnresolvedInputs: ['judge_1'],
      DurationMs: 11
    })
    await flushPromises()

    const text = w.text()
    expect(text).toContain('执行失败')
    expect(text).not.toContain('执行成功')
    expect(text).toContain('PRECONDITION_EMPTY')
    // ★ 告警框（来自 UnresolvedInputs —— 修复前面板把 data 丢掉，这里恒空）
    expect(text).toContain('1 个连线端口本次未取到值')
    expect(text).toContain('judge_1')
  })

  it('N2 负向对照：data.Success=true ⇒ 必须是「执行成功」（证明 N1 不是恒红）', async () => {
    const w = mountForm()
    const cb = await clickTest(w)

    cb.onSuccess({ TaskCode: 'T2', Success: true, Output: { conclusion: 'fail' }, UnresolvedInputs: [], DurationMs: 8 })
    await flushPromises()

    expect(w.text()).toContain('执行成功')
    expect(w.text()).not.toContain('执行失败')
  })

  it('N3 信封失败（onError）也把 data 带进面板，告警仍可见', async () => {
    const w = mountForm()
    const cb = await clickTest(w)

    // WorkflowDesigner 的失败分支：onError(res.err, res.data)
    cb.onError('节点执行失败: 上游未提供必要数据', { UnresolvedInputs: ['judge_2'] })
    await flushPromises()

    const text = w.text()
    expect(text).toContain('执行失败')
    expect(text).toContain('上游未提供必要数据')
    expect(text).toContain('judge_2')
  })

  it('N4 切换节点：上一个节点的测试结果必须清空（用户实测上报）', async () => {
    const w = mountForm()
    const cb = await clickTest(w)

    cb.onSuccess({
      TaskCode: 'T4', Success: true, UnresolvedInputs: [], DurationMs: 3,
      Output: { fieldCode: 'relatedDocuments', value: 'XASL-QP-002', confidence: 1 }
    })
    await flushPromises()
    expect(w.text()).toContain('relatedDocuments')

    // 切到另一个节点 ⇒ 旧结果（含 JSON 输出、成功/失败样式）不得留在新节点的面板上
    await w.setProps({
      selectedNode: {
        nodeId: 'other_node_n2', nodeType: 'skill', title: '另一个节点', skillCode: 'compare',
        inputs: {}, inputTypes: {}, inputPorts: [], outputPorts: [], config: {}
      }
    })
    await flushPromises()

    expect(w.text()).not.toContain('relatedDocuments')
    expect(w.text()).not.toContain('执行成功')
    expect(w.find('.test-result').exists()).toBe(false)
  })

  it('N5 同一节点重渲染：结果必须保留（清理不能做得过分）', async () => {
    const w = mountForm()
    const cb = await clickTest(w)
    cb.onSuccess({ TaskCode: 'T5', Success: true, Output: { keep: 1 }, UnresolvedInputs: [] })
    await flushPromises()

    // 同一个 nodeId（只是画布重渲染 / 选中态刷新）⇒ 不能把用户刚看到的结果抹掉
    await w.setProps({
      selectedNode: {
        nodeId: 'collect_judges_n1', nodeType: 'skill', title: '聚合多路judge', skillCode: 'collect_judges',
        inputs: { judge_1: 'stamp_judge_level_n1' }, inputTypes: { judge_1: 'link' },
        inputPorts: [{ name: 'judge_1', type: 'json', bindMode: 'Link' }], outputPorts: [], config: {}
      }
    })
    await flushPromises()

    expect(w.text()).toContain('执行成功')
    expect(w.text()).toContain('keep')
  })

  it('N6 连续两次测试：前一次的结果必须被清掉（不能「旧错误挂在新结果上」）', async () => {
    const w = mountForm()

    const cb1 = await clickTest(w)
    cb1.onSuccess({ Success: false, Error: '第一次失败', UnresolvedInputs: ['judge_1'] })
    await flushPromises()
    expect(w.text()).toContain('第一次失败')

    const cb2 = await clickTest(w)
    await flushPromises()
    // 点按钮即清空 ⇒ 此刻既不该显示旧错误，也不该有告警
    expect(w.text()).not.toContain('第一次失败')

    cb2.onSuccess({ Success: true, Output: {}, UnresolvedInputs: [] })
    await flushPromises()
    expect(w.text()).toContain('执行成功')
    expect(w.text()).not.toContain('第一次失败')
  })
})
