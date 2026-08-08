<template>
  <div class="yzh-crud-table">
    <!-- ====== 0. 业务工具栏（顶部额外按钮） ====== -->
    <div v-if="$slots.toolbarLeft || $slots.toolbarRight" class="yzh-toolbar-extra">
      <div class="yzh-toolbar-extra__left">
        <slot name="toolbarLeft" :selectedRow="singleSelectedRow" :selectedRows="selectedRows" :editMode="editMode" />
      </div>
      <div class="yzh-toolbar-extra__right">
        <slot name="toolbarRight" :selectedRow="singleSelectedRow" :selectedRows="selectedRows" :editMode="editMode" />
      </div>
    </div>

    <!-- ====== 1. 搜索区 ====== -->
    <div v-if="searchMode !== 'hidden' && searchableColumns.length" class="yzh-search-bar" :class="{ 'is-fixed': searchMode === 'fixed' }">
      <el-form :model="searchForm" inline label-width="auto" size="default" class="yzh-search-form">
        <el-form-item
          v-for="col in searchableColumns"
          :key="col.field"
          :label="col.title || col.field"
        >
          <!-- select 类型 -->
          <el-select
            v-if="col.type === 'select'"
            v-model="searchForm[col.field]"
            :placeholder="`请选择${col.title || ''}`"
            clearable
            filterable
            style="width: 180px"
            @change="onSearchChange"
          >
            <el-option
              v-for="item in (col.data || [])"
              :key="item.key"
              :label="item.value"
              :value="item.key"
            />
          </el-select>
          <!-- input / 其他类型默认用 input -->
          <el-input
            v-else
            v-model="searchForm[col.field]"
            :placeholder="col.placeholder || `请输入${col.title || ''}`"
            clearable
            style="width: 200px"
            @keyup.enter="handleSearch"
          />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" icon="Search" @click="handleSearch">查询</el-button>
          <el-button icon="RefreshRight" @click="handleResetSearch">重置</el-button>
          <el-button v-if="searchMode === 'togglable'" link type="info" @click="searchExpanded = !searchExpanded">
            {{ searchExpanded ? '收起' : '展开' }}
            <el-icon><ArrowUp v-if="searchExpanded" /><ArrowDown v-else /></el-icon>
          </el-button>
        </el-form-item>
      </el-form>
    </div>

    <!-- ====== 2. Grid Header 自定义插槽 ====== -->
    <slot name="gridHeader" />

    <!-- ====== 3. 工具栏（YZH 按钮 + 列设置） ====== -->
    <div class="yzh-btn-bar">
      <!-- 左侧：操作按钮组 -->
      <div class="yzh-btn-bar__left">
        <el-button
          v-for="btn in visibleButtons"
          :key="btn.key"
          :type="btn.type || 'default'"
          :size="btn.size || 'small'"
          @click="onToolbarClick(btn.key)"
        >
          <el-icon v-if="btn.icon"><component :is="btn.icon" /></el-icon>
          {{ btn.label }}
        </el-button>
        <slot name="btnLeft" :selectedRow="singleSelectedRow" :editMode="editMode" />
      </div>
      <!-- 右侧：列筛选 + 列排序 -->
      <div class="yzh-btn-bar__right">
        <el-popover trigger="click" placement="bottom-end" :width="220">
          <template #reference>
            <el-button size="small" title="列设置">
              <el-icon><Setting /></el-icon>
              列设置
            </el-button>
          </template>
          <div class="yzh-column-settings">
            <div class="yzh-column-settings__header">列筛选与排序</div>
            <div class="yzh-column-settings__body">
              <div
                v-for="col in sortableFilterableColumns"
                :key="col.field"
                class="yzh-column-settings__item"
              >
                <el-checkbox
                  :model-value="!hiddenColumnFields.has(col.field) && !col.hidden"
                  @change="(val: boolean) => toggleColumnVisibility(col, val)"
                >
                  {{ col.title || col.field }}
                </el-checkbox>
                <el-button
                  size="small"
                  link
                  type="primary"
                  :class="{ 'is-active': currentSortProp.value === col.field }"
                  @click="toggleSort(col.field)"
                >
                  {{ getSortIcon(col.field) }}
                </el-button>
              </div>
            </div>
            <div class="yzh-column-settings__footer">
              <el-button size="small" @click="resetColumnSettings">重置</el-button>
              <el-button size="small" type="primary" @click="applyColumnSettings">确定</el-button>
            </div>
          </div>
        </el-popover>
      </div>
    </div>

    <!-- ====== 4. 数据表格（Element Plus 原生 el-table） ====== -->
    <el-table
      ref="tableRef"
      v-loading="loading"
      :data="tableData"
      :border="true"
      :stripe="true"
      :row-key="schema.keyField"
      :default-sort="{ prop: defaultSortField, order: sortOrderMap[defaultSortOrder] || 'descending' }"
      :height="tableHeight"
      style="width: 100%"
      highlight-current-row
      @sort-change="onSortChange"
      @current-change="onCurrentRowChange"
      @selection-change="onSelectionChange"
      @row-dblclick="onRowDbClick"
    >
      <!-- 多选列（始终显示，支持批量删除） -->
      <el-table-column type="selection" width="48" align="center" reserve-selection />

