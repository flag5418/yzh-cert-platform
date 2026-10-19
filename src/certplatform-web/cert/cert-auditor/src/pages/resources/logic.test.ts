/**
 * resources/logic.ts — Characterization Tests（③ 拆分前的行为基线）
 *
 * 目的：`resources` 是 R24 债务里**最后两处裸 class 之一**，计划 §3.1 给它的目标形态是
 * **拆分**（`logic.ts` 生命周期壳 + `useUpload.ts` 四阶段状态机 + `useFileOps.ts`），
 * 而不是「硬套一个基类」。拆分必然要搬动 `ref` 与方法，靠肉眼看不出「搬漏了 / 搬错了」。
 *
 * ⇒ 本文件只断言**现有行为**，不评判对错；拆分后这些断言必须逐条继续通过。
 *
 * 覆盖的是「**搬动时最容易失真**」的部分：
 *   · 目录树拼装（ParentCode 嵌套 + **子树全量**计数汇总）
 *   · 目标文件夹下拉（根目录必须存在 + 层级缩进 + 排序）
 *   · 本地文件 ↔ 计划行的关联键（同名不同目录 ⛔ 不能塌缩）
 *   · 计划行的默认勾选口径（被拦 / 需确认 ⇒ 不勾）
 *   · 纯文案映射（未归属原因 / 匹配层级 / 历史操作类型）
 *
 * ⛔ 不测网络请求（那属于页面级联调，不是 characterization 的职责）。
 */
import { describe, it, expect } from 'vitest'
import type {
  DispatchRow,
  FileSlot,
  StandardDirectoryResult,
  StageStandardSummary,
} from '@share/api'
import { isSlotLive, isSlotRemoved } from '@share/api'
import { ResourcesLogic } from './logic'

// ─── fixtures ───────────────────────────────────────────────────

function makeSlot(p: Partial<FileSlot> = {}): FileSlot {
  return {
    Code: 'SLOT-1',
    FolderCode: 'F1',
    FolderName: 'F1',
    FolderPath: 'F1',
    FileName: 'a.pdf',
    IsRequired: true,
    VersionNumber: 1,
    IsValid: 1,
    StoragePath: 'ent/a.pdf',
    Status: 'ready',
    ...p,
  } as FileSlot
}

function makeFolder(p: Partial<StandardDirectoryResult['Folders'][number]> = {}) {
  return {
    Code: 'F1',
    ParentCode: null,
    FolderName: 'F1',
    Depth: 1,
    Sort: 1,
    FullPath: 'F1',
    ...p,
  } as StandardDirectoryResult['Folders'][number]
}

function makeDir(p: Partial<StandardDirectoryResult> = {}): StandardDirectoryResult {
  return {
    Configured: true,
    EnterpriseCode: 'ENT1',
    StageCode: 'ST1',
    StandardCode: 'STD1',
    StandardName: '质量管理体系',
    EnterpriseConfigCode: 'EC1',
    TemplateConfigCode: 'TC1',
    Folders: [],
    Files: [],
    Summary: { TotalRequired: 0, Live: 0, Ready: 0, Converting: 0, Missing: 0 },
    ...p,
  }
}

function makeDispatchRow(p: Partial<DispatchRow> = {}): DispatchRow {
  return {
    FileName: 'a.pdf',
    RelativePath: '',
    FileSize: 10,
    StandardCode: 'STD1',
    StandardName: '质量管理体系',
    SlotCode: 'SLOT-1',
    SlotFileName: 'a.pdf',
    FolderCode: 'F1',
    FolderPath: 'F1',
    Level: 'M1Exact',
    NeedsConfirm: false,
    BlockReason: null,
    ...p,
  }
}

/** 造一个挂上目录数据的实例（不走网络） */
function withDirectories(dirs: Record<string, StandardDirectoryResult>): ResourcesLogic {
  const logic = new ResourcesLogic()
  logic.directories.value = dirs
  return logic
}

