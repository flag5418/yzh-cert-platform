<script setup lang="ts">
/**
 * YzhIcon - 统一图标组件
 * 支持 Element Plus 图标和 Bootstrap Icons
 * 所有图标通过此组件渲染，便于统一管理
 */
import { computed } from 'vue'

const props = defineProps({
  /**
   * 图标名称
   * - Element Plus 图标：传入组件名，如 'Plus', 'Search', 'Setting'
   * - Bootstrap Icons：传入类名，如 'bi-plus-lg', 'bi-search'
   * - 自动识别：以 'bi-' 开头则为 Bootstrap Icons
   */
  name: {
    type: String,
    required: true
  },
  /**
   * 图标大小
   */
  size: {
    type: [String, Number],
    default: 'default'
  },
  /**
   * 图标颜色
   */
  color: {
    type: String,
    default: ''
  },
  /**
   * 额外类名
   */
  class: {
    type: String,
    default: ''
  }
})

const isBootstrapIcon = computed(() => {
  return props.name.startsWith('bi-')
})

const iconClass = computed(() => {
  const classes = ['yzh-icon']
  if (isBootstrapIcon.value) {
    classes.push(props.name)
  }
  if (props.class) {
    classes.push(props.class)
  }
  return classes.join(' ')
})

const iconStyle = computed(() => {
  const style: Record<string, string> = {}
  if (props.color) {
    style.color = props.color
  }
  if (props.size) {
    style.fontSize = typeof props.size === 'number' ? `${props.size}px` : props.size
  }
  return style
})
</script>

<template>
  <!-- Element Plus 图标组件 -->
  <component
    v-if="!isBootstrapIcon"
    :is="name"
    :class="iconClass"
    :style="iconStyle"
  />
  
  <!-- Bootstrap Icons 字体图标 -->
  <i
    v-else
    :class="iconClass"
    :style="iconStyle"
  />
</template>

<style scoped>
.yzh-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: inherit;
  line-height: 1;
}
</style>
