<template>
  <el-dropdown trigger="click" class="yzh-theme-toggle" @command="handleCommand">
    <span class="yzh-theme-toggle__trigger">
      <span class="yzh-theme-toggle__label">主题</span>
      <el-icon class="yzh-theme-toggle__caret"><ArrowDown /></el-icon>
    </span>
    <template #dropdown>
      <el-dropdown-menu>
        <el-dropdown-item command="light">
          <el-icon class="yzh-theme-toggle__check" :class="{ 'is-hidden': theme !== 'light' }"><Check /></el-icon>
          亮色主题
        </el-dropdown-item>
        <el-dropdown-item command="dark">
          <el-icon class="yzh-theme-toggle__check" :class="{ 'is-hidden': theme !== 'dark' }"><Check /></el-icon>
          暗色主题
        </el-dropdown-item>
      </el-dropdown-menu>
    </template>
  </el-dropdown>
</template>

<script setup lang="ts">
import { ArrowDown, Check } from '@element-plus/icons-vue'
import { useTheme } from '../../composables/useTheme'

const { theme, setTheme } = useTheme()

function handleCommand(command: string) {
  if (command === 'light' || command === 'dark') {
    setTheme(command)
  }
}
</script>

<style scoped>
.yzh-theme-toggle__trigger {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
  padding: var(--yzh-space-1, 4px) var(--yzh-space-2, 8px);
  border-radius: var(--yzh-radius-sm, 4px);
  cursor: pointer;
  color: var(--yzh-color-text-body, #334155);
  transition: background var(--yzh-transition-fast, 150ms ease);
}

.yzh-theme-toggle__trigger:hover {
  background: var(--yzh-color-bg-hover, #f1f5f9);
}

.yzh-theme-toggle__label {
  font-size: var(--yzh-font-size-sm, 13px);
}

.yzh-theme-toggle__caret {
  font-size: var(--yzh-font-size-xs, 12px);
}

.yzh-theme-toggle__check {
  margin-right: var(--yzh-space-1, 4px);
  color: var(--yzh-color-primary, #1e3a8a);
}

.yzh-theme-toggle__check.is-hidden {
  visibility: hidden;
}
</style>
