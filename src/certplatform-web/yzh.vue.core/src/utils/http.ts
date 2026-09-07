import axios, { type AxiosInstance, type AxiosResponse } from 'axios'

// API 响应结构（与后端 WebResponseContent 对齐）
export interface ApiResponse<T = any> {
  code: number
  message: string
  data: T
}

// 登录响应 data 结构
export interface LoginData {
  token: string
  userName: string
  img?: string
}

// axios 实例
const http: AxiosInstance = axios.create({
  baseURL: '/api',
  timeout: 30000,
  headers: { 'Content-Type': 'application/json' }
})

// 请求拦截器：注入 token
http.interceptors.request.use(
  (config) => {
    // 从 localStorage 读取 token
    const token = localStorage.getItem('YZH_TOKEN')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => Promise.reject(error)
)

// 响应拦截器：归一化处理
// 统一输出格式：{code: 200, message: string, data: any}
http.interceptors.response.use(
  (response: AxiosResponse) => {
    const res = response.data

    // 1. Vol 框架格式：{status: true/false, message: string, data: any}
    if (typeof res.status === 'boolean') {
      if (res.status === true) {
        // 成功，包装为标准格式
        return { code: 200, message: res.message || 'success', data: res.data } as any
      }
      // 失败
      const msg = res.message || '请求失败'
      if (msg.includes('token') || msg.includes('登录') || msg.includes('未授权')) {
        localStorage.removeItem('YZH_TOKEN')
        if (window.location.pathname !== '/login') {
          window.location.href = '/login'
        }
      }
      return Promise.reject(new Error(msg))
    }

    // 2. Restful 标准格式：{code: 200, message: '...', data: ...}
    if (typeof res.code === 'number') {
      if (res.code === 200) {
        return res as any
      }
      if (res.code === 401 || res.message?.includes('token') || res.message?.includes('登录')) {
        localStorage.removeItem('YZH_TOKEN')
        if (window.location.pathname !== '/login') {
          window.location.href = '/login'
        }
      }
      return Promise.reject(new Error(res.message || '请求失败'))
    }

    // 3. 直接返回数据的接口（如验证码 {img, uuid}），包装为标准格式
    return { code: 200, message: 'success', data: res } as any
  },
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('YZH_TOKEN')
      if (window.location.pathname !== '/login') {
        window.location.href = '/login'
      }
    }
    const msg = error.response?.data?.message || error.message || '网络错误'
    return Promise.reject(new Error(msg))
  }
)

export default http
