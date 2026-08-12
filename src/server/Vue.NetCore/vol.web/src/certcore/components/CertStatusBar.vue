<template>
  <div class="cert-status-bar">
    <div class="cert-status-bar__left">
      <slot />
    </div>
    <div v-if="$slots.right" class="cert-status-bar__right">
      <slot name="right" />
    </div>
  </div>
</template>

<script setup>
/**
 * CertStatusBar —— 底部状态栏（统一样式与间距）
 * 用法：<CertStatusBar>共 N 项 | 总大小 X</CertStatusBar>
 */
</script>

<style scoped>
.cert-status-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--yzh-space-3, 12px);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-5, 20px);
  background: var(--yzh-color-bg-page, #f5f7fa);
  border-top: 1px solid var(--yzh-color-border-light, #ebeef5);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #909399);
  flex-shrink: 0;
}

.cert-status-bar__left,
.cert-status-bar__right {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-4, 16px);
  min-width: 0;
}
</style>
