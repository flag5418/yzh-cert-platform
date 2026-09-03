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
  height: 48px; /* 压缩高度 */
  padding: 0 16px;
  background: #fff;
  border-bottom: 1px solid #e2e8f0;
}

.cert-page-header__title {
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 15px; /* 严谨字号 */
  font-weight: 700;
  color: #1e293b;
}

.cert-page-header__icon {
  font-size: 18px;
  color: var(--yzh-color-primary, #2f54eb);
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 0; /* 彻底去圆角 */
}

.cert-page-header__actions {
  display: flex;
  align-items: center;
  gap: 8px;
}
</style>
