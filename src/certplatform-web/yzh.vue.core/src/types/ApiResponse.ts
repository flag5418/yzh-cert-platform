/** API 响应包装 */
export interface ApiResponse<T = any> {
  status: number
  msg: string | null
  data?: T
  rows?: T[]
  total?: number
  [key: string]: any
}
