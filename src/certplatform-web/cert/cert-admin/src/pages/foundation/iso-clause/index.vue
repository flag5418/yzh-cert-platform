<script setup lang="ts">
import { YzhTable } from '@yzh-core'
import type { PageParams, SearchField, YzhTableColumn } from '@yzh-core/types'
import { getISOClausePage } from '@share/api/cert/iso-clause'
import type { ISOClause } from '@share/types/cert'

const columns: YzhTableColumn<ISOClause>[] = [
  { prop: 'clauseNo', label: '条款编号', width: 150 },
  { prop: 'clauseName', label: '条款名称', minWidth: 200 },
  { prop: 'standardId', label: '标准ID', width: 100 }
]

const searchFields: SearchField[] = [
  { prop: 'clauseNo', label: '条款编号', type: 'text' }
]

async function loadData(params: PageParams) {
  return getISOClausePage(params)
}
</script>

<template>
  <YzhTable :columns="columns" :data-loader="loadData" :search-fields="searchFields" />
</template>
