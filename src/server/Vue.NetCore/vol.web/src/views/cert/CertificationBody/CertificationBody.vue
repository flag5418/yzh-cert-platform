<!--
  认证机构管理 —— YZH V2.0 落地页
  基于 YzhCrudTable 组件，零 Vol 依赖

  设计原则：
  - 业务页面只写「差异」代码，通用逻辑全部在 YzhCrudTable 基类
  - lifecycles 只写有实际业务逻辑的钩子，空壳不写
  - schema 定义实体特征，options.js 定义 UI 配置
-->
<template>
  <YzhCrudTable
    ref="crudTable"
    :schema="schema"
    :options="viewOptions"
    :lifecycles="lifecycles"
    page-key="CertificationBody"
    :incremental-update="true"
    :search-mode="'fixed'"
  >
    <!-- 头部提示 -->
    <template #gridHeader>
      <el-alert
        title="认证机构管理：维护所有ISO认证机构基本信息（CNAS编号、联系人、状态）"
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
  keyField: 'Code',           // 使用 Code 作为业务主键（与后端 Remove 接口一致）
  keyType: 'string',
  defaultSortField: 'CreateDate',
  defaultSortOrder: 'desc',
  controllerName: 'CertCertificationBody',
  tableName: 'cert_certification_body',
  statusTagColors: { Status: 'org_status' },
})

// —— ② 生命周期钩子（只写有业务逻辑的）——
const lifecycles = markRaw({
  /**
   * 新增保存前：业务特定的默认值
   * - CbCode 未填时自动生成（此逻辑也可由后端 AddOnExecuting 钩子替代）
   * - 注：字符串字段的 null 兜底已由 YzhCrudTable 基类的 applyStringFieldDefaults 统一处理
   */
  onAddSaveBefore(main: any) {
    if (!main.CbCode) {
      main.CbCode = `CB${Date.now().toString().slice(-3)}`
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
