/**
 * 锚点规则 —— 抽屉接线与保存载荷（组件测试）
 *
 * 【为什么测它 —— 本次改动引入的三种**静默**失败】
 *   ① 「点『配置规则』→ 右侧抽屉」是 2026-10-05 的结构性改动。
 *      若 `v-model:visible` 的接线断了，表现是**点一下什么都不发生**（不报错）；
 *      若 `closed` 事件没接上，表现是「点 A → 取消 → 再点 A，面板里还是上次没保存的内容」。
 *   ② 「必填项」开关走 `saveAnchorBatch` 的**整行 upsert**（`uk_tpl_anchor`）。
 *      若只提交 `{ Code, Required }`，其余业务列会被写成 CLR 默认值 ——
 *      **保存成功、无报错**，但来源配置全被清空。这是本页最贵的一类事故。
 *   ③ 保存后必须**关闭抽屉 + 刷新列表**，否则用户不知道存没存上。
 *
 * 做法：挂**真实** `AnchorRuleTab` + **真实** `AnchorSidePanel`，只把
 * `YzhDrawer`（外壳）与后端 API 换成桩 —— 于是测到的是真实的 props/emit 通路。
 */
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'

// ⚠️ vi.mock 会被提升到文件顶部
//
// ★ 第 28 轮改为「**只替换 UI 桩，其余用真货**」（`importOriginal`）：
//   本测试现在挂的是**真实** `DocFillRuleLogic`，而它 `extends TreeTableLogic` ——
//   若整包 mock 掉 `@yzh-core`，`TreeTableLogic` 会变成 `undefined`，
//   `class X extends undefined` 在**模块加载期**就抛（整份测试文件收集失败）。
vi.mock('@yzh-core', async (importOriginal) => {
  const actual = await importOriginal<Record<string, any>>()
  return {
    ...actual,
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
    /** 抽屉外壳桩：把 `modelValue` / `title` 摊到 DOM 上，便于断言「开没开、开的哪一条」 */
    YzhDrawer: {
      name: 'YzhDrawer',
      props: [
        'modelValue',
        'title',
        'size',
        'confirmText',
        'confirmDisabled',
        'confirmLoading',
      ],
      emits: ['update:modelValue', 'confirm', 'cancel', 'open', 'close', 'closed'],
      template:
        '<div class="drawer-stub" :data-open="String(modelValue)">' +
        '<span class="drawer-title">{{ title }}</span>' +
        '<slot />' +
        '</div>',
    },
    /** 信封判定桩：本测试只关心「传了什么」，信封语义另有单测 */
    unwrapOk: (r: any) => r,
  }
})

vi.mock('@share/api/workflow/doc-fill-rule', () => ({
  listFillParamDefs: vi.fn(async () => ({ data: { Items: [] } })),
  saveAnchorBatch: vi.fn(async () => ({ success: true })),
  // ★ 第 28 轮（C7）：行内「锁定」开关走它
  lockAnchor: vi.fn(async () => ({
    success: true,
    data: { Locked: true, Affected: 1, Warnings: [] },
  })),
}))

import * as api from '@share/api/workflow/doc-fill-rule'
import AnchorRuleTab from './AnchorRuleTab.vue'
import { DocFillRuleLogic } from '../logic'

const mockApi = api as unknown as Record<string, ReturnType<typeof vi.fn>>

const TEMPLATE_CODE = 'TPL-1'

function makeRows() {
  return [
    {
      Code: 'A1',
      TemplateCode: TEMPLATE_CODE,
      AnchorRef: 'ENT_NAME',
      AnchorType: 'scalar',
      Position: '',
      Required: false,
      IsOrphan: false,
      IsLocked: false,
      WriteMode: 'overwrite',
      ValueType: 'text',
      DefaultText: '未填写',
      NumberFormat: '',
      FieldCode: 'enterprise.name',
      Remark: '备注 A',
      SourceSpec: JSON.stringify({
        combine: 'firstHit',
        sources: [{ kind: 'global', ref: 'ENT_NAME' }],
      }),
    },
    {
      Code: 'A2',
      TemplateCode: TEMPLATE_CODE,
      AnchorRef: 'AUDIT_DATE',
      AnchorType: 'scalar',
      Position: '',
      Required: true,
      IsOrphan: false,
      IsLocked: false,
      WriteMode: 'overwrite',
      ValueType: 'date',
      DefaultText: '',
      NumberFormat: 'yyyy-MM-dd',
      FieldCode: '',
      Remark: '',
      SourceSpec: '', // 未配来源
    },
  ]
}

