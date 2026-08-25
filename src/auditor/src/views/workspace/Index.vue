<script setup lang="ts">
import { NCard, NStatistic, NGrid, NGi, NSpace, NText } from 'naive-ui'
</script>

<template>
  <NSpace vertical size="large">
    <NCard title="审核工作台">
      <NText depth="2">
        欢迎使用审核员端。当前为基础框架占位页面，业务模块（任务分派、资料预审、复核）接入中。
      </NText>
    </NCard>
    <NGrid :cols="3" :x-gap="16" responsive="screen">
      <NGi>
        <NCard>
          <NStatistic label="待审核任务" value="0" />
        </NCard>
      </NGi>
      <NGi>
        <NCard>
          <NStatistic label="进行中" value="0" />
        </NCard>
      </NGi>
      <NGi>
        <NCard>
          <NStatistic label="已完成" value="0" />
        </NCard>
      </NGi>
    </NGrid>
  </NSpace>
</template>
