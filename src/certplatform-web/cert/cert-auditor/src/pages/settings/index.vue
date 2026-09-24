<script setup lang="ts">
/**
 * 系统设置（路由 `/settings`）
 *
 * 规划定位：个人与工作区级设置。按「Tab / 子路由」承载多块内容 ——
 * 侧边栏只渲染 2 层菜单，更深层级必须由页面内 Tab 承担（见 `项目全局规则.md` §16.10 相关约定）。
 */
import { CertPagePlaceholder } from '@share/components'
</script>

<template>
  <CertPagePlaceholder
    title="系统设置"
    desc="个人与工作区级设置（页内按 Tab 分组）。"
    :features="[
      '个人信息：姓名、邮箱、电话、头像、修改密码',
      '账户与用量：套餐 / 配额 / AI 调用费用明细',
      '消息设置：站内信与邮件通知开关',
      '操作日志：本人及工作区内的关键操作记录'
    ]"
  />
</template>
