<script setup lang="ts" generic="T extends Record<string, any> = any">
/**
 * YzhTreeTable - 行成树的单表原子组件（零领域依赖）
 *
 * 定位（06-§4.3）：
 * - 一份 dataLoader 返回树根数组；行数据经 children 字段渲染为可展开树表
 * - 与 YzhTreeTableLayout（左树右表布局壳）并存：Layout 管分栏，本组件管单表层级
 *
 * 默认（菜单/条款树表模式）：
 * - showPagination=false（整树加载）
 * - defaultExpandAll=true
 * - childrenField='children'（字段参数化，透传 el-table tree-props）
 *
 * 行按钮：
 * - allowAddChild 为真时在 rowActionButtons 前插入 {key:'add-child',text:'新增下级'}
 * - 后端 RowButtons.AddChild 契约见 TODO F2（本组件仅前端兜底）
 *
 * 事件契约（C-A10 + 树表）：
 * - row-action / toolbar-action / selection-change / refresh / expand-change
 *
 * 独立可用性（C-A11）：仅传 columns + dataLoader 即可渲染与交互。
 */
import { computed, ref } from 'vue'
import YzhTable from './YzhTable.vue'
import type {
  DefaultSort,
  SearchField,
  YzhAction,
  YzhTableColumn,
  YzhTableDataLoader,
  YzhTableToolbar
} from './types'

const props = withDefaults(
  defineProps<{
    columns: YzhTableColumn<T>[]
    dataLoader: YzhTableDataLoader<T>
    searchFields?: SearchField[]
    selectable?: boolean
    selectMode?: 'none' | 'single' | 'multiple'
    /** 树表默认关分页（整树加载；total 仅作展示） */
    showPagination?: boolean
    pageSize?: number
    defaultSort?: DefaultSort
    height?: string | number
    rowKey?: string
    emptyText?: string
    toolbar?: boolean | YzhTableToolbar
    toolbarActions?: YzhAction[]
    searchMaxFields?: number
    noPadding?: boolean
    rowActionButtons?: Record<string, string> | YzhAction[] | ((row: T) => Record<string, string> | YzhAction[])
    rowActionLink?: boolean
    actionMaxInline?: number
    /** 树表默认全展开 */
    defaultExpandAll?: boolean
    /** 行子级字段（el-table tree-props.children；字段参数化） */
    childrenField?: string
    /** 行级「新增下级」开关（前端兜底；F2 后端契约另做） */
    allowAddChild?: boolean | ((row: T) => boolean)
    /** add-child 按钮文案 */
    addChildText?: string
  }>(),
  {
    searchFields: undefined,
    selectable: undefined,
    selectMode: undefined,
    showPagination: false,
    pageSize: 20,
    defaultSort: undefined,
    height: undefined,
    rowKey: 'Code',
    emptyText: '暂无数据',
    toolbar: true,
    toolbarActions: () => [],
    searchMaxFields: 2,
    noPadding: false,
    rowActionButtons: () => [],
    rowActionLink: true,
    actionMaxInline: 0,
    defaultExpandAll: true,
    childrenField: 'children',
    allowAddChild: false,
    addChildText: '新增下级'
  }
)

const emit = defineEmits<{
  (e: 'selection-change', rows: T[]): void
  (e: 'row-click', row: T, index: number): void
  (e: 'refresh'): void
  (e: 'row-action', key: string, row: T, action?: YzhAction): void
  (e: 'toolbar-action', key: string, action: YzhAction): void
  (e: 'expand-change', row: T, expandedRows: T[]): void
}>()

const tableRef = ref<any>(null)

/** 该行是否允许新增下级 */
function canAddChild(row: T): boolean {
  if (typeof props.allowAddChild === 'function') return !!props.allowAddChild(row)
  return !!props.allowAddChild
}

/** 合并 add-child 到行按钮（resolver 保持按行求值） */
const effectiveRowActions = computed(() => {
  const base = props.rowActionButtons
  if (!props.allowAddChild) return base
  const inject = (list: Record<string, string> | YzhAction[]): Record<string, string> | YzhAction[] => {
    const arr = Array.isArray(list)
      ? [...list]
      : Object.entries(list || {}).map(([key, text]) => ({ key, text }))
    if (arr.some((a) => a.key === 'add-child')) return arr
    const addChild: YzhAction = { key: 'add-child', text: props.addChildText, type: 'primary' }
    return [addChild, ...arr]
  }
  if (typeof base === 'function') {
    return (row: T) => {
      const list = inject(base(row))
      if (Array.isArray(list) && !canAddChild(row)) {
        return list.map((a) => (a.key === 'add-child' ? { ...a, visible: false } : a))
      }
      return list
    }
  }
  return inject(base)
})

/** 树 props（childrenField 参数化） */
const treeProps = computed(() => ({
  children: props.childrenField,
  hasChildren: 'hasChildren'
}))

function refresh() {
  tableRef.value?.refresh()
}
function loadData() {
  tableRef.value?.loadData()
}
function expandAll() {
  tableRef.value?.expandAll()
}
function collapseAll() {
  tableRef.value?.collapseAll()
}

defineExpose({
  refresh,
  loadData,
  expandAll,
  collapseAll,
  insertRow: (...args: any[]) => (tableRef.value as any)?.insertRow(...args),
  replaceRow: (...args: any[]) => (tableRef.value as any)?.replaceRow(...args),
  removeRow: (...args: any[]) => (tableRef.value as any)?.removeRow(...args),
  getRowCount: () => (tableRef.value as any)?.getRowCount?.() ?? 0,
  getSelectedRows: () => (tableRef.value as any)?.getSelectedRows?.() ?? [],
  setCheckedRows: (...args: any[]) => (tableRef.value as any)?.setCheckedRows(...args),
  clearSelection: () => (tableRef.value as any)?.clearSelection?.(),
  tableRef
})
</script>

<template>
  <YzhTable
    ref="tableRef"
    :columns="columns"
    :data-loader="dataLoader"
    :search-fields="searchFields"
    :selectable="selectable as any"
    :select-mode="selectMode"
    :show-pagination="showPagination"
    :page-size="pageSize"
    :default-sort="defaultSort"
    :height="height"
    :row-key="rowKey"
    :empty-text="emptyText"
    :toolbar="toolbar"
    :toolbar-actions="toolbarActions"
    :search-max-fields="searchMaxFields"
    :no-padding="noPadding"
    :row-action-buttons="effectiveRowActions as any"
    :row-action-link="rowActionLink"
    :action-max-inline="actionMaxInline"
    :default-expand-all="defaultExpandAll"
    :tree-props="treeProps"
    v-bind="$attrs"
    @selection-change="(rows: T[]) => emit('selection-change', rows)"
    @row-click="(row: T, index: number) => emit('row-click', row, index)"
    @refresh="emit('refresh')"
    @row-action="(key: string, row: T, action?: YzhAction) => emit('row-action', key, row, action)"
    @toolbar-action="(key: string, action: YzhAction) => emit('toolbar-action', key, action)"
    @expand-change="(row: T, expandedRows: T[]) => emit('expand-change', row, expandedRows)"
  >
    <template v-for="(_, name) in $slots" #[name]="slotData">
      <slot :name="name" v-bind="slotData as any" />
    </template>
  </YzhTable>
</template>
