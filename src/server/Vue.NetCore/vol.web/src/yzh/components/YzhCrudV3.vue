<template>
  <div class="yzh-crud-v3">
    <!-- ====== 0. 业务工具栏（顶部额外按钮） ====== -->
    <div v-if="$slots.toolbarLeft || $slots.toolbarRight" class="yzh-crud-v3__extra-toolbar">
      <div class="yzh-crud-v3__extra-toolbar-left">
        <slot name="toolbarLeft" :selected-row="currentRow" :selected-rows="selectedRows" />
      </div>
      <div class="yzh-crud-v3__extra-toolbar-right">
        <slot name="toolbarRight" :selected-row="currentRow" :selected-rows="selectedRows" />
      </div>
    </div>

    <!-- ====== 1. 搜索区（YzhSearchBar） ====== -->
    <YzhSearchBar
      ref="searchBarRef"
      :fields="fieldConfigs"
      :mode="pageMeta.searchMode"
      :initial-values="initialSearchValues"
      @search="handleSearch"
      @reset="handleReset"
      @ready="onSearchBarReady"
    >
      <template #actions>
        <slot name="searchActions" />
      </template>
      <template #extra>
        <slot name="searchExtra" />
      </template>
    </YzhSearchBar>

    <!-- ====== 2. Grid Header 自定义插槽 ====== -->
    <slot name="gridHeader" />

    <!-- ====== 3. 工具栏（YzhToolbar） ====== -->
    <YzhToolbar
      :buttons="resolvedButtons"
      :show-column-setting="showColumnSetting"
      :column-list="columnSettingList"
      :current-sort-field="sortField"
      :current-sort-order="sortOrder"
      @button-click="onToolbarButtonClick"
      @column-visibility-change="toggleColumnVisibility"
      @sort-change="onToolbarSortChange"
      @column-reset="resetColumnSettings"
      @column-apply="applyColumnSettings"
    >
      <template #left>
        <slot name="btnLeft" :selected-row="currentRow" :selected-rows="selectedRows" />
      </template>
      <template #right>
        <slot name="btnRight" />
      </template>
    </YzhToolbar>

    <!-- ====== 4. 数据表格（YzhDataTable） ====== -->
    <YzhDataTable
      ref="tableRef"
      :data="tableData"
      :columns="tableColumns"
      :loading="loading"
      :row-key="pageMeta.keyField"
      :height="tableHeight"
      :stripe="pageMeta.stripe"
      :show-row-number="pageMeta.showRowNumber"
      :checkbox-selection="pageMeta.checkboxSelection"
      :show-action-column="pageMeta.showActionColumn"
      :default-sort-field="pageMeta.sortField"
      :default-sort-order="pageMeta.sortOrder"
      @sort-change="onTableSortChange"
      @current-change="onCurrentRowChange"
      @selection-change="onSelectionChange"
      @row-click="onRowClick"
      @row-dblclick="onRowDbClick"
      @edit="handleEdit"
      @delete="handleDelete"
    >
      <!-- 动态列插槽转发：cell-{fieldAlias} -->
      <template
        v-for="col in tableColumns"
        :key="`slot-${col.fieldAlias}`"
        #[`cell-${col.fieldAlias}`]="{ row, value }"
      >
        <slot
          :name="`cell-${col.fieldAlias}`"
          :row="row"
          :value="value"
        />
      </template>

      <!-- 操作列自定义 -->
      <template #action="{ row, index }">
        <slot name="action" :row="row" :index="index" />
      </template>

      <!-- 空数据 -->
      <template #empty>
        <slot name="empty" />
      </template>
    </YzhDataTable>

    <!-- ====== 5. 分页（YzhPagination） ====== -->
    <YzhPagination
      v-model:current-page="pagination.page"
      v-model:page-size="pagination.size"
      :total="pagination.total"
      @size-change="handleSearch"
      @current-change="loadData"
    />

    <!-- ====== 6. 编辑弹窗（YzhEditDialog） ====== -->
    <YzhEditDialog
      v-model="dialogVisible"
      :title="entityTitle"
      :mode="dialogMode"
      :fields="formFieldConfigs"
      :form-data="editForm"
      :dialog-width="pageMeta.dialogWidth"
      :dialog-max-height="pageMeta.dialogMaxHeight"
      :grid-columns="gridColumnsCount"
      :label-width="pageMeta.dialogLabelWidth"
      :saving="saving"
      @save="handleSave"
      @cancel="dialogVisible = false"
      @open="onDialogOpen"
      @field-change="onFormFieldChange"
      @field-ready="onFormFieldReady"
    >
      <template #extra="{ formData: currentData }">
        <slot name="dialogExtra" :form-data="currentData" />
      </template>
      <template #footerLeft="{ formData: currentData }">
        <slot name="dialogFooterLeft" :form-data="currentData" />
      </template>
      <template #footerRight="{ formData: currentData }">
        <slot name="dialogFooterRight" :form-data="currentData" />
      </template>
    </YzhEditDialog>
  </div>
