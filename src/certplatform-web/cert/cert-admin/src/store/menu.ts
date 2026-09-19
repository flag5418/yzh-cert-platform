import { defineStore } from 'pinia'
import { ref } from 'vue'
import { getMenuTree, type SysMenu } from '@/api/system/menu'

export const useMenuStore = defineStore('menu', () => {
  const menus = ref<SysMenu[]>([])
  const loading = ref(false)
  const loaded = ref(false)

  /**
   * 加载菜单树
   */
  async function loadMenus() {
    if (loaded.value) return
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

  /**
   * 清除菜单（退出登录时调用）
   */
  function clearMenus() {
    menus.value = []
    loaded.value = false
  }

  /**
   * 强制刷新菜单树（菜单数据变更后调用，重新从服务端拉取）
   */
  async function refreshMenus() {
    loaded.value = false
    await loadMenus()
  }

  return { menus, loading, loaded, loadMenus, clearMenus, refreshMenus }
})
