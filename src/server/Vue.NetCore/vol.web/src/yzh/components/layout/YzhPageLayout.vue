<script setup lang="ts">
/**
 * YzhPageLayout - 标准页面布局基类
 *
 * 统一所有单页面的整体结构：
 * ┌────────────────────────────────────────────────┐
 * │ 标题栏（页面标题 + 面包屑）                       │  ← #title
 * ├────────────────────────────────────────────────┤
 * │ 搜索栏（查询条件 grid 布局）                      │  ← #search
 * ├────────────────────────────────────────────────┤
 * │ 控制栏（左侧：新增/批量删除  右侧：刷新/列设置）    │  ← #toolbar
 * ├────────────────────────────────────────────────┤
 * │                                                │
 * │ 表格区域                                        │  ← #table（默认插槽）
 * │                                                │
 * ├────────────────────────────────────────────────┤
 * │ 分页                                            │  ← #pagination
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
    <!-- 标题栏 -->
    <div v-if="showTitle || pageTitle || helpText" class="yzh-page-layout__header">
      <slot name="title">
        <div class="yzh-page-layout__title-bar">
          <h2 class="yzh-page-layout__title">{{ pageTitle }}</h2>
          <span v-if="helpText" class="yzh-page-layout__help">{{ helpText }}</span>
        </div>
      </slot>
    </div>

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

  /* === 标题栏 === */
  &__header {
    flex-shrink: 0;
    padding: 16px 20px;
    background: #fff;
    border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
  }

  &__title-bar {
    display: flex;
    align-items: baseline;
    gap: 12px;
  }

  &__title {
    margin: 0;
    font-size: 18px;
    font-weight: 600;
    color: var(--yzh-color-text-primary, #303133);
    line-height: 1.4;
  }

  &__help {
    font-size: 13px;
    color: var(--yzh-color-text-secondary, #909399);
  }

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
    overflow: auto;
    padding: 16px 20px;
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
