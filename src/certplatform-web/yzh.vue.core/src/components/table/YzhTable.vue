<script setup lang="ts" generic="T extends Record<string, any> = any">
/**
 * YzhTable - 自研表格组件（原子组件，零领域依赖）
 *
 * 特性：
 * - 加载/空/错误三态
 * - 排序、分页、选择模式（selectMode）
 * - 工具栏（声明式 toolbarActions + 列设置）
 * - 搜索栏联动
 * - 行操作按钮下沉：icon / type / disabled / visible / confirm / 溢出折叠（actionMaxInline）
 * - render:'tag' 列级标签渲染（valueMap/tagTypeMap 由调用方传入）
 * - 插槽扩展（#column-prop）
 *
 * 事件契约（C-A10）：
 * - row-action(key, row, action)
 * - toolbar-action(key, action)
 *
 * 独立可用性（C-A11）：仅传 columns + dataLoader 即可渲染与交互。
 */
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, getCurrentInstance, onMounted, reactive, ref, watch } from 'vue'
import YzhPagination from '../layout/YzhPagination.vue'
import YzhSearchBar from '../layout/YzhSearchBar.vue'
import YzhToolbar from '../layout/YzhToolbar.vue'
import type {
  DefaultSort,
  Page,
  PageParams,
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
    /** 兼容旧属性：等价 selectMode='multiple' */
    selectable?: boolean
    /** 选择模式（C-A7）：none=无选择列；single=单选（行高亮）；multiple=多选（checkbox） */
    selectMode?: 'none' | 'single' | 'multiple'
    showPagination?: boolean
    pageSize?: number
    defaultSort?: DefaultSort
    height?: string | number
    /** 行键字段名。中立默认 'Code'（平台主键约定），不绑定 Id */
    rowKey?: string
    emptyText?: string
    toolbar?: boolean | YzhTableToolbar
    /** 声明式工具栏按钮（C-A9），配合 @toolbar-action 使用；#toolbar-left 插槽保留并排渲染 */
    toolbarActions?: YzhAction[]
    searchMaxFields?: number
    /** 是否禁用内部 padding（用于嵌套在卡片/TreeTable 中时避免双层 padding） */
    noPadding?: boolean
    /**
     * 行自定义操作按钮（C-A3 能力下沉）
     * - YzhAction[]：静态声明（含 type/icon/disabled/confirm）
     * - (row) => YzhAction[]：按行动态解析（可见性/禁用随行变化）
     * - 兼容旧 Record<string,string>（等价 [{key,text}]）
     */
    rowActionButtons?: Record<string, string> | YzhAction[] | ((row: T) => Record<string, string> | YzhAction[])
    /** 行操作按钮是否使用 link 样式（默认 true） */
    rowActionLink?: boolean
    /** 行按钮超过 N 个时折叠为「更多」下拉（0 = 不折叠） */
    actionMaxInline?: number
    /** 树形数据默认全部展开（children 字段驱动，透传 el-table default-expand-all） */
    defaultExpandAll?: boolean
    /** 树形字段映射（透传 el-table tree-props；children 默认 'children'） */
    treeProps?: { children?: string; hasChildren?: string }
  }>(),
  {
    selectable: false,
    selectMode: undefined,
    showPagination: true,
    pageSize: 20,
    rowKey: 'Code',
    emptyText: '暂无数据',
    toolbar: true,
    toolbarActions: () => [],
    searchMaxFields: 2,
    noPadding: false,
    rowActionButtons: () => [],
    rowActionLink: true,
    actionMaxInline: 0,
    defaultExpandAll: false,
    treeProps: undefined
  }
)

const emit = defineEmits<{
  (e: 'selection-change', rows: T[]): void
  (e: 'row-click', row: T, index: number): void
  (e: 'refresh'): void
  /** 行操作（载荷含动作描述符） */
  (e: 'row-action', key: string, row: T, action?: YzhAction): void
  /** 工具栏动作 */
  (e: 'toolbar-action', key: string, action: YzhAction): void
  /** 树表展开/收起（el-table expand-change 透传） */
  (e: 'expand-change', row: T, expandedRows: T[]): void
}>()

