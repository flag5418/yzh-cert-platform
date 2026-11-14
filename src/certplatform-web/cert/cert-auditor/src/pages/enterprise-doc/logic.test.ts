/**
 * `enterprise-doc/pure.ts` —— 纯函数单测
 *
 * 为什么这些函数值得单独测：它们是「状态 → 文案 / 数字 → 显示」的**唯一换算点**，
 * 页面模板只调它们、不写判定。一旦这里判错，页面上不会报错、只会**显示错的东西**
 * （老页 `enterprise-original` 就出现过「同一份文件上下两处显示不同状态」）。
 *
 * 重点覆盖两类「错了也不报错」的坑：
 *   ① `IsStale` 必须优先于 `ExtractStatus` —— 否则旧结论会冒充新结论
 *   ② 锚点定位必须**先归一标点再比** —— P2 实测 iso9001 有 1/24 因模型把 `"`
 *      输出成中文引号 `“”` 而逐字比对 miss
 */
import { describe, it, expect } from 'vitest'
import type { EntDocExtractionRow, EntDocRow } from '@share/api/ent/enterprise-doc'
import {
  buildFileTree,
  clauseSortKey,
  collectFileCodes,
  compareClauseNo,
  confidenceBadge,
  confidenceText,
  convertBadge,
  extractBadge,
  fileKindText,
  folderLabel,
  formatDuration,
  formatSize,
  isFolderNode,
  locateAnchor,
  normalizePunctuation,
  parseClauseNumbers,
  parseExtractContent,
  planActionBadge,
  planActionText,
  queueStatusBadge,
  sortClauses,
  splitByAnchor,
  standardExtractBadge,
  summarize,
  taskTypeText,
} from './pure'
import type { FolderTreeNode } from './pure'

// ─── fixtures ───────────────────────────────────────────────────

function extractionRow(over: Partial<EntDocExtractionRow> = {}): EntDocExtractionRow {
  return {
    Code: 'x',
    StandardCode: 'std',
    ClauseNumbers: '4.1',
    ClauseCount: 1,
    ExtractStatus: 'done',
    ExtractMessage: null,
    ExtractedAt: '2026-10-29 18:00:00',
    IsStale: false,
    PromptCode: 'doc_clause_extract',
    PromptVersion: 'v1',
    ...over,
  }
}

function docRow(over: Partial<EntDocRow> = {}): EntDocRow {
  return {
    Code: 'f1',
    EnterpriseCode: 'e1',
    RelFolderPath: '',
    FileName: 'a.pdf',
    FileType: '.pdf',
    FileSize: 1024,
    Sha256: '',
    StoragePath: '/x',
    VersionNumber: 1,
    ConvertStatus: 'completed',
    MarkdownStatus: 'completed',
    Extractions: [],
    MissingStandardCodes: [],
    CanExtract: true,
    CanPreview: true,
    ExtractStatus: 'none',
    ...over,
  }
}

// ─── 一、展示格式 ───────────────────────────────────────────────

describe('formatSize', () => {
  it('0 / null / 负数 一律显示 "-"（⛔ 不显示 0 B 让人以为文件是空的）', () => {
    expect(formatSize(0)).toBe('-')
    expect(formatSize(null)).toBe('-')
    expect(formatSize(undefined)).toBe('-')
    expect(formatSize(-1)).toBe('-')
  })

  it('按量级换单位', () => {
    expect(formatSize(512)).toBe('512 B')
    expect(formatSize(2048)).toBe('2.0 KB')
    expect(formatSize(3 * 1024 * 1024)).toBe('3.0 MB')
    expect(formatSize(2 * 1024 * 1024 * 1024)).toBe('2.00 GB')
  })
})

describe('folderLabel', () => {
  it('空串 / 空白 / 只有斜杠 = 根目录', () => {
    expect(folderLabel('')).toBe('根目录')
    expect(folderLabel(null)).toBe('根目录')
    expect(folderLabel('  ')).toBe('根目录')
    expect(folderLabel('/')).toBe('根目录')
  })

  it('去掉首尾斜杠，保留中间层级', () => {
    expect(folderLabel('/1 体系文件/')).toBe('1 体系文件')
    expect(folderLabel('1 体系文件/程序文件')).toBe('1 体系文件/程序文件')
  })
})

describe('fileKindText', () => {
  it('已知扩展名给中文名，未知给「文件」', () => {
    expect(fileKindText('.docx')).toBe('Word')
    expect(fileKindText('.PDF')).toBe('PDF')
    expect(fileKindText('.xlsx')).toBe('Excel')
    expect(fileKindText('.xyz')).toBe('文件')
    expect(fileKindText(null)).toBe('文件')
  })
})

