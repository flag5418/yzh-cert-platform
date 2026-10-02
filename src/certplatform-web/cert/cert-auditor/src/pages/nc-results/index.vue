<script setup lang="ts">
/**
 * NC 检查结果（专家端 · 路由 `/nc-results` · 菜单 `MENU_AUD_08`）
 *
 * 本页是**薄壳**：真正的左树右表 + 审批 + 修改 + 导出在
 * `@share/components/auditor/ExpertResultPanel.vue`（与「报告结论」共用）。
 *
 * 用户裁决（2026-09-30）：
 *   · 左侧 = 企业 + 阶段 + 任务；右侧 = 该任务下的 NC 检查结论列表
 *   · 审核员可**勾选批量审批**、**单行直接修改**、**整体列表导出**
 *   · ⛔ 点企业 / 阶段节点 → 右表留空（必须选到任务）
 */
import ExpertResultPanel from '@share/components/auditor/ExpertResultPanel.vue'
</script>

<template>
  <ExpertResultPanel type="nc" />
</template>
