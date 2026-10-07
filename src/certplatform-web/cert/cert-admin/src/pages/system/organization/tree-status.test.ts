/**
 * 启用/禁用徽章契约（`status-field`）—— 原子组件 + 唯一判据测试
 *
 * 覆盖三处消费方：`YzhTree`（左树零绑定）、`YzhTreeTableCheckSelector`（勾选树表 Name 列）、
 * 判据本体 `utils/status.ts` 的 `resolveStatusBadge`（直用 el-tree 的页面也走它）。
 *
 * 背景：机构-人员管理、菜单、角色等所有左树都要一眼看出节点「启用 / 停用」。
 * 该能力**统一落在组件层、页面零绑定**（2026-10-07 起由「每页手动绑 status-field」
 * 改为组件默认行为），故契约必须锁死：
 *   · 不传 status-field         → 默认读 `Extra.IsValid`，自动渲染（全站统一）
 *   · `statusField: ''`         → 显式关闭徽章
 *   · status-field 命中 Extra   → 按 1 / true / '1' / 'true' 判启用，其余判停用
 *   · Extra 双 Key              → PascalCase 与 camelCase 都认
 *   · 节点无该字段              → 该节点不标（无启停语义的树不受影响）
 *   · 文案传空串                → 该状态不标注（长树只标停用）
 *
 * 位置说明：vitest 的 include 为 cert-admin 的 `src/**`（见 cert-admin/vitest.config.ts），
 * 与 logic.test.ts 同目录以便一次 `vitest run` 覆盖内核 + 页面两层。
 */

import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { h, shallowRef, watch } from 'vue'
import YzhTree from '@yzh-core/components/layout/YzhTree.vue'
import YzhTreeTableCheckSelector from '@yzh-core/components/layout/YzhTreeTableCheckSelector.vue'
import { resolveStatusBadge } from '@yzh-core/utils/status'

/** 三态节点：启用 / 停用 / 无状态字段 */
const NODES = [
  { Code: 'A', Name: '已启用机构', IsLeaf: true, Extra: { IsValid: 1 } },
  { Code: 'B', Name: '已停用机构', IsLeaf: true, Extra: { IsValid: 0 } },
  { Code: 'C', Name: '无状态字段', IsLeaf: true, Extra: {} },
]

function mountTree(props: Record<string, any> = {}) {
  return mount(YzhTree, { props: { data: NODES, ...props } })
}

describe('YzhTree 节点状态徽章（status-field）', () => {
  it('不传 status-field → 默认读 Extra.IsValid：启用标「启用」、停用标「禁用」、无字段不标（页面零绑定）', () => {
    const w = mountTree()
    expect(w.findAll('.yzh-status-badge').map((n) => n.text())).toEqual(['启用', '禁用'])
  })

  it("statusField 传空串 → 显式关闭，一个徽章都不渲染", () => {
    const w = mountTree({ statusField: '' })
    expect(w.find('.yzh-status-badge').exists()).toBe(false)
  })

  it('显式传 status-field → 与默认同契约：启用/停用/无字段三态', () => {
    const w = mountTree({ statusField: 'IsValid' })
    const texts = w.findAll('.yzh-status-badge').map((n) => n.text())
    expect(texts).toEqual(['启用', '禁用'])
  })

  it('Extra 双 Key：camelCase（isValid）同样识别', () => {
    const w = mount(YzhTree, {
      props: {
        data: [{ Code: 'X', Name: '停用', IsLeaf: true, Extra: { isValid: 0, IsValid: 0 } }],
        statusField: 'IsValid',
      },
    })
    expect(w.findAll('.yzh-status-badge').map((n) => n.text())).toEqual(['禁用'])
  })

  it("启用文案传空串 → 只标注停用节点（长树推荐）", () => {
    const w = mountTree({ statusField: 'IsValid', statusEnabledText: '' })
    expect(w.findAll('.yzh-status-badge').map((n) => n.text())).toEqual(['禁用'])
  })

  it('可自定义启停文案', () => {
    const w = mountTree({
      statusField: 'IsValid',
      statusEnabledText: '已启用',
      statusDisabledText: '已停用',
    })
    expect(w.findAll('.yzh-status-badge').map((n) => n.text())).toEqual(['已启用', '已停用'])
  })

  it("字符串 '1' / 'true' 视为启用（与内核 isRowEnabled 同源）", () => {
    const w = mount(YzhTree, {
      props: {
        data: [
          { Code: 'S1', Name: 'A', IsLeaf: true, Extra: { IsValid: '1' } },
          { Code: 'S2', Name: 'B', IsLeaf: true, Extra: { IsValid: 'true' } },
        ],
        statusField: 'IsValid',
        statusDisabledText: '停',
      },
    })
    expect(w.findAll('.yzh-status-badge').map((n) => n.text())).toEqual(['启用', '启用'])
  })

  it('状态字段名可参数化（不硬编码 IsValid）', () => {
    const w = mount(YzhTree, {
      props: {
        data: [{ Code: 'P', Name: '合作中', IsLeaf: true, Extra: { Status: 0 } }],
        statusField: 'Status',
      },
    })
    expect(w.findAll('.yzh-status-badge').map((n) => n.text())).toEqual(['禁用'])
  })
})

// ============================================================
// 唯一判据：utils/status.ts（YzhTree / CheckSelector / 直用 el-tree 页面三处共用）
// ============================================================

