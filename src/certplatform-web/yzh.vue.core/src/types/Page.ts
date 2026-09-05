/** 分页响应 */
export interface Page<T = any> {
  rows: T[]
  total: number
}

/** 分页请求参数 */
export interface PageParams {
  page: number
  rows: number
  sort?: string
  order?: 'asc' | 'desc' | 'ascending' | 'descending'
  [key: string]: any
}
