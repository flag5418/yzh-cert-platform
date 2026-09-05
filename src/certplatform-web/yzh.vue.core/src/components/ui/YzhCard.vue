<template>
  <div class="yzh-card">
    <div v-if="$slots.header || title" class="yzh-card__header">
      <slot name="header">{{ title }}</slot>
    </div>
    <div class="yzh-card__body">
      <slot />
    </div>
    <div v-if="$slots.footer" class="yzh-card__footer">
      <slot name="footer" />
    </div>
  </div>
</template>

<script setup>
defineProps({
  title: { type: String, default: '' }
})
</script>

<style scoped>
.yzh-card {
  background: var(--yzh-color-bg-card, #fff);
  border: 1px solid var(--yzh-color-border-light, #ebeef5);
  border-radius: 4px;
  overflow: hidden;
}

.yzh-card__header {
  padding: 16px 20px;
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
  font-size: 14px;
  font-weight: 600;
  color: var(--yzh-color-text-primary, #303133);
}

.yzh-card__body {
  padding: 20px;
}

.yzh-card__footer {
  padding: 12px 20px;
  border-top: 1px solid var(--yzh-color-border-light, #ebeef5);
  background: #fafafa;
}
</style>
