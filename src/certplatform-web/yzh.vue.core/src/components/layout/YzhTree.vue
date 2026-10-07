<template>
  <div class="yzh-tree">
    <!-- 搜索框 -->
    <div v-if="searchable" class="yzh-tree__search">
      <el-input
        v-model="searchKeyword"
        :placeholder="searchPlaceholder"
        clearable
        prefix-icon="Search"
        size="small"
      />
    </div>

    <!-- 树组件 -->
    <el-tree
      ref="treeRef"
      :data="data"
      :props="treeProps"
      :show-checkbox="showCheckbox"
      :check-strictly="checkStrictly"
      :lazy="lazy"
      :load="loadData"
      :default-expand-all="defaultExpandAll"
      :default-expanded-keys="defaultExpandedKeys"
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
        <div class="yzh-tree__node" @mouseenter="hoveredNode = getNodeKey(data)" @mouseleave="hoveredNode = null">
          <!-- 图标（Extra 双 Key：后端注入 PascalCase Icon，历史数据为 camel icon） -->
          <el-icon v-if="nodeIcon(data) && !isEmoji(nodeIcon(data)!)" class="yzh-tree__icon">
            <component :is="nodeIcon(data)" />
          </el-icon>
          <span v-else-if="nodeIcon(data)" class="yzh-tree__icon">{{ nodeIcon(data) }}</span>
          <el-icon v-else-if="isLeafNode(data)" class="yzh-tree__icon yzh-tree__icon--leaf">
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
            {{ getLabel(data) }}
          </span>

          <!-- 计数徽标（放在状态**之前**：状态列位置只取决于它右侧的元素 ⇒ 名称/计数长短都对齐） -->
          <span v-if="nodeExtra(data).badge" class="yzh-tree__badge">
            {{ nodeExtra(data).badge }}
          </span>

          <!-- 启用/停用状态（statusField 命中 Extra 时渲染；启停文案可覆盖） -->
          <YzhStatusBadge
            v-if="nodeStatus(data)"
            class="yzh-tree__status"
            :type="nodeStatus(data)?.type"
            :text="nodeStatus(data)?.text"
            size="small"
          />

          <!-- 操作下拉菜单 -->
          <el-dropdown
            v-if="resolveNodeActions(data).length"
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
                  v-for="action in resolveNodeActions(data)"
                  :key="action.key"
                  :command="action.key"
                  :disabled="action.disabled"
                  :class="getDropdownItemClass(action)"
                >
                  {{ action.text }}
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
/**
 * YzhTree - 通用树组件（原子组件，零领域依赖）
 *
 * 设计（C-C1..C-C5）：
 * - 零实体依赖：不 import 任何 @share / 业务类型，使用组件自身结构化类型 YzhTreeNode
 * - 字段参数化：nodeKey / labelField / childrenField / isLeafField / extraField 可配
 * - 节点动作下沉：nodeActions 支持 YzhAction[] 或 (node) => YzhAction[]（动态文案/禁用/danger）
 * - 搜索防抖 + 不深拷贝：过滤完全交给 el-tree filter-node-method（原地过滤，不克隆数据）
 */
import { computed, ref, watch } from 'vue'
import { ElTree, ElInput } from 'element-plus'
import { Document, Folder } from '@element-plus/icons-vue'
import YzhStatusBadge from '../ui/YzhStatusBadge.vue'
import { resolveStatusBadge } from '../../utils/status'
import type { YzhAction } from '../table/types'

/** 组件内结构化树节点（字段参数化的默认形状） */
export interface YzhTreeNode {
  [key: string]: any
  Code: string
  Name: string
  // ParentCode/Children 允许 undefined：与内核 TreeNode（types/tree）双向兼容，
  // 页面可直接把 TreeNode[] 传入 :data，组件事件回调也可直接绑定 (node: TreeNode) => void
  ParentCode?: string | null
  NodeType?: string
  IsLeaf?: boolean
  Extra?: Record<string, any>
  Children?: YzhTreeNode[]
}

// ========================================================
// 工具函数
// ========================================================

