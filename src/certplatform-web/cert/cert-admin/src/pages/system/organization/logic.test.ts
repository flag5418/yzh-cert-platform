/**
 * 机构-人员管理（system/organization）—— 页面操作逻辑测试
 *
 * 覆盖 2026-10-06 用户实测报出的两个缺陷（结论以本文件为准）：
 *   D1「请选择末端机构（不含子机构的节点）」误报
 *        根因：/tree/add、/tree/update 不回填 IsLeaf（DB 无该列），
 *        dtoToNode 照抄 → 本地节点 IsLeaf=false → openRowDialog 拦截
 *   D2 非 Dept 只读规则误伤所有机构
 *        根因：logic.ts isManagedDeptNode 用 'DEPT' === 'Dept' 恒 false，
 *        导致所有节点（含 Dept）被过滤掉 编辑/删除/新增下级
 *
 * 断言一律写「期望的正确行为」：当前代码若仍带缺陷，对应用例为红（即缺陷证据）。
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

vi.mock('element-plus', async (importOriginal) => {
  const actual = (await importOriginal()) as Record<string, any>
  const msg: any = Object.assign(vi.fn(), {
    success: vi.fn(),
    warning: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
  })
  return { ...actual, ElMessage: msg }
})

import { ElMessage } from 'element-plus'
import { TreeTableCore } from '@yzh-core'
import { OrgPageLogic } from '@yzh-core/pages/system/organization/logic'
import type { TreeNode } from '@yzh-core/types/tree'

/** 与 /api/Organization/treepconfig 实测返回一致 */
const TREE_CONFIG = {
  Lazy: true,
  AllowEdit: true,
  AllowAddChild: true,
  AllowDelete: true,
  AllowRename: true,
  NameField: 'OrgName',
  CodeField: 'Code',
  ParentCodeField: 'ParentCode',
  RelateField: 'OrgCode',
  NoSelectionBehavior: 'empty',
  MaxLevel: 10,
  AllowDeleteWithChildren: false,
  CustomActions: { disable: '禁用', enable: '启用' },
  EnableField: 'IsValid',
  AllowToggle: false,
} as any

function makeLogic(): OrgPageLogic {
  const l = new OrgPageLogic()
  ;(l as any).treeTableConfig.value = { TreeConfig: TREE_CONFIG }
  ;(l as any).config.value = {
    RowButtons: {
      Edit: true,
      Delete: true,
      CustomButtons: { disable: '禁用', enable: '启用' },
    },
  }
  return l
}

function makeNode(over: Partial<TreeNode> = {}): TreeNode {
  return {
    Code: 'ORG001',
    Name: '部门A',
    ParentCode: null,
    NodeType: 1,
    IsLeaf: true,
    Extra: { OrgType: 'Dept', IsValid: 1, level: 0 },
    Children: [],
    ...over,
  } as TreeNode
}

function actionKeys(l: OrgPageLogic, node: TreeNode): string[] {
  return (l as any).nodeActions(node).map((a: any) => a.key)
}

beforeEach(() => {
  vi.clearAllMocks()
})

afterEach(() => {
  vi.restoreAllMocks()
})

// ========================================================
// D2：树节点操作按钮（只读规则）
// ========================================================

describe('D2 树节点操作按钮：仅 Dept 归管理端维护', () => {
  it('Dept 节点保留「新增下级 / 编辑 / 删除」', () => {
    const l = makeLogic()
    const keys = actionKeys(l, makeNode())
    expect(keys).toContain('add-child')
    expect(keys).toContain('edit')
    expect(keys).toContain('delete')
  })

  it.each(['Platform', 'CertBody', 'VirtualOrg', 'Enterprise'])(
    '%s 节点隐藏「新增下级 / 编辑 / 删除」，只留启停',
    (orgType) => {
      const l = makeLogic()
      const keys = actionKeys(l, makeNode({ Extra: { OrgType: orgType, IsValid: 1, level: 0 } as any }))
      expect(keys).not.toContain('add-child')
      expect(keys).not.toContain('edit')
      expect(keys).not.toContain('delete')
      expect(keys).toContain('custom:disable')
    },
  )

  it('Extra 缺省 OrgType 的节点按 Dept 对待', () => {
    const l = makeLogic()
    const keys = actionKeys(l, makeNode({ Extra: { IsValid: 1, level: 0 } as any }))
    expect(keys).toContain('edit')
    expect(keys).toContain('delete')
  })

  it('禁用节点（IsValid=0）显示「启用」而非「禁用」', () => {
    const l = makeLogic()
    const keys = actionKeys(l, makeNode({ Extra: { OrgType: 'Dept', IsValid: 0, level: 0 } as any }))
    expect(keys).toContain('custom:enable')
    expect(keys).not.toContain('custom:disable')
  })
})

