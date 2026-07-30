<template>
  <vol-box :width="width" title="配置默认排序字段" :height="height + 10" :padding="10" v-model="model">
    <el-alert title="左侧点击字段加入右侧；右侧可拖拽排序" class="alert-primary" :closable="false"></el-alert>
    <div class="sort-field-body">
      <div class="sort-field-left">
        <div style="margin:8px 0;">
          <vol-title title="表字段" icon="MessageBox">
          </vol-title>
        </div>
        <el-scrollbar class="sort-field-scroll" :height="height - 78">
          <div v-for="(col, index) in columnList" :key="col.columnName" class="sort-field-item"
            :class="{ 'sort-field-item--picked': isFieldPicked(col) }" @click="onPickField(col)">
            {{ index + 1 }}. {{ fieldLineText(col) }}
          </div>
          <div v-if="!columnList.length" class="sort-field-empty">暂无字段，请先加载表结构</div>
        </el-scrollbar>
      </div>
      <div class="sort-field-right">
        <div style="margin:8px 0;">
          <vol-title title="已选排序" icon="Edit">
            <span style="color: #41a940; font-size: 13px" class="el-icon-info">拖拽行可调整优先级</span>
          </vol-title>
        </div>
        <div class="sort-sql-preview" title="点击复制 SQL" @click="copyPreviewSql">
          {{ orderByPreviewText }}
        </div>
        <vol-table ref="tableRef" :column-index="true" :ck="false" :table-data="selectedRows" :columns="tableColumns"
          :max-height="height - 93" :sortable="true" :text-inline="false" :pagination-hide="true"
          @onSortEnd="onSortEnd"></vol-table>
      </div>
    </div>
    <template #footer>
      <div style="text-align: center;">
        <el-button type="default" size="small" @click="model = false"><i class="el-icon-close"></i> 关闭</el-button>
        <el-button type="primary" color="#4f58d2" size="small" @click="onConfirm"><i class="el-icon-check"></i>
          保存</el-button>
      </div>
    </template>
  </vol-box>
</template>

<script setup lang="jsx">
import { ref, computed, getCurrentInstance } from 'vue'

const { proxy } = getCurrentInstance()
const emit = defineEmits(['onSortConfirm'])

const model = ref(false)
const tableRef = ref()
const columnList = ref([])
const selectedRows = ref([])
/** 预览 SQL 中 from 后的表名（实际表名） */
const sqlTableName = ref('table')

const height = ref(Math.min(560, document.body.clientHeight * 0.82))
const width = ref(800)

const tableColumns = ref([
  {
    field: 'columnCnName',
    title: '中文名',
    width: 100,
    align: 'left',
  },
  {
    field: 'columnName',
    title: '字段',
    width: 110,
    align: 'left',
  },
  {
    field: 'sortOrder',
    title: '排序方式',
    width: 80,
    align: 'left',
    edit: { type: "select", keep: true },
    bind: { data: [{ key: "asc", value: "asc" }, { key: "desc", value: "desc" }] }
  },
  {
    title: "操作",
    field: "_action",
    width: 70,
    align: "center",
    render: (h, { row, index }) => {
      return (
        <div style="display:flex;">
          <el-button color="#0425dd" link plain size="small"
            style="padding: 5px 3px !important;margin: 0;" onClick={() => removeRow(index)}> <i style="font-size:13px" class="el-icon-delete"></i>删除</el-button>
          {/* <el-button color="#0425dd" link plain size="small"
            style="padding: 5px 3px !important;margin: 0;margin-left:0" onClick={() => configClick(row)}> <i style="font-size:13px" class="el-icon-edit-outline"></i>配置</el-button> */}
        </div>
      )
    }
  }
  // render: (h, { row, index }) => {
  //   <div style="display:flex;">
  //     <el-button color="#0425dd" link plain size="small"
  //       style="padding: 5px 3px !important;margin: 0;" onClick={() => removeRow(index)}> <i style="font-size:13px" class="el-icon-delete"></i>删除</el-button>
  //     {/* <el-button color="#0425dd" link plain size="small"
  //         style="padding: 5px 3px !important;margin: 0;margin-left:0" onClick={() => configClick(row)}> <i style="font-size:13px" class="el-icon-edit-outline"></i>配置</el-button> */}
  //   </div>
  // }
])

/** 左侧已加入右侧排序的字段高亮 */
const isFieldPicked = (col) => {
  const name = (col.columnName || '').trim()
  if (!name) return false
  return selectedRows.value.some((r) => r.columnName === name)
}

/** 可执行的 order by SQL 片段（不含「排序预览」前缀，用于复制） */
const orderBySqlPlain = computed(() => {
  const fromName = sqlTableName.value || 'table'
  const parts = selectedRows.value
    .filter((r) => r.columnName)
    .map((r) => `${r.columnName} ${normalizeOrder(r.sortOrder)}`)
  const tail = parts.join(', ')
  return `select * from ${fromName} order by ${tail}`
})

/** 排序 SQL 预览文案（含说明前缀） */
const orderByPreviewText = computed(() => {
  return `排序预览：${orderBySqlPlain.value}`
})

const copyPreviewSql = async () => {
  const text = orderBySqlPlain.value
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text)
    } else {
      const ta = document.createElement('textarea')
      ta.value = text
      ta.style.position = 'fixed'
      ta.style.left = '-9999px'
      document.body.appendChild(ta)
      ta.select()
      document.execCommand('copy')
      document.body.removeChild(ta)
    }
    proxy.$message.success('SQL已复制到粘贴板')
  } catch {
    proxy.$message.error('复制失败，请手动复制')
  }
}

