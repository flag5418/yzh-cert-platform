/**
 * YzhTable 新能力回归测试（2026-10-17 · 第 2 批组件补齐）
 *
 * 【为什么测它】第 2 批把 4 处裸 `<el-table>` 收进 `YzhTable`，为此给内核加了三个能力。
 * 这三个能力都有「不报错、只静默出错」的失败模式，靠类型检查拦不住：
 *
 *   ① `slotKey` —— 无 `prop` 的展示列。不给 slotKey 时插槽名会变成
 *      `column-undefined`，页面写了 `#column-Status` 也**渲染成空白**。
 *   ② `data` 静态通道 —— 与 `dataLoader` 二选一。若误走 loader 分支会拿不到数据。
 *   ③ `type:'expand'` 展开列 —— 列循环若没单独分支，展开图标根本不出现。
 *
 * 【判据】断言的是**渲染出来的文字**，不是内部状态 —— 这三条都是「用户看得见」的行为。
 *
 * 为什么放 cert-admin：vitest 只扫 `cert-admin/src`（vitest.config.ts include），
 * 与 `yzh-form-switch.test.ts` 同一处置方式（本机无 Chromium，走 happy-dom 组件测试）。
 */
import { describe, it, expect } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import YzhTable from '@yzh-core/components/table/YzhTable.vue'
import type { YzhTableColumn } from '@yzh-core/components/table/types'

interface Row {
  Code: string
  Name: string
  Status: string
}

const rows: Row[] = [
  { Code: 'A1', Name: '甲', Status: 'ok' },
  { Code: 'A2', Name: '乙', Status: 'bad' },
]

/** 挂载：统一关掉工具栏与分页，只测列渲染 */
function mountTable(columns: YzhTableColumn<Row>[], extra: Record<string, any> = {}, slots: Record<string, any> = {}) {
  return mount(YzhTable as any, {
    props: {
      columns,
      data: rows,
      toolbar: false,
      showPagination: false,
      rowKey: 'Code',
      ...extra,
    },
    slots,
    global: { plugins: [ElementPlus] },
  })
}

describe('YzhTable · slotKey（无 prop 的展示列）', () => {
  it('prop 为空 + slotKey 时，插槽内容正常渲染（不会变成 column-undefined 空白列）', async () => {
    const columns: YzhTableColumn<Row>[] = [
      { prop: 'Name', label: '名称' },
      { slotKey: 'Status', label: '状态', slot: true },
    ]
    const wrapper = mountTable(columns, {}, {
      'column-Status': `<template #column-Status="{ row }">状态:{{ row.Status }}</template>`,
    })
    await flushPromises()

    const text = wrapper.text()
    expect(text).toContain('名称')
    expect(text).toContain('状态')
    // 关键断言：插槽真的命中了（若 slotKey 没生效，这里只有表头没有单元格文字）
    expect(text).toContain('状态:ok')
    expect(text).toContain('状态:bad')
  })

  it('有 prop 又标了 slot 时，仍按 prop 命名插槽（向后兼容）', async () => {
    const columns: YzhTableColumn<Row>[] = [
      { prop: 'Name', label: '名称', slot: true },
    ]
    const wrapper = mountTable(columns, {}, {
      'column-Name': `<template #column-Name="{ row }">【{{ row.Name }}】</template>`,
    })
    await flushPromises()

    expect(wrapper.text()).toContain('【甲】')
    expect(wrapper.text()).toContain('【乙】')
  })
})

describe('YzhTable · data 静态通道', () => {
  it('给 data 就直出内存数组，不需要 dataLoader', async () => {
    const columns: YzhTableColumn<Row>[] = [{ prop: 'Name', label: '名称' }]
    const wrapper = mountTable(columns)
    await flushPromises()

    expect(wrapper.text()).toContain('甲')
    expect(wrapper.text()).toContain('乙')
  })

  it('data 引用变化后自动重载（原地 push 不触发，这是约定不是 bug）', async () => {
    const columns: YzhTableColumn<Row>[] = [{ prop: 'Name', label: '名称' }]
    const wrapper = mountTable(columns)
    await flushPromises()

    await wrapper.setProps({ data: [...rows, { Code: 'A3', Name: '丙', Status: 'ok' }] })
    await flushPromises()
    expect(wrapper.text()).toContain('丙')
  })

  it('data 与 dataLoader 都不给时报错，而不是静默空白', async () => {
    const wrapper = mount(YzhTable as any, {
      props: {
        columns: [{ prop: 'Name', label: '名称' }],
        toolbar: false,
        showPagination: false,
      },
      global: { plugins: [ElementPlus] },
    })
    await flushPromises()
    expect(wrapper.text()).toContain('dataLoader')
  })
})

describe('YzhTable · type=expand 展开列', () => {
  it('展开列渲染出展开图标，且展开后 #expand 插槽内容可见', async () => {
    const columns: YzhTableColumn<Row>[] = [
      { type: 'expand', width: 40 },
      { prop: 'Name', label: '名称' },
    ]
    const wrapper = mountTable(columns, {}, {
      expand: `<template #expand="{ row }">明细-{{ row.Name }}</template>`,
    })
    await flushPromises()

    const icons = wrapper.findAll('.el-table__expand-icon')
    expect(icons.length).toBe(rows.length)

    await icons[0].trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('明细-甲')
  })
})
