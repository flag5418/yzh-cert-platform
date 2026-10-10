<template>
  <div class="org-std-stage-tree">
    <div v-if="title" class="org-std-stage-tree__header">
      <span class="org-std-stage-tree__title">{{ title }}</span>
      <slot name="header-actions" />
    </div>

    <div v-if="filterable" class="org-std-stage-tree__search">
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

    <div v-loading="loading" class="org-std-stage-tree__content">
      <el-tree
        v-if="displayData.length > 0"
        ref="treeRef"
        :data="displayData"
        :props="treeProps"
        :node-key="nodeKey"
        :highlight-current="highlightCurrent"
        :current-node-key="currentKey"
        :expand-on-click-node="false"
        :default-expanded-keys="innerExpandedKeys"
        @node-expand="onNodeExpand"
        @node-click="onNodeClick"
      >
        <template #default="{ data }">
          <div class="org-std-stage-tree__node" :class="{ 'is-active': isActive(data) }">
            <el-icon class="org-std-stage-tree__node-icon" :class="`is-${nodeType(data)}`">
              <component :is="getNodeIcon(nodeType(data))" />
            </el-icon>
            <span class="org-std-stage-tree__node-label">{{ data.Name }}</span>
            <span v-if="countField && data[countField]" class="org-std-stage-tree__count-badge">{{ data[countField] }}</span>
            <slot name="node-extra" :node="data" />
          </div>
        </template>
      </el-tree>

      <YzhEmptyState
        v-if="!loading && displayData.length === 0"
        :title="filterText ? '未匹配到数据' : '暂无数据'"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { YzhEmptyState } from '@yzh-core'
import { ref, computed, watch, onMounted } from 'vue'
import { Search } from '@element-plus/icons-vue'
import {
  FolderOpened,
  Document,
  Calendar,
  OfficeBuilding,
} from '@element-plus/icons-vue'

/**
 * 通用树节点类型（兼容两种格式）
 *
 * ① 扁平格式（useFileTree.TreeNode）：Type + OrgCode/StdCode 顶层字段
 * ② 核心 TreeNode 格式（@yzh-core）：NodeType + Extra.{OrgCode,StdCode,...}
 *
 * 组件内部通过 helper 函数统一读取，调用方任选一种格式即可。
 */
export interface StdStageTreeNode {
  Code: string | number
  Name: string
  Type?: 'organization' | 'standard' | 'stage' | 'folder' | 'file' | string
  NodeType?: string
  IsLeaf?: boolean
  Children?: StdStageTreeNode[]
  OrgCode?: string
  StdCode?: string
  StandardCode?: string
  PhaseCode?: string
  PhaseDefinitionCode?: string
  DirectoryCode?: string | null
  FileCode?: string
  FolderCode?: string
  Extra?: Record<string, any>
  [key: string]: any
}

/**
 * 统一读取节点类型（兼容 Type / NodeType / Extra.kind 三种字段）。
 * - `useFileTree` 系列页面用 `Type` / `NodeType`
 * - 核心 TreeNode 体系（doc-fill-rule / TreeTableLogic）用 `Extra.kind`
 */
function nodeType(node: StdStageTreeNode): string {
  return node.Type || node.NodeType || node.Extra?.kind || ''
}

// ========================================================
// Props
// ========================================================

const props = withDefaults(defineProps<{
  /** 树标题 */
  title?: string
  /** 搜索框占位文字 */
  searchPlaceholder?: string
  /** 是否显示搜索框 */
  filterable?: boolean
  /** 树节点唯一键字段 */
  nodeKey?: string
  /** 是否高亮当前选中节点 */
  highlightCurrent?: boolean
  /** 默认展开层级：1=机构, 2=标准, 3=阶段, 4+ = 更深层级 */
  defaultExpandLevel?: number
  /** 树数据（已由外部加载完成） */
  data?: StdStageTreeNode[]
  /** 最大渲染层级：超过此层级节点不渲染 children（1=机构, 2=标准, 3=阶段, 4=文件） */
  maxLevel?: number
  /** 可选中节点类型：点击该类型节点时触发 @select */
  leafTypes?: string[]
  /** 数量徽标字段名：若设置，读取节点的该字段值并显示为徽标（如 RuleCount / SectionCount） */
  countField?: string
  /** 懒加载函数：展开节点时调用，返回 children 数组（用于动态加载阶段文件/规则等） */
  lazyLoad?: (node: StdStageTreeNode) => Promise<StdStageTreeNode[]>
  /** 初始加载态（外部控制） */
  loading?: boolean
  /** 当前选中节点键值（外部控制，用于树重建后恢复选中态） */
  currentKey?: string | number
}>(), {
  title: '组织 → 标准 → 阶段',
  searchPlaceholder: '搜索节点...',
  filterable: true,
  nodeKey: 'Code',
  highlightCurrent: true,
  defaultExpandLevel: 2,
  data: () => [],
  maxLevel: 4,
  leafTypes: () => ['stage'],
  countField: undefined,
  lazyLoad: undefined,
  loading: false,
  currentKey: undefined,
})

const emit = defineEmits<{
  'node-click': [node: StdStageTreeNode]
  select: [node: StdStageTreeNode]
  'lazy-load': [node: StdStageTreeNode]
}>()

// ========================================================
// Internal State
// ========================================================

const treeRef = ref()
const filterText = ref('')
const selectedCode = ref<string | number | null>(null)
const innerLoading = ref(false)

// ========================================================
// Search & Filter
// ========================================================

/**
 * 节点类型过滤 + 搜索过滤 + 层级截断
 */
