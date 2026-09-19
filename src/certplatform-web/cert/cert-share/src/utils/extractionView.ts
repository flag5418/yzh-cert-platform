import type {
  FieldDefDto,
  TableDefDto,
  ExtractionData,
} from '../api/workflow/doc-extraction-rule'

/**
 * 提取结果的「中文视图」
 *
 * 背景：AI 返回的键可能是英文编码（field_code/table_code/column_code），
 * 也可能是中文名（V2 分析模板的 rows 用中文列名），甚至还可能自作主张多输出一些条目。
 * 直接把原始对象丢给界面，会出现：
 *   · 看不懂的英文/中文键混排
 *   · 表格行键与列定义对不上 → 表格「有数据但显示为空」
 *   · AI 跑偏内容（如把表格列名当字段）与规则定义混在一起
 *
 * 因此显示一律以**规则定义（中文）**为准：
 *   1. 字段/表格清单来自规则定义，按定义顺序展示，未提取到的显式标「未提取到」
 *   2. 结果键先按编码匹配、再按中文名匹配，行键归一为列编码
 *   3. AI 多返回的、定义里没有的条目单列出来（便于发现模型跑偏），不混入正式清单
 */

export interface ViewColumn {
  /** 列编码（结果行取值键） */
  key: string
  /** 列中文名（表头显示） */
  label: string
}

export interface ViewField {
  code: string
  name: string
  dataType?: string
  value: unknown
  /** 是否真的取到了值（空串/undefined 视为未提取到） */
  extracted: boolean
}

export interface ViewTable {
  code: string
  name: string
  columns: ViewColumn[]
  /** 行数据（键已归一为列编码） */
  rows: Record<string, unknown>[]
  extracted: boolean
}

export interface ExtraItem {
  key: string
  /** 字段 = 单值；表格 = 行数组 */
  value: unknown
}

export interface ExtractionView {
  fields: ViewField[]
  tables: ViewTable[]
  /** AI 返回但规则定义中没有的字段（模型跑偏提示） */
  extraFields: ExtraItem[]
  /** AI 返回但规则定义中没有的表格 */
  extraTables: ExtraItem[]
  extractedFieldCount: number
  extractedTableCount: number
  extractedRowCount: number
}

function hasValue(v: unknown): boolean {
  if (v === null || v === undefined) return false
  if (typeof v === 'string') return v.trim() !== ''
  if (Array.isArray(v)) return v.length > 0
  return true
}

const norm = (v: unknown) => String(v ?? '').trim().toLowerCase()

/** 在「编码/中文名 → 键」的候选集合里找结果键 */
function pickByKey(
  record: Record<string, unknown> | undefined,
  code: string,
  name: string,
): { hit: boolean; value: unknown } {
  if (!record) return { hit: false, value: undefined }
  const keys = Object.keys(record)
  for (const want of [code, name]) {
    if (!want) continue
    const k = keys.find((x) => norm(x) === norm(want))
    if (k !== undefined) return { hit: true, value: record[k] }
  }
  return { hit: false, value: undefined }
}

/** 表格内某张表的列映射（中文名/编码 → 列编码） */
function buildColumnMap(def: TableDefDto, sampleRow?: Record<string, unknown>) {
  const columns: ViewColumn[] = (def.columns || []).map((c) => ({
    key: c.code || c.nameEn || c.name,
    label: c.name || c.code || c.nameEn || '',
  }))
  if (columns.length > 0) return columns
  // 无列定义时退回行键
  return Object.keys(sampleRow || {}).map((k) => ({ key: k, label: k }))
}

function normalizeRow(
  row: Record<string, unknown>,
  columns: ViewColumn[],
  def: TableDefDto,
): Record<string, unknown> {
  const out: Record<string, unknown> = {}
  const defCols = def.columns || []
  for (const [k, v] of Object.entries(row || {})) {
    // 键归一：先按列编码，再按列中文名
    const hitCode = defCols.find((c) => norm(c.code) === norm(k) || norm(c.nameEn) === norm(k))
    const hitName = hitCode || defCols.find((c) => norm(c.name) === norm(k))
    out[hitName ? hitName.code || hitName.nameEn || hitName.name : k] = v
  }
  // 补齐定义中的列，保证表格列头稳定
  for (const c of columns) if (!(c.key in out)) out[c.key] = ''
  return out
}

export function buildExtractionView(
  defFields: FieldDefDto[] | undefined,
  defTables: TableDefDto[] | undefined,
  data: ExtractionData | null | undefined,
): ExtractionView {
  const resultFields = (data?.fields || {}) as Record<string, unknown>
  const resultTables = (data?.tables || {}) as Record<string, Record<string, unknown>[]>

  const fields: ViewField[] = []
  const usedFieldKeys = new Set<string>()

  for (const d of defFields || []) {
    const code = d.code || d.nameEn || d.name
    const { hit, value } = pickByKey(resultFields, code, d.name)
    if (hit) usedFieldKeys.add(String(code).trim().toLowerCase())
    fields.push({
      code,
      name: d.name || code,
      dataType: d.dataType,
      value,
      extracted: hit && hasValue(value),
    })
  }

  const tables: ViewTable[] = []
  const usedTableKeys = new Set<string>()

  for (const d of defTables || []) {
    const code = d.code || d.nameEn || d.name
    const { hit, value } = pickByKey(resultTables, code, d.name)
    const rawRows = (hit && Array.isArray(value) ? value : []) as Record<string, unknown>[]
    const columns = buildColumnMap(d, rawRows[0])
    const rows = rawRows
      .filter((r) => r && typeof r === 'object' && !Array.isArray(r))
      .map((r) => normalizeRow(r, columns, d))
    if (hit) usedTableKeys.add(String(code).trim().toLowerCase())
    tables.push({
      code,
      name: d.name || code,
      columns,
      rows,
      extracted: rows.length > 0,
    })
  }

  // AI 多返回的内容：定义中不存在的键
  const extraFields: ExtraItem[] = Object.entries(resultFields)
    .filter(([k]) => !usedFieldKeys.has(norm(k)) && !(defFields || []).some((d) => norm(d.name) === norm(k)))
    .map(([key, value]) => ({ key, value }))

  const extraTables: ExtraItem[] = Object.entries(resultTables)
    .filter(([k]) => !usedTableKeys.has(norm(k)) && !(defTables || []).some((d) => norm(d.name) === norm(k)))
    .map(([key, value]) => ({ key, value }))

  return {
    fields,
    tables,
    extraFields,
    extraTables,
    extractedFieldCount: fields.filter((f) => f.extracted).length,
    extractedTableCount: tables.filter((t) => t.extracted).length,
    extractedRowCount: tables.reduce((n, t) => n + t.rows.length, 0),
  }
}
