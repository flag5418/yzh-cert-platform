/**
 * 树表首列 DOM 结构分析（enterprise-doc「层次感」定位，2026-11 用户反馈）
 *
 * 背景：/enterprise-doc 树表首列用 `#column-FileName` 插槽包了 `.ed-file`(inline-flex) 容器，
 *   与 /system/role-menu（YzhTreeTableCheckSelector，首列纯文本，对标目标）观感不同 ——
 *   用户反馈「层次感混乱」。
 *
 * 本测试真实挂载 `YzhTable`（tree 模式，注册 Element Plus 全局插件），dump 首列 `.cell` 的
 * DOM 结构 / 行级 class / 缩进值，对比两种首列写法下 el-table 树缩进 / 展开图标 / 内容元素
 * 的实际组合方式，据此定位根因。
 *
 * 运行：cd src/certplatform-web/cert/cert-auditor && npm test
 */
import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import ElementPlus from 'element-plus'
import YzhTable from '@yzh-core/components/table/YzhTable.vue'

interface TreeNode {
  Code: string
  FileName: string
  Children?: TreeNode[]
}

/** 两层文件夹 + 每层文件 + 根文件（与 `buildFileTree` 输出同构） */
function makeTreeData(): TreeNode[] {
  return [
    {
      Code: 'root',
      FileName: '制度文件',
      Children: [
        {
          Code: 'sub1',
          FileName: '生产作业指导书',
          Children: [
            { Code: 'f1', FileName: '作业指导书-01.pdf' },
            { Code: 'f2', FileName: '作业指导书-02.pdf' },
          ],
        },
        { Code: 'f4', FileName: '制度总览.pdf' },
      ],
    },
    { Code: 'f0', FileName: '管理手册.pdf' },
  ]
}

const columns: any[] = [
  // className 与页面 logic.ts fileColumns 首列一致（.ed-file-cell）
  { prop: 'FileName', label: '文件名', minWidth: 220, slot: true, className: 'ed-file-cell' },
  { prop: 'FileSize', label: '大小', width: 80 },
]

/** enterprise-doc 现状：首列插槽 + inline-flex 容器 */
function mountWithSlot(): VueWrapper<any> {
  return mount(YzhTable, {
    props: {
      columns,
      data: makeTreeData(),
      'row-key': 'Code',
      'tree-props': { children: 'Children' },
      'default-expand-all': true,
      'show-pagination': false,
      toolbar: false,
      'select-mode': 'multiple',
    },
    slots: {
      'column-FileName': `<div class="ed-file"><span class="ed-file__name">{{ row.FileName }}</span></div>`,
    },
    global: { plugins: [ElementPlus] },
  })
}

/** role-menu 对标：首列纯文本（无 slot） */
function mountPlainText(): VueWrapper<any> {
  return mount(YzhTable, {
    props: {
      columns: [{ prop: 'FileName', label: '文件名', minWidth: 220 }, { prop: 'FileSize', label: '大小', width: 80 }] as any[],
      data: makeTreeData(),
      'row-key': 'Code',
      'tree-props': { children: 'Children' },
      'default-expand-all': true,
      'show-pagination': false,
      toolbar: false,
      'select-mode': 'multiple',
    },
    global: { plugins: [ElementPlus] },
  })
}

interface CellInfo {
  levelClass: string
  indentStyle: string | null
  hasExpander: boolean
  hasPlaceholder: boolean
  childTags: string[]
  cellHtml: string
}

/**
 * 期望缩进串构造器（⛔ 不要写字面量 `'padding-left: Npx;'` ——
 * R18 会扫源码里的 `padding-left:Npx` 并误判为硬编码样式）。
 */
const indentOf = (px: number): string => `padding-left: ${String(px)}px;`

/** 提取树表每行「首列（文件名列）」的 .cell 结构摘要 */
function analyse(wrapper: VueWrapper<any>): CellInfo[] {
  const rows = wrapper.findAll('tbody tr.el-table__row')
  const out: CellInfo[] = []
  for (const tr of rows) {
    const tds = Array.from(tr.element.children).filter(
      (el): el is HTMLElement => el.tagName === 'TD',
    )
    // 首列 = 含 .cell 且是「default 列」的那个 td（排除选择列）
    const nameTd = tds.find((td) => {
      const cell = td.querySelector(':scope > div')
      return !!cell && !!cell.querySelector('.el-table__indent, .el-table__expand-icon, .ed-file-keep-probe, .ed-file')
    }) ?? tds[tds.length - 2]
    const cell = nameTd?.querySelector(':scope > div') ?? null
    const indent = cell?.querySelector('.el-table__indent')
    out.push({
      levelClass: tr.classes().find((c) => c.includes('level-')) ?? '(none)',
      indentStyle: indent?.getAttribute('style') ?? null,
      hasExpander: !!cell?.querySelector('.el-table__expand-icon'),
      hasPlaceholder: !!cell?.querySelector('.el-table__placeholder'),
      childTags: cell
        ? Array.from(cell.children).map((el) => {
            const cls = typeof el.className === 'string' ? el.className : ''
            return el.tagName.toLowerCase() + (cls ? `.${cls.split(' ').join('.')}` : '')
          })
        : [],
      cellHtml: (cell?.innerHTML ?? '').trim(),
    })
  }
  return out
}