</template>

<script setup lang="ts">
/**
 * YZH Framework V3.0 —— 数据库驱动的 CRUD 组件
 *
 * 核心设计：
 * 1. 从后端 yzh_page_config + yzh_field_config 加载 UI 配置
 * 2. 组合原子组件：YzhToolbar / YzhSearchBar / YzhDataTable / YzhPagination / YzhEditDialog
 * 3. 配置驱动渲染，零硬编码
 * 4. 支持增量更新、精确控制
 */
import {
  ref,
  reactive,
  computed,
  watch,
  onMounted,
  onUnmounted,
  nextTick,
  getCurrentInstance,
} from 'vue'
import type { IYzhPageUIConfig, IYzhPageMeta, IYzhFieldConfig, IYzhColumnConfig } from '../types/YZHV3Config'
import { loadPageConfig } from '../core/YZHConfigLoader'
import { YZHBaseApiClient } from '../core/YZHBaseApiClient'

// 子组件
import YzhToolbar from './YzhToolbar.vue'
import type { YzhButtonConfig } from './YzhToolbar.vue'
import YzhSearchBar from './YzhSearchBar.vue'
import YzhDataTable from './YzhDataTable.vue'
import type { YzhTableColumn } from './YzhDataTable.vue'
import YzhPagination from './YzhPagination.vue'
import YzhEditDialog from './YzhEditDialog.vue'

// ============================================================
// Props & Emits
// ============================================================
const props = withDefaults(defineProps<{
  /** 页面唯一标识（对应 yzh_page_config.page_key） */
  pageKey: string
  /** API 前缀 */
  apiPrefix?: string
  /** 是否显示列设置 */
  showColumnSetting?: boolean
  /** Grid 表单列数 */
  gridColumnsCount?: number
  /** 外部过滤条件 */
  externalFilter?: Array<{ name: string; value: any; cond?: string }>
  /** 初始搜索值 */
  initialSearchValues?: Record<string, any>
}>(), {
  apiPrefix: '/api/yzh-page-config',
  showColumnSetting: true,
  gridColumnsCount: 2,
  externalFilter: () => [],
  initialSearchValues: () => ({}),
})

const emit = defineEmits<{
  (e: 'ready', instance: any): void
  (e: 'rowClick', row: any): void
  (e: 'selectionChange', rows: any[]): void
}>()

const { proxy } = getCurrentInstance()

// ============================================================
// 核心状态
// ============================================================

/** 页面配置（从数据库加载） */
const pageConfig = ref<IYzhPageUIConfig | null>(null)
const pageMeta = computed<IYzhPageMeta>(() => pageConfig.value?.pageMeta ?? defaultPageMeta())
const fieldConfigs = computed<IYzhFieldConfig[]>(() => pageConfig.value?.fieldConfigs ?? [])

/** 默认页面元数据（加载中或失败时的回退） */
function defaultPageMeta(): IYzhPageMeta {
  return {
    pageKey: props.pageKey,
    pageTitle: '',
    entityName: '',
    tableName: '',
    controllerName: '',
    keyField: 'Id',
    keyFieldType: 'number',
    sortField: 'Id',
    sortOrder: 'desc',
    dialogWidth: 800,
    dialogMaxHeight: '60vh',
    dialogLabelWidth: 120,
    rowHeight: 'default',
    stripe: true,
    showRowNumber: false,
    searchMode: 'fixed',
    visibleButtons: ['add', 'refresh', 'batchDelete'],
    showActionColumn: true,
    checkboxSelection: true,
    incrementalUpdate: true,
  }
}

