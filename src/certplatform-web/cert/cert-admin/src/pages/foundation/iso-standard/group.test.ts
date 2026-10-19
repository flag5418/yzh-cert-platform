/**
 * ISO 标准左树「体系（分类） → 族 → 标准」三层分组（foundation/iso-standard/group.ts）单测
 *
 * 覆盖：
 *   ① 标签格式：标准 `编号:年份 名称`、族 `族编号 族名`（含退化形态）
 *   ② 体系节点身份键 = 字典项 Code(GUID)，⛔ 不是 `__cat_*` 合成码
 *   ③ 体系节点 `NodeType='virtual'`、族节点 `NodeType='family'`（两者都不可选）
 *   ④ 标准节点 `NodeType` 为空（可选，唯一可维护条款的节点）
 *   ⑤ 标准真名落档到 `Extra.StandardName`（否则编辑会把标签存回库）
 *   ⑥ 字典为空 → 降级返回原扁平结构
 *   ⑦ 无族标准 → 该体系下「未归族」兜底；字典外体系/族 → 「未分类」兜底
 *   ⑧ 排序：体系按字典顺序、族按 Sort→FamilyNo、标准按 StandardCode 数字序
 *   ⑨ 层级 level：体系 0 / 族 1 / 标准 2
 */

import { describe, it, expect } from 'vitest'
import type { TreeNode } from '@yzh-core/types/tree'
import {
  buildCategoryFamilyStandardTree,
  formatStdLabel,
  formatFamilyLabel,
  UNCATEGORIZED_CODE,
  UNFAMILIED_LABEL,
  type CategoryItem,
  type FamilyItem,
} from './group'

// ========================================================
// 夹具
// ========================================================

const CATS: CategoryItem[] = [
  { Value: 'quality', Label: '质量管理', Code: 'cat-quality' },
  { Value: 'environment', Label: '环境管理', Code: 'cat-environment' },
  { Value: 'medical', Label: '医疗器械', Code: 'cat-medical' },
]

const FAM_9000: FamilyItem = {
  Code: 'fam-9000',
  FamilyNo: 'iso9000',
  FamilyName: 'ISO 9000 族',
  Category: 'quality',
  Sort: 0,
  IsValid: 1,
}
const FAM_13485: FamilyItem = {
  Code: 'fam-13485',
  FamilyNo: 'iso13485',
  FamilyName: 'ISO 13485 族',
  Category: 'medical',
  Sort: 10,
  IsValid: 1,
}


function std(code: string, name: string, extra: Record<string, any>): TreeNode {
  return {
    Code: code,
    Name: name,
    ParentCode: null,
    IsLeaf: true,
    Extra: { ...extra },
    Children: [],
  }
}

const STD_9001 = std('std-9001', '9001标准', {
  StandardCode: 'iso9001',
  VersionYear: 2015,
  Category: 'quality',
  FamilyCode: 'fam-9000',
})
const STD_13485 = std('std-13485', '医疗器械标准', {
  StandardCode: 'iso13485',
  VersionYear: 2016,
  Category: 'medical',
  FamilyCode: 'fam-13485',
})

// ========================================================
// ① 标签
// ========================================================

