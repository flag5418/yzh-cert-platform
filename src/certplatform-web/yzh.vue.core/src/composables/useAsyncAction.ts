import { computed, getCurrentScope, inject, onScopeDispose, ref } from 'vue'
import { routeLocationKey } from 'vue-router'
import { useGlobalPending } from './useGlobalPending'

/**
 * 异步动作包裹 —— 「按钮 loading + 全局等待提示」一次接好
 *
 * 【为什么必须包裹，而不是各页各写各的】
 *   ① **并发缺陷**：单 `boolean` 下，两个请求先后 `finally` 会把先结束的那次置 `false`，
 *      另一个还在跑时按钮却恢复可点 ⇒ 同一操作被**重复发起**（本项目已实测踩过）。
 *      本 composable 用**并发计数**：`busy = runs > 0`。
 *   ② **`end` 不能漏调**：用 `try/finally` 保证；另加**超时兜底**，防请求卡死时遮罩不消失。
 *
 * 【用法】
 *   const { busy, run } = useAsyncAction()
 *   <el-button type="primary" :loading="busy" @click="run(() => doAnalyze(), '正在分析：XX.docx')">
 *
 * ⚠️ **必须解构**（`{ busy, run }`）：`busy` 是 `ComputedRef`；若写成
 *   `const a = useAsyncAction()` 再在模板里用 `a.busy`，模板**不会自动解包**
 *   （自动解包只对顶层 ref 生效）⇒ 传进 `:loading` 的会是 ref 对象，恒为真。
 *
 * 【⛔ 不要接的操作】列表加载 / 详情读取 / 保存配置等秒级操作 ——
 *   全接会让遮罩一直闪，反而变成噪音。判据：**预计 > 3 秒 且 用户可能想切走**。
 *   队列任务（入队即返回、进度由队列页负责）同样不接。
 */

export interface YzhAsyncActionOptions {
  /** 是否接入全局等待提示（默认 true；`false` = 只做并发计数） */
  global?: boolean
  /** 超时兜底（ms）—— 即使请求卡死也保证遮罩会消失；`0` = 关闭（默认 120000） */
  timeout?: number
}

/**
 * 取「响应式路由对象」（等待遮罩的**作用域键**来源）。
 *
 * ⚠️ 必须容错，不能用裸 `useRoute()`：本 composable 也会在**没装 router 的上下文**里被调用 ——
 *    典型是单测 `mount(页面)` 只给 stubs、不给 router（见 `prompt-template/index.test.ts`）。
 *    裸 `useRoute()` 此时会刷 `injection "Symbol(route location)" not found` 警告。
 *    改用 `inject(routeLocationKey, null)`：带默认值 ⇒ 拿不到时**静默**返回 `null`。
 *
 * ⚠️ `inject` 只能在 **setup 同步执行期**调用（在事件回调里调会刷
 *    `inject() can only be used inside setup()`）⇒ 这里只在 setup 取一次**对象**，
 *    `run()` 里再读它的 `.path`。该对象是 `shallowReactive(currentRoute)`
 *    ⇒ 值恒为**当前**路由，切页后作用域跟着变（⛔ 不能在 setup 时把 path 存成字符串）。
 *
 * 拿不到 router 时 scope 退化为 `''`：遮罩按 `route.path` 精确匹配，`''` 永不命中
 * ⇒ 等价于「不显示遮罩」，⛔ 但**并发计数照常工作**（并发缺陷的修复不受影响）。
 */
function injectRoute() {
  return inject(routeLocationKey, null)
}

export function useAsyncAction(options: YzhAsyncActionOptions = {}) {
  const { global: useGlobal = true, timeout = 120_000 } = options
  const { begin, end } = useGlobalPending()
  const route = injectRoute()

  const runs = ref(0)
  /** ★ 并发计数派生 —— ⛔ 不用单 boolean */
  const busy = computed(() => runs.value > 0)
  /** 本动作占用的 key —— 组件卸载时兜底清理，防遮罩卡住 */
  const ownedKeys = new Set<string>()

  /**
   * 执行一个异步动作
   * @param fn 真正的业务调用
   * @param label 等待提示文案（用户视角，如「正在分析文档：XX.docx」）
   */
  async function run<T>(fn: () => Promise<T>, label: string): Promise<T> {
    const key = useGlobal ? begin(label, { scope: route?.path ?? '' }) : ''
    if (key) ownedKeys.add(key)
    runs.value += 1

    let watchdog: ReturnType<typeof setTimeout> | undefined
    if (key && timeout > 0) watchdog = setTimeout(() => end(key), timeout)

    try {
      return await fn()
    } finally {
      if (watchdog) clearTimeout(watchdog)
      runs.value -= 1
      if (key) {
        ownedKeys.delete(key)
        end(key)
      }
    }
  }

  if (getCurrentScope()) {
    onScopeDispose(() => {
      ownedKeys.forEach((k) => end(k))
      ownedKeys.clear()
    })
  }

  return { busy, runs, run }
}
