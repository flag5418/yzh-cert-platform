/**
 * treeUtils - 树形结构只读查询函数（V2）
 *
 * 设计原则：
 * 1. 纯函数：无副作用，输入确定则输出确定
 * 2. 只读：不修改入参，返回新数据或计算结果
 * 3. 可测试：每个函数可独立单元测试
 * 4. 通用性：不依赖特定框架
 *
 * 与 V1 的区别：
 * - 移除了所有 UI 状态相关函数（勾选/展开/选中）
 * - 移除了所有修改类函数（归入 treeOps）
 * - 保留只读查询函数
 */

import type { TreeNode, TreeConfig } from '../types/tree'

// ========================================================
// 一、构造类
// ========================================================

/**
 * 核心：List → Tree（扁平集合转标准树结构）
 *
 * 算法：O(n) 两次遍历
 * 1. 第一次遍历：所有节点放入 map<code, TreeNode>
 * 2. 第二次遍历：根据 parentCode 挂载到父节点 children
 *
 * @param items 扁平实体列表
 * @param config 字段映射配置
 * @param options 构造选项
 * @returns 树节点数组（新数据，不修改入参）
 *
 * @example
 * const items = [
 *   { deptCode: 'C001', deptName: '总公司', parentCode: null },
 *   { deptCode: 'D001', deptName: '技术部', parentCode: 'C001' },
 *   { deptCode: 'T001', deptName: '前端组', parentCode: 'D001' }
 * ]
 * const tree = buildTree(items, {
 *   codeField: 'deptCode',
 *   nameField: 'deptName',
 *   parentCodeField: 'parentCode',
 *   rootParentCode: null
 * })
 */
export function buildTree<T>(
  items: T[],
  config: Pick<TreeConfig<T>, 'codeField' | 'nameField' | 'parentCodeField' | 'typeField' | 'leafField' | 'sortField' | 'extraFields' | 'rootParentCode'>,
  options?: {
    /** 从某个节点开始构建子树 */
    startFromCode?: string
    /** 最大层级限制 */
    maxLevel?: number
    /** 当前层级（内部递归用） */
    currentLevel?: number
  }
): TreeNode<T>[] {
  if (!items || items.length === 0) return []

  const {
    codeField,
    nameField,
    parentCodeField,
    typeField,
    leafField,
    sortField,
    extraFields,
    rootParentCode
  } = config

  const maxLevel = options?.maxLevel ?? 0
  const startFromCode = options?.startFromCode
  const currentLevel = options?.currentLevel ?? 0

  // 限制层级
  if (maxLevel > 0 && currentLevel >= maxLevel) return []

  // 创建节点映射
  const nodeMap = new Map<string, TreeNode<T>>()
  const result: TreeNode<T>[] = []

  for (const item of items) {
    const code = getFieldValue(item, codeField) as string
    const name = getFieldValue(item, nameField) as string
    const parentCode = (getFieldValue(item, parentCodeField) as string | null) ?? null
    const nodeType = typeField ? (getFieldValue(item, typeField) as string) : undefined
    const isLeaf = leafField ? (getFieldValue(item, leafField) as boolean) : undefined
    const sort = sortField ? (getFieldValue(item, sortField) as number) : undefined

    // 收集扩展字段
    let extra: Record<string, any> | undefined
    if (extraFields && extraFields.length > 0) {
      extra = {}
      for (const field of extraFields) {
        extra[field] = getFieldValue(item, field)
      }
    }

    const node: TreeNode<T> = {
      code,
      name,
      parentCode,
      nodeType,
      isLeaf,
      sort,
      extra,
      children: [],
      raw: item
    }

    nodeMap.set(code, node)
  }

  // 构建父子关系
  for (const node of nodeMap.values()) {
    if (startFromCode && node.code === startFromCode) {
      result.push(node)
    } else if (!startFromCode && (node.parentCode === rootParentCode || node.parentCode === null || node.parentCode === '')) {
      result.push(node)
    } else {
      const parent = nodeMap.get(node.parentCode ?? '')
      if (parent) {
        parent.children.push(node)
      }
    }
  }

  // 排序
  result.sort((a, b) => (a.sort ?? 0) - (b.sort ?? 0))

  return result
}