describe('resolveStatusBadge（徽章唯一判据）', () => {
  it('Extra 优先、节点顶层兜底；PascalCase / camelCase 双 Key 认读', () => {
    expect(resolveStatusBadge({ Extra: { IsValid: 0 } }, 'IsValid')).toEqual({ text: '禁用', type: 'info' })
    expect(resolveStatusBadge({ Extra: { isValid: 1 } }, 'IsValid')).toEqual({ text: '启用', type: 'success' })
    expect(resolveStatusBadge({ IsValid: 1 }, 'IsValid')).toEqual({ text: '启用', type: 'success' })
    expect(resolveStatusBadge({ isValid: 0 }, 'IsValid')).toEqual({ text: '禁用', type: 'info' })
  })

  it('statusField 空串 / 字段缺失 / data 空 → null（不渲染徽章）', () => {
    expect(resolveStatusBadge({ IsValid: 1 }, '')).toBeNull()
    expect(resolveStatusBadge({ Extra: {} }, 'IsValid')).toBeNull()
    expect(resolveStatusBadge(null, 'IsValid')).toBeNull()
  })

  it('文案空串 = 该状态不标注', () => {
    expect(resolveStatusBadge({ IsValid: 1 }, 'IsValid', '', '禁用')).toBeNull()
    expect(resolveStatusBadge({ IsValid: 0 }, 'IsValid', '启用', '')).toBeNull()
  })

  it("判据与内核同源：1 / true / '1' / 'true' 判启用，其余判停用", () => {
    for (const v of [1, true, '1', 'true', 'TRUE']) {
      expect(resolveStatusBadge({ IsValid: v }, 'IsValid')?.type).toBe('success')
    }
    for (const v of [0, false, '0', 'false', 2]) {
      expect(resolveStatusBadge({ IsValid: v }, 'IsValid')?.type).toBe('info')
    }
  })
})

describe('YzhTreeTableCheckSelector 启用/禁用徽章（statusField）', () => {
  const ROWS = [
    { Code: 'M1', ParentCode: null, NodeType: 'menu', CheckFlag: false, Name: '已启用菜单', Extra: { IsValid: 1 } },
    { Code: 'M2', ParentCode: null, NodeType: 'menu', CheckFlag: false, Name: '已停用菜单', Extra: { IsValid: 0 } },
    { Code: 'M3', ParentCode: null, NodeType: 'menu', CheckFlag: false, Name: '无状态字段', Extra: {} },
  ]

  // el-table 在 happy-dom 下不渲染单元格（仓库惯例：测试打桩）。
  // 这里按最小契约还原「el-table 持有行数据 → el-table-column 逐行调 default slot」，
  // 只为断言**本组件**的徽章挂载列与文案；判据本身已由上方 resolveStatusBadge 契约锁死。
  const rowsRef = shallowRef<any[]>([])
  const ElTableStub = {
    name: 'ElTableStub',
    props: ['data'],
    // 组件 initCheckedState / syncTableCheckState 会调 el-table 实例方法（桩上必须存在）
    methods: {
      clearSelection() {},
      toggleRowSelection() {},
    },
    setup(props: any, { slots }: any) {
      watch(() => props.data, (v) => (rowsRef.value = v ?? []), { immediate: true, deep: true })
      return () => h('div', { class: 'fake-table' }, slots.default?.())
    },
  }
  const ElTableColumnStub = {
    name: 'ElTableColumnStub',
    props: ['prop', 'label'],
    setup(props: any, { slots }: any) {
      return () =>
        h(
          'div',
          { class: 'fake-col', 'data-label': props.label },
          rowsRef.value.map((row) =>
            h('div', { class: 'fake-cell', key: row.Code }, [
              slots.default?.({ row, column: { prop: props.prop } }),
            ]),
          ),
        )
    },
  }

  function mountSelector(props: Record<string, any> = {}) {
    return mount(YzhTreeTableCheckSelector, {
      props: {
        flatData: ROWS,
        columns: [{ prop: 'Name', label: '名称' }],
        typeLabels: { menu: '菜单' },
        typeTagTypes: { menu: 'info' },
        checkAllExcludeTypes: [],
        ...props,
      },
      global: {
        stubs: {
          'el-table': ElTableStub,
          'el-table-column': ElTableColumnStub,
        },
      },
    })
  }

  it('默认渲染徽章：启用标「启用」、停用标「禁用」、无字段不标（Name 列，页面零绑定）', () => {
    const w = mountSelector()
    expect(w.findAll('.yzh-status-badge').map((n) => n.text())).toEqual(['启用', '禁用'])
  })

  it("statusField 传空串 → 显式关闭，一个徽章都不渲染", () => {
    const w = mountSelector({ statusField: '' })
    expect(w.findAll('.yzh-status-badge').length).toBe(0)
  })

  it('徽章挂在 statusNameField 列（默认 Name）；其余列不挂', async () => {
    const w = mountSelector({
      flatData: [{ ...ROWS[1], Method: 'GET' }],
      columns: [
        { prop: 'Name', label: '名称' },
        { prop: 'Method', label: '方法' },
      ],
    })
    await w.vm.$nextTick()
    const cols = w.findAll('.fake-col')
    const byLabel = (label: string) => cols.filter((c) => c.attributes('data-label') === label)
    expect(cols.length).toBeGreaterThanOrEqual(2)
    expect(byLabel('名称')).toHaveLength(1)
    expect(byLabel('方法')).toHaveLength(1)
    expect(byLabel('名称').at(0)!.findAll('.yzh-status-badge').map((n) => n.text())).toEqual(['禁用'])
    expect(byLabel('方法').at(0)!.findAll('.yzh-status-badge')).toHaveLength(0)
  })
})
