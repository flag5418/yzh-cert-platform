<template>
  <el-container class="admin-layout">
    <!-- ========== 侧边栏 ========== -->
    <el-aside :width="sidebarCollapsed ? '64px' : '230px'" class="admin-layout__aside">
      <!-- Logo 区域 -->
      <div class="admin-layout__brand">
        <div class="brand-logo">
          <span class="brand-logo__icon">YZH</span>
        </div>
        <transition name="fade">
          <span v-if="!sidebarCollapsed" class="brand-text">映智汇认证平台</span>
        </transition>
      </div>

      <!-- 菜单区域 -->
      <el-menu
        :default-active="activeMenu"
        :collapse="sidebarCollapsed"
        :collapse-transition="false"
        router
        class="admin-layout__menu"
      >
        <template v-for="menu in menuStore.menus" :key="menu.id">
          <!-- 有子菜单：渲染为 el-sub-menu -->
          <el-sub-menu v-if="menu.children && menu.children.length > 0" :index="String(menu.id)">
            <template #title>
              <el-icon v-if="menu.icon"><component :is="formatIcon(menu.icon)" /></el-icon>
              <span>{{ menu.name }}</span>
            </template>
            <el-menu-item
              v-for="child in menu.children"
              :key="child.id"
              :index="child.url || String(child.id)"
            >
              <el-icon v-if="child.icon"><component :is="formatIcon(child.icon)" /></el-icon>
              <span>{{ child.name }}</span>
            </el-menu-item>
          </el-sub-menu>

          <!-- 无子菜单：渲染为 el-menu-item -->
          <el-menu-item v-else :index="menu.url || String(menu.id)">
            <el-icon v-if="menu.icon"><component :is="formatIcon(menu.icon)" /></el-icon>
            <span>{{ menu.name }}</span>
          </el-menu-item>
        </template>
      </el-menu>
    </el-aside>

    <!-- ========== 主体区域 ========== -->
    <el-container class="admin-layout__main-container">
      <!-- 顶部导航栏 -->
      <el-header class="admin-layout__header">
        <div class="header-left">
          <!-- 折叠按钮 -->
          <el-button text class="header-btn" @click="sidebarCollapsed = !sidebarCollapsed">
            <el-icon size="18"><Fold v-if="!sidebarCollapsed" /><Expand v-else /></el-icon>
          </el-button>
          <!-- 面包屑 -->
          <el-breadcrumb separator="/" class="header-breadcrumb">
            <el-breadcrumb-item :to="{ path: '/' }">首页</el-breadcrumb-item>
            <el-breadcrumb-item>{{ currentPageTitle }}</el-breadcrumb-item>
          </el-breadcrumb>
        </div>

        <div class="header-right">
          <!-- 用户信息下拉 -->
          <el-dropdown trigger="click" @command="handleCommand">
            <div class="header-user">
              <el-avatar :size="32" class="header-user__avatar">
                {{ userInitial }}
              </el-avatar>
              <span class="header-user__name">{{ authStore.userInfo?.userName || '管理员' }}</span>
              <el-icon><ArrowDown /></el-icon>
            </div>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item command="password">修改密码</el-dropdown-item>
                <el-dropdown-item divided command="logout">退出登录</el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </el-header>

      <!-- 内容区域 -->
      <el-main class="admin-layout__content">
        <router-view v-slot="{ Component }">
          <keep-alive><component :is="Component" /></keep-alive>
        </router-view>
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowDown, Expand, Fold } from '@element-plus/icons-vue'
import { useAuthStore } from '@/store/auth'
import { useMenuStore } from '@/store/menu'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const menuStore = useMenuStore()
const sidebarCollapsed = ref(false)

// 当前激活的菜单
const activeMenu = computed(() => route.path)

// 当前页面标题（从菜单中查找）
const currentPageTitle = computed(() => {
  const path = route.path
  // 递归查找菜单名称
  const findMenuName = (menus: any[]): string | null => {
    for (const m of menus) {
      if (m.url === path) return m.name
      if (m.children?.length) {
        const found = findMenuName(m.children)
        if (found) return found
      }
    }
    return null
  }
  return findMenuName(menuStore.menus) || '首页'
})

// 用户头像首字母
const userInitial = computed(() => {
  const name = authStore.userInfo?.userName || 'A'
  return name.charAt(0).toUpperCase()
})

