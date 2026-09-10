<template>
  <div class="yzh-tree">
    <!-- 搜索框 -->
    <div v-if="searchable" class="yzh-tree__search">
      <el-input
        v-model="searchKeyword"
        placeholder="搜索节点"
        clearable
        prefix-icon="Search"
        size="small"
      />
    </div>

    <!-- 树组件 -->
    <el-tree
      ref="treeRef"
      :data="filteredData"
      :props="treeProps"
      :show-checkbox="showCheckbox"
      :check-strictly="checkStrictly"
      :lazy="lazy"
      :load="loadData"
      :default-expand-all="defaultExpandAll"
      :expand-on-click-node="expandOnClickNode"
      :highlight-current="highlightCurrent"
      :node-key="nodeKey"
      :current-node-key="currentKey"
      :filter-node-method="filterNode"
      empty-text="暂无数据"
      class="yzh-tree__inner"
      @node-click="handleNodeClick"
      @check-change="handleCheckChange"
      @node-expand="handleNodeExpand"
      @node-collapse="handleNodeCollapse"
    >
      <template #default="{ data }">
        <div class="yzh-tree__node" @mouseenter="hoveredNode = data.code" @mouseleave="hoveredNode = null">
          <!-- 图标 -->
          <el-icon v-if="data.extra?.icon && !isEmoji(data.extra.icon)" class="yzh-tree__icon">
            <component :is="data.extra.icon" />
          </el-icon>
          <span v-else-if="data.extra?.icon" class="yzh-tree__icon">{{ data.extra.icon }}</span>
          <el-icon v-else-if="data.isLeaf" class="yzh-tree__icon yzh-tree__icon--leaf">
            <Document />
          </el-icon>
          <el-icon v-else class="yzh-tree__icon yzh-tree__icon--folder">
            <Folder />
          </el-icon>

          <!-- 名称 -->
          <span
            class="yzh-tree__label"
            :class="{ 'is-highlight': highlightKeyword && isMatchNode(data) }"
          >
            {{ data.name }}
          </span>

          <!-- 徽标 -->
          <span v-if="data.extra?.badge" class="yzh-tree__badge">
            {{ data.extra.badge }}
          </span>

          <!-- 操作下拉菜单 -->
          <el-dropdown
            v-if="nodeActions && Object.keys(nodeActions).length"
            trigger="click"
            @command="(cmd: string) => handleNodeAction(cmd, data)"
            @click.stop
          >
            <el-button link size="small" class="yzh-tree__more-btn">
              ⋯
            </el-button>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item
                  v-for="(text, key) in nodeActions"
                  :key="key"
                  :command="key"
                  :class="getDropdownItemClass(key)"
                >
                  {{ getActionLabel ? getActionLabel(key, data) : text }}
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </template>
    </el-tree>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ElTree, ElInput } from 'element-plus'
import { Document, Folder } from '@element-plus/icons-vue'
import type { TreeNode } from '@share/types/tree'

// ========================================================
// 工具函数
// ========================================================

/** 判断字符串是否为 emoji（用于区分 element-plus 图标组件名和 emoji 字符串） */
function isEmoji(str: string): boolean {
  const emojiRegex = /[\u{1F300}-\u{1F9FF}]|[\u{2600}-\u{26FF}]|[\u{2700}-\u{27BF}]/u
  return emojiRegex.test(str)
}

// ========================================================
// Props
// ========================================================

