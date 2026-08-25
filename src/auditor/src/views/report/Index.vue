<script setup lang="ts">
import { useRoute } from 'vue-router'
import { NCard, NTag, NEmpty } from 'naive-ui'

const route = useRoute()
const taskId = route.params.taskId as string
</script>

<template>
  <NCard title="审核报告">
    <template #header-extra>
      <NTag type="success">任务 {{ taskId }}</NTag>
    </template>
    <NEmpty description="审核报告页为基础框架占位，报告生成与导出待接入" />
  </NCard>
</template>
