/**
 * enterprise-dept（企业机构管理，路由 /enterprise-dept）—— 回归测试
 *
 * 覆盖 2026-10-30 用户实测报出的两个缺陷：
 *
 * D1「底部『新增部门』保存成功后，左侧树没有刷新」
 *     根因（两处，缺一都表现为"保存成功但树上没有新节点"）：
 *       ① 追加只进 el-tree store 的 childNodes，父节点未展开 ⇒ 新节点收着看不见；
 *       ② 父节点原先是叶子（`IsLeaf=true`），而 el-tree 懒加载会把 `props.isLeaf`
 *          的返回值缓存进 `isLeafByUser`，之后改 data.IsLeaf 不再生效
 *          ⇒ 连展开箭头都没有，永远看不到子节点。
 *     本文件锁死内核侧（TreeTableCore）可验证的一半：父节点数据层 IsLeaf 由 true → false，
 *     且新节点经 appendNode 挂到父节点下；组件侧（revealAppendedChild：纠正
 *     isLeafByUser + 自动展开）在 YzhTree.appendNode 处断言。
 *
 * D2「编辑弹窗里备注没有跨 2 列」
 *     根因：两份 EntityConfig 的 Remark 只写了 ColSpan 1，且树表单没透传 RowSpan。
 *     ⇒ 本文件直接读**后端真实 JSON**（不是内联桩），防配置被改回去。
 */

import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, it, expect, vi } from 'vitest'

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

import { toFormFields } from '@yzh-core'
import type { TreeNode } from '@yzh-core/types/tree'
import EnterpriseDeptLogic from './logic'

// ─── 后端真实配置（⛔ 不要内联复制，避免与 JSON 分叉） ─────────────────

const CONFIG_DIR = resolve(
  __dirname,
  '../../../../../../certplatform-api/CertPlatform.Auditor/Assets/EntityConfigs/Expert',
)

function loadConfig(name: string): any {
  return JSON.parse(readFileSync(resolve(CONFIG_DIR, `${name}.json`), 'utf8'))
}

const DEPT_CONFIG = loadConfig('CertEnterpriseDept')
const MEMBER_CONFIG = loadConfig('CertEnterpriseDeptMember')

// ─── fixtures ─────────────────────────────────────────────────────────

function makeNode(over: Partial<TreeNode> = {}): TreeNode {
  return {
    Code: 'E1',
    Name: '示例企业',
    ParentCode: null,
    NodeType: 'enterprise',
    IsLeaf: true,
    Extra: {},
    Children: [],
    ...over,
  } as TreeNode
}

function makeLogic(opts: { appendNode?: (parentCode: string | null, node: any) => void } = {}) {
  const logic = new EnterpriseDeptLogic()
  ;(logic as any).treeTableConfig.value = {
    TreeConfig: {
      Lazy: true,
      AllowEdit: true,
      AllowAddChild: true,
      AllowDelete: true,
      NameField: 'DeptName',
      CodeField: 'Code',
      ParentCodeField: 'ParentCode',
      RelateField: 'DeptCode',
      EnableField: 'IsValid',
      NoSelectionBehavior: 'empty',
      AllowToggle: false,
      AllowDeleteWithChildren: false,
      CustomActions: { disable: '禁用', enable: '启用' },
    },
    TreeFormConfig: DEPT_CONFIG,
    TableConfig: MEMBER_CONFIG,
  }
  ;(logic as any).config.value = MEMBER_CONFIG
  ;(logic as any).setTreeTableRef({ appendNode: opts.appendNode ?? vi.fn() })
  return logic
}

// ─── D1：新增部门后本地树可见 ─────────────────────────────────────────

describe('新增部门 → 左树本地增量', () => {
  it('父节点从末端变非末端（IsLeaf true → false），否则 el-tree 继续按叶子渲染', async () => {
    const logic = makeLogic()
    ;(logic as any).apiPost = vi.fn(async (path: string) => {
      if (path !== '/tree/add') throw new Error(`unexpected path: ${path}`)
      return {
        success: true,
        data: { Code: 'D1', Name: '质量部', ParentCode: null, NodeType: 'dept', IsLeaf: true },
      }
    })

    const enterprise = makeNode({ IsLeaf: true })
    const created = await logic.addTreeNode(enterprise, { DeptName: '质量部' })

    expect(created?.Code).toBe('D1')
    expect(enterprise.IsLeaf).toBe(false)
  })

  it('新节点挂到选中父节点下（不是根级），且新节点恒为末端', async () => {
    const appendNode = vi.fn()
    const logic = makeLogic({ appendNode })
    ;(logic as any).apiPost = vi.fn(async () => ({
      success: true,
      data: { Code: 'D2', Name: '安全部', ParentCode: 'E1', NodeType: 'dept', IsLeaf: false },
    }))

    const enterprise = makeNode()
    const created = await logic.addTreeNode(enterprise, { DeptName: '安全部' })

    expect(appendNode).toHaveBeenCalledTimes(1)
    const [parentCode, node] = appendNode.mock.calls[0]
    expect(parentCode).toBe('E1')
    expect(node.Code).toBe('D2')
    // 后端不回填 IsLeaf（非库列），前端必须按"新节点必无子级"落成末端
    expect(created?.IsLeaf).toBe(true)
  })

  it('接口失败不造假节点（F-1：success:false 必抛，不动本地树）', async () => {
    const appendNode = vi.fn()
    const logic = makeLogic({ appendNode })
    ;(logic as any).apiPost = vi.fn(async () => ({ success: false, err: '部门名称已存在' }))

    await expect(logic.addTreeNode(makeNode(), { DeptName: '质量部' })).rejects.toThrow(
      '部门名称已存在',
    )
    expect(appendNode).not.toHaveBeenCalled()
  })
})

// ─── D2：备注跨 2 列（读真实后端 JSON） ───────────────────────────────

describe('编辑弹窗版式：备注跨 2 列 2 行', () => {
  it('部门弹窗（树表单）备注 = 满行 24 + 跨 2 行', () => {
    const logic = makeLogic()
    const remark = logic.treeFormFields.find((f) => f.prop === 'Remark')

    expect(remark).toBeTruthy()
    expect(remark!.type).toBe('textarea')
    expect(remark!.span).toBe(24)
    expect(remark!.rowSpan).toBe(2)
  })

  it('人员弹窗（右表表单）备注 = 满行 24 + 跨 2 行', () => {
    const remark = toFormFields(MEMBER_CONFIG).find((f) => f.prop === 'Remark')

    expect(remark).toBeTruthy()
    expect(remark!.type).toBe('textarea')
    expect(remark!.span).toBe(24)
    expect(remark!.rowSpan).toBe(2)
  })
})

// ─── 附带：底部按钮守卫 ──────────────────────────────────────────────

describe('底部「新增部门」前置条件', () => {
  it('未选中节点时置灰（后端要求部门必须归属企业）', () => {
    const logic = makeLogic()
    expect(logic.treeFooterAddDisabled).toBe(true)
    ;(logic as any).selectedNode = makeNode()
    expect(logic.treeFooterAddDisabled).toBe(false)
  })
})