// ========================================================
// D1：新增人员的「末端机构」校验
// ========================================================

describe('D1 新增人员 openRowDialog', () => {
  it('未选机构 → false + 文案「请先选择机构」', () => {
    const l = makeLogic()
    l.selectedNode = null
    expect(l.openRowDialog(null)).toBe(false)
    expect((ElMessage.warning as any).mock.calls[0][0]).toBe('请先选择机构')
  })

  it('选中非末端机构 → false + 文案「请选择末端机构（不含子机构的节点）」', () => {
    const l = makeLogic()
    l.selectedNode = makeNode({ IsLeaf: false, Children: [makeNode({ Code: 'ORG002' })] })
    expect(l.openRowDialog(null)).toBe(false)
    expect((ElMessage.warning as any).mock.calls[0][0]).toBe(
      '请选择末端机构（不含子机构的节点）',
    )
  })

  it('选中末端机构 → 放行（进入行新增弹窗）', () => {
    const l = makeLogic()
    l.selectedNode = makeNode({ IsLeaf: true })
    const spy = vi
      .spyOn(TreeTableCore.prototype as any, 'openRowDialog')
      .mockReturnValue(true)
    expect(l.openRowDialog(null)).toBe(true)
    expect(spy).toHaveBeenCalledTimes(1)
  })

  it('新增机构后，本地节点 IsLeaf 必须为 true（否则该节点无法新增人员）', async () => {
    const l = makeLogic()
    // 模拟真实后端：/tree/add 的 data 里 IsLeaf 恒为 false（DB 无该列、未回填）
    vi.spyOn(l as any, 'apiPost').mockResolvedValue({
      success: true,
      code: 200,
      data: { Code: 'NEW001', Name: '新机构', ParentCode: null, IsLeaf: false, Extra: { OrgType: 'Dept' } },
    })

    const created = await l.addTreeNode(null, { OrgName: '新机构', IsValid: 1 })
    expect(created).not.toBeNull()
    expect(created!.IsLeaf).toBe(true)
  })

  it('编辑机构后，末端状态不得被后端响应打回 false', async () => {
    const l = makeLogic()
    const target = makeNode({ Code: 'ORG001', IsLeaf: true })
    ;(l as any).treeSide.setNodes([target])

    // 模拟真实后端：/tree/update 的 data 里 IsLeaf 恒为 false
    vi.spyOn(l as any, 'apiPost').mockResolvedValue({
      success: true,
      code: 200,
      data: { Code: 'ORG001', Name: '部门A', ParentCode: null, IsLeaf: false, Extra: { OrgType: 'Dept' } },
    })

    await l.updateTreeNode(target, '部门A', { OrgName: '部门A', IsValid: 1 })
    const live = (l as any).treeSide.findNode('ORG001')
    expect(live.IsLeaf).toBe(true)
  })

  it('新增下级后，父节点被置为非末端（appendChild 语义）', async () => {
    const l = makeLogic()
    const parent = makeNode({ Code: 'P001', IsLeaf: true })
    ;(l as any).treeSide.setNodes([parent])
    vi.spyOn(l as any, 'apiPost').mockResolvedValue({
      success: true,
      code: 200,
      data: { Code: 'C001', Name: '子机构', ParentCode: 'P001', IsLeaf: false, Extra: { OrgType: 'Dept' } },
    })

    await l.addTreeNode(parent, { OrgName: '子机构', IsValid: 1 })
    const live = (l as any).treeSide.findNode('P001')
    expect(live.IsLeaf).toBe(false)
  })
})

// ========================================================
// 人员新增：OrgCode 注入（回归）
// ========================================================

describe('新增人员注入所属机构', () => {
  it('onPrepareAdd 把选中机构 Code 写入 OrgCode', () => {
    const l = makeLogic()
    l.selectedNode = makeNode({ Code: 'ORG777' })
    const entity: Record<string, any> = {}
    ;(l as any).onPrepareAdd(entity)
    expect(entity.OrgCode).toBe('ORG777')
  })
})

// ========================================================
// 非 Dept 机构（业务系统所有）→ 机构与人员只读，仅可启用/禁用
// （与后端 OrganizationController 第 11 条规则双端对应）
// ========================================================

/** 与后端 BusinessOwnedOrgMessage 逐字一致 */
const OWNED_MESSAGE =
  '该机构由业务系统自动生成，不允许在机构管理中修改或删除；请到对应业务模块操作。'

function ownedNode(orgType = 'VirtualOrg'): TreeNode {
  return makeNode({ Extra: { OrgType: orgType, IsValid: 1, level: 0 } as any })
}

