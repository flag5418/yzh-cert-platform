<script setup lang="ts">
import { YzhTable } from '@yzh-core/components/table'
import type { PageParams, SearchField, YzhTableColumn } from '@yzh-core/components/table/types'
import { getEnterprisePage } from '@share/api/enterprise'
import type { Enterprise } from '@share/types/cert'

const columns: YzhTableColumn<Enterprise>[] = [
  { prop: 'entCode', label: '企业编码', width: 150 },
  { prop: 'entName', label: '企业名称', minWidth: 200 },
  { prop: 'entStatus', label: '状态', width: 100, formatter: (v) => v === 1 ? '启用' : '禁用' },
  { prop: 'orgId', label: '所属机构', width: 100 }
]

const searchFields: SearchField[] = [
  { prop: 'entName', label: '企业名称', type: 'text' },
  { prop: 'entCode', label: '企业编码', type: 'text' }
]

async function loadData(params: PageParams) {
  return getEnterprisePage(params)
}
</script>

<template>
  <YzhTable :columns="columns" :data-loader="loadData" :search-fields="searchFields" />
</template>
