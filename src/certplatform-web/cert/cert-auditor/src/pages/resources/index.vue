<script setup lang="ts">
/**
 * 资料库（路由 `/resources`）
 *
 * 规划定位：跨企业的资料检索 —— 不依附于某一家企业，方便专家按「文件类型 / 标准条款」横向找料。
 * 与「企业详情 → 文件」的区别：那里是按企业纵向看，这里按资料维度横向看。
 */
import { CertPagePlaceholder } from '@share/components'
</script>

<template>
  <CertPagePlaceholder
    title="资料库"
    desc="跨企业资料检索与目录模板管理。"
    :features="[
      '跨企业检索：按文件名 / 文件类型 / 所属标准条款 / 上传时间',
      '目录模板：标准化的企业资料目录结构（按标准 + 阶段）',
      '文件预览：在线预览 PDF / Word / Excel',
      '提取结果回看：查看某份文件已提取出的字段与表格'
    ]"
  />
</template>
