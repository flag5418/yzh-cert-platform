<script setup lang="ts">
/**
 * YzhToolbar - 通用工具栏
 * 用法：
 *   <YzhToolbar>
 *     <template #left>
 *       <el-button>新增</el-button>
 *     </template>
 *     <template #right>
 *       <el-button text>刷新</el-button>
 *     </template>
 *   </YzhToolbar>
 */
</script>

<template>
  <div class="yzh-toolbar">
    <div class="yzh-toolbar__left">
      <slot name="left" />
    </div>
    <div class="yzh-toolbar__right">
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

.yzh-toolbar__right {
  display: flex;
  align-items: center;
  gap: 4px;
}
</style>