/** 判断字符串是否为 emoji（用于区分 element-plus 图标组件名和 emoji 字符串） */
function isEmoji(str: string): boolean {
  const emojiRegex = /[\u{1F300}-\u{1F9FF}]|[\u{2600}-\u{26FF}]|[\u{2700}-\u{27BF}]/u
  return emojiRegex.test(str)
}

/**
 * 节点字段读取：默认契约 PascalCase（Code/Name/IsLeaf/Extra），
 * 历史上模板里曾按小写读过导致静默失效 —— 统一 PascalCase 优先、camelCase 兜底。
 */
function readNodeField(node: Record<string, any>, pascal: string): any {
  if (!node) return undefined
  const camel = pascal.charAt(0).toLowerCase() + pascal.slice(1)
  return node[pascal] ?? node[camel]
}

// ========================================================
// Props
// ========================================================

interface Props {
  /** 树数据 */
  data: YzhTreeNode[]
  /** 节点唯一键字段名（默认 Code） */
  nodeKey?: string
  /** 显示文字字段名（默认 Name） */
  labelField?: string
  /** 子节点集合字段名（默认 Children） */
  childrenField?: string
  /** 叶子标志字段名（默认 IsLeaf） */
  isLeafField?: string
  /** 扩展字段名（icon/badge/disabled 等，默认 Extra） */
  extraField?: string
  /** 显示复选框 */
  showCheckbox?: boolean
  /** 严格模式（父子不联动） */
  checkStrictly?: boolean
  /** 懒加载 */
  lazy?: boolean
  /** 懒加载函数 */
  loadData?: (node: any, resolve: (data: YzhTreeNode[]) => void) => void
  /** 默认展开全部 */
  defaultExpandAll?: boolean
  /**
   * 默认展开的节点 key 列表（`nodeKey` 的值）。
   *
   * <para>⚠️ 与 `defaultExpandAll` 二选一：层级深的树（如资料清单「机构→标准→阶段→文件夹→文件」）
   * 全展开会有上百个节点，用本属性只展开前两级。</para>
   */
  defaultExpandedKeys?: (string | number)[]
  /** 点击节点展开 */
  expandOnClickNode?: boolean
  /** 高亮当前 */
  highlightCurrent?: boolean
  /** 当前选中节点 key */
  currentKey?: string
  /** 可搜索 */
  searchable?: boolean
  /** 搜索框占位文字 */
  searchPlaceholder?: string
  /** 高亮关键字（搜索时） */
  highlightKeyword?: boolean
  /**
   * 节点状态字段名（`Extra` 内，PascalCase / camelCase 双 Key）。
   *
   * <para>**默认 `'IsValid'`（全站统一，页面无需绑定）**：后端
   * `TreeMapper.MapToTreeItem` 已给所有含 `IsValid` 的实体注入 `Extra.IsValid`，
   * 故任何一棵树（左树右表 / 裸 YzhTree）自动显示启用·禁用徽章 —— 铁律九：启停唯一字段。</para>
   * <para>节点 `Extra` 无该字段 → 不渲染（如无启停语义的字典树/标准树不受影响）。</para>
   * <para>传 `''` = 关闭徽章；传其他字段名 = 覆盖（如 `Status`）。仍可绑内核
   * `treeStatusField`（取后端 `TreeConfig.EnableField`）做后端驱动的覆盖。</para>
   */
  statusField?: string
  /** 启用态文案（传空串 = 启用态不渲染徽章，长树只标注停用更清爽） */
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
  nodeKey: 'Code',
  labelField: 'Name',
  childrenField: 'Children',
  isLeafField: 'IsLeaf',
  extraField: 'Extra',
  showCheckbox: false,
  checkStrictly: false,
  lazy: false,
  defaultExpandAll: false,
  defaultExpandedKeys: () => [],
  expandOnClickNode: true,
  highlightCurrent: true,
  searchable: false,
  searchPlaceholder: '搜索节点',
  highlightKeyword: true,
  // 默认读 Extra.IsValid ⇒ 全站树自动显示启停徽章（页面零绑定）；传 '' 关闭
  statusField: 'IsValid',
  statusEnabledText: '启用',
  statusDisabledText: '禁用',
  nodeActions: () => [],
  legacyNodeActions: () => ({}),
  getActionLabel: undefined
})

// ========================================================
// Emits
// ========================================================

