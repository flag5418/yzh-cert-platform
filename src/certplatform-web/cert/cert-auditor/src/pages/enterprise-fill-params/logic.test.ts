/**
 * EnterpriseFillParamsLogic — Characterization Tests
 *
 * 目的：在 Batch 3 重构前建立行为基线。⛔ 本文件只测「当前行为是什么」，
 * 不评判行为是否正确；后续重构必须保证这些测试继续通过。
 *
 * 覆盖范围：
 *   - 纯函数：toTreeNodes / firstStandard / parseEnumOptions
 *   - 类行为：init / loadTree / onNodeClick / loadMergeList / valueOf /
 *            onValueChange / liveCompletion / save / resetEdits
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import {
  parseEnumOptions,
  EnterpriseFillParamsLogic,
  VALUE_SOURCE_LABEL,
  VALUE_SOURCE_TAG,
} from './logic'

// ─── mocks ──────────────────────────────────────────────────────

const mockPush = vi.fn()
vi.mock('@/router', () => ({ default: { push: mockPush } }))

const mockGetEnterpriseTree = vi.fn()
const mockGetMergeList = vi.fn()
const mockSaveValues = vi.fn()

vi.mock('@share/api/cert/fill-param-value', async () => {
  return {
    getEnterpriseTree: mockGetEnterpriseTree,
    getMergeList: mockGetMergeList,
    saveValues: mockSaveValues,
  }
})

const mockError = vi.fn()
const mockWarning = vi.fn()
const mockInfo = vi.fn()
const mockSuccess = vi.fn()

vi.mock('element-plus', async () => {
  return {
    ElMessage: {
      error: mockError,
      warning: mockWarning,
      info: mockInfo,
      success: mockSuccess,
    },
  }
})

// ─── fixtures ───────────────────────────────────────────────────

function makeTreeNode(overrides: Partial<any> = {}): any {
  return {
    Code: 'ent-1',
    Name: '企业A',
    NodeType: 'enterprise',
    EnterpriseCode: 'ENT001',
    StandardCode: '',
    Subtitle: 'NO.001',
    Children: [],
    ...overrides,
  }
}

function makeStandardNode(overrides: Partial<any> = {}): any {
  return makeTreeNode({
    Code: 'std-1',
    Name: 'ISO9001',
    NodeType: 'standard',
    StandardCode: 'STD001',
    Subtitle: '质量管理体系',
    ...overrides,
  })
}

function makeParamItem(overrides: Partial<any> = {}): any {
  return {
    ParamCode: 'P1',
    ParamName: '参数1',
    GroupName: 'G1',
    ValueType: 'text',
    MaintainMode: 'manual',
    SourceKind: 'manual',
    IsRequired: true,
    IsBuiltin: false,
    Sort: 1,
    ParamValue: '',
    ValueSource: 'empty',
    SourceRef: '',
    IsManualEdited: false,
    IsFilled: false,
    Editable: true,
    ...overrides,
  }
}

function makeGroup(items: any[] = []): any {
  return { GroupName: 'G1', Items: items }
}

function makeMergeResult(overrides: Partial<any> = {}): any {
  return {
    OrgCode: 'ORG001',
    WorkspaceCode: 'WS001',
    StandardCode: '',
    StageCode: '',
    Enterprise: { Name: '企业A' },
    Groups: [makeGroup([makeParamItem()])],
    Progress: { Total: 1, Filled: 0, Empty: 1, Completion: 0, Required: 1, RequiredFilled: 0 },
    ...overrides,
  }
}

// ─── 纯函数 ─────────────────────────────────────────────────────

describe('parseEnumOptions', () => {
  it('空输入返回空数组', () => {
    expect(parseEnumOptions('')).toEqual([])
    expect(parseEnumOptions(null as any)).toEqual([])
    expect(parseEnumOptions(undefined)).toEqual([])
  })

  it('合法 JSON 数组转成选项', () => {
    const json = JSON.stringify([
      { Value: 'A', Label: '甲' },
      { Value: 'B', Label: '乙' },
    ])
    expect(parseEnumOptions(json)).toEqual([
      { label: '甲', value: 'A' },
      { label: '乙', value: 'B' },
    ])
  })

  it('兼容小写 key', () => {
    const json = JSON.stringify([{ value: 'x', label: 'X' }])
    expect(parseEnumOptions(json)).toEqual([{ label: 'X', value: 'x' }])
  })

  it('非法 JSON 退化为空数组', () => {
    expect(parseEnumOptions('not-json')).toEqual([])
  })

  it('非数组 JSON 退化为空数组', () => {
    expect(parseEnumOptions('{"a":1}')).toEqual([])
  })
})

describe('VALUE_SOURCE_LABEL / VALUE_SOURCE_TAG', () => {
  it('覆盖全部已知来源', () => {
    expect(VALUE_SOURCE_LABEL.auto).toBe('自动带出')
    expect(VALUE_SOURCE_LABEL.manual).toBe('企业填写')
    expect(VALUE_SOURCE_LABEL.default).toBe('默认值')
    expect(VALUE_SOURCE_LABEL.empty).toBe('待完善')
  })
})

// ─── EnterpriseFillParamsLogic ──────────────────────────────────

describe('EnterpriseFillParamsLogic', () => {
  let logic: EnterpriseFillParamsLogic

  beforeEach(() => {
    vi.clearAllMocks()
    logic = new EnterpriseFillParamsLogic()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('init / loadTree', () => {
    it('init 会加载树并自动定位到第一个标准节点', async () => {
      const std = makeStandardNode()
      mockGetEnterpriseTree.mockResolvedValue({
        Nodes: [makeTreeNode({ Children: [std] })],
        EnterpriseCount: 1,
        LinkCount: 1,
        Hint: '',
      })
      mockGetMergeList.mockResolvedValue(makeMergeResult())

      await logic.init()

      expect(logic.treeNodes.value.length).toBeGreaterThan(0)
      expect(logic.enterpriseCode.value).toBe('ENT001')
      expect(logic.standardCode.value).toBe('STD001')
      expect(logic.scopeLevel.value).toBe('standard')
      expect(mockGetMergeList).toHaveBeenCalled()
    })

    it('若树为空则企业编码留空', async () => {
      mockGetEnterpriseTree.mockResolvedValue({
        Nodes: [],
        EnterpriseCount: 0,
        LinkCount: 0,
        Hint: '无数据',
      })

      await logic.loadTree()

      expect(logic.treeHint.value).toBe('无数据')
      expect(logic.enterpriseCode.value).toBe('')
    })

    it('异常时清空树并弹错误', async () => {
      mockGetEnterpriseTree.mockRejectedValue(new Error('网络错误'))

      await logic.loadTree()

      expect(logic.treeNodes.value).toEqual([])
      expect(mockError).toHaveBeenCalledWith('网络错误')
    })

    it('没有标准节点时退化到第一个企业节点', async () => {
      const ent = makeTreeNode()
      mockGetEnterpriseTree.mockResolvedValue({
        Nodes: [ent],
        EnterpriseCount: 1,
        LinkCount: 0,
        Hint: '',
      })
      mockGetMergeList.mockResolvedValue(makeMergeResult())

      await logic.loadTree()

      expect(logic.scopeLevel.value).toBe('enterprise')
    })
  })

  describe('onNodeClick / applyScope', () => {
    it('点击节点切换作用域并重载清单', async () => {
      mockGetMergeList.mockResolvedValue(makeMergeResult())

      const node: any = {
        Code: 'std-2',
        Name: 'ISO14001',
        NodeType: 'standard',
        Extra: { EnterpriseCode: 'ENT002', StandardCode: 'STD002' },
      }
      await logic.onNodeClick(node)

      expect(logic.enterpriseCode.value).toBe('ENT002')
      expect(logic.standardCode.value).toBe('STD002')
      expect(logic.scopeLabel.value).toBe('ISO14001')
      expect(mockGetMergeList).toHaveBeenCalledWith({
        EnterpriseCode: 'ENT002',
        StandardCode: 'STD002',
      })
    })

    it('企业编码为空时不调 merge-list', async () => {
      const node: any = {
        Code: 'ent-x',
        Name: '空节点',
        NodeType: 'enterprise',
        Extra: { EnterpriseCode: '', StandardCode: '' },
      }
      await logic.onNodeClick(node)

      expect(mockGetMergeList).not.toHaveBeenCalled()
    })
  })

  describe('loadMergeList', () => {
    it('未选企业时弹警告', async () => {
      await logic.loadMergeList()
      expect(mockWarning).toHaveBeenCalledWith('请先在左侧选择企业')
    })

    it('正常加载后更新 groups / progress / enterpriseName', async () => {
      mockGetEnterpriseTree.mockResolvedValue({
        Nodes: [makeTreeNode({ Children: [makeStandardNode()] })],
        EnterpriseCount: 1,
        LinkCount: 1,
        Hint: '',
      })
      mockGetMergeList.mockResolvedValue(makeMergeResult({
        Enterprise: { Name: '测试企业' },
        Progress: { Total: 3, Filled: 1, Empty: 2, Completion: 0.33, Required: 2, RequiredFilled: 1 },
      }))

      await logic.init()

      expect(logic.groups.value.length).toBe(1)
      expect(logic.progress.value.Total).toBe(3)
      expect(logic.enterpriseName.value).toBe('测试企业')
      expect(logic.loaded.value).toBe(true)
    })

    it('异常时弹错误', async () => {
      logic.enterpriseCode.value = 'ENT001'
      mockGetMergeList.mockRejectedValue(new Error('加载失败'))

      await logic.loadMergeList()

      expect(mockError).toHaveBeenCalledWith('加载失败')
      expect(logic.loading.value).toBe(false)
    })
  })

  describe('valueOf / onValueChange', () => {
    it('valueOf 返回本地编辑优先的值', () => {
      const item = makeParamItem({ ParamValue: '原始值' })
      expect(logic.valueOf(item)).toBe('原始值')

      logic.edited.value['P1'] = '修改值'
      expect(logic.valueOf(item)).toBe('修改值')
    })

    it('onValueChange 记录改动', () => {
      const item = makeParamItem({ ParamValue: 'old' })
      logic.onValueChange(item, 'new')
      expect(logic.edited.value['P1']).toBe('new')
      expect(logic.dirtyCount.value).toBe(1)
    })

    it('改回原始值时删除编辑记录', () => {
      const item = makeParamItem({ ParamValue: 'orig' })
      logic.onValueChange(item, 'new')
      expect(Object.keys(logic.edited.value)).toContain('P1')

      logic.onValueChange(item, 'orig')
      expect(Object.keys(logic.edited.value)).not.toContain('P1')
      expect(logic.dirtyCount.value).toBe(0)
    })

    it('只读项不记录编辑', () => {
      const item = makeParamItem({ Editable: false })
      logic.onValueChange(item, 'x')
      expect(Object.keys(logic.edited.value)).toHaveLength(0)
    })
  })

  describe('liveCompletion', () => {
    it('无数据时返回 0', () => {
      expect(logic.liveCompletion.value).toBe(0)
    })

    it('本地改动实时反映到完成度', () => {
      logic.progress.value = { Total: 2, Filled: 1, Empty: 1, Completion: 0.5, Required: 1, RequiredFilled: 0 }
      logic.groups.value = [
        makeGroup([
          makeParamItem({ ParamCode: 'P1', IsFilled: false }),
          makeParamItem({ ParamCode: 'P2', IsFilled: true }),
        ]),
      ]

      // 填了一个空的 → filled 从 1 变 2
      logic.onValueChange(makeParamItem({ ParamCode: 'P1', IsFilled: false }), 'x')
      expect(logic.liveCompletion.value).toBe(100)

      // 清空一个原本有值的 → filled 从 2 变 1
      logic.onValueChange(makeParamItem({ ParamCode: 'P2', IsFilled: true, ParamValue: 'old' }), '')
      expect(logic.liveCompletion.value).toBe(50)
    })
  })

  describe('save', () => {
    it('无改动时直接返回并提示', async () => {
      await logic.save()
      expect(mockInfo).toHaveBeenCalledWith('没有需要保存的改动')
      expect(mockSaveValues).not.toHaveBeenCalled()
    })

    it('提交编辑并成功后重载清单', async () => {
      logic.enterpriseCode.value = 'ENT001'
      logic.standardCode.value = 'STD001'
      logic.edited.value = { P1: 'v1', P2: 'v2' }
      mockSaveValues.mockResolvedValue({ savedCount: 2, ignoredAuto: [], message: '保存成功' })
      mockGetMergeList.mockResolvedValue(makeMergeResult())

      await logic.save()

      expect(mockSaveValues).toHaveBeenCalledWith({
        EnterpriseCode: 'ENT001',
        StandardCode: 'STD001',
        Items: [
          { ParamCode: 'P1', ParamValue: 'v1' },
          { ParamCode: 'P2', ParamValue: 'v2' },
        ],
      })
      expect(mockSuccess).toHaveBeenCalledWith('保存成功')
      expect(mockGetMergeList).toHaveBeenCalled() // 成功后重载
      expect(logic.saving.value).toBe(false)
    })

    it('异常时弹错误', async () => {
      logic.enterpriseCode.value = 'ENT001'
      logic.edited.value = { P1: 'v1' }
      mockSaveValues.mockRejectedValue(new Error('保存出错'))

      await logic.save()

      expect(mockError).toHaveBeenCalledWith('保存出错')
      expect(logic.saving.value).toBe(false)
    })
  })

  describe('resetEdits', () => {
    it('清空编辑并提示', () => {
      logic.edited.value = { P1: 'x' }
      logic.resetEdits()
      expect(Object.keys(logic.edited.value)).toHaveLength(0)
      expect(mockInfo).toHaveBeenCalledWith('已放弃未保存的改动')
    })
  })

  describe('goToEnterprise', () => {
    it('跳转到企业管理页', () => {
      logic.goToEnterprise()
      expect(mockPush).toHaveBeenCalledWith({ name: 'Enterprises' })
    })
  })
})
