<template>
  <el-container class="admin-layout">
    <!-- ========== 侧边栏 ========== -->
    <el-aside width="230px" class="admin-layout__aside">
      <!-- Logo 区域 -->
      <div class="admin-layout__brand">
        <div class="brand-logo">
          <span class="brand-logo__icon">YZH</span>
        </div>
        <span class="brand-text">映智汇认证平台</span>
      </div>

      <!-- 菜单区域 -->
      <el-menu
        :default-active="activeMenu"
        router
        class="admin-layout__menu"
      >
        <template v-for="menu in menuStore.menus" :key="menu.id">
          <!-- 有子菜单：渲染为 el-sub-menu -->
          <el-sub-menu v-if="menu.children && menu.children.length > 0" :index="menu.url || String(menu.id)">
            <template #title>
              <el-icon v-if="menu.icon"><component :is="formatIcon(menu.icon)" /></el-icon>
              <span>{{ menu.menuName }}</span>
            </template>
            <el-menu-item
              v-for="child in menu.children"
              :key="child.id"
              :index="child.url || String(child.id)"
            >
              <el-icon v-if="child.icon"><component :is="formatIcon(child.icon)" /></el-icon>
              <span>{{ child.menuName }}</span>
            </el-menu-item>
          </el-sub-menu>

          <!-- 无子菜单：渲染为 el-menu-item -->
          <el-menu-item v-else :index="menu.url || String(menu.id)">
            <el-icon v-if="menu.icon"><component :is="formatIcon(menu.icon)" /></el-icon>
            <span>{{ menu.menuName }}</span>
          </el-menu-item>
        </template>
      </el-menu>
    </el-aside>

    <!-- ========== 主体区域 ========== -->
    <el-container class="admin-layout__main-container">
      <!-- 顶部导航栏 -->
      <el-header class="admin-layout__header">
        <div class="header-left">
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
                <el-dropdown-item command="profile">个人设置</el-dropdown-item>
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

  <!-- 个人信息弹窗 -->
  <el-dialog
    v-if="authStore.userInfo"
    v-model="profileDialogVisible"
    title="个人信息"
    width="480px"
    :close-on-click-modal="false"
  >
    <el-form :model="profileForm" label-width="90px" class="profile-form">
      <el-form-item label="用户账号">
        <el-input :model-value="authStore.userInfo.userName" disabled />
      </el-form-item>
      <el-form-item label="昵称">
        <el-input v-model="profileForm.nickname" placeholder="请输入昵称" />
      </el-form-item>
      <el-form-item label="姓名">
        <el-input v-model="profileForm.userTrueName" placeholder="请输入真实姓名" />
      </el-form-item>
      <el-form-item label="邮箱">
        <el-input v-model="profileForm.email" placeholder="请输入邮箱" />
      </el-form-item>
      <el-form-item label="电话">
        <el-input v-model="profileForm.phone" placeholder="请输入电话" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="profileDialogVisible = false">取消</el-button>
      <el-button type="primary" @click="saveProfile">保存</el-button>
    </template>
  </el-dialog>

  <!-- 修改密码弹窗 -->
  <el-dialog
    v-model="passwordDialogVisible"
    title="修改密码"
    width="480px"
    :close-on-click-modal="false"
  >
    <el-form
      ref="passwordFormRef"
      :model="passwordForm"
      :rules="passwordRules"
      label-width="90px"
      class="profile-form"
    >
      <el-form-item label="原密码" prop="oldPassword">
        <el-input
          v-model="passwordForm.oldPassword"
          type="password"
          placeholder="请输入原密码"
          show-password
        />
      </el-form-item>
      <el-form-item label="新密码" prop="newPassword">
        <el-input
          v-model="passwordForm.newPassword"
          type="password"
          placeholder="至少6位字符"
          show-password
        />
      </el-form-item>
      <el-form-item label="确认密码" prop="confirmPassword">
        <el-input
          v-model="passwordForm.confirmPassword"
          type="password"
          placeholder="请再次输入新密码"
          show-password
        />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="passwordDialogVisible = false">取消</el-button>
      <el-button type="primary" @click="changePassword">确认修改</el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowDown } from '@element-plus/icons-vue'
import type { FormInstance, FormRules } from 'element-plus'
import { useAuthStore } from '@/store/auth'
import { useMenuStore } from '@/store/menu'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const menuStore = useMenuStore()

