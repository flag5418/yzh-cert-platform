/**
 * buildFolderFileTree 回归测试（2026-10-15）
 *
 * 背景：NC 规则设计器三处文件下拉（标准文件缺失 / 文档字段 / 文档表格）
 * 由扁平 el-select 改为 el-tree-select —— 本工具负责「文件夹路径 + 文件」构树。
 *
 * 锁定契约：
 * - 文件夹 value = `dir:${path}` + disabled（不可选）；文件 value = code（与 config 写入值一致）
 * - 根级文件（folderPath 空）直接挂根；同级先文件夹后文件
 * - 路径容错：首尾 `/`、空段、code 空行
 */
import { describe, expect, it } from 'vitest'
import { buildFolderFileTree } from '@share/composables/workflow/buildFolderFileTree'

describe('buildFolderFileTree', () => {
  it('空列表 / 全部 code 为空 → 空树', () => {
    expect(buildFolderFileTree([])).toEqual([])
    expect(buildFolderFileTree([{ code: '', name: 'x', folderPath: 'a' }])).toEqual([])
  })

  it('根级文件（folderPath 空）直接挂根，为可选叶子', () => {
    const tree = buildFolderFileTree([
      { code: 'r1', name: '手册.doc' },
      { code: 'r2', name: '程序.doc', folderPath: '' },
      { code: 'r3', name: '记录.doc', folderPath: undefined },
    ])
    expect(tree).toHaveLength(3)
    expect(tree.every((n) => n.disabled !== true)).toBe(true)
    expect(tree.map((n) => n.value).sort()).toEqual(['r1', 'r2', 'r3'])
  })

  it('多级嵌套：中间层文件夹 disabled、叶子 value = code、label 取路径末段', () => {
    const tree = buildFolderFileTree([
      { code: 'f1', name: '质量手册.doc', folderPath: '质量/一级' },
      { code: 'f2', name: '程序文件.doc', folderPath: '质量/一级' },
      { code: 'f3', name: '管理制度.doc', folderPath: '管理' },
    ])
    expect(tree).toHaveLength(2)
    const zhiliang = tree.find((n) => n.label === '质量')!
    expect(zhiliang.disabled).toBe(true)
    expect(zhiliang.value).toBe('dir:质量')
    expect(zhiliang.children).toHaveLength(1)
    const yiji = zhiliang.children![0]
    expect(yiji.label).toBe('一级')
    expect(yiji.disabled).toBe(true)
    expect(yiji.value).toBe('dir:质量/一级')
    // 叶子按 label zh-CN 升序：程序文件 < 质量手册
    expect(yiji.children!.map((n) => n.value)).toEqual(['f2', 'f1'])
    expect(yiji.children![0].disabled).toBeUndefined()
  })

  it('同级排序：先文件夹后文件、各自按名升序（zh-CN）', () => {
    const tree = buildFolderFileTree([
      { code: 'fileB', name: 'B文件.doc', folderPath: '' },
      { code: 'dirB', name: '乙夹', folderPath: '乙夹' },
      { code: 'fileA', name: 'A文件.doc', folderPath: '' },
      { code: 'dirA', name: '甲夹', folderPath: '甲夹' },
    ])
    const labels = tree.map((n) => n.label)
    // 文件夹在前（甲/乙 zh-CN 升序），文件在后（A/B）
    expect(labels).toEqual(['甲夹', '乙夹', 'A文件.doc', 'B文件.doc'])
  })

  it('路径容错：首尾 / 与空段剔除，等价路径合并到同一文件夹', () => {
    const tree = buildFolderFileTree([
      { code: 'a', name: 'a.doc', folderPath: '/质量//程序/' },
      { code: 'b', name: 'b.doc', folderPath: '质量/程序' },
    ])
    // 根 = 质量 → 子 = 质量/程序 → 叶子 a/b（等价路径已合并，无重复文件夹）
    expect(tree).toHaveLength(1)
    expect(tree[0].value).toBe('dir:质量')
    expect(tree[0].children!).toHaveLength(1)
    expect(tree[0].children![0].value).toBe('dir:质量/程序')
    expect(tree[0].children![0].children!.map((n) => n.value).sort()).toEqual(['a', 'b'])
  })

  it('孤立深层路径：缺失的中间层文件夹自动补齐', () => {
    const tree = buildFolderFileTree([
      { code: 'x', name: 'x.doc', folderPath: 'a/b/c' },
    ])
    const a = tree[0]
    expect(a.value).toBe('dir:a')
    expect(a.children![0].value).toBe('dir:a/b')
    expect(a.children![0].children![0].value).toBe('dir:a/b/c')
    expect(a.children![0].children![0].children![0].value).toBe('x')
  })
})
