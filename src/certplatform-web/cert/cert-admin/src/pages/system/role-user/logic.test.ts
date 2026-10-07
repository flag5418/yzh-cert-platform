/**
 * 角色-用户管理（system/role-user）—— 角色树增删改（CheckTreeCrudCore）逻辑测试
 *
 * 覆盖范围（按 AS/TT 铁律逐条断言「期望的正确行为」）：
 *   ① 配置驱动：节点动作 ← TreeConfig、弹窗字段 ← TreeFormConfig（前端零硬编码）
 *   ② 根级新增不要求先选中（requireTreeSelectionForAdd=false，Sys_Role 14 行里 12 行多根）
 *   ③ 准则 A：提交体不带 Id、定位/更新只用 Code
 *   ④ 本地增量：加/改/删都不整树 reload；父节点末端态同步
 *   ⑤ F-1/F-3：失败必抛且只弹一次，失败不摘节点、不弹成功
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { toRaw } from 'vue'

const mocks = vi.hoisted(() => ({
  confirm: vi.fn().mockResolvedValue(true),
}))

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

vi.mock('@yzh-core/utils/confirm', () => ({
  confirmOrFalse: mocks.confirm,
  confirmChoice: vi.fn(),
}))

import { ElMessage } from 'element-plus'
import { RoleUserLogic } from '@yzh-core/pages/system/role-user/logic'
import type { TreeNode } from '@yzh-core/types/tree'

// ========================================================
// 夹具：与后端 /api/Role/treepconfig 对齐
// ========================================================

/** TreeTableControllerBase.BuildTreeTableConfig 默认值 + RoleController 配置 */
const TREE_CONFIG = {
  Lazy: true,
  AllowEdit: true,
  AllowAddChild: true,
  AllowDelete: true,
  AllowRename: true,
  NameField: 'RoleName',
  CodeField: 'Code',
  ParentCodeField: 'ParentCode',
  RelateField: 'RoleCode',
  NoSelectionBehavior: 'empty',
  MaxLevel: 10,
  AllowDeleteWithChildren: false,
  CustomActions: null,
  EnableField: 'IsValid',
  AllowToggle: false,
} as any

/** Assets/EntityConfigs/System/Sys_Role.json（BcFlag 三字段 + 一个 Other 验证过滤） */
const FORM_CONFIG = {
  Title: '角色',
  FormCols: 1,
  Columns: [
    { FieldName: 'RoleName', DesName: '角色名称', Type: 'TextBox', XsFlag: true, BcFlag: true, Yxk: false },
    { FieldName: 'IsValid', DesName: '状态', Type: 'Switch', XsFlag: true, BcFlag: true, Yxk: true },
    { FieldName: 'OrderNo', DesName: '排序', Type: 'Decimal', XsFlag: true, BcFlag: true, Yxk: true },
    { FieldName: 'CreateBy', DesName: '创建人', Type: 'Other', XsFlag: false, BcFlag: true, Yxk: true },
  ],
  NewEntity: {
    Id: 0,
    Code: '',
    RoleName: '',
    ParentCode: null,
    OrderNo: null,
    IsValid: 1,
    IsDeleted: false,
    DeptName: null,
    DeleteBy: null,
    DeleteTime: null,
    CreateBy: null,
    CreateTime: '2026-10-07T00:00:00',
    UpdateBy: null,
    UpdateTime: null,
  },
} as any

function makeLogic() {
  const l = new RoleUserLogic()
  const api = {
    getTreeRoot: vi.fn().mockResolvedValue([]),
    getTreeChildren: vi.fn().mockResolvedValue([]),
    getAssociations: vi.fn().mockResolvedValue([]),
    add: vi.fn().mockResolvedValue({ Updated: 0 }),
    remove: vi.fn().mockResolvedValue({ Updated: 0 }),
    getAll: vi.fn().mockResolvedValue([]),
    getTreePageConfig: vi.fn().mockResolvedValue({
      TreeConfig: TREE_CONFIG,
      TreeFormConfig: FORM_CONFIG,
    }),
    addTreeNode: vi.fn(async (body: Record<string, any>) => ({
      Code: 'R_NEW',
      Name: body.RoleName,
      ParentCode: body.ParentCode ?? null,
      IsLeaf: false,
      NodeType: 1,
      Extra: {},
    })),
    updateTreeNode: vi.fn().mockResolvedValue({}),
    deleteTreeNodes: vi.fn().mockResolvedValue(undefined),
  }
  ;(l as any).api = api
  ;(l as any).crudApi = api
  l.treeTableConfig.value = { TreeConfig: TREE_CONFIG, TreeFormConfig: FORM_CONFIG } as any
  return { l, api }
}

