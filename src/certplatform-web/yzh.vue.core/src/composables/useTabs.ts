import { computed, ref } from 'vue'

/**
 * 多标签页 —— 机制层（模块级单例）
 *
 * 【解决什么】原布局没有页签：离开一个页面后想回去，**只能从菜单重新找一遍**。
 * 本模块记录「已打开的页面」，供 `YzhTabsBar` 渲染与切换。
 *
 * 【两条铁律】
 *   ① **不重复**：同一 `path` 只留一个标签（`open` 幂等）
 *   ② **可批量关闭**：关闭其他 / 关闭右侧 / 关闭全部
 *
 * 【⛔ 与 keep-alive 的关系】标签**只负责导航**，⛔ 不控制缓存。
 * 实测页面组件均无 `defineOptions({ name })` ⇒ `keep-alive :include` 无法按标签过滤，
 * 故保持「缓存所有访问过的页面」的现状
 * （用户 2026-10-16 明确：「回到规则页面，页面没有刷新 —— 当前这个不是问题」）。
 */

export interface YzhTabItem {
  /** 路由 path —— 唯一键，保证「不重复」 */
  path: string
  /** 标签标题（取自菜单名，见 `YzhTabsBar`） */
  title: string
  /** 是否可关闭（固定页不可关） */
  closable: boolean
}

/** 固定标签：首页 —— ⛔ 不可关闭（否则「关闭全部」后没有落脚点） */
export const YZH_TAB_HOME_PATH = '/'

const tabs = ref<YzhTabItem[]>([])
const activePath = ref('')

export function useTabs() {
  /** 打开（或激活）一个标签 —— ★ 同 path 幂等，⛔ 不产生重复标签 */
  function open(path: string, title: string): void {
    if (!path) return
    activePath.value = path
    if (tabs.value.some((t) => t.path === path)) return
    tabs.value = [
      ...tabs.value,
      { path, title: title || path, closable: path !== YZH_TAB_HOME_PATH },
    ]
  }

  /**
   * 关闭一个标签
   * @returns 关的是**当前标签**时返回需要跳转到的 path；否则 `null`（不需要跳转）
   */
  function close(path: string): string | null {
    const idx = tabs.value.findIndex((t) => t.path === path)
    if (idx < 0) return null
    if (!tabs.value[idx].closable) return null
    tabs.value = tabs.value.filter((t) => t.path !== path)
    if (activePath.value !== path) return null
    // 关的是当前标签 ⇒ 落到**右邻**，没有右邻取**左邻**
    const next = tabs.value[idx] ?? tabs.value[idx - 1] ?? tabs.value[0]
    return next ? next.path : YZH_TAB_HOME_PATH
  }

  /** 关闭其他（保留指定标签 + 所有不可关闭标签） */
  function closeOthers(path: string): void {
    tabs.value = tabs.value.filter((t) => t.path === path || !t.closable)
  }

  /** 关闭右侧（保留指定标签及其左侧 + 所有不可关闭标签） */
  function closeRight(path: string): void {
    const idx = tabs.value.findIndex((t) => t.path === path)
    if (idx < 0) return
    tabs.value = tabs.value.filter((t, i) => i <= idx || !t.closable)
  }

  /**
   * 关闭全部（保留不可关闭标签）
   * @returns 需要跳转到的 path（落到第一个剩余标签）
   */
  function closeAll(): string {
    tabs.value = tabs.value.filter((t) => !t.closable)
    const fallback = tabs.value[0]?.path ?? YZH_TAB_HOME_PATH
    activePath.value = fallback
    return fallback
  }

  /** 回填标题（菜单名是异步加载的，首次 `open` 时可能还没拿到） */
  function retitle(path: string, title: string): void {
    if (!path || !title) return
    tabs.value = tabs.value.map((t) =>
      t.path === path && t.title !== title ? { ...t, title } : t,
    )
  }

  const activeTab = computed(
    () => tabs.value.find((t) => t.path === activePath.value) ?? null,
  )

  return { tabs, activePath, activeTab, open, close, closeOthers, closeRight, closeAll, retitle }
}