<!-- 数据列 -->
<el-table-column
v-for="col in actualVisibleColumns"
:key="col.field"
        :prop="col.field"
        :label="col.title || col.field"
        :width="col.width || (col.width === 0 ? 0 : undefined)"
        :min-width="col.minWidth"
        :sortable="col.sortable !== false ? 'custom' : false"
        :align="col.align || 'left'"
        :show-overflow-tooltip="col.showOverflowTooltip !== false"
        :fixed="col.fixed"
        :formatter="col.formatter || undefined"
      >
        <!-- 字典值渲染：Status 等字段用 Tag 显示 -->
        <template #default="{ row }">
          <!-- 自定义 render 函数优先 -->
          <template v-if="col.render">
            <component :is="() => col.render($createElement, { row, column: col })" />
          </template>
          <!-- 字典 Tag 色映射 -->
          <el-tag
            v-else-if="getStatusTagColor(col.field)"
            :type="getTagType(row[col.field], col.field)"
            size="small"
          >
            {{ formatDictValue(row[col.field], col) }}
          </el-tag>
          <!-- 普通文本 -->
          <span v-else>{{ row[col.field] ?? '' }}</span>
        </template>
      </el-table-column>

      <!-- 操作列 -->
      <el-table-column
        v-if="showActionColumn"
        label="操作"
        width="160"
        align="center"
        fixed="right"
      >
        <template #default="{ row }">
          <el-button link type="primary" size="small" @click="handleEdit(row)">修改</el-button>
          <el-popconfirm title="确认删除？" @confirm="handleDelete(row)">
            <template #reference>
              <el-button link type="danger" size="small">删除</el-button>
            </template>
          </el-popconfirm>
        </template>
      </el-table-column>
    </el-table>

    <!-- ====== 5. 分页 ====== -->
    <div class="yzh-pagination-wrap">
      <el-pagination
        v-model:current-page="pagination.page"
        v-model:page-size="pagination.size"
        :page-sizes="[10, 20, 50, 100]"
        :total="pagination.total"
        layout="total, sizes, prev, pager, next, jumper"
        background
        @size-change="handleSearch"
        @current-change="loadData"
      />
    </div>

    <!-- ====== 6. 编辑弹窗 ====== -->
    <el-dialog
      v-model="dialogVisible"
      :title="dialogTitle"
      :width="dialogWidth"
      :close-on-click-modal="false"
      destroy-on-close
      @open="onDialogOpen"
    >
      <el-form
        ref="editFormRef"
        :model="editForm"
        :rules="editFormRules"
        label-width="120px"
        size="default"
      >
        <el-row :gutter="20">
          <el-col
            v-for="(rowCols, rowIndex) in editFormOptions"
            :key="'row-' + rowIndex"
            :span="24"
          >
            <template v-for="item in rowCols" :key="item.field">
              <!-- 隐藏字段不渲染（支持 hidden:true 或 type:'hidden'） -->
              <el-form-item
                v-if="!item.hidden && item.type !== 'hidden'"
                :label="item.title || item.field"
                :prop="item.field"
                :style="{ display: (item.hidden || item.type === 'hidden') ? 'none' : undefined }"
              >
                <!-- select -->
                <el-select
                  v-if="item.type === 'select'"
                  v-model="editForm[item.field]"
                  :placeholder="`请选择${item.title || ''}`"
                  :disabled="item.readonly || item.disabled"
                  clearable
                  filterable
                  style="width: 100%"
                >
                  <el-option
                    v-for="opt in (item.data || [])"
                    :key="opt.key"
                    :label="opt.value"
                    :value="opt.key"
                  />
                </el-select>

                <!-- textarea -->
                <el-input
                  v-else-if="item.type === 'textarea'"
                  v-model="editForm[item.field]"
                  type="textarea"
                  :rows="item.rows || 4"
                  :placeholder="item.placeholder || item.title"
                  :disabled="item.readonly || item.disabled"
                  :maxlength="item.maxlength"
                  show-word-limit
                />

                <!-- number -->
                <el-input-number
                  v-else-if="item.type === 'number' || item.type === 'decimal'"
                  v-model="editForm[item.field]"
                  :placeholder="item.placeholder || item.title"
                  :disabled="item.readonly || item.disabled"
                  :min="item.min"
                  :max="item.max"
                  :precision="item.precision"
                  controls-position="right"
                  style="width: 100%"
                />

                <!-- 默认 input -->
                <el-input
                  v-else
                  v-model="editForm[item.field]"
                  :placeholder="item.placeholder || item.title"
                  :disabled="item.readonly || item.disabled"
                  :maxlength="item.maxlength"
                  clearable
                />
              </el-form-item>
            </template>
          </el-col>
        </el-row>
      </el-form>

      <template #footer>
        <div class="dialog-footer">
          <el-button @click="dialogVisible = false">取消</el-button>
          <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
        </div>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
/**
 * YZH Framework V2.0 —— 核心单表 CRUD 基类
 *
 * 设计原则：
 * 1. 纯 Element Plus 组件，零 Vol 依赖
 * 2. v-model 双向绑定，输入即时回显
 * 3. 增量刷新：增/改/删 后局部 patch 内存，不做全表 reload
 * 4. 手动搜索：填条件 → 点查询 → 执行搜索（不自动触发）
 * 5. 接口兼容后端 Vol ApiBaseController 标准格式
 */
import {
  ref,
  reactive,
  computed,
  watch,
  onMounted,
  getCurrentInstance,
  nextTick,
  shallowRef,
} from 'vue'
import {
  Search,
  RefreshRight,
  Plus,
  Upload,
  Download,
  Delete,
  ArrowUp,
  ArrowDown,
  Setting,
} from '@element-plus/icons-vue'
import type { IYZHCrudTableProps } from '../types/YZHPageProps'
import type { IYZHEntitySchema } from '../types/YZHEntitySchema'
import { YZHBaseApiClient } from '../core/YZHBaseApiClient'
import { createDefaultLifecycles, runGuard } from '../core/YZHPageLifecycle'
import { YZHEditGuard } from '../core/YZHEditGuard'
import { useYZHEditMode } from '../composables/useYZHEditMode'
import { useYZHIncrementSync } from '../composables/useYZHIncrementSync'
import { mergeDefaultButtons } from '../presets/defaultButtons'
import { loadPageConfig } from '../core/YZHConfigLoader'

// ============================================================
// Props & Emits
// ============================================================
const props = withDefaults(defineProps<IYZHCrudTableProps<any, any>>(), {
  incrementalUpdate: true,
  searchMode: 'fixed',
  showActionColumn: true,
  dialogWidth: 960,
})

const emit = defineEmits<{
  (e: 'ready', instance: any): void
}>()

const { proxy } = getCurrentInstance()

// ============================================================
// Schema & Options 解析（V2.5：支持数据库配置驱动）
// ============================================================
/** options() 返回的完整配置对象 */
const opts = computed(() => {
  const raw = typeof props.options === 'function' ? props.options() : props.options
  return raw || {}
})

const schema = computed(() => props.schema as IYZHEntitySchema<any, any>)

const tableConfig = computed(() => opts.value.table || {})

// ---- V2.5 数据库配置状态 ----
/** 后端加载的页面元数据 */
const dbPageConfig = ref<any>(null)
/** 后端加载的字段配置列表 */
const dbFieldConfigs = ref<any[]>([])
/** 是否已完成后端配置加载 */
const dbConfigLoaded = ref(false)

/**
 * V2.5 核心方法：从后端（或 Store）加载页面 UI 配置
 * 优先级：Vuex Store > 单页 API
 */