// ════════════════════════════════════════════════════════════════

describe('panelHeight', () => {
  it('按行数线性增长', () => {
    const logic = new ResourcesLogic()
    expect(logic.panelHeight(1)).toBe(100) // 44*1 + 56
    expect(logic.panelHeight(3)).toBe(188) // 44*3 + 56
  })

  it('0 行按 1 行算（不留 0 高度）', () => {
    const logic = new ResourcesLogic()
    expect(logic.panelHeight(0)).toBe(100)
  })

  it('封顶 420', () => {
    const logic = new ResourcesLogic()
    expect(logic.panelHeight(100)).toBe(420)
  })
})

describe('directoryOf / standardOf', () => {
  it('未加载的标准返回 null', () => {
    const logic = new ResourcesLogic()
    expect(logic.directoryOf('STD-X')).toBeNull()
    expect(logic.standardOf('STD-X')).toBeNull()
  })

  it('directoryOf 按 StandardCode 取', () => {
    const logic = withDirectories({ STD1: makeDir() })
    expect(logic.directoryOf('STD1')?.StandardCode).toBe('STD1')
  })

  it('standardOf 只认 Configured 的标准（未配置的不算 Tab）', () => {
    const logic = new ResourcesLogic()
    logic.overview.value = {
      EnterpriseCode: 'ENT1',
      StageCode: 'ST1',
      Configured: true,
      Standards: [
        { StandardCode: 'STD1', StandardName: '已配置', Configured: true } as StageStandardSummary,
        { StandardCode: 'STD2', StandardName: '未配置', Configured: false } as StageStandardSummary,
      ],
    } as any
    expect(logic.standardOf('STD1')?.StandardName).toBe('已配置')
    expect(logic.standardOf('STD2')).toBeNull()
  })
})

describe('folderTreeOf — 层级与计数', () => {
  it('按 ParentCode 嵌套，根节点只含顶层', () => {
    const logic = withDirectories({
      STD1: makeDir({
        Folders: [
          makeFolder({ Code: 'A', FolderName: 'A', FullPath: 'A', Depth: 1, Sort: 1 }),
          makeFolder({ Code: 'B', ParentCode: 'A', FolderName: 'B', FullPath: 'A/B', Depth: 2, Sort: 1 }),
        ],
      }),
    })
    const roots = logic.folderTreeOf('STD1')
    expect(roots.map((r) => r.Code)).toEqual(['A'])
    expect(roots[0].Children.map((c) => c.Code)).toEqual(['B'])
  })

  it('文件挂到所属文件夹', () => {
    const logic = withDirectories({
      STD1: makeDir({
        Folders: [makeFolder({ Code: 'A', FullPath: 'A' })],
        Files: [makeSlot({ Code: 'X', FolderCode: 'A' })],
      }),
    })
    expect(logic.folderTreeOf('STD1')[0].Files.map((f) => f.Code)).toEqual(['X'])
  })

  it('★ 计数按子树全量汇总（父 = 自身 + 全部后代）', () => {
    const logic = withDirectories({
      STD1: makeDir({
        Folders: [
          makeFolder({ Code: 'A', FullPath: 'A', Depth: 1 }),
          makeFolder({ Code: 'B', ParentCode: 'A', FullPath: 'A/B', Depth: 2 }),
        ],
        Files: [
          makeSlot({ Code: 'A1', FolderCode: 'A', Status: 'ready', StoragePath: 'p' }),
          makeSlot({ Code: 'A2', FolderCode: 'A', Status: 'missing', StoragePath: null }),
          makeSlot({ Code: 'B1', FolderCode: 'B', Status: 'ready', StoragePath: 'p' }),
        ],
      }),
    })
    const [a] = logic.folderTreeOf('STD1')
    expect(a.Required).toBe(3) // 自身 2 + 子 1
    expect(a.Ready).toBe(2)
    expect(a.Missing).toBe(1)
    // 子节点只看自己
    expect(a.Children[0].Required).toBe(1)
    expect(a.Children[0].Ready).toBe(1)
  })

  it('非必填槽位不计入 Required', () => {
    const logic = withDirectories({
      STD1: makeDir({
        Folders: [makeFolder({ Code: 'A', FullPath: 'A' })],
        Files: [
          makeSlot({ Code: 'R', FolderCode: 'A', IsRequired: true }),
          makeSlot({ Code: 'O', FolderCode: 'A', IsRequired: false }),
        ],
      }),
    })
    expect(logic.folderTreeOf('STD1')[0].Required).toBe(1)
  })

  it('removed 计入 Missing（移除后仍算缺口）', () => {
    const logic = withDirectories({
      STD1: makeDir({
        Folders: [makeFolder({ Code: 'A', FullPath: 'A' })],
        Files: [makeSlot({ Code: 'X', FolderCode: 'A', Status: 'removed' })],
      }),
    })
    expect(logic.folderTreeOf('STD1')[0].Missing).toBe(1)
  })

  it('FolderCode 找不到文件夹时挂到第一个根节点（⛔ 文件不能凭空消失）', () => {
    const logic = withDirectories({
      STD1: makeDir({
        Folders: [makeFolder({ Code: 'A', FullPath: 'A' })],
        Files: [makeSlot({ Code: 'ORPHAN', FolderCode: 'NOT-EXIST' })],
      }),
    })
    const roots = logic.folderTreeOf('STD1')
    expect(roots).toHaveLength(1)
    expect(roots[0].Files.map((f) => f.Code)).toEqual(['ORPHAN'])
  })

  it('没有目录数据返回空数组', () => {
    expect(new ResourcesLogic().folderTreeOf('STD-X')).toEqual([])
  })
})

