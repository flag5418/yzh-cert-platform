<template>
  <el-container class="auditor-layout">
    <el-aside width="200px" class="auditor-layout__aside">
      <div class="auditor-layout__logo"><span>映智汇审核端</span></div>
      <el-menu :default-active="activeMenu" router class="auditor-layout__menu" background-color="#304156" text-color="#bfcbd9" active-text-color="#409eff">
        <el-menu-item index="/workspace">
          <span>工作台</span>
        </el-menu-item>
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
              {{ authStore.userInfo?.userName || '审核员' }}
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
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowDown } from '@element-plus/icons-vue'
import { useAuthStore } from '@/store/auth'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const activeMenu = computed(() => route.path)
const pageTitle = computed(() => {
  const map: Record<string, string> = { '/workspace': '工作台' }
  return map[route.path] || '审核员端'
})

function handleLogout() { authStore.clearToken(); router.push('/login') }
</script>

<style scoped>
.auditor-layout { height: 100vh; }
.auditor-layout__aside { background: #304156; overflow: hidden; }
.auditor-layout__logo { height: 60px; display: flex; align-items: center; justify-content: center; color: #fff; font-size: 16px; font-weight: bold; border-bottom: 1px solid #3a4a5b; }
.auditor-layout__menu { border-right: none; }
.auditor-layout__header { display: flex; align-items: center; justify-content: space-between; background: #fff; border-bottom: 1px solid #e4e7ed; padding: 0 20px; }
.auditor-layout__page-title { font-size: 16px; font-weight: 600; }
.auditor-layout__header-right { display: flex; align-items: center; }
.auditor-layout__user { display: flex; align-items: center; gap: 4px; cursor: pointer; color: #606266; }
.auditor-layout__main { background: #f5f7fa; padding: 20px; }
</style>
