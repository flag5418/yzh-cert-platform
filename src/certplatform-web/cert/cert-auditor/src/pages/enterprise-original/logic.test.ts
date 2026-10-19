/**
 * enterprise-original/logic.ts — Characterization Tests（④ 去单例前的行为基线）
 *
 * 目的：改造前先把「纯函数口径」钉死，否则重构只能靠肉眼看。
 *
 * ★ 2026-10-19：`logic.ts` 已由**模块级单例**（28 个顶层 `export const ref`）整类化为
 *   `class EnterpriseOriginalLogic extends SingleTableCore`。纯函数（statusOf / buildTree /
 *   folderFileCodes …）**仍留在模块层**，故本文件断言口径未变；末尾新增「内核契约」三条
 *   验收继承关系、`loadConfig` 置空、以及「实例间状态互不影响」。
 *
 * ⛔ 本文件只断言**行为**，不评判对错。
 *
 * 覆盖：状态映射（三态压一态 + 优先级）、提示文案、可预览判据、文件名、
 *       文件夹树构建（层级 + 计数汇总）、文件夹文件码递归。
 */
import { describe, it, expect } from 'vitest'
import { SingleTableCore } from '@yzh-core'
import type { OriginalFile } from '@share/api/ent/enterprise-original'
import {
  EnterpriseOriginalLogic,
  buildTree,
  canPreview,
  describeQueueType,
  displayName,
  folderFileCodes,
  isExtractionUnsupported,
  statusKeyOf,
  statusOf,
  statusTip,
  unsupportedExtractionTip,
  type OriginalFolderNode,
} from './logic'

// ─── fixtures ───────────────────────────────────────────────────

function makeFile(over: Partial<OriginalFile> = {}): OriginalFile {
  return {
    Code: 'F1',
    EnterpriseCode: 'ENT1',
    StageCode: 'ST1',
    RelFolderPath: '',
    FileName: 'a.pdf',
    FileType: 'pdf',
    FileSize: 1024,
    Sha256: 'sha',
    StoragePath: 'p/a.pdf',
    VersionNumber: 1,
    ConvertStatus: 'completed',
    MarkdownStatus: 'completed',
    AnalyzeStatus: 'analyzed',
    AnalyzePolicy: 'analyze',
    ...over,
  }
}

function makeFolder(over: Partial<OriginalFolderNode> = {}): OriginalFolderNode {
  return {
    Path: 'A',
    Name: 'A',
    Depth: 1,
    Files: [],
    Children: [],
    Total: 0,
    Done: 0,
    Failed: 0,
    ...over,
  }
}

// ─── 队列类型人话 ────────────────────────────────────────────────

describe('describeQueueType', () => {
  it('三种已知类型各自映射', () => {
    expect(describeQueueType('enterprise_original_file')).toBe('处理资料内容')
    expect(describeQueueType('enterprise_original_analyze')).toBe('识别资料内容')
    expect(describeQueueType('something_else')).toBe('读取文件内容')
  })

  it('空值走兜底（⛔ 不抛错、不显示 undefined）', () => {
    expect(describeQueueType(undefined)).toBe('读取文件内容')
    expect(describeQueueType(null)).toBe('读取文件内容')
  })
})

// ─── 状态映射 ───────────────────────────────────────────────────