describe('folderOptionsOf — 目标文件夹下拉', () => {
  it('首项固定「根目录」且 FolderCode 为空串（根目录槽位必须能选中）', () => {
    const logic = withDirectories({
      STD1: makeDir({ Folders: [makeFolder({ Code: 'A', FullPath: 'A' })] }),
    })
    const opts = logic.folderOptionsOf('STD1')
    expect(opts[0]).toEqual({ FolderCode: '', Label: '根目录', FullPath: '' })
  })

  it('按 Depth 再 Sort 排序', () => {
    const logic = withDirectories({
      STD1: makeDir({
        Folders: [
          makeFolder({ Code: 'A2', FolderName: 'A2', FullPath: 'A2', Depth: 1, Sort: 2 }),
          makeFolder({ Code: 'B', ParentCode: 'A1', FolderName: 'B', FullPath: 'A1/B', Depth: 2, Sort: 1 }),
          makeFolder({ Code: 'A1', FolderName: 'A1', FullPath: 'A1', Depth: 1, Sort: 1 }),
        ],
      }),
    })
    expect(logic.folderOptionsOf('STD1').map((o) => o.FolderCode)).toEqual(['', 'A1', 'A2', 'B'])
  })

  it('层级用全角空格缩进（Depth-1 个）', () => {
    const logic = withDirectories({
      STD1: makeDir({
        Folders: [makeFolder({ Code: 'A', FolderName: 'A', FullPath: 'A', Depth: 3 })],
      }),
    })
    const a = logic.folderOptionsOf('STD1').find((o) => o.FolderCode === 'A')!
    expect(a.Label).toBe('　　A')
  })

  it('没有目录数据返回空数组', () => {
    expect(new ResourcesLogic().folderOptionsOf('STD-X')).toEqual([])
  })
})

describe('folderFullPathOf', () => {
  it('命中返回 FullPath', () => {
    const logic = withDirectories({
      STD1: makeDir({ Folders: [makeFolder({ Code: 'A', FullPath: 'A/B/C' })] }),
    })
    expect(logic.folderFullPathOf('STD1', 'A')).toBe('A/B/C')
  })

  it('未命中 / 空 code 返回空串', () => {
    const logic = withDirectories({ STD1: makeDir({ Folders: [makeFolder({ Code: 'A' })] }) })
    expect(logic.folderFullPathOf('STD1', 'NOPE')).toBe('')
    expect(logic.folderFullPathOf('STD1', '')).toBe('')
    expect(new ResourcesLogic().folderFullPathOf('STD-X', 'A')).toBe('')
  })
})

