<script setup lang="ts">
/**
 * YzhPageLayout - 标准页面布局基类
 *
 * 统一所有单页面的整体结构：
 * ┌────────────────────────────────────────────────┐
 * │  [全局 padding: 16px 20px]                      │
 * ├────────────────────────────────────────────────┤
 * │  搜索栏（左侧查询条件 Grid，右侧按钮）           │  ← #search
 * ├────────────────────────────────────────────────┤
 * │  控制栏（左侧：操作按钮  右侧：列设置）          │  ← #toolbar
 * ├────────────────────────────────────────────────┤
 * │                                                │
 * │  表格区域（弹性填充剩余空间）                     │  ← #table（默认插槽）
 * │                                                │
 * ├────────────────────────────────────────────────┤
 * │  分页（置底显示）                                │  ← #pagination
 * └────────────────────────────────────────────────┘
 */
defineProps<{
  /** 页面标题（显示在左上角） */
  pageTitle?: string
  /** 标题右侧的帮助说明 */
  helpText?: string
  /** 是否显示 pageTitle 行 */
  showTitle?: boolean
}>()
</script>

<template>
  <div class="yzh-page-layout">
    <!-- 搜索栏 -->
    <div v-if="$slots.search" class="yzh-page-layout__search">
      <slot name="search" />
    </div>

    <!-- 控制栏 -->
    <div class="yzh-page-layout__toolbar">
      <slot name="toolbar">
        <div class="yzh-page-layout__toolbar-left">
          <slot name="toolbar-left" />
        </div>
        <div class="yzh-page-layout__toolbar-right">
          <slot name="toolbar-right" />
        </div>
      </slot>
    </div>

    <!-- 表格/内容区 -->
    <div class="yzh-page-layout__content">
      <slot />
    </div>

    <!-- 分页栏 -->
    <div v-if="$slots.pagination" class="yzh-page-layout__footer">
      <slot name="pagination" />
    </div>
  </div>
</template>

<style lang="less" scoped>
/* 页面布局基类 - 统一风格，所有单页面均以此结构为基础 */

.yzh-page-layout {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: var(--yzh-color-bg-page, #f5f7fa);

  /* === 搜索栏 === */
  &__search {
    flex-shrink: 0;
    padding: 16px 20px;
    background: #fff;
    border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
  }

  /* === 控制栏 === */
  &__toolbar {
    flex-shrink: 0;
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 12px 20px;
    background: #fff;
    border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
    gap: 12px;
  }

  &__toolbar-left,
  &__toolbar-right {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  &__toolbar-left {
    flex-wrap: wrap;
  }

  &__toolbar-right {
    flex-wrap: nowrap;
  }

  /* === 内容区 === */
  &__content {
    flex: 1;
    min-height: 0;
    overflow: hidden;
    padding: 16px 20px;
    background: var(--yzh-color-bg-page, #f5f7fa);
  }

  /* === 底部栏（分页） === */
  &__footer {
    flex-shrink: 0;
    padding: 12px 20px;
    background: #fff;
    border-top: 1px solid var(--yzh-color-border-light, #ebeef5);
    display: flex;
    justify-content: flex-end;
  }
}
</style>
