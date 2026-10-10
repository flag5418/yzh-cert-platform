<template>
  <el-container class="auditor-layout">
    <el-aside width="230px" class="auditor-layout__aside">
      <!-- Logo 区域：与 9990 管理员端 YzhAppLayout 的 #brand 同款（渐变方块 + 品牌名） -->
      <div class="auditor-layout__brand">
        <span class="brand-logo__icon">YZH</span>
        <span class="brand-text">映智汇认证专家平台</span>
      </div>

      <!--
        侧边栏菜单：**动态渲染**，数据来自 `/api/System/MenuManagement/tree`
        （后端按角色权限过滤，只认 Sys_RoleMenu）。
        ★ 渲染复用 core 的递归组件 YzhMenuNode（与 9990 同一实现）：
          - 支持 N 级层级（原硬编码 2 层会静默丢弃第 3 级及以下）
          - 无 Url 的目录/叶子不再退化成 index=Id 去跳不存在的路由
        样式由 <style> 内 :deep() 覆盖（令牌驱动，与 YzhAppLayout 逐条对齐），
        ⛔ 不传 background-color / text-color / active-text-color prop——
        传 prop 会让 Element Plus 写入内联 --el-menu-* 变量，绕过 CSS 层。
      -->
      <el-menu
        :default-active="activeMenu"
        router
        class="auditor-layout__menu"
      >
        <YzhMenuNode
          v-for="menu in visibleMenus"
          :key="menu.code || menu.id"
          :menu="menu"
        />
      </el-menu>
    </el-aside>

    <el-container>
      <el-header class="auditor-layout__header">
        <div class="auditor-layout__header-left">
          <span class="auditor-layout__page-title">{{ pageTitle }}</span>
        </div>
        <div class="auditor-layout__header-right">
          <MessageBell />
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
        <!--
          ★ 2026-10-07 修正 `keep-alive` 策略（用户报「返回列表不刷新」的根因层）。

          修之前是裸 `<keep-alive>`：**没有 include、没有 max、也没有 key** ⇒
            ① 所有页面**永久缓存**，返回时组件被复用、`onMounted` 不再触发；
            ② 缓存**无上限**，长期使用会一直堆积；
            ③ `/tasks/:code` 是同一个组件类型 ⇒ 点开**第二个任务详情**时复用了
               第一个任务的实例，界面上显示的是**上一条任务的数据**。

          `:key="route.fullPath"` ⇒ 不同 URL 各占一个缓存实例（③ 直接消失）；
          `:max="12"` ⇒ 给缓存封顶，超出按 LRU 淘汰（② 消失）；
          页面自身的「回到页面要刷新」由各页 `onActivated` 兜底（① 消失）。
        -->
        <router-view v-slot="{ Component }">
          <keep-alive :max="12">
            <component :is="Component" :key="route.fullPath" />
          </keep-alive>
        </router-view>
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowDown } from '@element-plus/icons-vue'
import { useAuthStore } from '@/store/auth'
import { useMenuTree } from '@yzh-core/composables/useMenuTree'
import { onMenuChanged } from '@yzh-core/composables/useMenuChanged'
import { filterMenuTreeByTag } from '@yzh-core/utils/menu'
import YzhMenuNode from '@yzh-core/layouts/components/YzhMenuNode.vue'
import type { SysMenu } from '@yzh-core/api/system/menu'
import MessageBell from '@share/components/MessageBell/MessageBell.vue'
import { yzhPush } from '@yzh-core/api/push'
import { handleForceLogout } from '@yzh-core/api/push'

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

// 订阅菜单变更：菜单管理页 notifyMenuChanged 后侧栏强刷（force 绕过 loaded 缓存）
let stopMenuWatch: (() => void) | null = null
let offLogout: (() => void) | null = null
onMounted(() => {
  loadMenus()
  yzhPush.connect()
  offLogout = yzhPush.on('logout', (msg) => {
    handleForceLogout(msg.message)
  })
  stopMenuWatch = onMenuChanged(() => {
    loadMenus(true)
  })
})
onUnmounted(() => {
  yzhPush.disconnect()
  stopMenuWatch?.()
  offLogout?.()
  stopMenuWatch = null
  offLogout = null
})
</script>

