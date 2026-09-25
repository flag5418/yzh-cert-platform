<template>
  <el-container class="yzh-layout">
    <!-- ========== 侧边栏 ========== -->
    <el-aside width="230px" class="yzh-layout__aside">
      <!-- Logo 区域（#brand slot 可整块替换） -->
      <div class="yzh-layout__brand">
        <slot name="brand">
          <div class="brand-logo">
            <span class="brand-logo__icon">{{ logoText }}</span>
          </div>
          <span class="brand-text">{{ appTitle }}</span>
        </slot>
      </div>

      <!-- 菜单区域（按 menuTag 分流；数据来自 useMenuTree 模块单例） -->
      <el-menu
        :default-active="activeMenu"
        router
        class="yzh-layout__menu"
      >
        <template v-for="menu in visibleMenus" :key="menu.id">
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
    <el-container class="yzh-layout__main-container">
      <!-- 顶部导航栏 -->
      <el-header class="yzh-layout__header">
        <div class="header-left">
          <!-- 面包屑 -->
          <el-breadcrumb separator="/" class="header-breadcrumb">
            <el-breadcrumb-item :to="{ path: '/' }">首页</el-breadcrumb-item>
            <el-breadcrumb-item>{{ currentPageTitle }}</el-breadcrumb-item>
          </el-breadcrumb>
        </div>

        <div class="header-right">
          <!-- 宿主附加顶栏项（slot） -->
          <slot name="header-right" />
          <!-- 用户信息下拉（#user-dropdown 可整块替换） -->
          <slot name="user-dropdown">
            <el-dropdown trigger="click" @command="handleCommand">
              <div class="header-user">
                <el-avatar :size="32" class="header-user__avatar">
                  {{ userInitial }}
                </el-avatar>
                <span class="header-user__name">{{ userInfo?.UserTrueName || userInfo?.UserName || '管理员' }}</span>
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
          </slot>
        </div>
      </el-header>

      <!-- 内容区域 -->
      <el-main class="yzh-layout__content">
        <router-view v-slot="{ Component }">
          <keep-alive><component :is="Component" /></keep-alive>
        </router-view>
      </el-main>
    </el-container>
  </el-container>

  <!-- 个人信息弹窗 -->
  <el-dialog
    v-if="userInfo"
    v-model="profileDialogVisible"
    title="个人信息"
    width="480px"
    :close-on-click-modal="false"
  >
    <el-form :model="profileForm" label-width="90px" class="profile-form">
      <el-form-item label="用户账号">
        <el-input :model-value="userInfo?.UserName" disabled />
      </el-form-item>
      <el-form-item label="姓名">
        <el-input v-model="profileForm.UserTrueName" placeholder="请输入真实姓名" />
      </el-form-item>
      <el-form-item label="邮箱">
        <el-input v-model="profileForm.Email" placeholder="请输入邮箱" />
      </el-form-item>
      <el-form-item label="电话">
        <el-input v-model="profileForm.PhoneNo" placeholder="请输入电话" />
      </el-form-item>
      <el-form-item label="备注">
        <el-input v-model="profileForm.Remark" type="textarea" :rows="2" placeholder="请输入备注" />
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
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowDown } from '@element-plus/icons-vue'
import type { FormInstance, FormRules } from 'element-plus'
import { getCurrentUser, modifyPwd, updateUserInfo } from '../api/auth'
import { useAuthState } from '../composables/useAuthState'
import { useMenuTree } from '../composables/useMenuTree'
import { onMenuChanged } from '../composables/useMenuChanged'
import { filterMenuTreeByTag, formatMenuIcon as formatIcon } from '../utils/menu'

/**
 * 原子应用布局（系统底座默认壳：侧栏 + 顶栏 + 个人中心 + 改密）
 *
 * 宿主定制：
 *   - props：logoText / appTitle（品牌）、menuTag（侧栏菜单分流，如 'admin' / 'auditor'）
 *   - slots：#brand（Logo 区整块替换）、#header-right（顶栏附加项）、#user-dropdown（用户区整块替换）
 *   - 路由：本组件仅是 '/' 的 component —— path 归宿主（契约铁律 #1）
 *
 * 状态全部走 core 模块单例（useAuthState / useMenuTree），零 pinia、零宿主 store 依赖（守卫 R10）。
 * 品牌色 `--yzh-color-primary` 等 CSS 变量由宿主全局样式注入。
 */

const props = withDefaults(defineProps<{
  /** Logo 方块文字 */
  logoText?: string
  /** 品牌名（Logo 旁） */
  appTitle?: string
  /** 侧边栏菜单按 Sys_Menu.Tag 分流（超管全量菜单需滤掉其他端分组） */
  menuTag?: string
}>(), {
  logoText: 'YZH',
  appTitle: '映智汇认证平台',
  menuTag: 'admin'
})