// 当前激活的菜单
const activeMenu = computed(() => route.path)

// 当前页面标题（从菜单中查找）
const currentPageTitle = computed(() => {
  const path = route.path
  // 递归查找菜单名称
  const findMenuName = (menus: any[]): string | null => {
    for (const m of menus) {
      if (m.url === path) return m.menuName
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

// ========== 弹窗状态 ==========
const profileDialogVisible = ref(false)
const passwordDialogVisible = ref(false)
const passwordFormRef = ref<FormInstance>()

const profileForm = reactive({
  nickname: authStore.userInfo?.nickname || '',
  userTrueName: authStore.userInfo?.userTrueName || '',
  email: authStore.userInfo?.email || '',
  phone: authStore.userInfo?.phone || ''
})

const passwordForm = reactive({
  oldPassword: '',
  newPassword: '',
  confirmPassword: ''
})

const validateConfirmPassword = (_rule: any, value: string, callback: (err?: Error) => void) => {
  if (value !== passwordForm.newPassword) {
    callback(new Error('两次输入的密码不一致'))
  } else {
    callback()
  }
}

const passwordRules: FormRules = {
  oldPassword: [{ required: true, message: '请输入原密码', trigger: 'blur' }],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, message: '密码长度至少6位', trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, message: '请确认新密码', trigger: 'blur' },
    { validator: validateConfirmPassword, trigger: 'blur' }
  ]
}

function openProfileDialog() {
  profileForm.nickname = authStore.userInfo?.nickname || ''
  profileForm.userTrueName = authStore.userInfo?.userTrueName || ''
  profileForm.email = authStore.userInfo?.email || ''
  profileForm.phone = authStore.userInfo?.phone || ''
  profileDialogVisible.value = true
}

function openPasswordDialog() {
  passwordForm.oldPassword = ''
  passwordForm.newPassword = ''
  passwordForm.confirmPassword = ''
  passwordDialogVisible.value = true
}

function saveProfile() {
  if (!authStore.userInfo) return
  authStore.userInfo.nickname = profileForm.nickname
  authStore.userInfo.userTrueName = profileForm.userTrueName
  authStore.userInfo.email = profileForm.email
  authStore.userInfo.phone = profileForm.phone
  ElMessage.success('个人信息已保存')
  profileDialogVisible.value = false
}

async function changePassword() {
  if (!passwordFormRef.value) return
  await passwordFormRef.value.validate((valid) => {
    if (valid) {
      ElMessage.success('密码修改成功，请重新登录')
      passwordDialogVisible.value = false
      setTimeout(() => {
        authStore.clearToken()
        menuStore.clearMenus()
        router.push('/login')
      }, 1000)
    }
  })
}

// Element UI → Element Plus 图标名称映射表
// 用于兼容旧版 Vol 框架的图标命名
const ICON_NAME_MAP: Record<string, string> = {
  // 系统管理
  'setting': 'Setting',
  's-home': 'HomeFilled',
  'user-solid': 'UserFilled',
  'menu': 'Menu',
  'connection': 'Connection',
  'folder': 'Folder',
  'link': 'Link',
  'receiving': 'Collection',
  'document': 'Document',
  's-tools': 'Tools',
  // 业务管理
  'document-checked': 'DocumentChecked',
  'office-building': 'OfficeBuilding',
  'date': 'Date',
  'document-copy': 'DocumentCopy',
  'operation': 'Operation',
  'files': 'Files',
  'tickets': 'Tickets',
  'collection': 'Collection',
  'chat-line-round': 'ChatLineRound',
  'cpu': 'Cpu',
  'edit': 'Edit',
  'warning': 'Warning',
  'set-up': 'SetUp',
  'money': 'Money',
  's-data': 'DataAnalysis',
}

// 默认图标（当找不到匹配时使用）
const DEFAULT_ICON = 'Menu'

// 格式化图标名称（el-icon-xxx → 组件名）
function formatIcon(iconName: string): string {
  if (!iconName) return DEFAULT_ICON

  // 移除常见前缀
  let name = iconName
    .replace(/^el-icon-/, '')
    .replace(/^ivu-icon ivu-icon-/, '')

  // 优先使用映射表
  if (ICON_NAME_MAP[name]) {
    return ICON_NAME_MAP[name]
  }

  // 尝试直接转换 PascalCase（可能是已经是 Element Plus 名称）
  const pascalName = name
    .split('-')
    .map(s => s.charAt(0).toUpperCase() + s.slice(1))
    .join('')

  // 检查是否是有效的 Element Plus 图标名（首字母大写）
  if (pascalName.length > 0 && /^[A-Z]/.test(pascalName)) {
    return pascalName
  }

  return DEFAULT_ICON
}

// 下拉菜单命令处理
function handleCommand(command: string) {
  if (command === 'logout') {
    ElMessageBox.confirm('确定要退出登录吗？', '提示', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
      .then(() => {
        authStore.clearToken()
        menuStore.clearMenus()
        ElMessage.success('已退出登录')
        router.push('/login')
      })
      .catch(() => {})
  } else if (command === 'profile') {
    openProfileDialog()
  } else if (command === 'password') {
    openPasswordDialog()
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
  background: #1a2332;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

/* Logo 区域 */
.admin-layout__brand {
  height: 64px;
  display: flex;
  align-items: center;
  padding: 0 16px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
}

.brand-logo__icon {
  width: 36px;
  height: 36px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 700;
  color: #fff;
  background: linear-gradient(135deg, var(--yzh-color-primary), var(--yzh-color-primary-light));
  border-radius: 8px;
  flex-shrink: 0;
  letter-spacing: 0.5px;
}

.brand-text {
  margin-left: 12px;
  color: rgba(255, 255, 255, 0.88);
  font-size: 15px;
  font-weight: 600;
  white-space: nowrap;
  letter-spacing: 0.3px;
}

/* 菜单区域 */
.admin-layout__menu {
  border-right: none;
  background: #1a2332;
  overflow-y: auto;
  overflow-x: hidden;
  flex: 1;
  padding-top: 8px;
}

/* 覆盖 el-menu 自身背景 */
.admin-layout__menu :deep(.el-menu) {
  background-color: transparent;
  border-right: none;
}

/* 覆盖 Element Plus 菜单项样式 */
.admin-layout__menu :deep(.el-menu-item),
.admin-layout__menu :deep(.el-sub-menu__title) {
  color: #ffffff;
  background-color: transparent;
  height: 42px;
  line-height: 42px;
  margin: 0 8px;
  border-radius: 6px;
}

.admin-layout__menu :deep(.el-menu-item:hover),
.admin-layout__menu :deep(.el-sub-menu__title:hover) {
  color: #ffffff;
  background: rgba(255, 255, 255, 0.1);
}

.admin-layout__menu :deep(.el-menu-item.is-active) {
  color: #ffffff;
  background: var(--yzh-color-primary);
  font-weight: 500;
}

/* 子菜单项 */
.admin-layout__menu :deep(.el-sub-menu .el-menu-item) {
  background: transparent;
  padding-left: 50px !important;
}

.admin-layout__menu :deep(.el-sub-menu .el-menu-item:hover) {
  background: rgba(255, 255, 255, 0.1);
}

/* ========== 主体容器 ========== */
.admin-layout__main-container {
  display: flex;
  flex-direction: column;
  background: #f5f5f5;
}

/* ========== 顶部导航栏 ========== */
.admin-layout__header {
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: #ffffff;
  padding: 0 24px;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.06);
}

.header-left {
  display: flex;
  align-items: center;
}

.header-breadcrumb :deep(.el-breadcrumb__inner) {
  color: #64748b;
  font-size: 13px;
}

.header-breadcrumb :deep(.el-breadcrumb__inner.is-link) {
  color: var(--yzh-color-primary);
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
  background: linear-gradient(135deg, var(--yzh-color-primary), var(--yzh-color-primary-light));
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
  background: #f5f5f5;
  padding: 24px;
  overflow-y: auto;
}

/* ========== 弹窗内表单 ========== */
.profile-form {
  max-width: 100%;
}

.profile-form .el-form-item {
  margin-bottom: 20px;
}

.profile-form .el-input__wrapper {
  background: #f9fafb;
}

.profile-form .el-input.is-disabled .el-input__wrapper {
  background: #f3f4f6;
}

.profile-form .el-input.is-disabled .el-input__inner {
  color: #6b7280;
}
</style>
