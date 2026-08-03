// ============================================================
//  YZH 前端框架 —— 实体元信息 Schema
//  参考：YZH 方案 V1.0 §4.1
//  对应后端：ApiBaseController<TEntity, TKey>
//  用途：告诉基类「这张表的主键是什么、叫什么 Controller、如何排序」
// ============================================================

export type KeyType = 'guid' | 'number' | 'string';
export type SortOrder = 'asc' | 'desc';

export interface IYZHEntitySchema<TKey, TEntity> {
  /** 主键字段名（三端对齐，PascalCase），例如 'Id' / 'Code' */
  keyField: keyof TEntity & string;

  /** 主键类型：guid 字符串比较 / number 数字比较 / string 通用比较 */
  keyType: KeyType;

  /** 默认排序字段（新增行自动插入时的定位依据） */
  defaultSortField: keyof TEntity & string;

  /** 默认排序方向 */
  defaultSortOrder: SortOrder;

  /** 后端 Controller 名，自动拼 /api/{controllerName}/GetPageData ...
   *  ⚠ 必须与 VOL.Sys/Controllers/ 下真实类名严格一致（不含 Controller 后缀）
   */
  controllerName: string;

  /** （可选）字典色自动映射：{ 字段名 : 字典编号 }，用于 Status 等 Tag 色列 */
  statusTagColors?: Partial<Record<keyof TEntity, string>>;

  /** （可选）后端 API 前缀，默认 '/api/'，若后续改网关可统一改此处 */
  apiPrefix?: string;
}

// 后端 action 名称常量（对齐 components/basic/ViewGrid/Action.js，不得自定义）
export const YZH_ACTIONS = Object.freeze({
  PAGE:       'GetPageData',
  ADD:        'Add',
  UPDATE:     'Update',
  DEL:        'Del',
  EXPORT:     'Export',
  IMPORT:     'Import',
  DOWN_TPL:   'DownLoadTemplate',
  UPLOAD:     'Upload',
  AUDIT:      'Audit',
} as const);
export type YZH_ACTION = (typeof YZH_ACTIONS)[keyof typeof YZH_ACTIONS];
