<template>
  <div class="cert-page-header">
    <div class="cert-page-header__title"><slot name="title">{{ title }}</slot></div>
    <div v-if="subtitle" class="cert-page-header__subtitle">{{ subtitle }}</div>
  </div>
</template>

<script setup>
defineProps({ title: { type: String, default: '' }, subtitle: { type: String, default: '' } })
</script>

<style scoped>
.cert-page-header { margin-bottom: 16px; }
.cert-page-header__title { font-size: 18px; font-weight: 600; color: #303133; }
.cert-page-header__subtitle { font-size: 13px; color: #909399; margin-top: 4px; }
</style>
