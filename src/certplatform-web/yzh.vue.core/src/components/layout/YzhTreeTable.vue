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
          :show-checkbox="treeCheckable"
          :check-strictly="treeCheckStrictly"
          :lazy="treeLazy"
          :load-data="treeLoadData"
          :default-expand-all="treeDefaultExpandAll"
          :node-actions="nodeActions"
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
import { computed, ref } from 'vue'
import { ElInput } from 'element-plus'
import YzhTree from './YzhTree.vue'
import type { TreeNode } from '@share/types/tree'

// ========================================================
// Props & Emits
// ========================================================

interface Props {
  /** 树数据 */
  treeData?: TreeNode[]
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
  treeLoadData?: (node: any, resolve: (data: TreeNode[]) => void) => void
  /** 树默认展开 */
  treeDefaultExpandAll?: boolean
  /** 节点自定义操作按钮：{ 方法名: 显示文字 }（后端自动注入） */
  nodeActions?: Record<string, string>
  /** 动态操作文本函数（根据节点状态返回显示文字） */
  getActionLabel?: (action: string, node: TreeNode) => string
}

const props = withDefaults(defineProps<Props>(), {
  treeData: () => [],
  treeWidth: 260,
  treeToolbar: true,
  treeSearchable: true,
  treeCheckable: false,
  treeCheckStrictly: false,
  treeLazy: false,
  treeDefaultExpandAll: false,
  nodeActions: () => ({}),
  getActionLabel: undefined
})

const emit = defineEmits<{
  (e: 'tree-node-click', node: TreeNode): void
  (e: 'tree-check-change', checkedNodes: TreeNode[]): void
  (e: 'tree-node-action', action: string, node: TreeNode): void
}>()

// ========================================================
// 内部状态
// ========================================================

const treeRef = ref<InstanceType<typeof YzhTree>>()
const treeSearchKeyword = ref('')

// ========================================================
// 计算属性
// ========================================================

const filteredTreeData = computed(() => {
  if (!treeSearchKeyword.value) return props.treeData
  return filterTreeData(props.treeData, treeSearchKeyword.value)
})

// ========================================================
// 树事件处理
// ========================================================

function handleTreeNodeClick(node: TreeNode) {
  emit('tree-node-click', node)
}

function handleTreeCheckChange(checkedNodes: TreeNode[]) {
  emit('tree-check-change', checkedNodes)
}

function handleTreeNodeAction(action: string, node: TreeNode) {
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

function filterTreeData(nodes: TreeNode[], keyword: string): TreeNode[] {
  const lower = keyword.toLowerCase()
  const result: TreeNode[] = []

  for (const node of nodes) {
    const matched = (node.Name || node.name || '').toLowerCase().includes(lower)
    const filteredChildren = filterTreeData(node.children, keyword)

    if (matched || filteredChildren.length > 0) {
      result.push({ ...node, children: filteredChildren })
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
  appendNode: (parentCode: string | null, newNode: TreeNode) => treeRef.value?.appendNode(parentCode, newNode)
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