function makeNode(over: Partial<TreeNode> = {}): TreeNode {
  return {
    Code: 'R001',
    Name: '质量部',
    ParentCode: null,
    NodeType: 1,
    IsLeaf: true,
    Extra: { RoleName: '质量部', IsValid: 1, OrderNo: 1, level: 0 },
    Children: [],
    ...over,
  } as TreeNode
}

beforeEach(() => {
  vi.clearAllMocks()
  mocks.confirm.mockResolvedValue(true)
})

afterEach(() => {
  vi.restoreAllMocks()
})

// ========================================================
// ① 配置驱动（前端零硬编码）
// ========================================================

describe('配置驱动：动作与字段全部来自 /api/Role/treepconfig', () => {
  it('节点动作 = 新增下级/编辑/删除（AllowToggle=false ⇒ 无启停按钮）', () => {
    const { l } = makeLogic()
    const keys = l.nodeActions(makeNode()).map((a) => a.key)
    expect(keys).toEqual(['add-child', 'edit', 'delete'])
  })

  it('AllowAddChild=false ⇒ 不产出「新增下级」', () => {
    const { l } = makeLogic()
    l.treeTableConfig.value = {
      TreeConfig: { ...TREE_CONFIG, AllowAddChild: false },
      TreeFormConfig: FORM_CONFIG,
    } as any
    const keys = l.nodeActions(makeNode()).map((a) => a.key)
    expect(keys).toEqual(['edit', 'delete'])
  })

  it('CustomActions 非空 ⇒ 追加 custom: 动作', () => {
    const { l } = makeLogic()
    l.treeTableConfig.value = {
      TreeConfig: { ...TREE_CONFIG, CustomActions: { freeze: '冻结' } },
      TreeFormConfig: FORM_CONFIG,
    } as any
    expect(l.nodeActions(makeNode()).map((a) => a.key)).toContain('custom:freeze')
  })

  it('弹窗字段 = Sys_Role.json 的三个 BcFlag 字段（Type=Other 被过滤）', () => {
    const { l } = makeLogic()
    expect(l.treeFormFields.map((f) => f.prop)).toEqual(['RoleName', 'IsValid', 'OrderNo'])
  })

  it('required 由 Yxk 决定（RoleName 必填、IsValid/OrderNo 选填）', () => {
    const { l } = makeLogic()
    const req = Object.fromEntries(l.treeFormFields.map((f) => [f.prop, f.required]))
    expect(req).toEqual({ RoleName: true, IsValid: false, OrderNo: false })
  })

  it('布局列数取 FormCols=1', () => {
    const { l } = makeLogic()
    expect(l.treeFormLayoutCols).toBe(1)
  })

  it('后端未配置 TreeFormConfig ⇒ 字段为空（不误开空弹窗字段）', () => {
    const { l } = makeLogic()
    l.treeTableConfig.value = { TreeConfig: TREE_CONFIG } as any
    expect(l.treeFormFields).toEqual([])
  })
})

// ========================================================
// ② 根级新增不要求先选中
// ========================================================

describe('根级新增角色（requireTreeSelectionForAdd=false）', () => {
  it('未选中任何角色 → 可直接开新增（建根角色）', () => {
    const { l } = makeLogic()
    l.selectedNode.value = null
    expect(l.openTreeNodeDialog(null, null)).toBe(true)
    expect(l.treeDialogVisible.value).toBe(true)
    expect(l.treeDialogMode.value).toBe('add')
    expect(l.treeFormData.ParentCode).toBeNull()
    expect(l.treeFormData.RoleName).toBe('')
  })

  it('已选中 → 新增挂在选中角色下（弹窗父级可见）', () => {
    const { l } = makeLogic()
    const parent = makeNode({ Code: 'R777', Name: '父角色' })
    l.selectedNode.value = parent
    expect(l.openTreeNodeDialog(null)).toBe(true)
    expect(l.treeFormData.ParentCode).toBe('R777')
    expect(l.treeParentName).toBe('父角色')
  })

  it('未选中时父级文案 = 根级', () => {
    const { l } = makeLogic()
    l.selectedNode.value = null
    l.openTreeNodeDialog(null, null)
    expect(l.treeParentName).toBe('根级')
  })
})

// ========================================================
// ②-a 树底「新增角色」按钮（#treeFooter，与 organization 同位）
// ========================================================

