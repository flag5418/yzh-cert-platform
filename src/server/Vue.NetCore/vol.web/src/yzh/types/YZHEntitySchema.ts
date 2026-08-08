// ============================================================
//  YZH Framework V2.0 —— 实体元信息 Schema
//  告诉基类：主键是什么、叫什么 Controller、如何排序
//  对应后端：ApiBaseController<TEntity, TKey>
// ============================================================

export type KeyType = 'guid' | 'number' | 'string'
export type SortOrder = 'asc' | 'desc'

export interface IYZHEntitySchema<TKey, TEntity> {
  /** 主键字段名，例如 'Id' / 'Code' */
  keyField: keyof TEntity & string

  /** 主键类型：guid / number / string */
  keyType: KeyType

  /** 默认排序字段（新增行自动插入时的定位依据） */
  defaultSortField: keyof TEntity & string

  /** 默认排序方向 */
  defaultSortOrder: SortOrder

  /** 后端 Controller 名（不含 Controller 后缀）
   *  自动拼 /api/{controllerName}/GetPageData ...
   */
  controllerName: string

  /** 后端数据库表名（用于 SaveModel.TableName），如 'cert_certification_body'
   *  如果不设置，默认使用 controllerName
   */
  tableName?: string

  /** 字典色自动映射：{ 字段名 : 字典编号 }，用于 Status 等 Tag 色列 */
  statusTagColors?: Partial<Record<keyof TEntity, string>>

  /** 后端 API 前缀，默认 '/api/' */
  apiPrefix?: string
}

// 后端 action 名称常量（对齐后端 ApiBaseController）
export const YZH_ACTIONS = Object.freeze({
  PAGE: 'GetPageData',
  ADD: 'Add',
  UPDATE: 'Update',
  DEL: 'Del',
  EXPORT: 'Export',
  IMPORT: 'Import',
  DOWN_TPL: 'DownLoadTemplate',
  UPLOAD: 'Upload',
} as const)
export type YZH_ACTION = (typeof YZH_ACTIONS)[keyof typeof YZH_ACTIONS]
