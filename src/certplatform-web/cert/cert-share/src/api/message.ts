import { yzhApi } from '@yzh-core'

/**
 * 站内消息 API
 *
 * 接口路径：/api/Admin/System/Message/*
 * 契约：ApiResponse 信封，成功码 200
 */

export interface MessageItem {
  id: number
  code: string
  userCode: string
  userName: string
  title: string
  content: string
  messageType: string
  isRead: number
  extraData: string
  createTime: string
  readDate: string | null
}

export interface MessageListParams {
  page: number
  pageSize: number
  unreadOnly?: boolean
}

/** 查询未读消息数 */
export async function getUnreadCount() {
  return yzhApi.post<{ data: number }>('/api/Admin/System/Message/unread-count', {})
}

/** 查询消息列表 */
export async function getMessageList(params: MessageListParams) {
  return yzhApi.post<{ data: MessageItem[] }>('/api/Admin/System/Message/list', params)
}

/** 标记单条消息为已读 */
export async function markMessageRead(code: string) {
  return yzhApi.post<{ data: boolean }>(`/api/Admin/System/Message/read/${code}`, {})
}

/** 批量标记全部已读（可按 messageType 过滤） */
export async function markAllRead(params?: { messageType?: string }) {
  return yzhApi.post<{ data: number }>('/api/Admin/System/Message/read-all', params ?? {})
}
