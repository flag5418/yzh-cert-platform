// ============================================================
//  YZH 前端框架 —— 通用 HTTP API 客户端（泛型）
//  参考：YZH 方案 V1.0 §4.1
//  职责：按 schema.controllerName 自动拼 URL，发 HTTP，
//        不涉及 Vue ref / Vol 实例，可在单元测试直接跑。
// ============================================================
import type { IYZHEntitySchema } from '@/types/yzh/YZHEntitySchema'
import { YZH_ACTIONS } from '@/types/yzh/YZHEntitySchema'

/** 从 main.js 的 getCurrentInstance().proxy 里取的全局对象（与原 Vol 等价） */
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
    // ⚠ 末尾必须带 '/'，详见 P2-06 踩坑记录
    this.base = `${this.prefix}${schema.controllerName}/`
  }

  // —— CRUD（Action 名对齐 ViewGrid/Action.js + 后端 ApiBaseController）——
  getPageData = <TResp = any>(param: TResp): Promise<any> =>
    this.proxy.http.post(`${this.base}${YZH_ACTIONS.PAGE}`, param)

  add = (saveModel: any) => this.proxy.http.post(`${this.base}${YZH_ACTIONS.ADD}`, saveModel)
  update = (saveModel: any) => this.proxy.http.post(`${this.base}${YZH_ACTIONS.UPDATE}`, saveModel)
  del = (ids: TKey[]) => this.proxy.http.post(`${this.base}${YZH_ACTIONS.DEL}`, { ids })

  // —— IO ——
  export = (param: any) => this.proxy.http.post(`${this.base}${YZH_ACTIONS.EXPORT}`, param, true)
  import = (formData: FormData) =>
    this.proxy.http.post(`${this.base}${YZH_ACTIONS.IMPORT}`, formData)
  downloadTpl = () => this.proxy.http.post(`${this.base}${YZH_ACTIONS.DOWN_TPL}`, {}, true)

  /** 手动拼任意 Action，用于后续扩展非标准 Action */
  custom = (action: string, data?: any, blob = false) =>
    this.proxy.http.post(`${this.base}${action}`, data, blob)
}
