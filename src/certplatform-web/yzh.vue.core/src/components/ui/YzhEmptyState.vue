<template>
  <div class="yzh-empty-state" :class="{ 'is-compact': compact, 'is-icon-bg': iconBackgroundColor }">
    <div class="yzh-empty-state__inner">
      <div v-if="iconBackgroundColor" class="yzh-empty-state__icon-wrap" :style="{ backgroundColor: iconBackgroundColor }">
        <el-icon class="yzh-empty-state__icon" :style="{ fontSize: iconSize + 'px', color: iconColor }">
          <component :is="icon" />
        </el-icon>
      </div>
      <el-icon v-else class="yzh-empty-state__icon" :style="{ fontSize: iconSize + 'px', color: iconColor }">
        <component :is="icon" />
      </el-icon>

      <div class="yzh-empty-state__title">{{ title }}</div>
      <div v-if="description" class="yzh-empty-state__description">{{ description }}</div>

      <div v-if="actionLabel && onAction" class="yzh-empty-state__action">
        <slot name="action">
          <el-button size="small" @click="onAction">{{ actionLabel }}</el-button>
        </slot>
      </div>
    </div>
  </div>
</template>

<script setup>
/**
 * YzhEmptyState —— 空状态组件（对齐 vidlang EmptyState）
 * 三种模式：
 *   - 默认：居中显示（全屏空态）
 *   - compact：无居中包裹（列表内部）
 *   - iconBackgroundColor 传入：图标外层圆形色底
 */
defineProps({
  icon: { type: Object, required: true },
  title: { type: String, required: true },
  description: { type: String, default: '' },
  actionLabel: { type: String, default: '' },
  onAction: { type: Function, default: null },
  compact: { type: Boolean, default: false },
  iconSize: { type: Number, default: 48 },
  iconColor: { type: String, default: 'var(--yzh-color-text-secondary)' },
  iconBackgroundColor: { type: String, default: '' },
  iconBackgroundPadding: { type: String, default: '20px' }
})
</script>

<style scoped>
.yzh-empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: var(--yzh-space-10, 40px) var(--yzh-space-8, 32px);
  box-sizing: border-box;
}

.yzh-empty-state.is-compact {
  display: block;
  padding: var(--yzh-space-8, 32px) 0;
}

.yzh-empty-state__inner {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: var(--yzh-space-3, 12px);
}

.yzh-empty-state__icon-wrap {
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  padding: 20px;
}

.yzh-empty-state__icon {
  color: var(--yzh-color-text-secondary, #909399);
}

.yzh-empty-state__title {
  font-size: var(--yzh-font-size-md, 14px);
  color: var(--yzh-color-text-regular, #606266);
}

.yzh-empty-state__description {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #909399);
}

.yzh-empty-state__action {
  margin-top: var(--yzh-space-2, 8px);
}
</style>
