import { onMounted, onUnmounted } from 'vue'

export function usePolling(fn: () => Promise<void>, interval: number, options: { enabled?: boolean; immediate?: boolean } = {}) {
  let timer: number | null = null

  function start() {
    if (timer) return
    timer = window.setInterval(fn, interval)
  }

  function stop() {
    if (timer) {
      clearInterval(timer)
      timer = null
    }
  }

  if (options.immediate !== false) start()
  if (options.enabled === false) stop()

  onUnmounted(() => stop())

  return { start, stop }
}