async function loadDbPageConfig(): Promise<boolean> {
  if (!props.pageKey) return false

  try {
    console.log(`[YzhCrudTable] 🔄 正在加载配置: ${props.pageKey}`)
    const config = await loadPageConfig(props.pageKey)
    if (config) {
      dbPageConfig.value = config.pageMeta || null
      dbFieldConfigs.value = config.fieldConfigs || []
      dbConfigLoaded.value = true
      console.log(`[YzhCrudTable] ✅ 配置加载成功: ${dbFieldConfigs.value.length} 个字段`)
      return true
    }
    return false
  } catch (e: any) {
    console.warn(`[YzhCrudTable] ⚠️ 配置加载失败，回退到 options.js: ${e?.message}`)
    dbConfigLoaded.value = true // 标记已尝试过，避免重复请求
    return false
  }
}

/**
 * V2.5：从数据库字段配置构建 columns 数组
 * 仅包含 xs_flag=true 的字段
 */
function buildColumnsFromDbConfig(): any[] {
  const fields = dbFieldConfigs.value
  if (!fields.length) return []

  return fields
    .filter((f: any) => !!f.xsFlag && f.controlType !== 'hidden' && f.controlType !== 'none' && f.controlType !== 'readonly')
    .sort((a: any, b: any) => (a.columnSxh || 0) - (b.columnSxh || 0))
    .map((f: any) => ({
      field: f.fieldName,
      title: f.columnTitle,
      width: f.columnWidth || 120,
      align: f.align || 'left',
      sortable: !!f.sortable,
      showOverflow: !!f.showOverflow,
      render: undefined, // 可由 lifecycles.onRenderColumn 覆盖
    }))
}

/**
 * V2.5：从数据库字段配置构建 editFormOptions（分组行格式）
 * 仅包含 bc_flag=true 且 controlType 不是 hidden/none 的字段
 */
function buildEditFormFromDbConfig(): any[][] {
  const fields = dbFieldConfigs.value
  if (!fields.length) return []

  // 按 groupIndex 分组
  const groups: Record<number, any[]> = {}
  fields.forEach((f: any) => {
    // bcFlag is 0/1 from backend (byte type), not boolean
    // Filter out: bc_flag=0 (not saveable), hidden, none, readonly (display-only audit fields)
    if (!f.bcFlag || f.controlType === 'hidden' || f.controlType === 'none' || f.controlType === 'readonly') return
    const gi = f.groupIndex ?? 0
    if (!groups[gi]) groups[gi] = []
    groups[gi].push(f)
  })

  // 按组号排序，每组内按 gridRow/gridCol 排序
  const sortedGroups = Object.keys(groups)
    .map(Number)
    .sort((a, b) => a - b)
    .map((gi) =>
      groups[gi]
        .sort((a: any, b: any) => {
          if (a.gridRow !== b.gridRow) return (a.gridRow || 0) - (b.gridRow || 0)
          return (a.gridCol || 0) - (b.gridCol || 0)
        })
        .map((f: any) => ({
          field: f.fieldName,
          title: f.formTitle || f.columnTitle,
          type: f.controlType || 'input',
          // required is 0/1 from backend (byte type), convert to boolean
          required: !!f.required,
          maxlength: f.maxlength || 200,
          placeholder: f.placeholder || `请输入${f.formTitle || f.columnTitle}`,
          dataKey: f.dataKey || null,
          colSize: f.gridColSpan || 1, // Element Plus 栅格占位
        }))
    )

  return sortedGroups.length ? sortedGroups : [[]]
}

/**
 * V2.5：从数据库字段配置构建 searchFormOptions
 * 仅包含 search_flag=true 的字段
 */
function buildSearchFormFromDbConfig(): any[][] {
  const fields = dbFieldConfigs.value
  if (!fields.length) return []

  const searchFields = fields.filter(
    (f: any) => !!f.searchFlag && f.controlType !== 'hidden' && f.controlType !== 'none' && f.controlType !== 'readonly'
  )

  if (!searchFields.length) return [[]]

  return [
    searchFields.map((f: any) => ({
      field: f.fieldName,
      title: f.searchTitle || f.formTitle || f.columnTitle,
      type: f.searchControlType || f.controlType || 'input',
      placeholder: f.searchPlaceholder || `请输入${f.searchTitle || f.formTitle || f.columnTitle}`,
      width: f.searchWidth || 180,
      dataKey: f.dataKey || null,
    })),
  ]
}

// ---- V2.5：优先使用数据库配置，回退到 options.js ----
const columns = computed(() => {
  if (dbConfigLoaded.value && dbFieldConfigs.value.length > 0) {
    const dbCols = buildColumnsFromDbConfig()
    if (dbCols.length > 0) return dbCols
  }
  return opts.value.columns || []
})

const editFormOptions = computed(() => {
  if (dbConfigLoaded.value && dbFieldConfigs.value.length > 0) {
    const dbForm = buildEditFormFromDbConfig()
    if (dbForm.length > 0 && dbForm.some((row: any[]) => row.length > 0)) return dbForm
  }
  return opts.value.editFormOptions || []
})

const searchFormOptions = computed(() => {
  if (dbConfigLoaded.value && dbFieldConfigs.value.length > 0) {
    const dbSearch = buildSearchFormFromDbConfig()
    if (dbSearch.length > 0 && dbSearch.some((row: any[]) => row.length > 0)) return dbSearch
  }
  return opts.value.searchFormOptions || []
})
const boxOptions = computed(() => opts.value.boxOptions || {})

// 默认排序
const defaultSortField = computed(() => tableConfig.value.sortName || schema.value.defaultSortField)
const defaultSortOrder = computed(() => (tableConfig.value.sortOrder as any) || schema.value.defaultSortOrder)
const sortOrderMap: Record<string, string> = { asc: 'ascending', desc: 'descending' }

// ============================================================
// API 客户端
// ============================================================
const api = new YZHBaseApiClient(schema.value, proxy as any)
const guard = new YZHEditGuard(proxy as any)

// ============================================================
// 生命周期钩子（合并默认空实现）
// ============================================================
const lc = Object.assign(createDefaultLifecycles<any, any>(), props.lifecycles || {})

// ============================================================
// 编辑模式状态
// ============================================================
const {
  editMode,
  selectedRows,
  singleSelectedRow,
  toggleEditMode,
  setSelectedRows,
  setSingleSelected,
  clearSelected,
  selectedRowObjects,
  hasSelection,
} = useYZHEditMode(schema.value, {
  onEditModeChange: (v) => lc.onEditModeChange?.(v),
  onSelectChange: (rows) => lc.onRowSelect?.(singleSelectedRow.value, rows),
})

// ============================================================
// 表格数据 & 分页
// ============================================================
const tableRef = ref()
const loading = ref(false)
const tableData = ref<any[]>([])
const pagination = reactive({ page: 1, size: 20, total: 0 })

// 当前排序状态
const currentSortProp = ref(defaultSortField.value)
const currentSortOrder = ref(defaultSortOrder.value)