// 数据状态
const loading = ref(false)
const error = ref<string>('')
const rows = ref<T[]>([])
const total = ref(0)
const selectedRows = ref<T[]>([])

// 分页状态
const page = ref(1)
const pageSize = ref(props.pageSize)
const sort = ref<DefaultSort | null>(props.defaultSort || null)

// 搜索状态
const searchParams = reactive<Record<string, any>>({})

// 用户手动隐藏的列 field 集合
const hiddenColumnFields = ref<Set<string>>(new Set())

/** 实际生效的选择模式（selectable 兼容 selectMode） */
const effectiveSelectMode = computed<'none' | 'single' | 'multiple'>(() => {
  if (props.selectMode) return props.selectMode
  return props.selectable ? 'multiple' : 'none'
})
const isMultiple = computed(() => effectiveSelectMode.value === 'multiple')

/** 可被列设置的列（有 label 且非操作列） */
const columnSettingList = computed(() =>
  props.columns.filter((c) => c.label && c.prop !== '__yzh_action')
)

/** 实际可见的列 */
const visibleColumns = computed(() =>
  props.columns.filter((c) => {
    if (c.hidden) return false
    if (hiddenColumnFields.value.has(c.prop as string)) return false
    return true
  })
)

// ========================================================
// 行/工具栏动作解析
// ========================================================

/** Record<string,string> → YzhAction[]（兼容旧形状） */
function fromRecord(rec: Record<string, string>): YzhAction[] {
  return Object.entries(rec).map(([key, text]) => ({ key, text }))
}

/** 解析某行的操作按钮为 YzhAction[] */
function resolveRowActions(row: T): YzhAction[] {
  const raw = typeof props.rowActionButtons === 'function'
    ? props.rowActionButtons(row)
    : props.rowActionButtons
  const list = Array.isArray(raw) ? raw : fromRecord(raw || {})
  return list.filter((a) => a.visible !== false)
}

/** 是否显示动态行操作列 */
const showDynamicActionColumn = computed(() => {
  const hasActionsCol = props.columns.some((c) => c.prop === 'actions')
  if (typeof props.rowActionButtons === 'function') return !hasActionsCol
  const count = Array.isArray(props.rowActionButtons)
    ? props.rowActionButtons.length
    : Object.keys(props.rowActionButtons || {}).length
  return count > 0 && !hasActionsCol
})

/** 溢出折叠：前 N 个平铺，其余收进「更多」下拉 */
const inlineOverflow = computed(() => props.actionMaxInline > 0)

function splitRowActions(list: YzhAction[]): { inline: YzhAction[]; overflow: YzhAction[] } {
  if (!inlineOverflow.value || list.length <= props.actionMaxInline) {
    return { inline: list, overflow: [] }
  }
  return { inline: list.slice(0, props.actionMaxInline), overflow: list.slice(props.actionMaxInline) }
}

/** 工具栏按钮（声明式） */
const toolbarButtons = computed<YzhAction[]>(() =>
  props.toolbarActions.filter((a) => a.visible !== false)
)

/** 行动作点击：confirm → 确认弹窗 → emit */
async function onRowActionClick(action: YzhAction, row: T) {
  if (action.disabled) return
  if (action.confirm) {
    try {
      await ElMessageBox.confirm(action.confirm, '操作确认', { type: 'warning' })
    } catch {
      return
    }
  }
  emit('row-action', action.key, row, action)
}

/** 工具栏动作点击 */
async function onToolbarActionClick(action: YzhAction) {
  if (action.disabled) return
  if (action.confirm) {
    try {
      await ElMessageBox.confirm(action.confirm, '操作确认', { type: 'warning' })
    } catch {
      return
    }
  }
  emit('toolbar-action', action.key, action)
}

// ========================================================
// 列设置
// ========================================================