/**
 * 真实 Logic + 两个出口被换成 spy 后的类型。
 *
 * ⚠️ 必须把这两个方法交叉成 `any`：`vi.spyOn` 返回的是 mock，但**变量静态类型**
 *   仍是类上的普通方法 ⇒ 直接写 `logic.updateAnchor.mock` 会 TS2339。
 *   （用 `as any` 一把梭也行，但那样连 `selectedNode` / `anchorReadiness` 都失去提示。）
 */
type LogicMock = DocFillRuleLogic & {
  loadAnchors: any
  updateAnchor: any
}

/**
 * ★ 第 28 轮：改用**真实** `DocFillRuleLogic`（只把两个会发请求的方法换成 spy）。
 *
 * 【为什么不再用手搓的 mock 对象】
 *   本轮把「锚点清单」上移成了 `logic` 的**共享仓库**（`anchorRows` + 三个
 *   `computed` 派生）。手搓 mock 就得把 `anchorViews` / `anchorStats` /
 *   `anchorReadiness` / `reloadAnchors` 全部复写一遍 —— 那是**在测试里重写一遍生产逻辑**，
 *   它永远不会发现生产逻辑写错。用真实例 + 只桩住出口，测到的是真通路。
 */
function makeLogic(): LogicMock {
  const logic = new DocFillRuleLogic()
  vi.spyOn(logic, 'loadAnchors').mockResolvedValue(makeRows())
  vi.spyOn(logic, 'updateAnchor')
  // `anchorReadiness` / `templateCode` 都从选中节点读 ⇒ 必须给一个「已上传模板」的文件叶子
  logic.selectedNode = {
    Code: 'X1',
    Name: 'X1.docx',
    NodeType: 'file',
    IsLeaf: true,
    Extra: {
      kind: 'file',
      rawName: 'X1.docx',
      hasTemplate: true,
      templateCode: TEMPLATE_CODE,
      scanStatus: 'completed',
    },
    Children: [],
  } as any
  return logic as LogicMock
}

const stubs = {
  'el-button': {
    template:
      '<button class="eb" :disabled="disabled" @click="$emit(\'click\')"><slot /></button>',
    props: ['disabled', 'loading', 'type', 'size', 'icon', 'link', 'text', 'plain'],
  },
  'el-switch': {
    name: 'ElSwitch',
    props: ['modelValue', 'size', 'loading', 'ariaLabel'],
    emits: ['change'],
    template:
      '<button class="sw-stub" :data-on="String(modelValue)" @click="$emit(\'change\')" />',
  },
  'el-icon': { template: '<i class="ei"><slot /></i>' },
  /**
   * ⚠️ `InfoFilled` 在 `AnchorSidePanel` 的模板里**没有 import** —— 它靠宿主
   *   `main.ts` 的「注册全部 Element Plus 图标」全局可用（项目既有约定）。
   *   测试环境没有那次注册，必须补桩，否则每个用例都会刷一条 `Failed to resolve component`。
   */
  InfoFilled: { template: '<i class="ei-info" />' },
  'el-popover': {
    template: '<span class="ep"><slot name="reference" /><slot /></span>',
  },
  'el-alert': {
    template: '<div class="ea">{{ title }}<slot /></div>',
    props: ['type', 'title', 'showIcon', 'closable'],
  },
  'el-select': { template: '<div class="esel"><slot /></div>', props: ['modelValue'] },
  'el-option': { template: '<div class="eopt" />', props: ['value', 'label', 'disabled'] },
  'el-input': {
    template: '<input class="ein" />',
    props: ['modelValue', 'placeholder', 'size', 'disabled', 'type', 'rows'],
  },
  'el-input-number': {
    template: '<input class="einnum" />',
    props: ['modelValue', 'size', 'min', 'max', 'step', 'controls', 'placeholder'],
  },
  'el-radio-group': {
    template: '<div class="erg"><slot /></div>',
    props: ['modelValue', 'size'],
  },
  'el-radio-button': {
    template: '<button class="erb"><slot /></button>',
    props: ['value', 'label'],
  },
}

