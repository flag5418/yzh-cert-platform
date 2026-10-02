<script setup lang="ts">
/**
 * 报告结论（专家端 · 路由 `/report-results` · 菜单 `MENU_AUD_09`）
 *
 * 本页是**薄壳**：真正的左树右表 + 审批 + 修改 + 导出在
 * `@share/components/auditor/ExpertResultPanel.vue`（与「NC 检查结果」共用）。
 *
 * ★ 与 NC 页的三点差异（都在面板内部按 `type` 分支）：
 *   ① 结论列换成「生成方式」（自动生成 / 引用上轮结果 / 人工撰写）
 *   ② 修改弹窗改的是**章节正文**（纯文本，D19），没有「判定 / 严重度」
 *   ③ 导出列头换成章节号 / 章节名称 / 生成方式
 */
import ExpertResultPanel from '@share/components/auditor/ExpertResultPanel.vue'
</script>

<template>
  <ExpertResultPanel type="report" />
</template>
