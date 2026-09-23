<template>
  <div class="yzh-tree-table">
    <!-- 主内容区 -->
    <div class="yzh-tree-table__main">
      <!-- 左侧：树面板 -->
      <div
        class="yzh-tree-table__tree-panel"
        :style="{ width: treeWidth + 'px' }"
      >
        <!-- 树工具栏 -->
        <div v-if="treeToolbar" class="yzh-tree-table__tree-toolbar">
          <el-input
            v-if="treeSearchable"
            v-model="treeSearchKeyword"
            placeholder="搜索节点"
            clearable
            prefix-icon="Search"
          />
        </div>

        <!-- 树内容 -->
        <YzhTree
          ref="treeRef"
          :data="filteredTreeData"
          :node-key="nodeKey"
          :label-field="labelField"
          :children-field="childrenField"
          :is-leaf-field="isLeafField"
          :extra-field="extraField"
          :show-checkbox="treeCheckable"
          :check-strictly="treeCheckStrictly"
          :lazy="treeLazy"
          :load-data="treeLoadData"
          :default-expand-all="treeDefaultExpandAll"
          :node-actions="nodeActions"
          :legacy-node-actions="legacyNodeActions"
          :get-action-label="getActionLabel"
          @node-click="handleTreeNodeClick"
          @check-change="handleTreeCheckChange"
          @node-action="handleTreeNodeAction"
        />

        <!-- 树底部工具栏（slot：可由业务页面自定义按钮） -->
        <div v-if="$slots.treeFooter" class="yzh-tree-table__tree-footer">
          <slot name="treeFooter" />
        </div>
      </div>

      <!-- 右侧：表格面板 -->
      <div class="yzh-tree-table__table-panel">
        <slot name="default" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * YzhTreeTableLayout - 左树右表布局容器（布局壳，零领域依赖）
 *
 * 设计（C-D1..D4）：
 * - 零实体依赖：不 import 任何 @share / 业务类型，使用组件自身结构化类型
 * - 字段参数化：labelField/childrenField 等透传 YzhTree，过滤逻辑同字段感知
 * - nodeActions 透传为 YzhAction[] / resolver
 * - 不对 node.Code / data.Code 等做任何硬编码
 */
import { computed, ref } from 'vue'
import { ElInput } from 'element-plus'
import YzhTree, { type YzhTreeNode } from './YzhTree.vue'
import type { YzhAction } from '../table/types'

// ========================================================
// Props & Emits
// ========================================================

interface Props {
  /** 树数据 */
  treeData?: YzhTreeNode[]
  /** 节点唯一键字段名（透传 YzhTree，默认 Code） */
  nodeKey?: string
  /** 显示文字字段名（默认 Name） */
  labelField?: string
  /** 子节点集合字段名（默认 Children） */
  childrenField?: string
  /** 叶子标志字段名（默认 IsLeaf） */
  isLeafField?: string
  /** 扩展字段名（默认 Extra） */
  extraField?: string
  /** 树面板宽度 */
  treeWidth?: number
  /** 树工具栏 */
  treeToolbar?: boolean
  /** 树可搜索 */
  treeSearchable?: boolean
  /** 树显示复选框 */
  treeCheckable?: boolean
  /** 树严格模式 */
  treeCheckStrictly?: boolean
  /** 树懒加载 */
  treeLazy?: boolean
  /** 树懒加载函数 */
  treeLoadData?: (node: any, resolve: (data: YzhTreeNode[]) => void) => void
  /** 树默认展开 */
  treeDefaultExpandAll?: boolean
  /** 节点操作按钮：YzhAction[] 或 (node) => YzhAction[] */
  nodeActions?: YzhAction[] | ((node: YzhTreeNode) => YzhAction[])
  /** 兼容旧属性：{ 方法名: 显示文字 } */
  legacyNodeActions?: Record<string, string>
  /** 兼容旧属性：动态操作文本函数 */
  getActionLabel?: (action: string, node: YzhTreeNode) => string
}

const props = withDefaults(defineProps<Props>(), {
  treeData: () => [],
  nodeKey: 'Code',
  labelField: 'Name',
  childrenField: 'Children',
  isLeafField: 'IsLeaf',
  extraField: 'Extra',
  treeWidth: 260,
  treeToolbar: true,
  treeSearchable: true,
  treeCheckable: false,
  treeCheckStrictly: false,
  treeLazy: false,
  treeDefaultExpandAll: false,
  nodeActions: () => [],
  legacyNodeActions: () => ({}),
  getActionLabel: undefined
})

const emit = defineEmits<{
  (e: 'tree-node-click', node: YzhTreeNode): void
  (e: 'tree-check-change', checkedNodes: YzhTreeNode[]): void
  (e: 'tree-node-action', action: string, node: YzhTreeNode): void
}>()

// ========================================================
// 内部状态
// ========================================================

const treeRef = ref<InstanceType<typeof YzhTree>>()
const treeSearchKeyword = ref('')

// ========================================================
// 计算属性（字段参数化感知的搜索过滤）
// ========================================================

const filteredTreeData = computed(() => {
  if (!treeSearchKeyword.value) return props.treeData
  return filterTreeData(props.treeData, treeSearchKeyword.value)
})

// ========================================================
// 树事件处理
// ========================================================

function handleTreeNodeClick(node: YzhTreeNode) {
  emit('tree-node-click', node)
}

function handleTreeCheckChange(checkedNodes: YzhTreeNode[]) {
  emit('tree-check-change', checkedNodes)
}

function handleTreeNodeAction(action: string, node: YzhTreeNode) {
  emit('tree-node-action', action, node)
}

function handleExpandAll() {
  treeRef.value?.expandAll()
}

function handleCollapseAll() {
  treeRef.value?.collapseAll()
}

// ========================================================
// 辅助函数
// ========================================================

function filterTreeData(nodes: YzhTreeNode[], keyword: string): YzhTreeNode[] {
  const lower = keyword.toLowerCase()
  const result: YzhTreeNode[] = []

  for (const node of nodes) {
    const label = String(node[props.labelField] ?? '')
    const matched = label.toLowerCase().includes(lower)
    const children = (node[props.childrenField] as YzhTreeNode[]) ?? []
    const filteredChildren = filterTreeData(children, keyword)

    if (matched || filteredChildren.length > 0) {
      result.push({ ...node, [props.childrenField]: filteredChildren })
    }
  }

  return result
}

// ========================================================
// 公开方法
// ========================================================

defineExpose({
  treeRef,
  getCheckedNodes: () => treeRef.value?.getCheckedNodes() ?? [],
  expandAll: handleExpandAll,
  collapseAll: handleCollapseAll,
  appendNode: (parentCode: string | null, newNode: YzhTreeNode) => treeRef.value?.appendNode(parentCode, newNode),
  removeNode: (parentCode: string | null, code: string) => treeRef.value?.removeNode(parentCode, code),
})
</script>

<style scoped>
.yzh-tree-table {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

.yzh-tree-table__main {
  display: flex;
  flex: 1;
  min-height: 0;
}

.yzh-tree-table__tree-panel {
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--el-border-color-lighter);
  overflow: hidden;
}

.yzh-tree-table__tree-toolbar {
  padding: 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fff;
  flex-shrink: 0;
}

.yzh-tree-table__tree-footer {
  padding: 12px;
  border-top: 1px solid var(--el-border-color-lighter);
  background: #fff;
  flex-shrink: 0;
}

.yzh-tree-table__table-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  overflow: hidden;
}
</style>