// ============================================================
// API 客户端
// ============================================================
let apiClient: YZHBaseApiClient<any, any> | null = null

function initApiClient() {
  if (!pageConfig.value) return
  apiClient = new YZHBaseApiClient<any, any>({
    keyField: pageMeta.value.keyField as any,
    keyType: pageMeta.value.keyFieldType || 'number',
    defaultSortField: (pageMeta.value.sortField || 'Id') as any,
    defaultSortOrder: pageMeta.value.sortOrder || 'desc',
    controllerName: pageMeta.value.controllerName,
    tableName: pageMeta.value.tableName,
    apiPrefix: '/api/',
  }, proxy as any)
}

// ============================================================
// 表格数据 & 分页
// ============================================================
const tableRef = ref()
const loading = ref(false)
const tableData = ref<any[]>([])
const pagination = reactive({ page: 1, size: 20, total: 0 })

/** 当前排序 */
const sortField = ref(pageMeta.value.sortField)
const sortOrder = ref<'asc' | 'desc'>(pageMeta.value.sortOrder)

/** 表格高度自适应 */
const tableHeight = ref(500)

// ============================================================
// 行选择
// ============================================================
const currentRow = ref<any>(null)
const selectedRows = ref<any[]>([])

// ============================================================
// 弹窗状态
// ============================================================
const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const editForm = reactive<Record<string, any>>({})
const saving = ref(false)
let editingRow: any = null

// ============================================================
// 搜索栏
// ============================================================
const searchBarRef = ref()

// ============================================================
// 计算属性：从 fieldConfigs 派生各类子配置
// ============================================================

/** 实体标题（用于弹窗标题） */
const entityTitle = computed(() => pageMeta.value.pageTitle || '')

/** 表格列配置（xsFlag=true 的字段） */
const tableColumns = computed<YzhTableColumn[]>(() => {
  return fieldConfigs.value
    .filter(f => f.xsFlag && f.controlType !== 'hidden')
    .sort((a, b) => a.columnSxh - b.columnSxh)
    .map(f => ({
      fieldAlias: f.fieldAlias,
      fieldName: f.fieldName,
      columnTitle: f.columnTitle || f.formTitle,
      columnWidth: f.columnWidth,
      columnFixed: f.columnFixed,
      sortable: f.sortable,
      showOverflow: f.showOverflow,
      align: f.align,
      visible: true,
      order: f.columnSxh,
      _raw: f,
    }))
})

/** 表单字段配置（用于编辑弹窗，排除 hidden 和 groupIndex=9） */
const formFieldConfigs = computed<IYzhFieldConfig[]>(() => {
  return fieldConfigs.value.filter(f => {
    if (f.groupIndex === 9) return false
    return true // hidden 也保留用于提交
  })
})

/** 工具栏按钮配置 */
const resolvedButtons = computed<(YzhButtonConfig | string)[]>(() => {
  const buttons: (YzhButtonConfig | string)[] = []
  for (const btnKey of pageMeta.value.visibleButtons) {
    buttons.push(btnKey)
  }
  return buttons
})

/** 列设置面板的列列表 */
const columnSettingList = computed(() =>
  tableColumns.value.map(col => ({
    fieldAlias: col.fieldAlias,
    fieldName: col.fieldName,
    title: col.columnTitle,
    hidden: !col.visible,
    sortable: col.sortable,
  }))
)

// ============================================================
// 核心方法：加载数据
// ============================================================
async function loadData() {
  loading.value = true
  try {
    const param: any = {
      page: pagination.page,
      rows: pagination.size,
      sort: sortField.value || pageMeta.value.sortField,
      order: sortOrder.value === 'asc' ? 'asc' : 'desc',
      filter: buildFilter(),
    }

    if (!apiClient) initApiClient()
    const res = await apiClient?.getPageData(param)

    // 解析返回数据
    let rows: any[] = []
    if (res?.rows && Array.isArray(res.rows)) {
      rows = res.rows
      pagination.total = res.total ?? rows.length ?? 0
    } else if (Array.isArray(res)) {
      rows = res
      pagination.total = rows.length
    }

    tableData.value = rows
  } catch (e: any) {
    console.error('[YzhCrudV3] loadData error:', e)
    proxy?.$message?.error?.(e?.message || '加载数据失败')
  } finally {
    loading.value = false
  }
}