/** 切换列显示/隐藏 */
function toggleColumnVisibility(col: YzhTableColumn<T>, visible: boolean) {
  if (visible) {
    hiddenColumnFields.value.delete(col.prop as string)
  } else {
    hiddenColumnFields.value.add(col.prop as string)
  }
  hiddenColumnFields.value = new Set(hiddenColumnFields.value)
}

/** 在列设置 popover 中切换排序 */
function toggleSortInSettings(col: YzhTableColumn<T>) {
  if (col.sortable === false) return
  const prop = col.prop as string
  if (sort.value && sort.value.prop === prop) {
    sort.value = { ...sort.value, order: sort.value.order === 'asc' ? 'desc' : 'asc' }
  } else {
    sort.value = { prop, order: 'asc' }
  }
}

/** 获取排序图标文字 */
function getSortIconInSettings(col: YzhTableColumn<T>): string {
  const prop = col.prop as string
  if (!sort.value || sort.value.prop !== prop) return '排序'
  return sort.value.order === 'asc' ? '↑ 升序' : '↓ 降序'
}

/** 重置列设置为默认 */
function resetColumnSettings() {
  hiddenColumnFields.value = new Set()
  sort.value = props.defaultSort || null
}

/** 应用列设置（触发数据刷新） */
function applyColumnSettings() {
  loadData()
}

// 工具栏配置
const toolbarConfig = computed<YzhTableToolbar>(() => {
  if (props.toolbar === false) return {}
  if (props.toolbar === true) return { columnSetting: true }
  return props.toolbar
})

/** 是否渲染工具栏（有配置 或 有声明式按钮 或 有插槽内容） */
const showToolbar = computed(() => {
  return Object.keys(toolbarConfig.value).length > 0 || toolbarButtons.value.length > 0
})

/**
 * 加载数据
 */
async function loadData() {
  loading.value = true
  error.value = ''
  try {
    // 记录当前选中行的 key（用于数据重新加载后恢复选择）
    const selectedKeys = new Set(selectedRows.value.map((r: any) => r[props.rowKey]))

    const params: PageParams = {
      page: page.value,
      rows: pageSize.value,
      ...(sort.value ? { sort: sort.value.prop, order: sort.value.order } : {}),
      ...searchParams
    } as PageParams
    const res: Page<T> = await props.dataLoader(params)
    rows.value = res.rows || []
    total.value = res.total || 0

    // 恢复选择状态：新数据中匹配之前选中 key 的行自动勾选
    if (selectedKeys.size > 0) {
      const restored: T[] = []
      for (const row of rows.value) {
        if (selectedKeys.has((row as any)[props.rowKey])) {
          restored.push(row as T)
        }
      }
      selectedRows.value = restored
    }
  } catch (e: any) {
    // F-10：加载失败保留上次数据（不清空闪烁）；err 优先（P0 后失败时 message 为 ""）
    error.value = (e as any)?.err || e?.message || '数据加载失败'
    ElMessage.error(error.value)
  } finally {
    loading.value = false
  }
}

/**
 * 处理排序变化
 */
function onSortChange({ prop, order }: { prop: string; order: 'ascending' | 'descending' | null }) {
  if (!order) {
    sort.value = null
  } else {
    sort.value = {
      prop,
      order: order === 'ascending' ? 'asc' : 'desc'
    }
  }
  loadData()
}

/**
 * 处理分页变化
 */
function onPageChange(p: number) {
  page.value = p
  loadData()
}

function onSizeChange(s: number) {
  pageSize.value = s
  page.value = 1
  loadData()
}

/**
 * 处理搜索
 */
function onSearch(params: Record<string, any>) {
  Object.assign(searchParams, params)
  page.value = 1
  loadData()
}

function onSearchReset() {
  Object.keys(searchParams).forEach((k) => delete searchParams[k])
  if (props.searchFields) {
    props.searchFields.slice(0, props.searchMaxFields).forEach((f) => {
      if (f.defaultValue !== undefined) searchParams[f.prop] = f.defaultValue
    })
  }
  page.value = 1
  loadData()
}

/**
 * 处理选择变化
 */
function onSelectionChange(selection: T[]) {
  selectedRows.value = selection
  emit('selection-change', selection)
}

