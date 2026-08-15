<template>
  <div class="yzh-data-table">
    <el-table
      ref="tableRef"
      v-loading="loading"
      :data="data"
      :border="border"
      :stripe="stripe"
      :row-key="rowKey"
      :height="height"
      :max-height="maxHeight"
      :default-sort="defaultSortConfig"
      :style="{ width: '100%' }"
      :highlight-current-row="highlightCurrentRow"
      :row-class-name="rowClassName"
      :cell-class-name="cellClassName"
      @sort-change="onSortChange"
      @current-change="onCurrentRowChange"
      @selection-change="onSelectionChange"
      @row-click="onRowClick"
      @row-dblclick="onRowDbClick"
    >
      <!-- 多选列 -->
      <el-table-column
        v-if="checkboxSelection"
        type="selection"
        width="48"
        align="center"
        reserve-selection
      />

      <!-- 序号列 -->
      <el-table-column
        v-if="showRowNumber"
        type="index"
        label="#"
        width="55"
        align="center"
        :index="indexMethod"
      />

      <!-- 数据列（从配置动态生成） -->
      <el-table-column
        v-for="col in visibleColumns"
        :key="col.fieldAlias || col.fieldName"
        :prop="col.fieldName"
        :label="col.columnTitle"
        :width="col.columnWidth || (col.columnWidth === 0 ? 0 : undefined)"
        :min-width="col.minWidth"
        :sortable="col.sortable ? 'custom' : false"
        :align="col.align || 'left'"
        :show-overflow-tooltip="col.showOverflow !== false"
        :fixed="col.columnFixed"
        :formatter="col.formatter || undefined"
      >
        <!-- 自定义渲染插槽（按 fieldAlias 命名） -->
        <template #default="{ row, column, $index }">
          <!-- 优先使用具名插槽 -->
          <slot
            :name="`cell-${col.fieldAlias}`"
            :row="row"
            :column="column"
            :index="$index"
            :value="row[col.fieldName]"
          >
            <!-- 字典 Tag 渲染 -->
            <el-tag
              v-if="shouldRenderDictTag(col, row[col.fieldName])"
              :type="getDictTagType(row[col.fieldName], col)"
              size="small"
              disable-transitions
            >
              {{ formatDictValue(row[col.fieldName], col) }}
            </el-tag>
            <!-- 默认文本渲染 -->
            <span v-else>{{ row[col.fieldName] ?? '' }}</span>
          </slot>
        </template>
      </el-table-column>

      <!-- 操作列 -->
      <el-table-column
        v-if="showActionColumn"
        label="操作"
        :width="actionColumnWidth"
        align="center"
        fixed="right"
      >
        <template #default="{ row, $index }">
          <slot name="action" :row="row" :index="$index">
            <el-button link type="primary" size="small" @click="emit('edit', row)">修改</el-button>
            <el-button link type="danger" size="small" @click="emit('delete', row)">删除</el-button>
          </slot>
        </template>
      </el-table-column>

      <!-- 空数据插槽 -->
      <template #empty>
        <slot name="empty">
          <div class="yzh-data-table__empty">
            <p>暂无数据</p>
          </div>
        </slot>
      </template>
    </el-table>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { IYzhFieldConfig, IYzhColumnConfig } from '../types/YZHV3Config'

// ====== 类型定义 ======
export interface YzhTableColumn extends IYzhColumnConfig {
  /** 原始字段配置 */
  _raw?: IYzhFieldConfig
  /** 自定义格式化函数 */
  formatter?: (row: any, column: any, cellValue: any, index: number) => string
  /** 最小宽度 */
  minWidth?: number
}

