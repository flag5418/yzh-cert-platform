// ============================================================
//  YZH 前端框架 —— 4 类基类窗体 Props 类型
//  参考：YZH 方案 V1.0 §3 基类规划
// ============================================================
import type { IYZHEntitySchema } from './YZHEntitySchema'
import type { IYZHPageLifecycle } from './YZHLifecycles'

/** 顶部 8 类按钮开关（业务页传 false 可单独关闭） */
export interface IYZHButtons {
  add: boolean
  refresh: boolean
  import: boolean
  export: boolean
  column: boolean
  /** 编辑模式按钮（只控制多选框显示/隐藏，与删除解耦） */
  editMode: boolean
  /** 顶部批量删除（用户 §1 后常显，无选中时提示） */
  batchDelete: boolean
  sort: boolean
}

/** 外部过滤条件（左树右表场景：CbCode=xxx 这种父级限定） */
export interface IYZHExternalFilter {
  name: string
  value: any
  /** where 拼接方式，默认 '==' */
  cond?: '==' | '!=' | '>' | '<' | '>=' | '<=' | 'like' | 'contains'
}

/** 单表基类 Props */
export interface IYZHSingleTableProps<TKey, TEntity> {
  /** （必填）实体 Schema：主键 / 排序 / 后端 Controller 名 */
  schema: IYZHEntitySchema<TKey, TEntity>
  /** （必填）原 Vol 生成器输出的 options.js，原样传入即可 */
  options: any
  /** （可选）业务生命周期钩子，按需实现 */
  lifecycles?: Partial<IYZHPageLifecycle<TKey, TEntity>>
  /** 是否启用 CRUD 增量刷新（默认 true，关闭后走 Vol 原生 search()） */
  incrementalUpdate?: boolean
  /** 顶部按钮开关（全部默认 true） */
  buttons?: Partial<IYZHButtons>
  /** 查询条模式：fixed=默认展开；togglable=可折叠 */
  searchMode?: 'fixed' | 'togglable'
  /** 外部过滤条件（父节点筛选、左右树右表） */
  externalFilter?: IYZHExternalFilter[]
  /** 是否在 columns 末尾追加「操作」列（行级修改/删除，默认 true） */
  showActionColumn?: boolean
}

/** 左树右表 Props（M3 预告） */
export interface IYZHTreeTableProps<TKey, TEntity> extends IYZHSingleTableProps<TKey, TEntity> {
  /** 左树数据源 API（如 ISOClauseTree） */
  treeControllerName: string
  /** 左树点击时，对应右表 externalFilter 里的字段名，如 'CbCode' / 'ParentCode' */
  filterField: string
  /** 左树展示字段，默认 'Name' */
  treeLabelField?: string
  /** 左树主键字段，默认 'Id' */
  treeKeyField?: string
}
