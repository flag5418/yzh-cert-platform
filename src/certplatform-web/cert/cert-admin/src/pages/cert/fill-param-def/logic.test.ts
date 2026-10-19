/**
 * 企业资料参数（cert/fill-param-def）—— 归属节点守卫测试
 *
 * 覆盖 2026-10-19 用户实测缺陷：
 *   「除了通用参数，应该限制只有标准才能新增参数」
 *   根因：内核 `SingleTableCore.runAction('add')` 走的是 `openAddDialog()`，
 *        而**不是** `openRowDialog()` —— 页面只覆写 `openRowDialog` 是**死代码**，
 *        选中体系/族节点后点「新增参数」照样弹窗（参数被挂到族 GUID 上）。
 *   修法：覆写 `openAddDialog()` 转调 `openRowDialog(null)`；归属判定唯一入口
 *        `isParamOwnerNode`（通用参数 Code='' 或标准叶子）。
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
import type { TreeNode } from '@yzh-core/types/tree'
import { FillParamDefLogic } from './logic'

/** 通用参数根节点（Code=''） */
function generalNode(): TreeNode {
  return {
    Code: '',
    Name: '通用参数',
    ParentCode: null,
    NodeType: undefined,
    IsLeaf: true,
    Extra: { level: -1 },
    Children: [],
  } as unknown as TreeNode
}

/** 标准叶子节点（真实归属） */
function standardNode(): TreeNode {
  return {
    Code: 'dfca5483-f83c-4999-b773-3624de5f7cf7',
    Name: 'ISO 13485 医疗器械质量管理体系',
    ParentCode: 'ad35df13-eb00-4741-9600-413122d0be22',
    NodeType: undefined,
    IsLeaf: true,
    Extra: { FamilyCode: 'ad35df13-eb00-4741-9600-413122d0be22', level: 2 },
    Children: [],
  } as unknown as TreeNode
}

/** 族导航节点 */
function familyNode(): TreeNode {
  return {
    Code: 'ad35df13-eb00-4741-9600-413122d0be22',
    Name: 'iso13485 ISO 13485 医疗器械质量管理体系族',
    ParentCode: 'cat-medical',
    NodeType: 'virtual',
    IsLeaf: false,
    Extra: { level: 1 },
    Children: [],
  } as unknown as TreeNode
}

/** 体系导航节点 */
function categoryNode(): TreeNode {
  return {
    Code: 'cat-medical',
    Name: '医疗器械',
    ParentCode: null,
    NodeType: 'virtual',
    IsLeaf: false,
    Extra: { level: 0 },
    Children: [],
  } as unknown as TreeNode
}

function makeLogic(selected?: TreeNode | null): FillParamDefLogic {
  const l = new FillParamDefLogic()
  ;(l as any).config.value = { NewEntity: {}, Columns: [], Schema: {} }
  if (selected !== undefined) {
    ;(l as any).treeSide.selectedNode.value = selected
  }
  return l
}

function addAction(l: FillParamDefLogic): any {
  return l.toolbarActions.find((a) => a.key === 'add')
}

beforeEach(() => {
  vi.clearAllMocks()
})

afterEach(() => {
  vi.restoreAllMocks()
})

// ========================================================
// 归属判定（唯一入口）
// ========================================================

describe('isSelectableNode / 归属判定', () => {
  it('通用参数根（Code=\'\'）可点', () => {
    expect(makeLogic().isSelectableNode(generalNode())).toBe(true)
  })

  it('标准节点可点', () => {
    expect(makeLogic().isSelectableNode(standardNode())).toBe(true)
  })

  it.each([
    ['体系', categoryNode()],
    ['族', familyNode()],
  ])('%s 导航节点不可点', (_name, node) => {
    expect(makeLogic().isSelectableNode(node)).toBe(false)
  })

  it('空节点不可点', () => {
    expect(makeLogic().isSelectableNode(null)).toBe(false)
    expect(makeLogic().isSelectableNode(undefined)).toBe(false)
  })
})

// ========================================================
// M1：新增参数归属守卫（回归 —— 曾经是死代码）
// ========================================================

describe('M1 新增参数：仅通用参数 / 标准可新增', () => {
  it('选中族节点 → openAddDialog 被拦截（不再直接弹窗）', () => {
    const l = makeLogic(familyNode())
    l.openAddDialog()
    expect(ElMessage.warning).toHaveBeenCalledTimes(1)
    expect((l as any).dialogVisible.value).toBe(false)
  })

  it('选中体系节点 → openAddDialog 被拦截', () => {
    const l = makeLogic(categoryNode())
    l.openAddDialog()
    expect(ElMessage.warning).toHaveBeenCalledTimes(1)
    expect((l as any).dialogVisible.value).toBe(false)
  })

  it('选中族节点 → openRowDialog(null) 返回 false', () => {
    const l = makeLogic(familyNode())
    expect(l.openRowDialog(null)).toBe(false)
    expect((l as any).dialogVisible.value).toBe(false)
  })

  it('选中通用参数 → openRowDialog(null) 放行并注入 StandardCode=\'\'', () => {
    const l = makeLogic(generalNode())
    expect(l.openRowDialog(null)).toBe(true)
    expect((l as any).dialogVisible.value).toBe(true)
    expect((l as any).formData.StandardCode).toBe('')
  })

  it('选中标准 → openRowDialog(null) 放行并注入 StandardCode=标准 Code', () => {
    const l = makeLogic(standardNode())
    expect(l.openRowDialog(null)).toBe(true)
    expect((l as any).formData.StandardCode).toBe(standardNode().Code)
  })

  it('未选中（null）→ 拒绝', () => {
    const l = makeLogic(null)
    expect(l.openRowDialog(null)).toBe(false)
    expect((l as any).dialogVisible.value).toBe(false)
  })

  it('编辑既有行（row 非空）不受归属守卫影响', () => {
    const l = makeLogic(familyNode())
    const spy = vi.spyOn(l as any, 'openEditDialog').mockImplementation(() => {})
    expect(l.openRowDialog({ Code: 'P1', ParamName: 'x' })).toBe(true)
    expect(spy).toHaveBeenCalledTimes(1)
  })
})

// ========================================================
// M2：工具栏「新增参数」按钮禁用态
// ========================================================

describe('M2 工具栏「新增参数」按钮禁用态', () => {
  it('族 / 体系节点 → 按钮禁用', () => {
    expect(addAction(makeLogic(familyNode())).disabled).toBe(true)
    expect(addAction(makeLogic(categoryNode())).disabled).toBe(true)
  })

  it('通用参数 / 标准节点 → 按钮可用', () => {
    expect(addAction(makeLogic(generalNode())).disabled).toBe(false)
    expect(addAction(makeLogic(standardNode())).disabled).toBe(false)
  })

  it('未选中 → 按钮禁用', () => {
    expect(addAction(makeLogic(null)).disabled).toBe(true)
  })
})