describe('本地文件关联键（★ 同名不同目录不能塌缩）', () => {
  const anyLogic = () => new ResourcesLogic() as any

  it('优先用 webkitRelativePath', () => {
    const f = Object.assign(new File(['x'], 'a.pdf'), { webkitRelativePath: '4记录文件/a.pdf' })
    expect(anyLogic().fileKey(f)).toBe('4记录文件/a.pdf')
  })

  it('无相对路径时退回 name', () => {
    const f = new File(['x'], 'a.pdf')
    expect(anyLogic().fileKey(f)).toBe('a.pdf')
  })

  it('给了目标文件夹时补前缀（与请求体同口径）', () => {
    const f = new File(['x'], 'a.pdf')
    expect(anyLogic().fileKey(f, 'A/B')).toBe('A/B/a.pdf')
  })

  it('★ 同名不同目录是两条记录（dedupeByKey 不合并）', () => {
    const f1 = Object.assign(new File(['1'], 'x.doc'), { webkitRelativePath: '1质量手册/x.doc' })
    const f2 = Object.assign(new File(['2'], 'x.doc'), { webkitRelativePath: '4记录文件/x.doc' })
    const out = anyLogic().dedupeByKey([f1, f2])
    expect(out).toHaveLength(2)
  })

  it('同路径真重复才去重', () => {
    const f1 = Object.assign(new File(['1'], 'x.doc'), { webkitRelativePath: 'A/x.doc' })
    const f2 = Object.assign(new File(['2'], 'x.doc'), { webkitRelativePath: 'A/x.doc' })
    expect(anyLogic().dedupeByKey([f1, f2])).toHaveLength(1)
  })

  it('appendFiles 返回新增数，重复不追加', () => {
    const target: File[] = []
    const f1 = Object.assign(new File(['1'], 'x.doc'), { webkitRelativePath: 'A/x.doc' })
    const f2 = Object.assign(new File(['2'], 'x.doc'), { webkitRelativePath: 'A/x.doc' })
    const added = anyLogic().appendFiles(target, [f1, f2])
    expect(added).toBe(1)
    expect(target).toHaveLength(1)
  })
})

describe('toPlanRow — 计划行默认勾选口径', () => {
  const anyLogic = () => new ResourcesLogic() as any

  it('正常行默认勾选', () => {
    const row = anyLogic().toPlanRow(makeDispatchRow(), new File(['x'], 'a.pdf'), 'STD1', '质量')
    expect(row.Selected).toBe(true)
  })

  it('★ 需确认（M3）默认不勾选', () => {
    const row = anyLogic().toPlanRow(
      makeDispatchRow({ NeedsConfirm: true, Level: 'M3FolderExt' }),
      new File(['x'], 'a.pdf'), 'STD1', '质量',
    )
    expect(row.Selected).toBe(false)
  })

  it('★ 被拦的行默认不勾选', () => {
    const row = anyLogic().toPlanRow(
      makeDispatchRow({ BlockReason: '槽位已就位，请走替换' }),
      new File(['x'], 'a.pdf'), 'STD1', '质量',
    )
    expect(row.Selected).toBe(false)
  })

  it('Key 取 RelativePath，空则退 FileName', () => {
    const l = anyLogic()
    expect(l.toPlanRow(makeDispatchRow({ RelativePath: 'A/a.pdf' }), new File(['x'], 'a.pdf'), 'S', 'N').Key).toBe('A/a.pdf')
    expect(l.toPlanRow(makeDispatchRow({ RelativePath: '' }), new File(['x'], 'a.pdf'), 'S', 'N').Key).toBe('a.pdf')
  })
})

