/**
 * 扁平「文件夹路径 + 文件」列表 → el-tree-select 树（2026-10-15）
 *
 * 背景：NC 规则设计器三处文件下拉（标准文件缺失 / 文档字段 / 文档表格）原为
 * 扁平 el-select —— 标准文件动辄几十上百个，用户按文件名海选极难。
 * 改为「文件夹 → 文件」树形选择，仅文件叶子可选。
 *
 * 数据来源（folderPath 口径与实体 `StandardDirectoryFile.FolderPath` 一致）：
 * - `std-files`：后端实体计算属性 FolderPath 已随 JSON 序列化（PascalCase），前端归一为小写
 * - `configured-rules`：后端新增 camelCase `folderPath`（由视图 FullPath 去文件名后缀得出）
 *
 * 树节点约定：
 * - 文件夹：`value = dir:${path}`（路径全键，保证唯一）+ `disabled: true`（不可选）
 * - 文件：`value = code`（ruleCode / standardFileCode —— 与既有 config 写入值逐字一致）
 * - 根级文件（folderPath 空）直接挂根；同级排序：先文件夹后文件、各自按名升序
 */

export interface FlatFileItem {
  /** 业务键：ruleCode（文档下拉）/ standardFileCode（标准文件下拉）—— 作为树叶子 value */
  code: string
  /** 展示名（文件名） */
  name: string
  /** 文件夹路径（'/' 分隔，无首尾斜杠）；空 = 根级文件 */
  folderPath?: string
}

export interface FolderFileTreeNode {
  value: string
  label: string
  disabled?: boolean
  children?: FolderFileTreeNode[]
}

/** 文件夹节点 value 前缀 —— 与文件 code（GUID/规则码）不可能撞 */
const DIR_PREFIX = 'dir:'

/**
 * 扁平列表 → 文件夹树。
 *
 * <p>容错：code 空的行跳过（树叶子必须有可写入 config 的 value）；
 * folderPath 首尾 `/` 与空段一律剔除（`/a//b/` ≡ `a/b`）。</p>
 */
export function buildFolderFileTree(items: FlatFileItem[]): FolderFileTreeNode[] {
  const roots: FolderFileTreeNode[] = []
  /** path → 文件夹节点（插入序 = 首次遇到该路径的顺序，排序在末尾统一做） */
  const dirIndex = new Map<string, FolderFileTreeNode>()

  function ensureDir(path: string): FolderFileTreeNode {
    const existing = dirIndex.get(path)
    if (existing) return existing
    const node: FolderFileTreeNode = {
      value: DIR_PREFIX + path,
      label: path.split('/').pop() || path,
      disabled: true,
      children: [],
    }
    dirIndex.set(path, node)
    const parentPath = parentOf(path)
    if (parentPath) ensureDir(parentPath).children!.push(node)
    else roots.push(node)
    return node
  }

  for (const item of items || []) {
    if (!item?.code) continue
    const folderPath = normalizeFolderPath(item.folderPath)
    const leaf: FolderFileTreeNode = { value: item.code, label: item.name || item.code }
    if (folderPath) ensureDir(folderPath).children!.push(leaf)
    else roots.push(leaf)
  }

  sortSiblings(roots)
  return roots
}

/** 去首尾 `/`、剔除空段；null/undefined/纯斜杠 → '' */
function normalizeFolderPath(raw?: string): string {
  if (!raw) return ''
  return raw
    .split('/')
    .map((s) => s.trim())
    .filter(Boolean)
    .join('/')
}

/** `a/b/c` → `a/b`；根级 → '' */
function parentOf(path: string): string {
  const i = path.lastIndexOf('/')
  return i <= 0 ? '' : path.slice(0, i)
}

/** 同级排序：先文件夹（按名）后文件（按名）——树形浏览的稳定预期 */
function sortSiblings(nodes: FolderFileTreeNode[]): void {
  nodes.sort((a, b) => {
    const aDir = a.disabled ? 0 : 1
    const bDir = b.disabled ? 0 : 1
    if (aDir !== bDir) return aDir - bDir
    return a.label.localeCompare(b.label, 'zh-CN')
  })
  for (const n of nodes) if (n.children?.length) sortSiblings(n.children)
}
