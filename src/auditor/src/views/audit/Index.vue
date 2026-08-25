<script setup lang="ts">
import { useRoute } from 'vue-router'
import { NCard, NTag, NEmpty } from 'naive-ui'

const route = useRoute()
const taskId = route.params.taskId as string
</script>

<template>
  <NCard title="审核详情">
    <template #header-extra>
      <NTag type="info">任务 {{ taskId }}</NTag>
    </template>
    <NEmpty description="审核详情页为基础框架占位，业务逻辑待接入" />
  </NCard>
</template>
