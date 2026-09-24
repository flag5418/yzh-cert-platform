<template>
  <el-container class="auditor-layout">
    <el-aside width="220px" class="auditor-layout__aside">
      <div class="auditor-layout__logo"><span>映智汇认证专家平台</span></div>

      <!--
        侧边栏菜单：**动态渲染**，数据来自 `/api/System/MenuManagement/tree`
        （后端按角色权限过滤，只认 Sys_RoleMenu）。
        ⚠️ 结构固定 2 层（el-sub-menu + el-menu-item），更深层级不渲染。
      -->
      <el-menu
        :default-active="activeMenu"
        router
        class="auditor-layout__menu"
        background-color="#304156"
        text-color="#bfcbd9"
        active-text-color="#409eff"
      >
        <template v-for="menu in visibleMenus" :key="menu.id">
          <!-- 有子菜单：渲染为 el-sub-menu -->
          <el-sub-menu
            v-if="menu.children && menu.children.length > 0"
            :index="menu.url || String(menu.id)"
          >
            <template #title>
              <el-icon v-if="menu.icon"><component :is="formatMenuIcon(menu.icon)" /></el-icon>
              <span>{{ menu.menuName }}</span>
            </template>
            <el-menu-item
              v-for="child in menu.children"
              :key="child.id"
              :index="child.url || String(child.id)"
            >
              <el-icon v-if="child.icon"><component :is="formatMenuIcon(child.icon)" /></el-icon>
              <span>{{ child.menuName }}</span>
            </el-menu-item>
          </el-sub-menu>

          <!-- 无子菜单：渲染为 el-menu-item -->
          <el-menu-item v-else :index="menu.url || String(menu.id)">
            <el-icon v-if="menu.icon"><component :is="formatMenuIcon(menu.icon)" /></el-icon>
            <span>{{ menu.menuName }}</span>
          </el-menu-item>
        </template>
      </el-menu>
    </el-aside>

    <el-container>
      <el-header class="auditor-layout__header">
        <div class="auditor-layout__header-left">
          <span class="auditor-layout__page-title">{{ pageTitle }}</span>
        </div>
        <div class="auditor-layout__header-right">
          <el-dropdown>
            <span class="auditor-layout__user">
              {{ displayName }}
              <el-icon><arrow-down /></el-icon>
            </span>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item @click="handleLogout">退出登录</el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </el-header>

      <el-main class="auditor-layout__main">
        <router-view v-slot="{ Component }">
          <keep-alive>
            <component :is="Component" />
          </keep-alive>
        </router-view>
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowDown } from '@element-plus/icons-vue'
import { useAuthStore } from '@/store/auth'
import { useMenuTree } from '@share/composables/useMenuTree'
import { filterMenuTreeByTag, formatMenuIcon } from '@share/utils'
import type { SysMenu } from '@share/api/system/menu'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const { menus, loadMenus, clearMenus } = useMenuTree()

/**
 * 侧边栏可见菜单
 *
 * ⚠️ 必须按 `Tag` 分流：后端权限过滤**只认 `Sys_RoleMenu`**，`Tag` 不参与过滤；
 *    而超管会**绕过授权**拿到全量菜单。不过滤的话专家端侧边栏会出现管理端菜单项，
 *    点击后专家端路由不存在 → 白屏。这层分流必须在前端做。
 */
const visibleMenus = computed(() => filterMenuTreeByTag(menus.value, 'auditor'))

const activeMenu = computed(() => route.path)

/**
 * 页面标题：从**当前可见菜单**中反查，避免标题与菜单名两处维护导致不一致
 * （`v_workflow` 视图与后端 status 字段曾出现同类「两处维护」问题）。
 */
const pageTitle = computed(() => {
  const find = (list: SysMenu[]): string | null => {
    for (const m of list) {
      if (m.url === route.path) return m.menuName
      if (m.children?.length) {
        const found = find(m.children)
        if (found) return found
      }
    }
    return null
  }
  return find(visibleMenus.value) || '专家端'
})

/**
 * 顶栏显示名
 * ⚠️ 字段名必须 PascalCase（§16.9 铁律七）——`UserTrueName` / `UserName` 直接来自
 *    `/api/User/login` 响应 data；写成 `userTrueName` 会读到 undefined 且不报错。
 */
const displayName = computed(
  () => authStore.userInfo?.UserTrueName || authStore.userInfo?.UserName || '专家'
)

function handleLogout() {
  authStore.clearToken()
  // ⚠️ 必须清菜单缓存：useMenuTree 是模块级单例，不清会残留上一个账号的菜单
  clearMenus()
  router.push('/login')
}

onMounted(() => {
  loadMenus()
})
</script>

<style scoped>
.auditor-layout { height: 100vh; }
.auditor-layout__aside { background: #304156; overflow: hidden; }
.auditor-layout__logo { height: 60px; display: flex; align-items: center; justify-content: center; color: #fff; font-size: 15px; font-weight: bold; border-bottom: 1px solid #3a4a5b; }
.auditor-layout__menu { border-right: none; }
.auditor-layout__header { display: flex; align-items: center; justify-content: space-between; background: #fff; border-bottom: 1px solid #e4e7ed; padding: 0 20px; }
.auditor-layout__page-title { font-size: 16px; font-weight: 600; }
.auditor-layout__header-right { display: flex; align-items: center; }
.auditor-layout__user { display: flex; align-items: center; gap: 4px; cursor: pointer; color: #606266; }
.auditor-layout__main { background: #f5f7fa; padding: 20px; }
</style>