<style scoped>
/*
 * 菜单/侧栏样式：逐条对齐 9990 管理员端共享布局
 * `yzh.vue.core/src/layouts/YzhAppLayout.vue`（25 号 §四 S01 令牌 + 兜底）。
 */
.auditor-layout { height: 100vh; }

/* ========== 侧边栏 ========== */
.auditor-layout__aside {
  background: var(--yzh-color-sidebar-bg, #1a2332);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

/* Logo 区域（对齐 YzhAppLayout .yzh-layout__brand） */
.auditor-layout__brand {
  height: var(--yzh-sidebar-brand-height, 64px);
  display: flex;
  align-items: center;
  padding: 0 var(--yzh-space-4, 16px);
  border-bottom: 1px solid var(--yzh-color-sidebar-divider, rgba(255, 255, 255, 0.06));
}

.brand-logo__icon {
  width: 36px;
  height: 36px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: 700;
  color: var(--yzh-color-text-inverse, #fff);
  background: linear-gradient(135deg, var(--yzh-color-primary, #1e3a8a), var(--yzh-color-primary-light, #2563eb));
  border-radius: 8px;
  flex-shrink: 0;
  letter-spacing: 0.5px;
}

.brand-text {
  margin-left: var(--yzh-space-3, 12px);
  color: var(--yzh-color-sidebar-text-muted, rgba(255, 255, 255, 0.88));
  font-size: var(--yzh-font-size-lg, 15px);
  font-weight: 600;
  white-space: nowrap;
  letter-spacing: 0.3px;
}

/* ========== 菜单区域（对齐 YzhAppLayout .yzh-layout__menu） ========== */
.auditor-layout__menu {
  border-right: none;
  background: var(--yzh-color-sidebar-bg, #1a2332);
  overflow-y: auto;
  overflow-x: hidden;
  flex: 1;
  padding-top: var(--yzh-space-2, 8px);
  /* 隐藏滚动条（保留滚动功能） */
  scrollbar-width: none;        /* Firefox */
  -ms-overflow-style: none;     /* IE 10+ */
}

.auditor-layout__menu::-webkit-scrollbar {
  width: 0;
  display: none;
}

/* 覆盖 el-menu 自身背景 */
.auditor-layout__menu :deep(.el-menu) {
  background-color: transparent;
  border-right: none;
}

/* 覆盖 Element Plus 菜单项样式 */
.auditor-layout__menu :deep(.el-menu-item),
.auditor-layout__menu :deep(.el-sub-menu__title) {
  color: var(--yzh-color-sidebar-text, #fff);
  background-color: transparent;
  height: 42px;
  line-height: 42px;
  margin: 0 var(--yzh-space-2, 8px);
  border-radius: 6px;
}

.auditor-layout__menu :deep(.el-menu-item:hover),
.auditor-layout__menu :deep(.el-sub-menu__title:hover) {
  color: var(--yzh-color-sidebar-text, #fff);
  background: var(--yzh-color-sidebar-hover, rgba(255, 255, 255, 0.1));
}

.auditor-layout__menu :deep(.el-menu-item.is-active) {
  color: var(--yzh-color-sidebar-text, #fff);
  background: var(--yzh-color-primary, #1e3a8a);
  font-weight: 500;
}

/* 子菜单项 */
.auditor-layout__menu :deep(.el-sub-menu .el-menu-item) {
  background: transparent;
  padding-left: 50px !important;
}

.auditor-layout__menu :deep(.el-sub-menu .el-menu-item:hover) {
  background: var(--yzh-color-sidebar-hover, rgba(255, 255, 255, 0.1));
}

/* ========== 顶部导航栏 ========== */
.auditor-layout__header { display: flex; align-items: center; justify-content: space-between; background: var(--yzh-color-bg-container, #fff); border-bottom: 1px solid var(--yzh-color-border, #e4e7ed); padding: 0 var(--yzh-space-5, 20px); }
.auditor-layout__page-title { font-size: var(--yzh-font-size-lg, 16px); font-weight: 600; }
.auditor-layout__header-right { display: flex; align-items: center; }
.auditor-layout__user { display: flex; align-items: center; gap: var(--yzh-space-1, 4px); cursor: pointer; color: var(--yzh-color-text-regular, #606266); }
.auditor-layout__main { background: var(--yzh-color-bg-page, #f5f7fa); padding: var(--yzh-space-5, 20px); }
</style>
