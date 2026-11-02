<script setup lang="ts">
/**
 * 认证准备 · 详情（路由 /audit-prep/:code）
 *
 * 一个计划下该分支的全部检查项。准备分支：资料状态 + 疑点 + 可定义审核员，
 * **不签发 NC**（只有认证检查能签发）。
 */
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import CheckDetailPage from '@/components/check/CheckDetailPage.vue'

const route = useRoute()
const planCode = computed(() => String(route.params.code ?? ''))
</script>

<template>
  <CheckDetailPage v-if="planCode" stage-kind="prep" :plan-code="planCode" />
</template>