/**
 * 处理行点击
 */
function onRowClick(row: T, index: number) {
  emit('row-click', row, index)
}

// ========================================================
// 开发期护栏：行操作按钮必须由父组件监听 @row-action
// ========================================================
const instance = getCurrentInstance()
let rowActionWarned = false

/**
 * 操作列宽度自适应（按按钮实际文案估算，非固定 70px/个）
 * - 中文 ~14px/字、西文 ~8px/字、单按钮两侧边距 16px
 * - 函数式 rowActionButtons：取已加载行中的最大宽度（行数据变化自动重算）
 * - 最终宽度 = 内容宽 + 单元格内边距 24px，下限 88px（容纳表头「操作」）
 */
function estimateActionWidth(action: YzhAction): number {
  const text = String(action.text ?? '')
  let textWidth = 0
  for (const ch of text) {
    textWidth += /[一-鿿]/.test(ch) ? 14 : 8
  }
  if (textWidth === 0 && action.icon) textWidth = 16
  return textWidth + 16
}

/** 一组行按钮的渲染宽度（含溢出折叠时的「更多」下拉） */
function estimateActionsWidth(list: YzhAction[]): number {
  const { inline, overflow } = splitRowActions(list)
  let width = inline.reduce((sum, a) => sum + estimateActionWidth(a), 0)
  if (overflow.length > 0) width += estimateActionWidth({ key: '__overflow__', text: '更多' })
  return width
}

const actionColWidth = computed(() => {
  let contentWidth = 0
  if (typeof props.rowActionButtons === 'function') {
    contentWidth = (rows.value as T[]).reduce(
      (max, row) => Math.max(max, estimateActionsWidth(resolveRowActions(row))),
      0
    )
    // 数据未加载 / 所有行均无按钮时兜底，避免列宽塌陷
    if (contentWidth === 0) {
      contentWidth = estimateActionsWidth([{ key: '__placeholder__', text: '操作' }])
    }
  } else {
    const raw = Array.isArray(props.rowActionButtons)
      ? props.rowActionButtons
      : fromRecord(props.rowActionButtons || {})
    contentWidth = estimateActionsWidth(raw.filter((a) => a.visible !== false))
  }
  return Math.max(contentWidth + 24, 88)
})

watch(
  () => {
    if (typeof props.rowActionButtons === 'function') return 1
    return Array.isArray(props.rowActionButtons)
      ? props.rowActionButtons.length
      : Object.keys(props.rowActionButtons || {}).length
  },
  (count) => {
    if (count === 0 || rowActionWarned) return
    if (!(import.meta as any).env?.DEV) return
    if (instance?.vnode.props?.onRowAction) return
    rowActionWarned = true
    console.warn(
      '[YzhTable] 已配置 rowActionButtons，但父组件未监听 @row-action：行操作按钮点击不会有任何效果。' +
        ' 请绑定 @row-action（SingleTableCore.onRowAction 可直接派发 edit/delete/toggle-valid）。'
    )
  },
  { immediate: true }
)

/**
 * 刷新
 */
function refresh() {
  page.value = 1
  loadData()
  emit('refresh')
}

// 初始化默认值
onMounted(() => {
  if (props.searchFields) {
    props.searchFields.slice(0, props.searchMaxFields).forEach((f) => {
      if (f.defaultValue !== undefined) {
        searchParams[f.prop] = f.defaultValue
      }
    })
  }
  loadData()
})

/**
 * 插入行（局部刷新，不重新请求 API）
 */
function insertRow(row: T, position: 'top' | 'bottom' = 'top') {
  if (position === 'top') {
    rows.value.unshift(row as any)
  } else {
    rows.value.push(row as any)
  }
  total.value++
}

/**
 * 替换行（按匹配函数查找并替换）
 */
function replaceRow(matchFn: (row: any) => boolean, newRow: T) {
  const idx = rows.value.findIndex((r: any) => matchFn(r))
  if (idx >= 0) {
    rows.value.splice(idx, 1, newRow as any)
  }
}

/**
 * 移除行（按匹配函数查找并移除）
 */
