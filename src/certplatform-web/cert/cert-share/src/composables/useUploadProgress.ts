import * as signalR from '@microsoft/signalr'
import { ref, onUnmounted } from 'vue'
import { tokenStore } from '@yzh-core/api/client'

/**
 * 上传进度 SignalR 订阅（P2b③ 框架能力）
 *
 * <para>后端 Hub：<c>/api/yzh-upload</c>（UploadProgressHub）。</para>
 * <para>分组名：<c>upload:{taskId}</c>，前端 subscribe 后服务端通过
 * <c>BroadcastProgressAsync(taskId, data)</c> 推送进度。</para>
 *
 * <para>用法：</para>
 * <code>
 * const { progress, error, connect, disconnect } = useUploadProgress('task-123')
 * connect()  // 上传开始时
 * disconnect()  // 上传完成/失败/取消时
 * </code>
 *
 * <para>进度数据格式随业务自定义（YzhPushMessage.Data），本 composable 只提供透传。</para>
 */
export function useUploadProgress(taskId: string) {
  const progress = ref<unknown>(null)
  const error = ref<string | null>(null)
  let conn: signalR.HubConnection | null = null

  function connect() {
    if (!taskId || !tokenStore.get()) return
    try {
      const url = new URL('/api/yzh-upload', window.location.origin).toString()
      const c = new signalR.HubConnectionBuilder()
        .withUrl(url, { accessTokenFactory: () => tokenStore.get() || '' })
        .build()
      c.on('ReceiveUploadProgress', (data: unknown) => {
        progress.value = data
        error.value = null
      })
      c.start().then(() => c.invoke('SubscribeAsync', taskId)).catch((e: unknown) => {
        error.value = String(e)
        console.warn('[useUploadProgress] 连接失败:', e)
      })
      conn = c
    } catch (e) {
      error.value = String(e)
    }
  }

  function disconnect() {
    if (conn) {
      void conn.stop()
      conn = null
    }
  }

  onUnmounted(() => {
    disconnect()
  })

  return { progress, error, connect, disconnect }
}