/** 构建查询条件 */
function buildFilter(): any[] {
  const filter: any[] = []

  // 外部过滤条件
  props.externalFilter.forEach(f => {
    filter.push({ Name: f.name, Value: String(f.value), DisplayType: f.cond || '==' })
  })

  return filter
}

// ============================================================
// 搜索
// ============================================================
function handleSearch(params: Record<string, any>) {
  pagination.page = 1
  // 合并搜索参数到过滤条件
  Object.keys(params).forEach(k => {
    // TODO: 将搜索参数转换为 Vol 格式的 filter
  })
  loadData()
}

function handleReset() {
  pagination.page = 1
  loadData()
}

// ============================================================
// 排序
// ============================================================
function onTableSortChange({ prop, order }: { prop: string; order: string | null }) {
  sortField.value = prop || pageMeta.value.sortField
  sortOrder.value = order === 'ascending' ? 'asc' : 'desc'
  loadData()
}

function onToolbarSortChange(fieldName: string) {
  if (sortField.value === fieldName) {
    sortOrder.value = sortOrder.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortField.value = fieldName
    sortOrder.value = 'asc'
  }
  loadData()
}

// ============================================================
// 行操作
// ============================================================
function onCurrentRowChange(row: any) {
  currentRow.value = row
  emit('rowClick', row)
}

function onSelectionChange(rows: any[]) {
  selectedRows.value = rows
  emit('selectionChange', rows)
}

function onRowClick(row: any, column: any, event: Event) {
  currentRow.value = row
}

function onRowDbClick(row: any) {
  handleEdit(row)
}

// ============================================================
// 新增/编辑/删除/保存
// ============================================================
async function handleAdd() {
  dialogMode.value = 'add'
  resetEditForm({})
  dialogVisible.value = true
}

async function handleEdit(row: any) {
  dialogMode.value = 'edit'
  editingRow = row
  resetEditForm({ ...row })
  dialogVisible.value = true
}

async function handleDelete(row: any) {
  if (!apiClient) initApiClient()
  const id = row[pageMeta.value.keyField]
  try {
    const res = await apiClient?.del([id])
    if (res?.status) {
      proxy?.$message?.success?.('删除成功')
      loadData()
    } else {
      proxy?.$message?.error?.(res?.message || '删除失败')
    }
  } catch (e: any) {
    proxy?.$message?.error?.(e?.message || '删除异常')
  }
}

async function handleSave(formData: Record<string, any>) {
  if (!apiClient) initApiClient()
  saving.value = true
  try {
    const isAdd = dialogMode.value === 'add'

    if (isAdd) {
      const res = await apiClient?.add(formData)
      if (res?.status) {
        proxy?.$message?.success?.('新增成功')
        dialogVisible.value = false
        loadData()
      } else {
        proxy?.$message?.error?.(res?.message || '新增失败')
      }
    } else {
      // 合并主键
      formData[pageMeta.value.keyField] = editingRow?.[pageMeta.value.keyField]
      const res = await apiClient?.update(formData)
      if (res?.status) {
        proxy?.$message?.success?.('保存成功')
        dialogVisible.value = false
        loadData()
      } else {
        proxy?.$message?.error?.(res?.message || '保存失败')
      }
    }
  } catch (e: any) {
    console.error('[YzhCrudV3] save error:', e)
    proxy?.$message?.error?.(e?.message || '操作失败')
  } finally {
    saving.value = false
  }
}

/** 重置编辑表单 */
function resetEditForm(data: any) {
  Object.keys(editForm).forEach(k => delete editForm[k])
  if (data) {
    Object.keys(data).forEach(k => { editForm[k] = data[k] })
  }
  // 设置默认值
  formFieldConfigs.value.forEach(f => {
    if (f.defaultValue !== undefined && f.defaultValue !== '' && !(f.fieldName in editForm)) {
      editForm[f.fieldName] = f.defaultValue
    }
  })
}

// ============================================================
// 工具栏按钮处理
// ============================================================
function onToolbarButtonClick(key: string) {
  switch (key) {
    case 'add': return handleAdd()
    case 'refresh': return handleRefresh()
    case 'batchDelete': return handleBatchDelete()
    case 'export': return handleExport()
    case 'import': return handleImport()
  }
}