function removeRow(matchFn: (row: any) => boolean) {
  const idx = rows.value.findIndex((r: any) => matchFn(r))
  if (idx >= 0) {
    rows.value.splice(idx, 1)
    total.value = Math.max(0, total.value - 1)
  }
}

/**
 * 获取当前行数
 */
function getRowCount(): number {
  return rows.value.length
}

/**
 * 批量设置行勾选状态（用于外部联动选择）
 * @param matchFn 匹配函数，返回 true 的行会被勾选
 * @param checked 是否勾选
 */
function setCheckedRows(matchFn: (row: T) => boolean, checked: boolean) {
  if (checked) {
    // 勾选匹配的行（合并到已有选中行）
    const existing = new Set<any>(selectedRows.value)
    for (const row of rows.value) {
      if (matchFn(row as T) && !existing.has(row)) {
        selectedRows.value.push(row as any)
      }
    }
  } else {
    // 取消勾选匹配的行
    selectedRows.value = selectedRows.value.filter((r) => !matchFn(r as T))
  }
  emit('selection-change', [...selectedRows.value as T[]])
}

// ========================================================
// 树表展开/收起（el-table tree-props + toggleRowExpansion）
// ========================================================
const tableRef = ref()

/** 子级字段名（treeProps.children，默认 'children'） */
const childrenField = computed(() => props.treeProps?.children || 'children')

/** 生效的 tree-props（未显式传时使用默认 children） */
const effectiveTreeProps = computed(() => ({
  children: childrenField.value,
  hasChildren: props.treeProps?.hasChildren || 'hasChildren',
  ...props.treeProps
}))

/** 深度遍历行树 */
function walkRows(list: T[], fn: (row: T) => void) {
  for (const row of list) {
    fn(row)
    const kids = (row as any)[childrenField.value]
    if (Array.isArray(kids) && kids.length) walkRows(kids as T[], fn)
  }
}

/** 全部展开（树表） */
function expandAll() {
  walkRows(rows.value as T[], (row) => tableRef.value?.toggleRowExpansion?.(row, true))
}

/** 全部收起（树表） */
function collapseAll() {
  walkRows(rows.value as T[], (row) => tableRef.value?.toggleRowExpansion?.(row, false))
}

function onExpandChange(row: T, expandedRows: T[]) {
  emit('expand-change', row, expandedRows as T[])
}

// 暴露方法给父组件
defineExpose({
  refresh,
  loadData,
  insertRow,
  replaceRow,
  removeRow,
  getRowCount,
  getSelectedRows: () => selectedRows.value,
  setCheckedRows,
  clearSelection: () => {
    selectedRows.value = []
    emit('selection-change', [])
  },
  expandAll,
  collapseAll
})
</script>

