/**
 * useOrgStageTree — 通用「机构 → 标准 → 阶段」树加载器
 *
 * 统一 nc-config / report-rule 等页面的左树数据源：
 * - 加载 StandardDirectory 组织树（getOrganizationTree）
 * - 转换为 YZH 内核 TreeNode 格式（含 Extra.PhaseDefinitionCode）
 * - 只保留 organization / standard / stage 三层
 *
 * ★ 2026-10-10 从 nc-config/logic.ts 与 report-rule/logic.ts 提取公共逻辑。
 *   report-rule 原 toCoreNodes 遗漏 PhaseDefinitionCode → 右表关联失效（已修复）。
 */
import { getOrganizationTree } from './useDirectoryApi'
import { useFileTree, type TreeNode as FileTreeNode } from './useFileTree'
import type { TreeNode } from '@yzh-core'

/** 节点类型 → 图标（Element Plus 图标名） */
const NODE_ICON: Record<string, string> = {
  organization: 'OfficeBuilding',
  standard: 'Document',
  stage: 'Calendar',
}

/**
 * 目录域树（组织 → 标准 → 阶段 → 目录 → 文件）→ 内核 TreeNode。
 *
 * 只保留 组织/标准/阶段 三层；阶段三编码进 Extra 供 buildFilters 读取。
 * ★ PhaseDefinitionCode 必须包含：report-rule 页面右表关联依赖此字段。
 */
function toCoreNodes(nodes: FileTreeNode[]): TreeNode[] {
  const result: TreeNode[] = []
  for (const n of nodes) {
    if (!(n.Type in NODE_ICON)) continue
    const children = toCoreNodes(n.Children ?? [])
    result.push({
      Code: String(n.Code),
      Name: n.Name,
      NodeType: n.Type,
      IsLeaf: children.length === 0,
      Extra: {
        Icon: NODE_ICON[n.Type],
        OrgCode: n.OrgCode ?? '',
        StdCode: n.StdCode ?? '',
        PhaseCode: n.PhaseCode ?? '',
        PhaseDefinitionCode: n.PhaseDefinitionCode ?? '',
      },
      Children: children,
    })
  }
  return result
}

/**
 * 加载「机构 → 标准 → 阶段」树，返回内核 TreeNode 数组。
 *
 * 使用方式（在 TreeTableLogic 子类中）：
 *   override async loadTreeRoot(): Promise<void> {
 *     this.treeSide.treeLoading.value = true
 *     try {
 *       this.treeSide.setNodes(await loadOrgStageTree())
 *     } finally {
 *       this.treeSide.treeLoading.value = false
 *     }
 *   }
 */
export async function loadOrgStageTree(): Promise<TreeNode[]> {
  const fileTree = useFileTree()
  const orgTree = await getOrganizationTree()
  return toCoreNodes(fileTree.transformOrgTree(orgTree))
}
