<script setup lang="ts">
/**
 * 系统一览（专家端落地页，路由 `/overview`）
 *
 * 规划定位：驾驶舱 —— 一眼看清「我这边现在什么状态、下一步该做什么」。
 * 与「任务中心」的区别：本页看**全局汇总**，任务中心看**逐条明细**。
 */
import { CertPagePlaceholder } from '@share/components'
</script>

<template>
  <CertPagePlaceholder
    title="系统一览"
    desc="专家端驾驶舱：汇总当前工作区的整体状态与待办。"
    :features="[
      '能力就绪度：认证标准 / 阶段 / 规则库 / 模板 的配置完成情况',
      '待办汇总：待分配、待审核、待生成报告的任务数量',
      '最近动态：最近上传、最近提取、最近 NC 与报告',
      '用量概览：AI 调用次数与费用（对接 api/ai-usage/balance）'
    ]"
  />
</template>