<template>
  <div class="yzh-table">
    <!-- 搜索栏 -->
    <YzhSearchBar
      v-if="searchFields && searchFields.length"
      :fields="searchFields"
      :default-values="searchParams"
      :cols="2"
      :max-fields="searchMaxFields"
      @search="onSearch"
      @reset="onSearchReset"
    />

    <!-- 工具栏 -->
    <YzhToolbar v-if="showToolbar" :buttons="toolbarButtons" @action="(_key, action) => onToolbarActionClick(action)">
      <template #left>
        <slot name="toolbar-left" />
      </template>
      <template #right>
        <slot name="toolbar-right" :selected="selectedRows" :refresh="refresh">
          <!-- 列设置 popover -->
          <el-popover v-if="toolbarConfig.columnSetting" trigger="click" placement="bottom-end" :width="200">
            <template #reference>
              <el-button text>
                <i class="bi bi-columns"></i>
                列设置
              </el-button>
            </template>
            <div class="yzh-column-settings">
              <div class="yzh-column-settings__header">列筛选与排序</div>
              <div class="yzh-column-settings__body">
                <div
                  v-for="col in columnSettingList"
                  :key="col.prop as string"
                  class="yzh-column-settings__item"
                >
                  <el-checkbox
                    :model-value="!hiddenColumnFields.has(col.prop as string) && !col.hidden"
                    @change="(val: boolean) => toggleColumnVisibility(col, val)"
                  >
                    {{ col.label }}
                  </el-checkbox>
                  <el-button
                    class="yzh-column-settings__sort-btn"
                    :class="{ 'is-active': sort?.prop === col.prop }"
                    :disabled="col.sortable === false"
                    @click="toggleSortInSettings(col)"
                  >
                    {{ getSortIconInSettings(col) }}
                  </el-button>
                </div>
              </div>
              <div class="yzh-column-settings__footer">
                <el-button size="small" @click="resetColumnSettings">重置</el-button>
                <el-button size="small" type="primary" @click="applyColumnSettings">确定</el-button>
              </div>
            </div>
          </el-popover>
        </slot>
      </template>
    </YzhToolbar>

    <!-- 表格容器 -->
    <div class="yzh-table__wrapper" :class="{ 'yzh-table__wrapper--no-padding': noPadding }">
      <!-- 表格 -->
      <div
        class="yzh-table__body"
        :style="height ? { height: typeof height === 'number' ? height + 'px' : height } : {}"
      >
        <el-table
          ref="tableRef"
          v-loading="loading"
          :data="rows"
          :row-key="rowKey"
          :default-expand-all="defaultExpandAll"
          :tree-props="effectiveTreeProps"
          :height="height !== undefined && height !== null ? height : '100%'"
          :highlight-current-row="effectiveSelectMode === 'single'"
          stripe
          border
          @selection-change="onSelectionChange"
          @sort-change="onSortChange"
          @row-click="onRowClick"
          @expand-change="onExpandChange"
        >
          <el-table-column v-if="isMultiple" type="selection" width="48" :reserve-selection="false" />

          <template v-for="col in visibleColumns" :key="col.prop">
            <el-table-column
              :prop="col.prop as string"
              :label="col.label"
              :width="col.width"
              :min-width="col.minWidth"
              :fixed="col.fixed"
              :sortable="col.sortable"
              :align="col.align || 'left'"
              :show-overflow-tooltip="!col.slot"
              :class-name="col.className"
            >
              <template #default="{ row, $index }">
                <slot
                  v-if="col.slot"
                  :name="`column-${String(col.prop)}`"
                  :row="row"
                  :index="$index"
                  :value="row[col.prop]"
                >
                  <!-- 默认 fallback 渲染 -->
                  {{ col.formatter ? col.formatter(row[col.prop], row, $index) : row[col.prop] }}
                </slot>
                <template v-else-if="col.dictCode">
                  <el-tag v-if="col.tagType" :type="col.tagType" disable-transitions>
                    <!-- dictStore 将在后续版本引入 -->
                    {{ row[col.prop] }}
                  </el-tag>
                  <span v-else>{{ row[col.prop] }}</span>
                </template>
                <!-- 通用标签渲染（valueMap/tagTypeMap 由调用方传入，组件不内置业务语义） -->
                <template v-else-if="col.tagMap">
                  <el-tag :type="col.tagTypeMap?.[row[col.prop]] ?? 'info'" size="small" disable-transitions>
                    {{ col.tagMap![row[col.prop]] ?? row[col.prop] }}
                  </el-tag>
                </template>
                <template v-else>
                  {{ col.formatter ? col.formatter(row[col.prop], row, $index) : row[col.prop] }}
                </template>
              </template>
            </el-table-column>
          </template>

          <!-- 动态行操作列（YzhAction[] 驱动：icon/type/disabled/confirm/溢出折叠） -->
          <el-table-column
            v-if="showDynamicActionColumn"
            label="操作"
            :width="actionColWidth"
            fixed="right"
            align="center"
          >
            <template #default="{ row }">
              <template v-for="action in splitRowActions(resolveRowActions(row)).inline" :key="action.key">
                <el-button
                  :link="rowActionLink"
                  size="small"
                  :type="action.type ?? 'primary'"
                  :disabled="action.disabled"
                  @click="onRowActionClick(action, row)"
                >
                  {{ action.text }}
                </el-button>
              </template>
              <el-dropdown
                v-if="splitRowActions(resolveRowActions(row)).overflow.length > 0"
                trigger="click"
                @command="(key: string) => { const a = splitRowActions(resolveRowActions(row)).overflow.find(x => x.key === key); if (a) onRowActionClick(a, row) }"
              >
                <el-button link size="small">更多</el-button>
                <template #dropdown>
                  <el-dropdown-menu>
                    <el-dropdown-item
                      v-for="action in splitRowActions(resolveRowActions(row)).overflow"
                      :key="action.key"
                      :command="action.key"
                      :disabled="action.disabled"
                      :class="{ 'yzh-row-action-danger': action.type === 'danger' }"
                    >
                      {{ action.text }}
                    </el-dropdown-item>
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
            </template>
          </el-table-column>

          <template #empty>
            <div class="yzh-table__empty">
              <el-empty v-if="!loading && !error" :description="emptyText" />
              <div v-else-if="error" class="yzh-table__error">
                <i class="bi bi-exclamation-triangle"></i>
                <span>{{ error }}</span>
                <el-button text type="primary" @click="refresh">重试</el-button>
              </div>
            </div>
          </template>
        </el-table>
      </div>
    </div>

    <!-- 分页 -->
    <div v-if="showPagination" class="yzh-table__pagination">
      <YzhPagination
        :page="page"
        :page-size="pageSize"
        :total="total"
        @update:page="onPageChange"
        @update:page-size="onSizeChange"
      />
    </div>
  </div>