describe('statusKeyOf 优先级', () => {
  it('IsUsableForFilling 最高优先 —— 即便其他状态是 failed', () => {
    expect(
      statusKeyOf(makeFile({ IsUsableForFilling: true, ConvertStatus: 'failed' })),
    ).toBe('done')
  })

  it('IsUsableForFilling 压过 IsNotSuggested（两者同真时算已提取）', () => {
    expect(
      statusKeyOf(makeFile({ IsUsableForFilling: true, IsNotSuggested: true })),
    ).toBe('done')
  })

  it('IsNotSuggested 次高 —— 压过 processing/failed', () => {
    expect(
      statusKeyOf(makeFile({ IsNotSuggested: true, ConvertStatus: 'failed' })),
    ).toBe('notSuggested')
  })

  it('任一环节 converting/analyzing ⇒ processing', () => {
    expect(statusKeyOf(makeFile({ ConvertStatus: 'converting' }))).toBe('processing')
    expect(statusKeyOf(makeFile({ MarkdownStatus: 'converting' }))).toBe('processing')
    expect(statusKeyOf(makeFile({ AnalyzeStatus: 'analyzing' }))).toBe('processing')
  })

  it('unsupported ⇒ manual（早于 failed 判定）', () => {
    expect(statusKeyOf(makeFile({ ConvertStatus: 'unsupported' }))).toBe('manual')
    expect(statusKeyOf(makeFile({ MarkdownStatus: 'unsupported' }))).toBe('manual')
  })

  it('任一环节 failed ⇒ failed', () => {
    expect(statusKeyOf(makeFile({ ConvertStatus: 'failed' }))).toBe('failed')
    expect(statusKeyOf(makeFile({ MarkdownStatus: 'failed' }))).toBe('failed')
    expect(statusKeyOf(makeFile({ AnalyzeStatus: 'failed' }))).toBe('failed')
  })

  it('skipped / ignore / skip ⇒ excluded', () => {
    expect(statusKeyOf(makeFile({ AnalyzeStatus: 'skipped' }))).toBe('excluded')
    expect(statusKeyOf(makeFile({ AnalyzePolicy: 'ignore' }))).toBe('excluded')
    expect(statusKeyOf(makeFile({ AnalyzePolicy: 'skip' }))).toBe('excluded')
  })

  it('都不命中 ⇒ pending', () => {
    expect(
      statusKeyOf(makeFile({ ConvertStatus: 'pending', MarkdownStatus: 'pending', AnalyzeStatus: 'pending' })),
    ).toBe('pending')
  })
})

describe('statusOf / STATUS_TEXT', () => {
  it('键 → 文案与颜色一一对应', () => {
    expect(statusOf(makeFile({ IsUsableForFilling: true }))).toEqual({ text: '已提取', type: 'success' })
    expect(statusOf(makeFile({ ConvertStatus: 'converting' }))).toEqual({ text: '提取中', type: 'primary' })
    expect(statusOf(makeFile({ ConvertStatus: 'failed' }))).toEqual({ text: '提取失败', type: 'danger' })
    expect(statusOf(makeFile({ MarkdownStatus: 'unsupported' }))).toEqual({ text: '无法读取', type: 'warning' })
    expect(statusOf(makeFile({ AnalyzePolicy: 'skip' }))).toEqual({ text: '不参与提取', type: 'info' })
  })
})

describe('statusTip', () => {
  it('done 无提示', () => {
    expect(statusTip(makeFile({ IsUsableForFilling: true }))).toBe('')
  })

  it('manual 给「换 PDF/Word」的行动指引', () => {
    expect(statusTip(makeFile({ MarkdownStatus: 'unsupported' }))).toContain('换 PDF 或 Word')
  })

  it('failed 优先用后端消息，无则兜底', () => {
    expect(statusTip(makeFile({ ConvertStatus: 'failed', ConvertMessage: '磁盘满了' }))).toBe('磁盘满了')
    expect(statusTip(makeFile({ ConvertStatus: 'failed' }))).toContain('重新生成')
  })

  it('notSuggested 按「格式预判 / 策略」给两种文案', () => {
    expect(
      statusTip(makeFile({ IsNotSuggested: true, NotSuggestedByFormat: true })),
    ).toContain('图片类文件')
    expect(
      statusTip(makeFile({ IsNotSuggested: true, NotSuggestedByFormat: false })),
    ).toContain('不参与提取')
  })

  it('pending 优先用 UnusableReason', () => {
    expect(
      statusTip(makeFile({ ConvertStatus: 'pending', UnusableReason: '还在排队' })),
    ).toBe('还在排队')
  })
})