/**
 * 将单个实体转为 TreeNode
 */
export function entityToNode<T>(
  entity: T,
  config: Pick<TreeConfig<T>, 'codeField' | 'nameField' | 'parentCodeField' | 'typeField' | 'leafField' | 'sortField' | 'extraFields'>,
  _level: number
): TreeNode<T> {
  const {
    codeField,
    nameField,
    parentCodeField,
    typeField,
    leafField,
    sortField,
    extraFields
  } = config

  const code = getFieldValue(entity, codeField) as string
  const name = getFieldValue(entity, nameField) as string
  const parentCode = (getFieldValue(entity, parentCodeField) as string | null) ?? null
  const nodeType = typeField ? (getFieldValue(typeField as any, typeField) as string) : undefined
  const isLeaf = leafField ? (getFieldValue(leafField as any, leafField) as boolean) : undefined
  const sort = sortField ? (getFieldValue(entity, sortField) as number) : undefined

  let extra: Record<string, any> | undefined
  if (extraFields && extraFields.length > 0) {
    extra = {}
    for (const field of extraFields) {
      extra[field] = getFieldValue(entity, field)
    }
  }

  return {
    code,
    name,
    parentCode,
    nodeType,
    isLeaf,
    sort,
    extra,
    children: [],
    raw: entity
  }
}

/**
 * TreeNode 转回实体（用于提交修改，返回部分字段）
 */
export function nodeToEntity<T>(
  node: TreeNode<T>,
  config: Pick<TreeConfig<T>, 'codeField' | 'nameField' | 'parentCodeField' | 'typeField' | 'sortField'>
): Partial<T> {
  const result: Record<string, any> = {
    [config.codeField]: node.code,
    [config.nameField]: node.name,
    [config.parentCodeField]: node.parentCode
  }
  if (config.typeField && node.nodeType) {
    result[config.typeField] = node.nodeType
  }
  if (config.sortField && node.sort !== undefined) {
    result[config.sortField] = node.sort
  }
  return result as Partial<T>
}

/**
 * 扁平化：Tree → List
 */
export function flattenTree<T>(roots: TreeNode<T>[]): TreeNode<T>[] {
  const result: TreeNode<T>[] = []
  const collect = (nodes: TreeNode<T>[]) => {
    for (const node of nodes) {
      result.push(node)
      if (node.children && node.children.length > 0) {
        collect(node.children)
      }
    }
  }
  collect(roots)
  return result
}

// ========================================================
// 二、遍历/查询类
// ========================================================

/**
 * 获取所有子孙节点（扁平列表，不包含自身）
 */
export function getDescendants<T>(node: TreeNode<T>): TreeNode<T>[] {
  const result: TreeNode<T>[] = []
  const collect = (children: TreeNode<T>[]) => {
    for (const child of children) {
      result.push(child)
      if (child.children && child.children.length > 0) {
        collect(child.children)
      }
    }
  }
  collect(node.children)
  return result
}

/**
 * 获取所有子孙节点（包含自身）
 */
export function getDescendantsWithSelf<T>(node: TreeNode<T>): TreeNode<T>[] {
  return [node, ...getDescendants(node)]
}

/**
 * 向上：获取所有祖先节点（从根到父节点路径）
 */
export function getAncestors<T>(node: TreeNode<T>, rootNodes: TreeNode<T>[]): TreeNode<T>[] {
  const path: TreeNode<T>[] = []
  let current: TreeNode<T> | undefined = node

  while (current && current.parentCode) {
    const found: TreeNode<T> | null = findNode(rootNodes, current.parentCode)
    if (found) {
      path.unshift(found)
      current = found
    } else {
      break
    }
  }

  return path
}

/**
 * 获取从根到当前节点的完整路径 code[]
 */
export function getPath<T>(rootNodes: TreeNode<T>[], node: TreeNode<T>): string[] {
  const ancestors = getAncestors(node, rootNodes)
  return [...ancestors.map(n => n.code), node.code]
}

