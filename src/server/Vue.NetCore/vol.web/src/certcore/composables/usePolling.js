/**
 * certcore usePolling —— 统一轮询
 * 转换队列监控 / 转换进度面板 / 状态刷新复用
 */
import { onUnmounted } from 'vue'

export function usePolling(fn, interval = 5000, immediate = true) {
  let timer = null

  function stop() {
    if (timer) {
      clearInterval(timer)
      timer = null
    }
  }

  function start() {
    stop()
    if (immediate && typeof fn === 'function') fn()
    timer = setInterval(() => {
      if (typeof fn === 'function') fn()
    }, interval)
  }

  onUnmounted(stop)

  return { start, stop }
}
