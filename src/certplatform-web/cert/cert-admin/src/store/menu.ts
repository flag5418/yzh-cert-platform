import { defineStore } from 'pinia'
import { useMenuTree } from '@share/composables/useMenuTree'

/**
 * 菜单 store（**薄适配层**）
 *
 * 真实实现已上移到共享层 `@share/composables/useMenuTree`（admin + auditor 唯一实现）。
 * 本文件只保留 pinia store 外观，使既有调用方（`AdminLayout.vue`、`router/index.ts`、
 * `pages/system/menu/logic.ts`）无需改动。
 *
 * ⚠️ 退出登录时必须调用 `clearMenus()`，否则共享层的模块级缓存会残留上一个账号的菜单。
 */
export const useMenuStore = defineStore('menu', () => {
  const { menus, loading, loaded, loadMenus, clearMenus } = useMenuTree()

  /** 强制刷新菜单树（菜单数据变更后调用） */
  function refreshMenus() {
    return loadMenus(true)
  }

  return { menus, loading, loaded, loadMenus, clearMenus, refreshMenus }
})