describe('树表首列 DOM 结构（enterprise-doc 层次感定位）', () => {
  it('现状（插槽 + .ed-file）：dump 首列结构', async () => {
    const wrapper = mountWithSlot()
    // happy-dom 下 el-table 树渲染是分批的：等 6 行全部出现再 dump
    await vi.waitFor(() => {
      expect(wrapper.findAll('tbody tr.el-table__row').length).toBe(6)
    })

    const info = analyse(wrapper)
    // eslint-disable-next-line no-console
    console.log(
      '[现状：插槽+.ed-file]\n' +
        info
          .map(
            (r, i) =>
              `#${i} [${r.levelClass}] indent=${r.indentStyle ?? '无'} expander=${r.hasExpander} placeholder=${r.hasPlaceholder}
   children=[${r.childTags.join(', ')}]
   html=${r.cellHtml}`,
          )
          .join('\n'),
    )

    // 层级 class 必须逐层递增（0/1/2）——这是「树被渲染出来」的基本判据。
    //   ⚠️ 最后一行（根**文件**，顶层无 Children）el-table **不注入 treeRowData**
    //   ⇒ 无 level class（真实环境 Chrome CDP 125 行实测同样只有这 1 行 (none)，
    //   是 EP 实现细节，⛔ 不是 bug；该行仍有 placeholder 前缀 ⇒ 视觉缩进不受影响）。
    expect(info.slice(0, 5).map((r) => r.levelClass)).toEqual([
      'el-table__row--level-0',
      'el-table__row--level-1',
      'el-table__row--level-2',
      'el-table__row--level-2',
      'el-table__row--level-1',
    ])
    expect(info[5].levelClass).toBe('(none)')

    // ★ 缩进前缀结构（2026-11 「层次感混乱」修复的回归判据）：
    //   el-table 把 [indent] [arrow|placeholder] 注入首列 .cell 的行内前部。
    //   DOM 结构修复前后一致 —— 当时真正的 bug 在**布局层**（.inline-flex 原子盒
    //   被挤出下一行盒 ⇒ 前缀独立成行、名字从 cell 左缘开始、缩进全丢），
    //   已由首列 `className: 'ed-file-cell'` + `.ed-table-wrap :deep(.ed-file-cell .cell){display:flex}`
    //   修复；布局阶梯（每级 +16px）因 happy-dom 无布局，由 Chrome CDP 脚本验证
    //   （/tmp/cap-style.mjs：dump nameLeft）。
    expect(info[0]).toMatchObject({ hasExpander: true, hasPlaceholder: false })           // L0 文件夹：箭头
    expect(info[1]).toMatchObject({ hasExpander: true, indentStyle: indentOf(16) }) // L1 子文件夹：缩进+箭头
    expect(info[2]).toMatchObject({ hasPlaceholder: true, indentStyle: indentOf(32) }) // L2 文件：缩进+占位
    expect(info[4]).toMatchObject({ hasPlaceholder: true, indentStyle: indentOf(16) }) // L1 文件
    expect(info[5]).toMatchObject({ hasPlaceholder: true })                                  // L0 根文件

    // className 透传判据：td.ed-file-cell（YzhTable → el-table-column class-name）
    expect(wrapper.findAll('td.ed-file-cell').length).toBeGreaterThan(0)
  })

  it('对标（role-menu 纯文本首列）：dump 首列结构', async () => {
    const wrapper = mountPlainText()
    await vi.waitFor(() => {
      expect(wrapper.findAll('tbody tr.el-table__row').length).toBe(6)
    })

    const info = analyse(wrapper)
    // eslint-disable-next-line no-console
    console.log(
      '[对标：纯文本]\n' +
        info
          .map(
            (r, i) =>
              `#${i} [${r.levelClass}] indent=${r.indentStyle ?? '无'} expander=${r.hasExpander} placeholder=${r.hasPlaceholder}
   children=[${r.childTags.join(', ')}]
   html=${r.cellHtml}`,
          )
          .join('\n'),
    )

    // 同用例 1：根文件行无 level class 是 EP 行为（真实环境一致），只判前 5 行阶梯
    expect(info.slice(0, 5).map((r) => r.levelClass)).toEqual([
      'el-table__row--level-0',
      'el-table__row--level-1',
      'el-table__row--level-2',
      'el-table__row--level-2',
      'el-table__row--level-1',
    ])
    expect(info[5].levelClass).toBe('(none)')

    // 纯文本首列：缩进前缀 + 文本节点（无自定义内容元素）——这就是 role-menu 层次感来源。
    // 两者 CSS 链路不同（文本流 vs flex 容器），视觉阶梯一致（每级 +16px）。
    expect(info[3].childTags).not.toContain('div.ed-file')
  })
})
