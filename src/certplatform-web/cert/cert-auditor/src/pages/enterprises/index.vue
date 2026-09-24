<script setup lang="ts">
/**
 * 企业管理（路由 `/enterprises`）
 *
 * 规划定位：跨企业的档案总入口 —— 专家端所有业务的起点（先有企业，才有任务与文件）。
 */
import { CertPagePlaceholder } from '@share/components'
</script>

<template>
  <CertPagePlaceholder
    title="企业管理"
    desc="企业档案列表与新建向导：所有审核业务的起点。"
    :features="[
      '企业列表：按名称 / 统一社会信用代码 / 状态筛选',
      '新建企业向导：录入基本信息 → 选择认证标准 → 生成阶段（S1/S2/监督/再认证）',
      '企业详情：档案、关联文件、认证阶段进度、历史任务',
      '批量导入企业（Excel 模板）'
    ]"
  />
</template>
