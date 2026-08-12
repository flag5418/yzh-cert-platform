<template>
  <div class="yzh-base-card" :class="[`is-${variant}`, { 'is-border': showBorder }]" :style="cardStyle">
    <slot />
  </div>
</template>

<script setup>
/**
 * YzhBaseCard —— 基础卡片（对齐 vidlang BaseCard）
 * 变体：default（轻阴影）/ outlined（描边）/ elevated（悬浮阴影）/ filled（实色背景）
 * 样式全部取自 --yzh-* 令牌，禁止硬编码色值/间距/圆角
 */
import { computed } from 'vue'

const props = defineProps({
  variant: { type: String, default: 'default' }, // default | outlined | elevated | filled
  padding: { type: String, default: 'var(--yzh-space-4)' },
  margin: { type: String, default: '' },
  borderRadius: { type: String, default: 'var(--yzh-radius-sm)' },
  backgroundColor: { type: String, default: '' },
  showBorder: { type: Boolean, default: false },
  shadow: { type: String, default: '' }
})

const cardStyle = computed(() => {
  const style = {
    padding: props.padding,
    margin: props.margin,
    borderRadius: props.borderRadius
  }
  if (props.backgroundColor) style.backgroundColor = props.backgroundColor
  if (props.shadow) style.boxShadow = props.shadow
  return style
})
</script>

<style scoped>
.yzh-base-card {
  background: var(--yzh-color-bg-card, #fff);
  box-sizing: border-box;
  transition: box-shadow var(--yzh-duration-normal, 0.3s), border-color var(--yzh-duration-normal, 0.3s);
}

/* default：轻阴影 */
.yzh-base-card.is-default {
  box-shadow: var(--yzh-shadow-sm, 0 1px 4px rgba(0, 0, 0, 0.04));
}

/* outlined：描边卡片，无阴影 */
.yzh-base-card.is-outlined {
  border: 1px solid var(--yzh-color-border, #e4e7ed);
  box-shadow: none;
}

/* elevated：双层柔和阴影 */
.yzh-base-card.is-elevated {
  box-shadow: var(--yzh-shadow-md, 0 2px 12px rgba(0, 0, 0, 0.06));
}

/* filled：实色背景（由调用方传 backgroundColor） */
.yzh-base-card.is-filled {
  box-shadow: none;
}

/* 边框开关（default/elevated 场景按需加边框） */
.yzh-base-card.is-border {
  border: 1px solid var(--yzh-color-border, #e4e7ed);
}
</style>
