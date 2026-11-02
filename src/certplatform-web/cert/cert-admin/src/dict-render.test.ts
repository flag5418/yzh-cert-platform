/**
 * 字典翻译回归测试 —— 「列表/弹窗里 0、1 原样上屏」的防线（2026-10-30）
 *
 * 【为什么测它】用户实测 `/audit-member`（专家组设置）：
 *   - 列表「性别」列直接显示 `0` / `1`；
 *   - 编辑弹窗里下拉框也显示 `0` / `1`。
 *   根因是**两层**、且**都不报错**：
 *     ① `YzhTable` 的 `v-else-if="col.dictCode"` 分支此前是空壳（注释「dictStore 将在后续版本引入」），
 *        只把原始值渲染出来 —— 列配置里 `DictCode` 早就下发，但没人消费。
 *     ② `YzhForm` 的字典选项 `value` 恒为**字符串**（`Sys_DictionaryList.DicValue` 是 varchar），
 *        而实体列 `cert_audit_member.Gender` 是 **int** ⇒ `el-select` 用 `===` 比较 `0` 与 `'0'`
 *        不命中：下拉打开没有选中项，输入框退回显示原值。
 *
 * 【判据】断言的是**用户看得见的文字/值类型**：
 *   - 表格：单元格出现「男 / 女」，不再出现裸 `0` / `1`；
 *   - 表单：`el-option` 的 value 是 number，且选中项文本是「男」。
 *
 * 为什么放 cert-admin：vitest 只扫 `cert-admin/src`（vitest.config.ts include），
 * 与 `yzh-table-capabilities.test.ts` / `yzh-form-switch.test.ts` 同一处置方式。
 */
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import { ElOption, ElSelect } from 'element-plus'

// yzhApi 单例打桩（hoisted：vi.mock 会被提升到 import 之前）
const { getMock } = vi.hoisted(() => ({ getMock: vi.fn() }))
vi.mock('@yzh-core/api/client', () => ({ yzhApi: { get: getMock } }))

import YzhTable from '@yzh-core/components/table/YzhTable.vue'
import YzhForm from '@yzh-core/components/form/YzhForm.vue'
import type { YzhFormField } from '@yzh-core/components/form'
import type { YzhTableColumn } from '@yzh-core/components/table/types'
import {
  clearDictCache,
  coerceDictValue,
  dictLabelOf,
  loadDictItems,
} from '@yzh-core/utils/dictionary'

/** 字典 gender 的真实数据（DB 实测：DicValue '0'=男 / '1'=女，均为字符串） */
const GENDER_ITEMS = [
  { Value: '0', Label: '男', Code: 'x1' },
  { Value: '1', Label: '女', Code: 'x2' },
]

interface Row {
  Code: string
  Name: string
  Gender: number
}

const rows: Row[] = [
  { Code: 'A1', Name: '审核员', Gender: 0 },
  { Code: 'A2', Name: '审核组长', Gender: 1 },
]

beforeEach(() => {
  clearDictCache()
  getMock.mockReset()
  getMock.mockResolvedValue({ success: true, data: GENDER_ITEMS })
})

describe('utils/dictionary · 全局字典池', () => {
  it('加载后按 DicNo 缓存，并发/重复调用只打一次接口', async () => {
    await Promise.all([loadDictItems('gender'), loadDictItems('gender')])
    await loadDictItems('gender')

    expect(getMock).toHaveBeenCalledTimes(1)
    expect(getMock.mock.calls[0][0]).toBe('/api/System/Dictionary/items/by-no/gender')
  })

  it('int 值按 String() 松散命中，未命中回落原值', async () => {
    await loadDictItems('gender')
    expect(dictLabelOf('gender', 0)).toBe('男')
    expect(dictLabelOf('gender', 1)).toBe('女')
    // 未命中 / 未加载：回落原值，⛔ 不能抛错或变成 undefined
    expect(dictLabelOf('gender', 9)).toBe(9)
    expect(dictLabelOf('not_loaded', 0)).toBe(0)
  })

  it('接口失败不写缓存，回落原值（不阻塞表格渲染）', async () => {
    getMock.mockResolvedValueOnce({ success: false, err: '字典编码【gender】不存在或已禁用' })
    await loadDictItems('gender')
    expect(dictLabelOf('gender', 0)).toBe(0)
  })

  it('coerceDictValue 按 schema 类型对齐（number / boolean / string）', () => {
    expect(coerceDictValue('0', 'number')).toBe(0)
    expect(coerceDictValue('1', 'boolean')).toBe(true)
    expect(coerceDictValue('0', 'boolean')).toBe(false)
    expect(coerceDictValue('0', 'string')).toBe('0')
    expect(coerceDictValue('0', undefined)).toBe('0')
    // 非数字字符串落 number 列：保持字符串（不产生 NaN）
    expect(coerceDictValue('male', 'number')).toBe('male')
  })
})

describe('YzhTable · dictCode 列翻译', () => {
  const columns: YzhTableColumn<Row>[] = [
    { prop: 'Name', label: '姓名' },
    { prop: 'Gender', label: '性别', dictCode: 'gender', align: 'center' },
  ]

  it('int 列渲染字典文本（不再显示裸 0/1）', async () => {
    const wrapper = mount(YzhTable as any, {
      props: { columns, data: rows, toolbar: false, showPagination: false, rowKey: 'Code' },
      global: { plugins: [ElementPlus] },
    })
    await flushPromises()

    const text = wrapper.text()
    expect(text).toContain('男')
    expect(text).toContain('女')
    // 单元格里不应再有裸数字（表头/其它文本里也没有 0、1，故可直接断言）
    expect(text).not.toMatch(/\b0\b/)
    expect(text).not.toMatch(/\b1\b/)
  })
})

describe('YzhForm · dictCode 选项类型对齐', () => {
  it('int 实体列：字典 value 强转为 number，选中项显示中文', async () => {
    const fields: YzhFormField[] = [
      {
        prop: 'Gender',
        label: '性别',
        type: 'select',
        dictCode: 'gender',
        fieldSchema: { Type: 'number', Optional: false },
      },
    ]
    const wrapper = mount(YzhForm as any, {
      props: { fields, modelValue: { Gender: 0 }, showActions: false },
      global: { plugins: [ElementPlus] },
    })
    await flushPromises()

    const options = wrapper.findAllComponents(ElOption)
    expect(options.length).toBe(2)
    // 关键断言：value 必须是数字，否则 el-select 严格比较不命中
    expect(options[0].props('value')).toBe(0)
    expect(options[1].props('value')).toBe(1)
    expect(wrapper.findComponent(ElSelect).props('modelValue')).toBe(0)
    // 选中项文本走字典 label（不是 0）
    expect(wrapper.text()).toContain('男')
  })
})