interface Props {
  /** 树数据 */
  data: TreeNode[]
  /** 节点唯一键 */
  nodeKey?: string
  /** 显示复选框 */
  showCheckbox?: boolean
  /** 严格模式（父子不联动） */
  checkStrictly?: boolean
  /** 懒加载 */
  lazy?: boolean
  /** 懒加载函数 */
  loadData?: (node: any, resolve: (data: TreeNode[]) => void) => void
  /** 默认展开全部 */
  defaultExpandAll?: boolean
  /** 点击节点展开 */
  expandOnClickNode?: boolean
  /** 高亮当前 */
  highlightCurrent?: boolean
  /** 当前选中节点 key */
  currentKey?: string
  /** 可搜索 */
  searchable?: boolean
  /** 高亮关键字（搜索时） */
  highlightKeyword?: boolean
  /** 节点图标字段 */
  iconField?: string
  /** 节点自定义操作按钮：{ 方法名: 显示文字 }（后端自动注入） */
  nodeActions?: Record<string, string>
  /** 动态操作文本函数（根据节点状态返回显示文字） */
  getActionLabel?: (action: string, node: TreeNode) => string
}

const props = withDefaults(defineProps<Props>(), {
  nodeKey: 'code',
  showCheckbox: false,
  checkStrictly: false,
  lazy: false,
  defaultExpandAll: false,
  expandOnClickNode: true,
  highlightCurrent: true,
  searchable: false,
  highlightKeyword: true,
  nodeActions: () => ({}),
  getActionLabel: undefined
})

// ========================================================
// Emits
// ========================================================

const emit = defineEmits<{
  (e: 'node-click', node: TreeNode): void
  (e: 'check-change', checkedNodes: TreeNode[]): void
  (e: 'node-expand', node: TreeNode): void
  (e: 'node-collapse', node: TreeNode): void
  (e: 'node-action', action: string, node: TreeNode): void
}>()

// ========================================================
// 内部状态
// ========================================================

const treeRef = ref<InstanceType<typeof ElTree>>()
const searchKeyword = ref('')
const hoveredNode = ref<string | null>(null)

// ========================================================
// 树配置
// ========================================================

const treeProps = computed(() => ({
  label: 'name',
  children: 'children',
  isLeaf: (data: Record<string, any>) => (data as TreeNode).isLeaf ?? false,
  disabled: (data: Record<string, any>) => (data as TreeNode).extra?.disabled ?? false
}))

// ========================================================
// 搜索过滤
// ========================================================

const filteredData = computed(() => {
  if (!searchKeyword.value) return props.data
  return filterTree(props.data, searchKeyword.value)
})

function filterNode(_value: string, data: Record<string, any>): boolean {
  return isMatchNode(data as TreeNode)
}

function isMatchNode(node: TreeNode): boolean {
  if (!searchKeyword.value) return false
  return node.name.toLowerCase().includes(searchKeyword.value.toLowerCase())
}

function filterTree(nodes: TreeNode[], keyword: string): TreeNode[] {
  const lower = keyword.toLowerCase()
  const result: TreeNode[] = []

  for (const node of nodes) {
    const matched = node.name.toLowerCase().includes(lower)
    const filteredChildren = filterTree(node.children, keyword)

    if (matched || filteredChildren.length > 0) {
      result.push({
        ...node,
        children: filteredChildren
      })
    }
  }

  return result
}

// ========================================================
// 事件处理
// ========================================================

function handleNodeClick(node: TreeNode) {
  emit('node-click', node)
}

function handleCheckChange() {
  if (!treeRef.value) return
  const checkedNodes = treeRef.value.getCheckedNodes() as unknown as TreeNode[]
  emit('check-change', checkedNodes)
}

function handleNodeExpand(node: TreeNode) {
  emit('node-expand', node)
}

function handleNodeCollapse(node: TreeNode) {
  emit('node-collapse', node)
}

// ========================================================
// 节点操作按钮
// ========================================================

/** 根据操作 key 返回下拉菜单项样式类 */
function getDropdownItemClass(key: string): string {
  const map: Record<string, string> = {
    'toggle-valid': 'yzh-tree__action-toggle',
    delete: 'yzh-tree__action-danger',
  }
  return map[key] || ''
}

/** 处理节点操作按钮点击 */
function handleNodeAction(action: string, node: TreeNode) {
  emit('node-action', action, node)
}

// ========================================================
// 搜索关键字变化时重新过滤
// ========================================================

watch(searchKeyword, (val) => {
  treeRef.value?.filter(val)
})

