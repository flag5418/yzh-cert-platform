<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { MenuOption } from 'naive-ui'
import {
  NLayout,
  NLayoutSider,
  NLayoutHeader,
  NLayoutContent,
  NMenu,
  NButton,
  NAvatar,
  NDropdown,
  NSpace,
  NText
} from 'naive-ui'
import { storeToRefs } from 'pinia'
import { useAppStore } from '@/stores/app'
import { useUserStore } from '@/stores/user'

const route = useRoute()
const router = useRouter()
const appStore = useAppStore()
const userStore = useUserStore()
const { themeMode } = storeToRefs(appStore)
const { userInfo } = storeToRefs(userStore)

const collapsed = ref(false)

// 侧边栏菜单（audit/report 为动态路由，先用示例任务占位）
const menuOptions: MenuOption[] = [
  { label: '审核工作台', key: '/workspace' },
  { label: '审核详情', key: '/audit/demo-task' },
  { label: '审核报告', key: '/report/demo-task' }
]

const activeKey = computed(() => {
  const path = route.path
  if (path.startsWith('/audit')) return '/audit/demo-task'
  if (path.startsWith('/report')) return '/report/demo-task'
  return path
})

function handleMenuSelect(key: string): void {
  router.push(key)
}

const userMenuOptions = [{ label: '退出登录', key: 'logout' }]

function handleUserSelect(key: string): void {
  if (key === 'logout') {
    userStore.logout()
    router.replace('/login')
  }
}

const isDark = computed(() => themeMode.value === 'dark')

function toggleTheme(): void {
  appStore.toggleTheme()
}

const displayName = computed(() => userInfo.value.realName || userInfo.value.userName || '审核员')
</script>

<template>
  <NLayout has-sider style="height: 100%">
    <NLayoutSider
      bordered
      collapse-mode="width"
      :collapsed-width="64"
      :width="220"
      :collapsed="collapsed"
      show-trigger
      @collapse="collapsed = true"
      @expand="collapsed = false"
    >
      <div class="yzh-logo">
        <NText strong>审核员端</NText>
      </div>
      <NMenu
        :value="activeKey"
        :options="menuOptions"
        :collapsed="collapsed"
        :collapsed-width="64"
        @update:value="handleMenuSelect"
      />
    </NLayoutSider>
    <NLayout>
      <NLayoutHeader bordered class="yzh-header">
        <NSpace align="center" justify="space-between" style="height: 100%; padding: 0 16px">
          <NText strong>{{ route.meta.title }}</NText>
          <NSpace align="center">
            <NButton quaternary size="small" @click="toggleTheme">
              {{ isDark ? '浅色' : '深色' }}
            </NButton>
            <NDropdown :options="userMenuOptions" @select="handleUserSelect">
              <NAvatar round size="small" style="cursor: pointer">
                {{ displayName.charAt(0) }}
              </NAvatar>
            </NDropdown>
          </NSpace>
        </NSpace>
      </NLayoutHeader>
      <NLayoutContent content-style="padding: 16px;">
        <router-view />
      </NLayoutContent>
    </NLayout>
  </NLayout>
</template>