/** 左侧展示：字段 + 中文名（无中文名则不显示中文名） */
const fieldLineText = (col) => {
  const name = col.columnName || ''
  const cn = (col.columnCnName || '').trim()
  return cn ? `${cn}(${name})` : name
}

/** 左侧点击添加时最多可选字段数（回写/解析/保存不截断） */
const MAX_SORT_FIELDS = 20

const normalizeOrder = (v) => {
  const s = String(v == null ? 'asc' : v).toLowerCase()
  return s === 'desc' ? 'desc' : 'asc'
}

/** 非 JSON 时：整段字符串是否为当前表某一列的字段名（兼容旧版存单个字段名） */
const findColumnByFieldToken = (token, columns) => {
  const t = String(token || '').trim()
  if (!t) {
    return null
  }
  const cols = columns || []
  const byExact = cols.find((c) => c.columnName === t)
  if (byExact) {
    return byExact
  }
  const tl = t.toLowerCase()
  return cols.find((c) => String(c.columnName || '').toLowerCase() === tl) || null
}

/** 从 JSON 字符串解析右侧表格行；非合法 JSON 时若整段为表字段名则作为单列默认排序 */
const parseToRows = (sortStr, columns) => {
  const rows = []
  const map = new Map((columns || []).map((c) => [c.columnName, c]))
  if (sortStr == null || !String(sortStr).trim()) {
    return rows
  }
  const raw = String(sortStr).trim()
  let obj
  try {
    obj = JSON.parse(raw)
  } catch {
    const col = findColumnByFieldToken(raw, columns)
    if (col?.columnName) {
      return [
        {
          columnName: col.columnName,
          columnCnName: (col.columnCnName || '').trim(),
          sortOrder: 'asc',
        },
      ]
    }
    return rows
  }
  if (!obj || typeof obj !== 'object' || Array.isArray(obj)) {
    return rows
  }
  for (const key of Object.keys(obj)) {
    const meta = map.get(key)
    const cn = meta?.columnCnName != null ? String(meta.columnCnName) : ''
    rows.push({
      columnName: key,
      columnCnName: cn,
      sortOrder: normalizeOrder(obj[key]),
    })
  }
  return rows
}

const onPickField = (col) => {
  const name = (col.columnName || '').trim()
  if (!name) {
    return
  }
  if (selectedRows.value.some((r) => r.columnName === name)) {
    proxy.$message.error('字段已使用请重新选择')
    return
  }
  if (selectedRows.value.length >= MAX_SORT_FIELDS) {
    proxy.$message.warning(`最多只能选择 ${MAX_SORT_FIELDS} 个排序字段`)
    return
  }
  selectedRows.value.push({
    columnName: name,
    columnCnName: (col.columnCnName || '').trim(),
    sortOrder: 'asc',
  })
}

const removeRow = (index) => {
  if (index >= 0 && index < selectedRows.value.length) {
    selectedRows.value.splice(index, 1)
  }
}

const onSortEnd = (rows) => {
  if (Array.isArray(rows)) {
    selectedRows.value = rows
  }
}

const onConfirm = () => {
  const obj = {}
  selectedRows.value.forEach((r) => {
    if (r.columnName) {
      obj[r.columnName] = normalizeOrder(r.sortOrder)
    }
  })
  emit('onSortConfirm', JSON.stringify(obj))
  model.value = false
}

/**
 * @param {string} sortNameJson 表单中的默认排序字段（JSON 字符串）
 * @param {Array} tableColumns 当前表结构行
 * @param {string} [physicalTableName] 当前操作表的实际表名（如 tableTrueName），用于 SQL 预览
 */
const show = (sortNameJson, tableColumns, physicalTableName) => {
  const tn = physicalTableName != null ? String(physicalTableName).trim() : ''
  sqlTableName.value = tn || 'table'
  columnList.value = Array.isArray(tableColumns) ? [...tableColumns] : []
  selectedRows.value = parseToRows(sortNameJson, columnList.value)
  model.value = true
}

defineExpose({
  show,
})
</script>

<style lang="less" scoped>
.sort-field-body {
  display: flex;
  min-height: 380px;
  gap: 12px;
}

.sort-field-left {
  width: 260px;
  border-right: 1px solid #eee;
  // padding-right: 8px;
  display: flex;
  flex-direction: column;
}

.sort-field-scroll {
  flex: 1;

  :deep(.el-scrollbar__bar.is-vertical) {
    width: 2px !important;
  }
}

.sort-field-item {
  padding: 6px 8px;
  // margin-bottom: 4px;
  border-radius: 4px;
  cursor: pointer;
  font-size: 13px;
  line-height: 1.45;
  color: #303133;
  border: 1px solid transparent;

  &:hover {
    background: #f0f4ff;
    border-color: #c6d2ff;
  }

  &--picked {
    color: #0425dd;
    font-weight: 500;

    &:hover {
      color: #0425dd;
    }
  }
}

.sort-sql-preview {
  font-size: 13px;
  line-height: 1.5;
  color: #0040cb;
  margin-bottom: 5px;
  word-break: break-all;
  cursor: pointer;

  &:hover {
    text-decoration: underline;
    opacity: 0.92;
  }
}

.sort-field-empty {
  padding: 16px 8px;
  color: #909399;
  font-size: 13px;
}

.sort-field-right {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
}
</style>
