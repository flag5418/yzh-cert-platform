<template>
  <div class="cert-biz-tree">
    <div class="cert-biz-tree__header">
      <span class="cert-biz-tree__title">{{ title }}</span>
      <slot name="header-actions" />
    </div>

    <div v-if="filterable" class="cert-biz-tree__search">
      <slot name="search-prefix" />
      <el-input
        v-model="filterText"
        :placeholder="searchPlaceholder"
        clearable
        size="small"
      >
        <template #prefix>
          <el-icon><Search /></el-icon>
        </template>
      </el-input>
      <slot name="search-suffix" />
    </div>

    <div v-if="showRuleStatusFilter && filterable" class="cert-biz-tree__rule-filter">
      <el-select
        v-model="ruleFilter"
        size="small"
        placeholder="规则状态"
        style="width: 110px"
      >
        <el-option label="全部" value="all" />
        <el-option label="已配置规则" value="configured" />
        <el-option label="未配置规则" value="none" />
        <el-option label="配置失败" value="failed" />
      </el-select>
    </div>

    <div v-loading="loading" class="cert-biz-tree__content">
      <el-tree
        v-if="filteredTreeData.length > 0"
        ref="treeRef"
        :data="filteredTreeData"
        :props="treeProps"
        :node-key="nodeKey"
        :highlight-current="highlightCurrent"
        :expand-on-click-node="false"
        :default-expanded-keys="defaultExpandedKeys"
        :filter-node-method="filterNode"
        @node-expand="onNodeExpand"
        @node-click="onNodeClick"
      >
        <template #default="{ data }">
          <div class="cert-biz-tree__node" :class="{ 'is-active': isActive(data) }">
            <el-icon class="cert-biz-tree__node-icon" :class="`is-${data.Type}`">
              <component :is="getNodeIcon(data.Type)" />
            </el-icon>
            <span class="cert-biz-tree__node-label">{{ data.Name }}</span>
            <slot name="node-extra" :node="data">
              <CertConvertBadge
                v-if="showConvertBadge && data.Type === 'file' && data.ConvertStatus"
                :status="data.ConvertStatus"
              />
              <span
                v-if="showRuleStatus && data.Type === 'file'"
                class="cert-biz-tree__rule-tag"
                :class="`is-${data.RuleStatus || 'none'}`"
              >{{ RULE_STATUS_TEXT[data.RuleStatus || 'none'] }}</span>
            </slot>
          </div>
        </template>
      </el-tree>

      <el-empty
        v-if="!loading && filteredTreeData.length === 0"
        :description="filterText ? '未匹配到目录' : '暂无目录数据'"
        :image-size="80"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { Search } from '@element-plus/icons-vue'
import {
  FolderOpened,
  Document,
  Calendar,
  OfficeBuilding,
} from '@element-plus/icons-vue'
import { useFileTree, type TreeNode } from '../composables/useFileTree'
import CertConvertBadge from './CertConvertBadge.vue'

const props = withDefaults(defineProps<{
  /** 树标题 */
  title?: string
  /** 搜索框占位文字 */
  searchPlaceholder?: string
  /** 是否显示搜索框 */
  filterable?: boolean
  /** 是否显示规则状态筛选下拉框（仅 showRuleStatus 时为 true） */
  showRuleStatusFilter?: boolean
  /** 是否显示文件规则状态标签 */
  showRuleStatus?: boolean
  /** 是否显示转换状态徽章 */
  showConvertBadge?: boolean
  /** 树节点唯一键 */
  nodeKey?: string
  /** 是否高亮当前选中节点 */
  highlightCurrent?: boolean
  /** 默认展开层级：1=机构, 2=标准, 3=阶段 */
  defaultExpandLevel?: number
  /** 节点类型过滤：只显示指定类型的节点 */
  nodeTypes?: Array<'organization' | 'standard' | 'stage' | 'folder' | 'file'>
}>(), {
  title: '组织 → 标准 → 阶段',
  searchPlaceholder: '搜索节点...',
  filterable: true,
  nodeKey: 'Code',
  highlightCurrent: true,
  defaultExpandLevel: 3,
})

const emit = defineEmits<{
  'node-click': [node: TreeNode]
  select: [node: TreeNode]
}>()

const { fileTreeData, loading, defaultExpandedKeys, loadTree } = useFileTree()
const treeRef = ref()
const filterText = ref('')
const ruleFilter = ref<'all' | 'configured' | 'none' | 'failed'>('all')
const selectedCode = ref<string | number | null>(null)

/** fileCode → 规则状态 */
const ruleStatusMap = ref<Record<string, 'configured' | 'failed'>>({})

const RULE_STATUS_TEXT: Record<string, string> = {
  none: '未配置',
  configured: '已配置',
  failed: '配置失败'
}

/**
 * 过滤后的树数据：
 * 1. 按 nodeTypes 过滤节点类型
 * 2. 应用搜索过滤（保留父节点逻辑）
 */