// ─── 二、状态徽章 ───────────────────────────────────────────────

describe('convertBadge', () => {
  it('后端字面量是 completed，⛔ 不是 success', () => {
    expect(convertBadge('completed').type).toBe('success')
    expect(convertBadge('success').type).toBe('info') // 自造值 ⇒ 回落「未开始」，⛔ 不静默成功
    expect(convertBadge('failed').type).toBe('danger')
    expect(convertBadge('converting').type).toBe('warning')
    expect(convertBadge(null).text).toBe('未开始')
  })
})

describe('standardExtractBadge', () => {
  it('IsStale 优先于 ExtractStatus：源文件变了 ⇒ 即使 done 也显示已过期', () => {
    const b = standardExtractBadge(extractionRow({ ExtractStatus: 'done', IsStale: true }))
    expect(b.text).toBe('已过期')
    expect(b.type).toBe('warning')
    expect(b.tip).toContain('源文件已变更')
  })

  it('done 显示命中条款数', () => {
    expect(standardExtractBadge(extractionRow({ ClauseCount: 19 })).text).toBe('已提取 19 条')
  })

  it('null 行 = 还没提取', () => {
    expect(standardExtractBadge(null).text).toBe('未提取')
  })
})

describe('extractBadge（表格「提取」列的唯一口径）', () => {
  it('有失败先说失败（即使其他标准成功）', () => {
    const b = extractBadge(docRow({
      Extractions: [
        extractionRow({ StandardCode: 'a', ExtractStatus: 'done', ClauseCount: 5 }),
        extractionRow({ StandardCode: 'b', ExtractStatus: 'failed', ExtractMessage: '模型超时' }),
      ],
    }))
    expect(b.type).toBe('danger')
    expect(b.text).toBe('提取失败 1 个标准')
    expect(b.tip).toBe('模型超时')
  })

  it('有已过期 ⇒ 已过期（优先于「待提取」）', () => {
    const b = extractBadge(docRow({
      Extractions: [extractionRow({ IsStale: true })],
      MissingStandardCodes: ['another'],
    }))
    expect(b.text).toBe('已过期')
  })

  it('计划内标准还没产物 ⇒ 待提取 N 个', () => {
    const b = extractBadge(docRow({
      Extractions: [extractionRow({ StandardCode: 'a', ClauseCount: 3 })],
      MissingStandardCodes: ['b', 'c'],
    }))
    expect(b.text).toBe('待提取 2 个标准')
  })

  it('全部 done ⇒ 已提取（各标准条款数之和）', () => {
    const b = extractBadge(docRow({
      Extractions: [
        extractionRow({ StandardCode: 'a', ClauseCount: 19 }),
        extractionRow({ StandardCode: 'b', ClauseCount: 5 }),
      ],
    }))
    expect(b.text).toBe('已提取 24 条')
    expect(b.type).toBe('success')
  })

  it('skipped 是「没有可提取的内容」，⛔ 不是失败', () => {
    const b = extractBadge(docRow({
      ExtractStatus: 'skipped',
      ExtractMessage: '标准【iso9001】的条款还没配 DocCheckMode',
    }))
    expect(b.type).toBe('info')
    expect(b.text).toBe('已跳过')
    expect(b.tip).toContain('DocCheckMode')
  })

  it('null 行不抛', () => {
    expect(extractBadge(null).text).toBe('未提取')
  })
})

// ─── 三、条款号 ─────────────────────────────────────────────────

describe('parseClauseNumbers', () => {
  it('逗号分隔、去空、去重、保持原顺序', () => {
    expect(parseClauseNumbers('1, 4.1 ,4.2.4,,4.1')).toEqual(['1', '4.1', '4.2.4'])
  })

  it('空值返回空数组', () => {
    expect(parseClauseNumbers('')).toEqual([])
    expect(parseClauseNumbers(null)).toEqual([])
  })
})

describe('compareClauseNo（自然序，⛔ 不是字典序）', () => {
  it('4.2.4 排在 4.2.10 前面', () => {
    expect(compareClauseNo('4.2.4', '4.2.10')).toBeLessThan(0)
    expect(['4.2.10', '4.2.4'].sort(compareClauseNo)).toEqual(['4.2.4', '4.2.10'])
  })

  it('前缀短的排前面（1 < 1.1）', () => {
    expect(compareClauseNo('1', '1.1')).toBeLessThan(0)
  })

  it('非数字段兜底 9999 但不抛', () => {
    expect(clauseSortKey('a')).toEqual([9999])
    expect(() => compareClauseNo('附录A', '4.1')).not.toThrow()
  })
})

