<script setup lang="ts">
/**
 * 认证准备 · 列表（MENU_AUD_21 · 路由 /audit-prep）
 *
 * 由「认证计划」派生的执行实例列表。产出是**入场计划**，不签发 NC。
 * 主体在 `components/check/StageListPage.vue`（与认证检查共用），
 * 这里只做分支参数绑定 —— 保证两个菜单的交互完全一致。
 */
import StageListPage from '@/components/check/StageListPage.vue'
</script>

<template>
  <StageListPage stage-kind="prep" />
</template>