async function mountTab(logic: ReturnType<typeof makeLogic>) {
  const w = mount(AnchorRuleTab, {
    props: {
      logic,
      templateCode: TEMPLATE_CODE,
      hasTemplate: true,
      scanStatus: 'completed',
    },
    global: {
      stubs,
      directives: { loading: {} },
    },
  })
  await flushPromises()
  return w
}

/** 抽屉外壳桩（断言开关与标题） */
function drawer(w: Awaited<ReturnType<typeof mountTab>>) {
  return w.findComponent({ name: 'YzhDrawer' })
}

function cards(w: Awaited<ReturnType<typeof mountTab>>) {
  return w.findAll('.ac')
}

beforeEach(() => {
  vi.clearAllMocks()
  mockApi.saveAnchorBatch.mockResolvedValue({ success: true })
})

describe('AnchorRuleTab — 列表与统计', () => {
  it('加载锚点后按行渲染卡片，统计条给出总数', async () => {
    const logic = makeLogic()
    const w = await mountTab(logic)
    expect(logic.loadAnchors).toHaveBeenCalledWith()
    expect(cards(w).length).toBe(2)
    expect(w.find('.stat').text()).toContain('2 锚点')
  })

  it('「未配来源」筛选只留没配来源的那一条', async () => {
    const w = await mountTab(makeLogic())
    const chip = w
      .findAll('.filters .f')
      .find((b) => b.text().includes('未配来源'))!
    await chip.trigger('click')
    expect(cards(w).length).toBe(1)
    expect(cards(w)[0].text()).toContain('AUDIT_DATE')
  })
})

describe('AnchorRuleTab — 「配置规则」走右侧抽屉', () => {
  it('★ 点卡片 ⇒ 抽屉打开，且标题带上该锚点', async () => {
    const w = await mountTab(makeLogic())
    expect(drawer(w).props('modelValue')).toBe(false)

    await cards(w)[0].trigger('click')
    expect(drawer(w).props('modelValue')).toBe(true)
    expect(drawer(w).props('title')).toContain('ENT_NAME')
  })

  it('★ 抽屉「离场动画结束」（closed）才清掉当前锚点 —— 下次点同一行能重新初始化', async () => {
    const w = await mountTab(makeLogic())
    await cards(w)[0].trigger('click')
    expect(drawer(w).props('title')).toContain('ENT_NAME')

    // `close` 阶段不清（否则抽屉滑出时内容先消失，肉眼可见闪空）
    drawer(w).vm.$emit('close')
    await flushPromises()
    expect(drawer(w).props('title')).toContain('ENT_NAME')

    // `closed` 阶段清
    drawer(w).vm.$emit('closed')
    await flushPromises()
    expect(drawer(w).props('title')).not.toContain('ENT_NAME')

    // 再点同一行 ⇒ 仍然能正确带上它
    await cards(w)[0].trigger('click')
    expect(drawer(w).props('title')).toContain('ENT_NAME')
  })

  it('★ 换模板（templateCode 变）⇒ 关抽屉并清空列表', async () => {
    const logic = makeLogic()
    const w = await mountTab(logic)
    await cards(w)[0].trigger('click')
    expect(drawer(w).props('modelValue')).toBe(true)

    await w.setProps({ templateCode: 'TPL-2' })
    await flushPromises()
    expect(drawer(w).props('modelValue')).toBe(false)
    expect(drawer(w).props('title')).not.toContain('ENT_NAME')
    expect(cards(w).length).toBe(2) // 新模板重新拉回两行
  })
})