const emit = defineEmits<{
  (e: 'node-click', node: YzhTreeNode): void
  (e: 'check-change', checkedNodes: YzhTreeNode[]): void
  (e: 'node-expand', node: YzhTreeNode): void
  (e: 'node-collapse', node: YzhTreeNode): void
  (e: 'node-action', action: string, node: YzhTreeNode): void
}>()

// ========================================================
// 内部状态
// ========================================================

const treeRef = ref<InstanceType<typeof ElTree>>()
const searchKeyword = ref('')
const hoveredNode = ref<string | null>(null)

// ========================================================
// 字段参数化读取
// ========================================================

function getNodeKey(node: YzhTreeNode): string {
  return String(readNodeField(node, props.nodeKey) ?? '')
}

function getLabel(node: YzhTreeNode): string {
  return String(readNodeField(node, props.labelField) ?? '')
}

function getChildren(node: YzhTreeNode): YzhTreeNode[] {
  return readNodeField(node, props.childrenField) ?? []
}

function isLeafNode(node: YzhTreeNode | null | undefined): boolean {
  return readNodeField(node as Record<string, any>, props.isLeafField) === true
}

function nodeExtra(node: YzhTreeNode | null | undefined): Record<string, any> {
  return (readNodeField(node as Record<string, any>, props.extraField) as Record<string, any>) ?? {}
}

/** 节点图标名：Extra 双 Key（PascalCase 优先，camelCase 兜底，与 readNodeField 同约定） */
function nodeIcon(node: YzhTreeNode | null | undefined): string | undefined {
  const extra = nodeExtra(node)
  const icon = extra.Icon ?? extra.icon
  return icon ? String(icon) : undefined
}

/**
 * 节点启用状态徽章（S08：状态色只出现在徽章，不出现在按钮）。
 *
 * 判据统一走 `utils/status.ts` 的 `resolveStatusBadge`（与 CheckSelector、
 * 直用 el-tree 的页面同一份口径）—— 组件只负责传 statusField 与文案。
 * 字段缺失（Extra 未注入）→ 返回 null，不渲染徽章。
 */
function nodeStatus(
  node: YzhTreeNode | null | undefined,
): { text: string; type: 'success' | 'info' } | null {
  if (!props.statusField) return null
  return resolveStatusBadge(
    node as Record<string, any> | null | undefined,
    props.statusField,
    props.statusEnabledText,
    props.statusDisabledText,
    props.extraField,
  )
}

// ========================================================
// 树配置（el-tree props 由字段参数化派生）
// ========================================================

const treeProps = computed(() => ({
  label: props.labelField,
  children: props.childrenField,
  // 必须读叶子字段（后端 TreeControllerBase.FillIsLeafBatch 批量计算）。
  // 读错字段会让末端节点也长出展开箭头并白跑一次 tree/children。
  isLeaf: (data: Record<string, any>) => isLeafNode(data as YzhTreeNode),
  disabled: (data: Record<string, any>) => nodeExtra(data as YzhTreeNode).disabled ?? false
}))

// ========================================================
// 搜索过滤（el-tree 原地过滤，不深拷贝）
// ========================================================

function filterNode(value: string, data: Record<string, any>): boolean {
  if (!value) return true
  return (getLabel(data as YzhTreeNode) || '').toLowerCase().includes(String(value).toLowerCase())
}

function isMatchNode(node: YzhTreeNode): boolean {
  if (!searchKeyword.value) return false
  return (getLabel(node) || '').toLowerCase().includes(searchKeyword.value.toLowerCase())
}

// 搜索防抖（C-C5）：200ms 内连续输入只触发一次 el-tree.filter
let filterTimer: ReturnType<typeof setTimeout> | null = null
watch(searchKeyword, (val) => {
  if (filterTimer) clearTimeout(filterTimer)
  filterTimer = setTimeout(() => {
    treeRef.value?.filter(val)
  }, 200)
})

/**
 * 外部搜索框驱动本树的原地过滤（布局层 `.yzh-tree-table__tree-toolbar` 用）。
 * <para>⚠️ 外部只能走这条路，⛔ 不要在外层克隆 data 再传进来：el-tree lazy store
 * 重建时子节点不会从 data 还原（`Node.initialize` 对 lazy 跳过 `setData`），
 * 克隆一次 = 懒加载结果与展开态全丢（已踩坑 2026-10-07 菜单树搜索）。</para>
 */
