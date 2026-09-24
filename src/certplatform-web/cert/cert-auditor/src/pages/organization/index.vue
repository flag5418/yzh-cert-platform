<script setup lang="ts">
/**
 * 组织与成员（路由 `/organization`）
 *
 * 规划定位：一个注册账号 = 一个机构工作区，本页管理工作区下的人与角色。
 */
import { CertPagePlaceholder } from '@share/components'
</script>

<template>
  <CertPagePlaceholder
    title="组织与成员"
    desc="成员管理与角色分组：控制谁能看到什么、能做什么。"
    :features="[
      '成员列表：邀请 / 停用 / 移除成员',
      '角色分组：按角色（管理员 / 审核员 / 只读）分配功能权限',
      '角色-菜单分配：控制各角色可见的侧边栏菜单',
      '机构信息：名称、简称、联系方式、Logo 等'
    ]"
  />
</template>
