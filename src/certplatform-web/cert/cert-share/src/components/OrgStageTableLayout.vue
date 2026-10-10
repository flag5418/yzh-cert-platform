<template>
  <div class="org-stage-tt">
    <div class="org-stage-tt__main">
    <!-- 左树面板 -->
    <div class="org-stage-tt__tree-panel" :style="{ width: treeWidth + 'px' }">
      <OrgStandardStageTree
        ref="treeRef"
        :data="data"
        :title="title"
        :filterable="filterable"
        :search-placeholder="searchPlaceholder"
        :node-key="nodeKey"
        :highlight-current="highlightCurrent"
        :default-expand-level="defaultExpandLevel"
        :max-level="maxLevel"
        :leaf-types="leafTypes"
        :count-field="countField"
        :lazy-load="lazyLoad"
        :loading="loading"
        @node-click="onNodeClick"
        @select="onSelect"
      >
        <template #header-actions>
          <slot name="tree-header-actions" />
        </template>
        <template #search-prefix>
          <slot name="tree-search-prefix" />
        </template>
        <template #search-suffix>
          <slot name="tree-search-suffix" />
        </template>
        <template #node-extra="{ node }">
          <slot name="tree-node-extra" :node="node" />
        </template>
      </OrgStandardStageTree>
      <!-- 树底部插槽 -->
      <div v-if="$slots.treeFooter" class="org-stage-tt__tree-footer">
        <slot name="treeFooter" />
      </div>
    </div>

    <!-- 右表面板 -->
    <div class="org-stage-tt__table-panel">
      <slot name="default" />
    </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import OrgStandardStageTree, { type StdStageTreeNode } from './OrgStandardStageTree.vue'

withDefaults(defineProps<{
  /** 树数据 */
  data?: StdStageTreeNode[]
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
  /** 默认展开层级 */
  defaultExpandLevel?: number
  /** 最大渲染层级 */
  maxLevel?: number
  /** 可选中节点类型 */
  leafTypes?: string[]
  /** 数量徽标字段名：读取节点的该字段值并显示为徽标 */
  countField?: string
  /** 懒加载函数 */
  lazyLoad?: (node: StdStageTreeNode) => Promise<StdStageTreeNode[]>
  /** 加载态 */
  loading?: boolean
  /** 树面板宽度 */
  treeWidth?: number
}>(), {
  data: () => [],
  title: '组织 → 标准 → 阶段',
  searchPlaceholder: '搜索节点...',
  filterable: true,
  nodeKey: 'Code',
  highlightCurrent: true,
  defaultExpandLevel: 2,
  maxLevel: 3,
  leafTypes: () => ['stage'],
  countField: undefined,
  lazyLoad: undefined,
  loading: false,
  treeWidth: 280,
})

const emit = defineEmits<{
  'node-click': [node: StdStageTreeNode]
  select: [node: StdStageTreeNode]
}>()

const treeRef = ref<InstanceType<typeof OrgStandardStageTree>>()

function onNodeClick(node: StdStageTreeNode) {
  emit('node-click', node)
}

function onSelect(node: StdStageTreeNode) {
  emit('select', node)
}

defineExpose({
  treeRef,
  clearSelection: () => treeRef.value?.clearSelection(),
})
</script>

<style scoped>
.org-stage-tt {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
  box-sizing: border-box;
}

.org-stage-tt__main {
  display: flex;
  flex: 1;
  min-height: 0;
}

.org-stage-tt__tree-panel {
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--el-border-color-lighter);
  overflow: hidden;
}

.org-stage-tt__tree-footer {
  padding: var(--yzh-space-3, 12px);
  border-top: 1px solid var(--el-border-color-lighter);
  background: var(--yzh-color-bg-container, #fff);
  flex-shrink: 0;
}

.org-stage-tt__table-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  overflow: hidden;
}
</style>