// 表格高度（自适应）
const tableHeight = ref(500)

// ============================================================
// 增量同步
// ============================================================
const incSync = useYZHIncrementSync({
  enabled: computed(() => !!props.incrementalUpdate),
  schema,
  pageRows: tableData,
  currentSortField: currentSortProp,
  currentSortOrder: currentSortOrder,
})

// ============================================================
// 搜索区
// ============================================================
const searchExpanded = ref(true)
const searchForm = reactive<Record<string, any>>({})

/** 从 searchFormOptions 提取可搜索的列 */
const searchableColumns = computed(() => {
  const flat: any[] = []
  ;(searchFormOptions.value || []).forEach((row: any[]) => {
    ;(row || []).forEach((col: any) => {
      if (col && col.field && !col.hidden) flat.push(col)
    })
  })
  return flat
})

/** 初始化搜索表单默认值 */
function initSearchForm() {
  // 清空 searchForm
  Object.keys(searchForm).forEach((k) => delete searchForm[k])
  // 用 searchFormFields 的初始值填充
  const fields = opts.value.searchFormFields || {}
  Object.keys(fields).forEach((k) => {
    searchForm[k] = fields[k] ?? ''
  })
}

function handleSearch() {
  pagination.page = 1
  loadData()
}

function handleResetSearch() {
  initSearchForm()
  pagination.page = 1
  loadData()
}

function onSearchChange() {
  // 选择器变化时不自动搜索，等用户点查询
}

// ============================================================
// 可见列过滤（隐藏列 + 无 title 的内部列）
// ============================================================
const visibleColumns = computed(() =>
  columns.value.filter(
    (c: any) => !c.hidden && c.field && c.field !== '__yzh_action' && c.title
  )
)

// ============================================================
// 工具栏按钮
// ============================================================
const visibleButtons = computed(() => mergeDefaultButtons(props.buttons))

function onToolbarClick(key: string) {
  switch (key) {
    case 'add':
      return handleAdd()
    case 'refresh':
      return handleRefresh()
    case 'batchDelete':
      return handleBatchDelete()
    case 'export':
      return handleExport()
    case 'import':
      return handleImport()
  }
}

// ============================================================
// 数据加载核心方法
// ============================================================
async function loadData() {
  loading.value = true
  try {
    // 构建适配后端 Vol PageDataOptions 格式的参数
    const param: any = {
      page: pagination.page,
      rows: pagination.size,
      sort: currentSortProp.value || defaultSortField.value,
      order: currentSortOrder.value === 'asc' ? 'asc' : 'desc',
      filter: buildFilter(),
    }

    // onLoadBefore 钩子
    const ok = await runGuard(lc.onLoadBefore, [param])
    if (!ok) { loading.value = false; return }

    const res = await api.getPageData(param)

    // 后端返回格式：PageGridData<T> { status, msg, total, rows, summary, extra }
    let rows: any[] = []
    if (res?.rows && Array.isArray(res.rows)) {
      // 标准 Vol PageGridData 返回格式
      rows = res.rows
      pagination.total = res.total ?? rows.length ?? 0
    } else if (res?.status && Array.isArray(res?.data)) {
      // 兼容：部分接口可能包装在 data 中
      rows = res.data
      pagination.total = res.total ?? rows.length ?? 0
    } else if (Array.isArray(res)) {
      // 兼容：直接返回数组
      rows = res
      pagination.total = rows.length
    } else if (res?.data) {
      rows = Array.isArray(res.data) ? res.data : [res.data]
      pagination.total = res.total ?? rows.length ?? 0
    }

    // onLoadAfter 钩子
    const processed = await lc.onLoadAfter?.(rows, res)
    tableData.value = processed || rows

    // 调试：检查加载后的数据中 keyField 字段是否存在
    if (tableData.value?.length > 0) {
      const firstRow = tableData.value[0]
      console.log('[YzhCrudTable] loadData 完成:')
      console.log('  - keyField:', schema.value.keyField)
      console.log('  - 首行 keyField 值:', firstRow[schema.value.keyField])
      console.log('  - 首行所有字段:', Object.keys(firstRow))
      // 检查大小写变体
      const kfLower = schema.value.keyField.toLowerCase()
      const kfUpper = schema.value.keyField.toUpperCase()
      if (firstRow[kfLower] !== undefined && firstRow[schema.value.keyField] === undefined) {
        console.warn(`  - ⚠️ 字段名大小写不匹配: 期望 "${schema.value.keyField}", 实际为 "${kfLower}"`)
      }
      if (firstRow[kfUpper] !== undefined && firstRow[schema.value.keyField] === undefined) {
        console.warn(`  - ⚠️ 字段名大小写不匹配: 期望 "${schema.value.keyField}", 实际为 "${kfUpper}"`)
      }
    }

    // 同步到增量同步器
    incSync.setRows(tableData.value)
  } catch (e: any) {
    console.error('[YzhCrudTable] loadData error:', e)
    proxy?.$message?.error?.(e?.message || '加载数据失败')
  } finally {
    loading.value = false
  }
}

/** 构建查询条件（适配后端 Vol SearchParameters 格式） */
function buildFilter(): any[] {
  const filter: any[] = []

  // 外部过滤条件
  if (props.externalFilter?.length) {
    props.externalFilter.forEach((f) => {
      filter.push({ Name: f.name, Value: String(f.value), DisplayType: f.cond || '==' })
    })
  }

  // 搜索表单条件
  Object.keys(searchForm).forEach((k) => {
    const v = searchForm[k]
    if (v !== undefined && v !== null && v !== '') {
      // 关键词模糊匹配（如果配置了 searchKeywordFields）
      const kwFields = (schema.value as any).searchKeywordFields
      if (kwFields?.includes(k) || k === 'Name') {
        // 多字段 OR LIKE
        const fields = kwFields || [k]
        fields.forEach((f: string) => {
          filter.push({ Name: f, Value: String(v).trim(), DisplayType: 'like' })
        })
      } else {
        filter.push({ Name: k, Value: v })
      }
    }
  })

  return filter
}

/** 构建排序 */
function buildSortOrder(): string {
  if (!currentSortProp.value) return ''
  const dir = currentSortOrder.value === 'asc' ? ' asc' : ' desc'
  return `${currentSortProp.value} ${dir}`
}

// ============================================================
// 排序变化
// ============================================================
function onSortChange({ prop, order }: { prop: string; order: string | null }) {
  currentSortProp.value = prop || defaultSortField.value
  currentSortOrder.value = order === 'ascending' ? 'asc' : 'desc'
  loadData()
}

