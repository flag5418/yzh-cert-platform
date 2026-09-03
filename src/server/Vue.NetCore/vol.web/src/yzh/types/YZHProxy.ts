import type { ComponentPublicInstance } from 'vue'

export interface IYZHProxy extends ComponentPublicInstance {
  $message: any
  $alert: any
  $confirm: any
  $createElement: any
  http: {
    post: (url: string, data?: any, loading?: boolean | string, config?: any) => Promise<any>
    get: (url: string, data?: any, loading?: boolean | string, config?: any) => Promise<any>
  }
}
