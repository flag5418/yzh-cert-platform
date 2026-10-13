import { ref, type Ref } from 'vue'
import type { TreeNode } from './useFileTree'

/** 文件规则状态：none=未配置 / configured=已配置 / failed=配置失败 */
export type FileRuleStatus = 'none' | 'configured' | 'failed'

/**
 * 文件树「提取规则状态」加载与回写（Q4 树收敛后从 CertBizTree / CertDirectoryTree 提炼）。
 *
 * <para>职责：拉取已配置规则清单 → 回写到树上的 file 节点（`RuleStatus` 字段）。
 * 消费方（doc-extraction-rule 页面）用 `ruleStatusMap` 读状态、用 `applyRuleStatusToLoaded`
 * 在懒加载新文件后补写。</para>
 *
 * ⚠️ 回写是**就地 mutate**（不换 `fileTreeData` 数组引用）——这是刻意的：
 * 换引用会触发 `OrgStandardStageTree` 的 data watch 清高亮、并导致整树重渲染。
 * 响应性依赖 ref 深层代理：过滤态 computed 读过 `RuleStatus` 属性即自动收集依赖。
 */
export function useFileRuleStatus(fileTreeData: Ref<TreeNode[]>) {
  /** fileCode → 规则状态 */
  const ruleStatusMap = ref<Record<string, 'configured' | 'failed'>>({})

  /** 拉取已配置规则并回写到树中已加载的文件节点 */
  async function loadRuleStatuses() {
    try {
      const { getConfiguredRules } = await import('../api/workflow/doc-extraction-rule')
      const res = await getConfiguredRules()
      const map: Record<string, 'configured' | 'failed'> = {}
      for (const r of res?.data || []) {
        const code = r.standardFileCode || (r as any).fileCode
        if (!code) continue
        map[code] = r.isValid === false ? 'failed' : 'configured'
      }
      ruleStatusMap.value = map
    } catch {
      // 规则状态获取失败不影响目录浏览
    }
    applyRuleStatusToLoaded()
  }

  /** 把规则状态回写到树中已加载的文件节点（就地 mutate，见顶部注释） */
  function applyRuleStatusToLoaded() {
    const walk = (nodes: TreeNode[]) => {
      for (const n of nodes || []) {
        if (n.Type === 'file') {
          n.RuleStatus = ruleStatusMap.value[String(n.FileCode || n.Code)] || 'none'
        } else if (n.Children?.length) {
          walk(n.Children)
        }
      }
    }
    walk(fileTreeData.value)
  }

  return { ruleStatusMap, loadRuleStatuses, applyRuleStatusToLoaded }
}
