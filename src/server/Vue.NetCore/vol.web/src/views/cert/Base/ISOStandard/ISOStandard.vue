<!--
  ISO 标准注册 —— YZH V2.0 落地页
  基于 YzhCrudTable 组件，零 Vol 依赖

  改造说明（2026-08-07）：
  - 从机构从表改造为全局独立基础资料
  - 移除 CbCode 字段，不再属于某个认证机构
  - 机构和标准的关系通过 cert_org_standard 关联表管理
-->
<template>
  <YzhCrudTable
    ref="crudTable"
    :schema="schema"
    :options="viewOptions"
    :lifecycles="lifecycles"
    page-key="ISOStandard"
    :incremental-update="true"
    :search-mode="'fixed'"
  >
    <!-- 头部提示 -->
    <template #gridHeader>
      <el-alert
        title="ISO 标准注册：维护全局 ISO 标准基础资料（如 ISO 9001、ISO 13485 等）"
        type="info"
        :closable="false"
        show-icon
        style="margin-bottom: 10px"
      />
    </template>
  </YzhCrudTable>
</template>

<script setup lang="ts">
import { ref, markRaw } from 'vue'
import { YzhCrudTable } from '@/yzh/index'
import viewOptions from './options.js'

const crudTable = ref()

// —— ① 实体 Schema ——
const schema = Object.freeze({
  keyField: 'Id',
  keyType: 'number',
  defaultSortField: 'CreateDate',
  defaultSortOrder: 'desc',
  controllerName: 'ISOStandard',
  tableName: 'cert_iso_standard',
  statusTagColors: { Status: 'standard_status' },
})

// —— ② 生命周期钩子（只写有业务逻辑的）——
const lifecycles = markRaw({
  // 新增保存前：自动设置默认值
  onAddSaveBefore(main: any) {
    if (!main.StandardCode) {
      main.StandardCode = `STD-${Date.now().toString(36).toUpperCase()}`
    }
    return true
  },
})
</script>

<style lang="less" scoped>
.el-alert {
  border-radius: 4px;
}
</style>
