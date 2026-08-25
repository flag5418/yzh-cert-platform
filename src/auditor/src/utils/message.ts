import { createDiscreteApi, useMessage, useNotification, zhCN, dateZhCN } from 'naive-ui'
import type { ConfigProviderProps, MessageApi, NotificationApi } from 'naive-ui'

const configProviderProps: ConfigProviderProps = {
  locale: zhCN,
  dateLocale: dateZhCN
}

/**
 * 非组件环境可用的离散 API（如 axios 拦截器、工具函数）
 * 通过 createDiscreteApi 创建，不依赖组件上下文
 */
export const { message, notification, dialog, loadingBar } = createDiscreteApi(
  ['message', 'notification', 'dialog', 'loadingBar'],
  { configProviderProps }
)

/**
 * 组件内便捷封装（需在 NMessageProvider / NNotificationProvider 环境下调用）
 */
export function useAppMessage(): MessageApi {
  return useMessage()
}

export function useAppNotification(): NotificationApi {
  return useNotification()
}
