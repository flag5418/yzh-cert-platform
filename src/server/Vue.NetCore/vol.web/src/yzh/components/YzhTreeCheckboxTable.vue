<template>
  <div class="yzh-tree-checkbox-table">
    <!-- ====== 左侧树 ====== -->
    <div class="left-tree" :style="{ width: treeWidth }">
      <div class="tree-header">
        <span>{{ treeTitle }}</span>
        <el-button v-if="allowRefresh" text size="small" @click="handleRefreshTree">
          <el-icon><Refresh /></el-icon> 刷新
        </el-button>
      </div>
      <el-tree
        ref="treeRef"
        :data="treeData"
        :props="treeProps"
        :expand-on-click-node="false"
        :highlight-current="true"
        :default-expand-all="defaultExpandAll"
        node-key="keyField"
        @node-click="handleTreeNodeClick"
        v-loading="treeLoading"
      >
        <template #default="{ node, data }">
          <span class="tree-node">
            <el-tag
              v-if="data[statusTagField]"
              :type="getTagType(data[statusTagField])"
              size="small"
              effect="light"
              style="margin-left: 6px"
            >{{ formatDictValue(data[statusTagField], { dataKey: statusDataKey }) }}</el-tag>
            <span class="tree-label">{{ node.label }}</span>
          </span>
        </template>
      </el-tree>
    </div>

    <!-- ====== 右侧 checkbox 表格 ====== -->
    <div class="right-table">
      <!-- 工具栏 -->
      <div class="table-toolbar">
        <div class="toolbar-left">
          <span class="checked-info">
            已勾选 <strong>{{ checkedIds.length }}</strong> / 共 <strong>{{ tableData.length }}</strong> 项
          </span>
          <el-tag v-if="hasChanges" type="warning" size="small" effect="light">
            {{ pendingAddCount }} 新增 / {{ pendingRemoveCount }} 移除
          </el-tag>
        </div>
        <div class="toolbar-right">
          <el-button text size="small" @click="handleSelectAll" :disabled="!tableData.length">全选</el-button>
          <el-button text size="small" @click="handleDeselectAll" :disabled="!checkedIds.length">取消全选</el-button>
          <el-button
            v-if="showSaveButton"
            type="primary"
            size="small"
            :loading="saving"
            :disabled="!hasChanges"
            @click="handleSave"
          >保存变更</el-button>
          <el-button v-if="allowRefresh" text size="small" @click="handleRefreshTable">
            <el-icon><Refresh /></el-icon> 刷新
          </el-button>
        </div>
      </div>

      <!-- 表格 -->
      <el-table
        ref="tableRef"
        :data="tableData"
        v-loading="tableLoading"
        border
        stripe
        height="100%"
        :row-key="rowKeyField"
        @selection-change="handleSelectionChange"
        style="width: 100%"
      >
        <el-table-column type="selection" width="50" align="center" reserve-selection />
        <el-table-column
          v-for="col in columns"
          :key="col.field"
          :prop="col.field"
          :label="col.title"
          :width="col.width"
          :align="col.align || 'left'"
          :sortable="col.sortable !== false"
          :show-overflow-tooltip="col.showOverflow !== false"
        >
          <template #default="{ row }">
            <template v-if="col.dataKey && getDictItems(col.dataKey)">
              <el-tag
                :type="getTagType(row[col.field], col.field)"
                size="small"
                effect="light"
              >{{ formatDictValue(row[col.field], col) }}</el-tag>
            </template>
            <template v-else>{{ row[col.field] }}</template>
          </template>
        </el-table-column>

        <!-- 分页 -->
        <div class="pagination-wrap" v-if="total > 0">
          <el-pagination
            v-model:current-page="pagination.page"
            v-model:page-size="pagination.rows"
            :total="total"
            :page-sizes="[10, 20, 50, 100]"
            layout="total, sizes, prev, pager, next"
            small
            @current-change="handlePageChange"
            @size-change="handleSizeChange"
          />
        </div>
      </el-table>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * YzhTreeCheckboxTable — 左树 + 右 checkbox 表格关联组件
 *
 * 核心用途：机构-标准关联、机构-阶段关联
 * 交互模式：逐条实时同步（勾选即保存）
 */
import { ref, computed, watch, onMounted, nextTick, type Ref } from 'vue'
import { Refresh } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import http from '@/api/http'

// Types
interface IColumn {
  field: string
  title: string
  width?: number
  align?: string
  sortable?: boolean
  showOverflow?: boolean
  dataKey?: string
}

interface ILinkApi {
  syncFn: (cbCode: string, addIds: string[], removeIds: string[]) => Promise<any>
  getIdsFn: (cbCode: string) => Promise<string[]>
}