// ============================================================
// 行选择
// ============================================================
function onCurrentRowChange(row: any) {
  setSingleSelected(row)
  lc.onRowClick?.(row)
}

function onSelectionChange(rows: any[]) {
  setSelectedRows(rows)
}

function onRowDbClick(row: any) {
  lc.onRowDbClick?.(row)
  handleEdit(row)
}

// ============================================================
// 新增
// ============================================================
const dialogVisible = ref(false)
const dialogTitle = ref('')
const dialogAction = ref<'add' | 'edit'>('add')
const editFormRef = ref()
const editForm = reactive<Record<string, any>>({})
const saving = ref(false)

/** 编辑表单校验规则 */
const editFormRules = computed(() => {
  const rules: Record<string, any[]> = {}
  editFormOptions.value.forEach((row: any[]) => {
    ;(row || []).forEach((item: any) => {
      if (item.required && item.field) {
        rules[item.field] = [
          { required: true, message: `${item.title || item.field}不能为空`, trigger: 'blur' },
        ]
      }
    })
  })
  return rules
})

async function handleAdd() {
  dialogAction.value = 'add'
  dialogTitle.value = `新增${tableConfig.value.cnName || ''}`
  // 先重置表单为默认值，再调用 onAddBefore
  // 这样 onAddBefore 可以在已重置的表单上设置值（如 YzhTreeTable 填入 CbCode）
  resetEditForm({})

  const ok = await runGuard(lc.onAddBefore, [editForm])
  if (!ok) return

  dialogVisible.value = true
}

// ============================================================
// 编辑
// ============================================================
let editingRow: any = null

async function handleEdit(row: any) {
  const ok = await runGuard(lc.onUpdateBefore, [row, editForm])
  if (!ok) return

  dialogAction.value = 'edit'
  dialogTitle.value = `编辑${tableConfig.value.cnName || ''}`
  editingRow = row
  resetEditForm({ ...row })
  dialogVisible.value = true
}

/** 重置编辑表单 */
function resetEditForm(data: any) {
  // 清空所有字段
  Object.keys(editForm).forEach((k) => delete editForm[k])
  // 用数据填充
  if (data) {
    Object.keys(data).forEach((k) => {
      editForm[k] = data[k]
    })
  }
  // 对没有值的字段给默认值
  const defaultFields = opts.value.editFormFields || {}
  Object.keys(defaultFields).forEach((k) => {
    if (!(k in editForm) || editForm[k] === undefined) {
      editForm[k] = defaultFields[k] ?? ''
    }
  })

  nextTick(() => {
    editFormRef.value?.clearValidate?.()
  })
}

// ============================================================
// 弹窗打开后的回调
// ============================================================
async function onDialogOpen() {
  // 加载字典数据（如果有 dataKey 但 data 为空）
  await loadDictionaryData()
  // modelOpenAfter 钩子
  lc.onAddAfter?.(editingRow, dialogAction.value)
}

/** 收集所有需要加载的字典 dataKey */
function collectDictKeys(): string[] {
  const keys = new Set<string>()
  editFormOptions.value.forEach((row: any[]) => {
    ;(row || []).forEach((item: any) => {
      if ((item.type === 'select') && item.dataKey && (!item.data || !item.data.length)) {
        keys.add(item.dataKey)
      }
    })
  })
  searchableColumns.value.forEach((col: any) => {
    if (col.type === 'select' && col.dataKey && (!col.data || !col.data.length)) {
      keys.add(col.dataKey)
    }
  })
  return Array.from(keys)
}

  /** 解析 GetVueDictionary 返回的字典数据并填充到对应字段 */
  /**
   * 后端实际返回格式（GetVueDictionary → Content(Serialize())）：
   *   [{ dicNo: "org_status", config: "...", data: [{ key, value, color }] }, ...]
   * 注意：最终字段名是 "data" 而非中间变量 "list"
   */
  function applyDictData(dictResponse: any) {
    if (!dictResponse) return

    // 统一转为 { dicNo: [{key,value}] } 的 Map 结构
    const dictMap: Record<string, any[]> = {}

    if (Array.isArray(dictResponse)) {
      // 格式A：后端 GetVueDictionary 实际返回数组
      // 每项结构: { dicNo, config, data: [{key, value, color}] }
      ;(dictResponse as any[]).forEach((item: any) => {
        if (item.dicNo) {
          // 优先取 data 字段（后端实际返回的字段名）
          const list = item.data || item.list
          if (Array.isArray(list)) {
            dictMap[item.dicNo] = list.map((d: any) => ({
              key: String(d.key ?? d.value ?? ''),
              value: String(d.value ?? d.key ?? ''),
            }))
          }
        }
      })
    } else if (typeof dictResponse === 'object') {
      // 格式B：对象格式（兼容其他接口）
      Object.keys(dictResponse).forEach((k) => {
        const v = dictResponse[k]
        if (Array.isArray(v)) {
          dictMap[k] = v.map((d: any) => ({
            key: String(d.key ?? d.value ?? ''),
            value: String(d.value ?? d.key ?? ''),
          }))
        }
      })
    }

    // 填充编辑表单的 select 字典
    editFormOptions.value.forEach((row: any[]) => {
      ;(row || []).forEach((item: any) => {
        if (item.type === 'select' && item.dataKey && dictMap[item.dataKey]) {
          item.data = dictMap[item.dataKey]
        }
      })
    })
    // 填充搜索区的 select 字典
    searchableColumns.value.forEach((col: any) => {
      if (col.type === 'select' && col.dataKey && dictMap[col.dataKey]) {
        col.data = dictMap[col.dataKey]
      }
    })
    // 填充表格列的字典数据（用于 formatDictValue 翻译显示）
    columns.value.forEach((col: any) => {
      if (col.dataKey && dictMap[col.dataKey]) {
        col.data = dictMap[col.dataKey]
      }
    })
  }

/** 加载字典数据到 select 类型的选项中 */
async function loadDictionaryData() {
  const dictKeys = collectDictKeys()
  if (dictKeys.length === 0) return

  try {
    // 后端 GetVueDictionary 返回的是数组格式（不是标准 Vol 包装）
    // 接口签名: [HttpPost] Content(Service.GetVueDictionary(dicNos).Serialize())
    const res = await proxy?.http?.post('/api/Sys_Dictionary/GetVueDictionary', dictKeys, false)
    // res 可能是：
    //   - 直接返回数组（Content 返回，已反序列化）
    //   - 包装在 data 中（某些 http 封装层可能包一层）
    const dataToApply = Array.isArray(res) ? res : (res?.data ?? null)
    if (dataToApply) {
      applyDictData(dataToApply)
    }
  } catch (e: any) {
    console.warn('[YzhCrudTable] 字典加载失败:', e?.message || e)
    // 字典加载失败不影响主流程，静默处理
  }
}

