<script setup lang="ts">
/**
 * 页面占位组件 —— 菜单已建立、业务页尚未实现的过渡态
 *
 * 用途：保证侧边栏菜单点击**不 404、不白屏**，同时向使用者说明该页面的规划范围。
 * 实现真实页面时，替换页面内容并移除本组件引用即可，无需改动路由与菜单。
 */
defineProps<{
  /** 页面标题（与菜单名保持一致） */
  title: string
  /** 规划说明：该页面将要承载什么 */
  desc?: string
  /** 计划承载的功能点 */
  features?: string[]
}>()
</script>

<template>
  <el-card shadow="never" class="cert-page-placeholder">
    <template #header>
      <span class="cert-page-placeholder__title">{{ title }}</span>
    </template>

    <el-empty :image-size="90">
      <template #description>
        <div class="cert-page-placeholder__desc">
          <p class="cert-page-placeholder__lead">{{ desc || '该功能尚未实现，页面已就位。' }}</p>
          <ul v-if="features && features.length" class="cert-page-placeholder__list">
            <li v-for="f in features" :key="f">{{ f }}</li>
          </ul>
        </div>
      </template>
    </el-empty>
  </el-card>
</template>

<style scoped>
.cert-page-placeholder__title {
  font-weight: 600;
}
.cert-page-placeholder__desc {
  color: var(--el-text-color-regular);
  line-height: 1.8;
}
.cert-page-placeholder__lead {
  margin: 0;
}
.cert-page-placeholder__list {
  margin: 10px 0 0;
  padding-left: 18px;
  text-align: left;
}
.cert-page-placeholder__list li {
  list-style: disc;
}
</style>
