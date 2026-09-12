<template>
  <el-select
    v-model="selectedIcon"
    placeholder="选择图标"
    filterable
    :disabled="disabled"
    class="icon-picker"
    popper-class="icon-picker-popper"
  >
    <template #prefix>
      <el-icon v-if="selectedIcon">
        <component :is="selectedIcon" />
      </el-icon>
    </template>
    <el-option-group v-for="group in iconGroups" :key="group.label" :label="group.label">
      <el-option
        v-for="icon in group.icons"
        :key="icon"
        :label="icon"
        :value="icon"
      >
        <span class="icon-option">
          <el-icon><component :is="icon" /></el-icon>
          <span>{{ icon }}</span>
        </span>
      </el-option>
    </el-option-group>
  </el-select>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  modelValue: string
  disabled?: boolean
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
}>()

const selectedIcon = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

const iconGroups = [
  {
    label: '常用图标',
    icons: ['Plus', 'Delete', 'Edit', 'Search', 'Check', 'Close', 'Setting', 'Refresh']
  },
  {
    label: '方向图标',
    icons: ['ArrowUp', 'ArrowRight', 'ArrowDown', 'ArrowLeft', 'Top', 'Right', 'Bottom', 'Left']
  },
  {
    label: '导航图标',
    icons: ['Menu', 'Expand', 'Fold', 'FullScreen', 'FullScreenExit', 'RefreshRight']
  },
  {
    label: '数据图标',
    icons: ['Document', 'DocumentCopy', 'DataBoard', 'DataAnalysis', 'PieChart', 'Histogram']
  },
  {
    label: '用户图标',
    icons: ['User', 'UserFilled', 'Avatar', 'Lock', 'Key', 'OfficeBuilding']
  },
  {
    label: '商品图标',
    icons: ['Goods', 'GoodsFilled', 'ShoppingCart', 'ShoppingCard', 'ShoppingBag']
  },
  {
    label: '文件图标',
    icons: ['Folder', 'FolderOpened', 'Files', 'Paperclip', 'Notebook']
  },
  {
    label: '消息图标',
    icons: ['Bell', 'BellFilled', 'Message', 'MessageBox', 'ChatDotRound']
  }
]
</script>

<style scoped>
.icon-option {
  display: flex;
  align-items: center;
  gap: 8px;
}

.icon-picker :deep(.el-select__input-wrapper) {
  display: none;
}
</style>
