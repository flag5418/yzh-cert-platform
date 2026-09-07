/**
 * treeOps - 树形结构不可变变换函数（V2）
 *
 * 设计原则：
 * 1. 不可变语义：输入旧树，返回新树，从不修改入参
 * 2. 纯函数：无副作用，便于测试和并发安全
 * 3. 错误安全：危险操作（如移动/删除）返回 { tree, error? } 元组
 *
 * 与 V1 的区别：
 * - 从 TreeUtils 中拆出，专责"变换"
 * - 统一返回新数据而非 mutate 原数据
 *
 * 使用场景：
 * - 组件内：通过 TreeBridge 调用
 * - 非组件：直接 import 任意函数使用
 *
 * @example
 * // 场景1：组件内更新
 * const newTree = treeOps.addNode(treeData.value, parentCode, newNode)
 * treeData.value = newTree
 *
 * // 场景2：权限计算
 * const { tree, removed } = treeOps.removeSubtree(currentTree, code)
 * await api.delete(removed.map(n => n.code))
 *
 * // 场景3：多树合并导出
 * const merged = treeOps.mergeRoots(orgTree, stdTree)
 * const rows = treeOps.flatten(merged)
 */

import type { TreeNode, TreePatch } from '../types/tree'

// ========================================================
// 一、变换函数
// ========================================================

/**
 * 新增节点到树
 * 返回新树，不修改入参
 *
 * @param roots 原树
 * @param parentCode 父节点编码（null 表示根级）
 * @param node 要添加的节点
 * @returns 新树
 *
 * @example
 * const newTree = treeOps.addTreeNode(treeData, 'D001', {
 *   code: 'T003', name: '后端组', parentCode: 'D001', children: []
 * })
 */
export function addNode<T>(
  roots: TreeNode<T>[],
  parentCode: string | null,
  node: TreeNode<T>
): TreeNode<T>[] {
  if (parentCode === null || parentCode === '') {
    // 添加到根级
    return [...roots, node]
  }

  // 递归查找父节点并添加
  const addToParent = (nodes: TreeNode<T>[]): TreeNode<T>[] => {
    return nodes.map(n => {
      if (n.code === parentCode) {
        return { ...n, children: [...n.children, node] }
      }
      if (n.children && n.children.length > 0) {
        return { ...n, children: addToParent(n.children) }
      }
      return n
    })
  }

  return addToParent(roots)
}

/**
 * 删除子树
 * 返回新树 + 被删除的节点列表（供级联/审计用）
 *
 * @param roots 原树
 * @param code 要删除的节点编码
 * @returns { tree: 新树, removed: 被删除的子树（含自身） }
 *
 * @example
 * const { tree, removed } = treeOps.removeSubtree(treeData, 'D001')
 * console.log('删除的节点:', removed.map(n => n.code))
 */
export function removeSubtree<T>(
  roots: TreeNode<T>[],
  code: string
): { tree: TreeNode<T>[]; removed: TreeNode<T>[] } {
  const removed: TreeNode<T>[] = []

  const removeFrom = (nodes: TreeNode<T>[]): TreeNode<T>[] => {
    const result: TreeNode<T>[] = []
    for (const node of nodes) {
      if (node.code === code) {
        // 收集被删除的节点及其所有子孙
        removed.push(node)
        collectDescendants(node).forEach(n => removed.push(n))
        // 不加入结果（即删除）
      } else {
        if (node.children && node.children.length > 0) {
          result.push({ ...node, children: removeFrom(node.children) })
        } else {
          result.push(node)
        }
      }
    }
    return result
  }

  return { tree: removeFrom(roots), removed }
}

/**
 * 移动子树到新父节点
 * 内置防环/防深度越限校验，失败返回错误信息
 *
 * @param roots 原树
 * @param code 要移动的节点编码
 * @param newParentCode 新父节点编码（null 表示移到根级）
 * @param maxDepth 最大深度限制（可选）
 * @returns { tree: 新树, error?: 错误信息 }
 *
 * @example
 * const result = treeOps.moveSubtree(treeData, 'D001', 'C002')
 * if (result.error) {
 *   ElMessage.error(result.error)
 * } else {
 *   treeData.value = result.tree
 * }
 */