describe('sortClauses', () => {
  it('按条款号自然序，且不改原数组', () => {
    const input = [
      { ClauseNumber: '4.2.10' },
      { ClauseNumber: '4.2.4' },
      { ClauseNumber: '1' },
    ]
    const out = sortClauses(input)
    expect(out.map((c) => c.ClauseNumber)).toEqual(['1', '4.2.4', '4.2.10'])
    expect(input.map((c) => c.ClauseNumber)).toEqual(['4.2.10', '4.2.4', '1'])
  })
})

// ─── 四、提取 JSON ──────────────────────────────────────────────

describe('parseExtractContent', () => {
  it('PascalCase（后端实际输出）', () => {
    const r = parseExtractContent('{"DocMeta":{"FileName":"a"},"Clauses":[{"ClauseNumber":"4.1"}]}')
    expect(r?.DocMeta?.FileName).toBe('a')
    expect(r?.Clauses).toHaveLength(1)
  })

  it('camelCase 也能读（历史行兼容）', () => {
    const r = parseExtractContent('{"docMeta":{"fileName":"b"},"clauses":[{"clauseNumber":"7.5"}]}')
    expect(r?.Clauses).toHaveLength(1)
  })

  it('坏 JSON 返回 null 且不抛（一条脏数据不该打挂整页）', () => {
    expect(parseExtractContent('{not json')).toBeNull()
    expect(parseExtractContent('')).toBeNull()
    expect(parseExtractContent(null)).toBeNull()
  })

  it('Clauses 不是数组 ⇒ 空数组而不是崩', () => {
    expect(parseExtractContent('{"Clauses":"oops"}')?.Clauses).toEqual([])
  })
})

describe('confidenceBadge / confidenceText', () => {
  it('0–1 与 0–100 两种写法都吃', () => {
    expect(confidenceText(0.85)).toBe('85%')
    expect(confidenceText(85)).toBe('85%')
    expect(confidenceText(null)).toBe('-')
  })

  it('≥80 高 / ≥60 中 / 其余低', () => {
    expect(confidenceBadge(0.9).type).toBe('success')
    expect(confidenceBadge(0.7).type).toBe('warning')
    expect(confidenceBadge(0.3).type).toBe('danger')
  })
})

// ─── 五、锚点定位（P2 遗留 #1） ────────────────────────────────

describe('normalizePunctuation', () => {
  it('归一后长度不变（1:1 ⇒ 下标可与原串对齐）', () => {
    const src = '他说“合格”，然后走了。'
    expect(normalizePunctuation(src).length).toBe(src.length)
  })

  it('中文引号 / 全角标点都归一', () => {
    expect(normalizePunctuation('“a”，b。')).toBe('"a",b.')
  })
})

describe('locateAnchor', () => {
  // ★ 模拟 PDF→Markdown 的真实形态：句子中间被换行打断（模型摘录时给的是连续句子）
  const doc = '第一章 总则\n企业应当建立文件控制程序，\n确保文件"现行有效"。\n第二章 记录控制'

  it('① 逐字命中', () => {
    const hit = locateAnchor(doc, '确保文件"现行有效"')
    expect(hit?.mode).toBe('exact')
    expect(doc.slice(hit!.index, hit!.index + hit!.length)).toBe('确保文件"现行有效"')
  })

  it('② 标点归一后命中（模型把 " 写成 “ ”）', () => {
    const hit = locateAnchor(doc, '确保文件“现行有效”')
    expect(hit?.mode).toBe('punctuation')
    // ★ 返回的仍是**原串**区间（可以直接 slice 出来高亮）
    expect(doc.slice(hit!.index, hit!.index + hit!.length)).toBe('确保文件"现行有效"')
  })

  it('③ 跨行重排后命中（原文被换行打断，锚点是连续句子）', () => {
    const hit = locateAnchor(doc, '建立文件控制程序，确保文件"现行有效"')
    expect(hit?.mode).toBe('reflow')
    expect(doc.slice(hit!.index, hit!.index + hit!.length)).toContain('现行有效')
  })

  it('找不到返回 null（⛔ 不返回 -1 让调用方误判）', () => {
    expect(locateAnchor(doc, '这一段原文里根本没有')).toBeNull()
    expect(locateAnchor(doc, '')).toBeNull()
    expect(locateAnchor('', '任意')).toBeNull()
  })
})

