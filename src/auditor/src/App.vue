<script setup lang="ts">
import { computed } from 'vue'
import { storeToRefs } from 'pinia'
import {
  NConfigProvider,
  NMessageProvider,
  NNotificationProvider,
  NDialogProvider,
  NLoadingBarProvider,
  darkTheme,
  zhCN,
  dateZhCN
} from 'naive-ui'
import type { GlobalTheme } from 'naive-ui'
import { useAppStore } from '@/stores/app'
import { themeOverrides } from '@/theme'

const appStore = useAppStore()
const { themeMode } = storeToRefs(appStore)

const theme = computed<GlobalTheme | null>(() =>
  themeMode.value === 'dark' ? darkTheme : null
)
</script>

<template>
  <NConfigProvider
    :theme="theme"
    :theme-overrides="themeOverrides"
    :locale="zhCN"
    :date-locale="dateZhCN"
  >
    <NLoadingBarProvider>
      <NDialogProvider>
        <NNotificationProvider>
          <NMessageProvider>
            <router-view />
          </NMessageProvider>
        </NNotificationProvider>
      </NDialogProvider>
    </NLoadingBarProvider>
  </NConfigProvider>
</template>