interface IPagination {
  page: number
  rows: number
}

interface ITreeNode {
  [key: string]: any
  children?: ITreeNode[]
}

// Props & Emits
const props = withDefaults(defineProps<{
  treeData: ITreeNode[]
  treeTitle: string
  treeWidth?: string
  columns: IColumn[]
  loadDataFn: (params: any) => Promise<{ data: any[]; total: number }>
  linkApi: ILinkApi
  rowKeyField?: string
  treeProps?: object
  autoSave?: boolean
  showSaveButton?: boolean
  defaultExpandAll?: boolean
  allowRefresh?: boolean
  statusTagField?: string
  statusDataKey?: string
}>(), {
  treeWidth: '220px',
  rowKeyField: 'Id',
  treeProps: () => ({ label: 'Name', children: 'children', isLeaf: (d: any) => !d.children?.length }),
  autoSave: true,
  showSaveButton: false,
  defaultExpandAll: true,
  allowRefresh: true,
})

const emit = defineEmits<{
  (e: 'treeNodeSelect', data: any, node: any): void
  (e: 'save', changes: { added: string[]; removed: string[] }): void
  (e: 'change', checkedIds: string[], allIds: string[]): void
}>()

// Refs
const treeRef = ref()
const tableRef = ref()
const treeLoading = ref(false)
const tableLoading = ref(false)
const saving = ref(false)

// Data
const tableData: Ref<any[]> = ref([])
const total = ref(0)
const pagination = ref<IPagination>({ page: 1, rows: 50 })
const selectedTreeKey = ref<string | null>(null)

/** 已勾选的 ID 集合（来自数据库） */
const checkedIdsFromDb = ref<Set<string>>(new Set())
/** 当前界面勾选的 ID 集合（含未保存的变更） */
const checkedIds = ref<Set<string>>(new Set())
/** 待新增的 ID（在 db 中没有但用户勾选了） */
const pendingAddIds = ref<Set<string>>(new Set())
/** 待移除的 ID（在 db 中有但用户取消了） */
const pendingRemoveIds = ref<Set<string>>(new Set())

/**
 * 标志位：是否正在程序化设置 checkbox
 * 防止 toggleRowSelection 触发 selection-change 导致误同步
 */
let isSettingCheckboxes = false

// Computed
const hasChanges = computed(() => pendingAddIds.value.size > 0 || pendingRemoveIds.value.size > 0)
const pendingAddCount = computed(() => pendingAddIds.value.size)
const pendingRemoveCount = computed(() => pendingRemoveIds.value.size)

// Dict cache
const dictCache = ref<Record<string, any[]>>({})

function getDictItems(dataKey: string): any[] | undefined {
  return dictCache.value[dataKey]
}

async function loadDictData(keys: string[]) {
  const needLoad = keys.filter(k => !dictCache.value[k])
  if (!needLoad.length) return
  try {
    const res: any = await http.post('/api/Sys_Dictionary/GetVueDictionary', needLoad, null, false)
    if (res?.data) {
      const list = Array.isArray(res.data) ? res.data : []
      list.forEach((item: any) => {
        if (item.dicNo && item.data) {
          dictCache.value[item.dicNo] = item.data.map((d: any) => ({
            key: String(d.key ?? d.value ?? ''),
            value: String(d.value ?? d.key ?? ''),
          }))
        }
      })
    }
  } catch (e) {
    console.warn('[YzhTreeCheckboxTable] 字典加载失败', e)
  }
}

function formatDictValue(value: any, col: IColumn): string {
  if (value == null || value === '') return ''
  const items = col.data || dictCache.value[col.dataKey || '']
  if (!Array.isArray(items)) return String(value)
  const found = items.find((d: any) => d.key === value || d.value === value)
  return found ? found.value || found.label || String(value) : String(value)
}

function getTagType(value: any, _field?: string): '' | 'success' | 'warning' | 'danger' | 'info' {
  const map: Record<string, string> = {
    active: 'success', published: 'success', draft: 'warning',
    deprecated: 'info', inactive: 'danger', cancelled: 'info',
    suspended: 'warning', rectification: 'danger',
    process: '', audit: 'warning', post: 'info',
  }
  return map[String(value)] || 'info'
}

// Debounce sync (300ms)
let debounceTimer: ReturnType<typeof setTimeout> | null = null

function debouncedSync() {
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => doSync(), props.autoSave ? 300 : 0)
}