export function moveSubtree<T>(
  roots: TreeNode<T>[],
  code: string,
  newParentCode: string | null,
  maxDepth?: number
): { tree: TreeNode<T>[]; error?: string } {
  // 1. 查找要移动的节点
  const nodeToMove = findNode(roots, code)
  if (!nodeToMove) {
    return { tree: roots, error: `节点 "${code}" 不存在` }
  }

  // 2. 不能移动到自己
  if (code === newParentCode) {
    return { tree: roots, error: '不能移动到自己' }
  }

  // 3. 检查新父节点是否存在（根级除外）
  if (newParentCode !== null && newParentCode !== '') {
    const newParent = findNode(roots, newParentCode)
    if (!newParent) {
      return { tree: roots, error: `目标父节点 "${newParentCode}" 不存在` }
    }

    // 4. 防环：新父不能是当前节点的子孙
    if (isDescendant(nodeToMove, newParentCode)) {
      return { tree: roots, error: '不能移动到自己的子树下（会形成循环）' }
    }
  }

  // 5. 深度检查
  if (maxDepth !== undefined && maxDepth > 0) {
    const nodeDepth = getNodeDepth(nodeToMove)
    const newParentLevel = newParentCode === null || newParentCode === ''
      ? 0
      : getLevel(roots, newParentCode)
    if (newParentLevel + 1 + nodeDepth > maxDepth) {
      return { tree: roots, error: `移动后深度将超过限制 (${maxDepth})` }
    }
  }

  // 6. 执行移动：先删除再添加
  const { tree: treeAfterRemove } = removeSubtree(roots, code)
  const newTree = addNode(treeAfterRemove, newParentCode, {
    ...nodeToMove,
    parentCode: newParentCode
  })

  return { tree: newTree }
}

/**
 * 更新节点属性
 * 返回新树
 *
 * @param roots 原树
 * @param code 要更新的节点编码
 * @param patch 要更新的属性（Partial）
 * @returns 新树
 *
 * @example
 * const newTree = treeOps.updateNode(treeData, 'D001', {
 *   name: '研发部',
 *   extra: { icon: 'Code' }
 * })
 */
export function updateNode<T>(
  roots: TreeNode<T>[],
  code: string,
  patch: Partial<TreeNode<T>>
): TreeNode<T>[] {
  const update = (nodes: TreeNode<T>[]): TreeNode<T>[] => {
    return nodes.map(n => {
      if (n.code === code) {
        return { ...n, ...patch }
      }
      if (n.children && n.children.length > 0) {
        return { ...n, children: update(n.children) }
      }
      return n
    })
  }
  return update(roots)
}

/**
 * 多棵树合并（异构树聚合）
 * 把多个来源的根列表合并为一个根列表
 *
 * @param rootLists 多个树的根列表
 * @returns 合并后的根列表
 *
 * @example
 * // 合并组织机构和标准树
 * const merged = treeOps.mergeRoots(orgTree, stdTree)
 * const allRows = treeOps.flatten(merged).map(toExportRow)
 */
export function mergeRoots<T>(...rootLists: TreeNode<T>[][]): TreeNode<T>[] {
  const result: TreeNode<T>[] = []
  for (const roots of rootLists) {
    result.push(...roots)
  }
  return result
}

/**
 * 快照差异：计算两棵树的增量补丁
 * 供 UI bridge 应用或差异对比
 *
 * @param oldTree 旧树
 * @param newTree 新树
 * @returns TreePatch（added/removed/updated）
 *
 * @example
 * const patch = treeOps.diff(oldTreeData, newTreeData)
 * console.log('新增:', patch.added.map(n => n.name))
 * console.log('删除:', patch.removed)
 */
export function diff<T>(oldTree: TreeNode<T>[], newTree: TreeNode<T>[]): TreePatch<T> {
  const oldNodes = flattenWithLevel(oldTree)
  const newNodes = flattenWithLevel(newTree)
  const oldMap = new Map(oldNodes.map(n => [n.node.code, n]))
  const newMap = new Map(newNodes.map(n => [n.node.code, n]))

  const added: TreeNode<T>[] = []
  const removed: string[] = []
  const updated: Array<{ code: string; changes: Partial<TreeNode<T>> }> = []

  // 找出新增和更新的
  for (const [, newNode] of newMap) {
    const old = oldMap.get(newNode.node.code)
    if (!old) {
      added.push(newNode.node)
    } else if (!isNodeEqual(old.node, newNode.node)) {
      const changes = getNodeChanges(old.node, newNode.node)
      updated.push({ code: newNode.node.code, changes })
    }
  }

  // 找出删除的
  for (const [code] of oldMap) {
    if (!newMap.has(code)) {
      removed.push(code)
    }
  }

  return { added, removed, updated }
}