// ============================================================
// 保存（新增 / 修改）
// ============================================================
async function handleSave() {
  try {
    await editFormRef.value?.validate()
  } catch {
    return
  }

  saving.value = true
  try {
    const isAdd = dialogAction.value === 'add'

  if (isAdd) {
    // ———— 新增 ————
    // 基类默认行为：将 null/undefined 的字符串字段填充为空字符串（避免 DB 写入 null）
    applyStringFieldDefaults(editForm)

    const ok = await runGuard(lc.onAddSaveBefore, [editForm])
      if (!ok) return

      const res = await api.add(editForm)

      if (res?.status || res?.Status) {
        proxy?.$message?.success?.(res?.message || res?.Message || '新增成功')

        // 解析后端返回的数据（兼容 JSON 字符串和对象）
        let serverData = null
        try {
          const rawData = res?.Data || res?.data
          if (typeof rawData === 'string') {
            const parsed = JSON.parse(rawData)
            serverData = parsed?.data || parsed
          } else if (rawData && typeof rawData === 'object') {
            serverData = rawData?.data || rawData
          }
        } catch (e) {
          console.warn('[YzhCrudTable] 解析 serverData 失败:', e)
        }

        // 用服务端数据构建新行（包含服务端生成的 Id/Code/CreateDate 等）
        const serverKey = serverData?.[schema.value.keyField]
        const clientKey = editForm[schema.value.keyField]
        const newRow = {
          ...editForm,
          ...(serverData || {}),
          [schema.value.keyField]: serverKey || clientKey,
        }

        // 增量插入：affected=false 时回退到全量刷新
        const result = await incSync.applyInsert(newRow)
        if (!result.affected) {
          console.warn('[YzhCrudTable] applyInsert 未生效，回退到 loadData')
          loadData()
        }

        dialogVisible.value = false
        lc.onAddSaveAfter?.(newRow, res)
      } else {
        proxy?.$message?.error?.(res?.message || res?.Message || '新增失败')
      }
    } else {
      // ———— 修改 ————
      const ok = await runGuard(lc.onUpdateSaveBefore, [editForm])
      if (!ok) return

      // 合并主键（确保更新时携带原始主键）
      editForm[schema.value.keyField] = editingRow?.[schema.value.keyField]

      const res = await api.update(editForm)

      if (res?.status || res?.Status) {
        proxy?.$message?.success?.(res?.message || res?.Message || '保存成功')

        // 用 splice 原地替换当前行（String() 比较兼容 Vue proxy 包装）
        // 注意：后端审计字段（ModifyDate/Modifier）不会通过此方式更新
        // 如需展示最新审计字段，可在 onUpdateSaveAfter 中从 res 中合并
        const updatedRow = { ...editForm }
        const result = await incSync.applyReplace(updatedRow)
        if (!result.affected) {
          console.warn('[YzhCrudTable] applyReplace 未生效，回退到 loadData')
          loadData()
        }

        dialogVisible.value = false
        lc.onUpdateSaveAfter?.(updatedRow, res)
      } else {
        proxy?.$message?.error?.(res?.message || res?.Message || '保存失败')
      }
    }
  } catch (e: any) {
    console.error('[YzhCrudTable] handleSave error:', e)
    proxy?.$message?.error?.(e?.message || '操作失败')
  } finally {
    saving.value = false
  }
}

// ============================================================
// 删除（单行 / 批量）
// ============================================================
async function handleDelete(row: any) {
  const id = row[schema.value.keyField]
  console.log(`[YzhCrudTable] 🗑️ 开始删除: keyField=${schema.value.keyField}, id=${id}`, row)

  const ok = await guard.confirmDeleteOne(row.Name || row.Title || id)
  if (!ok) return

  const delOk = await runGuard(lc.onDeleteBefore, [[row], [id]])
  if (!delOk) return

  try {
    console.log(`[YzhCrudTable] 调用 api.del([${id}])...`)
    const res = await api.del([id])
    console.log(`[YzhCrudTable] 删除响应:`, JSON.stringify(res))

    if (res?.status || res?.Status) {
      proxy?.$message?.success?.(res?.message || res?.Message || '删除成功')

      // 增量移除：affected=false 说明当前页找不到该行，回退到全量刷新
      const result = await incSync.applyRemove([id], pagination)
      if (!result.affected) {
        console.warn('[YzhCrudTable] applyRemove 未生效（可能已翻页），回退到 loadData')
        loadData()
      }

      lc.onDeleteAfter?.([id])
    } else {
      // 错误信息可能是多行引用详情（含 \n），用 alert 弹窗展示更友好
      const errorMsg = res?.message || res?.Message || '删除失败'
      console.error(`[YzhCrudTable] 删除失败: ${errorMsg}`)
      if (errorMsg.includes('\n') && proxy?.$alert) {
        proxy.$alert(errorMsg, '无法删除', { type: 'error', dangerouslyUseHTMLString: false })
      } else {
        proxy?.$message?.error?.(errorMsg)
      }
    }
  } catch (e: any) {
    console.error('[YzhCrudTable] 删除异常:', e)
    proxy?.$message?.error?.(e?.message || '删除异常')
  }
}

async function handleBatchDelete() {
  const objs = selectedRowObjects()
  if (!objs.length) {
    proxy?.$message?.warning?.('请先选择要删除的行')
    return
  }

  const ids = objs.map((r) => r[schema.value.keyField])

  const ok = await guard.confirmDeleteBatch(ids.length)
  if (!ok) return

  const delOk = await runGuard(lc.onDeleteBefore, [objs, ids])
  if (!delOk) return

  try {
    const res = await api.del(ids)

    if (res?.status || res?.Status) {
      proxy?.$message?.success?.(res?.message || res?.Message || `成功删除 ${ids.length} 条`)

      const result = await incSync.applyRemove(ids, pagination)
      if (!result.affected) {
        console.warn('[YzhCrudTable] batch applyRemove 未生效，回退到 loadData')
        loadData()
      }

      clearSelected()
      lc.onDeleteAfter?.(ids)
    } else {
      // 多行引用错误用 alert 弹窗展示
      const errorMsg = res?.message || res?.Message || '删除失败'
      if (errorMsg.includes('\n') && proxy?.$alert) {
        proxy.$alert(errorMsg, '无法删除', { type: 'error', dangerouslyUseHTMLString: false })
      } else {
        proxy?.$message?.error?.(errorMsg)
      }
    }
  } catch (e: any) {
    proxy?.$message?.error?.(e?.message || '删除异常')
  }
}