async function doSync() {
  if (!selectedTreeKey.value) return

  const addArr = Array.from(pendingAddIds.value)
  const removeArr = Array.from(pendingRemoveIds.value)
  if (addArr.length === 0 && removeArr.length === 0) return

  saving.value = true
  try {
    const res: any = await props.linkApi.syncFn(selectedTreeKey.value, addArr, removeArr)
    pendingAddIds.value.clear()
    pendingRemoveIds.value.clear()
    addArr.forEach(id => checkedIdsFromDb.value.add(id))
    removeArr.forEach(id => checkedIdsFromDb.value.delete(id))

    emit('save', { added: addArr, removed: removeArr })

    if (props.autoSave) {
      const msg = `已保存：+${addArr.length} / -${removeArr.length}`
      if (res?.rejectedRemoves?.length > 0) {
        ElMessage.warning(`${msg}（${res.rejectedRemoves.length}项因引用无法移除）`)
        res.rejectedRemoves.forEach((r: any) => {
          checkedIdsFromDb.value.add(String(r.StdId))
          checkedIds.value.add(String(r.StdId))
        })
      } else {
        ElMessage.success(msg)
      }
    }
  } catch (e: any) {
    console.error('[YzhTreeCheckboxTable] 同步失败', e)
    ElMessage.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

// Tree node click
async function handleTreeNodeClick(data: any) {
  selectedTreeKey.value = data[props.treeProps?.label || 'Name'] ? data.Code || data.Id : null
  emit('treeNodeSelect', data, data)

  pendingAddIds.value.clear()
  pendingRemoveIds.value.clear()
  checkedIds.value.clear()

  // Load checked IDs from DB
  if (props.linkApi?.getIdsFn && selectedTreeKey.value) {
    try {
      const ids = await props.linkApi.getIdsFn(selectedTreeKey.value)
      checkedIdsFromDb.value = new Set(ids || [])
      checkedIds.value = new Set(checkedIdsFromDb.value)
    } catch (e) {
      console.warn('[YzhTreeCheckboxTable] 加载已关联 ID 失败', e)
      checkedIdsFromDb.value = new Set()
      checkedIds.value = new Set()
    }
  }

  await loadTableData()
}

// Table data loading
async function loadTableData() {
  tableLoading.value = true
  try {
    const res = await props.loadDataFn({
      page: pagination.value.page,
      rows: pagination.value.rows,
      ...(selectedTreeKey.value ? { filterValue: selectedTreeKey.value } : {}),
    })
    tableData.value = res?.data || []
    total.value = res?.total || tableData.value.length

    await nextTick()
    await nextTick()
    syncCheckboxes()
  } catch (e) {
    console.error('[YzhTreeCheckboxTable] 加载数据失败', e)
    tableData.value = []
    total.value = 0
  } finally {
    tableLoading.value = false
  }
}

/**
 * 将 checkedIds 同步到表格的 selection
 * 使用标志位 isSettingCheckboxes 防止触发 selection-change
 */
function syncCheckboxes() {
  if (tableRef.value && props.rowKeyField) {
    const rows = tableData.value
    const toCheck = rows.filter((row: any) => checkedIds.value.has(String(row[props.rowKeyField])))

    // 设置标志位，防止触发 selection-change 事件
    isSettingCheckboxes = true

    setTimeout(() => {
      toCheck.forEach((row: any) => {
        tableRef.value!.toggleRowSelection(row, true)
      })

      const allRows = tableRef.value!.data || []
      const checkedSet = new Set(toCheck.map((r: any) => String(r[props.rowKeyField])))
      tableRef.value!.clearSelection()
      allRows.forEach((row: any) => {
        if (checkedSet.has(String(row[props.rowKeyField]))) {
          nextTick(() => {
            tableRef.value!.toggleRowSelection(row, true, true)
          })
        }
      })

      // 延迟重置标志位
      setTimeout(() => { isSettingCheckboxes = false }, 200)
    }, 150)
  }
}

/**
 * Selection change handler
 * 关键：通过 isSettingCheckboxes 标志位区分「程序化设置」和「用户操作」
 */
function handleSelectionChange(rows: any[]) {
  // 程序化设置中 → 跳过
  if (isSettingCheckboxes) return

  const currentIds = new Set(rows.map((r: any) => String(r[props.rowKeyField])))
  const added: string[] = []
  const removed: string[] = []

  // 新增勾选的
  currentIds.forEach(id => {
    if (!checkedIds.value.has(id)) {
      added.push(id)
      checkedIds.value.add(id)
      if (!checkedIdsFromDb.value.has(id)) {
        pendingAddIds.value.add(id)
      } else {
        pendingRemoveIds.value.delete(id)
      }
    }
  })

  // 取消勾选的
  checkedIds.value.forEach(id => {
    if (!currentIds.has(id)) {
      removed.push(id)
      checkedIds.value.delete(id)
      if (checkedIdsFromDb.value.has(id)) {
        pendingRemoveIds.value.add(id)
      } else {
        pendingAddIds.value.delete(id)
      }
    }
  })

  emit('change', Array.from(checkedIds.value), tableData.value.map((r: any) => r[props.rowKeyField]))

  if (props.autoSave) {
    debouncedSync()
  }
}

// Select all / Deselect all
function handleSelectAll() {
  tableData.value.forEach((row: any) => {
    checkedIds.value.add(String(row[props.rowKeyField]))
    if (!checkedIdsFromDb.value.has(String(row[props.rowKeyField]))) {
      pendingAddIds.value.add(String(row[props.rowKeyField]))
    } else {
      pendingRemoveIds.value.delete(String(row[props.rowKeyField]))
    }
  })
  tableRef.value?.clearSelection()
  tableData.value.forEach((row: any) => {
    tableRef.value?.toggleRowSelection(row, true)
  })
  emit('change', Array.from(checkedIds.value), tableData.value.map((r: any) => r[props.rowKeyField]))
  if (props.autoSave) debouncedSync()
}

function handleDeselectAll() {
  const allIds = new Set(tableData.value.map((r: any) => String(r[props.rowKeyField])))
  allIds.forEach(id => {
    if (checkedIdsFromDb.value.has(id)) {
      pendingRemoveIds.value.add(id)
    } else {
      pendingAddIds.value.delete(id)
    }
  })
  checkedIds.value.clear()
  tableRef.value?.clearSelection()
  emit('change', [], tableData.value.map((r: any) => r[props.rowKeyField]))
  if (props.autoSave) debouncedSync()
}

// Manual save
async function handleSave() {
  await doSync()
}

// Refresh
async function handleRefreshTree() {
  treeLoading.value = true
  emit('refreshTree')
  await new Promise(r => setTimeout(r, 300))
  treeLoading.value = false
}

async function handleRefreshTable() {
  await loadTableData()
}

// Pagination
function handlePageChange(page: number) {
  pagination.value.page = page
  loadTableData()
}

function handleSizeChange(size: number) {
  pagination.value.rows = size
  pagination.value.page = 1
  loadTableData()
}

// Init
onMounted(async () => {
  const dictKeys = props.columns
    .filter((c: IColumn) => c.dataKey)
    .map((c: IColumn) => c.dataKey!)
    .filter((v, i, a) => a.indexOf(v) === i)

  if (dictKeys.length > 0) {
    await loadDictData(dictKeys)
  }

  if (props.treeData?.length > 0) {
    await handleTreeNodeClick(props.treeData[0])
  }
})

watch(() => props.treeData, (newData) => {
  if (newData && newData.length > 0) {
    nextTick(() => {
      handleTreeNodeClick(newData[0])
    })
  }
}, { immediate: false })

// Expose methods
defineExpose({
  get tree() { return treeRef.value },
  get table() { return tableRef.value },
  get checkedIds() { return Array.from(checkedIds.value) },
  getAllIds: () => tableData.value.map((r: any) => r[props.rowKeyField]),
  refresh: handleRefreshTable,
  save: handleSave,
  reload: async () => {
    await handleRefreshTree()
    await handleRefreshTable()
  },
})
</script>

<style lang="less" scoped>
.yzh-tree-checkbox-table {
  display: flex;
  height: 100%;
  overflow: hidden;
  border: 1px solid #ebeef5;
  border-radius: 4px;
  background: #fff;

  .left-tree {
    flex-shrink: 0;
    border-right: 1px solid #ebeef5;
    display: flex;
    flex-direction: column;
    overflow: hidden;

    .tree-header {
      padding: 12px 16px;
      font-weight: 600;
      font-size: 14px;
      border-bottom: 1px solid #ebeef5;
      background: #fafafa;
      display: flex;
      align-items: center;
      justify-content: space-between;
    }

    :deep(.el-tree) {
      flex: 1;
      overflow-y: auto;
      padding: 8px 0;

      .tree-node {
        display: inline-flex;
        align-items: center;

        .tree-label {
          margin-left: 4px;
        }
      }
    }
  }

  .right-table {
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    min-width: 0;

    .table-toolbar {
      flex-shrink: 0;
      padding: 10px 16px;
      border-bottom: 1px solid #ebeef5;
      background: #fafafa;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;

      .toolbar-left {
        display: flex;
        align-items: center;
        gap: 10px;

        .checked-info {
          font-size: 13px;
          color: #606266;

          strong { color: #409eff; font-weight: 600; }
        }
      }

      .toolbar-right {
        display: flex;
        align-items: center;
        gap: 4px;
      }
    }

    :deep(.el-table) { flex: 1; }

    .pagination-wrap {
      flex-shrink: 0;
      padding: 10px 16px;
      border-top: 1px solid #ebeef5;
      display: flex;
      justify-content: flex-end;
    }
  }
}
</style>
