<template>
  <div class="cert-directory-tree">
    <!-- 使用通用 CertBizTree 作为基础，添加文件层级的懒加载和规则状态 -->
    <CertBizTree
      ref="bizTreeRef"
      :title="title"
      :filterable="filterable"
      :search-placeholder="searchPlaceholder"
      :show-rule-status="showRuleStatus"
      :show-convert-badge="showConvertBadge"
      :show-rule-status-filter="showRuleStatusFilter"
      :node-types="nodeTypes"
      :default-expand-level="defaultExpandLevel"
      @node-click="onBizNodeClick"
    >
      <template #header-actions>
        <slot name="header-actions" />
      </template>
    </CertBizTree>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useFileTree, type TreeNode } from '../composables/useFileTree'
import CertBizTree from './CertBizTree.vue'

const props = withDefaults(defineProps<{
  title?: string
  searchPlaceholder?: string
  filterable?: boolean
  showConvertBadge?: boolean
  /** 显示文件规则状态标签（已配置/未配置/配置失败）并启用规则筛选 */
  showRuleStatus?: boolean
  showRuleStatusFilter?: boolean
  defaultExpandLevel?: number
  /** 节点类型过滤 */
  nodeTypes?: Array<'organization' | 'standard' | 'stage' | 'folder' | 'file'>
}>(), {
  title: '文件目录',
  searchPlaceholder: '搜索文档...',
  filterable: true,
  defaultExpandLevel: 3,
})

const emit = defineEmits<{
  select: [node: TreeNode]
  'node-click': [node: TreeNode]
}>()

const bizTreeRef = ref<InstanceType<typeof CertBizTree>>()
const { loadStageFiles, fileTreeData } = useFileTree()
const treeRef = ref<any>(null)

/** fileCode → 规则状态 */
const ruleStatusMap = ref<Record<string, 'configured' | 'failed'>>({})

function resolveRuleStatus(fileCode: string): 'configured' | 'none' | 'failed' {
  return ruleStatusMap.value[fileCode] || 'none'
}

async function loadRuleStatuses() {
  if (!props.showRuleStatus) return
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

function applyRuleStatusToLoaded() {
  const walk = (nodes: TreeNode[]) => {
    for (const n of nodes || []) {
      if (n.Type === 'file') {
        n.RuleStatus = resolveRuleStatus(String(n.FileCode || n.Code))
      } else if (n.Children?.length) {
        walk(n.Children)
      }
    }
  }
  walk(fileTreeData.value)
}

async function onBizNodeClick(data: TreeNode) {
  // 如果点击的是 stage 节点且未展开，触发懒加载
  if (data.Type === 'stage' && !data._loaded) {
    try {
      await loadStageFiles(data)
      applyRuleStatusToLoaded()
    } catch (e: any) {
      ElMessage.error('加载阶段文件失败：' + (e?.message || ''))
    }
  }
  
  emit('node-click', data)
  if (data.Type === 'file') {
    emit('select', data)
  }
}

/** 重新加载规则状态并重绘 */
async function refresh() {
  await loadRuleStatuses()
  bizTreeRef.value?.refresh?.()
}

defineExpose({ refresh, loadRuleStatuses })

// 监听 treeRef 变化
watch(() => bizTreeRef, (val) => {
  if (val?.value) {
    treeRef.value = val.value
  }
}, { immediate: true })
</script>

<style scoped>
.cert-directory-tree {
  display: flex;
  flex-direction: column;
  height: 100%;
}
</style>