describe('formatStdLabel — 标签 = 编号:年份 名称', () => {
  it('有编号有年份 → `iso9001:2015 9001标准`', () => {
    expect(formatStdLabel({ StandardCode: 'iso9001', VersionYear: 2015 }, '9001标准')).toBe(
      'iso9001:2015 9001标准',
    )
  })
  it('缺年份 → `iso9001 9001标准`', () => {
    expect(formatStdLabel({ StandardCode: 'iso9001' }, '9001标准')).toBe('iso9001 9001标准')
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

describe('formatFamilyLabel — 标签 = 族编号 族名', () => {
  it('有编号有名字 → `iso9000 ISO 9000 族`', () => {
    expect(formatFamilyLabel(FAM_9000)).toBe('iso9000 ISO 9000 族')
  })
  it('缺名字 → 仅编号', () => {
    expect(formatFamilyLabel({ ...FAM_9000, FamilyName: '' })).toBe('iso9000')
  })
  it('全缺 → 退化回 Code', () => {
    expect(formatFamilyLabel({ Code: 'x', FamilyNo: '', FamilyName: '', Category: '' })).toBe('x')
  })
})

// ========================================================
// ② ③ ④ ⑤ ⑤层结构
// ========================================================

describe('buildCategoryFamilyStandardTree — 体系 → 族 → 标准', () => {
  it('体系节点 Code = 字典项 Code（GUID/业务码），不是 __cat_* 合成码', () => {
    const tree = buildCategoryFamilyStandardTree([STD_9001], CATS, [FAM_9000])
    expect(tree[0].Code).toBe('cat-quality')
    expect(tree[0].NodeType).toBe('virtual')
  })

  it('族节点 Code = 族 Code、NodeType=family、ParentCode = 体系 Code', () => {
    const tree = buildCategoryFamilyStandardTree([STD_9001], CATS, [FAM_9000])
    const quality = tree.find((n) => n.Code === 'cat-quality')!
    const fam = quality.Children![0]
    expect(fam.Code).toBe('fam-9000')
    expect(fam.NodeType).toBe('family')
    expect(fam.ParentCode).toBe('cat-quality')
    expect(fam.Name).toBe('iso9000 ISO 9000 族')
  })

  it('标准节点 NodeType 为空（可选），ParentCode = 族 Code，Name 是标签、真名落 Extra', () => {
    const tree = buildCategoryFamilyStandardTree([STD_9001], CATS, [FAM_9000])
    const std = tree[0].Children![0].Children![0]
    expect(std.Code).toBe('std-9001')
    expect(std.NodeType).toBeUndefined()
    expect(std.ParentCode).toBe('fam-9000')
    expect(std.Name).toBe('iso9001:2015 9001标准')
    expect(std.Extra?.StandardName).toBe('9001标准')
  })

  it('level：体系 0 / 族 1 / 标准 2', () => {
    const tree = buildCategoryFamilyStandardTree([STD_9001], CATS, [FAM_9000])
    expect(tree[0].Extra?.level).toBe(0)
    expect(tree[0].Children![0].Extra?.level).toBe(1)
    expect(tree[0].Children![0].Children![0].Extra?.level).toBe(2)
  })

  it('体系顺序 = 字典顺序，且空体系保留（IsLeaf=true）', () => {
    const tree = buildCategoryFamilyStandardTree([STD_9001], CATS, [FAM_9000])
    expect(tree.map((n) => n.Name)).toEqual(['质量管理', '环境管理', '医疗器械'])
    expect(tree[1].Children).toHaveLength(0)
    expect(tree[1].IsLeaf).toBe(true)
  })

  it('族按 Sort → FamilyNo 排序；标准按 StandardCode 数字序', () => {
    const famB: FamilyItem = { Code: 'fam-b', FamilyNo: 'iso14001', FamilyName: 'B', Category: 'quality', Sort: 20 }
    const famA: FamilyItem = { Code: 'fam-a', FamilyNo: 'iso9001', FamilyName: 'A', Category: 'quality', Sort: 5 }
    const tree = buildCategoryFamilyStandardTree([], CATS, [famB, famA])
    expect(tree[0].Children!.map((n) => n.Code)).toEqual(['fam-a', 'fam-b'])

    const s1 = std('c1', 'A', { StandardCode: 'iso14001', Category: 'quality', FamilyCode: 'fam-a' })
    const s2 = std('c2', 'B', { StandardCode: 'iso9001', Category: 'quality', FamilyCode: 'fam-a' })
    const t2 = buildCategoryFamilyStandardTree([s1, s2], CATS, [famA])
    expect(t2[0].Children![0].Children!.map((n) => n.Extra?.StandardCode)).toEqual([
      'iso9001',
      'iso14001',
    ])
  })

  it('族带有效标志（Extra.IsValid = 族 IsValid）', () => {
    const tree = buildCategoryFamilyStandardTree([], CATS, [{ ...FAM_9000, IsValid: 0 }])
    expect(tree[0].Children![0].IsValid).toBe(0)
    expect(tree[0].Children![0].Extra?.IsValid).toBe(0)
  })

  it('多体系：标准挂到各自体系下的族（医疗器械）', () => {
    const tree = buildCategoryFamilyStandardTree(
      [STD_9001, STD_13485],
      CATS,
      [FAM_9000, FAM_13485],
    )
    const medical = tree.find((n) => n.Code === 'cat-medical')!
    expect(medical.Children![0].Code).toBe('fam-13485')
    expect(medical.Children![0].Children![0].Code).toBe('std-13485')
  })
})

// ========================================================
// ⑥ 降级 / ⑦ 兜底
// ========================================================

describe('buildCategoryFamilyStandardTree — 降级与兜底', () => {
  it('字典为空 → 降级返回原扁平数组（不抛错、不丢节点）', () => {
    const flat = [STD_9001, STD_13485]
    const tree = buildCategoryFamilyStandardTree(flat, [], [FAM_9000])
    expect(tree).toBe(flat)
  })

  it('无族标准 → 该体系下「未归族」兜底族节点', () => {
    const orphan = std('o1', '孤儿', { StandardCode: 'iso1', Category: 'quality' })
    const tree = buildCategoryFamilyStandardTree([STD_9001, orphan], CATS, [FAM_9000])
    const quality = tree.find((n) => n.Code === 'cat-quality')!
    const unfamilied = quality.Children!.find((n) => n.Name === UNFAMILIED_LABEL)!
    expect(unfamilied).toBeDefined()
    expect(unfamilied.NodeType).toBe('family')
    expect(unfamilied.Children).toHaveLength(1)
    expect(unfamilied.Children![0].Code).toBe('o1')
  })

  it('多个体系的「未归族」Code 互不相同（避免 el-tree node-key 冲突）', () => {
    const q = std('q1', 'Q', { StandardCode: 'x', Category: 'quality' })
    const m = std('m1', 'M', { StandardCode: 'y', Category: 'medical' })
    const tree = buildCategoryFamilyStandardTree([q, m], CATS, [])
    const codes = tree
      .flatMap((c) => c.Children ?? [])
      .filter((n) => n.Name === UNFAMILIED_LABEL)
      .map((n) => n.Code)
    expect(codes).toHaveLength(2)
    expect(new Set(codes).size).toBe(2)
  })

  it('指向已删除族的标准 → 落入「未归族」', () => {
    const ghost = std('g1', 'Ghost', { StandardCode: 'iso2', Category: 'quality', FamilyCode: 'no-such' })
    const tree = buildCategoryFamilyStandardTree([ghost], CATS, [FAM_9000])
    const quality = tree.find((n) => n.Code === 'cat-quality')!
    const unfamilied = quality.Children!.find((n) => n.Name === UNFAMILIED_LABEL)!
    expect(unfamilied.Children![0].Code).toBe('g1')
  })

  it('字典外体系的族 / 无族标准 → 「未分类」兜底体系', () => {
    const famX: FamilyItem = { Code: 'fam-x', FamilyNo: 'x', FamilyName: 'X', Category: 'nope' }
    const orphan = std('o1', '孤儿', { StandardCode: 'iso1', Category: 'nope' })
    const tree = buildCategoryFamilyStandardTree([orphan], CATS, [famX])
    const un = tree.find((n) => n.Code === UNCATEGORIZED_CODE)!
    expect(un).toBeDefined()
    expect(un.Name).toBe('未分类')
    expect(un.NodeType).toBe('virtual')
    // 一个族节点 + 一个「未归族」兜底节点
    expect(un.Children!.map((n) => n.Code)).toEqual(['fam-x', `__unfamilied__${UNCATEGORIZED_CODE}`])
  })

  it('无兜底场景时不出「未分类」节点', () => {
    const tree = buildCategoryFamilyStandardTree([STD_9001], CATS, [FAM_9000])
    expect(tree.some((n) => n.Code === UNCATEGORIZED_CODE)).toBe(false)
  })

  it('重复字典项（同 Code）只出一个体系节点', () => {
    const bad = [...CATS, { Value: 'quality', Label: '重复', Code: 'cat-quality' }]
    const tree = buildCategoryFamilyStandardTree([STD_9001], bad, [FAM_9000])
    expect(tree.filter((n) => n.Code === 'cat-quality')).toHaveLength(1)
  })

  it('无 Code 的标准节点被跳过', () => {
    const noCode = { Code: '', Name: 'x', ParentCode: null, Extra: { Category: 'quality' }, Children: [] } as TreeNode
    const tree = buildCategoryFamilyStandardTree([noCode, STD_9001], CATS, [FAM_9000])
    expect(tree[0].Children![0].Children).toHaveLength(1)
  })

  it('标准 / 族数组为空 → 只剩空体系', () => {
    const tree = buildCategoryFamilyStandardTree([], CATS, [])
    expect(tree).toHaveLength(3)
    expect(tree.every((n) => n.Children!.length === 0)).toBe(true)
  })
})
