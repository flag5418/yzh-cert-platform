<template>
  <span class="yzh-status-badge" :class="[`is-${type}`, `is-${size}`]">
    <el-icon v-if="icon || defaultIcon" class="yzh-status-badge__icon">
      <component :is="icon || defaultIcon" />
    </el-icon>
    <slot>{{ text }}</slot>
  </span>
</template>

<script setup>
/**
 * YzhStatusBadge —— 状态徽章（对齐 vidlang Badge + 4 语义子类）
 * type: success（成功）/ warning（待处理）/ danger（失败）/ info（未配置/提示）
 * 颜色与图标全部取自 yzh 令牌与 YzhIcon，禁止 emoji / 文本字符当状态
 */
import { computed } from 'vue'
import { IconSuccess, IconWarning, IconError, IconInfo } from '@/yzh/icons'

const props = defineProps({
  type: { type: String, default: 'info' }, // success | warning | danger | info
  text: { type: String, default: '' },
  icon: { type: Object, default: null },
  size: { type: String, default: 'small' } // small | default
})

const defaultIcon = computed(() => {
  const map = {
    success: IconSuccess,
    warning: IconWarning,
    danger: IconError,
    info: IconInfo
  }
  return map[props.type] || IconInfo
})
</script>

<style scoped>
.yzh-status-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  border-radius: var(--yzh-radius-sm, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1;
  white-space: nowrap;
}

.yzh-status-badge.is-small {
  padding: 3px 6px;
}

.yzh-status-badge.is-default {
  padding: 5px 10px;
}

.yzh-status-badge__icon {
  font-size: 12px;
}

/* 语义类型（颜色取自 yzh-colors 令牌） */
.yzh-status-badge.is-success {
  color: var(--yzh-color-success, #67c23a);
  background: var(--yzh-color-success-light-9, #f0f9eb);
}

.yzh-status-badge.is-warning {
  color: var(--yzh-color-warning, #e6a23c);
  background: var(--yzh-color-warning-light-9, #fdf6ec);
}

.yzh-status-badge.is-danger {
  color: var(--yzh-color-danger, #f56c6c);
  background: var(--yzh-color-danger-light-9, #fef0f0);
}

.yzh-status-badge.is-info {
  color: var(--yzh-color-text-secondary, #909399);
  background: var(--yzh-color-info-light-9, #f4f4f5);
}
</style>