const filteredTreeData = computed(() => {
  let data = fileTreeData.value

  // 节点类型过滤
  if (props.nodeTypes?.length) {
    function filterByType(node: TreeNode): TreeNode | null {
      if (!props.nodeTypes!.includes(node.Type)) return null
      if (!node.Children?.length) return node
      const filtered = node.Children.map(filterByType).filter(Boolean) as TreeNode[]
      return { ...node, Children: filtered }
    }
    data = data.map(filterByType).filter(Boolean) as TreeNode[]
  }

  // 搜索过滤
  const q = filterText.value.trim().toLowerCase()
  if (!q) return data

  // 复杂搜索：父节点命中时保留整棵子树
  const hit = (n: TreeNode) => String(n?.Name || '').toLowerCase().includes(q)

  function filterTree(nodes: TreeNode[]): TreeNode[] {
    const result: TreeNode[] = []
    for (const node of nodes) {
      const nodeHit = hit(node)
      if (!node.Children?.length) {
        // 叶子节点：匹配则保留
        if (nodeHit) result.push(node)
        continue
      }
      const filteredChildren = filterTree(node.Children)
      if (nodeHit || filteredChildren.length > 0) {
        result.push({ ...node, Children: nodeHit ? node.Children : filteredChildren, Expanded: true })
      }
    }
    return result
  }

  return filterTree(data)
})

const treeProps = {
  children: 'Children',
  label: 'Name',
}

function getNodeIcon(type: string) {
  const iconMap: Record<string, any> = {
    organization: OfficeBuilding,
    standard: Document,
    stage: Calendar,
    folder: FolderOpened,
    file: Document,
  }
  return iconMap[type] || Document
}

function isActive(data: TreeNode) {
  return selectedCode.value === data.Code
}

async function onNodeExpand(_data: TreeNode) {
  // 由 useFileTree 处理懒加载，此处不需要额外逻辑
}

function onNodeClick(data: TreeNode) {
  selectedCode.value = data.Code
  emit('node-click', data)
  // 当点击阶段节点或允许非叶子节点点击时触发 select
  if (data.Type === 'stage' || !props.highlightCurrent) {
    emit('select', data)
  }
}

function filterNode(value: string, data: TreeNode) {
  const textOk = !value || (data.Name || '').toLowerCase().includes(value.toLowerCase())
  if (data.Type !== 'file') return true
  if (ruleFilter.value !== 'all') {
    return textOk && (data.RuleStatus || 'none') === ruleFilter.value
  }
  return textOk
}

watch(filterText, (val) => {
  treeRef.value?.filter(val)
})

watch(ruleFilter, () => {
  treeRef.value?.filter(filterText.value)
})

/** 重新加载规则状态并重绘 */
async function refresh() {
  await loadRuleStatuses()
  treeRef.value?.filter(filterText.value)
}

/** 拉取已配置规则并刷新规则状态标签 */
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

/** 把规则状态回写到树中已加载的文件节点 */
function applyRuleStatusToLoaded() {
  const walk = (nodes: TreeNode[]) => {
    for (const n of nodes || []) {
      if (n.Type === 'file') {
        n.RuleStatus = ruleStatusMap.value[n.FileCode || n.Code] || 'none'
      } else if (n.Children?.length) {
        walk(n.Children)
      }
    }
  }
  walk(fileTreeData.value)
}

defineExpose({ refresh, loadRuleStatuses })

onMounted(async () => {
  await loadTree()
  await loadRuleStatuses()
})
</script>

<style scoped>
.cert-biz-tree {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.cert-biz-tree__header {
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  font-weight: 600;
  font-size: 14px;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.cert-biz-tree__search {
  padding: 8px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  display: flex;
  gap: 8px;
}

.cert-biz-tree__rule-filter {
  padding: 8px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.cert-biz-tree__content {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
  min-height: 0;
}

.cert-biz-tree__content :deep(.el-tree) {
  height: 100%;
}

.cert-biz-tree__node {
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 1;
  min-width: 0;
}

.cert-biz-tree__node-icon {
  font-size: 16px;
  flex-shrink: 0;
}

.cert-biz-tree__node-icon.is-organization {
  color: #e6a23c;
}

.cert-biz-tree__node-icon.is-standard {
  color: #409eff;
}

.cert-biz-tree__node-icon.is-stage {
  color: #67c23a;
}

.cert-biz-tree__node-icon.is-folder {
  color: #909399;
}

.cert-biz-tree__node-icon.is-file {
  color: #909399;
}

.cert-biz-tree__node-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 13px;
}

.cert-biz-tree__node.is-active .cert-biz-tree__node-label {
  color: var(--el-color-primary);
  font-weight: 500;
}

.cert-biz-tree__rule-tag {
  flex-shrink: 0;
  font-size: 11px;
  line-height: 18px;
  padding: 0 6px;
  border-radius: 2px;
  border: 1px solid transparent;
}

.cert-biz-tree__rule-tag.is-none {
  color: #909399;
  background: #f4f4f5;
  border-color: #e9e9eb;
}

.cert-biz-tree__rule-tag.is-configured {
  color: #529b2e;
  background: #f0f9eb;
  border-color: #e1f3d8;
}

.cert-biz-tree__rule-tag.is-failed {
  color: #c45656;
  background: #fef0f0;
  border-color: #fde2e2;
}
</style>
