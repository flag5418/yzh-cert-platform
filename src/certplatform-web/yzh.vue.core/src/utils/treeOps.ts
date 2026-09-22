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
 * await api.delete(removed.map(n => n.Code))
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
 *   Code: 'T003', Name: '后端组', ParentCode: 'D001', Children: []
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
      if (n.Code === parentCode) {
        return { ...n, Children: [...(n.Children ?? []), node] }
      }
      if (n.Children && n.Children.length > 0) {
        return { ...n, Children: addToParent(n.Children) }
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
 * console.log('删除的节点:', removed.map(n => n.Code))
 */
export function removeSubtree<T>(
  roots: TreeNode<T>[],
  code: string
): { tree: TreeNode<T>[]; removed: TreeNode<T>[] } {
  const removed: TreeNode<T>[] = []

  const removeFrom = (nodes: TreeNode<T>[]): TreeNode<T>[] => {
    const result: TreeNode<T>[] = []
    for (const node of nodes) {
      if (node.Code === code) {
        // 收集被删除的节点及其所有子孙
        removed.push(node)
        collectDescendants(node).forEach(n => removed.push(n))
        // 不加入结果（即删除）
      } else {
        if (node.Children && node.Children.length > 0) {
          result.push({ ...node, Children: removeFrom(node.Children) })
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
    ParentCode: newParentCode
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
 *   Name: '研发部',
 *   Extra: { icon: 'Code' }
 * })
 */
export function updateNode<T>(
  roots: TreeNode<T>[],
  code: string,
  patch: Partial<TreeNode<T>>
): TreeNode<T>[] {
  const update = (nodes: TreeNode<T>[]): TreeNode<T>[] => {
    return nodes.map(n => {
      if (n.Code === code) {
        return { ...n, ...patch }
      }
      if (n.Children && n.Children.length > 0) {
        return { ...n, Children: update(n.Children) }
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
 * console.log('新增:', patch.added.map(n => n.Name))
 * console.log('删除:', patch.removed)
 */
export function diff<T>(oldTree: TreeNode<T>[], newTree: TreeNode<T>[]): TreePatch<T> {
  const oldNodes = flattenWithLevel(oldTree)
  const newNodes = flattenWithLevel(newTree)
  const oldMap = new Map(oldNodes.map(n => [n.node.Code, n]))
  const newMap = new Map(newNodes.map(n => [n.node.Code, n]))

  const added: TreeNode<T>[] = []
  const removed: string[] = []
  const updated: Array<{ code: string; changes: Partial<TreeNode<T>> }> = []

  // 找出新增和更新的
  for (const [, newNode] of newMap) {
    const old = oldMap.get(newNode.node.Code)
    if (!old) {
      added.push(newNode.node)
    } else if (!isNodeEqual(old.node, newNode.node)) {
      const changes = getNodeChanges(old.node, newNode.node)
      updated.push({ code: newNode.node.Code, changes })
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
  const nodeCodes = new Set(nodes.map(n => n.Code))

  // 检查重复 Code
  const codeCount = new Map<string, number>()
  for (const node of nodes) {
    codeCount.set(node.Code, (codeCount.get(node.Code) ?? 0) + 1)
  }
  for (const [code, count] of codeCount) {
    if (count > 1) {
      errors.push(`Code "${code}" 重复 ${count} 次`)
    }
  }

  // 检查孤儿节点
  for (const node of nodes) {
    if (node.ParentCode != null && node.ParentCode !== '' && !nodeCodes.has(node.ParentCode)) {
      errors.push(`节点 "${node.Name}" 的 ParentCode "${node.ParentCode}" 不存在`)
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
      if (node.Children && node.Children.length > 0) {
        collect(node.Children)
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
    if (node.Code === code) return node
    if (node.Children && node.Children.length > 0) {
      const found = findNode(node.Children, code)
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
    for (const child of n.Children ?? []) collect(child)
  }
  collect(node)
  return result
}

/** 判断 target 是否是 node 的子孙 */
function isDescendant<T>(node: TreeNode<T>, targetCode: string): boolean {
  for (const child of node.Children ?? []) {
    if (child.Code === targetCode) return true
    if (isDescendant(child, targetCode)) return true
  }
  return false
}

/** 获取节点的深度（子树最大深度） */
function getNodeDepth<T>(node: TreeNode<T>): number {
  if (!node.Children || node.Children.length === 0) return 1
  let max = 0
  for (const child of node.Children) {
    max = Math.max(max, getNodeDepth(child))
  }
  return max + 1
}

/** 获取节点在树中的层级 */
function getLevel<T>(roots: TreeNode<T>[], targetCode: string): number {
  const traverse = (nodes: TreeNode<T>[], level: number): number => {
    for (const node of nodes) {
      if (node.Code === targetCode) return level
      if (node.Children && node.Children.length > 0) {
        const found = traverse(node.Children, level + 1)
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
    if (stack.has(node.Code)) return true
    if (visited.has(node.Code)) return false

    visited.add(node.Code)
    stack.add(node.Code)

    for (const child of node.Children ?? []) {
      if (dfs(child)) return true
    }

    stack.delete(node.Code)
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
      if (node.Children && node.Children.length > 0) {
        traverse(node.Children, level + 1)
      }
    }
  }
  traverse(roots, 0)
  return result
}

/** 比较节点是否相等 */
function isNodeEqual<T>(a: TreeNode<T>, b: TreeNode<T>): boolean {
  return a.Code === b.Code &&
    a.Name === b.Name &&
    a.ParentCode === b.ParentCode &&
    a.NodeType === b.NodeType &&
    a.IsLeaf === b.IsLeaf &&
    a.Sort === b.Sort
}

/** 获取节点变更 */
function getNodeChanges<T>(oldNode: TreeNode<T>, newNode: TreeNode<T>): Partial<TreeNode<T>> {
  const changes: Partial<TreeNode<T>> = {}
  if (oldNode.Name !== newNode.Name) changes.Name = newNode.Name
  if (oldNode.ParentCode !== newNode.ParentCode) changes.ParentCode = newNode.ParentCode
  if (oldNode.NodeType !== newNode.NodeType) changes.NodeType = newNode.NodeType
  if (oldNode.IsLeaf !== newNode.IsLeaf) changes.IsLeaf = newNode.IsLeaf
  if (oldNode.Sort !== newNode.Sort) changes.Sort = newNode.Sort
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
