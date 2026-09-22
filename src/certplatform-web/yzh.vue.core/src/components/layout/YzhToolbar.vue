<script setup lang="ts">
/**
 * YzhToolbar - 通用工具栏（原子组件，零领域依赖）
 *
 * 两种用法（可共存）：
 * 1. 声明式（C-B1）：传 buttons: YzhAction[] + 监听 @action(key, action)
 *    <YzhToolbar :buttons="[{ key: 'add', text: '新增', type: 'primary' }]" @action="..." />
 * 2. 插槽式（保留）：#left / #right 自由定制
 */
import type { YzhAction } from '../table/types'

withDefaults(
  defineProps<{
    /** 声明式按钮（group 默认 left；group='right' 渲染到右侧） */
    buttons?: YzhAction[]
  }>(),
  { buttons: () => [] }
)

const emit = defineEmits<{
  (e: 'action', key: string, action: YzhAction): void
}>()

function onClick(action: YzhAction) {
  if (action.disabled) return
  emit('action', action.key, action)
}
</script>

<template>
  <div class="yzh-toolbar">
    <div class="yzh-toolbar__left">
      <template v-for="action in buttons.filter((b) => b.group !== 'right')" :key="action.key">
        <el-button
          :type="action.type ?? 'default'"
          :disabled="action.disabled"
          @click="onClick(action)"
        >
          {{ action.text }}
        </el-button>
      </template>
      <slot name="left" />
    </div>
    <div class="yzh-toolbar__right">
      <template v-for="action in buttons.filter((b) => b.group === 'right')" :key="action.key">
        <el-button
          :type="action.type ?? 'default'"
          :disabled="action.disabled"
          @click="onClick(action)"
        >
          {{ action.text }}
        </el-button>
      </template>
      <slot name="right" />
    </div>
  </div>
</template>

<style scoped>
.yzh-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 20px;
  background: #fff;
  border-bottom: 1px solid var(--yzh-color-border-light, #f1f5f9);
  min-height: 56px;
}

.yzh-toolbar__left {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.yzh-toolbar__left:empty,
.yzh-toolbar__right:empty {
  display: none;
}

.yzh-toolbar__right {
  display: flex;
  align-items: center;
  gap: 4px;
}
</style>
