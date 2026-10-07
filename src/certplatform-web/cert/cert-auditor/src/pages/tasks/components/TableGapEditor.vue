<script setup lang="ts">
/**
 * TableGapEditor —— 「关键表格信息补录」的可编辑小表格
 *
 * 【为什么要有这个组件】
 *   旧实现让审核员**手填 JSON 数组**（`detail.vue` 的 `doFill` 要求
 *   `[{"列名":"值"}]`）—— 用户原话「填写的信息不是中文，全是字段的英文和看不懂的编号」。
 *   而表格的**列定义本来就存在**（`cert_doc_table_field_def` → 后端 `GapColumn`），
 *   所以这里直接用「中文列头 + 逐格输入」渲染，⛔ 不需要用户理解任何 JSON。
 *
 * 【为什么不用 `YzhTable`】
 *   `YzhTable` 是**分页数据表**（`data-loader` + 分页 + 列配置驱动），
 *   而这里是「**表单控件**」—— 行由用户增删、格子是输入框、没有分页。
 *   所以用原生 `<table>` + `v-for` 拼版式（法条 R6 禁的是**页面内联 `<el-table>`**，
 *   ⛔ 不针对原生 `<table>`）。
 *
 * 【用法】
 *   `<TableGapEditor v-model="rows" :columns="gap.Columns" />`
 *   `rows` = `[{ <列Code>: '值', … }, …]`，行内 key 一律用**列 Code**（PascalCase 铁律）。
 *   父组件提交时 `JSON.stringify(rows)` 即后端要的 `Value`（JSON 数组字符串）。
 */
import { computed } from 'vue'
import type { GapColumn } from '@share/api/auditor/expert-task'

/**
 * 一行 = 列 Code → 文本值。
 * ⚠️ 类型只在本文件内用（`<script setup>` 不允许 `export`）——
 *    父组件按 `Record<string, string>[]` 使用即可。
 */
type TableGapRow = Record<string, string>

const props = withDefaults(
  defineProps<{
    /** 列定义（中文列头 + 类型），来源 = `cert_doc_table_field_def` */
    columns: GapColumn[]
    /** 行数据（v-model） */
    modelValue: TableGapRow[]
    disabled?: boolean
    addText?: string
  }>(),
  { disabled: false, addText: '添加一行' },
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: TableGapRow[]): void
}>()

const rows = computed<TableGapRow[]>(() => props.modelValue ?? [])

/** 新行：所有列都置空字符串，保证列结构完整（少 key 会被后端当缺列） */
function emptyRow(): TableGapRow {
  const r: TableGapRow = {}
  for (const c of props.columns) r[c.Code] = ''
  return r
}

function addRow() {
  emit('update:modelValue', [...rows.value, emptyRow()])
}

function removeRow(index: number) {
  const next = rows.value.filter((_, i) => i !== index)
  // 至少留一行：全删光后界面会变成空壳，用户找不到入口再加
  emit('update:modelValue', next.length > 0 ? next : [emptyRow()])
}

function setCell(index: number, code: string, value: string) {
  emit(
    'update:modelValue',
    rows.value.map((r, i) => (i === index ? { ...r, [code]: value } : r)),
  )
}
</script>

<template>
  <div class="tge">
    <div class="tge__scroll">
      <table class="tge__table">
        <thead>
          <tr>
            <th class="tge__idx">#</th>
            <th v-for="c in columns" :key="c.Code">{{ c.Name }}</th>
            <th class="tge__op">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(row, i) in rows" :key="i">
            <td class="tge__idx">{{ i + 1 }}</td>
            <td v-for="c in columns" :key="c.Code">
              <el-input
                :model-value="row[c.Code] ?? ''"
                size="small"
                :disabled="disabled"
                :placeholder="c.Name"
                @update:model-value="(v: string) => setCell(i, c.Code, v)"
              />
            </td>
            <td class="tge__op">
              <el-button type="danger" link :disabled="disabled" @click="removeRow(i)">
                删除
              </el-button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="tge__bar">
      <el-button type="primary" plain size="small" :disabled="disabled" @click="addRow">
        {{ addText }}
      </el-button>
      <span class="tge__hint">共 {{ rows.length }} 行 · {{ columns.length }} 列</span>
    </div>
  </div>
</template>

<style scoped>
.tge {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
}

/* 列多时横向滚动，⛔ 不让它把抽屉撑宽 */
.tge__scroll {
  overflow-x: auto;
  border: 1px solid var(--yzh-color-border, #e4e7ed);
  border-radius: var(--yzh-radius-sm, 4px);
}

.tge__table {
  width: 100%;
  border-collapse: collapse;
}

.tge__table th,
.tge__table td {
  padding: var(--yzh-space-1, 4px) var(--yzh-space-2, 8px);
  border-bottom: 1px solid var(--yzh-color-border, #e4e7ed);
  text-align: left;
  vertical-align: middle;
}

.tge__table thead th {
  background: var(--el-fill-color-light);
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: 600;
  color: var(--el-text-color-regular);
  white-space: nowrap;
}

.tge__table tbody tr:last-child td {
  border-bottom: none;
}

.tge__table td {
  min-width: 140px;
}

.tge__idx {
  width: 40px;
  min-width: 40px;
  color: var(--el-text-color-secondary);
  font-size: var(--yzh-font-size-xs, 12px);
  text-align: center;
}

.tge__op {
  width: 64px;
  min-width: 64px;
  text-align: center;
}

.tge__bar {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-3, 12px);
}

.tge__hint {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-secondary);
}
</style>
