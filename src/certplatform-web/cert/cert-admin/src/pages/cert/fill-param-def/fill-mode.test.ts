/**
 * AD-1 表格列填充模式（FillMode）测试 —— 2026-10-19
 *
 * 覆盖用户实测缺陷：`/business/fill-param-def` 表格只有 6 列，却在右侧留大片空白。
 * 根因：后端下发的 `FillMode`（AutoFix / PixFix）在前端**从未被消费**（死配置），
 *       `toTableColumns` 一律按 `Width` 像素渲染 ⇒ 列窄、右侧留白。
 * 修法：
 *   · AutoFix → 列设 `minWidth`（不设 width），交 el-table 按 minWidth 等比例填满容器；
 *   · PixFix  → 设 `width`（固定像素，超出时横向滚动）；
 *   · 显示列 < `FILL_MODE_MIN_COLUMNS`（10）时一律按 AutoFix（用户裁决）。
 *
 * 断言一律写「期望的正确行为」：若线上代码仍漏消费 FillMode，对应用例为红（缺陷证据）。
 */

import { describe, it, expect } from 'vitest'
import { toTableColumns, FILL_MODE_MIN_COLUMNS } from '@yzh-core'
import type { EntityConfigDto } from '@yzh-core/types/contracts'

/** 造一个最小可用列定义 */
function col(FieldName: string, Width?: number, extra: Record<string, any> = {}) {
  return {
    FieldName,
    DesName: FieldName,
    Type: 'TextBox',
    XsFlag: true,
    BcFlag: true,
    Yxk: true,
    Width,
    ...extra,
  }
}

/** 造一份最小可用 EntityConfig（含 FillMode） */
function cfg(FillMode: 'AutoFix' | 'PixFix' | undefined, columns: any[]): EntityConfigDto {
  return { Title: 't', FillMode, Columns: columns } as unknown as EntityConfigDto
}

/** 造 n 个显示列 */
function nCols(n: number) {
  return Array.from({ length: n }, (_v, i) => col(`F${i}`, 100 + i))
}

describe('FILL_MODE_MIN_COLUMNS', () => {
  it('阈值为 10', () => {
    expect(FILL_MODE_MIN_COLUMNS).toBe(10)
  })
})

describe('AD-1 toTableColumns · FillMode 消费', () => {
  it('AutoFix → 列用 minWidth 撑满（不设 width），保留配置宽度为最小宽', () => {
    const cols = toTableColumns(
      cfg('AutoFix', [col('ParamCode', 170), col('ParamName', 160), col('ValueType', 100)]),
    )
    expect(cols).toHaveLength(3)
    for (const c of cols) {
      expect(c.width).toBeUndefined()
    }
    expect(cols[0].minWidth).toBe(170)
    expect(cols[1].minWidth).toBe(160)
    expect(cols[2].minWidth).toBe(100)
  })

  it('FillMode 缺失（undefined）→ 等价 AutoFix（撑满）', () => {
    const cols = toTableColumns(cfg(undefined, [col('A', 120)]))
    expect(cols[0].width).toBeUndefined()
    expect(cols[0].minWidth).toBe(120)
  })

  it('PixFix 且列数 ≥ 10 → 按像素 width 渲染（允许横向滚动）', () => {
    const cols = toTableColumns(cfg('PixFix', nCols(FILL_MODE_MIN_COLUMNS)))
    expect(cols).toHaveLength(FILL_MODE_MIN_COLUMNS)
    for (const c of cols) {
      expect(c.minWidth).toBeUndefined()
    }
    expect(cols[0].width).toBe(100)
    expect(cols[9].width).toBe(109)
  })

  it('PixFix 但列数 < 10 → 改走 AutoFix（填满，小表不留白）', () => {
    const cols = toTableColumns(cfg('PixFix', nCols(FILL_MODE_MIN_COLUMNS - 1)))
    expect(cols).toHaveLength(FILL_MODE_MIN_COLUMNS - 1)
    for (const c of cols) {
      expect(c.width).toBeUndefined()
      expect(c.minWidth).toBeGreaterThan(0)
    }
  })

  it('未配置 Width → 两者皆不设（交 el-table 默认 flex）', () => {
    const cols = toTableColumns(cfg('AutoFix', [col('NoWidth')]))
    expect(cols[0].width).toBeUndefined()
    expect(cols[0].minWidth).toBeUndefined()
  })

  it('XsFlag=false 的列被过滤（填充判定只看显示列）', () => {
    const cols = toTableColumns(
      cfg('PixFix', [
        ...nCols(9),
        col('Hidden', 100, { XsFlag: false }),
      ]),
    )
    // 9 个显示列 < 10 → 即便配了 PixFix 也走填充
    expect(cols).toHaveLength(9)
    expect(cols.every((c) => c.width === undefined && c.minWidth !== undefined)).toBe(true)
  })

  it('EnableField 列仍自动挂 slot（填充模式下不丢插槽）', () => {
    const config = cfg('AutoFix', [col('IsValid', 90)]) as any
    config.EnableField = 'IsValid'
    const cols = toTableColumns(config)
    expect(cols[0].slot).toBe('IsValid')
    expect(cols[0].minWidth).toBe(90)
    expect(cols[0].width).toBeUndefined()
  })
})
