import { computed, ref } from 'vue'

/**
 * 全局等待提示 —— 机制层（模块级单例）
 *
 * 【解决什么】按钮的 `loading` 只挂在按钮上 ⇒ 用户切到别的页面就看不见「还在跑」，
 * 只能靠猜。本模块把「等待」上抛到一个全局的地方，由顶层遮罩统一渲染。
 *
 * 【范围口径】`scope` = **发起时**的 `route.path` ⇒ 遮罩**只显示当前页面**的等待：
 *   · 在 A 页发起分析、切到 B 页 ⇒ B 页没有等待 ⇒ ⛔ 不显示（不打扰）
 *   · 切回 A 页 ⇒ 等待仍在 ⇒ 又显示（页面状态由 `keep-alive` 保留）
 *
 * 【为什么是模块级单例】与 `useAuthState` 同口径：同一应用内所有调用方共享一份。
 * ⛔ 不用 pinia —— `yzh.vue.core` 是「与项目完全无关的通用底层能力」，不依赖任何 store 实现。
 *
 * 【⛔ 不要接的操作】列表加载 / 详情读取 / 保存配置等秒级操作 ——
 *   全接会让遮罩一直闪，反而变成噪音。判据：**预计 > 3 秒 且 用户可能想切走**。
 */

export interface YzhPendingItem {
  /** 唯一键（`begin` 返回，`end` 用它撤销） */
  key: string
  /** 展示文案（用户视角，如「正在分析：XX.docx」） */
  label: string
  /** 归属页面（`route.path`）—— 遮罩按它过滤，只显示当前页面 */
  scope: string
  /** 0–100；`undefined` = 不确定进度（只转圈） */
  progress?: number
  /** 发起时间戳（ms） */
  startedAt: number
}

const pendings = ref<YzhPendingItem[]>([])
let seq = 0

export function useGlobalPending() {
  /** 登记一项等待，返回 key（供 `end` 撤销） */
  function begin(label: string, options: { scope?: string; progress?: number } = {}): string {
    const key = `yzh_pending_${++seq}`
    pendings.value = [
      ...pendings.value,
      {
        key,
        label,
        scope: options.scope ?? '',
        progress: options.progress,
        startedAt: Date.now(),
      },
    ]
    return key
  }

  /** 撤销一项等待 —— 幂等：key 不存在时无副作用（超时兜底与 `finally` 可能都调一次） */
  function end(key: string): void {
    pendings.value = pendings.value.filter((p) => p.key !== key)
  }

  /** 局部更新（如队列进度变化） */
  function update(key: string, patch: Partial<Pick<YzhPendingItem, 'label' | 'progress'>>): void {
    pendings.value = pendings.value.map((p) => (p.key === key ? { ...p, ...patch } : p))
  }

  /** 清空指定页面的等待（页面卸载兜底，避免遮罩卡死） */
  function clearScope(scope: string): void {
    pendings.value = pendings.value.filter((p) => p.scope !== scope)
  }

  /** 清空全部（退出登录等场景） */
  function clearAll(): void {
    pendings.value = []
  }

  const count = computed(() => pendings.value.length)

  return { pendings, count, begin, end, update, clearScope, clearAll }
}

/**
 * 当前页面的等待（遮罩用）
 *
 * ⚠️ 传 **getter**（`() => route.path`）而非路径值 —— 路径会变，传值会失去响应式。
 */
export function usePendingByScope(getScope: () => string) {
  return computed(() => pendings.value.filter((p) => p.scope === getScope()))
}