describe('AnchorRuleTab — 保存必须「整行 upsert」', () => {
  it('★ 抽屉确认 ⇒ 提交**原始整行**（业务列一个都不能丢），随后关抽屉并刷新列表', async () => {
    const logic = makeLogic()
    const w = await mountTab(logic)
    const loadCallsBefore = logic.loadAnchors.mock.calls.length

    await cards(w)[0].trigger('click')
    drawer(w).vm.$emit('confirm')
    await flushPromises()

    expect(mockApi.saveAnchorBatch).toHaveBeenCalledTimes(1)
    const [tplCode, rows] = mockApi.saveAnchorBatch.mock.calls[0]
    expect(tplCode).toBe(TEMPLATE_CODE)
    expect(rows).toHaveLength(1)

    const payload = rows[0]
    // 身份与归属列
    expect(payload.Code).toBe('A1')
    expect(payload.AnchorRef).toBe('ENT_NAME')
    expect(payload.TemplateCode).toBe(TEMPLATE_CODE)
    // 未被表单编辑、但**必须原样回写**的业务列（丢了就是静默清空）
    expect(payload.AnchorType).toBe('scalar')
    expect(payload.FieldCode).toBe('enterprise.name')
    expect(payload.Remark).toBe('备注 A')
    expect(payload.DefaultText).toBe('未填写')
    // 表单列
    expect(payload.WriteMode).toBe('overwrite')
    expect(payload.ValueType).toBe('text')
    expect(payload.Required).toBe(false)
    // 来源链：JSON 往返后仍是原来那条
    const spec = JSON.parse(payload.SourceSpec)
    expect(spec.combine).toBe('firstHit')
    expect(spec.sources).toHaveLength(1)
    expect(spec.sources[0]).toMatchObject({ kind: 'global', ref: 'ENT_NAME' })

    // 保存成功 ⇒ 关抽屉 + 通知父页 + 重载列表
    expect(drawer(w).props('modelValue')).toBe(false)
    expect(w.emitted('saved')).toBeTruthy()
    expect(logic.loadAnchors.mock.calls.length).toBe(loadCallsBefore + 1)
  })

  it('保存失败 ⇒ 抽屉**不关**（用户还得接着改），也不发 saved', async () => {
    mockApi.saveAnchorBatch.mockRejectedValueOnce(new Error('后端拒绝了'))
    const w = await mountTab(makeLogic())

    await cards(w)[0].trigger('click')
    drawer(w).vm.$emit('confirm')
    await flushPromises()

    expect(drawer(w).props('modelValue')).toBe(true)
    expect(w.emitted('saved')).toBeFalsy()
  })
})

describe('AnchorRuleTab — 「必填项」就地开关', () => {
  it('★ 开关 ⇒ 调 logic.updateAnchor 且传的是**整行**（含 Code 与来源配置）', async () => {
    const logic = makeLogic()
    const w = await mountTab(logic)
    expect(cards(w)[0].find('.sw-stub').attributes('data-on')).toBe('false')

    await cards(w)[0].find('.sw-stub').trigger('click')
    await flushPromises()

    expect(logic.updateAnchor).toHaveBeenCalledTimes(1)
    const row = logic.updateAnchor.mock.calls[0][0]
    expect(row.Code).toBe('A1')
    expect(row.Required).toBe(true)
    expect(row.TemplateCode).toBe(TEMPLATE_CODE)
    expect(JSON.parse(row.SourceSpec).sources).toHaveLength(1)
    // 开关进入「已开」态
    expect(cards(w)[0].find('.sw-stub').attributes('data-on')).toBe('true')
    expect(w.emitted('saved')).toBeTruthy()
  })

  it('★ 保存失败 ⇒ 开关回滚（不能留下「界面已改、库里没改」）', async () => {
    const logic = makeLogic()
    let submitted: any = null
    logic.updateAnchor.mockImplementationOnce(async (row: any) => {
      submitted = row.Required
      throw new Error('网络断了')
    })
    const w = await mountTab(logic)
    expect(cards(w)[0].find('.sw-stub').attributes('data-on')).toBe('false')

    await cards(w)[0].find('.sw-stub').trigger('click')
    await flushPromises()

    expect(submitted).toBe(true) // 提交的是翻转后的值
    expect(cards(w)[0].find('.sw-stub').attributes('data-on')).toBe('false') // 回滚
    expect(w.emitted('saved')).toBeFalsy()
  })

  it('点开关**不会**打开抽屉（`.ac-op` 上的 `@click.stop` 生效）', async () => {
    const w = await mountTab(makeLogic())
    await cards(w)[0].find('.sw-stub').trigger('click')
    await flushPromises()
    expect(drawer(w).props('modelValue')).toBe(false)
  })
})
