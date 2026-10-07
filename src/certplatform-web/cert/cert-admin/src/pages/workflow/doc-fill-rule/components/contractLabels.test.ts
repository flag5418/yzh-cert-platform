import { describe, expect, it } from 'vitest'
import { labelOf } from './contractLabels'

/**
 * chips 展示文本回归（2026-10-06 用户实测 `[object Object]` 两次返工后立的闸）。
 * 覆盖三种真实数据形态：AI 标签对象 / AI 信息项对象 / 人工字符串。
 */
describe('labelOf（chips 展示文本）', () => {
  it('人工新增的纯字符串原样返回', () => {
    expect(labelOf('年度')).toBe('年度')
    expect(labelOf('  空格串  ')).toBe('  空格串  ')
  })

  it('★ AI 业务标签对象取 tagName（`{tagCode,tagName,…}`）', () => {
    expect(
      labelOf({ tagCode: 'RecordInternalAudit', tagName: '内审记录', confidence: 0.92 }),
    ).toBe('内审记录')
    expect(labelOf({ TagName: '管理评审', TagCode: 'MgmtReview' })).toBe('管理评审')
  })

  it('★ AI 关键信息项对象取 itemName（`{itemName,itemDesc,…}`，首版漏了这个键）', () => {
    expect(
      labelOf({ itemName: '年度', itemDesc: '所覆盖年度', valueType: 'number', isKey: true }),
    ).toBe('年度')
    expect(labelOf({ ItemName: '审核范围', valueType: 'string' })).toBe('审核范围')
  })

  it('无名对象兜底取第一个非空字符串值', () => {
    expect(labelOf({ Required: true, Hint: '填写认证范围' })).toBe('填写认证范围')
  })

  it('纯数字 / 布尔对象兜底摊成 JSON，⛔ 永不返回 [object Object]', () => {
    expect(labelOf({ n: 1 })).toBe('{"n":1}')
    expect(labelOf(2026)).toBe('2026')
    expect(labelOf(null)).toBe('')
    expect(labelOf(undefined)).toBe('')
  })
})
