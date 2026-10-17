<script setup lang="ts">
/**
 * 队列监控（后台端 · 路由 `/business/queue-monitor`）
 *
 * 2026-10-17：文件队列监控面板已上移到 cert-share 的 `QueueFileMonitor`，
 * 与专家端 `/queue`「文件队列」Tab 共用同一份实现（消除双份近似重复代码）。
 */
import { YzhPageLayout } from '@yzh-core'
import QueueFileMonitor from '@share/components/workflow/QueueFileMonitor.vue'
</script>

<template>
  <YzhPageLayout title="队列监控">
    <QueueFileMonitor
      default-type="file_convert"
      :type-options="[
        { label: '文件转换', value: 'file_convert' },
        { label: '内容提取', value: 'doc_extract' },
        { label: '全部类型', value: '' },
      ]"
    />
  </YzhPageLayout>
</template>

<style scoped lang="less">
/* 内容区改为纵向 flex：让 QueueFileMonitor 占满剩余高度，仅表体内部滚动 */
:deep(.yzh-page-layout__content) {
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
</style>
