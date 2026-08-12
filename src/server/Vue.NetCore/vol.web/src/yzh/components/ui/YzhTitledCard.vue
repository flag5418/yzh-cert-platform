<template>
  <YzhBaseCard :variant="variant" :padding="padding" :margin="margin" :border-radius="borderRadius" :background-color="backgroundColor" :show-border="showBorder" :shadow="shadow">
    <div class="yzh-titled-card">
      <div class="yzh-titled-card__header">
        <div class="yzh-titled-card__heading">
          <el-icon v-if="leadingIcon" class="yzh-titled-card__icon"><component :is="leadingIcon" /></el-icon>
          <div class="yzh-titled-card__texts">
            <div class="yzh-titled-card__title">{{ title }}</div>
            <div v-if="subtitle" class="yzh-titled-card__subtitle">{{ subtitle }}</div>
          </div>
        </div>
        <div v-if="$slots.actions" class="yzh-titled-card__actions">
          <slot name="actions" />
        </div>
      </div>
      <div class="yzh-titled-card__body">
        <slot />
      </div>
    </div>
  </YzhBaseCard>
</template>

<script setup>
/**
 * YzhTitledCard —— 带标题卡片（对齐 vidlang TitledCard）
 * 在 YzhBaseCard 基础上增加标题行（title + subtitle + leadingIcon + actions 插槽）
 */
import { defineProps } from 'vue'
import YzhBaseCard from './YzhBaseCard.vue'

defineProps({
  title: { type: String, required: true },
  subtitle: { type: String, default: '' },
  leadingIcon: { type: Object, default: null },
  variant: { type: String, default: 'default' },
  padding: { type: String, default: 'var(--yzh-space-4)' },
  margin: { type: String, default: '' },
  borderRadius: { type: String, default: 'var(--yzh-radius-sm)' },
  backgroundColor: { type: String, default: '' },
  showBorder: { type: Boolean, default: false },
  shadow: { type: String, default: '' }
})
</script>

<style scoped>
.yzh-titled-card__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--yzh-space-3, 12px);
  margin-bottom: var(--yzh-space-4, 16px);
  padding-bottom: var(--yzh-space-3, 12px);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
}

.yzh-titled-card__heading {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  min-width: 0;
}

.yzh-titled-card__icon {
  font-size: var(--yzh-font-size-lg, 16px);
  color: var(--yzh-color-text-secondary, #909399);
  flex-shrink: 0;
}

.yzh-titled-card__texts {
  min-width: 0;
}

.yzh-titled-card__title {
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: var(--yzh-font-weight-bold, 600);
  color: var(--yzh-color-text-primary, #303133);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.yzh-titled-card__subtitle {
  margin-top: 2px;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #909399);
}

.yzh-titled-card__actions {
  flex-shrink: 0;
}
</style>