// 格式化图标名称（el-icon-xxx → 组件名）
function formatIcon(iconName: string): string {
  if (!iconName) return ''
  // 移除常见前缀
  let name = iconName
    .replace(/^el-icon-/, '')
    .replace(/^ivu-icon ivu-icon-/, '')
  // 转换为 PascalCase
  return name
    .split('-')
    .map(s => s.charAt(0).toUpperCase() + s.slice(1))
    .join('')
}

// 下拉菜单命令处理
async function handleCommand(command: string) {
  if (command === 'logout') {
    await ElMessageBox.confirm('确定要退出登录吗？', '提示', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
    authStore.clearToken()
    menuStore.clearMenus()
    ElMessage.success('已退出登录')
    router.push('/login')
  } else if (command === 'password') {
    ElMessage.info('功能开发中')
  }
}

// 加载菜单
onMounted(() => {
  menuStore.loadMenus()
})
</script>

<style scoped>
/* ========== 整体布局 ========== */
.admin-layout {
  height: 100vh;
}

/* ========== 侧边栏 ========== */
.admin-layout__aside {
  background: #1e293b;
  border-right: 1px solid #334155;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  transition: width 0.3s ease;
}

/* Logo 区域 */
.admin-layout__brand {
  height: 60px;
  display: flex;
  align-items: center;
  padding: 0 12px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  background: #1a2332;
  overflow: hidden;
}

.brand-logo__icon {
  width: 36px;
  height: 36px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  font-weight: 700;
  color: #fff;
  background: linear-gradient(135deg, #3b82f6, #1d4ed8);
  border-radius: 8px;
  flex-shrink: 0;
  letter-spacing: 0.5px;
}

.brand-text {
  margin-left: 12px;
  color: #e2e8f0;
  font-size: 15px;
  font-weight: 600;
  white-space: nowrap;
  letter-spacing: 0.5px;
}

/* 菜单区域 */
.admin-layout__menu {
  border-right: none;
  background: #1e293b;
  overflow-y: auto;
  overflow-x: hidden;
  flex: 1;
}

/* 覆盖 Element Plus 菜单样式 */
.admin-layout__menu :deep(.el-menu-item),
.admin-layout__menu :deep(.el-sub-menu__title) {
  color: #94a3b8;
  height: 44px;
  line-height: 44px;
}

.admin-layout__menu :deep(.el-menu-item:hover),
.admin-layout__menu :deep(.el-sub-menu__title:hover) {
  color: #e2e8f0;
  background: rgba(59, 130, 246, 0.08);
}

.admin-layout__menu :deep(.el-menu-item.is-active) {
  color: #3b82f6;
  background: rgba(59, 130, 246, 0.12);
  font-weight: 500;
}

/* 子菜单项缩进 */
.admin-layout__menu :deep(.el-sub-menu .el-menu-item) {
  background: #1a2332;
}

.admin-layout__menu :deep(.el-sub-menu .el-menu-item:hover) {
  background: rgba(59, 130, 246, 0.08);
}

/* ========== 主体容器 ========== */
.admin-layout__main-container {
  display: flex;
  flex-direction: column;
  background: #f1f5f9;
}

/* ========== 顶部导航栏 ========== */
.admin-layout__header {
  height: 60px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: #fff;
  border-bottom: 1px solid #e2e8f0;
  padding: 0 20px;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
}

.header-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.header-btn {
  color: #64748b;
  padding: 8px;
}

.header-btn:hover {
  color: #3b82f6;
  background: #f1f5f9;
}

.header-breadcrumb {
  margin-left: 4px;
}

.header-breadcrumb :deep(.el-breadcrumb__inner) {
  color: #64748b;
  font-size: 13px;
}

.header-breadcrumb :deep(.el-breadcrumb__inner.is-link) {
  color: #3b82f6;
}

/* 用户区域 */
.header-right {
  display: flex;
  align-items: center;
}

.header-user {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 8px;
  border-radius: 6px;
  cursor: pointer;
  transition: background 0.2s;
}

.header-user:hover {
  background: #f1f5f9;
}

.header-user__avatar {
  background: linear-gradient(135deg, #3b82f6, #1d4ed8);
  color: #fff;
  font-size: 13px;
  font-weight: 600;
}

.header-user__name {
  font-size: 13px;
  color: #334155;
  font-weight: 500;
}

/* ========== 内容区域 ========== */
.admin-layout__content {
  background: #f1f5f9;
  padding: 20px;
  overflow-y: auto;
}

/* ========== 动画 ========== */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
