<script setup lang="ts" generic="T extends Record<string, any> = any">
/**
 * YzhTable - 自研表格组件
 *
 * 特性：
 * - 加载/空/错误三态
 * - 排序、分页、多选
 * - 工具栏（列设置）
 * - 搜索栏联动
 * - 插槽扩展（#column-prop）
 */
import { ElMessage } from 'element-plus'
import { computed, onMounted, reactive, ref } from 'vue'
import YzhPagination from '../layout/YzhPagination.vue'
import YzhSearchBar from '../layout/YzhSearchBar.vue'
import YzhToolbar from '../layout/YzhToolbar.vue'
import type {
  DefaultSort,
  Page,
  PageParams,
  SearchField,
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
    showPagination?: boolean
    pageSize?: number
    defaultSort?: DefaultSort
    height?: string | number
    rowKey?: string
    emptyText?: string
    toolbar?: boolean | YzhTableToolbar
    searchMaxFields?: number
    /** 是否禁用内部 padding（用于嵌套在卡片/TreeTable 中时避免双层 padding） */
    noPadding?: boolean
    /** 行自定义操作按钮：{ 方法名: 显示文字 }（后端自动注入） */
    rowActionButtons?: Record<string, string>
    /** 行操作按钮是否使用 link 样式（默认 true） */
    rowActionLink?: boolean
  }>(),
  {
    selectable: false,
    showPagination: true,
    pageSize: 20,
    rowKey: 'id',
    emptyText: '暂无数据',
    toolbar: true,
    searchMaxFields: 2,
    noPadding: false,
    rowActionButtons: () => ({}),
    rowActionLink: true
  }
)

const emit = defineEmits<{
  (e: 'selection-change', rows: T[]): void
  (e: 'row-click', row: T, index: number): void
  (e: 'refresh'): void
  (e: 'row-action', action: string, row: T): void
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

/** 是否显示动态行操作列（当 rowActionButtons 有值且 columns 中无 actions 列时自动追加） */
const showDynamicActionColumn = computed(() =>
  Object.keys(props.rowActionButtons).length > 0 &&
  !props.columns.some((c) => c.prop === 'actions')
)

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
const selectable = computed(() => props.selectable)

// 工具栏配置
const toolbarConfig = computed<YzhTableToolbar>(() => {
  if (props.toolbar === false) return {}
  if (props.toolbar === true) return { columnSetting: true }
  return props.toolbar
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
    }
    const res: Page<T> = await props.dataLoader(params)
    rows.value = res.rows || []
    total.value = res.total || 0

    // 恢复选择状态：新数据中匹配之前选中 key 的行自动勾选
    if (selectedKeys.size > 0) {
      const restored: T[] = []
      for (const row of rows.value) {
        if (selectedKeys.has((row as any)[props.rowKey])) {
          restored.push(row)
        }
      }
      selectedRows.value = restored
    }
  } catch (e: any) {
    error.value = e?.message || '数据加载失败'
    rows.value = []
    total.value = 0
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

/**
 * 根据操作 key 返回按钮类型
 */
function getRowActionType(key: string): 'primary' | 'success' | 'warning' | 'danger' | 'info' {
  const map: Record<string, 'primary' | 'success' | 'warning' | 'danger' | 'info'> = {
    disable: 'warning',
    enable: 'success',
    delete: 'danger',
    edit: 'primary',
  }
  return map[key] || 'primary'
}

/**
 * 处理行操作按钮点击
 */
function handleRowAction(action: string, row: T) {
  emit('row-action', action, row)
}

/**
 * 刷新
 */
function refresh() {
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
    const existing = new Set(selectedRows.value)
    for (const row of rows.value) {
      if (matchFn(row) && !existing.has(row)) {
        selectedRows.value.push(row)
      }
    }
  } else {
    // 取消勾选匹配的行
    selectedRows.value = selectedRows.value.filter((r) => !matchFn(r))
  }
  emit('selection-change', [...selectedRows.value])
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
  }
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
    <YzhToolbar v-if="Object.keys(toolbarConfig).length > 0">
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
          v-loading="loading"
          :data="rows"
          :row-key="rowKey"
          :height="height ? '100%' : undefined"
          stripe
          border
          @selection-change="onSelectionChange"
          @sort-change="onSortChange"
          @row-click="onRowClick"
        >
          <el-table-column v-if="selectable" type="selection" width="48" :reserve-selection="false" />

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
                <template v-else>
                  {{ col.formatter ? col.formatter(row[col.prop], row, $index) : row[col.prop] }}
                </template>
              </template>
            </el-table-column>
          </template>

          <!-- 动态行操作列（由 rowActionButtons 自动驱动） -->
          <el-table-column
            v-if="showDynamicActionColumn"
            label="操作"
            :width="Object.keys(rowActionButtons).length * 70"
            fixed="right"
            align="center"
          >
            <template #default="{ row }">
              <el-button
                v-for="(text, key) in rowActionButtons"
                :key="key"
                :link="rowActionLink"
                size="small"
                :type="getRowActionType(key)"
                @click="handleRowAction(key, row)"
              >
                {{ text }}
              </el-button>
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

<style scoped>
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
