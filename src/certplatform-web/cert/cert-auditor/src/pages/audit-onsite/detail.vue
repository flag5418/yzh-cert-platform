<script setup lang="ts">
/**
 * 认证检查 · 详情（路由 /audit-onsite/:code）
 *
 * 一个计划下该分支的全部检查项。检查分支：判定结论 + 严重程度 + 逐条签发 NC，
 * 审核员只读（分工在准备期已确定）。
 */
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import CheckDetailPage from '@/components/check/CheckDetailPage.vue'

const route = useRoute()
const planCode = computed(() => String(route.params.code ?? ''))
</script>

<template>
  <CheckDetailPage v-if="planCode" stage-kind="onsite" :plan-code="planCode" />
</template>
