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
          :data="treeData"
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
          :default-expanded-keys="treeDefaultExpandedKeys"
          :status-field="statusField"
          :status-enabled-text="statusEnabledText"
          :status-disabled-text="statusDisabledText"
          :node-actions="nodeActions"
          :legacy-node-actions="legacyNodeActions"
          :get-action-label="getActionLabel"
          @node-click="handleTreeNodeClick"
          @node-expand="handleTreeNodeExpand"
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
import { ref, watch } from 'vue'
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
  /**
   * 树默认展开的节点 key（`nodeKey` 的值）。
   * <para>层级深的树（机构→标准→阶段→文件夹→文件）用它只展开前两级，
   * 避免 `treeDefaultExpandAll` 一次铺开上百个节点。</para>
   */
  treeDefaultExpandedKeys?: (string | number)[]
  /**
   * 节点状态字段名（`Extra` 内，透传 YzhTree）。
   * <para>**不传 = 页面零绑定**：YzhTree 默认读 `Extra.IsValid`，全站树自动显示启停徽章。</para>
   * <para>需后端驱动覆盖时才绑 `logic.treeStatusField`（取 `TreeConfig.EnableField`）；
   * 传 `''` = 关闭徽章。</para>
   */
  statusField?: string
  /** 启用态文案（传空串 = 仅标注停用） */
  statusEnabledText?: string
  /** 停用态文案 */
  statusDisabledText?: string
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
  treeDefaultExpandedKeys: () => [],
  statusField: undefined,
  statusEnabledText: '启用',
  statusDisabledText: '禁用',
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
// 树搜索（委托 YzhTree 原地过滤，不克隆 data）
// ========================================================

/**
 * 工具栏搜索只做转发：真正的过滤由 el-tree `filter-node-method` 原地完成
 * （隐藏不匹配节点 + 保留匹配祖先），**不重建 store**。
 *
 * ⛔ 曾经这里用 `filterTreeData` 克隆 `props.treeData` 再传给 YzhTree：
 *   ① 读 `node[childrenField]`（Children），而懒加载子节点写在 `data.children` → 搜深层节点恒为空；
 *   ② 换数组 = el-tree lazy store 重建，`Node.initialize` 对 lazy 跳过 `setData` →
 *      已展开的懒加载子节点全丢（搜完清空只剩 3 个根）。
 * 两条合起来即「树搜索清空后子节点消失」的根因 → 改走 `setSearchKeyword`（2026-10-07）。
 */
watch(treeSearchKeyword, (val) => {
  treeRef.value?.setSearchKeyword(val ?? '')
})

// ========================================================
// 树事件处理
// ========================================================

function handleTreeNodeClick(node: YzhTreeNode) {
  emit('tree-node-click', node)
}

/**
 * 点展开箭头 → 同步选中，右表跟随（与点节点名一致）。
 *
 * el-tree 只有 `.el-tree-node` 根上的 `@click.stop` 才 emit `node-click`，
 * 箭头是 `@click.stop="handleExpandIconClick"`，**只 expand 不选中** ⇒ 右表不刷新。
 * 「点名」路径 handleClick 先 setCurrent 再 expand，故此处以 store.currentNode 去重，
 * 不会出现一次点击两次刷新；箭头点在**已选中**节点上则不重复查询（右表本就是它的数据）。
 */
function handleTreeNodeExpand(node: YzhTreeNode) {
  const key = String(node[props.nodeKey] ?? '')
  if (!key) return
  const current = treeRef.value?.getCurrentNode?.() as YzhTreeNode | null | undefined
  if (current && String(current[props.nodeKey] ?? '') === key) return
  treeRef.value?.setCurrentNode(key)
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
  background: var(--yzh-color-bg-container, #fff);
  flex-shrink: 0;
}

.yzh-tree-table__tree-footer {
  padding: 12px;
  border-top: 1px solid var(--el-border-color-lighter);
  background: var(--yzh-color-bg-container, #fff);
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
