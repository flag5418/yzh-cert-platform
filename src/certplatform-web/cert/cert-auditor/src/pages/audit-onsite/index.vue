<script setup lang="ts">
/**
 * 认证检查 · 列表（MENU_AUD_22 · 路由 /audit-onsite）
 *
 * 由「认证计划」派生的执行实例列表。产出是**认证报告**，NC 逐条签发。
 * 主体在 `components/check/StageListPage.vue`（与认证准备共用）。
 */
import StageListPage from '@/components/check/StageListPage.vue'
</script>

<template>
  <StageListPage stage-kind="onsite" />
</template>