describe('文案映射', () => {
  const logic = () => new ResourcesLogic()

  it('unmatchedReasonText 四个已知原因', () => {
    expect(logic().unmatchedReasonText('no_standard')).toBe('该阶段无可用标准')
    expect(logic().unmatchedReasonText('no_match')).toBe('未命中任何标准槽位')
    expect(logic().unmatchedReasonText('ambiguous')).toBe('有相近槽位但分不清')
    expect(logic().unmatchedReasonText('ext_mismatch')).toBe('扩展名与匹配槽位不兼容（格式不符）')
  })

  it('unmatchedReasonText 未知原因原样透出（⛔ 不吞掉后端新原因）', () => {
    expect(logic().unmatchedReasonText('brand_new')).toBe('brand_new')
  })

  it('levelText 四个匹配层级', () => {
    expect(logic().levelText('M0Path')).toBe('M0 路径强匹配')
    expect(logic().levelText('M1Exact')).toBe('M1 文件名精确')
    expect(logic().levelText('M2Contains')).toBe('M2 主词包含')
    expect(logic().levelText('M3FolderExt')).toBe('M3 分类+扩展名（需确认）')
  })

  it('levelText 未知层级原样透出', () => {
    expect(logic().levelText('M9')).toBe('M9')
  })

  it('opTypeText 七种历史操作', () => {
    const l = logic()
    const cases: Array<[string, string]> = [
      ['upload', '上传'], ['replace', '替换'], ['delete', '移除'], ['restore', '恢复'],
      ['extract_trigger', '触发提取'], ['extract_done', '提取完成'], ['archive', '归档'],
    ]
    for (const [op, text] of cases) {
      expect(l.opTypeText({ OpType: op } as any)).toBe(text)
    }
  })

  it('opTypeText 未知操作原样透出', () => {
    expect(logic().opTypeText({ OpType: 'whatever' } as any)).toBe('whatever')
  })
})

describe('isLive / isRemoved 委托共享判据', () => {
  it('isLive = IsValid≠0 且有 StoragePath', () => {
    const logic = new ResourcesLogic()
    const live = makeSlot({ IsValid: 1, StoragePath: 'p' })
    const noObject = makeSlot({ IsValid: 1, StoragePath: null })
    const invalid = makeSlot({ IsValid: 0, StoragePath: 'p' })
    for (const s of [live, noObject, invalid]) {
      expect(logic.isLive(s)).toBe(isSlotLive(s))
    }
    expect(logic.isLive(live)).toBe(true)
    expect(logic.isLive(noObject)).toBe(false)
    expect(logic.isLive(invalid)).toBe(false)
  })

  it('isRemoved 只认 Status=removed', () => {
    const logic = new ResourcesLogic()
    const removed = makeSlot({ Status: 'removed' })
    expect(logic.isRemoved(removed)).toBe(true)
    expect(logic.isRemoved(removed)).toBe(isSlotRemoved(removed))
    expect(logic.isRemoved(makeSlot({ Status: 'ready' }))).toBe(false)
  })
})

describe('YzhTable 加载器', () => {
  it('folderRowsLoader 原样返回该文件夹的槽位行', async () => {
    const logic = new ResourcesLogic()
    const folder = {
      Code: 'A', FolderName: 'A', FullPath: 'A', Depth: 1,
      Files: [makeSlot({ Code: 'X' })], Children: [],
      Required: 1, Live: 1, Ready: 1, Converting: 0, Missing: 0,
    }
    const res = await logic.folderRowsLoader(folder as any)({} as any)
    expect(res.rows.map((r: FileSlot) => r.Code)).toEqual(['X'])
    expect(res.total).toBe(1)
  })

  it('versionRowsLoader 返回当前版本行', async () => {
    const logic = new ResourcesLogic()
    logic.versionRows.value = [{ VersionNumber: 2 } as any]
    const res = await logic.versionRowsLoader()({} as any)
    expect(res.total).toBe(1)
  })
})