const route = useRoute()
const router = useRouter()
const { token, userInfo, setUserInfo, patchUserInfo, clearToken } = useAuthState()
const { menus, loadMenus, clearMenus } = useMenuTree()

// 当前激活的菜单
const activeMenu = computed(() => route.path)

/** 侧边栏菜单：按 menuTag 分流 */
const visibleMenus = computed(() => filterMenuTreeByTag(menus.value, props.menuTag))

// 当前页面标题（从菜单中查找）
const currentPageTitle = computed(() => {
  const path = route.path
  const findMenuName = (list: typeof menus.value): string | null => {
    for (const m of list) {
      if (m.url === path) return m.menuName
      if (m.children?.length) {
        const found = findMenuName(m.children)
        if (found) return found
      }
    }
    return null
  }
  return findMenuName(visibleMenus.value) || '首页'
})

// 用户头像首字母
const userInitial = computed(() => {
  const name = userInfo.value?.UserTrueName || userInfo.value?.UserName || 'A'
  return name.charAt(0).toUpperCase()
})

// ========== 弹窗状态 ==========
const profileDialogVisible = ref(false)
const passwordDialogVisible = ref(false)
const passwordFormRef = ref<FormInstance>()
const profileSaving = ref(false)
const passwordSaving = ref(false)

/**
 * 个人资料表单
 *
 * ⚠️ 字段名与后端 `Sys_User` 的 C# 属性名逐字一致（`项目全局规则.md` §16.9 铁律）。
 *    camelCase 提交后端会全部落空；无对应列的「昵称」用真实存在的 `Remark`。
 */