describe('splitByAnchor', () => {
  it('切成前 / 命中 / 后三段', () => {
    const r = splitByAnchor('abcdef', 'cd')
    expect(r.before).toBe('ab')
    expect(r.hit).toBe('cd')
    expect(r.after).toBe('ef')
    expect(r.mode).toBe('exact')
  })

  it('找不到时 hit=null，全文落在 before（原文照样能看）', () => {
    const r = splitByAnchor('abcdef', 'zz')
    expect(r.hit).toBeNull()
    expect(r.before).toBe('abcdef')
    expect(r.mode).toBeNull()
  })
})

// ─── 六、上传预检 ───────────────────────────────────────────────

describe('planActionBadge / planActionText', () => {
  it('skip 是「已存在，跳过」，⛔ 不是失败', () => {
    expect(planActionBadge('skip').type).toBe('info')
    expect(planActionText('skip')).toBe('已存在，跳过')
  })

  it('replace 提示版本 +1', () => {
    expect(planActionBadge('replace').type).toBe('warning')
    expect(planActionText('replace')).toContain('版本 +1')
  })
})

// ─── 七、队列 ───────────────────────────────────────────────────

describe('queueStatusBadge / taskTypeText / formatDuration', () => {
  it('队列状态映射（running 与 processing 同档）', () => {
    expect(queueStatusBadge('completed').type).toBe('success')
    expect(queueStatusBadge('running').text).toBe('处理中')
    expect(queueStatusBadge('processing').text).toBe('处理中')
    expect(queueStatusBadge('failed').type).toBe('danger')
  })

  it('任务类型给中文，未知类型原样回显（排障时不能丢信息）', () => {
    expect(taskTypeText('enterprise_clause_extract')).toBe('按标准提取条款')
    expect(taskTypeText('unknown_type')).toBe('unknown_type')
  })

  it('耗时格式化', () => {
    expect(formatDuration(320)).toBe('320ms')
    expect(formatDuration(1200)).toBe('1.2s')
    expect(formatDuration(200_000)).toBe('3m20s')
    expect(formatDuration(null)).toBe('-')
  })
})

// ─── 八、汇总 ───────────────────────────────────────────────────

describe('summarize', () => {
  it('五个计数各按各的判据', () => {
    const s = summarize([
      docRow({ Code: '1', CanExtract: true, ExtractStatus: 'done' }),
      docRow({ Code: '2', CanExtract: false, ConvertStatus: 'converting' }),
      docRow({ Code: '3', MarkdownStatus: 'failed' }),
      docRow({ Code: '4', ExtractStatus: 'done', CanExtract: true }),
    ])
    expect(s.total).toBe(4)
    expect(s.ready).toBe(3)
    expect(s.converting).toBe(1)
    expect(s.failed).toBe(1)
    expect(s.extracted).toBe(2)
  })

  it('空数组不抛', () => {
    expect(summarize([]).total).toBe(0)
  })
})

// ─── 九、文件夹树构建 ───────────────────────────────────────────────────

/*
 * 为什么单独列一组：树表是「文件管理」页的主视图，而**树构建错了不会报错** ——
 * 只会表现为「层级不对 / 文件夹重复 / 文件数不对」，用户看到的是一个
 * 说不清哪里坏的界面。而这些函数是纯函数（输入确定 ⇒ 输出确定），
 * 必须钉死以下契约：
 *   ① 空文件夹不凭空出现（树只由「有文件的行」长出来）
 *   ② 文件夹 Code 带 `folder:` 前缀，⛔ 不与真实文件 Code（GUID）撞键
 *   ③ 子树 `FileCount` 自底向上汇总（⛔ 不是只数直接子级）
 *   ④ 「删除整个文件夹」用的是**递归**收集，漏一层就删不干净
 */