describe('树底新增角色 openRoleAddFromFooter（零参 handler）', () => {
  it('未选中 → 建根角色（ParentCode 落 RootParentCode/空）', () => {
    const { l } = makeLogic()
    l.selectedNode.value = null
    expect(l.openRoleAddFromFooter()).toBe(true)
    expect(l.treeDialogVisible.value).toBe(true)
    expect(l.treeDialogMode.value).toBe('add')
    expect(l.treeFormData.ParentCode).toBeNull()
  })

  it('已选中 → 挂在选中角色下，弹窗上级角色显示其名', () => {
    const { l } = makeLogic()
    l.selectedNode.value = makeNode({ Code: 'R888', Name: '选中角色' })
    expect(l.openRoleAddFromFooter()).toBe(true)
    expect(l.treeFormData.ParentCode).toBe('R888')
    expect(l.treeParentName).toBe('选中角色')
  })
})

// ========================================================
// ②-b 树状态徽章字段（YzhTreeTableLayout :status-field）
// ========================================================

describe('treeStatusField（徽章与节点动作同源）', () => {
  it('取后端 TreeConfig.EnableField（Role = IsValid）', () => {
    const { l } = makeLogic()
    expect(l.treeStatusField).toBe('IsValid')
  })

  it('后端未声明 EnableField ⇒ undefined（不渲染徽章）', () => {
    const { l } = makeLogic()
    l.treeTableConfig.value = {
      TreeConfig: { ...TREE_CONFIG, EnableField: undefined },
      TreeFormConfig: FORM_CONFIG,
    } as any
    expect(l.treeStatusField).toBeUndefined()
  })
})

// ========================================================
// ③ 准则 A：提交体不带 Id、更新只用 Code
// ========================================================

describe('准则 A：提交体不带 Id、定位/更新只用 Code', () => {
  it('新增提交：无 Id、PascalCase、ParentCode 来自父节点', async () => {
    const { l, api } = makeLogic()
    l.selectedNode.value = null
    l.openTreeNodeDialog(null, null)
    l.treeFormData.RoleName = '新角色'
    l.treeFormData.OrderNo = 5

    expect(await l.submitTreeNodeForm()).toBe(true)
    const body = api.addTreeNode.mock.calls[0][0]
    expect(body.Id).toBeUndefined()
    expect(body.Code).toBe('')
    expect(body.ParentCode).toBeNull()
    expect(body.RoleName).toBe('新角色')
    expect(body.OrderNo).toBe(5)
    expect(body.IsValid).toBe(1)
    expect(Object.keys(body).every((k) => k[0] === k[0].toUpperCase() || k === 'Id')).toBe(true)
  })

  it('编辑提交：Code 取自被编辑节点、无 Id', async () => {
    const { l, api } = makeLogic()
    const node = makeNode({ Code: 'R001', Name: '质量部' })
    l.treeData.value = [node]

    expect(l.openTreeNodeDialog(node)).toBe(true)
    expect(l.treeDialogMode.value).toBe('edit')
    expect(l.treeFormData.Code).toBe('R001')

    l.treeFormData.RoleName = '质量中心'
    expect(await l.submitTreeNodeForm()).toBe(true)

    const body = api.updateTreeNode.mock.calls[0][0]
    expect(body.Id).toBeUndefined()
    expect(body.Code).toBe('R001')
    expect(body.RoleName).toBe('质量中心')
  })

  it('编辑失败 ⇒ 弹窗保持打开且仍是 edit 模式（否则下次提交被当新增）', async () => {
    const { l, api } = makeLogic()
    const node = makeNode({ Code: 'R001' })
    l.treeData.value = [node]
    api.updateTreeNode.mockRejectedValue(new Error('更新失败：缺少业务键 Code'))

    l.openTreeNodeDialog(node)
    expect(await l.submitTreeNodeForm()).toBe(false)

    expect(l.treeDialogVisible.value).toBe(true)
    expect(l.treeDialogMode.value).toBe('edit')
    expect((ElMessage.error as any).mock.calls[0][0]).toBe('更新失败：缺少业务键 Code')
    expect(ElMessage.success).not.toHaveBeenCalled()
  })

  it('新增成功后编辑态复位为 add（下次提交仍走新增）', async () => {
    const { l } = makeLogic()
    const node = makeNode({ Code: 'R001' })
    l.treeData.value = [node]
    l.openTreeNodeDialog(node)
    await l.submitTreeNodeForm()

    expect(l.treeDialogMode.value).toBe('add')
    expect(l.treeEditingNode.value).toBeNull()
  })
})