const profileForm = reactive({
  UserTrueName: '',
  Email: '',
  PhoneNo: '',
  Remark: ''
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

/**
 * 加载当前登录用户信息（个人中心）
 *
 * 登录响应只给 Token/UserCode/UserName/UserTrueName/RoleCode 且未持久化 ——
 * 刷新页面后 userInfo 为空，顶栏退化成「管理员」、个人中心字段全空。挂载时必须回填。
 */
async function loadCurrentUser() {
  if (!token.value) return
  try {
    const res = await getCurrentUser()
    if (res?.success === false) {
      ElMessage.error(res?.message || '获取用户信息失败')
      return
    }
    const data = res?.data
    if (data) setUserInfo({ ...data, Token: token.value })
  } catch {
    // 静默失败：顶栏已有兜底文案，不打断用户操作
  }
}

function openProfileDialog() {
  profileForm.UserTrueName = userInfo.value?.UserTrueName || ''
  profileForm.Email = userInfo.value?.Email || ''
  profileForm.PhoneNo = userInfo.value?.PhoneNo || ''
  profileForm.Remark = userInfo.value?.Remark || ''
  profileDialogVisible.value = true
}

function openPasswordDialog() {
  passwordForm.oldPassword = ''
  passwordForm.newPassword = ''
  passwordForm.confirmPassword = ''
  passwordDialogVisible.value = true
}

async function saveProfile() {
  if (profileSaving.value) return
  profileSaving.value = true
  try {
    const res = await updateUserInfo({
      UserTrueName: profileForm.UserTrueName,
      Email: profileForm.Email,
      PhoneNo: profileForm.PhoneNo,
      Remark: profileForm.Remark
    })
    if (res?.success === false) {
      ElMessage.error(res?.message || '保存失败')
      return
    }
    patchUserInfo({
      UserTrueName: profileForm.UserTrueName,
      Email: profileForm.Email,
      PhoneNo: profileForm.PhoneNo,
      Remark: profileForm.Remark
    })
    ElMessage.success('个人信息已保存')
    profileDialogVisible.value = false
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    profileSaving.value = false
  }
}

async function changePassword() {
  if (!passwordFormRef.value) return
  await passwordFormRef.value.validate(async (valid) => {
    if (!valid) return
    if (passwordSaving.value) return
    passwordSaving.value = true
    try {
      const res = await modifyPwd(passwordForm.oldPassword, passwordForm.newPassword)
      if (res?.success === false) {
        ElMessage.error(res?.message || '密码修改失败')
        return
      }
      ElMessage.success(res?.message || '密码修改成功，请重新登录')
      passwordDialogVisible.value = false
      setTimeout(() => {
        clearToken()
        clearMenus()
        router.push('/login')
      }, 1000)
    } catch (e: any) {
      ElMessage.error(e?.message || '密码修改失败')
    } finally {
      passwordSaving.value = false
    }
  })
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
        clearToken()
        clearMenus()
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

// 加载菜单 + 回填当前用户信息；订阅菜单变更（菜单管理页 notifyMenuChanged 后侧栏强刷）
let stopMenuWatch: (() => void) | null = null
onMounted(() => {
  loadMenus()
  loadCurrentUser()
  stopMenuWatch = onMenuChanged(() => {
    loadMenus(true)
  })
})
onUnmounted(() => {
  stopMenuWatch?.()
  stopMenuWatch = null
})
</script>

<style scoped>
/* ========== 整体布局 ========== */
.yzh-layout {
  height: 100vh;
}

/* ========== 侧边栏 ========== */
.yzh-layout__aside {
  background: var(--yzh-color-sidebar-bg);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

/* Logo 区域 */
.yzh-layout__brand {
  height: 64px;
  display: flex;
  align-items: center;
  padding: 0 16px;
  border-bottom: 1px solid var(--yzh-color-sidebar-divider);
}

.brand-logo__icon {
  width: 36px;
  height: 36px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 700;
  color: var(--yzh-color-text-inverse);
  background: linear-gradient(135deg, var(--yzh-color-primary), var(--yzh-color-primary-light));
  border-radius: 8px;
  flex-shrink: 0;
  letter-spacing: 0.5px;
}

.brand-text {
  margin-left: 12px;
  color: var(--yzh-color-sidebar-text-muted);
  font-size: 15px;
  font-weight: 600;
  white-space: nowrap;
  letter-spacing: 0.3px;
}

/* 菜单区域 */
.yzh-layout__menu {
  border-right: none;
  background: var(--yzh-color-sidebar-bg);
  overflow-y: auto;
  overflow-x: hidden;
  flex: 1;
  padding-top: 8px;
  /* 隐藏滚动条（保留滚动功能） */
  scrollbar-width: none;        /* Firefox */
  -ms-overflow-style: none;     /* IE 10+ */
}

/* Webkit 浏览器隐藏滚动条 */
.yzh-layout__menu::-webkit-scrollbar {
  width: 0;
  display: none;
}

/* 覆盖 el-menu 自身背景 */
.yzh-layout__menu :deep(.el-menu) {
  background-color: transparent;
  border-right: none;
}

/* 覆盖 Element Plus 菜单项样式 */
.yzh-layout__menu :deep(.el-menu-item),
.yzh-layout__menu :deep(.el-sub-menu__title) {
  color: var(--yzh-color-sidebar-text);
  background-color: transparent;
  height: 42px;
  line-height: 42px;
  margin: 0 8px;
  border-radius: 6px;
}

.yzh-layout__menu :deep(.el-menu-item:hover),
.yzh-layout__menu :deep(.el-sub-menu__title:hover) {
  color: var(--yzh-color-sidebar-text);
  background: var(--yzh-color-sidebar-hover);
}

.yzh-layout__menu :deep(.el-menu-item.is-active) {
  color: var(--yzh-color-sidebar-text);
  background: var(--yzh-color-primary);
  font-weight: 500;
}

/* 子菜单项 */
.yzh-layout__menu :deep(.el-sub-menu .el-menu-item) {
  background: transparent;
  padding-left: 50px !important;
}

.yzh-layout__menu :deep(.el-sub-menu .el-menu-item:hover) {
  background: var(--yzh-color-sidebar-hover);
}

/* ========== 主体容器 ========== */
.yzh-layout__main-container {
  display: flex;
  flex-direction: column;
  background: var(--yzh-color-bg-page);
}

/* ========== 顶部导航栏 ========== */
.yzh-layout__header {
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: var(--yzh-color-bg-container);
  padding: 0 24px;
  box-shadow: var(--yzh-shadow-header);
}

.header-left {
  display: flex;
  align-items: center;
}

.header-breadcrumb :deep(.el-breadcrumb__inner) {
  color: var(--yzh-color-text-muted);
  font-size: 13px;
}

.header-breadcrumb :deep(.el-breadcrumb__inner.is-link) {
  color: var(--yzh-color-primary);
}

/* 用户区域 */
.header-right {
  display: flex;
  align-items: center;
  gap: 8px;
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
  background: var(--yzh-color-bg-hover);
}

.header-user__avatar {
  background: linear-gradient(135deg, var(--yzh-color-primary), var(--yzh-color-primary-light));
  color: var(--yzh-color-text-inverse);
  font-size: 13px;
  font-weight: 600;
}

.header-user__name {
  font-size: 13px;
  color: var(--yzh-color-text-body);
  font-weight: 500;
}

/* ========== 内容区域 ========== */
.yzh-layout__content {
  background: var(--yzh-color-bg-page);
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
  background: var(--yzh-color-bg-subtle);
}

.profile-form .el-input.is-disabled .el-input__wrapper {
  background: var(--yzh-color-bg-muted);
}

.profile-form .el-input.is-disabled .el-input__inner {
  color: var(--yzh-color-text-subtle);
}
</style>
