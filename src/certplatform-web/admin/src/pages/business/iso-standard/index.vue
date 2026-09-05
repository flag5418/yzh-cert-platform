<script setup lang="ts">
import { YzhTable } from '@yzh-core/components/table'
import type { PageParams, SearchField, YzhTableColumn } from '@yzh-core/components/table/types'
import { getISOStandardPage } from '@share/api/iso-standard'
import type { ISOStandard } from '@share/types/cert'

const columns: YzhTableColumn<ISOStandard>[] = [
  { prop: 'standardCode', label: '标准编码', width: 150, sortable: true },
  { prop: 'standardName', label: '标准名称', minWidth: 200 },
  { prop: 'standardDesc', label: '描述', minWidth: 200 },
  { prop: 'orgName', label: '机构', width: 150 },
  { prop: 'createDate', label: '创建时间', width: 180 }
]

const searchFields: SearchField[] = [
  { prop: 'standardCode', label: '标准编码', type: 'text' },
  { prop: 'standardName', label: '标准名称', type: 'text' }
]

async function loadData(params: PageParams) {
  return getISOStandardPage(params)
}
</script>

<template>
  <YzhTable :columns="columns" :data-loader="loadData" :search-fields="searchFields" />
</template>
