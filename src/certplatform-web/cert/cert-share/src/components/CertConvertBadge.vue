<template>
  <span class="cert-convert-badge" :class="badgeClass">
    <el-icon v-if="icon"><component :is="icon" /></el-icon>
    {{ label }}
  </span>
</template>

<script setup>
import { computed } from 'vue'

const props = defineProps({
  status: { type: String, default: '' }
})

const STATUS_MAP = {
  pending: { class: 'is-pending', label: '待转换', color: '#909399' },
  converting: { class: 'is-converting', label: '转换中', color: '#409eff' },
  completed: { class: 'is-completed', label: '已转换', color: '#67c23a' },
  failed: { class: 'is-failed', label: '失败', color: '#f56c6c' }
}

const config = computed(() => STATUS_MAP[props.status] || STATUS_MAP.pending)
const badgeClass = computed(() => `cert-convert-badge${config.value.class}`)
const label = computed(() => config.value.label)
</script>

<style scoped>
.cert-convert-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
  padding: 2px 6px;
  border-radius: 3px;
  white-space: nowrap;
}

.cert-convert-badge.is-pending { color: #909399; background: #f4f4f5; }
.cert-convert-badge.is-converting { color: #409eff; background: #ecf5ff; }
.cert-convert-badge.is-completed { color: #67c23a; background: #f0f9eb; }
.cert-convert-badge.is-failed { color: #f56c6c; background: #fef0f0; }
</style>