/**
 * 获取从根到当前节点的完整路径 name[]
 */
export function getPathNames<T>(rootNodes: TreeNode<T>[], node: TreeNode<T>): string[] {
  const ancestors = getAncestors(node, rootNodes)
  return [...ancestors.map(n => n.name), node.name]
}

/**
 * 按 code 查找节点（深度优先）
 */
export function findNode<T>(rootNodes: TreeNode<T>[], code: string): TreeNode<T> | null {
  for (const node of rootNodes) {
    if (node.code === code) return node
    if (node.children && node.children.length > 0) {
      const found = findNode(node.children, code)
      if (found) return found
    }
  }
  return null
}

/**
 * 按条件查找节点
 */
export function findNodeBy<T>(
  rootNodes: TreeNode<T>[],
  predicate: (node: TreeNode<T>) => boolean
): TreeNode<T> | null {
  for (const node of rootNodes) {
    if (predicate(node)) return node
    if (node.children && node.children.length > 0) {
      const found = findNodeBy(node.children, predicate)
      if (found) return found
    }
  }
  return null
}

/**
 * 按条件查找所有匹配节点
 */
export function findNodesBy<T>(
  rootNodes: TreeNode<T>[],
  predicate: (node: TreeNode<T>) => boolean
): TreeNode<T>[] {
  const result: TreeNode<T>[] = []
  const collect = (nodes: TreeNode<T>[]) => {
    for (const node of nodes) {
      if (predicate(node)) result.push(node)
      if (node.children && node.children.length > 0) {
        collect(node.children)
      }
    }
  }
  collect(rootNodes)
  return result
}

/**
 * 获取父节点
 */
export function getParent<T>(rootNodes: TreeNode<T>[], node: TreeNode<T>): TreeNode<T> | null {
  if (!node.parentCode) return null
  return findNode(rootNodes, node.parentCode)
}

// ========================================================
// 三、过滤/搜索类
// ========================================================

/**
 * 按节点类型过滤
 */
export function filterByType<T>(rootNodes: TreeNode<T>[], nodeType: string): TreeNode<T>[] {
  return findNodesBy(rootNodes, n => n.nodeType === nodeType)
}

/**
 * 搜索：返回匹配节点列表（支持模糊匹配 name）
 */
export function search<T>(rootNodes: TreeNode<T>[], keyword: string): TreeNode<T>[] {
  if (!keyword || !keyword.trim()) return []
  const lower = keyword.toLowerCase()
  return findNodesBy(rootNodes, n => n.name.toLowerCase().includes(lower))
}

/**
 * 搜索：返回匹配节点及其所有祖先（用于高亮+展开）
 */
export function searchWithAncestors<T>(rootNodes: TreeNode<T>[], keyword: string): TreeNode<T>[] {
  const matched = search(rootNodes, keyword)
  const result = new Set<TreeNode<T>>()

  for (const node of matched) {
    result.add(node)
    const ancestors = getAncestors(node, rootNodes)
    ancestors.forEach(a => result.add(a))
  }

  return Array.from(result)
}

/**
 * 过滤树（保留满足条件的节点及其祖先）
 * 返回新树，不修改入参
 */
export function filterTree<T>(
  rootNodes: TreeNode<T>[],
  predicate: (node: TreeNode<T>) => boolean
): TreeNode<T>[] {
  const filter = (nodes: TreeNode<T>[]): TreeNode<T>[] => {
    const result: TreeNode<T>[] = []
    for (const node of nodes) {
      const filteredChildren = filter(node.children)
      if (predicate(node) || filteredChildren.length > 0) {
        result.push({
          ...node,
          children: filteredChildren
        })
      }
    }
    return result
  }
  return filter(rootNodes)
}

// ========================================================
// 四、统计类
// ========================================================

/**
 * 获取子节点数量（递归，不包含自身）
 */
export function getChildrenCount<T>(node: TreeNode<T>): number {
  return getDescendants(node).length
}

/**
 * 获取树的深度
 */