// ─── 不支持提取规则 ─────────────────────────────────────────────

describe('isExtractionUnsupported', () => {
  it('只认 MarkdownStatus = unsupported（大小写不敏感）', () => {
    expect(isExtractionUnsupported(makeFile({ MarkdownStatus: 'unsupported' }))).toBe(true)
    expect(isExtractionUnsupported(makeFile({ MarkdownStatus: 'UNSUPPORTED' as any }))).toBe(true)
  })

  it('⛔ ConvertStatus = unsupported 不算 —— 预览链不支持 ≠ 提取链不支持', () => {
    expect(isExtractionUnsupported(makeFile({ ConvertStatus: 'unsupported' }))).toBe(false)
  })
})

describe('unsupportedExtractionTip', () => {
  it('MarkdownMessage 优先', () => {
    expect(
      unsupportedExtractionTip(makeFile({ MarkdownMessage: 'md 原因', ConvertMessage: 'cv 原因' })),
    ).toBe('md 原因')
  })

  it('回退 ConvertMessage', () => {
    expect(unsupportedExtractionTip(makeFile({ ConvertMessage: 'cv 原因' }))).toBe('cv 原因')
  })

  it('兜底句必须含行动指引（⛔ 不能只说「不支持」）', () => {
    expect(unsupportedExtractionTip(makeFile())).toContain('手工定义字段与表格')
  })
})

// ─── 可预览 / 文件名 ────────────────────────────────────────────

describe('canPreview', () => {
  it('转换完成即可预览', () => {
    expect(canPreview(makeFile({ ConvertStatus: 'completed' }))).toBe(true)
  })

  it('有预览 PDF 或原件路径即可预览', () => {
    expect(canPreview(makeFile({ ConvertStatus: 'failed', PreviewPdfPath: 'x.pdf' }))).toBe(true)
    expect(canPreview(makeFile({ ConvertStatus: 'failed', StoragePath: 'x.pdf' }))).toBe(true)
  })

  it('三者都没有 ⇒ 不可预览', () => {
    expect(canPreview(makeFile({ ConvertStatus: 'pending', StoragePath: '', PreviewPdfPath: null }))).toBe(false)
  })
})

describe('displayName', () => {
  it('去掉最后一个扩展名', () => {
    expect(displayName(makeFile({ FileName: '质量手册.v2.docx' }))).toBe('质量手册.v2')
  })

  it('没有扩展名时原样返回', () => {
    expect(displayName(makeFile({ FileName: 'README' }))).toBe('README')
  })

  it('点号在首位时不当作扩展名', () => {
    expect(displayName(makeFile({ FileName: '.gitignore' }))).toBe('.gitignore')
  })

  it('空文件名返回空串', () => {
    expect(displayName(makeFile({ FileName: '' }))).toBe('')
  })
})

// ─── 文件夹树 ───────────────────────────────────────────────────