const displayData = computed(() => {
  let data = props.data || []

  // 层级截断：超过 maxLevel 的节点，截断 children
  if (props.maxLevel && props.maxLevel > 0) {
    data = truncateTree(data, 1)
  }

  // 搜索过滤
  const q = filterText.value.trim().toLowerCase()
  if (!q) return data

  return searchTree(data)

  // --- helpers ---

  function truncateTree(nodes: StdStageTreeNode[], currentLevel: number): StdStageTreeNode[] {
    return nodes.map((n) => {
      if (currentLevel >= props.maxLevel) {
        // 已达到最大层级 → 不渲染子节点
        return { ...n, Children: undefined }
      }
      if (!n.Children?.length) return n
      return { ...n, Children: truncateTree(n.Children, currentLevel + 1) }
    })
  }

  function searchTree(nodes: StdStageTreeNode[]): StdStageTreeNode[] {
    const hit = (n: StdStageTreeNode) => String(n?.Name || '').toLowerCase().includes(q)
    const result: StdStageTreeNode[] = []
    for (const node of nodes) {
      const nodeHit = hit(node)
      if (!node.Children?.length) {
        if (nodeHit) result.push(node)
        continue
      }
      const filteredChildren = searchTree(node.Children)
      if (nodeHit || filteredChildren.length > 0) {
        result.push({ ...node, Children: nodeHit ? node.Children : filteredChildren })
      }
    }
    return result
  }
})

// ========================================================
// Expand State
// ========================================================

const innerExpandedKeys = computed<(string | number)[]>(() => {
  return collectExpandedKeys(props.data || [], 1)
})

function collectExpandedKeys(nodes: StdStageTreeNode[], currentLevel: number): (string | number)[] {
  const keys: (string | number)[] = []
  for (const n of nodes) {
    if (currentLevel < props.defaultExpandLevel) {
      keys.push(n.Code)
      if (n.Children?.length) {
        keys.push(...collectExpandedKeys(n.Children, currentLevel + 1))
      }
    }
  }
  return keys
}

// ========================================================
// Node Style & Icon
// ========================================================

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

function isActive(data: StdStageTreeNode) {
  return selectedCode.value === data.Code
}

// ========================================================
// Events
// ========================================================

function onNodeClick(data: StdStageTreeNode) {
  selectedCode.value = data.Code
  emit('node-click', data)
  if (props.leafTypes.includes(nodeType(data))) {
    emit('select', data)
  }
}

async function onNodeExpand(data: StdStageTreeNode) {
  // 如果有懒加载函数且节点未加载 → 调用懒加载
  if (props.lazyLoad && data.Children === undefined) {
    innerLoading.value = true
    try {
      const children = await props.lazyLoad(data)
      data.Children = children
      data._loaded = true
    } catch {
      data.Children = []
      data._loaded = true
    } finally {
      innerLoading.value = false
    }
  }
}

// ========================================================
// Watchers
// ========================================================

watch(() => props.data, () => {
  // 数据变化时清空选中
  selectedCode.value = null
})

// 外部 currentKey 变化时同步到内部 selectedCode（用于树重建后恢复选中态）
watch(() => props.currentKey, (val) => {
  if (val != null) selectedCode.value = val
})

// ========================================================
// Methods (exposed)
// ========================================================

defineExpose({
  clearSelection: () => { selectedCode.value = null },
})

onMounted(() => {
  // 数据已在外部加载完成
})
</script>

<style scoped>
.org-std-stage-tree {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--yzh-color-bg, #fff);
  border-radius: var(--yzh-border-radius, 8px);
  border: 1px solid var(--el-border-color-lighter, #ebeef5);
}

.org-std-stage-tree__header {
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  border-bottom: 1px solid var(--el-border-color-lighter);
  font-weight: var(--yzh-font-weight-semibold, 600);
  font-size: var(--yzh-font-size-md, 14px);
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.org-std-stage-tree__search {
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-bottom: 1px solid var(--el-border-color-lighter);
  display: flex;
  gap: var(--yzh-space-2, 8px);
}

.org-std-stage-tree__content {
  flex: 1;
  overflow-y: auto;
  padding: var(--yzh-space-2, 8px);
  min-height: 0;
}

.org-std-stage-tree__content :deep(.el-tree) {
  height: 100%;
}

.org-std-stage-tree__node {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1, 6px);
  flex: 1;
  min-width: 0;
}

.org-std-stage-tree__node-icon {
  font-size: var(--yzh-font-size-lg, 16px);
  flex-shrink: 0;
}

.org-std-stage-tree__node-icon.is-organization {
  color: var(--yzh-color-warning, #e6a23c);
}

.org-std-stage-tree__node-icon.is-standard {
  color: var(--yzh-color-primary, #409eff);
}

.org-std-stage-tree__node-icon.is-stage {
  color: var(--yzh-color-success, #67c23a);
}

.org-std-stage-tree__node-icon.is-folder {
  color: var(--yzh-color-text-tertiary, #909399);
}

.org-std-stage-tree__node-icon.is-file {
  color: var(--yzh-color-text-tertiary, #909399);
}

.org-std-stage-tree__node-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: var(--yzh-font-size-sm, 13px);
}

.org-std-stage-tree__node.is-active .org-std-stage-tree__node-label {
  color: var(--el-color-primary);
  font-weight: var(--yzh-font-weight-medium, 500);
}

.org-std-stage-tree__count-badge {
  flex-shrink: 0;
  font-size: var(--yzh-font-size-xs, 11px);
  line-height: 18px;
  padding: 0 var(--yzh-space-1, 4px);
  border-radius: 9999px;
  background: var(--el-color-primary-light-8, #d9ecff);
  color: var(--el-color-primary, #409eff);
  font-weight: var(--yzh-font-weight-medium, 500);
  min-width: 18px;
  text-align: center;
}
</style>
