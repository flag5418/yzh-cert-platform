/**
 * ISO 标准左树「类别 → 标准」分组（foundation/iso-standard/group.ts）单测
 *
 * 覆盖（逐条对应设计约束）：
 *   ① 标签格式 = `编号:年份 名称`，缺年份/缺编号的退化形态
 *   ② 分类节点身份键 = **字典项 Code(GUID)**，⛔ 不是 `__cat_*` 合成码
 *   ③ 分类节点 `NodeType='virtual'`（内核 `isVirtualNode` 据此把关联值归零）
 *   ④ 分类节点 `Extra.Category` = `DicValue`（与 `ISOStandard.Category` 同口径）
 *   ⑤ 标准真名落档到 `Extra.StandardName`（否则编辑会把标签存回库）
 *   ⑥ 字典为空 → 降级返回原扁平结构
 *   ⑦ 字典外类别 → 有才出现的「未分类」兜底
 *   ⑧ 排序：分类按字典顺序，标准按 StandardCode 升序
 */

import { describe, it, expect } from 'vitest'
import type { TreeNode } from '@yzh-core/types/tree'
import {
  buildCategoryTree,
  formatStdLabel,
  UNCATEGORIZED_CODE,
  type CategoryItem,
} from './group'

// ========================================================
// 夹具
// ========================================================

const CATS: CategoryItem[] = [
  { Value: 'quality', Label: '质量管理', Code: '27560df1ae6c11f1953796fd503fd974' },
  { Value: 'environment', Label: '环境管理', Code: '27560e1dae6c11f1953796fd503fd974' },
  { Value: 'safety', Label: '职业健康安全', Code: '27560e48ae6c11f1953796fd503fd974' },
]

function std(code: string, name: string, extra: Record<string, any>): TreeNode {
  return {
    Code: code,
    Name: name,
    ParentCode: null,
    IsLeaf: true,
    Extra: { level: 0, ...extra },
    Children: [],
  }
}

const STD_9001 = std('846dec4b-c534-4983-94e6-8cf04982b7d9', '9001标准', {
  StandardCode: 'iso9001',
  VersionYear: 2015,
  Category: 'quality',
})
const STD_4001 = std('475da4fe-8f50-4bf7-bf2b-b39869d5ddf7', '食品标准', {
  StandardCode: 'iso4001',
  VersionYear: 2016,
  Category: 'quality',
})

// ========================================================
// ① 标签
// ========================================================

describe('formatStdLabel — 标签 = 编号:年份 名称', () => {
  it('有编号有年份 → `iso9001:2015 9001标准`', () => {
    expect(formatStdLabel({ StandardCode: 'iso9001', VersionYear: 2015 }, '9001标准'))
      .toBe('iso9001:2015 9001标准')
  })

  it('缺年份 → `iso9001 9001标准`', () => {
    expect(formatStdLabel({ StandardCode: 'iso9001' }, '9001标准')).toBe('iso9001 9001标准')
  })

  it('年份为 0 → 不拼年份', () => {
    expect(formatStdLabel({ StandardCode: 'iso9001', VersionYear: 0 }, 'X')).toBe('iso9001 X')
  })

  it('缺编号 → 退化为纯名称', () => {
    expect(formatStdLabel({ VersionYear: 2015 }, '9001标准')).toBe('9001标准')
  })

  it('extra 为空/undefined 不抛错', () => {
    expect(formatStdLabel(undefined, '9001标准')).toBe('9001标准')
    expect(formatStdLabel(null, '9001标准')).toBe('9001标准')
    expect(formatStdLabel({}, '9001标准')).toBe('9001标准')
  })
})

// ========================================================
// ② ③ ④ ⑤ 分组
// ========================================================

