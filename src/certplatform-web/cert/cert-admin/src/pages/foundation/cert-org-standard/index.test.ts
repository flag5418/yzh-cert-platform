/**
 * 机构-标准关联管理 —— 前端测试
 *
 * 覆盖 2026-10-08 用户报出的缺陷：
 *   「树中有空白叶子节点（Code/Name 为空），可以被勾选，保存时报唯一约束冲突」
 *
 * 根因：buildStdTree 将无子节点的体系/族节点标记为 IsLeaf:true，
 *       el-tree 的 show-checkbox 让所有节点都有复选框，
 *       用户勾选空节点后 handleStdCheckChange 把 cat_xxx 当作 StandardCode 发给后端。
 *
 * 修复：
 *   1. handleStdCheckChange 增加守卫：仅处理有有效 standardCode 的节点
 *   2. buildStdTree 中体系节点不设 IsLeaf:true（即使无子节点也应显示为可折叠父节点）
 *
 * 6 个用例：
 *   T1 正常渲染 —— API 返回 1 条标准，树中恰好 1 个有效叶子
 *   T2 空节点不可勾选 —— 无子节点的体系节点不渲染为可勾选的叶子
 *   T3 空 Code/Name 标准不入树 —— 后端返回脏数据时前端过滤
 *   T4 勾选有效叶子 —— 正常保存流程
 *   T5 取消勾选 —— 正常取消关联流程
 *   T6 空节点勾选不触发保存 —— 核心回归：空节点勾选不发 API
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'

// ⚠️ vi.mock 会被提升到文件顶部
vi.mock('@share/api/cert/cert-org-standard', () => ({
  certOrgStandardApi: {
    treeRoot: vi.fn(),
    list: vi.fn(),
    save: vi.fn()
  }
}))

vi.mock('@share/api/cert/cert-standard-family', () => ({
  getCertStandardFamilyList: vi.fn()
}))

vi.mock('@yzh-core', async () => {
  const { resolveStatusBadge } = await import('@yzh-core/utils/status')
  return {
    YzhTree: {
      name: 'YzhTree',
      props: ['data', 'nodeKey', 'loading'],
      template:
        '<div class="yzh-tree">' +
        '<template v-for="n in (data || [])" :key="n.Code">' +
        '<span class="org-node" @click="$emit(\'node-click\', n)">{{ n.Name }}</span>' +
        '</template>' +
        '</div>',
      emits: ['node-click']
    },
    YzhStatusBadge: {
      name: 'YzhStatusBadge',
      props: ['type', 'text', 'size'],
      template: '<span class="yzh-status-badge">{{ text }}</span>'
    },
    resolveStatusBadge,
    type: vi.fn()
  }
})

vi.mock('@yzh-core/api/client', () => ({
  yzhApi: {
    get: vi.fn().mockResolvedValue({ data: [
      { Value: 'quality', Label: '质量管理体系', Code: 'dict-quality' },
      { Value: 'environment', Label: '环境管理体系', Code: 'dict-environment' },
      { Value: 'safety', Label: '职业健康安全', Code: 'dict-safety' }
    ]}),
    post: vi.fn().mockResolvedValue({ data: [] })
  }
}))

import { ElMessage } from 'element-plus'
import * as orgStandardApi from '@share/api/cert/cert-org-standard'
import * as familyApi from '@share/api/cert/cert-standard-family'
import Index from './index.vue'

const mockOrgStandardApi = orgStandardApi.certOrgStandardApi as unknown as Record<string, ReturnType<typeof vi.fn>>
const mockFamilyApi = familyApi as unknown as Record<string, ReturnType<typeof vi.fn>>

// ──── 模拟数据 ────

const ORG_CODE_1 = '1579641b3d61498bba78dcbc959bd4ba'
const ORG_CODE_2 = '906e8b2a962c4062b21144af4cc4abc0'

const FAMILY_ISO9000 = {
  Code: '64df1dbd-c2ef-11f1-be9a-3e60ac2dc9d1',
  FamilyNo: 'iso9000',
  FamilyName: 'ISO 9000 质量管理体系族',
  Category: 'quality',
  IsValid: 1
}

const STD_ISO9001 = {
  Code: '846dec4b-c534-4983-94e6-8cf04982b7d9',
  StandardCode: 'iso9001',
  StandardName: '9001标准',
  VersionYear: 2015,
  Category: 'quality',
  FamilyCode: '64df1dbd-c2ef-11f1-be9a-3e60ac2dc9d1',
  FamilyName: 'iso9000 ISO 9000 质量管理体系族',
  Linked: true
}

// ──── el-tree stub（带作用域插槽 + checkbox） ────
const stubs = {
  'el-button': {
    template: '<button :disabled="disabled" @click="$emit(\'click\')"><slot /></button>',
    props: ['disabled', 'type', 'size', 'icon', 'link', 'plain']
  },
  'el-input': {
    template: '<input class="el-input" :disabled="disabled" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
    props: ['modelValue', 'disabled', 'placeholder', 'clearable', 'style']
  },
  'el-tree': {
    name: 'ElTree',
    props: ['data', 'nodeKey', 'props', 'showCheckbox', 'checkStrictly', 'defaultExpandAll', 'expandOnClickNode', 'checkOnClickNode', 'loading'],
    // ★ 递归渲染树节点（包括所有子节点），确保叶子节点的 checkbox 能被找到
    template:
      '<div class="el-tree">' +
      '<tree-node v-for="n in (data || [])" :key="n.id" :data="n" @check-change="$emit(\'check-change\', $event.data, $event.checked)" />' +
      '</div>',
    emits: ['check-change'],
    methods: {
      setCheckedKeys() { /* stub */ },
      getCheckedKeys() { return [] },
      setChecked() { /* stub */ },
      getCurrentKey() { return null },
      setCurrentKey() { /* stub */ }
    },
    components: {
      'tree-node': {
        name: 'TreeNode',
        props: ['data'],
        template:
          '<div class="el-tree-node">' +
          '<input type="checkbox" class="el-tree-node__checkbox" :checked="data.linked" @change="$emit(\'check-change\', { data, checked: $event.target.checked })" />' +
          '<slot :data="data" />' +
          '<template v-if="data.Children">' +
          '<tree-node v-for="c in data.Children" :key="c.id" :data="c" @check-change="$emit(\'check-change\', $event.data, $event.checked)" />' +
          '</template>' +
          '</div>',
        emits: ['check-change']
      }
    }
  },
  'el-icon': { template: '<span class="el-icon"><slot /></span>' }
}