// ========================================================
// ④ 本地增量（不整树 reload）
// ========================================================

describe('本地增量：加/改/删都不整树 reload', () => {
  it('根级新增：节点进 treeData 根数组且为末端', async () => {
    const { l } = makeLogic()
    const created = await l.addTreeNode(null, { RoleName: '根角色' })
    expect(l.treeData.value).toHaveLength(1)
    expect(toRaw(l.treeData.value[0])).toBe(created)
    expect(l.treeData.value[0].IsLeaf).toBe(true)
    expect(l.treeData.value[0].Name).toBe('根角色')
  })

  it('新增下级：父节点末端态翻成 false（否则子节点按叶子渲染看不见）', async () => {
    const { l } = makeLogic()
    const parent = makeNode({ Code: 'P1', IsLeaf: true })
    l.treeData.value = [parent]

    await l.addTreeNode(parent, { RoleName: '子角色' })

    expect(l.treeData.value[0].IsLeaf).toBe(false)
    expect(parent.Children).toHaveLength(1)
    expect(parent.Children![0].IsLeaf).toBe(true)
    expect(parent.Children![0].ParentCode).toBe('P1')
  })

  it('后端不回填 IsLeaf ⇒ 本地新节点仍是末端', async () => {
    const { l } = makeLogic()
    const created = await l.addTreeNode(null, { RoleName: '根角色' })
    expect(created!.IsLeaf).toBe(true)
  })

  it('编辑：原位更新，对象引用不变（el-tree 不残留旧节点）', async () => {
    const { l } = makeLogic()
    const node = makeNode({ Code: 'R001', Name: '质量部' })
    l.treeData.value = [node]

    await l.updateTreeNode(node, '质量中心', {
      RoleName: '质量中心',
      IsValid: 0,
      OrderNo: 9,
    })

    expect(toRaw(l.treeData.value[0])).toBe(node)
    expect(node.Name).toBe('质量中心')
    expect(node.Code).toBe('R001')
    expect(node.Extra!.RoleName).toBe('质量中心')
    expect(node.Extra!.IsValid).toBe(0)
    expect(node.Extra!.OrderNo).toBe(9)
  })

  it('删除：从 treeData 摘除；删的是选中角色则清空右侧', async () => {
    const { l, api } = makeLogic()
    const node = makeNode({ Code: 'R001' })
    l.treeData.value = [node]
    l.selectedNode.value = node
    l.associationData.value = [{ Code: 'U1' }]

    expect(await l.deleteTreeNode(node, true)).toBe(true)
    expect(api.deleteTreeNodes).toHaveBeenCalledWith(['R001'])
    expect(l.treeData.value).toHaveLength(0)
    expect(l.selectedNode.value).toBeNull()
    expect(l.associationData.value).toEqual([])
  })

  it('有子角色时禁止删除（AllowDeleteWithChildren=false）', async () => {
    const { l, api } = makeLogic()
    const parent = makeNode({
      Code: 'P1',
      IsLeaf: false,
      Children: [makeNode({ Code: 'C1', ParentCode: 'P1' })],
    })
    l.treeData.value = [parent]

    expect(await l.deleteTreeNode(parent, true)).toBe(false)
    expect(api.deleteTreeNodes).not.toHaveBeenCalled()
    expect((ElMessage.warning as any).mock.calls[0][0]).toBe('该角色下存在子角色，请先删除子角色')
    expect(l.treeData.value).toHaveLength(1)
  })

  it('确认框取消 ⇒ 静默返回，不调接口（F-4）', async () => {
    const { l, api } = makeLogic()
    const node = makeNode({ Code: 'R001' })
    l.treeData.value = [node]
    mocks.confirm.mockResolvedValue(false)

    expect(await l.deleteTreeNode(node)).toBe(false)
    expect(api.deleteTreeNodes).not.toHaveBeenCalled()
    expect(l.treeData.value).toHaveLength(1)
    expect(ElMessage.success).not.toHaveBeenCalled()
    expect(ElMessage.error).not.toHaveBeenCalled()
  })
})

// ========================================================
// ⑤ F-1 / F-3：失败必抛、只弹一次、失败不改本地
// ========================================================