function setSearchKeyword(value: string) {
  searchKeyword.value = value ?? ''
}

// ========================================================
// 节点动作（YzhAction[] / resolver）
// ========================================================

function resolveNodeActions(node: YzhTreeNode): YzhAction[] {
  let list: YzhAction[]
  if (typeof props.nodeActions === 'function') {
    list = props.nodeActions(node) || []
  } else {
    list = props.nodeActions
  }
  // 兼容旧 { 方法名: 文字 } + getActionLabel 动态文案
  const legacy: YzhAction[] = Object.entries(props.legacyNodeActions || {}).map(([key, text]) => ({
    key,
    text: props.getActionLabel ? props.getActionLabel(key, node) : text
  }))
  return [...list, ...legacy].filter((a) => a.visible !== false)
}

/**
 * 下拉菜单项样式类
 * S03：按钮 type 已归三档，启/停按钮是中性白钮，状态由行内徽章（S08）表达 ——
 * 因此这里只保留 `danger` 标志驱动的危险项强调（YzhAction.danger 是专用标志，
 * 不读 action.type），success/warning 分支随 S03 一并移除。
 */
function getDropdownItemClass(action: YzhAction): string {
  if (action.danger) return 'yzh-tree__action-danger'
  return ''
}

// ========================================================
// 事件处理
// ========================================================

function handleNodeClick(node: YzhTreeNode) {
  emit('node-click', node)
}

function handleCheckChange() {
  if (!treeRef.value) return
  const checkedNodes = treeRef.value.getCheckedNodes() as unknown as YzhTreeNode[]
  emit('check-change', checkedNodes)
}

function handleNodeExpand(node: YzhTreeNode) {
  emit('node-expand', node)
}

function handleNodeCollapse(node: YzhTreeNode) {
  emit('node-collapse', node)
}

/** 处理节点操作按钮点击 */
function handleNodeAction(action: string, node: YzhTreeNode) {
  emit('node-action', action, node)
}

// ========================================================
// 公开方法
// ========================================================

/** 获取勾选节点 */
function getCheckedNodes(): YzhTreeNode[] {
  return treeRef.value?.getCheckedNodes() as YzhTreeNode[] ?? []
}

/** 设置勾选节点 */
function setCheckedNodes(nodes: YzhTreeNode[]) {
  ;(treeRef.value as any)?.setCheckedNodes(nodes)
}

/** 设置勾选状态 */
function setChecked(code: string, checked: boolean) {
  treeRef.value?.setChecked(code, checked, false)
}

/** 展开所有节点 */
function expandAll() {
  const expandRecursive = (nodes: YzhTreeNode[]) => {
    for (const node of nodes) {
      const store = treeRef.value?.store
      if (store && store.nodesMap[getNodeKey(node)]) {
        store.nodesMap[getNodeKey(node)].expanded = true
      }
      if (getChildren(node).length) {
        expandRecursive(getChildren(node))
      }
    }
  }
  expandRecursive(props.data)
}

/** 折叠所有节点 */
function collapseAll() {
  const collapseRecursive = (nodes: YzhTreeNode[]) => {
    for (const node of nodes) {
      const store = treeRef.value?.store
      if (store && store.nodesMap[getNodeKey(node)]) {
        store.nodesMap[getNodeKey(node)].expanded = false
      }
      if (getChildren(node).length) {
        collapseRecursive(getChildren(node))
      }
    }
  }
  collapseRecursive(props.data)
}

/** 设置当前选中节点 */
function setCurrentNode(code: string) {
  treeRef.value?.setCurrentKey(code)
}

/** 当前高亮节点数据（el-tree store.currentNode.data；无选中返回 null） */
function getCurrentNode(): YzhTreeNode | null {
  return (treeRef.value?.getCurrentNode?.() as YzhTreeNode | null) ?? null
}

/**
 * 向指定父节点追加子节点（不触发 API，仅更新本地树 UI）
 * @param parentCode 父节点 key（null = 追加到根级）
 * @param newNode 新节点数据
 */
