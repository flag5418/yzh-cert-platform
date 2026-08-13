<template>
  <div class="yzh-lock-status">
    <el-icon class="yzh-lock-status__icon"><IconLoading /></el-icon>
    <div class="yzh-lock-status__body">
      <div class="yzh-lock-status__title">{{ title }}</div>
      <div v-if="description" class="yzh-lock-status__desc">{{ description }}</div>
      <el-tag v-if="tagText" :type="tagType" size="small" class="yzh-lock-status__tag">
        {{ tagText }}
      </el-tag>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { IconLoading } from '@/yzh/icons'

const props = defineProps({
  /** 标题（必填） */
  title: { type: String, required: true },
  /** 描述文字（可选） */
  description: { type: String, default: '' },
  /** 标签文字（可选，如"队列执行中锁定"） */
  tagText: { type: String, default: '' },
  /** 标签类型 */
  tagType: { type: String, default: 'danger' } // danger | warning | info
})
</script>

<style scoped>
.yzh-lock-status {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-4, 16px);
  padding: var(--yzh-space-6, 24px);
  background: var(--yzh-color-bg-page, #f5f7fa);
  border: 1px solid var(--yzh-color-danger-light-7, #fde2e2);
  border-radius: var(--yzh-radius-md, 8px);
}

.yzh-lock-status__icon {
  font-size: 36px;
  color: var(--yzh-color-danger, #f56c6c);
  flex-shrink: 0;
  animation: yzh-spin 1s linear infinite;
}

.yzh-lock-status__body {
  flex: 1;
  min-width: 0;
}

.yzh-lock-status__title {
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: 600;
  color: var(--yzh-color-text-primary, #303133);
  margin-bottom: 4px;
}

.yzh-lock-status__desc {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-secondary, #909399);
  line-height: 1.6;
  margin-bottom: 8px;
  word-break: break-all;
}

.yzh-lock-status__tag {
  flex-shrink: 0;
}

@keyframes yzh-spin {
  from { transform: rotate(0deg); }
  to   { transform: rotate(360deg); }
}
</style>