</template>

<style lang="less" scoped>
.yzh-table {
  display: flex;
  flex-direction: column;
  background: var(--yzh-color-bg-card, #fff);
  border-radius: 0;
  overflow: hidden;
  height: 100%;
}

/* 表格容器 - 白色背景，与外部卡片一致 */
.yzh-table__wrapper {
  flex: 1;
  min-height: 0;
  padding: 16px;
  overflow: hidden;
  background: #fff;
}

/* 无内部 padding 模式（用于嵌套场景，但仍保留一定内边距） */
.yzh-table__wrapper--no-padding {
  padding: 16px;
}

.yzh-table__body {
  height: 100%;
  overflow: hidden;
  background: #fff;
}

.yzh-table__pagination {
  padding: 12px 16px;
  border-top: 1px solid var(--yzh-color-border-light, #ebeef5);
  display: flex;
  justify-content: flex-end;
  background: #fff;
}

.yzh-table__empty {
  padding: 40px 0;
}

.yzh-table__error {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  color: #f56c6c;
}

.yzh-row-action-danger {
  color: var(--el-color-danger) !important;
}

/* 列设置 popover 内层 */
.yzh-column-settings {
  &__header {
    font-size: 14px;
    font-weight: 600;
    color: #303133;
    padding-bottom: 8px;
    border-bottom: 1px solid #ebeef5;
    margin-bottom: 8px;
  }

  &__body {
    max-height: 300px;
    overflow-y: auto;
  }

  &__item {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 6px 0;

    .el-checkbox {
      flex: 1;
      min-width: 0;
    }
  }

  &__sort-btn {
    flex-shrink: 0;
    margin-left: 8px;
    font-size: 12px;
    padding: 4px 8px;
    min-width: 50px;

    &.is-active {
      color: var(--el-color-primary);
      font-weight: 600;
    }
  }

  &__footer {
    display: flex;
    justify-content: center;
    gap: 12px;
    padding-top: 10px;
    margin-top: 8px;
    border-top: 1px solid #ebeef5;
  }
}

/* 全局：表格样式统一 */
:deep(.el-table) {
  border-radius: 0;
}

:deep(.el-table th.el-table__cell) {
  background: #fafafa !important;
  color: var(--yzh-color-text-primary, #303133);
  font-weight: 600;
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5) !important;
}

:deep(.el-table td.el-table__cell) {
  border-bottom: 1px solid var(--yzh-color-border-light, #f1f5f9) !important;
}

:deep(.el-table--border) {
  border: none !important;
}
</style>