function handleRefresh() {
  pagination.page = 1
  loadData()
}

async function handleBatchDelete() {
  if (!selectedRows.value.length) {
    proxy?.$message?.warning?.('请先选择要删除的行')
    return
  }
  const ids = selectedRows.value.map(r => r[pageMeta.value.keyField])
  if (!confirm(`确认删除选中的 ${ids.length} 条记录？`)) return

  if (!apiClient) initApiClient()
  try {
    const res = await apiClient?.del(ids)
    if (res?.status) {
      proxy?.$message?.success?.(`成功删除 ${ids.length} 条`)
      loadData()
    } else {
      proxy?.$message?.error?.(res?.message || '删除失败')
    }
  } catch (e: any) {
    proxy?.$message?.error?.(e?.message || '删除异常')
  }
}

function handleExport() {
  proxy?.$message?.info?.('导出功能开发中...')
}

function handleImport() {
  proxy?.$message?.info?.('导入功能开发中...')
}

// ============================================================
// 列设置
// ============================================================
function toggleColumnVisibility(fieldAlias: string, visible: boolean) {
  const col = tableColumns.value.find(c => c.fieldAlias === fieldAlias)
  if (col) col.visible = visible
}

function resetColumnSettings() {
  tableColumns.value.forEach(c => { c.visible = true })
  sortField.value = pageMeta.value.sortField
  sortOrder.value = pageMeta.value.sortOrder
}

function applyColumnSettings() {
  loadData()
}

// ============================================================
// 弹窗回调
// ============================================================
function onDialogOpen() {
  // 可在此处加载额外数据
}

function onFormFieldChange(fieldName: string, value: any) {
  // 字段变化时的业务逻辑
}

function onFormFieldReady(alias: string, instance: any) {
  // 字段实例注册
}

function onSearchBarReady(instance: any) {
  // 搜索栏就绪
}

// ============================================================
// 表格高度自适应
// ============================================================
function calcTableHeight() {
  nextTick(() => {
    const el = tableRef.value?.table?.$el
    if (el) {
      const top = el.getBoundingClientRect().top
      const winHeight = window.innerHeight
      tableHeight.value = Math.max(300, winHeight - top - 80)
    }
  })
}

// ============================================================
// 初始化
// ============================================================
onMounted(async () => {
  try {
    // 1. 从数据库加载 UI 配置
    pageConfig.value = await loadPageConfig(props.pageKey)

    // 2. 初始化 API 客户端
    initApiClient()

    // 3. 计算表格高度
    calcTableHeight()
    window.addEventListener('resize', calcTableHeight)

    // 4. 加载数据
    await loadData()

    // 5. 暴露实例
    emit('ready', exposedApi)
  } catch (e: any) {
    console.error('[YzhCrudV3] 初始化失败:', e)
    // 使用默认配置继续运行
    pageConfig.value = {
      pageMeta: defaultPageMeta(),
      fieldConfigs: [],
    }
    initApiClient()
    await loadData()
  }
})

onUnmounted(() => {
  window.removeEventListener('resize', calcTableHeight)
})

// ============================================================
// 对外暴露的实例 API
// ============================================================
const exposedApi = {
  get table() { return tableRef.value },
  get searchBar() { return searchBarRef.value },
  get currentPage() { return pagination.page },
  get total() { return pagination.total },
  get selectedRow() { return currentRow.value },
  get selectedRows() { return selectedRows.value },
  getData: () => tableData.value,
  refresh: handleRefresh,
  search: handleSearch,
  add: handleAdd,
  edit: handleEdit,
  getApi: () => apiClient,
  getPageConfig: () => pageConfig.value,
  getFieldInstance: (alias: string) => null, // TODO: 从 EditDialog 获取
}

defineExpose(exposedApi)
</script>

<style lang="scss">
/* 注意：不使用 scoped！因为 el-dialog 渲染在 body 层级 */

.yzh-crud-v3 {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  position: relative;
  padding: 0 16px;
  box-sizing: border-box;

  &__extra-toolbar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 8px 12px 4px;

    &-left,
    &-right {
      display: flex;
      gap: 8px;
      align-items: center;
    }
  }
}
</style>
