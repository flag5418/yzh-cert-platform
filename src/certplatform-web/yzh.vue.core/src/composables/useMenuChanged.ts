/**
 * 菜单变更事件 —— core 页面（如菜单管理页）与宿主布局（AdminLayout/AuditorLayout）解耦
 *
 * 背景：菜单管理页保存后需要宿主侧边栏重新拉取菜单树，但 core 页面不得反向依赖宿主 store
 * （守卫 R10）。约定：宿主布局挂载时订阅 `onMenuChanged`，core 页面改动后调 `notifyMenuChanged()`。
 *
 * 实现：浏览器 CustomEvent（SPA 无 SSR 顾虑），零依赖。
 */

export const MENU_CHANGED_EVENT = 'yzh:menu-changed'

/** 广播菜单已变更（菜单管理页保存/删除/启停后调用） */
export function notifyMenuChanged(): void {
  window.dispatchEvent(new Event(MENU_CHANGED_EVENT))
}

/**
 * 订阅菜单变更
 * @returns 取消订阅函数（onUnmounted 中调用）
 */
export function onMenuChanged(handler: () => void): () => void {
  window.addEventListener(MENU_CHANGED_EVENT, handler)
  return () => window.removeEventListener(MENU_CHANGED_EVENT, handler)
}