describe('buildTree', () => {
  it('根目录文件不进树，单独返回', () => {
    const { root, nodes } = buildTree([
      makeFile({ Code: 'R1', RelFolderPath: '' }),
      makeFile({ Code: 'A1', RelFolderPath: 'A' }),
    ])
    expect(root.map((f) => f.Code)).toEqual(['R1'])
    expect(nodes).toHaveLength(1)
    expect(nodes[0].Path).toBe('A')
  })

  it('多级路径按父子归并，Depth 从 1 开始', () => {
    const { nodes } = buildTree([
      makeFile({ Code: 'A1', RelFolderPath: 'A' }),
      makeFile({ Code: 'B1', RelFolderPath: 'A/B' }),
      makeFile({ Code: 'C1', RelFolderPath: 'A/B/C' }),
    ])
    expect(nodes).toHaveLength(1)
    const a = nodes[0]
    expect(a.Depth).toBe(1)
    expect(a.Children).toHaveLength(1)
    expect(a.Children[0].Name).toBe('B')
    expect(a.Children[0].Depth).toBe(2)
    expect(a.Children[0].Children[0].Name).toBe('C')
    expect(a.Children[0].Children[0].Depth).toBe(3)
  })

  it('前导/尾随斜杠被归一化', () => {
    const { nodes } = buildTree([makeFile({ Code: 'A1', RelFolderPath: '/A/' })])
    expect(nodes).toHaveLength(1)
    expect(nodes[0].Path).toBe('A')
  })

  it('计数逐级向上汇总：Total / Done / Failed', () => {
    const { nodes } = buildTree([
      makeFile({ Code: 'A1', RelFolderPath: 'A', IsUsableForFilling: true }),
      makeFile({ Code: 'B1', RelFolderPath: 'A/B', IsUsableForFilling: true }),
      makeFile({ Code: 'B2', RelFolderPath: 'A/B', ConvertStatus: 'failed' }),
    ])
    const a = nodes[0]
    const b = a.Children[0]
    expect(b.Total).toBe(2)
    expect(b.Done).toBe(1)
    expect(b.Failed).toBe(1)
    // 父节点 = 自身文件 + 子节点汇总
    expect(a.Total).toBe(3)
    expect(a.Done).toBe(2)
    expect(a.Failed).toBe(1)
  })

  it('同级按名称中文排序', () => {
    const { nodes } = buildTree([
      makeFile({ Code: '1', RelFolderPath: 'B' }),
      makeFile({ Code: '2', RelFolderPath: 'A' }),
    ])
    expect(nodes.map((n) => n.Name)).toEqual(['A', 'B'])
  })
})

describe('folderFileCodes', () => {
  it('递归包含子文件夹里的文件', () => {
    const child = makeFolder({
      Path: 'A/B',
      Name: 'B',
      Depth: 2,
      Files: [makeFile({ Code: 'B1' }), makeFile({ Code: 'B2' })],
    })
    const parent = makeFolder({
      Path: 'A',
      Files: [makeFile({ Code: 'A1' })],
      Children: [child],
    })
    expect(folderFileCodes(parent)).toEqual(['A1', 'B1', 'B2'])
  })

  it('空文件夹返回空数组', () => {
    expect(folderFileCodes(makeFolder())).toEqual([])
  })
})

/* ============================================================================
 * 内核契约（R24 去单例后新增）
 *
 * ★ 这三条是**改造本身的验收条件**，不是业务行为：
 *   ① 真的继承内核基类（守卫 R24 的正向锚点）
 *   ② `loadConfig()` 被置空 —— 本页没有 EntityConfig，不置空 `init()` 会打 `/config` 404
 *   ③ **实例间状态互不影响** —— 这正是「模块级单例」的病灶：
 *      状态挂在模块作用域，离开页面再进来不重置、多标签页共用一份
 * ========================================================================== */
describe('内核契约（R24）', () => {
  it('继承 SingleTableCore', () => {
    expect(new EnterpriseOriginalLogic()).toBeInstanceOf(SingleTableCore)
  })

  it('loadConfig 置空（init 不会请求不存在的 /config）', async () => {
    const logic = new EnterpriseOriginalLogic()
    // 置空后是空实现：既不抛错，也不改 config
    await expect((logic as any).loadConfig()).resolves.toBeUndefined()
  })

  it('两个实例状态互不影响（单例病灶已消除）', () => {
    const a = new EnterpriseOriginalLogic()
    const b = new EnterpriseOriginalLogic()
    a.enterpriseCode.value = 'ENT-A'
    a.filterTags.value = ['T1']
    expect(b.enterpriseCode.value).toBe('')
    expect(b.filterTags.value).toEqual([])
    expect(a.enterpriseCode.value).toBe('ENT-A')
  })
})
