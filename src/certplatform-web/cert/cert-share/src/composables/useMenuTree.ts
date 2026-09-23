import { ref } from 'vue'
import { getMenuTree, type SysMenu } from '../api/system/menu'

/**
 * 当前用户可见菜单树 —— **admin + auditor 唯一实现**（勿在 App 内重复定义）
 *
 * ⚠️ 模块级单例状态：同一应用内所有调用方共享一份缓存（靠 `loaded` 去重），
 *    目的是避免侧边栏与路由守卫各拉一次菜单。**退出登录必须调 `clearMenus()`**，
 *    否则换账号登录后会看到上一个账号的菜单。
 *
 * ⚠️ 过滤链路只认 `Sys_RoleMenu`（后端 `MenuPermissionService.GetVisibleMenusAsync()`），
 *    `Sys_Menu.Tag` **不参与**权限过滤 —— 它只是给前端做分类分流用的标签。
 *    前端分流请用 `filterMenuTreeByTag()`（见 `@share/utils`）。
 *
 * ⚠️ 超管会绕过授权拿到全量菜单，故两端都必须按 tag 过滤，否则侧边栏会出现
 *    另一端的菜单项 → 点击 404。
 */
const menus = ref<SysMenu[]>([])
const loading = ref(false)
const loaded = ref(false)

export function useMenuTree() {
  /**
   * 加载菜单树
   * @param force 为 true 时忽略缓存强制重拉（菜单数据变更后使用）
   */
  async function loadMenus(force = false) {
    if (loaded.value && !force) return
    loading.value = true
    try {
      const res = await getMenuTree()
      menus.value = res.data ?? []
      loaded.value = true
    } catch (e) {
      console.error('加载菜单失败:', e)
      menus.value = []
    } finally {
      loading.value = false
    }
  }

  /** 清空菜单缓存（退出登录 / 切换账号时调用） */
  function clearMenus() {
    menus.value = []
    loaded.value = false
  }

  return { menus, loading, loaded, loadMenus, clearMenus }
}
