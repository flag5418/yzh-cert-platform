<template>
  <!-- compact 变体：面板/表格内轻量行（无插画） -->
  <div v-if="compact" class="yzh-empty-state yzh-empty-state--compact">
    <div class="yzh-empty-state__inner">
      <span v-if="icon" class="yzh-empty-state__icon-wrap" :style="iconWrapStyle">
        <el-icon class="yzh-empty-state__icon" :style="iconStyle"><component :is="icon" /></el-icon>
      </span>
      <div class="yzh-empty-state__text">
        <div class="yzh-empty-state__title">{{ title }}</div>
        <!-- 富内容空态（多段说明 / 列表）走 #description；纯文本仍用 description prop -->
        <div v-if="$slots.description || description" class="yzh-empty-state__description">
          <slot name="description">{{ description }}</slot>
        </div>
      </div>
      <!-- #action 可独立使用（自带按钮 disabled/权限），不必强绑 actionLabel+onAction -->
      <div v-if="$slots.action || (actionLabel && onAction)" class="yzh-empty-state__action">
        <slot name="action">
          <el-button type="primary" size="small" @click="onAction">{{ actionLabel }}</el-button>
        </slot>
      </div>
    </div>
  </div>

  <!--
    标准变体：el-empty 打底（EP 官方插画 / 主题变量 / 间距）
    图像三级优先：#image 插槽 > image prop > icon 语义图标 > EP 内置插画
    （resolvedImage = 有 #image 插槽时不传 image，保证插槽优先于 prop）
  -->
  <el-empty v-else class="yzh-empty-state" :image="$slots.image ? undefined : image" :image-size="imageSize">
    <template #image>
      <slot name="image">
        <span v-if="icon" class="yzh-empty-state__icon-wrap" :style="iconWrapStyle">
          <el-icon class="yzh-empty-state__icon" :style="iconStyle"><component :is="icon" /></el-icon>
        </span>
      </slot>
    </template>
    <template #description>
      <div class="yzh-empty-state__text">
        <div class="yzh-empty-state__title">{{ title }}</div>
        <div v-if="$slots.description || description" class="yzh-empty-state__description">
          <slot name="description">{{ description }}</slot>
        </div>
      </div>
      <div v-if="$slots.action || (actionLabel && onAction)" class="yzh-empty-state__action">
        <slot name="action">
          <el-button type="primary" size="small" @click="onAction">{{ actionLabel }}</el-button>
        </slot>
      </div>
    </template>
  </el-empty>
</template>

<script setup lang="ts">
import { computed } from 'vue'

/**
 * YzhEmptyState —— 空态唯一组件（S05），底层基于 el-empty（EP 官方插画/主题）
 *
 * 两种变体，同一套字级/间距令牌：
 *   - 标准：el-empty 打底（padding 40px 0 · 图区 160px · 图→文 20px，均为 EP 默认）
 *   - compact：无插画轻量行（面板/表格内窄空态）
 *
 * 图像三级优先：#image 插槽 > image prop > icon（语义提示，默认柔和圆底）> EP 内置插画
 *   - data 类空态（暂无数据）→ 不传 icon，得 EP 插画；特殊业务传 image 换图
 *   - hint 类空态（请先选择）→ 传 icon（Pointer/InfoFilled…）
 */
const props = defineProps({
  icon: { type: Object, default: null },
  title: { type: String, required: true },
  description: { type: String, default: '' },
  actionLabel: { type: String, default: '' },
  onAction: { type: Function, default: null },
  compact: { type: Boolean, default: false },
  iconSize: { type: Number, default: 48 },
  iconColor: { type: String, default: 'var(--yzh-color-text-secondary)' },
  /** 覆盖 icon 圆底颜色；不传时标准变体走 --yzh-color-bg-muted 默认圆底，compact 不上底 */
  iconBackgroundColor: { type: String, default: '' },
  /** EP 透传：自定义空态图（优先级低于 #image 插槽、高于 icon） */
  image: { type: String, default: '' },
  /** EP 透传：图像宽度（缺省用 EP 默认 160px） */
  imageSize: { type: Number, default: undefined },
})

const iconStyle = computed(() => ({ fontSize: `${props.iconSize}px`, color: props.iconColor }))
const iconWrapStyle = computed(() =>
  props.iconBackgroundColor ? { backgroundColor: props.iconBackgroundColor } : undefined,
)
</script>

<style scoped>
.yzh-empty-state--compact {
  display: block;
  padding: var(--yzh-space-8, 32px) 0;
}

.yzh-empty-state--compact .yzh-empty-state__inner {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: var(--yzh-space-3, 12px);
}

.yzh-empty-state__icon-wrap {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
}

/* 标准变体：icon 主体给柔和圆底（与插画同视觉重量），可被 iconBackgroundColor 覆盖 */
.yzh-empty-state:not(.yzh-empty-state--compact) .yzh-empty-state__icon-wrap {
  background-color: var(--yzh-color-bg-muted, #f3f4f6);
  padding: var(--yzh-space-5, 20px);
}

.yzh-empty-state__icon {
  color: var(--yzh-color-text-secondary, #606266);
}

.yzh-empty-state__text {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: var(--yzh-space-1, 4px);
}

.yzh-empty-state__title {
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: 500;
  color: var(--yzh-color-text-primary, #303133);
}

.yzh-empty-state__description {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
}

/* 标准变体 20px（EP 图→文节奏的延续）；compact 由 gap 承担 */
.yzh-empty-state__action {
  margin-top: var(--yzh-space-5, 20px);
}

.yzh-empty-state--compact .yzh-empty-state__action {
  margin-top: 0;
}
</style>