function appendNode(parentCode: string | null, newNode: YzhTreeNode) {
  if (!treeRef.value) return

  // ① 优先走 el-tree 公开 API append(data, parentNode)
  //    · 父级 → store.getNode(parentCode).insertChild(...)
  //    · 根级（parentCode 为空）→ store.append 的 isPropAbsent(undefined)=true →
  //      落到 store.root.insertChild(...)，正常建节点并渲染
  //    ⛔ 根级不能只做 props.data.push：el-tree 对 :data 是**浅监听**
  //       （tree.vue `watch(() => props.data, setData)` 无 deep），原地 push 不触发
  //       setData，store.root.childNodes 不变 ⇒ 新节点不渲染。
  try {
    ;(treeRef.value as any).append(newNode, parentCode ?? undefined)
    if (!parentCode) {
      // store 根节点只把数据写进 array.Children（属性），数组本体不动 → 补写，
      // 保持 treeData 与 store 同源（findNodeByCode / badge 以它为准）。
      // 根节点没有 tree-node 监听器，此处 push 不会触发 updateChildren 重复建节点。
      const key = getNodeKey(newNode)
      if (!props.data.some((n) => getNodeKey(n) === key)) props.data.push(newNode)
    }
    return
  } catch {
    // append 不可用 → 走下方 fallback
  }

  if (parentCode) {
    // ② store.nodesMap 拿 Node 对象直接插（Node 无 append，用 insertChild）
    const store = (treeRef.value as any).store
    const parentNode = store?.nodesMap?.[parentCode]
    if (parentNode && typeof parentNode.insertChild === 'function') {
      parentNode.insertChild({ data: newNode })
      return
    }

    // ③ 最终 fallback：直接在数据中查找父节点并插入
    if (addToTree(props.data, parentCode, newNode)) return
    // 父节点既不在 store 也不在数据里 → 不动（⛔ 绝不降级成根级追加）
    return
  }

  // ④ 兜底：只更新数据
  props.data.push(newNode)
}

/** 在树数据中找到 parentCode 对应节点并追加子节点 */
function addToTree(
  nodes: YzhTreeNode[],
  parentCode: string,
  newNode: YzhTreeNode,
): boolean {
  for (const node of nodes) {
    if (getNodeKey(node) === parentCode) {
      const children = getChildren(node)
      children.push(newNode)
      ;(node as any)[props.childrenField] = children
      ;(node as any)[props.isLeafField] = false
      return true
    }
    if (getChildren(node).length && addToTree(getChildren(node), parentCode, newNode)) {
      return true
    }
  }
  return false
}

defineExpose({
  getCheckedNodes,
  setCheckedNodes,
  setChecked,
  expandAll,
  collapseAll,
  setCurrentNode,
  getCurrentNode,
  setSearchKeyword,
  appendNode,
  /** 从树中移除指定节点（不触发 API，仅更新本地树 UI） */
  removeNode: (_parentCode: string | null, code: string) => {
    if (!treeRef.value) return
    // ① el-tree 公开 API remove(nodeKey) → 只从 store.childNodes 摘除
    try {
      ;(treeRef.value as any).remove(code)
    } catch {
      // ② store.nodesMap 拿 Node 对象删除
      const store = (treeRef.value as any).store
      const node = store?.nodesMap?.[code]
      if (node?.parentNode) node.parentNode.remove(node)
    }
    // ⚠️ store.remove **不会**改 props.data（根级尤其会残留：渲染读 childNodes、
    //    数据读 treeData → 下次 setData 又把已删节点带回来）。
    //    与 appendNode 的「store + data 双写」对称，这里补一刀；找不到则静默返回。
    removeFromTree(props.data, code)
  },
})

/** 在树数据中递归查找并移除指定 code 的节点 */
function removeFromTree(nodes: YzhTreeNode[], code: string): boolean {
  for (let i = 0; i < nodes.length; i++) {
    if (getNodeKey(nodes[i]) === code) {
      nodes.splice(i, 1)
      return true
    }
    if (getChildren(nodes[i]).length && removeFromTree(getChildren(nodes[i]), code)) {
      return true
    }
  }
  return false
}
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

.yzh-tree__status {
  flex-shrink: 0;
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
</style>