describe('buildFileTree', () => {
  it('空数组直接返回空（⛔ 不凭空造根节点）', () => {
    expect(buildFileTree([])).toEqual([])
  })

  it('根目录文件（RelFolderPath 为空）直接成为顶层文件行', () => {
    const tree = buildFileTree([docRow({ Code: 'r1', FileName: 'a.pdf', RelFolderPath: '' })])
    expect(tree).toHaveLength(1)
    expect(isFolderNode(tree[0])).toBe(false)
    expect(tree[0].Code).toBe('r1')
  })

  it('按 RelFolderPath 逐级建文件夹，并把文件挂到最深目录', () => {
    const tree = buildFileTree([
      docRow({ Code: 'f1', FileName: 'a.pdf', RelFolderPath: '资料' }),
      docRow({ Code: 'f2', FileName: 'b.pdf', RelFolderPath: '资料/子' }),
    ])
    expect(tree).toHaveLength(1)
    const folder = tree[0] as FolderTreeNode
    expect(isFolderNode(folder)).toBe(true)
    expect(folder.Code).toBe('folder:资料')
    expect(folder.FileName).toBe('资料')
    expect(folder.FileCount).toBe(2)

    // 子层：文件夹在前、文件在后
    const kids = folder.Children
    expect(kids).toHaveLength(2)
    const sub = kids[0] as FolderTreeNode
    expect(sub.Code).toBe('folder:资料/子')
    expect(sub.FileCount).toBe(1)
    expect(isFolderNode(kids[1])).toBe(false)
    expect(kids[1].Code).toBe('f1')
  })

  it('同一文件夹下多份文件 ⇒ 只建一个节点（⛔ 不因文件多而复制文件夹）', () => {
    const tree = buildFileTree([
      docRow({ Code: 'f1', FileName: 'a.pdf', RelFolderPath: '资料' }),
      docRow({ Code: 'f2', FileName: 'b.pdf', RelFolderPath: '资料' }),
      docRow({ Code: 'f3', FileName: 'c.pdf', RelFolderPath: '资料' }),
    ])
    expect(tree).toHaveLength(1)
    const folder = tree[0] as FolderTreeNode
    expect(folder.FileCount).toBe(3)
  })

  it('路径首尾斜杠被归一（/资料/ ≡ 资料）', () => {
    const tree = buildFileTree([docRow({ Code: 'f1', RelFolderPath: '/资料/' })])
    expect(tree).toHaveLength(1)
    expect(tree[0].Code).toBe('folder:资料')
  })

  it('三层路径逐级建父节点，FileCount 自底向上汇总', () => {
    const tree = buildFileTree([docRow({ Code: 'f1', RelFolderPath: '资料/子/孙' })])
    expect(tree).toHaveLength(1)
    const top = tree[0] as FolderTreeNode
    expect(top.Code).toBe('folder:资料')
    expect(top.FileCount).toBe(1)
    const mid = top.Children[0] as FolderTreeNode
    expect(mid.Code).toBe('folder:资料/子')
    expect(mid.FileCount).toBe(1)
    const leaf = mid.Children[0] as FolderTreeNode
    expect(leaf.Code).toBe('folder:资料/子/孙')
    expect(leaf.FileCount).toBe(1)
  })

  it('同层排序：文件夹在前、文件在后，各按名称字典序', () => {
    const tree = buildFileTree([
      docRow({ Code: 'f1', FileName: 'z.pdf', RelFolderPath: '' }),
      docRow({ Code: 'f2', FileName: 'x.pdf', RelFolderPath: 'bfolder' }),
      docRow({ Code: 'f3', FileName: 'y.pdf', RelFolderPath: 'afolder' }),
    ])
    expect(tree.map((n) => n.FileName)).toEqual(['afolder', 'bfolder', 'z.pdf'])
  })

  it('文件夹与文件通过 IsFolder 区分（⛔ 不靠字段缺失推断）', () => {
    const tree = buildFileTree([docRow({ Code: 'f1', RelFolderPath: '资料' })])
    const folder = tree[0] as FolderTreeNode
    expect(isFolderNode(tree[0])).toBe(true)
    expect(isFolderNode(folder.Children[0])).toBe(false)
  })
})

describe('collectFileCodes（「删除整个文件夹」的依据）', () => {
  it('递归收集子树全部文件 Code，⛔ 不含文件夹自身的 folder:xxx', () => {
    const tree = buildFileTree([
      docRow({ Code: 'f1', FileName: 'a.pdf', RelFolderPath: 'A' }),
      docRow({ Code: 'f2', FileName: 'b.pdf', RelFolderPath: 'A/B' }),
      docRow({ Code: 'f3', FileName: 'c.pdf', RelFolderPath: 'A/B/C' }),
      docRow({ Code: 'f4', FileName: 'd.pdf', RelFolderPath: 'A' }),
    ])
    const root = tree.find((n) => n.Code === 'folder:A') as FolderTreeNode
    const codes = collectFileCodes(root)
    expect(codes.sort()).toEqual(['f1', 'f2', 'f3', 'f4'])
    expect(codes).not.toContain('folder:A')
  })

  it('收集到的是文件 Code（GUID），可直接交给后端批量删', () => {
    const tree = buildFileTree([docRow({ Code: 'only-file', RelFolderPath: 'single' })])
    const root = tree[0] as FolderTreeNode
    expect(collectFileCodes(root)).toEqual(['only-file'])
  })
})
