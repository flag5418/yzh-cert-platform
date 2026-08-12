<template>
  <div class="cert-page-header">
    <div class="cert-page-header__title">
      <el-icon v-if="icon" class="cert-page-header__icon"><component :is="icon" /></el-icon>
      <span>{{ title }}</span>
    </div>
    <div v-if="$slots.actions" class="cert-page-header__actions">
      <slot name="actions" />
    </div>
  </div>
</template>

<script setup>
/**
 * CertPageHeader —— 页面标题栏（统一图标 + 标题 + 右侧操作区）
 */
defineProps({
  title: { type: String, required: true },
  icon: { type: Object, default: null }
})
</script>

<style scoped>
.cert-page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-shrink: 0;
  height: 56px;
  padding: 0 var(--yzh-space-6, 24px);
  background: var(--yzh-color-bg-card, #fff);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
  box-shadow: var(--yzh-shadow-sm, 0 1px 4px rgba(0, 0, 0, 0.04));
}

.cert-page-header__title {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-lg, 16px);
  font-weight: var(--yzh-font-weight-bold, 600);
  color: var(--yzh-color-text-primary, #303133);
}

.cert-page-header__icon {
  font-size: 20px;
  color: var(--yzh-color-primary, #409eff);
}

.cert-page-header__actions {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}
</style>
