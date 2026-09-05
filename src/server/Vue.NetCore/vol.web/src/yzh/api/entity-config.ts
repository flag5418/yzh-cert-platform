/**
 * Entity Config API
 * 从后端实体特性反射获取页面配置
 */
import http from '@/api/http'

export interface PageUIConfig {
  pageMeta: PageMeta
  fieldConfigs: FieldConfig[]
}

export interface PageMeta {
  pageKey: string
  pageTitle: string
  entityName: string
  tableName: string
  controllerName: string
  keyField: string
  keyFieldType: string
  sortField: string
  sortOrder: string
  dialogWidth: number
  dialogMaxHeight: string
  dialogLabelWidth: number
  searchMode: string
  visibleButtons: string[]
  showRowNumber: boolean
  checkboxSelection: boolean
  showActionColumn: boolean
}

export interface FieldConfig {
  fieldName: string
  fieldAlias: string
  fieldType: string
  isKey: boolean
  xsFlag: boolean
  columnSxh: number
  columnTitle: string
  columnWidth: number
  columnFixed: string | null
  sortable: boolean
  align: string
  showOverflow: boolean
  columnFormatter: string | null
  bcFlag: boolean
  formTitle: string
  controlType: string
  gridRow: number
  gridCol: number
  gridRowSpan: number
  gridColSpan: number
  required: boolean
  maxLength: number
  placeholder: string
  defaultValue: string
  readonly: boolean
  disabled: boolean
  dataKey: string | null
  remoteUrl: string | null
  precision: number
  minVal: number | null
  maxVal: number | null
  textareaRows: number
  searchFlag: boolean
  searchTitle: string
  searchPlaceholder: string
  searchControlType: string | null
  searchWidth: number
}

/**
 * 获取所有已注解实体的配置列表
 */
export async function getEntityConfigList(): Promise<Array<{
  entityName: string
  pageKey: string
  title: string
  controllerName: string
}>> {
  const res = await http.get('/api/entity-config/list')
  return res as any
}

/**
 * 获取指定实体的完整UI配置
 */
export async function getEntityConfig(entityName: string): Promise<PageUIConfig> {
  const res = await http.get(`/api/entity-config/${entityName}`)
  return res as any
}