// ========================================================
// 公开方法
// ========================================================

/** 获取勾选节点 */
function getCheckedNodes(): TreeNode[] {
  return treeRef.value?.getCheckedNodes() as TreeNode[] ?? []
}

/** 设置勾选节点 */
function setCheckedNodes(nodes: TreeNode[]) {
  ;(treeRef.value as any)?.setCheckedNodes(nodes)
}

/** 设置勾选状态 */
function setChecked(code: string, checked: boolean) {
  treeRef.value?.setChecked(code, checked, false)
}

/** 展开所有节点 */
function expandAll() {
  const expandRecursive = (nodes: TreeNode[]) => {
    for (const node of nodes) {
      const store = treeRef.value?.store
      if (store && store.nodesMap[node.code]) {
        store.nodesMap[node.code].expanded = true
      }
      if (node.children && node.children.length) {
        expandRecursive(node.children)
      }
    }
  }
  expandRecursive(props.data)
}

/** 折叠所有节点 */
function collapseAll() {
  const collapseRecursive = (nodes: TreeNode[]) => {
    for (const node of nodes) {
      const store = treeRef.value?.store
      if (store && store.nodesMap[node.code]) {
        store.nodesMap[node.code].expanded = false
      }
      if (node.children && node.children.length) {
        collapseRecursive(node.children)
      }
    }
  }
  collapseRecursive(props.data)
}

/** 设置当前选中节点 */
function setCurrentNode(code: string) {
  treeRef.value?.setCurrentKey(code)
}

/**
 * 向指定父节点追加子节点（直接操作 el-tree 内部 store，不触发 API）
 * @param parentCode 父节点 code（null = 追加到根级）
 * @param newNode 新节点数据（TreeNode 格式）
 */
function appendNode(parentCode: string | null, newNode: TreeNode) {
  if (!treeRef.value) return
  const store = treeRef.value.store
  if (parentCode) {
    const parentNode = store.nodesMap[parentCode]
    if (parentNode) {
      // el-tree Node.append 会自动处理 children 初始化和 isLeaf 更新
      parentNode.append(newNode)
      return
    }
  }
  // 根级：直接追加到 treeData
  props.data.push(newNode)
}

defineExpose({
  getCheckedNodes,
  setCheckedNodes,
  setChecked,
  expandAll,
  collapseAll,
  setCurrentNode,
  appendNode
})
</script>

<style scoped>
.yzh-tree {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

.yzh-tree__search {
  padding: 8px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.yzh-tree__inner {
  flex: 1;
  overflow: auto;
  padding: 16px;
}

.yzh-tree__node {
  display: flex;
  align-items: center;
  gap: 4px;
  flex: 1;
  min-width: 0;
}

.yzh-tree__icon {
  font-size: 14px;
  color: var(--el-color-primary);
  flex-shrink: 0;
}

.yzh-tree__icon--folder {
  color: var(--el-color-warning);
}

.yzh-tree__icon--leaf {
  color: var(--el-text-color-secondary);
}

.yzh-tree__label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 13px;
}

.yzh-tree__label.is-highlight {
  color: var(--el-color-primary);
  font-weight: 500;
}

.yzh-tree__badge {
  background: var(--el-color-info-light-7);
  color: var(--el-color-info-dark-2);
  font-size: 11px;
  padding: 0 6px;
  border-radius: 10px;
  line-height: 18px;
  flex-shrink: 0;
}

.yzh-tree__more-btn {
  font-size: 16px;
  padding: 0 4px;
  height: 20px;
  color: var(--el-text-color-secondary);
  opacity: 0;
  transition: opacity 0.2s;
}

.yzh-tree__node:hover .yzh-tree__more-btn {
  opacity: 1;
}

:deep(.yzh-tree__action-danger) {
  color: var(--el-color-danger) !important;
}

:deep(.yzh-tree__action-toggle) {
  color: var(--el-color-warning) !important;
}
</style>