export function getDepth<T>(rootNodes: TreeNode<T>[]): number {
  const getLevel = (nodes: TreeNode<T>[], level: number): number => {
    if (nodes.length === 0) return level
    let max = level
    for (const node of nodes) {
      if (node.children && node.children.length > 0) {
        max = Math.max(max, getLevel(node.children, level + 1))
      }
    }
    return max
  }
  return getLevel(rootNodes, 0)
}

/**
 * 获取节点总数
 */
export function getTotalCount<T>(rootNodes: TreeNode<T>[]): number {
  return flattenTree(rootNodes).length
}

/**
 * 获取指定层级节点数
 */
export function getNodesAtLevel<T>(rootNodes: TreeNode<T>[], targetLevel: number): TreeNode<T>[] {
  const result: TreeNode<T>[] = []
  const traverse = (nodes: TreeNode<T>[], level: number) => {
    for (const node of nodes) {
      if (level === targetLevel) result.push(node)
      if (node.children && node.children.length > 0) {
        traverse(node.children, level + 1)
      }
    }
  }
  traverse(rootNodes, 0)
  return result
}

// ========================================================
// 五、验证类
// ========================================================

/**
 * 验证树结构完整性
 * - 无循环引用
 * - 所有节点 parentCode 可找到（根节点除外）
 *
 * 返回 { valid, errors }
 */
export function validate<T>(roots: TreeNode<T>[]): { valid: boolean; errors: string[] } {
  const errors: string[] = []
  const nodes = flattenTree(roots)
  const nodeCodes = new Set(nodes.map(n => n.code))

  // 检查空 code
  for (const node of nodes) {
    if (!node.code && node.code !== null) {
      errors.push(`节点 ${node.name} 的 code 为空`)
    }
  }

  // 检查重复 code
  const codeCount = new Map<string, number>()
  for (const node of nodes) {
    codeCount.set(node.code, (codeCount.get(node.code) ?? 0) + 1)
  }
  for (const [code, count] of codeCount) {
    if (count > 1) {
      errors.push(`code "${code}" 重复 ${count} 次`)
    }
  }

  // 检查孤儿节点
  for (const node of nodes) {
    if (node.parentCode !== null && node.parentCode !== '' && !nodeCodes.has(node.parentCode)) {
      errors.push(`节点 "${node.name}" 的 parentCode "${node.parentCode}" 不存在`)
    }
  }

  // 检查循环引用
  if (hasCycle(roots)) {
    errors.push('树存在循环引用')
  }

  return {
    valid: errors.length === 0,
    errors
  }
}

/**
 * 检测循环引用
 */
export function hasCycle<T>(roots: TreeNode<T>[]): boolean {
  const visited = new Set<string>()
  const stack = new Set<string>()

  const dfs = (node: TreeNode<T>): boolean => {
    if (stack.has(node.code)) return true
    if (visited.has(node.code)) return false

    visited.add(node.code)
    stack.add(node.code)

    for (const child of node.children) {
      if (dfs(child)) return true
    }

    stack.delete(node.code)
    return false
  }

  for (const root of roots) {
    if (dfs(root)) return true
  }
  return false
}

// ========================================================
// 六、辅助函数
// ========================================================

/**
 * 获取嵌套字段值（支持点号路径）
 */
function getFieldValue(obj: any, path: string): any {
  if (!obj || !path) return undefined
  const parts = path.split('.')
  let current = obj
  for (const part of parts) {
    if (current == null) return undefined
    current = current[part]
  }
  return current
}

/**
 * 默认导出
 */
export const treeUtils = {
  // 构造
  buildTree,
  entityToNode,
  nodeToEntity,
  flattenTree,
  // 遍历/查询
  getDescendants,
  getDescendantsWithSelf,
  getAncestors,
  getPath,
  getPathNames,
  findNode,
  findNodeBy,
  findNodesBy,
  getParent,
  // 过滤/搜索
  filterByType,
  search,
  searchWithAncestors,
  filterTree,
  // 统计
  getChildrenCount,
  getDepth,
  getTotalCount,
  getNodesAtLevel,
  // 验证
  validate,
  hasCycle
}
