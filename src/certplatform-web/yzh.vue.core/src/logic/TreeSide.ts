/**
 * TreeSide - 树能力混入（TT-2）
 *
 * 承载左树的全部状态与操作：
 * - 树数据 / 加载状态 / 选中节点
 * - 节点索引 nodeIndex（O(1) 查找/替换/删除，TT-11）
 * - 增量变更：appendChild / removeNode / replaceNode
 *
 * TreeTableCore 与 AssociationTreeCore 通过组合 + 属性委托复用本能力。
 */

import { ref } from 'vue'
import type { TreeNode } from '../types/tree'

interface IndexEntry {
  node: TreeNode
  parent: TreeNode | null
}

export class TreeSide {
  /** 树数据（PascalCase，与后端 DTO 保持一致） */
  treeData = ref<TreeNode[]>([])

  /** 树加载状态 */
  treeLoading = ref(false)

  /** 当前选中节点 */
  selectedNode = ref<TreeNode | null>(null)

  /** 节点索引：Code → { node, parent }（O(1) 查找/替换/删除） */
  private index = new Map<string, IndexEntry>()

  /** 整树替换并重建索引 */
  setNodes(nodes: TreeNode[]): void {
    this.treeData.value = nodes
    this.rebuildIndex()
  }

  /** 重建索引（懒加载追加后调用） */
  rebuildIndex(): void {
    this.index.clear()
    const walk = (nodes: TreeNode[], parent: TreeNode | null) => {
      for (const n of nodes) {
        this.index.set(n.Code, { node: n, parent })
        if (n.Children?.length) walk(n.Children, n)
      }
    }
    walk(this.treeData.value, null)
  }

  /** 注册单个节点（append 后调用） */
  register(node: TreeNode, parent: TreeNode | null): void {
    this.index.set(node.Code, { node, parent })
  }

  /** O(1) 查找节点 */
  findNode(code: string): TreeNode | null {
    return this.index.get(code)?.node ?? null
  }

  /** O(1) 查找父节点 */
  findParent(code: string): TreeNode | null {
    return this.index.get(code)?.parent ?? null
  }

  /** 追加子节点（不触发 API，仅更新本地树 + 索引） */
  appendChild(parentCode: string | null, node: TreeNode): void {
    if (parentCode) {
      const parent = this.findNode(parentCode)
      if (parent) {
        parent.Children = parent.Children || []
        parent.Children.push(node)
        parent.IsLeaf = false
        this.register(node, parent)
        return
      }
    }
    this.treeData.value.push(node)
    this.register(node, null)
  }

  /** 删除节点（含整个子树），返回是否删除成功 */
  removeNode(code: string): boolean {
    const entry = this.index.get(code)
    if (!entry) return false
    const siblings =
      entry.parent
        ? (entry.parent.Children ??= [])
        : this.treeData.value
    const idx = siblings.findIndex((n) => n.Code === code)
    if (idx < 0) return false
    siblings.splice(idx, 1)
    // 删除整个子树的索引
    const walk = (node: TreeNode) => {
      this.index.delete(node.Code)
      for (const child of node.Children ?? []) walk(child)
    }
    walk(entry.node)
    return true
  }

  /** 替换节点（O(1) 定位） */
  replaceNode(code: string, newNode: TreeNode): boolean {
    const entry = this.index.get(code)
    if (!entry) return false
    const siblings =
      entry.parent
        ? (entry.parent.Children ??= [])
        : this.treeData.value
    const idx = siblings.findIndex((n) => n.Code === code)
    if (idx < 0) return false
    siblings.splice(idx, 1, newNode)
    this.index.delete(code)
    this.register(newNode, entry.parent)
    return true
  }

  /** 展开到指定节点（返回节点是否存在） */
  has(code: string): boolean {
    return this.index.has(code)
  }
}

export default TreeSide
