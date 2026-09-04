<!--
  认证阶段关联 —— YZH V2.0 落地页
  基于 YzhCrudTable 组件，零 Vol 依赖

  基于 ISO/IEC 17021-1:2015 规定的认证流程阶段
  9 个标准阶段：申请受理 → 合同评审 → 审核方案策划 →
    第一阶段审核 → 第二阶段审核 → 认证决定 → 颁发证书 → 监督审核 → 再认证
-->
<template>
  <YzhCrudTable
    ref="crudTable"
    :schema="schema"
    :options="viewOptions"
    :lifecycles="lifecycles"
    page-key="CertStage"
    :incremental-update="true"
    :search-mode="'fixed'"
  >
    <!-- 头部提示 -->
    <template #gridHeader>
      <el-alert
        title="认证阶段管理：配置全局认证流程阶段（基于 ISO/IEC 17021-1:2015）"
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
  defaultSortField: 'SortOrder',
  defaultSortOrder: 'asc',
  controllerName: 'CertStage',
  tableName: 'cert_cert_stage',
  statusTagColors: { Status: 'stage_status' },
})

// —— ② 生命周期钩子（只写有业务逻辑的）——
const lifecycles = markRaw({
  // 新增保存前：自动生成 StageCode
  onAddSaveBefore(main: any) {
    if (!main.StageCode) {
      main.StageCode = `STAGE-${Date.now().toString(36).toUpperCase()}`
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