function rowActionKeys(l: OrgPageLogic, row: Record<string, any> = { IsValid: 1 }): string[] {
  const actions = l.rowActions
  const list = typeof actions === 'function' ? actions(row) : actions
  return list.map((a: any) => a.key)
}

describe('非 Dept 机构：工具栏只留「刷新」', () => {
  it.each(['Platform', 'CertBody', 'VirtualOrg', 'Enterprise'])(
    '选中 %s → 无「新增人员 / 批量删除」',
    (orgType) => {
      const l = makeLogic()
      l.selectedNode = ownedNode(orgType)
      expect(l.toolbarActions.map((a: any) => a.key)).toEqual(['refresh'])
    },
  )

  it('选中 Dept → 保留「新增人员 / 批量删除 / 刷新」', () => {
    const l = makeLogic()
    l.selectedNode = makeNode()
    expect(l.toolbarActions.map((a: any) => a.key)).toEqual(['add', 'delete', 'refresh'])
  })
})

describe('非 Dept 机构：行按钮无「编辑 / 删除」，只留启停', () => {
  it('非 Dept → 启用行只显「禁用」', () => {
    const l = makeLogic()
    l.selectedNode = ownedNode('CertBody')
    expect(rowActionKeys(l, { IsValid: 1 })).toEqual(['custom:disable'])
  })

  it('非 Dept + 停用行 → 只显「启用」', () => {
    const l = makeLogic()
    l.selectedNode = ownedNode('VirtualOrg')
    expect(rowActionKeys(l, { IsValid: 0 })).toEqual(['custom:enable'])
  })

  it('Dept → 保留「编辑 / 删除」+ 启停', () => {
    const l = makeLogic()
    l.selectedNode = makeNode()
    expect(rowActionKeys(l, { IsValid: 1 })).toEqual(['edit', 'delete', 'custom:disable'])
  })

  it('未选中节点 → 不误伤（表已空，按钮按默认渲染）', () => {
    const l = makeLogic()
    l.selectedNode = null
    expect(rowActionKeys(l, { IsValid: 1 })).toContain('edit')
  })
})

describe('非 Dept 机构：新增入口全封', () => {
  it('openRowDialog → false + 业务系统文案', () => {
    const l = makeLogic()
    l.selectedNode = ownedNode('VirtualOrg')
    expect(l.openRowDialog(null)).toBe(false)
    expect((ElMessage.warning as any).mock.calls[0][0]).toBe(OWNED_MESSAGE)
  })

  it('canAddUnderNode / canAddUnderNodeMessage（树「新增下级」与树底「新增机构」共用）', () => {
    const l = makeLogic()
    const owned = ownedNode('CertBody')
    const dept = makeNode()
    expect((l as any).canAddUnderNode(owned)).toBe(false)
    expect((l as any).canAddUnderNode(dept)).toBe(true)
    expect((l as any).canAddUnderNodeMessage(owned)).toBe(OWNED_MESSAGE)
    expect((l as any).canAddUnderNodeMessage(dept)).toBe(
      '请选择末端机构（不含子机构的节点）',
    )
  })

  it('openOrgAddFromFooter：选中非 Dept → false + 文案', () => {
    const l = makeLogic()
    l.selectedNode = ownedNode('Platform')
    expect(l.openOrgAddFromFooter()).toBe(false)
    expect((ElMessage.warning as any).mock.calls[0][0]).toBe(OWNED_MESSAGE)
  })

  it('treeFooterAddDisabled：非 Dept 选中时按钮置灰，未选中/Dept 时可用', () => {
    const l = makeLogic()
    expect(l.treeFooterAddDisabled).toBe(false) // 未选中 → 仍可加根
    l.selectedNode = ownedNode('CertBody')
    expect(l.treeFooterAddDisabled).toBe(true)
    l.selectedNode = makeNode()
    expect(l.treeFooterAddDisabled).toBe(false)
  })
})

// ========================================================
// 节点状态徽章：字段来源 = 后端 TreeConfig.EnableField
// ========================================================

describe('treeStatusField（左树状态徽章字段）', () => {
  it('取后端 TreeConfig.EnableField', () => {
    const l = makeLogic()
    expect(l.treeStatusField).toBe('IsValid')
  })

  it('后端未声明 EnableField → undefined（不渲染徽章）', () => {
    const l = makeLogic()
    ;(l as any).treeTableConfig.value = { TreeConfig: { ...TREE_CONFIG, EnableField: undefined } }
    ;(l as any).config.value = { RowButtons: {} }
    expect(l.treeStatusField).toBeUndefined()
  })
})
