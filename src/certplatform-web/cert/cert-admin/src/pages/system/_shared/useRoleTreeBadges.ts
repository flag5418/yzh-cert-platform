/**
 * useRoleTreeBadges - 角色树徽标（badge）的「局部更新」
 *
 * ## 为什么需要它
 *
 * 早期实现是在每次勾选/取消后对整棵角色树做
 * `JSON.parse(JSON.stringify(...))` 深拷贝，再把克隆出来的新数组整体赋给
 * `YzhTree` 的 `:data`，以此「强制」刷新徽标。代价是 el-tree 拿到新数组引用后
 * 会重置内部 store：
 *
 * - 已展开的节点全部折叠；
 * - 懒加载出来的子角色被丢弃，下次展开要重新请求 `tree/children`；
 * - 整棵树重新渲染（12+ 节点，且随角色数量增长）。
 *
 * 而徽标本身只依赖「该角色的关联数量」，一次勾选**只会影响当前角色一个节点**。
 * 所以正确做法是原地改那一个节点的 `extra.badge`：数组由 `ref` 持有，
 * 节点是响应式代理，改字段即可精确触发该节点重渲染，el-tree 不会重置。
 *
 * ## 用法
 *
 * ```ts
 * const {
 *   treeNodes: roleTreeWithBadges,   // 直接作为 :data 传给 YzhTree
 *   init: initTreeBadges,            // 挂载时调用一次
 *   updateBadge: updateRoleBadge,    // 勾选/取消后调用（局部刷新）
 * } = useRoleTreeBadges((code) => logic.getCountForRole(code))
 * ```
 *
 * ⚠️ 除初始化外，不要再整体替换 `treeNodes.value` —— 那会让 el-tree 重置，
 *    等于退回全量刷新。
 */

import { ref, type Ref } from 'vue'

/** 树节点（只用到这些字段，其余原样透传） */
interface BadgeTreeNode {
  Code: string
  children?: BadgeTreeNode[]
  extra?: Record<string, any>
  Extra?: Record<string, any>
  [key: string]: any
}

export interface UseRoleTreeBadgesReturn {
  /** 传给 YzhTree 的 :data（仅初始化时整体赋值一次） */
  treeNodes: Ref<BadgeTreeNode[]>
  /** 初始化：深拷贝角色树并注入徽标（仅在挂载时调用一次） */
  init: (source: BadgeTreeNode[]) => void
  /** 局部更新单个角色的徽标（勾选/取消后调用） */
  updateBadge: (roleCode?: string | null) => void
}

export function useRoleTreeBadges(
  getCount: (roleCode: string) => number,
): UseRoleTreeBadgesReturn {
  const treeNodes = ref<BadgeTreeNode[]>([])

  /**
   * 写入/清除单个节点的徽标
   *
   * `Extra` 与 `extra` 同时写：YzhTree 模板读取的是小写 `extra`，
   * 而后端返回的数据用大写 `Extra`，两侧都写以兼容不同访问方式。
   */
  function applyBadge(node: BadgeTreeNode): void {
    const count = getCount(node.Code)
    const extra = { ...(node.extra ?? node.Extra ?? {}) }
    if (count > 0) {
      extra.badge = String(count)
    } else {
      delete extra.badge
    }
    node.extra = extra
    node.Extra = extra
  }

  function findNode(nodes: BadgeTreeNode[], code: string): BadgeTreeNode | null {
    for (const node of nodes) {
      if (String(node.Code) === String(code)) return node
      if (node.children?.length) {
        const hit = findNode(node.children, code)
        if (hit) return hit
      }
    }
    return null
  }

  function init(source: BadgeTreeNode[]): void {
    if (!source || source.length === 0) {
      treeNodes.value = []
      return
    }

    // 深拷贝，避免污染 logic.roleTreeData；仅在此处整体替换数组
    const cloned: BadgeTreeNode[] = JSON.parse(JSON.stringify(source))
    const walk = (nodes: BadgeTreeNode[]) => {
      for (const node of nodes) {
        applyBadge(node)
        if (node.children?.length) walk(node.children)
      }
    }
    walk(cloned)

    treeNodes.value = cloned
  }

  function updateBadge(roleCode?: string | null): void {
    if (!roleCode) return
    // 取到的是响应式代理，改字段即可触发该节点重渲染
    const node = findNode(treeNodes.value, roleCode)
    // 找不到（如懒加载出来的子角色不在 :data 里）时静默返回，不退回全量刷新
    if (node) applyBadge(node)
  }

  return { treeNodes, init, updateBadge }
}
