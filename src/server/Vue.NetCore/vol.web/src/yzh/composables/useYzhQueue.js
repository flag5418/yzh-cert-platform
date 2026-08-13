/**
 * yzh useYzhQueue —— 队列中心 API 封装（通用）
 * 供队列监控页 / 业务页面（上传后跳转、详情查看、资源锁检查）复用
 * 接口对齐后端 YZH.Core/Queue + QueueController
 */
import { getCurrentInstance } from 'vue'

export function useYzhQueue() {
  const { proxy } = getCurrentInstance()

  /** 队列主表分页（Tabs + 时间过滤） */
  async function getQueueList(params = {}) {
    const body = {
      type: params.type || '',
      status: params.status || '',
      startTime: params.startTime || null,
      endTime: params.endTime || null,
      page: params.page || 1,
      rows: params.rows || 20
    }
    const res = await proxy.http.post('api/queue/list', body, true)
    return res?.Data ?? res?.data ?? null
  }

  /** 队列统计卡 */
  async function getQueueStats() {
    const res = await proxy.http.post('api/queue/status', {}, true)
    return res?.Data ?? res?.data ?? null
  }

  /** 队列详情（主表 + 子任务 + 资源锁） */
  async function getQueueDetail(queueCode) {
    const res = await proxy.http.post(`api/queue/detail?queueCode=${encodeURIComponent(queueCode)}`, {}, true)
    return res?.Data ?? res?.data ?? null
  }

  /** 取消队列 */
  async function cancelQueue(queueCode) {
    return await proxy.http.post(`api/queue/cancel?queueCode=${encodeURIComponent(queueCode)}`, {}, true)
  }

  /** 整队重跑 */
  async function retryQueue(queueCode) {
    return await proxy.http.post(`api/queue/retry?queueCode=${encodeURIComponent(queueCode)}`, {}, true)
  }

  /** 单个子任务重试 */
  async function retryTask(taskId) {
    return await proxy.http.post(`api/queue/task/retry?taskId=${taskId}`, {}, true)
  }

  /** 通用资源锁查询（页面操作前检查：资源是否被某个队列锁定） */
  async function checkResourceLock(table, code) {
    if (!table || !code) return null
    const res = await proxy.http.post('api/queue/resource/locked', { table, code }, true)
    return res?.Data ?? res?.data ?? null
  }

  /** 重试失败的文档转换（failed/孤儿 pending 的 doc/xls 重新入队） */
  async function retryFailedConversions() {
    const res = await proxy.http.post('api/queue/file-convert/retry-failed', {}, true)
    return {
      ok: res?.Status === true || res?.status === true,
      message: res?.Message ?? res?.message ?? '',
      data: res?.Data ?? res?.data ?? null
    }
  }

  /** 查询某目录下的运行中队列（供 DirectoryManager 横幅展示） */
  async function getActiveQueue(directoryCode) {
    if (!directoryCode) return null
    const res = await proxy.http.get(`api/standard-directory/active-queue?directoryCode=${encodeURIComponent(directoryCode)}`)
    return res?.Data ?? res?.data ?? null
  }

  /** 批量查询文件锁定状态：返回 { [fileCode]: queueCode } 对象 */
  async function getFileLockStatus(fileCodes) {
    if (!fileCodes || fileCodes.length === 0) return {}
    const res = await proxy.http.post('api/standard-directory/file-lock-status', { fileCodes })
    return res?.Data ?? res?.data ?? {}
  }

  return {
    getQueueList,
    getQueueStats,
    getQueueDetail,
    cancelQueue,
    retryQueue,
    retryTask,
    checkResourceLock,
    retryFailedConversions,
    getActiveQueue,
    getFileLockStatus
  }
}
