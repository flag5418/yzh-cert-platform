// ============================================================
//  YZH Framework V2.0 —— 通用 HTTP API 客户端（泛型）
//  按 schema.controllerName 自动拼 URL
//  不涉及 Vue ref / Vol 实例，可在单元测试直接跑
// ============================================================

import type { IYZHEntitySchema } from '../types/YZHEntitySchema'
import { YZH_ACTIONS } from '../types/YZHEntitySchema'

export interface IYZHHttpProxy {
  http: {
    post: <TResp = any>(url: string, data?: any, isBlob?: boolean) => Promise<TResp>
  }
  $message?: {
    success?: (msg: string) => void
    error?: (msg: string) => void
    warning?: (msg: string) => void
    confirm?: (msg: string) => Promise<boolean>
  }
}

export class YZHBaseApiClient<TKey, TEntity> {
  private readonly prefix: string
  private readonly base: string

  constructor(
    public readonly schema: IYZHEntitySchema<TKey, TEntity>,
    private readonly proxy: IYZHHttpProxy
  ) {
    this.prefix = schema.apiPrefix ?? '/api/'
    // 末尾必须带 '/'
    this.base = `${this.prefix}${schema.controllerName}/`
  }

  // —— CRUD ——
  getPageData = <TResp = any>(param: TResp): Promise<any> =>
    this.proxy.http.post(`${this.base}${YZH_ACTIONS.PAGE}`, param)

  /** 新增：包装为 Vol SaveModel 格式（MainData + TableName） */
  add = (saveModel: any) => {
    const saveData = {
      TableName: this.schema.tableName || this.schema.controllerName,
      MainData: saveModel,
      DetailData: null,
      DelKeys: [],
    }
    return this.proxy.http.post(`${this.base}${YZH_ACTIONS.ADD}`, saveData)
  }

  /** 编辑：包装为 Vol SaveModel 格式 */
  update = (saveModel: any) => {
    const saveData = {
      TableName: this.schema.tableName || this.schema.controllerName,
      MainData: saveModel,
      DetailData: null,
      DelKeys: [],
    }
    return this.proxy.http.post(`${this.base}${YZH_ACTIONS.UPDATE}`, saveData)
  }

  /**
   * 删除：走 Vol 框架标准 Del 接口
   * 
   * ⚠️ Vol 框架的删除接口是 Del（不是 Remove）
   * - Vol 的 ServiceBase.Del(object[] keys) 期望直接接收数组
   * - 前端 POST 的 body 直接是 ids 数组: [id1, id2, ...]
   */
  del = (ids: TKey[]) => {
    console.log(`[YZHApiClient] 🗑️ 删除请求: ${this.base}Del, ids=`, ids)
    return this.proxy.http.post(`${this.base}Del`, ids)
  }

  // —— IO ——
  export = (param: any) => this.proxy.http.post(`${this.base}${YZH_ACTIONS.EXPORT}`, param, true)
  import = (formData: FormData) =>
    this.proxy.http.post(`${this.base}${YZH_ACTIONS.IMPORT}`, formData)
  downloadTpl = () => this.proxy.http.post(`${this.base}${YZH_ACTIONS.DOWN_TPL}`, {}, true)

  /** 手动拼任意 Action */
  custom = (action: string, data?: any, blob = false) =>
    this.proxy.http.post(`${this.base}${action}`, data, blob)
}