// ========================================================
// 二、校验函数
// ========================================================

/**
 * 校验树结构完整性
 * 封装 treeUtils.validate，供 treeOps 命名空间统一调用
 */
export function validate<T>(roots: TreeNode<T>[]): { valid: boolean; errors: string[] } {
  const errors: string[] = []
  const nodes = flatten(roots)
  const nodeCodes = new Set(nodes.map(n => n.code))

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
  if (hasCycleCheck(roots)) {
    errors.push('树存在循环引用')
  }

  return {
    valid: errors.length === 0,
    errors
  }
}

// ========================================================
// 三、纯导出函数（从 treeUtils 透传）
// ========================================================

/**
 * 扁平化树
 */
export function flatten<T>(roots: TreeNode<T>[]): TreeNode<T>[] {
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
// 四、内部辅助函数
// ========================================================

/** 查找节点 */
function findNode<T>(roots: TreeNode<T>[], code: string): TreeNode<T> | null {
  for (const node of roots) {
    if (node.code === code) return node
    if (node.children && node.children.length > 0) {
      const found = findNode(node.children, code)
      if (found) return found
    }
  }
  return null
}

/** 收集所有子孙（包含自身） */
function collectDescendants<T>(node: TreeNode<T>): TreeNode<T>[] {
  const result: TreeNode<T>[] = []
  const collect = (n: TreeNode<T>) => {
    result.push(n)
    n.children.forEach(collect)
  }
  node.children.forEach(collect)
  return result
}

/** 判断 target 是否是 node 的子孙 */
function isDescendant<T>(node: TreeNode<T>, targetCode: string): boolean {
  for (const child of node.children) {
    if (child.code === targetCode) return true
    if (isDescendant(child, targetCode)) return true
  }
  return false
}

/** 获取节点的深度（子树最大深度） */
function getNodeDepth<T>(node: TreeNode<T>): number {
  if (!node.children || node.children.length === 0) return 1
  let max = 0
  for (const child of node.children) {
    max = Math.max(max, getNodeDepth(child))
  }
  return max + 1
}

/** 获取节点在树中的层级 */
function getLevel<T>(roots: TreeNode<T>[], targetCode: string): number {
  const traverse = (nodes: TreeNode<T>[], level: number): number => {
    for (const node of nodes) {
      if (node.code === targetCode) return level
      if (node.children && node.children.length > 0) {
        const found = traverse(node.children, level + 1)
        if (found >= 0) return found
      }
    }
    return -1
  }
  return traverse(roots, 0)
}

/** 检测循环 */
function hasCycleCheck<T>(roots: TreeNode<T>[]): boolean {
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

/** 扁平化带层级 */
function flattenWithLevel<T>(roots: TreeNode<T>[]): Array<{ node: TreeNode<T>; level: number }> {
  const result: Array<{ node: TreeNode<T>; level: number }> = []
  const traverse = (nodes: TreeNode<T>[], level: number) => {
    for (const node of nodes) {
      result.push({ node, level })
      if (node.children && node.children.length > 0) {
        traverse(node.children, level + 1)
      }
    }
  }
  traverse(roots, 0)
  return result
}

/** 比较节点是否相等 */
function isNodeEqual<T>(a: TreeNode<T>, b: TreeNode<T>): boolean {
  return a.code === b.code &&
    a.name === b.name &&
    a.parentCode === b.parentCode &&
    a.nodeType === b.nodeType &&
    a.isLeaf === b.isLeaf &&
    a.sort === b.sort
}

/** 获取节点变更 */
function getNodeChanges<T>(oldNode: TreeNode<T>, newNode: TreeNode<T>): Partial<TreeNode<T>> {
  const changes: Partial<TreeNode<T>> = {}
  if (oldNode.name !== newNode.name) changes.name = newNode.name
  if (oldNode.parentCode !== newNode.parentCode) changes.parentCode = newNode.parentCode
  if (oldNode.nodeType !== newNode.nodeType) changes.nodeType = newNode.nodeType
  if (oldNode.isLeaf !== newNode.isLeaf) changes.isLeaf = newNode.isLeaf
  if (oldNode.sort !== newNode.sort) changes.sort = newNode.sort
  return changes
}

// ========================================================
// 五、默认导出
// ========================================================

export const treeOps = {
  addNode,
  removeSubtree,
  moveSubtree,
  updateNode,
  mergeRoots,
  diff,
  validate,
  flatten
}
