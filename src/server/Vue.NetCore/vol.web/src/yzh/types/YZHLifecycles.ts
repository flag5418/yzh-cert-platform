// ============================================================
//  YZH Framework V2.0 —— 生命周期钩子类型定义
//  对齐后端：ServiceBase / Partial Service 的 OnExecuting/OnExecuted
// ============================================================

// false = 阻断当前操作，true / Promise<true> / void = 继续
export type YZHGuardResult = boolean | void | Promise<boolean | void>

// 列表加载阶段
export interface ILifecycleLoad<TEntity = any, TParam = any> {
  /** 查询参数发后端前（可改 wheres / sort） */
  onLoadBefore?: (param: TParam) => YZHGuardResult
  /** 后端返回数据后（可二次加工 rows，例如翻译字典） */
  onLoadAfter?: (rows: TEntity[], rawResponse?: any) => TEntity[] | void | Promise<TEntity[] | void>
}

// 新增阶段
export interface ILifecycleAdd<TEntity = any, TResult = any> {
  onAddBefore?: (formData: Partial<TEntity>) => YZHGuardResult
  onAddSaveBefore?: (main: TEntity) => YZHGuardResult
  onAddSaveAfter?: (main: TEntity, result?: TResult) => void | Promise<void>
}

// 修改阶段
export interface ILifecycleUpdate<TEntity = any, TResult = any> {
  onUpdateBefore?: (row: TEntity, formData: Partial<TEntity>) => YZHGuardResult
  onUpdateSaveBefore?: (main: TEntity) => YZHGuardResult
  onUpdateSaveAfter?: (main: TEntity, result?: TResult) => void | Promise<void>
}

// 删除阶段
export interface ILifecycleDelete<TKey = any, TEntity = any> {
  onDeleteBefore?: (rows: TEntity[], ids: TKey[]) => YZHGuardResult
  onDeleteAfter?: (ids: TKey[]) => void | Promise<void>
}

// 导入 / 导出
export interface ILifecycleIO<TEntity = any> {
  onImportBefore?: (formData: FormData) => YZHGuardResult
  onImportAfter?: (importedRows?: TEntity[]) => void | Promise<void>
  onExportBefore?: (param: any) => YZHGuardResult
  onExportAfter?: (blob: Blob) => void | Promise<void>
}

// 行 / 单元格交互（UX 扩展）
export interface ILifecycleRow<TEntity = any> {
  onRowSelect?: (row: TEntity | null, selectedRows: TEntity[]) => void
  onRowClick?: (row: TEntity) => void
  onRowDbClick?: (row: TEntity) => void
  onEditModeChange?: (editing: boolean) => void
}

// ——— 总聚合 ——//
export interface IYZHPageLifecycle<TKey, TEntity>
  extends
    ILifecycleLoad<TEntity>,
    ILifecycleAdd<TEntity>,
    ILifecycleUpdate<TEntity>,
    ILifecycleDelete<TKey, TEntity>,
    ILifecycleIO<TEntity>,
    ILifecycleRow<TEntity> {}

// 基类内部用到的「完整钩子表 + schema 绑定」组合
export interface IYZHContext<TKey, TEntity> {
  schema: IYZHEntitySchema<TKey, TEntity>
  lifecycles: IYZHPageLifecycle<TKey, TEntity>
}