// ============================================================
// 刷新
// ============================================================
function handleRefresh() {
  initSearchForm()
  pagination.page = 1
  loadData()
}

// ============================================================
// 导入/导出
// ============================================================
/**
 * 导出 Excel
 * 
 * 问题修复说明（2026-08-07）：
 * 原来调用 api.export() → http.post(url, param, isBlob=true)
 * 但 http.js 的 post 签名是 post(url, params, loading, config)
 * isBlob=true 被错误传给 loading 参数，config 为 undefined
 * 导致 axios 没有设置 responseType:'blob'，返回 JSON 而非文件流
 * 
 * 修复方案：直接用 axios 请求 blob，再用 <a> 标签触发浏览器下载
 */
async function handleExport() {
  try {
    // 构建导出参数（对齐 Vol 原生 ViewGridEventButton.jsx 的传参格式）
    // 后端 ExportBytes 需要 pageData.Columns 来确定导出哪些列，不传则导出空文件
    const visibleCols = actualVisibleColumns.value
    const param: any = {
      filter: buildFilter(),
      sort: currentSortProp.value || defaultSortField.value,
      order: currentSortOrder.value || defaultSortOrder.value || 'desc',
      // 关键：把可见列的 field 名传给后端，EPPlus 用它决定导出哪些列
      columns: visibleCols.map((c: any) => c.field).filter(Boolean),
    }
    const ok = await runGuard(lc.onExportBefore, [param])
    if (!ok) return

    // 显示加载状态
    const loadingInstance = (window as any).ElLoading?.service({
      lock: true,
      text: '正在导出...',
      background: 'rgba(0, 0, 0, 0.3)',
    })

    try {
      // 直接用 axios 请求，确保 responseType: 'blob'
      const axiosInst = (await import('axios')).default
      // 获取 token（与 http.js 的 getToken 同款）
      const { default: store } = await import('@/store/index')
      const token = store.getters.getToken()
      const baseUrl = schema.value.apiPrefix || '/api/'
      const exportUrl = `${baseUrl}${schema.value.controllerName}/Export`

      const response = await axiosInst.post(exportUrl, param, {
        headers: {
          'Content-Type': 'application/json',
          'Authorization': token,
        },
        responseType: 'blob', // 关键：告诉 axios 返回 Blob 对象
        timeout: 120000, // 导出可能较慢，给 2 分钟
      })

      // 关闭加载
      loadingInstance?.close()

      // 检查响应：如果后端返回 JSON 错误信息（blob 类型），需要特殊处理
      const contentType = response.headers['content-type'] || ''
      if (contentType.includes('application/json')) {
        // 后端返回了 JSON 错误（如 "没有数据"）
        const text = await response.data.text()
        const errJson = JSON.parse(text)
        proxy?.$message?.error?.(errJson.message || '导出失败')
        return
      }

      // 创建 Blob 并触发下载
      const blob = new Blob([response.data], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      })

      // 从 schema 或 options 中获取文件名
      const fileName = `${(schema.value.controllerName || 'export')}_${new Date().toISOString().slice(0, 10)}.xlsx`

      // 使用 <a> 标签下载
      const url = window.URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.style.display = 'none'
      link.href = url
      link.download = fileName
      document.body.appendChild(link)
      link.click()
      
      // 清理
      setTimeout(() => {
        window.URL.revokeObjectURL(url)
        document.body.removeChild(link)
      }, 100)

      proxy?.$message?.success?.('导出成功')
      lc.onExportAfter?.(blob)
    } catch (exportErr: any) {
      loadingInstance?.close()
      throw exportErr
    }
  } catch (e: any) {
    console.error('[YzhCrudTable] handleExport error:', e)
    proxy?.$message?.error?.(e?.message || '导出失败')
  }
}

function handleImport() {
  const input = document.createElement('input')
  input.type = 'file'
  input.accept = '.xlsx,.xls,.csv'
  input.onchange = async (e: any) => {
    const file = e.target.files?.[0]
    if (!file) return
    const formData = new FormData()
    formData.append('file', file)
    try {
      const ok = await runGuard(lc.onImportBefore, [formData])
      if (!ok) return
      const res = await api.import(formData)
      if (res?.status) {
        proxy?.$message?.success?.('导入成功')
        loadData()
        lc.onImportAfter?.(res.data)
      } else {
        proxy?.$message?.error?.(res?.message || '导入失败')
      }
    } catch (err: any) {
      proxy?.$message?.error?.(err?.message || '导入异常')
    }
  }
  input.click()
}

// ============================================================
// 字典值格式化
// ============================================================
const dictCache = ref<Record<string, any[]>>({})

function getStatusTagColor(field: string): string | undefined {
  return (schema.value as any)?.statusTagColors?.[field]
}

function getTagType(value: any, field: string): '' | 'success' | 'warning' | 'danger' | 'info' {
  const colorMap: Record<string, string> = {
    // org_status 字典值
    active: 'success',
    suspended: 'warning',
    cancelled: 'info',
    rectification: 'danger',
    // standard_status 字典值
    draft: 'warning',
    published: 'success',
    deprecated: 'info',
    // 兼容旧值
    enabled: 'success',
    normal: 'success',
    inactive: 'danger',
    disabled: 'danger',
    blocked: 'danger',
    pending: 'warning',
    implemented: 'info',  // 兼容旧值，映射为 info 色
  }
  return colorMap[String(value)] || 'info'
}

function formatDictValue(value: any, col: any): string {
  if (value == null || value === '') return ''
  // 如果有字典数据，尝试翻译
  if (col.data?.length) {
    const found = col.data.find((d: any) => d.key === value || d.value === value)
    if (found) return found.value || found.label || String(value)
  }
  return String(value)
}

// ============================================================
// 表格高度自适应
// ============================================================
function calcTableHeight() {
  nextTick(() => {
    const el = tableRef.value?.$el
    if (el) {
      const top = el.getBoundingClientRect().top
      const winHeight = window.innerHeight
      tableHeight.value = Math.max(300, winHeight - top - 80) // 80 = 分页 + padding
    }
  })
}

// ============================================================
// 初始化（V2.5：支持配置驱动）
// ============================================================
onMounted(async () => {
  initSearchForm()
  calcTableHeight()
  window.addEventListener('resize', calcTableHeight)

  // V2.5: 如果有 pageKey，先从后端（或 Store）加载 UI 配置
  if (props.pageKey) {
    await loadDbPageConfig()
  }

  // 预加载搜索区字典（不等弹窗打开，让搜索区下拉立即可用）
  await loadDictionaryData()

  // 加载数据
  await loadData()

  // 暴露实例
  emit('ready', exposedApi)
})