describe('buildCategoryTree — 类别 → 标准', () => {
  it('分类节点 Code = 字典项 Code（GUID），不是 __cat_* 合成码', () => {
    const tree = buildCategoryTree([STD_9001], CATS)
    expect(tree[0].Code).toBe('27560df1ae6c11f1953796fd503fd974')
    expect(tree[0].Code.startsWith('__')).toBe(false)
  })

  it('分类节点 NodeType=virtual（内核 isVirtualNode 据此把关联值归零）', () => {
    const tree = buildCategoryTree([STD_9001], CATS)
    expect(tree[0].NodeType).toBe('virtual')
  })

  it('分类节点 Extra.Category = DicValue（与 ISOStandard.Category 同口径）', () => {
    const tree = buildCategoryTree([STD_9001], CATS)
    expect(tree[0].Extra?.Category).toBe('quality')
    expect(tree[0].Extra?.level).toBe(0)
  })

  it('标准被挂到其 Category 对应分类下，且 ParentCode = 分类 Code(GUID)', () => {
    const tree = buildCategoryTree([STD_9001, STD_4001], CATS)
    const quality = tree.find((n) => n.Code === '27560df1ae6c11f1953796fd503fd974')!
    expect(quality.Children).toHaveLength(2)
    expect(quality.Children!.every((c) => c.ParentCode === quality.Code)).toBe(true)
  })

  it('标准 Name 是标签，真名落档到 Extra.StandardName', () => {
    const tree = buildCategoryTree([STD_9001], CATS)
    const child = tree[0].Children![0]
    expect(child.Name).toBe('iso9001:2015 9001标准')
    expect(child.Extra?.StandardName).toBe('9001标准')
  })

  it('标准节点不是虚拟节点（否则会被当成分类、右表永远为空）', () => {
    const tree = buildCategoryTree([STD_9001], CATS)
    expect(tree[0].Children![0].NodeType).toBeUndefined()
  })

  it('字典顺序 = 展示顺序，且空分类也保留', () => {
    const tree = buildCategoryTree([STD_9001], CATS)
    expect(tree.map((n) => n.Name)).toEqual(['质量管理', '环境管理', '职业健康安全'])
    expect(tree[1].Children).toHaveLength(0)
    expect(tree[1].IsLeaf).toBe(true)
  })

  it('同分类内标准按 StandardCode 升序（数字感知：9001 < 13485 < 14001）', () => {
    const a = std('c2', 'B', { StandardCode: 'iso14001', Category: 'quality' })
    const b = std('c1', 'A', { StandardCode: 'iso9001', Category: 'quality' })
    const c = std('c3', 'C', { StandardCode: 'iso13485', Category: 'quality' })
    const tree = buildCategoryTree([a, b, c], CATS)
    expect(tree[0].Children!.map((n) => n.Extra?.StandardCode))
      .toEqual(['iso9001', 'iso13485', 'iso14001'])
  })

  it('标准 level 置 1（分类 0）', () => {
    const tree = buildCategoryTree([STD_9001], CATS)
    expect(tree[0].Children![0].Extra?.level).toBe(1)
  })
})

// ========================================================
// ⑥ 降级 / ⑦ 兜底
// ========================================================

describe('buildCategoryTree — 降级与兜底', () => {
  it('字典为空 → 降级返回原扁平数组（不抛错、不丢节点）', () => {
    const flat = [STD_9001, STD_4001]
    const tree = buildCategoryTree(flat, [])
    expect(tree).toBe(flat)
  })

  it('标准 Category 不在字典里 → 归入「未分类」兜底', () => {
    const orphan = std('x1', '孤儿', { StandardCode: 'iso1234', Category: 'nope' })
    const tree = buildCategoryTree([STD_9001, orphan], CATS)
    const un = tree.find((n) => n.Code === UNCATEGORIZED_CODE)
    expect(un).toBeDefined()
    expect(un!.Name).toBe('未分类')
    expect(un!.NodeType).toBe('virtual')
    expect(un!.Children).toHaveLength(1)
    expect(un!.Children![0].Name).toBe('iso1234 孤儿')
  })

  it('无孤儿时不出「未分类」节点', () => {
    const tree = buildCategoryTree([STD_9001], CATS)
    expect(tree.some((n) => n.Code === UNCATEGORIZED_CODE)).toBe(false)
  })

  it('空字典项（缺 Code 或 Value）被剔除，不产生重复节点', () => {
    const bad = [
      ...CATS,
      { Value: 'quality', Label: '重复', Code: '27560df1ae6c11f1953796fd503fd974' },
      { Value: '', Label: '无值', Code: 'zzz' },
      { Value: 'x', Label: '无码', Code: '' },
    ]
    const tree = buildCategoryTree([STD_9001], bad)
    expect(tree).toHaveLength(3)
    expect(tree.filter((n) => n.Name === '质量管理')).toHaveLength(1)
  })

  it('无 Code 的标准节点被跳过（不产生孤儿桶）', () => {
    const noCode = { Code: '', Name: 'x', ParentCode: null, Extra: {}, Children: [] } as TreeNode
    const tree = buildCategoryTree([noCode, STD_9001], CATS)
    expect(tree.some((n) => n.Code === UNCATEGORIZED_CODE)).toBe(false)
    expect(tree[0].Children).toHaveLength(1)
  })

  it('标准数组为空 → 只剩空分类', () => {
    const tree = buildCategoryTree([], CATS)
    expect(tree).toHaveLength(3)
    expect(tree.every((n) => n.Children!.length === 0)).toBe(true)
  })
})