async function mountPage() {
  const wrapper = mount(Index, {
    global: { stubs },
    attachTo: document.body
  })
  await flushPromises()
  return wrapper
}

/** 模拟点击左树机构节点 */
async function clickOrgNode(wrapper: ReturnType<typeof mount>, orgCode: string) {
  const orgNode = wrapper.find(`.org-node`)
  if (orgNode.exists()) {
    await orgNode.trigger('click')
  } else {
    // 直接调用 handler
    await (wrapper.vm as any).handleOrgNodeClick({ Code: orgCode, Name: '测试机构' })
  }
  await flushPromises()
  await flushPromises()
}

beforeEach(() => {
  vi.clearAllMocks()

  // 默认 mock 返回
  mockOrgStandardApi.treeRoot.mockResolvedValue({
    data: [
      { Code: ORG_CODE_1, Name: '测试机构A' },
      { Code: ORG_CODE_2, Name: '测试机构B' }
    ]
  })
  mockOrgStandardApi.list.mockResolvedValue({ data: [STD_ISO9001] })
  mockOrgStandardApi.save.mockResolvedValue({ data: {} })
  mockFamilyApi.getCertStandardFamilyList.mockResolvedValue({
    data: { Items: [FAMILY_ISO9000] }
  })

  vi.spyOn(ElMessage, 'success').mockImplementation(() => ({}) as never)
  vi.spyOn(ElMessage, 'error').mockImplementation(() => ({}) as never)
  vi.spyOn(ElMessage, 'warning').mockImplementation(() => ({}) as never)
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('机构-标准关联管理 · 树渲染与勾选', () => {
  it('T1 正常渲染：API 返回 1 条标准，树中恰好 1 个有效叶子', async () => {
    const wrapper = await mountPage()
    await clickOrgNode(wrapper, ORG_CODE_1)

    const treeData = (wrapper.vm as any).stdTreeData as any[]
    // 应该有体系节点
    expect(treeData.length).toBeGreaterThan(0)

    // 找到所有叶子节点
    const leaves: any[] = []
    const collectLeaves = (nodes: any[]) => {
      for (const n of nodes) {
        if (n.IsLeaf) leaves.push(n)
        if (n.Children) collectLeaves(n.Children)
      }
    }
    collectLeaves(treeData)

    // 恰好 1 个有效叶子
    expect(leaves.length).toBe(1)
    expect(leaves[0].standardCode).toBe('iso9001')
    expect(leaves[0].standardName).toBe('9001标准')
    expect(leaves[0].id).toBe(STD_ISO9001.Code)

    wrapper.unmount()
  })

  it('T2 空节点不可勾选：无子节点的体系节点不渲染为可勾选的叶子', async () => {
    const wrapper = await mountPage()
    await clickOrgNode(wrapper, ORG_CODE_1)

    const treeData = (wrapper.vm as any).stdTreeData as any[]

    // 遍历所有节点，检查是否有 IsLeaf=true 但无 standardCode 的节点
    const emptyLeaves: any[] = []
    const checkNodes = (nodes: any[]) => {
      for (const n of nodes) {
        if (n.IsLeaf && !n.standardCode) {
          emptyLeaves.push(n)
        }
        if (n.Children) checkNodes(n.Children)
      }
    }
    checkNodes(treeData)

    // ★ 核心断言：不应存在无 standardCode 的空叶子节点
    expect(emptyLeaves.length).toBe(0)

    wrapper.unmount()
  })

  it('T3 空 Code/Name 标准不入树：后端返回脏数据时前端过滤', async () => {
    // 模拟后端返回含空 Code 的脏数据
    mockOrgStandardApi.list.mockResolvedValue({
      data: [
        STD_ISO9001,
        { Code: '', StandardCode: '', StandardName: '', VersionYear: 0, Category: 'quality', FamilyCode: null, FamilyName: '', Linked: false },
        { Code: '   ', StandardCode: '  ', StandardName: '  ', VersionYear: 0, Category: 'safety', FamilyCode: null, FamilyName: '', Linked: false }
      ]
    })

    const wrapper = await mountPage()
    await clickOrgNode(wrapper, ORG_CODE_1)

    const treeData = (wrapper.vm as any).stdTreeData as any[]
    const leaves: any[] = []
    const collectLeaves = (nodes: any[]) => {
      for (const n of nodes) {
        if (n.IsLeaf) leaves.push(n)
        if (n.Children) collectLeaves(n.Children)
      }
    }
    collectLeaves(treeData)

    // 只有 1 个有效叶子（空 Code/Name 的被过滤）
    expect(leaves.length).toBe(1)
    expect(leaves[0].standardCode).toBe('iso9001')

    wrapper.unmount()
  })

  it('T4 勾选有效叶子：正常保存流程', async () => {
    // 预置未关联的标准
    const uncheckedStd = { ...STD_ISO9001, Linked: false }
    mockOrgStandardApi.list.mockResolvedValue({ data: [uncheckedStd] })

    const wrapper = await mountPage()
    await clickOrgNode(wrapper, ORG_CODE_1)
    await flushPromises()
    await flushPromises()

    // 获取树中的叶子节点数据（与 checkbox 勾选触发相同的 handler）
    const vm = wrapper.vm as any
    const treeData = vm.stdTreeData
    // 找到叶子节点（category → family → leaf）
    const catNode = treeData.find((n: any) => n.id === 'cat_quality')
    const famNode = catNode?.Children?.[0]
    const leafNode = famNode?.Children?.[0]
    expect(leafNode).toBeDefined()
    expect(leafNode.standardCode).toBe('iso9001')

    // 模拟勾选该叶子节点（等价于用户勾选 checkbox）
    await vm.handleStdCheckChange(leafNode, true)
    await flushPromises()

    // ★ 验证 save 被调用，且参数正确
    expect(mockOrgStandardApi.save).toHaveBeenCalledTimes(1)
    expect(mockOrgStandardApi.save.mock.calls[0][0]).toMatchObject({
      OrgCode: ORG_CODE_1,
      StandardCode: 'iso9001',
      Linked: true
    })
    wrapper.unmount()
  })

  it('T5 取消勾选：正常取消关联流程', async () => {
    const wrapper = await mountPage()
    await clickOrgNode(wrapper, ORG_CODE_1)
    await flushPromises()
    await flushPromises()

    // 获取树中的叶子节点数据
    const vm = wrapper.vm as any
    const treeData = vm.stdTreeData
    const catNode = treeData.find((n: any) => n.id === 'cat_quality')
    const famNode = catNode?.Children?.[0]
    const leafNode = famNode?.Children?.[0]
    expect(leafNode).toBeDefined()
    expect(leafNode.standardCode).toBe('iso9001')
    // 确认初始状态是已勾选
    expect(leafNode.linked).toBe(true)

    // 模拟取消勾选该叶子节点
    await vm.handleStdCheckChange(leafNode, false)
    await flushPromises()

    // ★ 验证 save 被调用，且 Linked = false
    expect(mockOrgStandardApi.save).toHaveBeenCalledTimes(1)
    expect(mockOrgStandardApi.save.mock.calls[0][0]).toMatchObject({
      OrgCode: ORG_CODE_1,
      StandardCode: 'iso9001',
      Linked: false
    })
    wrapper.unmount()
  })

  it('T6 空节点勾选不触发保存：核心回归 —— 空节点勾选不发 API', async () => {
    const wrapper = await mountPage()
    await clickOrgNode(wrapper, ORG_CODE_1)

    // 直接调用 handleStdCheckChange 模拟空节点勾选
    const vm = wrapper.vm as any
    const emptyNode = {
      id: 'cat_safety',
      label: '职业健康安全',
      IsLeaf: true,
      standardCode: '',
      standardName: ''
    }

    // 模拟勾选空节点
    await vm.handleStdCheckChange(emptyNode, true)
    await flushPromises()

    // ★ 核心断言：空节点勾选不应触发 save API
    expect(mockOrgStandardApi.save).not.toHaveBeenCalled()

    wrapper.unmount()
  })
})
