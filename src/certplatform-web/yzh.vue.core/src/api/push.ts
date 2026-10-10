import * as signalR from '@microsoft/signalr'
import { ElNotification, ElMessageBox } from 'element-plus'
import { tokenStore } from './client'
import { useRouter } from 'vue-router'

/**
 * 推送信封（服务端 YzhPushMessage 经 SignalR 默认 camelCase 序列化输出）。
 * 信封例外口径同 ApiResponse E1：信封字段不走 PascalCase 逐字一致铁律。
 */
export interface YzhPushMessage {
  title?: string
  message?: string
  date?: string
  /** 事件类型（见 YzhPushValues：queue_progress / logout）—— 按此分发 */
  value?: string
  data?: unknown
}

export type YzhPushHandler = (msg: YzhPushMessage) => void

const handlers = new Map<string, Set<YzhPushHandler>>()
let connection: signalR.HubConnection | null = null
let starting = false

function dispatch(msg: YzhPushMessage): void {
  if (!msg || !msg.value) return
  // 默认行为：有标题就弹通知（队列终态等）；logout 由订阅方处理、不弹窗
  if (msg.title && msg.value !== 'logout') {
    ElNotification({ title: msg.title, message: msg.message || '', type: 'info' })
  }
  const set = handlers.get(msg.value)
  if (set) {
    for (const fn of [...set]) {
      try {
        fn(msg)
      } catch (e) {
        console.error('[yzhPush] handler error:', e)
      }
    }
  }
}

/**
 * 强制下线处理器（P2b②）
 *
 * 由布局组件（YzhAppLayout / AuditorLayout）在 onMounted 时注册到 yzhPush.on('logout')。
 * 触发后：弹出 ElMessageBox.confirm（不可关闭、5 秒自动跳转），确认后清 token 跳登录页。
 */
export function handleForceLogout(reason?: string): void {
  const msg = reason || '您的账号已在其他设备登录，当前会话已失效。'
  let timer: ReturnType<typeof setTimeout> | null = null

  const hide = () => {
    if (timer !== null) {
      clearTimeout(timer)
      timer = null
    }
  }

  ElMessageBox.confirm(msg, '账号已下线', {
    confirmButtonText: '前往登录',
    cancelButtonText: '',
    type: 'warning',
    center: true,
    showCancelButton: false,
    closeOnClickModal: false,
    closeOnPressEscape: false,
    showClose: false,
  })
    .then(() => {
      hide()
      tokenStore.clear()
      useRouter().push('/login').catch(() => {})
    })
    .catch(() => {
      // 用户点"取消"（实际无按钮，但防御性处理）：5 秒后自动跳转
      timer = setTimeout(() => {
        tokenStore.clear()
        useRouter().push('/login').catch(() => {})
      }, 5000)
    })

  // 5 秒自动超时兜底
  timer = setTimeout(() => {
    hide()
    tokenStore.clear()
    useRouter().push('/login').catch(() => {})
  }, 5000)
}

/**
 * yzh 实时推送客户端（框架核心能力，对应后端 YzhMessageHub /api/yzh-msg）
 *
 * - 连接：登录后由 YzhAppLayout onMounted 调 connect()；票走 accessTokenFactory（每次握手现取，含重连）
 * - 身份：服务端从已验证 JWT 派生分组，客户端无需也无法自报身份
 * - 兜底：推送丢失不影响业务 —— 页面级轮询（如 enterprise-normalize 的 batch 轮询）保留为断线兜底
 * - 事件：服务端只发 ReceiveYzhMessage，按 msg.value 分发
 */
export const yzhPush = {
  /** 订阅某 value 的推送；返回取消函数 */
  on(value: string, fn: YzhPushHandler): () => void {
    if (!handlers.has(value)) handlers.set(value, new Set())
    handlers.get(value)!.add(fn)
    return () => {
      handlers.get(value)?.delete(fn)
    }
  },

  /** 建立连接（幂等；无 token 不连）。失败只告警，下次进入页面再试 */
  connect(): void {
    if (connection || starting) return
    if (!tokenStore.get()) return
    starting = true
    const url = new URL('/api/yzh-msg', window.location.origin).toString()
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(url, { accessTokenFactory: () => tokenStore.get() || '' })
      .withAutomaticReconnect()
      .build()
    conn.on('ReceiveYzhMessage', (msg: YzhPushMessage) => dispatch(msg))
    conn.onclose(() => {
      if (connection === conn) {
        connection = null
        starting = false
      }
    })
    connection = conn
    conn.start().catch((err: unknown) => {
      console.warn('[yzhPush] 连接失败（下次进入页面重试）:', err)
      if (connection === conn) {
        connection = null
        starting = false
      }
    })
  },

  /** 断开（退出登录 / 布局卸载时调用） */
  disconnect(): void {
    const conn = connection
    connection = null
    starting = false
    void conn?.stop()
  },
}