// ============================================================
// 列设置（筛选 + 排序）
// ============================================================

// 用独立的 Set 维护隐藏列的 field 名（不依赖 columns computed 的对象引用）
const hiddenColumnFields = ref<Set<string>>(new Set())

/** 可用于列设置面板的列（排除操作列等固定列） */
const sortableFilterableColumns = computed(() =>
  columns.value.filter((c: any) => c.field && c.title && c.field !== '__yzh_action')
)

/**
 * 实际可见的列 = 原始列 - 用户手动隐藏的列
 * 同时也排除原始配置中 hidden=true 的列（如 Id 列）
 */
const actualVisibleColumns = computed(() =>
  columns.value.filter((c: any) => {
    if (!c.field || !c.title || c.field === '__yzh_action') return false
    if (c.hidden) return false // 原始配置隐藏（如 Id）
    if (hiddenColumnFields.value.has(c.field)) return false // 用户手动隐藏
    return true
  })
)

/** 切换列显示/隐藏 */
function toggleColumnVisibility(col: any, visible: boolean) {
  if (visible) {
    // 显示 → 从隐藏集合中移除
    hiddenColumnFields.value.delete(col.field)
  } else {
    // 隐藏 → 加入隐藏集合
    hiddenColumnFields.value.add(col.field)
  }
  // 触发响应式更新（Set 需要重新赋值才能触发 Vue 响应式）
  hiddenColumnFields.value = new Set(hiddenColumnFields.value)
}

/**
 * 基类默认行为：将表单中 null/undefined 的字符串字段填充为空字符串
 * 
 * 为什么需要：
 * - 前端 v-model 绑定的 input 在用户未输入时值为空字符串 ''
 * - 但某些场景下（如程序化设置、部分字段未绑定 v-model）可能为 null/undefined
 * - 后端 EF Core / MySQL 对字符串字段写入 null 可能导致意外行为
 * - 此函数确保所有字符串字段至少为 ''，业务钩子 onAddSaveBefore 可覆盖
 * 
 * @param formData 编辑表单对象（会被原地修改）
 */
function applyStringFieldDefaults(formData: any) {
  if (!formData || typeof formData !== 'object') return
  // 从 editFormOptions 中获取所有字符串字段的 field 名
  const stringFields = new Set<string>()
  editFormOptions.value.forEach((row: any[]) => {
    ;(row || []).forEach((item: any) => {
      if (item.field && ['input', 'textarea', 'text'].includes(item.type)) {
        stringFields.add(item.field)
      }
    })
  })
  // 对 null/undefined 的字符串字段填充默认值
  stringFields.forEach((field) => {
    if (formData[field] === null || formData[field] === undefined) {
      formData[field] = ''
    }
  })
}

/** 从列设置面板切换排序 */
function toggleSort(field: string) {
  if (currentSortProp.value === field) {
    // 已是当前排序字段 → 切换升降序
    currentSortOrder.value = currentSortOrder.value === 'asc' ? 'desc' : 'asc'
  } else {
    currentSortProp.value = field
    currentSortOrder.value = 'asc'
  }
}

/** 获取排序图标文字 */
function getSortIcon(field: string): string {
  if (currentSortProp.value !== field) return '排序'
  return currentSortOrder.value === 'asc' ? '↑ 升序' : '↓ 降序'
}

/** 重置列设置为默认全部显示 */
function resetColumnSettings() {
  columns.value.forEach((c: any) => { if (c._origHidden !== undefined) c.hidden = c._origHidden })
  currentSortProp.value = defaultSortField.value
  currentSortOrder.value = defaultSortOrder.value
}

/** 应用列设置并刷新表格 */
function applyColumnSettings() {
  loadData()
}

// ============================================================
// 对外暴露的方法
// ============================================================
const exposedApi = {
  get table() { return tableRef.value },
  get selectedRow() { return singleSelectedRow.value },
  get selectedRows() { return selectedRowObjects() },
  refresh: handleRefresh,
  search: handleSearch,
  getData: () => tableData.value,
  getApi: () => api,
  // 暴露 loadData：供 YzhTreeTable 在树节点切换时直接调用刷新右侧表格
  loadData,
  // 暴露 pagination：供外部重置分页（如切换树节点时回到第1页）
  pagination,
}

defineExpose(exposedApi)
</script>

<style lang="less">
/* 注意：不使用 scoped！因为 el-dialog 渲染在 body 层级，scoped 样式无法穿透 */

/* ====== 主容器：带左右 padding ====== */
.yzh-crud-table {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  position: relative;
  padding: 0 16px; /* 左右各 16px padding */
  box-sizing: border-box;
}

/* 弹窗样式 —— el-dialog 挂载在 body 下，必须用全局选择器 */
.el-dialog {
  .el-dialog__header {
    padding: 16px 20px 12px;
    border-bottom: 1px solid #ebeef5;
    margin-right: 0;
  }
  .el-dialog__body {
    padding: 20px;
  }
  .el-dialog__footer {
    padding: 12px 20px 16px;
    border-top: 1px solid #ebeef5;
  }
}

.yzh-toolbar-extra {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px 4px;

  &__left,
  &__right {
    display: flex;
    gap: 8px;
    align-items: center;
  }
}

.yzh-search-bar {
  padding: 12px 16px;
  background: #fafafa;
  border: 1px solid #ebeef5;
  border-radius: 4px;
  margin-bottom: 12px;

  &.is-fixed {
    // 固定展开模式
  }

  .yzh-search-form {
    .el-form-item {
      margin-bottom: 8px;
    }
  }
}

/* 工具栏：左操作 + 右设置 分离布局 */
.yzh-btn-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
  padding: 8px 0;
  margin-bottom: 8px;

  &__left {
    display: flex;
    gap: 6px;
    align-items: center;
    flex-wrap: wrap;
  }

  &__right {
    display: flex;
    align-items: center;
    flex-shrink: 0;
    margin-left: auto;
  }
}

/* 列设置面板样式 */
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
    padding: 4px 0;

    .el-checkbox {
      flex: 1;
      min-width: 0;
    }

    .is-active {
      color: var(--el-color-primary);
      font-weight: 600;
    }
  }

  &__footer {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding-top: 10px;
    margin-top: 8px;
    border-top: 1px solid #ebeef5;
  }
}

.yzh-pagination-wrap {
  display: flex;
  justify-content: flex-end;
  padding: 12px 0 0;
}

.dialog-footer {
  text-align: right;
}
</style>