// ====== Props ======
const props = withDefaults(defineProps<{
  /** 表格数据 */
  data: any[]
  /** 列配置（从 yzh_field_config 的 xsFlag=true 筛选而来） */
  columns: YzhTableColumn[]
  /** 是否加载中 */
  loading?: boolean
  /** 主键字段名 */
  rowKey?: string
  /** 表格高度（固定） */
  height?: string | number
  /** 表格最大高度 */
  maxHeight?: string | number
  /** 是否显示斑马纹 */
  stripe?: boolean
  /** 是否显示边框 */
  border?: boolean
  /** 是否高亮当前行 */
  highlightCurrentRow?: boolean
  /** 是否显示多选框 */
  checkboxSelection?: boolean
  /** 是否显示序号列 */
  showRowNumber?: boolean
  /** 是否显示操作列 */
  showActionColumn?: boolean
  /** 操作列宽度 */
  actionColumnWidth?: number
  /** 默认排序配置 */
  defaultSortField?: string
  defaultSortOrder?: 'asc' | 'desc'
  /** 行样式类名 */
  rowClassName?: ({ row, rowIndex }: { row: any; rowIndex: number }) => string
  /** 单元格样式类名 */
  cellClassName?: ({ row, column, rowIndex, columnIndex }: { row: any; column: any; rowIndex: number; columnIndex: number }) => string
}>(), {
  loading: false,
  rowKey: 'Id',
  stripe: true,
  border: true,
  highlightCurrentRow: true,
  checkboxSelection: true,
  showRowNumber: false,
  showActionColumn: true,
  actionColumnWidth: 160,
  defaultSortOrder: 'desc',
})

// ====== Emits ======
const emit = defineEmits<{
  (e: 'sortChange', { prop, order }: { prop: string; order: string | null }): void
  (e: 'currentChange', row: any): void
  (e: 'selectionChange', rows: any[]): void
  (e: 'rowClick', row: any, column: any, event: Event): void
  (e: 'rowDbClick', row: any): void
  (e: 'edit', row: any): void
  (e: 'delete', row: any): void
  (e: 'ready', instance: any): void
}>()

// ====== Refs ======
const tableRef = ref()

// ====== 计算属性 ======

/** 过滤可见列 */
const visibleColumns = computed(() =>
  props.columns.filter(col => col.visible !== false && col.columnTitle)
)

/** el-table 的默认排序配置 */
const defaultSortConfig = computed(() => {
  if (!props.defaultSortField) return undefined
  return {
    prop: props.defaultSortField,
    order: props.defaultSortOrder === 'asc' ? 'ascending' : 'descending',
  }
})

// ====== 方法 ======

/** 序号列计算方法（支持分页） */
function indexMethod(index: number): number {
  // 如果父组件传入了分页信息，可以在这里计算真实序号
  return index + 1
}

/** 排序变化 */
function onSortChange({ prop, order }: { prop: string; order: string | null }) {
  emit('sortChange', { prop, order })
}

/** 当前行变化 */
function onCurrentRowChange(row: any) {
  emit('currentChange', row)
}

/** 选择变化 */
function onSelectionChange(rows: any[]) {
  emit('selectionChange', rows)
}

/** 行点击 */
function onRowClick(row: any, column: any, event: Event) {
  emit('rowClick', row, column, event)
}

/** 行双击 */
function onRowDbClick(row: any) {
  emit('rowDbClick', row)
}

/**
 * 判断是否需要渲染字典 Tag
 * 规则：字段有 dataKey 且值不为空
 */
function shouldRenderDictTag(col: YzhTableColumn, value: any): boolean {
  const raw = col._raw
  if (!raw?.dataKey) return false
  if (value === null || value === undefined || value === '') return false
  return true
}

/** 获取字典 Tag 类型 */
function getDictTagType(value: any, col: YzhTableColumn): '' | 'success' | 'warning' | 'danger' | 'info' {
  const colorMap: Record<string, string> = {
    active: 'success',
    enabled: 'success',
    normal: 'success',
    inactive: 'danger',
    disabled: 'danger',
    blocked: 'danger',
    pending: 'warning',
    approved: 'success',
    rejected: 'danger',
  }
  return colorMap[String(value)] || 'info'
}

/** 格式化字典值（后续可扩展为查字典表） */
function formatDictValue(value: any, col: YzhTableColumn): string {
  if (value == null || value === '') return ''
  return String(value)
}

// ====== 暴露实例方法 ======
const exposedApi = {
  /** 获取 el-table 实例 */
  get table() { return tableRef.value },
  /** 清除选择 */
  clearSelection() { tableRef.value?.clearSelection() },
  /** 切换行选择状态 */
  toggleRowSelection(row: any, selected?: boolean) { tableRef.value?.toggleRowSelection(row, selected) },
  /** 清除排序 */
  clearSort() { tableRef.value?.clearSort() },
  /** 获取当前高亮行 */
  getCurrentRow() { return tableRef.value?.getCurrentRow() },
}

defineExpose(exposedApi)
</script>

<style scoped lang="scss">
.yzh-data-table {
  width: 100%;
  flex: 1;
  overflow: hidden;

  &__empty {
    padding: 40px 0;
    text-align: center;
    color: #909399;
  }
}
</style>
