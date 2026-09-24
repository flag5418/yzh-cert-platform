import { ref } from 'vue'
import { getMenuTree, type SysMenu } from '../api/system/menu'

/**
 * 当前用户可见菜单树 —— **admin + auditor 的唯一实现**（勿在任一 App 内重复定义）
 *
 * 对应后端：`GET /api/System/MenuManagement/tree`（后端按角色权限过滤后返回**扁平**列表，
 * 本层负责组树 + 归一化为 camelCase）。
 *
 * ⚠️ 过滤链路只认 `Sys_RoleMenu`（后端 `MenuPermissionService.GetVisibleMenusAsync`），
 *    `Sys_Menu.Tag` **不参与权限过滤**；Tag 只用于前端侧边栏分流（见 `filterMenuTreeByTag`）。
 *
 * ⚠️ 状态是**模块级单例**：同一应用内所有调用方共享一份缓存（`loaded` 去重，避免重复请求）。
 *    因此**退出登录必须调 `clearMenus()`** —— 否则换账号后会看到上一个账号的菜单。
 */

const menus = ref<SysMenu[]>([])
const loading = ref(false)
const loaded = ref(false)

export function useMenuTree() {
  /**
   * 加载菜单树
   * @param force 为 true 时忽略缓存强制重新拉取（菜单数据变更后使用）
   */
  async function loadMenus(force = false) {
    if (loaded.value && !force) return
    loading.value = true
    try {
      const res = await getMenuTree()
      menus.value = res.data ?? []
      loaded.value = true
    } catch (e) {
      // 真实失败路径：保留 console.error 便于定位；正常流程不会触发
      console.error('加载菜单失败:', e)
      menus.value = []
    } finally {
      loading.value = false
    }
  }

  /** 清空菜单（退出登录 / 切换账号时必须调用） */
  function clearMenus() {
    menus.value = []
    loaded.value = false
  }

  return { menus, loading, loaded, loadMenus, clearMenus }
}