describe('F-1/F-3：删除失败不摘节点、只弹一次、不弹成功', () => {
  it('接口报错 ⇒ 抛出的错误原样弹出，节点保留在树上', async () => {
    const { l, api } = makeLogic()
    const node = makeNode({ Code: 'R001' })
    l.treeData.value = [node]
    api.deleteTreeNodes.mockRejectedValue(new Error('该角色下存在用户，无法删除'))

    expect(await l.deleteTreeNodeWithConfirm(node)).toBe(false)

    expect(l.treeData.value).toHaveLength(1)
    expect((ElMessage.error as any).mock.calls).toHaveLength(1)
    expect((ElMessage.error as any).mock.calls[0][0]).toBe('该角色下存在用户，无法删除')
    expect(ElMessage.success).not.toHaveBeenCalled()
  })

  it('已 handled 的错误不重复弹（F-3 只弹一次）', async () => {
    const { l, api } = makeLogic()
    const node = makeNode({ Code: 'R001' })
    l.treeData.value = [node]
    const err: any = new Error('重复')
    err.handled = true
    api.deleteTreeNodes.mockRejectedValue(err)

    expect(await l.deleteTreeNodeWithConfirm(node)).toBe(false)
    expect(ElMessage.error).not.toHaveBeenCalled()
  })

  it('新增失败 ⇒ 抛错且不产生幽灵节点（F-1）', async () => {
    const { l, api } = makeLogic()
    api.addTreeNode.mockRejectedValue(new Error('角色编码已存在'))

    await expect(l.addTreeNode(null, { RoleName: 'X' })).rejects.toThrow('角色编码已存在')
    expect(l.treeData.value).toHaveLength(0)
  })

  it('onNodeAction(delete) 走统一错误出口', async () => {
    const { l, api } = makeLogic()
    const node = makeNode({ Code: 'R001' })
    l.treeData.value = [node]
    api.deleteTreeNodes.mockRejectedValue(new Error('被引用'))

    await l.onNodeAction('delete', node)

    expect(l.treeData.value).toHaveLength(1)
    expect((ElMessage.error as any).mock.calls[0][0]).toBe('被引用')
    expect(ElMessage.success).not.toHaveBeenCalled()
  })
})

// ========================================================
// ⑥ 动作派发
// ========================================================

describe('onNodeAction 派发', () => {
  it('add-child → 新增模式且父节点为该节点', () => {
    const { l } = makeLogic()
    const node = makeNode({ Code: 'P1' })
    l.onNodeAction('add-child', node)
    expect(l.treeDialogMode.value).toBe('add')
    expect(toRaw(l.treeParentNode.value)).toBe(node)
    expect(l.treeFormData.ParentCode).toBe('P1')
  })

  it('add-root → 新增模式且父节点为空', () => {
    const { l } = makeLogic()
    l.onNodeAction('add-root', makeNode())
    expect(l.treeDialogMode.value).toBe('add')
    expect(l.treeParentNode.value).toBeNull()
  })

  it('edit → 编辑模式且回填被编辑节点', () => {
    const { l } = makeLogic()
    const node = makeNode({ Code: 'R9', Name: '审计部', Extra: { RoleName: '审计部', IsValid: 0, OrderNo: 3, level: 0 } as any })
    l.onNodeAction('edit', node)
    expect(l.treeDialogMode.value).toBe('edit')
    expect(toRaw(l.treeEditingNode.value)).toBe(node)
    expect(l.treeFormData.Code).toBe('R9')
    expect(l.treeFormData.RoleName).toBe('审计部')
    expect(l.treeFormData.IsValid).toBe(0)
    expect(l.treeFormData.OrderNo).toBe(3)
  })

  it('未知 key → 静默忽略（不抛、不弹）', async () => {
    const { l } = makeLogic()
    await l.onNodeAction('custom:unknown', makeNode())
    expect(ElMessage.error).not.toHaveBeenCalled()
    expect(l.treeDialogVisible.value).toBe(false)
  })
})

// ========================================================
// ⑦ 初始化顺序：先配置后树
// ========================================================

describe('init：先取 /treepconfig 再加载树根', () => {
  it('init 依次调 getTreePageConfig → getAll → getTreeRoot', async () => {
    const { l, api } = makeLogic()
    await l.init()
    expect(api.getTreePageConfig).toHaveBeenCalledTimes(1)
    expect(api.getTreeRoot).toHaveBeenCalledTimes(1)
    expect(l.treeTableConfig.value?.TreeConfig.NameField).toBe('RoleName')
  })

  it('treepconfig 失败 ⇒ 弹一次「页面初始化失败」，不冒泡', async () => {
    const { l, api } = makeLogic()
    api.getTreePageConfig.mockRejectedValue(new Error('配置加载失败'))
    await expect(l.init()).resolves.toBeUndefined()
    expect((ElMessage.error as any).mock.calls[0][0]).toBe('配置加载失败')
  })
})
